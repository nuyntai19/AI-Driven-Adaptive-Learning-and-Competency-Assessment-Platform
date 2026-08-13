using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning;

public interface ISubmitAttemptUseCase
{
    Task<SubmitAttemptResult> ExecuteAsync(
        SubmitAttemptRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);
}
