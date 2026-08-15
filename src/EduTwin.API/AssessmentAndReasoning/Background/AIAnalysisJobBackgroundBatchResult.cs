namespace EduTwin.API.AssessmentAndReasoning.Background;

public sealed record AIAnalysisJobBackgroundBatchResult(
    int CandidateCount,
    int ClaimedCount,
    int RecoveredCount,
    int StaleCount,
    int LostRaceCount,
    int FallbackCompletedCount,
    int AlreadyTerminalCount,
    int ProcessingStaleCount,
    int ProcessingLostRaceCount,
    int ExceptionCount)
{
    public bool HadCandidates => CandidateCount > 0;
}
