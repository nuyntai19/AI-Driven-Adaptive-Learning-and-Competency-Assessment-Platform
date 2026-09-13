using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.AssessmentAndReasoning;

/// <summary>
/// Immutable, tenant-scoped pointer to one verified scratchpad PNG for an attempt.
/// The SHA-256 digest is intentionally bound in the protected upload token, not persisted.
/// </summary>
public sealed class AttemptAttachment : ITenantAppendOnlyEntity
{
    public ulong AttachmentId { get; set; }
    public Guid CenterId { get; set; }
    public ulong AttemptId { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public string StorageKey { get; set; } = null!;
    public long FileSizeBytes { get; set; }
    public string UploadNonce { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }

    public Attempt Attempt { get; set; } = null!;
}
