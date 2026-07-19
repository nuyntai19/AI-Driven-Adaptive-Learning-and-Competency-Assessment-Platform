using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Organization;

public class CreateTeacherUseCase : ICreateTeacherUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;
    private readonly IPasswordHasher<User> _passwordHasher;

    public CreateTeacherUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider,
        IPasswordHasher<User> passwordHasher)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
        _passwordHasher = passwordHasher;
    }

    public async Task<CreateTeacherResult> ExecuteAsync(CreateTeacherRequest request, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty)
        {
            return CreateTeacherResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (_tenantContext.UserId == null || _tenantContext.UserId == Guid.Empty)
        {
            return CreateTeacherResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (_tenantContext.Role != nameof(UserRole.CenterManager))
        {
            return CreateTeacherResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var username = request.Username;
        var displayName = request.DisplayName;
        var department = request.Department;
        var rawPassword = request.TemporaryPassword;

        if (string.IsNullOrWhiteSpace(username) || username.Length > 100 ||
            string.IsNullOrWhiteSpace(displayName) || displayName.Length > 200 ||
            (department != null && department.Length > 150) ||
            string.IsNullOrWhiteSpace(rawPassword) || rawPassword.Length < 12 || rawPassword.Length > 200)
        {
            return CreateTeacherResult.Failure(ErrorCodes.ValidationFailed);
        }

        var centerId = _tenantContext.CenterId.Value;

        var centerExists = await _dbContext.Centers
            .AnyAsync(c => c.CenterId == centerId && !c.IsDeleted && c.Status == CenterStatus.Active, cancellationToken);

        if (!centerExists)
        {
            return CreateTeacherResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var duplicate = await _dbContext.Users
            .AnyAsync(u => u.CenterId == centerId && u.Username == username, cancellationToken);

        if (duplicate)
        {
            return CreateTeacherResult.Failure(ErrorCodes.DuplicateResource);
        }

        var teacherId = Guid.NewGuid();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var managerId = _tenantContext.UserId.Value;

        var user = new User
        {
            UserId = teacherId,
            CenterId = centerId,
            Username = username,
            RoleName = UserRole.Teacher,
            DisplayName = displayName,
            Status = UserStatus.Active,
            AuthVersion = 1,
            LastLoginAt = null,
            IsDeleted = false,
            CreatedAt = now,
            CreatedBy = managerId,
            UpdatedAt = now,
            UpdatedBy = managerId
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, rawPassword);

        var teacher = new Teacher
        {
            TeacherId = teacherId,
            CenterId = centerId,
            Department = department,
            Bio = null,
            IsDeleted = false,
            CreatedAt = now,
            CreatedBy = managerId,
            UpdatedAt = now,
            UpdatedBy = managerId,
            User = user
        };

        _dbContext.Users.Add(user);
        _dbContext.Teachers.Add(teacher);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();
            var exists = await _dbContext.Users
                .AsNoTracking()
                .AnyAsync(u => u.CenterId == centerId && u.Username == username, cancellationToken);
            if (exists)
            {
                return CreateTeacherResult.Failure(ErrorCodes.DuplicateResource);
            }
            throw;
        }

        var dto = new TeacherDto
        {
            TeacherId = teacher.TeacherId.ToString("D").ToLowerInvariant(),
            Username = user.Username,
            DisplayName = user.DisplayName,
            Department = teacher.Department,
            Status = user.Status.ToString(),
            ClassCount = 0,
            RowVersion = teacher.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return CreateTeacherResult.Success(dto);
    }
}
