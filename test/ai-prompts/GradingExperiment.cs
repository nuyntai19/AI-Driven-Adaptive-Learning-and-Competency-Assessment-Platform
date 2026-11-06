using System.Text.Json;
using EduTwin.API.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;
using Microsoft.Extensions.Options;

namespace EduTwin.PromptEvaluation;

public static class RequestBridge
{
    public static GradingCriteria Criteria(AuthoredCriteria input) => new()
    {
        RequiredIdeas = input.RequiredIdeas.ToList(), CommonErrors = input.CommonErrors.ToList(), ScoringNotes = input.ScoringNotes,
        Criteria = input.Criteria.Select(c => new RubricCriterion { CriterionId = c.CriterionId, Title = c.Title,
            Description = c.Description, MaxScore = c.MaxScore, VisualRequirements = c.VisualRequirements.ToList() }).ToList()
    };

    public static AnalyzeReasoningRequest Build(UserInput input)
    {
        FixtureReader.Validate(input);
        var teacher = input.Teacher; var student = input.Student;
        // Synthetic identities only satisfy canonical factory invariants; no database is opened.
        var center = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var subject = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var criteria = Criteria(teacher.GradingCriteria);
        var question = new Question
        {
            QuestionId = 1, CenterId = center, SubjectId = subject, PrimaryTopicNodeId = 1,
            QuestionType = teacher.QuestionType, AnswerEvaluationMode = teacher.AnswerEvaluationMode,
            QuestionText = teacher.QuestionText, CorrectAnswer = teacher.CorrectAnswer,
            Solution = teacher.Solution, ExpectedReasoning = teacher.ExpectedReasoning,
            LanguageCode = teacher.ContentLanguage, MaxScore = teacher.MaxScore, GradingCriteria = criteria
        };
        var options = teacher.Options.Select((o, i) => new QuestionOption
        {
            OptionId = (ulong)i + 1, CenterId = center, QuestionId = 1,
            OptionLabel = o.Label, OptionText = o.Text, IsCorrect = o.IsCorrect, OrderIndex = (uint)i
        }).ToArray();
        var graders = new PreliminaryGraderFactory(new(), new(), new());
        var grade = graders.GetGrader(teacher.QuestionType).Grade(student.FinalAnswer, teacher.CorrectAnswer,
            new QuestionGradingContext { EvaluationMode = teacher.AnswerEvaluationMode,
                MaxScore = teacher.MaxScore, Criteria = criteria, Options = options });
        var attempt = new Attempt
        {
            AttemptId = 1, CenterId = center, QuestionId = 1, FinalAnswer = student.FinalAnswer,
            ReasoningText = student.ReasoningText, AnswerDisplayLatex = student.AnswerDisplayLatex,
            ReasoningLanguage = teacher.ContentLanguage, IsCorrect = grade.IsCorrect,
            AwardedScore = grade.Score, TimeSpentSeconds = student.TimeSpentSeconds,
            Confidence = student.Confidence, AnswerChanges = student.AnswerChanges
        };
        var nodes = teacher.KnowledgeTopics.Select((name, i) => new KnowledgeNode
        {
            NodeId = (ulong)i + 1, CenterId = center, SubjectId = subject, NodeName = name, IsActive = true
        }).ToArray();
        IReadOnlyList<AnalyzeReasoningImagePart> Images(PngInput? image) => image is null ? [] : [new(FixtureReader.Image(image), "image/png")];
        return new AIAnalysisRequestFactory().Create(attempt, question, nodes,
            Images(student.ScratchpadImage), options, Images(teacher.ProblemImage));
    }
}

public sealed record ExportView(object Parameters, object Prompt);
public sealed record EvaluationEntry(object Parameters, object Prompt, JsonElement Output);

public static class UserOnlyExport
{
    // Explicit projection is intentional: adding an internal bridge field cannot
    // accidentally expand reports or expose the canonical system prompt.
    public static ExportView Input(UserInput input) => new(
        new
        {
            teacher = new
            {
                input.Teacher.QuestionType, input.Teacher.AnswerEvaluationMode, input.Teacher.ContentLanguage,
                input.Teacher.CorrectAnswer, input.Teacher.Solution, input.Teacher.ExpectedReasoning,
                input.Teacher.MaxScore, input.Teacher.KnowledgeTopics, input.Teacher.Options,
                input.Teacher.GradingCriteria
            },
            student = new { input.Student.TimeSpentSeconds, input.Student.Confidence, input.Student.AnswerChanges }
        },
        new
        {
            teacher = new { input.Teacher.QuestionText, input.Teacher.ProblemImage },
            student = new { input.Student.FinalAnswer, input.Student.AnswerDisplayLatex,
                input.Student.ReasoningText, input.Student.ScratchpadImage }
        });

    public static EvaluationEntry WithOutput(UserInput input, string actualOutput)
    {
        var view = Input(input);
        JsonElement output;
        try { using var doc = JsonDocument.Parse(actualOutput); output = doc.RootElement.Clone(); }
        catch (JsonException) { output = JsonSerializer.SerializeToElement(actualOutput); }
        return new(view.Parameters, view.Prompt, output);
    }
}

public interface IGradingTransport
{
    Task<string> GenerateAsync(AnalyzeReasoningRequest request, CancellationToken token);
    bool LastResponseComplete => true;
}

public sealed class GeminiTransport : IGradingTransport, IDisposable
{
    private readonly string model;
    private readonly GoogleGenAIGenerateContentClient client;

    public GeminiTransport(string apiKey, string model, int timeoutSeconds)
    {
        this.model = model;
        var options = new GeminiOptions { ApiKey = apiKey, Model = model,
            Timeout = TimeSpan.FromSeconds(timeoutSeconds), MaxConcurrentRequests = 1 };
        options.Validate();
        client = new(Options.Create(options));
    }

    public async Task<string> GenerateAsync(AnalyzeReasoningRequest request, CancellationToken token)
    {
        var prompt = new GeminiPromptBuilder().Build(request);
        var config = new GeminiResponseJsonSchema().CreateGenerateContentConfig();
        if (model.StartsWith("gemini-3", StringComparison.Ordinal)) config.Temperature = 1;
        var images = request.AllImages().Select(i => new GeminiInlineImagePart(i.Data, i.MimeType)).ToArray();
        var response = images.Length == 0
            ? await client.GenerateContentAsync(model, prompt, config, token)
            : await client.GenerateContentWithImagesAsync(model, prompt, images, config, token);
        // Preserve provider output before validation; do not replace it with fallback or a mock.
        return response.ResponseText ?? "";
    }

    public void Dispose() => client.Dispose();
}

// The production Groq transport requires a SQL quota lease. This independent
// experiment reuses the exact provider request shape from GroqGenerateContentClient,
// while sharing the real request factory, prompt builder and response validator.
// No endpoint override is accepted: API keys can only reach the named provider.
public sealed class GroqTransport(string apiKey, string model, int timeoutSeconds, HttpMessageHandler? handler = null) : IGradingTransport, IDisposable
{
    private readonly HttpClient client = handler is null ? new() { Timeout = TimeSpan.FromSeconds(timeoutSeconds) }
        : new(handler) { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
    public bool LastResponseComplete { get; private set; }

    public async Task<string> GenerateAsync(AnalyzeReasoningRequest request, CancellationToken token)
    {
        LastResponseComplete = false;
        var content = new List<object> { new { type = "text", text = new GeminiPromptBuilder().Build(request) } };
        foreach (var image in request.AllImages())
            content.Add(new { type = "image_url", image_url = new { url = "data:image/png;base64," + Convert.ToBase64String(image.Data) } });
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        message.Content = System.Net.Http.Json.JsonContent.Create(new
        {
            model, messages = new[] { new { role = "user", content } }, temperature = 0,
            max_completion_tokens = 2048, response_format = new { type = "json_object" }
        });
        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode) throw new IOException("Provider request failed.");
        await response.Content.LoadIntoBufferAsync(1024 * 1024, token);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var choice = document.RootElement.GetProperty("choices")[0];
        var actual = choice.GetProperty("message").GetProperty("content").GetString() ?? "";
        LastResponseComplete = choice.GetProperty("finish_reason").GetString() == "stop";
        return actual;
    }

    public void Dispose() => client.Dispose();
}

public sealed record ExpectedObservation
{
    public string? AnswerAssessment { get; init; }
    public string? ReasoningVerdict { get; init; }
    public decimal? MinimumSuggestedScore { get; init; }
    public decimal? MaximumSuggestedScore { get; init; }
    public int? ReasoningQuality { get; init; }
    public Dictionary<string, decimal>? RubricScores { get; init; }
    public int? MissingVisualRequirementCount { get; init; }

    public bool Matches(AnalyzeReasoningResponse output) =>
        (AnswerAssessment is null || output.AnswerAssessment == AnswerAssessment) &&
        (ReasoningVerdict is null || output.ReasoningVerdict == ReasoningVerdict) &&
        (!ReasoningQuality.HasValue || output.ReasoningQuality == ReasoningQuality) &&
        (RubricScores is null || RubricScores.All(pair => output.SuggestedRubricScores.FirstOrDefault(c => c.CriterionId == pair.Key)?.AwardedScore == pair.Value)) &&
        (!MissingVisualRequirementCount.HasValue || output.VisualEvidence?.Count(e => e.Status == "Missing") == MissingVisualRequirementCount) &&
        (!MinimumSuggestedScore.HasValue || output.SuggestedScore >= MinimumSuggestedScore) &&
        (!MaximumSuggestedScore.HasValue || output.SuggestedScore <= MaximumSuggestedScore);
}

public sealed record EvaluationResult(EvaluationEntry Entry, bool ContractValid, bool ObservationMatches);

public static class GradingExperiment
{
    public static async Task<EvaluationResult> RunAsync(UserInput input, ExpectedObservation? expected,
        IGradingTransport transport, CancellationToken token)
    {
        var request = RequestBridge.Build(input);
        var actual = await transport.GenerateAsync(request, token);
        var entry = UserOnlyExport.WithOutput(input, actual);
        if (!transport.LastResponseComplete) return new(entry, false, false);
        try
        {
            // The production parser intentionally accepts historical/legacy analyses.
            // New experiments must also satisfy the CURRENT provider's required fields.
            using var document = JsonDocument.Parse(actual);
            FixtureReader.NoDuplicates(document.RootElement);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return new(entry, false, false);
            var required = new GeminiResponseJsonSchema().CreateSchema()["required"]!.AsArray()
                .Select(n => n!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
            if (!document.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal).SetEquals(required))
                return new(entry, false, false);
            var response = new StrictAIAnalysisResponseParser(new AnalyzeReasoningResponseValidator())
                .ParseAndValidate(actual, request);
            return new(entry, true, expected?.Matches(response) ?? true);
        }
        catch (Exception e) when (e is AIAnalysisValidationException or JsonException or InvalidDataException)
        { return new(entry, false, false); }
    }
}
