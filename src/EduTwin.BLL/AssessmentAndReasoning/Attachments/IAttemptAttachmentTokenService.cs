namespace EduTwin.BLL.AssessmentAndReasoning.Attachments;

public interface IAttemptAttachmentTokenService
{
    string Protect(AttachmentUploadTokenPayload payload);
    bool TryRead(string token, out AttachmentUploadTokenPayload? payload);
}
