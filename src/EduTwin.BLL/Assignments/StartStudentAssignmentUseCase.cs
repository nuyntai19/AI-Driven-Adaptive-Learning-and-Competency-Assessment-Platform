using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.DAL;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.Persistence;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.Assignments;

public class StartStudentAssignmentUseCase : IStartStudentAssignmentUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IGetStudentAssignmentUseCase _getStudentAssignmentUseCase;
    private readonly TimeProvider _timeProvider;

    public StartStudentAssignmentUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IGetStudentAssignmentUseCase getStudentAssignmentUseCase,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _getStudentAssignmentUseCase = getStudentAssignmentUseCase;
        _timeProvider = timeProvider;
    }

    public async Task<GetStudentAssignmentResult> ExecuteAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            !string.Equals(_tenantContext.Role, nameof(UserRole.Student), StringComparison.Ordinal))
        {
            return GetStudentAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var currentUserId = _tenantContext.UserId;
        var centerId = _tenantContext.CenterId;
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        // 1. Verify assignment exists and is Published
        var assignment = await _dbContext.Assignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CenterId == centerId && a.AssignmentId == assignmentId && !a.IsDeleted, cancellationToken);

        if (assignment == null)
        {
            return GetStudentAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (assignment.Status != AssignmentStatus.Published)
        {
            return GetStudentAssignmentResult.Failure(ErrorCodes.AssignmentNotAvailable);
        }

        if (assignment.DueAt.HasValue && utcNow > assignment.DueAt.Value)
        {
            return GetStudentAssignmentResult.Failure(ErrorCodes.AssignmentNotAvailable);
        }

        // 2. Verify student is in AssignmentTargets
        var isTarget = await _dbContext.AssignmentTargets
            .AsNoTracking()
            .AnyAsync(at => at.CenterId == centerId && at.AssignmentId == assignmentId && at.StudentId == currentUserId, cancellationToken);

        if (!isTarget)
        {
            return GetStudentAssignmentResult.Failure(ErrorCodes.ForbiddenResource);
        }

        // 3. Load student progress
        var progress = await _dbContext.StudentAssignmentProgresses
            .FirstOrDefaultAsync(p =>
                p.CenterId == centerId &&
                p.StudentId == currentUserId &&
                p.AssignmentId == assignmentId &&
                !p.IsDeleted, cancellationToken);

        if (progress == null)
        {
            return GetStudentAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (progress.Status == ProgressStatus.Completed)
        {
            return GetStudentAssignmentResult.Failure(ErrorCodes.AssignmentNotAvailable);
        }

        if (progress.StartedAt.HasValue && assignment.TimeLimitMinutes.HasValue && assignment.TimeLimitMinutes.Value > 0)
        {
            var timeLimitExpiresAt = progress.StartedAt.Value.AddMinutes(assignment.TimeLimitMinutes.Value);
            var effectiveExpiresAt = assignment.DueAt.HasValue && assignment.DueAt.Value < timeLimitExpiresAt
                ? assignment.DueAt.Value
                : timeLimitExpiresAt;

            if (utcNow > effectiveExpiresAt)
            {
                return GetStudentAssignmentResult.Failure(ErrorCodes.AssignmentNotAvailable);
            }
        }

        // 4. Idempotent timer start: never reset StartedAt if already started
        if (progress.StartedAt == null)
        {
            progress.StartedAt = utcNow;
            if (progress.Status == ProgressStatus.NotStarted)
            {
                progress.Status = ProgressStatus.InProgress;
            }
            progress.UpdatedAt = utcNow;
            progress.RowVersion++;
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Concurrency conflict: another concurrent request already started the assignment.
                // Reload from database to retrieve the authoritative StartedAt.
                await _dbContext.Entry(progress).ReloadAsync(cancellationToken);
            }
        }

        return await _getStudentAssignmentUseCase.ExecuteAsync(assignmentId, cancellationToken);
    }
}
