using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning.Attachments;

/// <summary>
/// Enforces fail-closed tenant and role boundaries on attempts.
/// - CenterManager: allowed within tenant.
/// - Student: allowed only for own attempts.
/// - Teacher: strictly requires non-null AssignmentId, targeted assignment, and matching Class.TeacherId.
///            Free-practice attempts are unconditionally rejected (Fail-Closed).
/// </summary>
public sealed class AttemptTeacherReviewScopeGuard : IAttemptTeacherReviewScopeGuard
{
    private readonly EduTwinDbContext _dbContext;

    public AttemptTeacherReviewScopeGuard(EduTwinDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<bool> CanAccessAttemptAsync(
        Guid centerId,
        Guid actorUserId,
        string role,
        ulong attemptId,
        CancellationToken cancellationToken = default)
    {
        if (centerId == Guid.Empty || actorUserId == Guid.Empty || attemptId == 0)
        {
            return false;
        }

        var attempt = await _dbContext.Attempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.CenterId == centerId && candidate.AttemptId == attemptId,
                cancellationToken);

        if (attempt is null)
        {
            return false;
        }

        return await CanAccessAttemptAsync(centerId, actorUserId, role, attempt, cancellationToken);
    }

    public async Task<bool> CanAccessAttemptAsync(
        Guid centerId,
        Guid actorUserId,
        string role,
        Attempt attempt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        if (centerId == Guid.Empty || actorUserId == Guid.Empty || attempt.CenterId != centerId)
        {
            return false;
        }

        if (string.Equals(role, nameof(UserRole.CenterManager), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(role, nameof(UserRole.Student), StringComparison.OrdinalIgnoreCase))
        {
            return attempt.StudentId == actorUserId;
        }

        if (string.Equals(role, nameof(UserRole.Teacher), StringComparison.OrdinalIgnoreCase))
        {
            // Free-practice attempts have no class ownership boundary and are NEVER visible/accessible to teachers
            if (!attempt.AssignmentId.HasValue)
            {
                return false;
            }

            var assignmentId = attempt.AssignmentId.Value;
            var studentId = attempt.StudentId;

            return await _dbContext.AssignmentTargets
                .AsNoTracking()
                .AnyAsync(target =>
                    target.CenterId == centerId &&
                    target.AssignmentId == assignmentId &&
                    target.StudentId == studentId &&
                    target.Assignment != null &&
                    target.Assignment.Class != null &&
                    target.Assignment.Class.TeacherId == actorUserId,
                    cancellationToken);
        }

        return false;
    }
}
