using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning.Attachments;

/// <summary>
/// Resolves an attachment only after applying unified role-specific ownership rules. The
/// database query intentionally keeps global filters enabled to fail closed by tenant.
/// </summary>
public sealed class GetAttemptAttachmentUseCase : IGetAttemptAttachmentUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IAttemptTeacherReviewScopeGuard _scopeGuard;

    public GetAttemptAttachmentUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IAttemptTeacherReviewScopeGuard scopeGuard)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _scopeGuard = scopeGuard;
    }

    public async Task<GetAttemptAttachmentResult> ExecuteAsync(
        ulong attemptId,
        CancellationToken cancellationToken = default)
    {
        if (attemptId == 0 || !_tenantContext.IsResolved ||
            _tenantContext.CenterId is not { } centerId || centerId == Guid.Empty ||
            _tenantContext.UserId is not { } actorId || actorId == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role))
        {
            return GetAttemptAttachmentResult.NotFoundResult();
        }

        var attachment = await _dbContext.AttemptAttachments
            .AsNoTracking()
            .Include(candidate => candidate.Attempt)
            .SingleOrDefaultAsync(candidate =>
                candidate.CenterId == centerId && candidate.AttemptId == attemptId,
                cancellationToken);
        if (attachment is null || attachment.Attempt is null)
        {
            return GetAttemptAttachmentResult.NotFoundResult();
        }

        var allowed = await _scopeGuard.CanAccessAttemptAsync(
            centerId,
            actorId,
            _tenantContext.Role,
            attachment.Attempt,
            cancellationToken);

        // Do not disclose attachment existence to an actor outside its review scope.
        if (!allowed)
        {
            return GetAttemptAttachmentResult.NotFoundResult();
        }

        return GetAttemptAttachmentResult.Success(new AttemptAttachmentDescriptor(
            attachment.StorageKey,
            attachment.FileName,
            attachment.ContentType));
    }
}
