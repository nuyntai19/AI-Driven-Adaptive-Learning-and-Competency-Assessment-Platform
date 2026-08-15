namespace EduTwin.BLL.AssessmentAndReasoning.Processing;

public interface IAIAnalysisJobProcessor
{
    Task<AIAnalysisJobProcessingResult> ExecuteAsync(
        ulong analysisJobId,
        string workerId,
        CancellationToken cancellationToken);
}
