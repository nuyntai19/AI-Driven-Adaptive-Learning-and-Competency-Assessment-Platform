namespace EduTwin.BLL.AssessmentAndReasoning.Processing;

public enum AIAnalysisJobProcessingOutcome
{
    Completed,
    RetryScheduled,
    FallbackCompleted,
    AlreadyTerminal,
    NotFound,
    NotEligible,
    LostRace,
    FailedTerminal
}

public sealed record AIAnalysisJobProcessingResult(
    ulong AnalysisJobId,
    ulong? AttemptId,
    AIAnalysisJobProcessingOutcome Outcome);
