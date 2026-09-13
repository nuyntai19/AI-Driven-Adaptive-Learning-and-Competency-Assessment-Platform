using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class PrepareAttemptAttachmentUploadResponse
{
    public required PrepareAttemptAttachmentUploadDataDto Data { get; init; }
    public required MetaDto Meta { get; init; }
}

public sealed class PrepareAttemptAttachmentUploadDataDto
{
    public required string DrawingUploadToken { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
}
