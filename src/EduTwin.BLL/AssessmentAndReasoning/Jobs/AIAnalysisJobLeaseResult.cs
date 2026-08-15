namespace EduTwin.BLL.AssessmentAndReasoning.Jobs;

public enum AIAnalysisJobLeaseOutcome
{
    Claimed,
    Recovered,
    NotFound,
    NotEligible,
    LostRace
}

public sealed record AIAnalysisJobLeaseResult(
    ulong AnalysisJobId,
    Guid CenterId,
    AIAnalysisJobLeaseOutcome Outcome);
