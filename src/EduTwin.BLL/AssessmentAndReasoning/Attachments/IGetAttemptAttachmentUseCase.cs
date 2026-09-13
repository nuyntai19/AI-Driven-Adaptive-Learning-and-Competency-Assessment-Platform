namespace EduTwin.BLL.AssessmentAndReasoning.Attachments;

public interface IGetAttemptAttachmentUseCase
{
    Task<GetAttemptAttachmentResult> ExecuteAsync(
        ulong attemptId,
        CancellationToken cancellationToken = default);
}
