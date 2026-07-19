using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
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

public class UpdateStudentUseCase : IUpdateStudentUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IStudentOwnershipGuard _ownershipGuard;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UpdateStudentUseCase> _logger;

    public UpdateStudentUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IStudentOwnershipGuard ownershipGuard,
        TimeProvider timeProvider,
        ILogger<UpdateStudentUseCase> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _ownershipGuard = ownershipGuard;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<UpdateStudentResult> ExecuteAsync(Guid studentId, UpdateStudentRequest request)
    {
        if (studentId == Guid.Empty)
        {
            return UpdateStudentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!_tenantContext.IsResolved || _tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty || _tenantContext.UserId == null || _tenantContext.UserId == Guid.Empty)
        {
            return UpdateStudentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (string.IsNullOrWhiteSpace(_tenantContext.Role))
        {
            return UpdateStudentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!Enum.TryParse<UserRole>(_tenantContext.Role, false, out var roleEnum) ||
            (roleEnum != UserRole.Teacher && roleEnum != UserRole.CenterManager))
        {
            return UpdateStudentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerExists = await _dbContext.Centers
            .AnyAsync(c => c.CenterId == _tenantContext.CenterId && c.Status == CenterStatus.Active);

        if (!centerExists)
        {
            return UpdateStudentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var validationContext = new ValidationContext(request);
        var validationResults = new System.Collections.Generic.List<ValidationResult>();
        if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
        {
            return UpdateStudentResult.Failure(ErrorCodes.ValidationFailed);
        }

        var ownershipDecision = await _ownershipGuard.CheckStudentAccessAsync(studentId, default);
        switch (ownershipDecision)
        {
            case OwnershipDecision.Allowed:
                break;
            case OwnershipDecision.Forbidden:
                return UpdateStudentResult.Failure(ErrorCodes.ForbiddenResource);
            case OwnershipDecision.NotFound:
            default:
                return UpdateStudentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var student = await _dbContext.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s =>
                s.StudentId == studentId &&
                s.CenterId == _tenantContext.CenterId.Value &&
                !s.IsDeleted &&
                s.User != null &&
                s.User.CenterId == _tenantContext.CenterId.Value &&
                !s.User.IsDeleted &&
                s.User.RoleName == UserRole.Student);

        if (student == null)
        {
            return UpdateStudentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!ulong.TryParse(request.RowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedRowVersion))
        {
            return UpdateStudentResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (student.RowVersion != parsedRowVersion)
        {
            return UpdateStudentResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        _dbContext.Entry(student).Property(s => s.RowVersion).OriginalValue = parsedRowVersion;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var actorId = _tenantContext.UserId.Value;

        student.User.DisplayName = request.FullName.Trim();
        student.User.Status = request.Status!.Value;
        student.User.UpdatedAt = now;
        student.User.UpdatedBy = actorId;

        student.FullName = request.FullName.Trim();
        student.GradeLevel = (byte)request.GradeLevel;
        student.UpdatedAt = now;
        student.UpdatedBy = actorId;

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict on student {StudentId}", studentId);
            _dbContext.ChangeTracker.Clear();
            return UpdateStudentResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        var classCountQuery = _dbContext.ClassStudents
            .Include(cs => cs.Class)
            .Where(cs =>
                cs.StudentId == studentId &&
                cs.Status == ClassStudentStatus.Active &&
                cs.Class.CenterId == _tenantContext.CenterId.Value &&
                cs.Class.Status == ClassStatus.Active &&
                !cs.Class.IsDeleted);

        if (roleEnum == UserRole.Teacher)
        {
            classCountQuery = classCountQuery.Where(cs => cs.Class.TeacherId == _tenantContext.UserId.Value);
        }

        var activeClassCount = await classCountQuery.CountAsync();

        var dto = new StudentDto
        {
            StudentId = student.StudentId,
            Username = student.User.Username,
            FullName = student.FullName,
            GradeLevel = student.GradeLevel,
            Status = student.User.Status.ToString(),
            ActiveClassCount = activeClassCount,
            RowVersion = student.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return UpdateStudentResult.Success(dto);
    }
}
