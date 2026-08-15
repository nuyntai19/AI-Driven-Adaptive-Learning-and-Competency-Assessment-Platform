namespace EduTwin.BLL.AssessmentAndReasoning.Polling;

public interface IGetAnalysisJobStatusUseCase
{
    Task<GetAnalysisJobStatusResult> ExecuteAsync(
        string analysisJobId,
        CancellationToken cancellationToken = default);
}
