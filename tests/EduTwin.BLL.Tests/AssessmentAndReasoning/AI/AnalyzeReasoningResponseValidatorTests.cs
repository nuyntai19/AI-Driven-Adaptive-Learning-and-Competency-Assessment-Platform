using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed class AnalyzeReasoningResponseValidatorTests
{
    private readonly AnalyzeReasoningResponseValidator _validator = new();

    [Fact]
    public void Validate_ValidVietnameseResponse_Passes()
    {
        _validator.Validate(CreateRequest(), CreateResponse());
    }

    [Fact]
    public void Validate_ValidEnglishResponse_Passes()
    {
        _validator.Validate(CreateRequest(language: "en"), CreateResponse(language: "en", feedback: "Good work."));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Validate_ZeroAndHundredBoundaries_Pass(int value)
    {
        var response = CreateResponse() with { ReasoningQuality = value, Confidence = value };

        _validator.Validate(CreateRequest(), response);
    }

    [Fact]
    public void Validate_EmptyRootCauseAndMissingSteps_Pass()
    {
        var response = CreateResponse() with { RootCauseNodeIds = [], MissingSteps = [] };

        _validator.Validate(CreateRequest(), response);
    }

    [Fact]
    public void Validate_AllowedRootCauseIds_PassWithoutMutation()
    {
        var allowedNodes = new List<AnalyzeReasoningAllowedKnowledgeNode>
        {
            new() { NodeId = "2", NodeName = "Second" },
            new() { NodeId = "1", NodeName = "First" }
        };
        var rootIds = new List<string> { "2", "1" };
        var missingSteps = new List<string> { " step with spaces ", "second" };
        var request = CreateRequest() with { AllowedKnowledgeNodes = allowedNodes };
        var response = CreateResponse() with { RootCauseNodeIds = rootIds, MissingSteps = missingSteps };

        _validator.Validate(request, response);

        Assert.Same(allowedNodes, request.AllowedKnowledgeNodes);
        Assert.Same(rootIds, response.RootCauseNodeIds);
        Assert.Same(missingSteps, response.MissingSteps);
        Assert.Equal(["2", "1"], rootIds);
        Assert.Equal([" step with spaces ", "second"], missingSteps);
        Assert.Equal(["2", "1"], allowedNodes.Select(node => node.NodeId));
    }

    [Theory]
    [InlineData("AI-ANALYSIS-V1")]
    [InlineData(" ai-analysis-v1")]
    [InlineData("ai-analysis-v2")]
    public void Validate_WrongSchemaVersionCasingOrWhitespace_ThrowsSemanticError(string schemaVersion)
    {
        AssertSemanticFailure(CreateRequest(), CreateResponse() with { SchemaVersion = schemaVersion });
    }

    [Theory]
    [InlineData("VI")]
    [InlineData(" en")]
    [InlineData("fr")]
    public void Validate_UnsupportedRequestLanguage_ThrowsSemanticError(string language)
    {
        AssertSemanticFailure(CreateRequest(language), CreateResponse());
    }

    [Theory]
    [InlineData("VI")]
    [InlineData("en ")]
    [InlineData("fr")]
    public void Validate_UnsupportedResponseLanguage_ThrowsSemanticError(string language)
    {
        AssertSemanticFailure(CreateRequest(), CreateResponse(language));
    }

    [Fact]
    public void Validate_ResponseLanguageDifferentFromRequest_ThrowsSemanticError()
    {
        AssertSemanticFailure(CreateRequest(language: "vi"), CreateResponse(language: "en", feedback: "Feedback."));
    }

    [Theory]
    [InlineData("vi", "")]
    [InlineData("vi", "   ")]
    [InlineData("en", "")]
    [InlineData("en", "\r\n")]
    public void Validate_EmptyOrWhitespaceFeedbackForSupportedLanguage_ThrowsSemanticError(
        string language,
        string feedback)
    {
        AssertSemanticFailure(
            CreateRequest(language),
            CreateResponse(language, feedback));
    }

    [Fact]
    public void Validate_NullFeedback_ThrowsSemanticError()
    {
        AssertSemanticFailure(CreateRequest(), CreateResponse() with { Feedback = null! });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_ReasoningQualityOutsideRange_ThrowsSemanticError(int value)
    {
        AssertSemanticFailure(CreateRequest(), CreateResponse() with { ReasoningQuality = value });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_ConfidenceOutsideRange_ThrowsSemanticError(int value)
    {
        AssertSemanticFailure(CreateRequest(), CreateResponse() with { Confidence = value });
    }

    [Fact]
    public void Validate_UndefinedErrorTypeCast_ThrowsSemanticError()
    {
        AssertSemanticFailure(CreateRequest(), CreateResponse() with { ErrorType = (ErrorType)999 });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhitespaceMethodDetectedWhenNonNull_ThrowsSemanticError(string value)
    {
        AssertSemanticFailure(CreateRequest(), CreateResponse() with { MethodDetected = value });
    }

    [Theory]
    [InlineData("")]
    [InlineData("\t")]
    public void Validate_WhitespaceMisconceptionWhenNonNull_ThrowsSemanticError(string value)
    {
        AssertSemanticFailure(CreateRequest(), CreateResponse() with { Misconception = value });
    }

    [Fact]
    public void Validate_MethodDetectedLengthFiveHundred_Passes()
    {
        _validator.Validate(CreateRequest(), CreateResponse() with { MethodDetected = new string('m', 500) });
    }

    [Fact]
    public void Validate_MethodDetectedLengthFiveHundredOne_ThrowsSemanticError()
    {
        AssertSemanticFailure(CreateRequest(), CreateResponse() with { MethodDetected = new string('m', 501) });
    }

    [Fact]
    public void Validate_MisconceptionLengthOneThousand_Passes()
    {
        _validator.Validate(CreateRequest(), CreateResponse() with { Misconception = new string('m', 1000) });
    }

    [Fact]
    public void Validate_MisconceptionLengthOneThousandOne_ThrowsSemanticError()
    {
        AssertSemanticFailure(CreateRequest(), CreateResponse() with { Misconception = new string('m', 1001) });
    }

    [Fact]
    public void Validate_NullMethodDetectedAndMisconception_Pass()
    {
        _validator.Validate(
            CreateRequest(),
            CreateResponse() with { MethodDetected = null, Misconception = null });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrWhitespaceMissingStepsItem_ThrowsSemanticError(string value)
    {
        AssertSemanticFailure(CreateRequest(), CreateResponse() with { MissingSteps = [value] });
    }

    [Fact]
    public void Validate_NullMissingStepsCollectionOrItem_ThrowsSemanticError()
    {
        AssertSemanticFailure(CreateRequest(), CreateResponse() with { MissingSteps = null! });
        AssertSemanticFailure(CreateRequest(), CreateResponse() with { MissingSteps = [null!] });
    }

    [Fact]
    public void Validate_CanonicalAllowedPositiveUlongId_Passes()
    {
        var maxValue = ulong.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var request = CreateRequest([new() { NodeId = maxValue, NodeName = "Max" }]);
        var response = CreateResponse() with { RootCauseNodeIds = [maxValue] };

        _validator.Validate(request, response);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("1.0")]
    [InlineData("1e1")]
    [InlineData("01")]
    [InlineData("18446744073709551616")]
    public void Validate_NonCanonicalOrOutOfRangeRootId_ThrowsSemanticError(string nodeId)
    {
        var request = CreateRequest([new() { NodeId = nodeId, NodeName = "Node" }]);
        var response = CreateResponse() with { RootCauseNodeIds = [nodeId] };

        AssertSemanticFailure(request, response);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrWhitespaceRootId_ThrowsSemanticError(string nodeId)
    {
        AssertSemanticFailure(CreateRequest(), CreateResponse() with { RootCauseNodeIds = [nodeId] });
    }

    [Fact]
    public void Validate_NullRootCauseCollectionOrItem_ThrowsSemanticError()
    {
        AssertSemanticFailure(CreateRequest(), CreateResponse() with { RootCauseNodeIds = null! });
        AssertSemanticFailure(CreateRequest(), CreateResponse() with { RootCauseNodeIds = [null!] });
    }

    [Fact]
    public void Validate_DuplicateRootId_ThrowsSemanticError()
    {
        AssertSemanticFailure(CreateRequest(), CreateResponse() with { RootCauseNodeIds = ["101", "101"] });
    }

    [Fact]
    public void Validate_HallucinatedRootIdAbsentFromAllowList_ThrowsSemanticError()
    {
        AssertSemanticFailure(CreateRequest(), CreateResponse() with { RootCauseNodeIds = ["999"] });
    }

    [Fact]
    public void Validate_CrossTenantFixtureIdAbsentFromCenterAAllowList_ThrowsSemanticError()
    {
        var centerARequest = CreateRequest([new() { NodeId = "101", NodeName = "Shared topic" }]);
        var centerBResponse = CreateResponse() with { RootCauseNodeIds = ["202"] };

        AssertSemanticFailure(centerARequest, centerBResponse);
    }

    [Fact]
    public void Validate_SameNameDifferentIdNode_DoesNotPassByNodeName()
    {
        var request = CreateRequest([new() { NodeId = "101", NodeName = "Same name" }]);
        var response = CreateResponse() with { RootCauseNodeIds = ["202"] };

        AssertSemanticFailure(request, response);
    }

    [Fact]
    public void Validate_EmptyAllowListAndEmptyRoots_Passes()
    {
        _validator.Validate(CreateRequest([]), CreateResponse() with { RootCauseNodeIds = [] });
    }

    [Fact]
    public void Validate_EmptyAllowListAndNonEmptyRoots_ThrowsSemanticError()
    {
        AssertSemanticFailure(CreateRequest([]), CreateResponse() with { RootCauseNodeIds = ["101"] });
    }

    [Fact]
    public void Validate_NullRequest_ThrowsSemanticError()
    {
        AssertSemanticFailure(null!, CreateResponse());
    }

    [Fact]
    public void Validate_NullResponse_ThrowsSemanticError()
    {
        AssertSemanticFailure(CreateRequest(), null!);
    }

    [Fact]
    public void Validate_NullAllowedKnowledgeNodes_ThrowsSemanticError()
    {
        AssertSemanticFailure(
            CreateRequest() with { AllowedKnowledgeNodes = null! },
            CreateResponse());
    }

    private void AssertSemanticFailure(
        AnalyzeReasoningRequest request,
        AnalyzeReasoningResponse response)
    {
        var exception = Assert.Throws<AIAnalysisValidationException>(
            () => _validator.Validate(request, response));

        Assert.Equal("AI_RESPONSE_SEMANTIC_INVALID", exception.ErrorCode);
        Assert.Equal("AI response semantics are invalid.", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain("101", exception.Message, StringComparison.Ordinal);
    }

    private static AnalyzeReasoningRequest CreateRequest(
        string language = "vi") =>
        CreateRequest(
            [new AnalyzeReasoningAllowedKnowledgeNode { NodeId = "101", NodeName = "Mũ và Logarit" }],
            language);

    private static AnalyzeReasoningRequest CreateRequest(
        IReadOnlyList<AnalyzeReasoningAllowedKnowledgeNode> allowedNodes,
        string language = "vi") =>
        new()
        {
            SchemaVersion = AIAnalysisContract.SchemaVersion,
            Language = language,
            Question = new AnalyzeReasoningQuestion
            {
                QuestionType = QuestionType.MultipleChoice,
                QuestionText = "Question",
                CorrectAnswer = "B",
                Solution = "Solution",
                ExpectedReasoning = null,
                GradingCriteria = new AnalyzeReasoningGradingCriteria
                {
                    SchemaVersion = "1.0",
                    RequiredIdeas = [],
                    CommonErrors = [],
                    ScoringNotes = "Notes"
                }
            },
            StudentSubmission = new AnalyzeReasoningStudentSubmission
            {
                FinalAnswer = "B",
                ReasoningText = "Reasoning",
                TimeSpentSeconds = 10,
                Confidence = 80,
                AnswerChanges = 0
            },
            AllowedKnowledgeNodes = allowedNodes
        };

    private static AnalyzeReasoningResponse CreateResponse(
        string language = "vi",
        string feedback = "Em đã làm tốt.") =>
        new()
        {
            SchemaVersion = AIAnalysisContract.SchemaVersion,
            Language = language,
            MethodDetected = "Method",
            ReasoningQuality = 72,
            ErrorType = ErrorType.Reasoning,
            Misconception = null,
            MissingSteps = ["Missing step"],
            RootCauseNodeIds = ["101"],
            Confidence = 85,
            Feedback = feedback
        };
}
