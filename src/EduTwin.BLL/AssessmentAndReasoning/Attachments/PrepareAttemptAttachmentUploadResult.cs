namespace EduTwin.BLL.AssessmentAndReasoning.Attachments;

public sealed class PrepareAttemptAttachmentUploadResult
{
    private PrepareAttemptAttachmentUploadResult(
        bool isSuccess,
        string? drawingUploadToken,
        DateTime? expiresAtUtc,
        string? errorCode)
    {
        IsSuccess = isSuccess;
        DrawingUploadToken = drawingUploadToken;
        ExpiresAtUtc = expiresAtUtc;
        ErrorCode = errorCode;
    }

    public bool IsSuccess { get; }
    public string? DrawingUploadToken { get; }
    public DateTime? ExpiresAtUtc { get; }
    public string? ErrorCode { get; }

    public static PrepareAttemptAttachmentUploadResult Success(string token, DateTime expiresAtUtc) =>
        new(true, token, expiresAtUtc, null);

    public static PrepareAttemptAttachmentUploadResult Failure(string errorCode) =>
        new(false, null, null, errorCode);
}
