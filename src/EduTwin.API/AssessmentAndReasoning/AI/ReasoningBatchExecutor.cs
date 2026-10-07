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
        var images = items.SelectMany(x => x.Request.StudentSubmission.ImageParts)
            .Select(x => new GeminiInlineImagePart(x.Data, x.MimeType)).ToArray();
        if (items.Count > 1 && images.Length > _options.MaxImages) throw GeminiAdapterException.ConfigurationInvalid();
        if (_options.Provider == "Groq" && images.Length > 3) throw GeminiAdapterException.ConfigurationInvalid();
        var prompt = items.Count == 1 ? prompts.Build(items[0].Request) : prompts.BuildBatch(items);
        var config = items.Count == 1 ? schemas.CreateGenerateContentConfig() : schemas.CreateBatchConfig(items.Select(x => x.ItemId));
        var model = _options.Model(_gemini);
        // Google recommends keeping the default temperature for Gemini 3.x reasoning.
        if (model.StartsWith("gemini-3", StringComparison.Ordinal)) config.Temperature = 1;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(_gemini.Timeout);
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
            return new Dictionary<string, ReasoningBatchResult> { [items[0].ItemId] = Parse(raw.ResponseText, items[0].Request, model) };
        return ParseBatch(raw.ResponseText, items, model);
    }

    private ReasoningBatchResult Parse(string json, AnalyzeReasoningRequest request, string model)
    {
        try
        {
            return new(parser.ParseAndValidate(json, request), null);
        }
        catch (AIAnalysisValidationException ex) { return new(null, ex); }
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
