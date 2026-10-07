using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.Contracts.AssessmentAndReasoning;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Processing;

public sealed class AIReasoningAnalysisBuilderTests
{
    [Fact]
    public void Build_MapsValidatedResponseAndPreservesJsonArrayOrder()
    {
        var centerId = Guid.NewGuid();
        var utcNow = new DateTime(2026, 8, 27, 9, 15, 0, DateTimeKind.Utc);
        var missingSteps = new List<string> { "step-b", "step-a" };
        var rootNodeIds = new List<string> { "9", "2" };
        var response = new AnalyzeReasoningResponse
        {
            SchemaVersion = AIAnalysisContract.SchemaVersion,
            Language = "vi",
            MethodDetected = "substitution",
            ReasoningQuality = 73,
            ErrorType = ErrorType.Reasoning,
            Misconception = "sign error",
            MissingSteps = missingSteps,
            RootCauseNodeIds = rootNodeIds,
            Confidence = 88,
            Feedback = "Review the sign transition."
        };

        var analysis = new AIReasoningAnalysisBuilder().Build(centerId, 19, response, utcNow);

        Assert.Equal(centerId, analysis.CenterId);
        Assert.Equal(19ul, analysis.AttemptId);
        Assert.Equal(response.SchemaVersion, analysis.SchemaVersion);
        Assert.Equal(response.MethodDetected, analysis.MethodDetected);
        Assert.Equal(73m, analysis.ReasoningQuality);
        Assert.Equal(response.ErrorType, analysis.ErrorType);
        Assert.Equal(response.Misconception, analysis.Misconception);
        Assert.Equal(["step-b", "step-a"], analysis.MissingSteps.RootElement.EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(["9", "2"], analysis.RootCauseNodeIds.RootElement.EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(88m, analysis.AnalysisConfidence);
        Assert.Equal(response.Feedback, analysis.Feedback);
        Assert.False(analysis.IsFallback);
        Assert.True(analysis.NeedsTeacherReview);
        Assert.Equal(AnalysisProvider.Gemini, analysis.Provider);
        Assert.Null(analysis.ModelName);
        Assert.Null(analysis.OverrideReasoningQuality);
        Assert.Null(analysis.OverrideErrorType);
        Assert.Null(analysis.OverrideFeedback);
        Assert.Null(analysis.OverrideIsCorrect);
        Assert.Null(analysis.OverrideReason);
        Assert.Null(analysis.OverriddenByUserId);
        Assert.Null(analysis.OverriddenAt);
        Assert.Equal(0u, analysis.OverrideVersion);
        Assert.Equal(utcNow, analysis.CreatedAt);
        Assert.Equal(utcNow, analysis.UpdatedAt);
        Assert.Null(analysis.CreatedBy);
        Assert.Equal(["step-b", "step-a"], missingSteps);
        Assert.Equal(["9", "2"], rootNodeIds);
    }

    [Fact]
    public void Build_InvalidAggregateIdentityOrResponse_Throws()
    {
        var response = ValidResponse();
        var builder = new AIReasoningAnalysisBuilder();

        Assert.Throws<ArgumentException>(() => builder.Build(Guid.Empty, 1, response, DateTime.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build(Guid.NewGuid(), 0, response, DateTime.UtcNow));
        Assert.Throws<ArgumentNullException>(() => builder.Build(Guid.NewGuid(), 1, null!, DateTime.UtcNow));
    }

    [Fact]
    public void Build_CorrectAnswer_WithInvalidReasoning_PreservesConcernForTeacher()
    {
        var response = ValidResponse() with
        {
            ErrorType = ErrorType.Reasoning,
            Misconception = "AI incorrectly claims an error",
            ReasoningVerdict = "Invalid",
            MissingSteps = ["missing"],
            RootCauseNodeIds = ["42"],
            Feedback = "Incorrect answer."
        };

        var analysis = new AIReasoningAnalysisBuilder().Build(
            Guid.NewGuid(), 7, response, DateTime.UtcNow, true, "vi");

        Assert.Equal(ErrorType.Reasoning, analysis.ErrorType);
        Assert.Equal(response.Misconception, analysis.Misconception);
        Assert.Single(analysis.MissingSteps.RootElement.EnumerateArray());
        Assert.Single(analysis.RootCauseNodeIds.RootElement.EnumerateArray());
        Assert.Equal(response.Feedback, analysis.Feedback);
        Assert.True(analysis.NeedsTeacherReview);
        Assert.Equal("Gemini", analysis.FeedbackOrigin);
    }

    [Fact]
    public void Build_DeterministicMismatch_WithAiDisagreement_IsNotSilenced()
    {
        var response = ValidResponse() with { ErrorType = ErrorType.None, Feedback = "Equivalent notation.", AnswerAssessment = "Correct", ReasoningVerdict = "Valid" };

        var analysis = new AIReasoningAnalysisBuilder().Build(
            Guid.NewGuid(), 8, response, DateTime.UtcNow, false, "vi");

        Assert.Equal(ErrorType.None, analysis.ErrorType);
        Assert.Equal(response.Feedback, analysis.Feedback);
        Assert.True(analysis.NeedsTeacherReview);
    }

    [Fact]
    public void Build_ValidAlternativeMethod_KeepsFullQualityAndSpecificFeedback()
    {
        var response = ValidResponse() with { MethodDetected = "Geometry", AnswerAssessment = "Correct", ReasoningVerdict = "Valid", Feedback = "Valid geometric proof, no need to use the sample algebraic method." };
        var analysis = new AIReasoningAnalysisBuilder().Build(Guid.NewGuid(), 9, response, DateTime.UtcNow, true);
        Assert.Equal(100m, analysis.ReasoningQuality);
        Assert.False(analysis.NeedsTeacherReview);
        Assert.Equal(response.Feedback, analysis.Feedback);
    }

    [Fact]
    public void Build_DigitCancellationFallacy_DoesNotPassBecauseAnswerIsCorrect()
    {
        var response = ValidResponse() with { AnswerAssessment = "Correct", ReasoningVerdict = "Invalid", ErrorType = ErrorType.Reasoning,
            ReasoningQuality = 20, Misconception = "Cancelling the digit 6 in 16/64 is not a valid operation.", Feedback = "Correct number, invalid derivation." };
        var analysis = new AIReasoningAnalysisBuilder().Build(Guid.NewGuid(), 10, response, DateTime.UtcNow, true);
        Assert.True(analysis.NeedsTeacherReview);
        Assert.Equal(ErrorType.Reasoning, analysis.ErrorType);
        Assert.Equal(response.Misconception, analysis.Misconception);
        Assert.Equal(20m, analysis.ReasoningQuality);
    }

    [Theory]
    [InlineData("Uncertain", "Valid", 100)]
    [InlineData("Correct", "Uncertain", 100)]
    [InlineData("Correct", "Valid", 79)]
    public void Build_UnverifiedOrLowConfidenceAnalysis_RequiresTeacher(string answer, string verdict, int confidence)
    {
        var response = ValidResponse() with { AnswerAssessment = answer, ReasoningVerdict = verdict, Confidence = confidence };
        Assert.True(new AIReasoningAnalysisBuilder().Build(Guid.NewGuid(), 11, response, DateTime.UtcNow, true).NeedsTeacherReview);
    }

    private static AnalyzeReasoningResponse ValidResponse() => new()
    {
        SchemaVersion = AIAnalysisContract.SchemaVersion,
        Language = "en",
        ReasoningQuality = 100,
        ErrorType = ErrorType.None,
        MissingSteps = [],
        RootCauseNodeIds = [],
        Confidence = 100,
        Feedback = "Correct."
    };
}
