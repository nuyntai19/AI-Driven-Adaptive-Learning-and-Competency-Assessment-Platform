using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Assignments;

public class SaveAssignmentDraftUseCase : ISaveAssignmentDraftUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public SaveAssignmentDraftUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<SaveAssignmentDraftResult> ExecuteAsync(
        Guid assignmentId,
        SaveAssignmentDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            !string.Equals(_tenantContext.Role, nameof(UserRole.Student), StringComparison.Ordinal))
        {
            return SaveAssignmentDraftResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var currentUserId = _tenantContext.UserId.Value;
        var centerId = _tenantContext.CenterId.Value;
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var assignment = await _dbContext.Assignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CenterId == centerId && a.AssignmentId == assignmentId && !a.IsDeleted, cancellationToken);

        if (assignment == null)
        {
            return SaveAssignmentDraftResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (assignment.Status != AssignmentStatus.Published)
        {
            return SaveAssignmentDraftResult.Failure(ErrorCodes.AssignmentNotAvailable);
        }

        var isTarget = await _dbContext.AssignmentTargets
            .AsNoTracking()
            .AnyAsync(at => at.CenterId == centerId && at.AssignmentId == assignmentId && at.StudentId == currentUserId, cancellationToken);

        if (!isTarget)
        {
            return SaveAssignmentDraftResult.Failure(ErrorCodes.ForbiddenResource);
        }

        var progress = await _dbContext.StudentAssignmentProgresses
            .FirstOrDefaultAsync(p =>
                p.CenterId == centerId &&
                p.StudentId == currentUserId &&
                p.AssignmentId == assignmentId &&
                !p.IsDeleted, cancellationToken);

        if (progress == null)
        {
            return SaveAssignmentDraftResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (progress.Status == ProgressStatus.Completed)
        {
            return SaveAssignmentDraftResult.Failure(ErrorCodes.AssignmentNotAvailable);
        }

        // Authoritative expiration check: reject saving new draft answers after deadline
        if (assignment.TimeLimitMinutes.HasValue && assignment.TimeLimitMinutes.Value > 0)
        {
            if (progress.StartedAt.HasValue)
            {
                var timeLimitExpiresAt = progress.StartedAt.Value.AddMinutes(assignment.TimeLimitMinutes.Value);
                var effectiveExpiresAt = assignment.DueAt.HasValue && assignment.DueAt.Value < timeLimitExpiresAt
                    ? assignment.DueAt.Value
                    : timeLimitExpiresAt;

                if (utcNow > effectiveExpiresAt)
                {
                    return SaveAssignmentDraftResult.Failure(ErrorCodes.AssignmentNotAvailable);
                }
            }
        }
        else if (assignment.DueAt.HasValue)
        {
            if (utcNow > assignment.DueAt.Value)
            {
                return SaveAssignmentDraftResult.Failure(ErrorCodes.AssignmentNotAvailable);
            }
        }

        progress.DraftAnswersJson = JsonSerializer.Serialize(request.Answers ?? new());
        progress.DraftSavedAt = utcNow;
        progress.UpdatedAt = utcNow;
        progress.UpdatedBy = currentUserId;

        if (progress.Status == ProgressStatus.NotStarted)
        {
            progress.Status = ProgressStatus.InProgress;
            progress.StartedAt ??= utcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return SaveAssignmentDraftResult.Success();
    }
}
