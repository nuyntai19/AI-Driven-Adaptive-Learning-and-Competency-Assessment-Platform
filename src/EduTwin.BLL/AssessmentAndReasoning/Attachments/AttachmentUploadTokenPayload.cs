namespace EduTwin.BLL.AssessmentAndReasoning.Attachments;

/// <summary>
/// Protected, short-lived binding between a verified temporary PNG and its authenticated owner.
/// The SHA-256 digest is never persisted in the database.
/// </summary>
public sealed record AttachmentUploadTokenPayload(
    Guid CenterId,
    Guid StudentId,
    string UploadNonce,
    string Sha256Hex,
    string FileName,
    long FileSizeBytes,
    DateTime ExpiresAtUtc);
