namespace EduTwin.BLL.AssessmentAndReasoning.Processing;

public sealed record RuleBasedFallbackInput(
    Guid CenterId,
    ulong AttemptId,
    bool? IsCorrect,
    decimal? AwardedScore,
    bool Skipped,
    string ReasoningLanguage,
    DateTime UtcNow);
