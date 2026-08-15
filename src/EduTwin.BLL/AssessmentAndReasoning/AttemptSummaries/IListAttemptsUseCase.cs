using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.AttemptSummaries;

public interface IListAttemptsUseCase
{
    Task<ListAttemptsResult> ExecuteAsync(
        ListAttemptsQuery query,
        CancellationToken cancellationToken);
}
