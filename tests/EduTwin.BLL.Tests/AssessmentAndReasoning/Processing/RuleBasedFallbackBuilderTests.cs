using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.Contracts.AssessmentAndReasoning;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Processing;

public sealed class RuleBasedFallbackBuilderTests
{
    private static readonly DateTime UtcNow =
        new(2026, 8, 14, 9, 30, 0, DateTimeKind.Utc);

    private readonly RuleBasedFallbackBuilder _sut = new();

    [Fact]
    public void Build_ValidInput_MapsFrozenFallbackContract()
    {
        var centerId = Guid.NewGuid();

        var result = _sut.Build(Input(centerId, true, false, "vi"));

        Assert.Equal(centerId, result.CenterId);
        Assert.Equal(17ul, result.AttemptId);
        Assert.Equal("ai-analysis-v1", result.SchemaVersion);
        Assert.Null(result.MethodDetected);
        Assert.Null(result.ReasoningQuality);
        Assert.Equal(ErrorType.Unknown, result.ErrorType);
        Assert.Null(result.Misconception);
        Assert.Equal(0, result.MissingSteps.RootElement.GetArrayLength());
        Assert.Equal(0, result.RootCauseNodeIds.RootElement.GetArrayLength());
        Assert.Null(result.AnalysisConfidence);
        Assert.False(string.IsNullOrWhiteSpace(result.Feedback));
        Assert.True(result.IsFallback);
        Assert.True(result.NeedsTeacherReview);
        Assert.Equal(AnalysisProvider.RuleBased, result.Provider);
        Assert.Null(result.ModelName);
        Assert.Null(result.OverrideReasoningQuality);
        Assert.Null(result.OverrideErrorType);
        Assert.Null(result.OverrideFeedback);
        Assert.Null(result.OverrideIsCorrect);
        Assert.Null(result.OverrideReason);
        Assert.Null(result.OverriddenByUserId);
        Assert.Null(result.OverriddenAt);
        Assert.Equal(0u, result.OverrideVersion);
        Assert.Equal(UtcNow, result.CreatedAt);
        Assert.Null(result.CreatedBy);
        Assert.Equal(UtcNow, result.UpdatedAt);
    }

    [Theory]
    [InlineData("vi", true, false)]
    [InlineData("vi", false, false)]
    [InlineData("vi", null, false)]
    [InlineData("vi", null, true)]
    [InlineData("en", true, false)]
    [InlineData("en", false, false)]
    [InlineData("en", null, false)]
    [InlineData("en", null, true)]
    public void Build_LanguageAndPreliminaryOutcome_ProducesDeterministicNonEmptyFeedback(
        string language,
        bool? isCorrect,
        bool skipped)
    {
        var input = Input(Guid.NewGuid(), isCorrect, skipped, language);

        var first = _sut.Build(input);
        var second = _sut.Build(input);

        Assert.False(string.IsNullOrWhiteSpace(first.Feedback));
        Assert.Equal(first.Feedback, second.Feedback);
        Assert.Equal(first.ErrorType, second.ErrorType);
        Assert.Equal(first.Provider, second.Provider);
        Assert.Equal(
            first.MissingSteps.RootElement.GetRawText(),
            second.MissingSteps.RootElement.GetRawText());
        Assert.Equal(
            first.RootCauseNodeIds.RootElement.GetRawText(),
            second.RootCauseNodeIds.RootElement.GetRawText());
    }

    [Theory]
    [InlineData("")]
    [InlineData("VI")]
    [InlineData("fr")]
    public void Build_UnsupportedLanguage_FailsClosed(string language)
    {
        Assert.Throws<ArgumentException>(
            () => _sut.Build(Input(Guid.NewGuid(), true, false, language)));
    }

    private static RuleBasedFallbackInput Input(
        Guid centerId,
        bool? isCorrect,
        bool skipped,
        string language) =>
        new(
            centerId,
            17,
            isCorrect,
            isCorrect == true ? 1m : 0m,
            skipped,
            language,
            UtcNow);
}
