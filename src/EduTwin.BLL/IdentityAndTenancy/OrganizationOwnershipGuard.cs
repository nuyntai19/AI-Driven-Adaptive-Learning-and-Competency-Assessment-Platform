using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.DAL.Persistence;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using System.Linq;

namespace EduTwin.BLL.IdentityAndTenancy;

public class OrganizationOwnershipGuard : ITeacherOwnershipGuard, IClassOwnershipGuard, IStudentOwnershipGuard
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public OrganizationOwnershipGuard(EduTwinDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    private bool IsFailClosed(Guid targetId)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue ||
            _tenantContext.UserId.Value == Guid.Empty ||
            targetId == Guid.Empty)
        {
            return true;
        }

        var role = _tenantContext.Role;
        if (string.IsNullOrWhiteSpace(role))
        {
            return true;
        }

        if (role != nameof(UserRole.Student) &&
            role != nameof(UserRole.Teacher) &&
            role != nameof(UserRole.CenterManager))
        {
            return true;
        }

        return false;
    }

    public async Task<OwnershipDecision> CheckTeacherAccessAsync(Guid teacherId, CancellationToken cancellationToken)
    {
        if (IsFailClosed(teacherId)) return OwnershipDecision.NotFound;

        var exists = await _dbContext.Teachers
            .AsNoTracking()
            .AnyAsync(t => t.TeacherId == teacherId, cancellationToken);

        if (!exists) return OwnershipDecision.NotFound;

        if (_tenantContext.Role == nameof(UserRole.CenterManager))
            return OwnershipDecision.Allowed;

        if (_tenantContext.Role == nameof(UserRole.Teacher))
        {
            if (teacherId == _tenantContext.UserId)
                return OwnershipDecision.Allowed;
            else
                return OwnershipDecision.Forbidden;
        }

        // Student role and others
        return OwnershipDecision.Forbidden;
    }

    public async Task<OwnershipDecision> CheckClassAccessAsync(Guid classId, CancellationToken cancellationToken)
    {
        if (IsFailClosed(classId)) return OwnershipDecision.NotFound;

        var classEntity = await _dbContext.Classes
            .AsNoTracking()
            .Where(c => c.ClassId == classId)
            .Select(c => new { c.ClassId, c.TeacherId })
            .FirstOrDefaultAsync(cancellationToken);

        if (classEntity == null) return OwnershipDecision.NotFound;

        if (_tenantContext.Role == nameof(UserRole.CenterManager))
            return OwnershipDecision.Allowed;

        if (_tenantContext.Role == nameof(UserRole.Teacher))
        {
            if (classEntity.TeacherId == _tenantContext.UserId)
                return OwnershipDecision.Allowed;
            else
                return OwnershipDecision.Forbidden;
        }

        // Student role and others
        return OwnershipDecision.Forbidden;
    }

    public async Task<OwnershipDecision> CheckStudentAccessAsync(Guid studentId, CancellationToken cancellationToken)
    {
        if (IsFailClosed(studentId)) return OwnershipDecision.NotFound;

        var exists = await _dbContext.Students
            .AsNoTracking()
            .AnyAsync(s => s.StudentId == studentId, cancellationToken);

        if (!exists) return OwnershipDecision.NotFound;

        if (_tenantContext.Role == nameof(UserRole.CenterManager))
            return OwnershipDecision.Allowed;

        if (_tenantContext.Role == nameof(UserRole.Student))
        {
            if (studentId == _tenantContext.UserId)
                return OwnershipDecision.Allowed;
            else
                return OwnershipDecision.Forbidden;
        }

        if (_tenantContext.Role == nameof(UserRole.Teacher))
        {
            var hasAccess = await _dbContext.ClassStudents
                .AsNoTracking()
                .AnyAsync(cs =>
                    cs.StudentId == studentId &&
                    cs.Status == ClassStudentStatus.Active &&
                    cs.Class != null &&
                    cs.Class.TeacherId == _tenantContext.UserId &&
                    cs.Class.Status == ClassStatus.Active,
                    cancellationToken);

            if (hasAccess)
                return OwnershipDecision.Allowed;
            else
                return OwnershipDecision.Forbidden;
        }

        return OwnershipDecision.Forbidden;
    }
}
