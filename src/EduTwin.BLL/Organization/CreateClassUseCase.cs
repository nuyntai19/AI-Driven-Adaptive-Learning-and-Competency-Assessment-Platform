using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Organization;

public class CreateClassUseCase : ICreateClassUseCase
{
    private readonly EduTwinDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CreateClassUseCase> _logger;

    public CreateClassUseCase(
        EduTwinDbContext context,
        ITenantContext tenantContext,
        TimeProvider timeProvider,
        ILogger<CreateClassUseCase> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<CreateClassResult> ExecuteAsync(CreateClassRequest request, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved ||
            _tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty ||
            _tenantContext.UserId == null || _tenantContext.UserId == Guid.Empty ||
            _tenantContext.Role != nameof(UserRole.CenterManager))
        {
            _logger.LogWarning("Invalid tenant context or role.");
            return CreateClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerId = _tenantContext.CenterId.Value;
        var userId = _tenantContext.UserId.Value;

        if (request.SubjectId == Guid.Empty || request.TeacherId == Guid.Empty)
        {
            return CreateClassResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (string.IsNullOrWhiteSpace(request.ClassName) || string.IsNullOrWhiteSpace(request.AcademicYear))
        {
            return CreateClassResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (request.ClassName.Length > 150 || request.AcademicYear.Length > 20)
        {
            return CreateClassResult.Failure(ErrorCodes.ValidationFailed);
        }

        var className = request.ClassName.Trim();
        var academicYear = request.AcademicYear.Trim();

        // Validate Subject and Teacher
        var subjectAndTeacher = await _context.Centers
            .AsNoTracking()
            .Where(c => c.CenterId == centerId && c.Status == CenterStatus.Active && !c.IsDeleted)
            .Select(c => new
            {
                Subject = _context.Subjects
                    .Where(s => s.SubjectId == request.SubjectId && s.CenterId == centerId && !s.IsDeleted)
                    .Select(s => new { s.SubjectId, s.SubjectName })
                    .FirstOrDefault(),
                Teacher = _context.Teachers
                    .Where(t => t.TeacherId == request.TeacherId && t.CenterId == centerId && !t.IsDeleted &&
                                t.User != null && t.User.CenterId == centerId && !t.User.IsDeleted && t.User.RoleName == UserRole.Teacher)
                    .Select(t => new { t.TeacherId, UserDisplayName = t.User.DisplayName })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (subjectAndTeacher == null || subjectAndTeacher.Subject == null || subjectAndTeacher.Teacher == null)
        {
            return CreateClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        // Check duplicates pre-emptively
        var duplicate = await _context.Classes
            .AnyAsync(c => c.CenterId == centerId && c.ClassName == className && c.AcademicYear == academicYear, cancellationToken);

        if (duplicate)
        {
            return CreateClassResult.Failure(ErrorCodes.DuplicateResource);
        }

        var classId = Guid.NewGuid();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var newClass = new Class
        {
            ClassId = classId,
            CenterId = centerId,
            ClassName = className,
            AcademicYear = academicYear,
            Status = ClassStatus.Active,
            SubjectId = request.SubjectId,
            TeacherId = request.TeacherId,
            IsDeleted = false,
            RowVersion = 1,
            CreatedAt = now,
            CreatedBy = userId,
            UpdatedAt = now,
            UpdatedBy = userId
        };

        _context.Classes.Add(newClass);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _context.ChangeTracker.Clear();
            if (IsUniqueConstraintViolation(ex))
            {
                return CreateClassResult.Failure(ErrorCodes.DuplicateResource);
            }
            throw;
        }

        var dto = new ClassDto
        {
            ClassId = newClass.ClassId.ToString("D").ToLowerInvariant(),
            ClassName = newClass.ClassName,
            AcademicYear = newClass.AcademicYear,
            Subject = new ClassSubjectDto
            {
                SubjectId = subjectAndTeacher.Subject.SubjectId.ToString("D").ToLowerInvariant(),
                SubjectName = subjectAndTeacher.Subject.SubjectName
            },
            Teacher = new ClassTeacherDto
            {
                TeacherId = subjectAndTeacher.Teacher.TeacherId.ToString("D").ToLowerInvariant(),
                DisplayName = subjectAndTeacher.Teacher.UserDisplayName
            },
            Status = newClass.Status.ToString(),
            StudentCount = 0,
            RowVersion = newClass.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return CreateClassResult.Success(dto);
    }

    private static bool IsUniqueConstraintViolation(Exception ex)
    {
        var current = ex;
        while (current != null)
        {
            if (current.Message.Contains("ux_classes_center_id_class_name_academic_year", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            current = current.InnerException;
        }
        return false;
    }
}
