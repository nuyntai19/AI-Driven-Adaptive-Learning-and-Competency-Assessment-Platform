using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning.Attachments;

/// <summary>
/// Resolves an attachment only after applying role-specific ownership rules. The
/// database query intentionally keeps global filters enabled to fail closed by tenant.
/// </summary>
public sealed class GetAttemptAttachmentUseCase : IGetAttemptAttachmentUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetAttemptAttachmentUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<GetAttemptAttachmentResult> ExecuteAsync(
        ulong attemptId,
        CancellationToken cancellationToken = default)
    {
        if (attemptId == 0 || !_tenantContext.IsResolved ||
            _tenantContext.CenterId is not { } centerId || centerId == Guid.Empty ||
            _tenantContext.UserId is not { } actorId || actorId == Guid.Empty ||
            !Enum.TryParse<UserRole>(_tenantContext.Role, false, out var role))
        {
            return GetAttemptAttachmentResult.NotFoundResult();
        }

        var attachment = await _dbContext.AttemptAttachments
            .AsNoTracking()
            .Include(candidate => candidate.Attempt)
                .ThenInclude(attempt => attempt.Assignment)
            .SingleOrDefaultAsync(candidate =>
                candidate.CenterId == centerId && candidate.AttemptId == attemptId,
                cancellationToken);
        if (attachment is null)
        {
            return GetAttemptAttachmentResult.NotFoundResult();
        }

        var allowed = role switch
        {
            UserRole.Student => attachment.Attempt.StudentId == actorId,
            UserRole.Teacher => await IsAssignedTeacherAsync(centerId, actorId, attachment.Attempt, cancellationToken),
            UserRole.CenterManager => true,
            _ => false
        };

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

    private Task<bool> IsAssignedTeacherAsync(
        Guid centerId,
        Guid teacherId,
        Attempt attempt,
        CancellationToken cancellationToken)
    {
        // Free-practice work has no class ownership boundary and is never visible to a teacher.
        if (attempt.AssignmentId is null || attempt.Assignment is null)
        {
            return Task.FromResult(false);
        }

        var classId = attempt.Assignment.ClassId;

        return _dbContext.AssignmentTargets.AsNoTracking().AnyAsync(target =>
            target.CenterId == centerId &&
            target.AssignmentId == attempt.AssignmentId.Value &&
            target.StudentId == attempt.StudentId &&
            target.Assignment != null &&
            target.Assignment.ClassId == classId &&
            target.Assignment.Class != null &&
            target.Assignment.Class.TeacherId == teacherId,
            cancellationToken);
    }
}
