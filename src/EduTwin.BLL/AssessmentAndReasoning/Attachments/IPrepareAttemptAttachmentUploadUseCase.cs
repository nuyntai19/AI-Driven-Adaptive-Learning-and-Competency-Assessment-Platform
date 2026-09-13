namespace EduTwin.BLL.AssessmentAndReasoning.Attachments;

public interface IPrepareAttemptAttachmentUploadUseCase
{
    Task<PrepareAttemptAttachmentUploadResult> ExecuteAsync(
        Stream content,
        string? fileName,
        CancellationToken cancellationToken = default);
}
