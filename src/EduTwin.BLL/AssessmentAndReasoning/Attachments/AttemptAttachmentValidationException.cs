namespace EduTwin.BLL.AssessmentAndReasoning.Attachments;

public sealed class AttemptAttachmentValidationException : Exception
{
    public AttemptAttachmentValidationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
