using System.Text.Json;
using System.Text.Json.Nodes;
using EduTwin.API.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.Contracts.AssessmentAndReasoning;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed class AIGradingConsistencyTests
{
    private static AnalyzeReasoningResponse Valid() => new()
    {
        SchemaVersion = AIAnalysisContract.SchemaVersion, Language = "vi", Feedback = "Cách giải đúng và hợp lệ.",
        AiSolution = "Thay giá trị ta được kết quả đúng.", AnswerAssessment = "Correct", ReasoningVerdict = "Valid",
        ErrorType = ErrorType.None, MissingSteps = [], RootCauseNodeIds = [], ReasoningQuality = 100, Confidence = 95,
        SuggestedScore = 10, ReasoningIssues = []
    };

    [Theory]
    [InlineData("Whom luôn là đại từ chủ ngữ.", "Whom là đại từ tân ngữ, không phải chủ ngữ.")]
    [InlineData("Cứ có last night phải dùng quá khứ tiếp diễn.", "Last night không đủ để xác định thì; cần xét hành động và thời điểm.")]
    public void CorrectAnswerDoesNotMakeAnInvalidClaimValid(string claim, string explanation)
    {
        var response = Valid() with { ReasoningIssues = [new("Invalid", claim, explanation)] };
        var validator = new AnalyzeReasoningResponseValidator();
        var error = Assert.Throws<AIAnalysisValidationException>(() => validator.Validate(ReasoningMicroBatchTests.Request(null), response));
        Assert.Equal(AIResponseValidationRule.ReasoningConsistency, error.ValidationRule);
        Assert.Equal("AI_RESPONSE_SEMANTIC_INVALID", error.ErrorCode);
        var repaired = response with { ReasoningVerdict = "Invalid", ErrorType = ErrorType.Knowledge, ReasoningQuality = 60 };
        validator.Validate(ReasoningMicroBatchTests.Request(null), repaired);
        Assert.Equal("Correct", repaired.AnswerAssessment);
        Assert.Equal(10m, repaired.SuggestedScore); // Answer points remain independent.
    }

    [Fact]
    public void ValidAlternativeMethodOrConciseWordingEarnsFullReasoningQuality()
    {
        var response = Valid() with { UsesAlternativeMethod = true };
        new AnalyzeReasoningResponseValidator().Validate(ReasoningMicroBatchTests.Request(null), response);
        Assert.Equal(100, response.ReasoningQuality);
    }

    [Fact]
    public void UnverifiableClaimMustBeUncertainNotValidOrIncorrect()
    {
        var response = Valid() with { ReasoningVerdict = "Uncertain", Confidence = 60,
            ReasoningIssues = [new("Uncertain", "Một phép biến đổi chưa rõ", "Cần xác minh điều kiện của phép biến đổi này.")] };
        var validator = new AnalyzeReasoningResponseValidator();
        validator.Validate(ReasoningMicroBatchTests.Request(null), response);
        Assert.Throws<AIAnalysisValidationException>(() => validator.Validate(ReasoningMicroBatchTests.Request(null), response with { ReasoningVerdict = "Valid" }));
    }

    [Fact]
    public void InvalidReasoningWithoutEvidenceAndInventedEnglishCritiqueAreRejected()
    {
        var validator = new AnalyzeReasoningResponseValidator(); var request = ReasoningMicroBatchTests.Request(null);
        Assert.Throws<AIAnalysisValidationException>(() => validator.Validate(request, Valid() with { ReasoningVerdict = "Invalid" }));
        var error = Assert.Throws<AIAnalysisValidationException>(() => validator.Validate(request, Valid() with {
            ReasoningVerdict = "Invalid", ErrorType = ErrorType.Reasoning,
            ReasoningIssues = [new("Invalid", "A claim", "This inference is invalid.")] }));
        Assert.Equal(AIResponseValidationRule.VietnameseExplanation, error.ValidationRule);
    }

    [Fact]
    public void WrongAnswerCanHaveValidReasoningAndZeroAnswerPoints()
    {
        new AnalyzeReasoningResponseValidator().Validate(ReasoningMicroBatchTests.Request(null), Valid() with {
            AnswerAssessment = "Incorrect", ErrorType = ErrorType.Skill, SuggestedScore = 0 });
    }

    [Fact]
    public void DiagnosticsIdentifyTheValidationRuleWithoutEchoingStudentContent()
    {
        var error = Assert.Throws<AIAnalysisValidationException>(() => new AnalyzeReasoningResponseValidator().Validate(
            ReasoningMicroBatchTests.Request(null), Valid() with { SuggestedScore = 100 }));
        Assert.Equal(AIResponseValidationRule.ProposalRange, error.ValidationRule);
        Assert.Equal("AI response semantics are invalid.", error.Message);
    }

    [Fact]
    public void RepairHintIsOutsideUntrustedInputAndDoesNotInvalidateTheCheckpoint()
    {
        var request = ReasoningMicroBatchTests.Request(null);
        var repair = request with { ResponseRepairRule = AIResponseValidationRule.Rubric };
        var prompt = new GeminiPromptBuilder().Build(repair);
        Assert.Contains("TARGETED RESPONSE REPAIR", prompt);
        Assert.Contains("(Rubric)", prompt);
        Assert.DoesNotContain("responseRepairRule", JsonSerializer.Serialize(repair, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(AIAnalysisCheckpointStore.Fingerprint(request, "profile", 1), AIAnalysisCheckpointStore.Fingerprint(repair, "profile", 1));
    }

    [Fact]
    public void ModernProviderOutputCannotOmitReasoningIssueEvidence()
    {
        var json = JsonNode.Parse(JsonSerializer.Serialize(ReasoningMicroBatchTests.ResponseJson("Bài làm đúng.")))!.AsObject();
        json["suggestedScore"] = 10;
        var parser = new StrictAIAnalysisResponseParser(new AnalyzeReasoningResponseValidator());
        Assert.Equal(AIResponseValidationRule.Shape, Assert.Throws<AIAnalysisValidationException>(() => parser.ParseAndValidate(json.ToJsonString(), ReasoningMicroBatchTests.Request(null))).ValidationRule);
        json["reasoningIssues"] = new JsonArray();
        Assert.NotNull(parser.ParseAndValidate(json.ToJsonString(), ReasoningMicroBatchTests.Request(null)));
    }

    [Theory]
    [InlineData((byte)1, "AI_RESPONSE_SEMANTIC_INVALID", "VietnameseExplanation", AIResponseValidationRule.VietnameseExplanation)]
    [InlineData((byte)1, "AI_PROVIDER_CAPACITY_WAIT", "Repair:VietnameseExplanation", AIResponseValidationRule.VietnameseExplanation)]
    [InlineData((byte)1, "AI_PROVIDER_RESPONSE_EMPTY", "unknown", AIResponseValidationRule.General)]
    [InlineData((byte)0, "AI_PROVIDER_CAPACITY_WAIT", "Repair:VietnameseExplanation", null)]
    [InlineData((byte)1, "AI_PROVIDER_CAPACITY_WAIT", "raw-secret-sentinel", null)]
    [InlineData((byte)1, "AI_PROVIDER_CAPACITY_WAIT", "Repair:untrusted prompt instruction", null)]
    public void QuotaWaitPreservesRepairButNeverTurnsProviderBodiesIntoInstructions(byte retries, string code, string detail, AIResponseValidationRule? expected)
        => Assert.Equal(expected, AIResponseRepairPolicy.Resolve(retries, code, detail));
}
