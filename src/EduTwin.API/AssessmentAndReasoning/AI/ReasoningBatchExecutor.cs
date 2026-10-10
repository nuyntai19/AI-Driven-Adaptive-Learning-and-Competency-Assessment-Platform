using System.Text.Json;
using System.Text.Json.Nodes;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using Microsoft.Extensions.Options;

namespace EduTwin.API.AssessmentAndReasoning.AI;

public sealed record ReasoningBatchItem(string ItemId, AnalyzeReasoningRequest Request);
public sealed record ReasoningBatchResult(AnalyzeReasoningResponse? Response, Exception? Error);

public interface IReasoningBatchExecutor
{
    string ProfileVersion { get; }
    string ProviderName { get; }
    string ModelName { get; }
    Task<IReadOnlyDictionary<string, ReasoningBatchResult>> ExecuteAsync(IReadOnlyList<ReasoningBatchItem> items, CancellationToken token);
}

public sealed class ReasoningBatchExecutor(
    IOptions<GeminiOptions> geminiOptions, IOptions<AIGradingOptions> gradingOptions,
    IGeminiGenerateContentClient gemini, GroqGenerateContentClient groq,
    GeminiPromptBuilder prompts, GeminiResponseJsonSchema schemas, IAIAnalysisResponseParser parser,
    ILogger<ReasoningBatchExecutor>? logger = null)
    : IReasoningBatchExecutor
{
    private readonly GeminiOptions _gemini = geminiOptions.Value;
    private readonly AIGradingOptions _options = gradingOptions.Value;
    public string ProfileVersion => _options.Profile(_gemini);
    public string ProviderName => _options.Provider;
    public string ModelName => _options.Model(_gemini);

    public async Task<IReadOnlyDictionary<string, ReasoningBatchResult>> ExecuteAsync(IReadOnlyList<ReasoningBatchItem> items, CancellationToken token)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var outcome = "Failed";
        try
        {
            var results = await ExecuteCoreAsync(items, token);
            outcome = results.Values.All(x => x.Response is not null) ? "Succeeded" : "Partial";
            return results;
        }
        catch (AIAnalysisDeferredException) { outcome = "Deferred"; throw; }
        catch (OperationCanceledException) { outcome = "Canceled"; throw; }
        finally
        {
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            AIProcessingMetrics.Duration.Record(elapsed, new KeyValuePair<string, object?>("stage", "provider_batch"));
            logger?.LogInformation("AI batch completed for {Provider}, model {Model}, {QuestionCount} questions in {LatencyMs} ms, outcome {Outcome}.",
                _options.Provider, _options.Model(_gemini), items.Count, elapsed, outcome);
        }
    }

    private async Task<IReadOnlyDictionary<string, ReasoningBatchResult>> ExecuteCoreAsync(IReadOnlyList<ReasoningBatchItem> items, CancellationToken token)
    {
        _options.Validate(_gemini);
        if (items.Count is < 1 or > 5 || items.Select(x => x.ItemId).Distinct(StringComparer.Ordinal).Count() != items.Count)
            throw GeminiAdapterException.ConfigurationInvalid();
        var originalImageCount = items.Sum(i => i.Request.AllImages().Count());
        if (items.Count > 1 && originalImageCount > _options.MaxImages || _options.Provider == "Groq" && originalImageCount > 3)
            throw GeminiAdapterException.ConfigurationInvalid();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(_gemini.Timeout); // One deadline spans inspection + grading, not two unbounded waits.
        var combined = new Dictionary<string, ReasoningBatchResult>(StringComparer.Ordinal);
        IReadOnlyDictionary<string, VisualInspectionResult> inspections;
        try { inspections = await new GeminiVisualEvidenceInspector(gemini, _gemini, logger).InspectAsync(items, timeout.Token); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw GeminiAdapterException.Timeout(); }
        var gradingItems = new List<ReasoningBatchItem>();
        foreach (var item in items)
        {
            if (inspections.TryGetValue(item.ItemId, out var inspection))
            {
                if (inspection.Error is not null) { combined.Add(item.ItemId, new(null, inspection.Error)); continue; }
                gradingItems.Add(item with { Request = item.Request with { VerifiedVisualEvidence = inspection.Evidence } });
            }
            else gradingItems.Add(item);
        }
        if (gradingItems.Count == 0) return combined;
        items = gradingItems;
        var images = items.SelectMany(x => x.Request.AllImages())
            .Select(x => new GeminiInlineImagePart(x.Data, x.MimeType)).ToArray();
        var imageIndex = 0;
        foreach (var item in items)
        {
            var itemImageIndex = 0;
            foreach (var image in item.Request.AllImages())
            {
                var role = itemImageIndex++ < item.Request.Question.ImageParts.Count ? "Question" : "Student";
                logger?.LogInformation("AI image manifest item {ItemId}, image {ImageIndex}, role {Role}, bytes {Bytes}, sha256 {Sha256}.",
                    item.ItemId, ++imageIndex, role, image.Data.Length,
                    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image.Data)));
            }
        }
        if (items.Count > 1 && images.Length > _options.MaxImages) throw GeminiAdapterException.ConfigurationInvalid();
        if (_options.Provider == "Groq" && images.Length > 3) throw GeminiAdapterException.ConfigurationInvalid();
        var prompt = items.Count == 1 ? prompts.Build(items[0].Request) : prompts.BuildBatch(items);
        var requireDeductions = items.Any(i => i.Request.VerifiedVisualEvidence is not null);
        var config = items.Count == 1 ? schemas.CreateGenerateContentConfig(requireDeductions) : schemas.CreateBatchConfig(items.Select(x => x.ItemId), requireDeductions);
        var model = _options.Model(_gemini);
        // Google recommends keeping the default temperature for Gemini 3.x reasoning.
        if (model.StartsWith("gemini-3", StringComparison.Ordinal)) config.Temperature = 1;
        GeminiGenerateContentResult raw;
        try
        {
            raw = _options.Provider == "Groq"
                ? await groq.GenerateAsync(model, prompt, images, timeout.Token)
                : await gemini.GenerateContentWithImagesAsync(model, prompt, images, config, timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw GeminiAdapterException.Timeout(); }
        AIProcessingMetrics.BatchSize.Record(items.Count, new KeyValuePair<string, object?>("provider", _options.Provider));
        if (items.Count == 1)
            combined.Add(items[0].ItemId, Parse(raw.ResponseText, items[0].Request, model));
        else foreach (var result in ParseBatch(raw.ResponseText, items, model)) combined.Add(result.Key, result.Value);
        return combined;
    }

    private ReasoningBatchResult Parse(string json, AnalyzeReasoningRequest request, string model)
    {
        try
        {
            return new(parser.ParseAndValidate(json, request), null);
        }
        catch (AIAnalysisValidationException ex)
        {
            logger?.LogWarning("AI response validation failed for {Provider}, model {Model}; code {ErrorCode}, rule {ValidationRule}, detail {DiagnosticDetail}.",
                ProviderName, model, ex.ErrorCode, ex.ValidationRule, ex.DiagnosticDetail);
            return new(null, ex);
        }
    }

    private IReadOnlyDictionary<string, ReasoningBatchResult> ParseBatch(string json, IReadOnlyList<ReasoningBatchItem> items, string model)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!ExactShape(root, ["results"]) || root.GetProperty("results").ValueKind != JsonValueKind.Array)
                throw GeminiAdapterException.ResponseInvalid();
            var requests = items.ToDictionary(x => x.ItemId, x => x.Request, StringComparer.Ordinal);
            var results = new Dictionary<string, ReasoningBatchResult>(StringComparer.Ordinal);
            foreach (var entry in root.GetProperty("results").EnumerateArray())
            {
                if (!ExactShape(entry, ["itemId", "analysis"]) || entry.GetProperty("itemId").ValueKind != JsonValueKind.String)
                    throw GeminiAdapterException.ResponseInvalid();
                var id = entry.GetProperty("itemId").GetString()!;
                if (!requests.TryGetValue(id, out var request) || results.ContainsKey(id))
                    throw GeminiAdapterException.ResponseInvalid(); // Never guess by array order.
                results.Add(id, Parse(entry.GetProperty("analysis").GetRawText(), request, model));
            }
            foreach (var item in items)
                results.TryAdd(item.ItemId, new(null, GeminiAdapterException.ResponseInvalid()));
            return results;
        }
        catch (JsonException) { throw GeminiAdapterException.ResponseInvalid(); }
    }

    private static bool ExactShape(JsonElement value, string[] fields) => value.ValueKind == JsonValueKind.Object
        && value.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).SequenceEqual(fields.Order(StringComparer.Ordinal));
}
