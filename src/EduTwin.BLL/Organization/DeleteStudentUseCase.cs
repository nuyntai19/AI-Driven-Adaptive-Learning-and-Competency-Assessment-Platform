using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EduTwin.BLL.Organization;

public class DeleteStudentUseCase : IDeleteStudentUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DeleteStudentUseCase> _logger;

    public DeleteStudentUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider,
        ILogger<DeleteStudentUseCase> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<DeleteStudentResult> ExecuteAsync(
        Guid studentId,
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue ||
            _tenantContext.UserId.Value == Guid.Empty ||
            !string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal) ||
            studentId == Guid.Empty)
        {
            return DeleteStudentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerId = _tenantContext.CenterId.Value;

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == centerId, cancellationToken);

        if (center == null || center.IsDeleted || center.Status != CenterStatus.Active)
        {
            return DeleteStudentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var student = await _dbContext.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s =>
                s.StudentId == studentId &&
                s.CenterId == centerId &&
                !s.IsDeleted &&
                s.User != null &&
                s.User.CenterId == centerId &&
                !s.User.IsDeleted &&
                s.User.RoleName == UserRole.Student,
                cancellationToken);

        if (student == null)
        {
            return DeleteStudentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var managerId = _tenantContext.UserId.Value;

        student.IsDeleted = true;
        student.DeletedAt = now;
        student.DeletedBy = managerId;
        student.UpdatedAt = now;
        student.UpdatedBy = managerId;

        student.User!.IsDeleted = true;
        student.User.DeletedAt = now;
        student.User.DeletedBy = managerId;
        student.User.UpdatedAt = now;
        student.User.UpdatedBy = managerId;
        student.User.Status = UserStatus.Disabled;
        student.User.AuthVersion = checked(student.User.AuthVersion + 1);
        student.User.RowVersion = checked(student.User.RowVersion + 1);

        // Transition active class memberships to Removed status
        var activeMemberships = await _dbContext.ClassStudents
            .Where(cs => cs.CenterId == centerId && cs.StudentId == studentId && cs.Status == ClassStudentStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var membership in activeMemberships)
        {
            membership.Status = ClassStudentStatus.Removed;
            membership.RemovedAt = now;
        }

        // Revoke active refresh tokens for the student user
        var refreshTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.CenterId == centerId && rt.UserId == student.StudentId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in refreshTokens)
        {
            token.RevokedAt = now;
            token.RevokeReason = "Student account soft-deleted by CenterManager.";
        }

        // Append authorization audit log (preserving zero credentials)
        _dbContext.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
        {
            CenterId = centerId,
            ActorUserId = managerId,
            ActionType = "StudentDeleted",
            TargetType = "Student",
            TargetId = studentId.ToString("D"),
            TargetUserId = student.StudentId,
            BeforeData = JsonSerializer.Serialize(new { StudentId = studentId, UserId = student.StudentId, GradeLevel = student.GradeLevel }),
            AfterData = JsonSerializer.Serialize(new { IsDeleted = true, UserStatus = nameof(UserStatus.Disabled), ActiveMembershipsRemoved = activeMemberships.Count }),
            Reason = "Student account soft-deleted by CenterManager.",
            TraceId = traceId ?? string.Empty,
            CreatedAt = now,
            CreatedBy = managerId
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict when soft-deleting student {StudentId}", studentId);
            _dbContext.ChangeTracker.Clear();
            return DeleteStudentResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        return DeleteStudentResult.Success();
    }
}
