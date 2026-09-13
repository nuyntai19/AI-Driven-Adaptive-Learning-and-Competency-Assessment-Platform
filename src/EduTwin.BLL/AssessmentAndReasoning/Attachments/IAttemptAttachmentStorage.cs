namespace EduTwin.BLL.AssessmentAndReasoning.Attachments;

public interface IAttemptAttachmentStorage
{
    Task<StoredTemporaryAttachment> StoreTemporaryPngAsync(
        Guid centerId,
        string uploadNonce,
        Stream content,
        CancellationToken cancellationToken);

    Task<PromotedAttemptAttachment> PromoteToPermanentAsync(
        AttachmentUploadTokenPayload payload,
        CancellationToken cancellationToken);

    Task DeletePermanentAsync(string storageKey, CancellationToken cancellationToken);

    Task<Stream> OpenPermanentReadAsync(
        string storageKey,
        CancellationToken cancellationToken);
}

public sealed record StoredTemporaryAttachment(string Sha256Hex, long FileSizeBytes);
public sealed record PromotedAttemptAttachment(string StorageKey, bool WasNewlyPromoted);
