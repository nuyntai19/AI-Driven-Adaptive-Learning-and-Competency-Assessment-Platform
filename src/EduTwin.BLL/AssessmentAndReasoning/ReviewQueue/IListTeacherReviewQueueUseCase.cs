using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;

public interface IListTeacherReviewQueueUseCase
{
    Task<ListTeacherReviewQueueResult> ExecuteAsync(
        TeacherReviewQueueQuery query,
        CancellationToken cancellationToken);
}
