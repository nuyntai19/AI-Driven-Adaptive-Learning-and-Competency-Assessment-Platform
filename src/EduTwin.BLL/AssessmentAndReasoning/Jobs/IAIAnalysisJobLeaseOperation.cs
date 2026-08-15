namespace EduTwin.BLL.AssessmentAndReasoning.Jobs;

public interface IAIAnalysisJobLeaseOperation
{
    Task<AIAnalysisJobLeaseResult> ExecuteAsync(
        AIAnalysisJobWorkItem workItem,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);
}
