using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.AssessmentAndReasoning.Attachments;

public sealed class PrepareAttemptAttachmentUploadUseCase : IPrepareAttemptAttachmentUploadUseCase
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);
    private readonly ITenantContext _tenantContext;
    private readonly IAttemptAttachmentStorage _storage;
    private readonly IAttemptAttachmentTokenService _tokens;
    private readonly TimeProvider _timeProvider;

    public PrepareAttemptAttachmentUploadUseCase(
        ITenantContext tenantContext,
        IAttemptAttachmentStorage storage,
        IAttemptAttachmentTokenService tokens,
        TimeProvider timeProvider)
    {
        _tenantContext = tenantContext;
        _storage = storage;
        _tokens = tokens;
        _timeProvider = timeProvider;
    }

    public async Task<PrepareAttemptAttachmentUploadResult> ExecuteAsync(
        Stream content,
        string? fileName,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveStudent(out var centerId, out var studentId) || content is null || !content.CanRead)
        {
            return PrepareAttemptAttachmentUploadResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var nonce = Guid.NewGuid().ToString("N");
        try
        {
            var stored = await _storage.StoreTemporaryPngAsync(centerId, nonce, content, cancellationToken);
            var expiresAtUtc = _timeProvider.GetUtcNow().UtcDateTime.Add(TokenLifetime);
            var token = _tokens.Protect(new AttachmentUploadTokenPayload(
                centerId,
                studentId,
                nonce,
                stored.Sha256Hex,
                NormalizeFileName(fileName),
                stored.FileSizeBytes,
                expiresAtUtc));
            return PrepareAttemptAttachmentUploadResult.Success(token, expiresAtUtc);
        }
        catch (AttemptAttachmentValidationException)
        {
            return PrepareAttemptAttachmentUploadResult.Failure(ErrorCodes.ValidationFailed);
        }
    }

    private bool TryResolveStudent(out Guid centerId, out Guid studentId)
    {
        centerId = _tenantContext.CenterId ?? Guid.Empty;
        studentId = _tenantContext.UserId ?? Guid.Empty;
        return _tenantContext.IsResolved &&
               centerId != Guid.Empty &&
               studentId != Guid.Empty &&
               string.Equals(_tenantContext.Role, nameof(UserRole.Student), StringComparison.Ordinal);
    }

    private static string NormalizeFileName(string? fileName)
    {
        var normalized = Path.GetFileName(fileName ?? string.Empty).Trim();
        return string.IsNullOrEmpty(normalized) ? "scratchpad.png" : normalized[..Math.Min(normalized.Length, 255)];
    }
}
