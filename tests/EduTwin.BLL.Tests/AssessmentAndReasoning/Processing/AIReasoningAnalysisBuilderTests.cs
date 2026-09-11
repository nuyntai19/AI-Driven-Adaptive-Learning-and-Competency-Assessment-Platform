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
        Assert.False(analysis.NeedsTeacherReview);
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
