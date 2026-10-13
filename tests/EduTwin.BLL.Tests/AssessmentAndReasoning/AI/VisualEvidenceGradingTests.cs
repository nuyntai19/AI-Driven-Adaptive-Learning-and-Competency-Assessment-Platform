using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EduTwin.API.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed class VisualEvidenceGradingTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void MissingSymbolsCannotEarnFullVisualCriterionButCalculationKeepsItsPoints()
    {
        var response = Response();
        var parsed = Parse(Request(), response);
        Assert.Equal(8.5m, parsed.SuggestedScore);
        Assert.Equal(4m, parsed.SuggestedRubricScores.Single(s => s.CriterionId == "calculation").AwardedScore);
        Assert.Equal("Missing", parsed.VisualEvidence!.Single(e => e.CriterionId == "symbols" && e.RequirementIndex == 2).Status);
        Assert.Throws<AIAnalysisValidationException>(() => Parse(Request(), response with
        { SuggestedRubricScores = [Score("drawing", 4), Score("symbols", 2), Score("calculation", 4)] }));
    }

    [Fact]
    public void FullDrawingAndEquivalentMethodCanEarnFullCredit()
    {
        var response = Response() with { UsesAlternativeMethod = true,
            SuggestedRubricScores = [Score("drawing", 4), Score("symbols", 2), Score("calculation", 4)],
            VisualEvidence = Response().VisualEvidence!.Select(e => e with { Status = "Present", Observation = "Nhìn thấy ký hiệu tại vị trí yêu cầu trên ảnh." }).ToArray() };
        Assert.Equal(10m, Parse(Request(), response).SuggestedScore);
    }

    [Fact]
    public void NoStudentImageMeansZeroVisualPointsNotARefusalToGradeCalculation()
    {
        var original = Request();
        var request = original with { StudentSubmission = original.StudentSubmission with { ImageParts = [] } };
        var response = Response() with { SuggestedRubricScores = [Score("drawing", 0), Score("symbols", 0), Score("calculation", 4)],
            VisualEvidence = Response().VisualEvidence!.Select(e => e with { Status = "Missing", StudentImageIndex = null, Observation = "Không có ảnh nháp học sinh đính kèm." }).ToArray() };
        Assert.Equal(4m, Parse(request, response).SuggestedScore);
        Assert.Throws<AIAnalysisValidationException>(() => Parse(request, Response()));
    }

    [Fact]
    public void AllMissingVisualEvidenceCanRetainPartialCreditForAMixedCriterion()
    {
        var original = Request();
        var evidence = new RubricVisualEvidence[]
        {
            new("combined", 1, "Missing", 1, "Không thấy ký hiệu f(2)=4 trong ảnh."),
            new("combined", 2, "Missing", 1, "Không thấy nhãn x=2 trong ảnh.")
        };
        var request = original with
        {
            VerifiedVisualEvidence = evidence,
            Question = original.Question with
            {
                GradingCriteria = original.Question.GradingCriteria with
                {
                    Criteria =
                    [
                        new("combined", "Lập luận và hình minh họa", "Chấm lập luận đúng và các ký hiệu trong hình.", 10)
                        {
                            VisualRequirements = ["Có ký hiệu f(2)=4", "Có nhãn x=2"]
                        }
                    ]
                }
            }
        };
        var response = Response() with
        {
            SuggestedScore = 6,
            SuggestedRubricScores = [Score("combined", 6)],
            CriterionDeductions =
            [
                new("combined", "Visual", "Thiếu các ký hiệu bắt buộc trong hình minh họa.", "Ảnh không có ký hiệu f(2)=4 và nhãn x=2.")
            ],
            VisualEvidence = []
        };

        var parsed = Parse(request, response);

        Assert.Equal(6m, parsed.SuggestedScore);
        Assert.Equal(6m, Assert.Single(parsed.SuggestedRubricScores).AwardedScore);
        Assert.Equal(evidence, parsed.VisualEvidence);
    }

    [Fact]
    public void MissingVisualEvidenceStillCannotReceiveFullMixedCriterionCredit()
    {
        var original = Request();
        var request = original with
        {
            Question = original.Question with
            {
                GradingCriteria = original.Question.GradingCriteria with
                {
                    Criteria =
                    [
                        new("combined", "Lập luận và hình minh họa", "Chấm lập luận đúng và ký hiệu trong hình.", 10)
                        {
                            VisualRequirements = ["Có ký hiệu f(2)=4"]
                        }
                    ]
                }
            }
        };
        var response = Response() with
        {
            SuggestedScore = 10,
            SuggestedRubricScores = [Score("combined", 10)],
            VisualEvidence = [new("combined", 1, "Missing", 1, "Không thấy ký hiệu f(2)=4 trong ảnh.")]
        };

        var error = Assert.Throws<AIAnalysisValidationException>(() => Parse(request, response));

        Assert.Equal("IncompleteVisualEvidenceWithFullScore", error.DiagnosticDetail);
    }

    [Theory]
    [InlineData("missing-entry")]
    [InlineData("duplicate")]
    [InlineData("unknown-criterion")]
    [InlineData("unknown-requirement")]
    [InlineData("wrong-image")]
    [InlineData("present-without-image")]
    [InlineData("invented-status")]
    [InlineData("english")]
    public void RejectsUntraceableOrIncompleteEvidence(string corruption)
    {
        var response = Response(); var evidence = response.VisualEvidence!.ToArray();
        evidence[0] = corruption switch {
            "duplicate" => evidence[1],
            "unknown-criterion" => evidence[0] with { CriterionId = "invented" },
            "unknown-requirement" => evidence[0] with { RequirementIndex = 99 },
            "wrong-image" => evidence[0] with { StudentImageIndex = 2 },
            "present-without-image" => evidence[0] with { StudentImageIndex = null },
            "invented-status" => evidence[0] with { Status = "LooksFine" },
            "english" => evidence[0] with { Observation = "All labels are clearly present." },
            _ => evidence[0] };
        if (corruption == "missing-entry") evidence = evidence.Skip(1).ToArray();
        Assert.Throws<AIAnalysisValidationException>(() => Parse(Request(), response with { VisualEvidence = evidence }));
    }

    [Fact]
    public void NewVisualRubricRejectsLegacyResponseWithoutEvidence()
    {
        var raw = JsonNode.Parse(JsonSerializer.Serialize(Response(), Json))!.AsObject(); raw.Remove("visualEvidence");
        Assert.Throws<AIAnalysisValidationException>(() => Parser().ParseAndValidate(raw.ToJsonString(), Request()));
    }

    [Fact]
    public void ParserRetainsSafetyCapEvenWithoutProviderMaxItems()
    {
        var response = Response() with { VisualEvidence = Enumerable.Repeat(Response().VisualEvidence![0], 241).ToArray() };
        var error = Assert.Throws<AIAnalysisValidationException>(() => Parse(Request(), response));
        Assert.Equal("AI_RESPONSE_SHAPE_INVALID", error.ErrorCode);
    }

    [Fact]
    public void OrdinaryRubricsRemainCompatibleWithLegacyAndEmptyVisualEvidence()
    {
        var original = Request();
        var request = original with { Question = original.Question with { GradingCriteria = original.Question.GradingCriteria with
            { Criteria = original.Question.GradingCriteria.Criteria.Select(c => c with { VisualRequirements = null }).ToArray() } } };
        var raw = JsonNode.Parse(JsonSerializer.Serialize(Response() with { VisualEvidence = [] }, Json))!.AsObject();
        Assert.Equal(8.5m, Parser().ParseAndValidate(raw.ToJsonString(), request).SuggestedScore);
        raw.Remove("visualEvidence");
        Assert.Equal(8.5m, Parser().ParseAndValidate(raw.ToJsonString(), request).SuggestedScore);
    }

    [Fact]
    public void PromptRequiresActualMarksAndNeverWaivesAuthoredDrawingObjectives()
    {
        var prompt = new GeminiPromptBuilder().Build(Request());
        Assert.Contains("An L-shaped corner is NOT an explicit right-angle marker", prompt);
        Assert.Contains("This exception NEVER waives explicit drawing", prompt);
        Assert.Contains("visualRequirements", prompt);
        Assert.Contains("No authored visualRequirements means visualEvidence []", prompt);
        Assert.Contains("vertex LABELS, not right-angle squares", prompt);
        Assert.Contains("NO standard required top/bottom/left/right ordering", prompt);
        Assert.Contains("Do NOT deduct from a separately correct calculation/reasoning criterion", prompt);
        Assert.Contains("VISUAL DEFECTS ARE NOT LOGICAL GAPS", prompt);
    }

    [Fact]
    public void VisualDefectsDoNotRelaxTheLogicalGapConsistencyGuard()
    {
        var error = Assert.Throws<AIAnalysisValidationException>(() => Parse(Request(), Response() with
            { MissingSteps = ["Thiếu một bước suy luận thiết yếu."] }));
        Assert.Equal(AIResponseValidationRule.ReasoningConsistency, error.ValidationRule);
        Assert.Equal("CorrectValidReasoningWithConceptOrStepGap", error.DiagnosticDetail);
        Assert.DoesNotContain("Thiếu", error.Message);
    }

    private static AnalyzeReasoningResponse Parse(AnalyzeReasoningRequest request, AnalyzeReasoningResponse response) =>
        Parser().ParseAndValidate(JsonSerializer.Serialize(response, Json), request);
    private static StrictAIAnalysisResponseParser Parser() => new(new AnalyzeReasoningResponseValidator());
    private static RubricScoreInput Score(string id, decimal score) => new() { CriterionId = id, AwardedScore = score, Comment = "Chấm theo phần thực tế thể hiện được." };
    private static AnalyzeReasoningRequest Request() => new()
    {
        SchemaVersion = AIAnalysisContract.SchemaVersion, Language = "vi", AllowedKnowledgeNodes = [],
        Question = new() { QuestionType = QuestionType.Essay, AnswerEvaluationMode = QuestionAnswerEvaluationMode.Manual,
            QuestionText = "Vẽ tam giác vuông và trung tuyến, ghi tên/ký hiệu; tính BC, AM.", CorrectAnswer = "BC=10; AM=5", Solution = "Lời giải tham khảo.",
            GradingCriteria = new() { SchemaVersion = "2.0", RequiredIdeas = [], CommonErrors = [], ScoringNotes = "Chấm hình riêng, tính toán riêng.", Criteria = [
                new("drawing", "Hình dựng", "Quan hệ hình học đúng", 4) { VisualRequirements = ["Có tam giác và đoạn trung tuyến"] },
                new("symbols", "Ký hiệu", "Nhãn 1 điểm; dấu góc vuông và trung điểm 1 điểm", 2) { VisualRequirements = ["Có A/B/C", "Có M", "Dấu góc vuông tại A", "Dấu BM=MC"] },
                new("calculation", "Tính toán", "Pythagore và trung tuyến", 4)] } },
        StudentSubmission = new() { FinalAnswer = "BC=10; AM=5", ReasoningText = "Theo Pythagore và tính chất trung tuyến.",
            ImageParts = [new([1, 2, 3], "image/png")] }
    };
    private static AnalyzeReasoningResponse Response() => new()
    {
        SchemaVersion = AIAnalysisContract.SchemaVersion, Language = "vi", ReasoningQuality = 100, ErrorType = ErrorType.None,
        MissingSteps = [], RootCauseNodeIds = [], Confidence = 90, Feedback = "Tính toán đúng nhưng hình thiếu nhãn M và các dấu yêu cầu.",
        AnswerAssessment = "Correct", ReasoningVerdict = "Valid", ReasoningIssues = [], SuggestedScore = 10,
        SuggestedRubricScores = [Score("drawing", 4), Score("symbols", 0.5m), Score("calculation", 4)], VisualEvidence = [
            new("drawing", 1, "Present", 1, "Có tam giác với đường nối từ A đến cạnh BC."),
            new("symbols", 1, "Present", 1, "Nhìn thấy nhãn A, B, C tại các đỉnh."),
            new("symbols", 2, "Missing", 1, "Không có nhãn M ở đầu đoạn trung tuyến."),
            new("symbols", 3, "Missing", 1, "Không thấy ô đánh dấu góc vuông tại A."),
            new("symbols", 4, "Missing", 1, "Không thấy dấu hai đoạn bằng nhau hoặc số đo cho BM và MC.")]
    };
}
