using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Organization;

public class DeleteTeacherUseCase : IDeleteTeacherUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public DeleteTeacherUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<DeleteTeacherResult> ExecuteAsync(Guid teacherId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue ||
            _tenantContext.UserId.Value == Guid.Empty ||
            !string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal) ||
            teacherId == Guid.Empty)
        {
            return DeleteTeacherResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerId = _tenantContext.CenterId.Value;

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == centerId, cancellationToken);

        if (center == null || center.IsDeleted || center.Status != CenterStatus.Active)
        {
            return DeleteTeacherResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var teacher = await _dbContext.Teachers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.TeacherId == teacherId &&
                t.CenterId == centerId &&
                !t.IsDeleted &&
                t.User != null &&
                t.User.CenterId == centerId &&
                !t.User.IsDeleted &&
                t.User.RoleName == UserRole.Teacher,
                cancellationToken);

        if (teacher == null)
        {
            return DeleteTeacherResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var hasActiveClass = await _dbContext.Classes
            .AnyAsync(c =>
                c.CenterId == centerId &&
                c.TeacherId == teacherId &&
                !c.IsDeleted &&
                c.Status == ClassStatus.Active,
                cancellationToken);

        if (hasActiveClass)
        {
            return DeleteTeacherResult.Failure(ErrorCodes.InvalidStateTransition);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var managerId = _tenantContext.UserId.Value;

        teacher.IsDeleted = true;
        teacher.DeletedAt = now;
        teacher.DeletedBy = managerId;
        teacher.UpdatedAt = now;
        teacher.UpdatedBy = managerId;

        teacher.User!.IsDeleted = true;
        teacher.User.DeletedAt = now;
        teacher.User.DeletedBy = managerId;
        teacher.User.UpdatedAt = now;
        teacher.User.UpdatedBy = managerId;
        teacher.User.Status = UserStatus.Disabled;
        teacher.User.AuthVersion = checked(teacher.User.AuthVersion + 1);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();
            return DeleteTeacherResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        return DeleteTeacherResult.Success();
    }
}
