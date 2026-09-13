namespace EduTwin.BLL.AssessmentAndReasoning.Attachments;

public sealed class GetAttemptAttachmentResult
{
    private GetAttemptAttachmentResult(
        AttemptAttachmentDescriptor? attachment,
        bool forbidden,
        bool notFound)
    {
        Attachment = attachment;
        Forbidden = forbidden;
        NotFound = notFound;
    }

    public AttemptAttachmentDescriptor? Attachment { get; }
    public bool Forbidden { get; }
    public bool NotFound { get; }
    public bool IsSuccess => Attachment is not null;

    public static GetAttemptAttachmentResult Success(AttemptAttachmentDescriptor attachment) =>
        new(attachment, false, false);

    public static GetAttemptAttachmentResult NotFoundResult() => new(null, false, true);
    public static GetAttemptAttachmentResult ForbiddenResult() => new(null, true, false);
}

public sealed record AttemptAttachmentDescriptor(
    string StorageKey,
    string FileName,
    string ContentType);
