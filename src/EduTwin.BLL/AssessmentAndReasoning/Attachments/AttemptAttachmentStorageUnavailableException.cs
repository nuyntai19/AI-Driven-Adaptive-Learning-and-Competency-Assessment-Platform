namespace EduTwin.BLL.AssessmentAndReasoning.Attachments;

/// <summary>
/// Signals an operational storage fault. The analysis worker treats it as retryable;
/// it must never silently pretend that an attached drawing was absent.
/// </summary>
public sealed class AttemptAttachmentStorageUnavailableException : Exception
{
    public AttemptAttachmentStorageUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
