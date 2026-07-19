using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Organization;

public class UpdateTeacherUseCase : IUpdateTeacherUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public UpdateTeacherUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<UpdateTeacherResult> ExecuteAsync(Guid teacherId, UpdateTeacherRequest request, CancellationToken cancellationToken = default)
    {
        if (teacherId == Guid.Empty ||
            !_tenantContext.IsResolved ||
            _tenantContext.CenterId == Guid.Empty ||
            _tenantContext.UserId == Guid.Empty ||
            !string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal))
        {
            return UpdateTeacherResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerId = _tenantContext.CenterId;

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == centerId, cancellationToken);

        if (center == null || center.IsDeleted || center.Status != CenterStatus.Active)
        {
            return UpdateTeacherResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var rawRowVersion = request.RowVersion;
        if (string.IsNullOrEmpty(rawRowVersion))
        {
            return UpdateTeacherResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (!ulong.TryParse(rawRowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsedRowVersion) || parsedRowVersion == 0)
        {
            return UpdateTeacherResult.Failure(ErrorCodes.ValidationFailed);
        }

        var displayName = request.DisplayName;
        var department = request.Department;

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 200 ||
            (department != null && department.Length > 150) ||
            !Enum.IsDefined(typeof(UserStatus), request.Status))
        {
            return UpdateTeacherResult.Failure(ErrorCodes.ValidationFailed);
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
            return UpdateTeacherResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (teacher.RowVersion != parsedRowVersion)
        {
            return UpdateTeacherResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        // Set original value for EF optimistic concurrency
        _dbContext.Entry(teacher).Property(t => t.RowVersion).OriginalValue = parsedRowVersion;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var managerId = _tenantContext.UserId;

        teacher.User!.DisplayName = displayName;
        teacher.User.Status = request.Status;
        teacher.User.UpdatedAt = now;
        teacher.User.UpdatedBy = managerId;

        teacher.Department = department;
        teacher.UpdatedAt = now;
        teacher.UpdatedBy = managerId;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();
            return UpdateTeacherResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        var classCount = await _dbContext.Classes
            .CountAsync(c => c.TeacherId == teacherId && c.CenterId == centerId && !c.IsDeleted && c.Status == ClassStatus.Active, cancellationToken);

        var dto = new TeacherDto
        {
            TeacherId = teacher.TeacherId.ToString("D").ToLowerInvariant(),
            Username = teacher.User.Username,
            DisplayName = teacher.User.DisplayName,
            Department = teacher.Department,
            Status = teacher.User.Status.ToString(),
            ClassCount = classCount,
            RowVersion = teacher.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return UpdateTeacherResult.Success(dto);
    }
}
