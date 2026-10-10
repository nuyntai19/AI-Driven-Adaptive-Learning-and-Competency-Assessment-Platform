using System.Text.Json;
using System.Text.Json.Nodes;
using EduTwin.API.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.Contracts.AssessmentAndReasoning;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed class GeminiVisualEvidenceInspectionTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    [Fact]
    public async Task InspectionOmitsAnswersReferenceQuestionTextAndQuestionImages()
    {
        var request = Drawing();
        request = request with { Question = request.Question with { QuestionText = "SECRET_QUESTION", CorrectAnswer = "SECRET_ANSWER",
            Solution = "SECRET_REFERENCE", ImageParts = [new([9], "image/png")] },
            StudentSubmission = request.StudentSubmission with { ReasoningText = "SECRET_STUDENT_CLAIM" } };
        var client = new ClientFake();
        var result = await new GeminiVisualEvidenceInspector(client, Options()).InspectAsync([new("a", request)], default);
        Assert.Null(result["a"].Error);
        var call = Assert.Single(client.Calls);
        Assert.Equal("vision-model", call.Model); Assert.Equal(8192, call.Config.MaxOutputTokens);
        Assert.Equal(new byte[] { 1 }, Assert.Single(call.Images).Data);
        foreach (var secret in new[] { "SECRET_QUESTION", "SECRET_ANSWER", "SECRET_REFERENCE", "SECRET_STUDENT_CLAIM" }) Assert.DoesNotContain(secret, call.Prompt);
        Assert.Contains("NOT a grader", call.Prompt);
        Assert.Contains("status and observation MUST agree", call.Prompt);
    }

    [Fact]
    public async Task GraderCannotReplaceLockedMissingEvidenceAndCalculationKeepsPoints()
    {
        var client = new ClientFake { GraderEvidence = Evidence().Select(e => e with { Status = "Present" }).ToArray() };
        var result = (await Executor(client).ExecuteAsync([new("a", Drawing())], default))["a"];
        Assert.Null(result.Error); Assert.Equal(8.5m, result.Response!.SuggestedScore);
        Assert.Equal("Missing", result.Response.VisualEvidence!.Single(e => e.CriterionId == "symbols" && e.RequirementIndex == 2).Status);
        Assert.Equal(4m, result.Response.SuggestedRubricScores.Single(s => s.CriterionId == "calculation").AwardedScore);
        Assert.Equal(new[] { "vision-model", "grading-model" }, client.Calls.Select(c => c.Model));
        Assert.Contains("SERVER-LOCKED INDEPENDENT VISUAL EVIDENCE", client.Calls[1].Prompt);
    }

    [Fact]
    public async Task FullMarksDespiteMissingLockedEvidenceAreRejected()
    {
        var result = (await Executor(new ClientFake { SymbolPoints = 2 }).ExecuteAsync([new("a", Drawing())], default))["a"];
        Assert.Null(result.Response);
        Assert.Equal(AIResponseValidationRule.Rubric, Assert.IsType<AIAnalysisValidationException>(result.Error).ValidationRule);
    }

    [Fact]
    public async Task NonvisualPartialScoreWithoutSpecificDeductionIsRejected()
    {
        var result = (await Executor(new ClientFake { CalculationPoints = 3 }).ExecuteAsync([new("a", Drawing())], default))["a"];
        Assert.Null(result.Response);
        Assert.Equal("UnexplainedCriterionDeduction", Assert.IsType<AIAnalysisValidationException>(result.Error).DiagnosticDetail);
    }

    [Fact]
    public async Task VisualDefectCannotJustifyANonvisualDeduction()
    {
        var client = new ClientFake { CalculationPoints = 3, ExtraDeductions = [
            new("calculation", "Visual", "Hình thiếu ký hiệu.", "Không có nhãn M trên ảnh.")] };
        var result = (await Executor(client).ExecuteAsync([new("a", Drawing())], default))["a"];
        Assert.Null(result.Response);
        Assert.Equal("InvalidCriterionDeductionAudit", Assert.IsType<AIAnalysisValidationException>(result.Error).DiagnosticDetail);
    }

    [Fact]
    public async Task AConcreteNonvisualDeductionRemainsVisibleInCriterionComment()
    {
        var client = new ClientFake { CalculationPoints = 3, ExtraDeductions = [
            new("calculation", "Nonvisual", "Chưa nêu đơn vị của kết quả.", "Kết luận chỉ ghi số, chưa có đơn vị độ dài.")] };
        var result = (await Executor(client).ExecuteAsync([new("a", Drawing())], default))["a"];
        Assert.Null(result.Error);
        Assert.Contains("Phần chưa đạt: Chưa nêu đơn vị", result.Response!.SuggestedRubricScores.Single(s => s.CriterionId == "calculation").Comment);
    }

    [Fact]
    public void DeductionSchemaIsOptInAndOrdinaryProviderContractIsUnchanged()
    {
        var schemas = new GeminiResponseJsonSchema();
        Assert.Null(schemas.CreateSchema()["properties"]!["criterionDeductions"]);
        Assert.NotNull(schemas.CreateSchema(true)["properties"]!["criterionDeductions"]);
    }

    [Fact]
    public async Task OrdinaryQuestionsUseOnlyTheExistingGradingCall()
    {
        var client = new ClientFake();
        var result = await Executor(client).ExecuteAsync([new("plain", ReasoningMicroBatchTests.Request(null))], default);
        Assert.NotNull(result["plain"].Response);
        Assert.Equal("grading-model", Assert.Single(client.Calls).Model);
    }

    [Fact]
    public async Task MultipleVisualItemsShareOneInspectionAndRemainMappedByIds()
    {
        var client = new ClientFake();
        var results = await Executor(client).ExecuteAsync([new("a", Drawing()), new("b", Drawing()), new("plain", ReasoningMicroBatchTests.Request(null))], default);
        Assert.All(results.Values, r => Assert.Null(r.Error));
        Assert.Equal(2, client.Calls.Count); Assert.Equal(2, client.Calls[0].Images.Count);
        Assert.Contains("\"studentImageIndexes\":[1]", client.Calls[0].Prompt);
        Assert.Contains("\"studentImageIndexes\":[2]", client.Calls[0].Prompt);
        Assert.DoesNotContain("plain", client.Calls[0].Prompt);
        Assert.Contains("\"itemId\":\"a\"", client.Calls[1].Prompt);
        Assert.Contains("\"itemId\":\"b\"", client.Calls[1].Prompt);
    }

    [Fact]
    public async Task InspectionFailureDoesNotDiscardOrdinaryItemsInSameBatch()
    {
        var client = new ClientFake { InspectionFails = true };
        var results = await Executor(client).ExecuteAsync([new("a", Drawing()), new("plain", ReasoningMicroBatchTests.Request(null))], default);
        Assert.NotNull(results["a"].Error); Assert.Null(results["plain"].Error);
        Assert.Equal(2, client.Calls.Count); Assert.DoesNotContain("SERVER-LOCKED", client.Calls[1].Prompt);
    }

    [Fact]
    public async Task NoImageNeedsNoInspectionCallButStillGradesCalculation()
    {
        var request = Drawing(); request = request with { StudentSubmission = request.StudentSubmission with { ImageParts = [] } };
        var client = new ClientFake { NoImage = true };
        var result = (await Executor(client).ExecuteAsync([new("a", request)], default))["a"];
        Assert.Null(result.Error); Assert.Equal(4m, result.Response!.SuggestedScore);
        Assert.All(result.Response.VisualEvidence!, e => Assert.Equal("Missing", e.Status));
        Assert.Equal("grading-model", Assert.Single(client.Calls).Model);
    }

    [Theory]
    [InlineData("wrong-image")]
    [InlineData("duplicate")]
    [InlineData("unknown-criterion")]
    [InlineData("decimal-index")]
    public void InvalidInspectionCannotBecomeLockedEvidence(string corruption)
    {
        var root = JsonNode.Parse(InspectionJson(["a"]))!;
        var evidence = root["results"]![0]!["visualEvidence"]!.AsArray();
        if (corruption == "wrong-image") evidence[0]!["studentImageIndex"] = 2;
        if (corruption == "duplicate") evidence[0] = evidence[1]!.DeepClone();
        if (corruption == "unknown-criterion") evidence[0]!["criterionId"] = "invented";
        if (corruption == "decimal-index") evidence[0]!["requirementIndex"] = JsonNode.Parse("1.0");
        var result = GeminiVisualEvidenceInspector.Parse(root.ToJsonString(), [new("a", Drawing())]);
        Assert.NotNull(result["a"].Error); Assert.Null(result["a"].Evidence);
    }

    [Fact]
    public void LockedObservationsAreServerOwnedAndProfileChangesWithVisionModel()
    {
        var request = Drawing();
        Assert.Equal(JsonSerializer.Serialize(request, Json), JsonSerializer.Serialize(request with { VerifiedVisualEvidence = Evidence() }, Json));
        var options = Options(); var profile = new AIGradingOptions().Profile(options);
        Assert.Contains("locked-visual-v7", profile); Assert.True(profile.Length < 200);
        options.VisualEvidenceModel = "different-vision-model";
        Assert.NotEqual(profile, new AIGradingOptions().Profile(options));
        options.VisualEvidenceProfileApproved = false;
        Assert.Throws<GeminiAdapterException>(() => options.Validate());
    }

    private static GeminiOptions Options() => new() { ApiKey = "offline-key", Model = "grading-model", VisualEvidenceModel = "vision-model", VisualEvidenceProfileApproved = true };
    private static ReasoningBatchExecutor Executor(ClientFake client) => new(Microsoft.Extensions.Options.Options.Create(Options()),
        Microsoft.Extensions.Options.Options.Create(new AIGradingOptions()), client, null!, new(), new(), new StrictAIAnalysisResponseParser(new AnalyzeReasoningResponseValidator()));
    private static AnalyzeReasoningRequest Drawing()
    {
        var original = ReasoningMicroBatchTests.Request(null);
        return original with { Question = original.Question with { GradingCriteria = original.Question.GradingCriteria with { Criteria = [
            new("drawing", "Hình dựng", "Hình tam giác", 4) { VisualRequirements = ["Có tam giác"] },
            new("symbols", "Ký hiệu", "Nhãn và dấu", 2) { VisualRequirements = ["Có A/B/C", "Có M", "Có dấu góc vuông", "Có dấu hai đoạn bằng nhau"] },
            new("calculation", "Tính toán", "Tính đúng", 4)] } },
            StudentSubmission = original.StudentSubmission with { ImageParts = [new([1], "image/png")] } };
    }
    private static RubricVisualEvidence[] Evidence() => [new("drawing", 1, "Present", 1, "Có hình tam giác trên ảnh."),
        new("symbols", 1, "Present", 1, "Có nhãn A, B, C ở các đỉnh."), new("symbols", 2, "Missing", 1, "Không có nhãn M trên ảnh."),
        new("symbols", 3, "Missing", 1, "Không thấy ô đánh dấu góc vuông."), new("symbols", 4, "Missing", 1, "Không có dấu hai đoạn bằng nhau.")];
    private static string InspectionJson(IEnumerable<string> ids) => JsonSerializer.Serialize(new { results = ids.Reverse().Select(id => new { itemId = id, visualEvidence = Evidence() }) }, Json);
    private sealed record Call(string Model, string Prompt, IReadOnlyList<GeminiInlineImagePart> Images, GenerateContentConfig Config);
    private sealed class ClientFake : IGeminiGenerateContentClient
    {
        public List<Call> Calls { get; } = [];
        public RubricVisualEvidence[] GraderEvidence { get; init; } = [];
        public decimal SymbolPoints { get; init; } = 0.5m;
        public bool InspectionFails { get; init; }
        public bool NoImage { get; init; }
        public decimal CalculationPoints { get; init; } = 4;
        public AICriterionDeduction[]? ExtraDeductions { get; init; }
        public Task<GeminiGenerateContentResult> GenerateContentAsync(string model, string prompt, GenerateContentConfig config, CancellationToken token) => GenerateContentWithImagesAsync(model, prompt, [], config, token);
        public Task<GeminiGenerateContentResult> GenerateContentWithImagesAsync(string model, string prompt, IReadOnlyList<GeminiInlineImagePart> images, GenerateContentConfig config, CancellationToken token)
        {
            Calls.Add(new(model, prompt, images, config)); token.ThrowIfCancellationRequested();
            if (model == "vision-model")
            {
                if (InspectionFails) throw new HttpRequestException("offline synthetic outage");
                var data = JsonNode.Parse(prompt.Split("INPUT_DATA_BEGIN\n")[1].Split("\nINPUT_DATA_END")[0])!.AsArray();
                return Task.FromResult(new GeminiGenerateContentResult(InspectionJson(data.Select(x => x!["itemId"]!.GetValue<string>())), 1, 1, 2));
            }
            var batch = prompt.Contains("BATCH_INPUT_JSON_BEGIN");
            string raw;
            if (batch)
            {
                var input = JsonNode.Parse(prompt.Split("BATCH_INPUT_JSON_BEGIN\n")[1].Split("\nBATCH_INPUT_JSON_END")[0])!.AsArray();
                raw = JsonSerializer.Serialize(new { results = input.Select(i => new { itemId = i!["itemId"]!.GetValue<string>(),
                    analysis = Grade(i["input"]!["question"]!["gradingCriteria"]!["criteria"]!.AsArray().Count > 0) }) }, Json);
            }
            else raw = Grade(prompt.Contains("\"criterionId\":\"drawing\"" )).ToJsonString();
            return Task.FromResult(new GeminiGenerateContentResult(raw, 1, 1, 2));
        }
        private JsonObject Grade(bool drawing)
        {
            var value = JsonSerializer.SerializeToNode(ReasoningMicroBatchTests.ResponseJson("Lập luận và tính toán hợp lệ."), Json)!.AsObject();
            if (!drawing) return value;
            value["suggestedScore"] = 10; value["usesAlternativeMethod"] = false; value["reasoningIssues"] = new JsonArray();
            value["suggestedRubricScores"] = JsonSerializer.SerializeToNode(new[] {
                new { criterionId = "drawing", awardedScore = NoImage ? 0 : 4m, comment = "Chấm phần hình thể hiện được." },
                new { criterionId = "symbols", awardedScore = NoImage ? 0 : SymbolPoints, comment = "Chấm đúng phần ký hiệu có trong ảnh." },
                new { criterionId = "calculation", awardedScore = CalculationPoints, comment = "Lập luận và tính toán đúng." } }, Json);
            var deductions = new List<AICriterionDeduction> {
                new("symbols", "Visual", "Ký hiệu hình học chưa đầy đủ.", "Ảnh thiếu tên M và các dấu hình học.") };
            if (NoImage) deductions.Add(new("drawing", "Visual", "Chưa có hình dựng để kiểm chứng.", "Không có ảnh học sinh đính kèm."));
            if (ExtraDeductions is not null) deductions.AddRange(ExtraDeductions);
            value["criterionDeductions"] = JsonSerializer.SerializeToNode(deductions, Json);
            value["visualEvidence"] = JsonSerializer.SerializeToNode(GraderEvidence, Json);
            return value;
        }
    }
}
