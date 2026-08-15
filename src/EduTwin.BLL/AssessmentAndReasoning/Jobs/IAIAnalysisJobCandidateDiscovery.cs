namespace EduTwin.BLL.AssessmentAndReasoning.Jobs;

public interface IAIAnalysisJobCandidateDiscovery
{
    Task<AIAnalysisJobDiscoveryResult> DiscoverAsync(
        int perCenterBatchSize,
        int totalBatchSize,
        CancellationToken cancellationToken);
}
