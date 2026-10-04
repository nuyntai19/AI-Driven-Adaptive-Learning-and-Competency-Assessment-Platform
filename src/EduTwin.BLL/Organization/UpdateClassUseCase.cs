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
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Organization;

public class UpdateClassUseCase : IUpdateClassUseCase
{
    private readonly EduTwinDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UpdateClassUseCase> _logger;

    public UpdateClassUseCase(
        EduTwinDbContext context,
        ITenantContext tenantContext,
        TimeProvider timeProvider,
        ILogger<UpdateClassUseCase> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<UpdateClassResult> ExecuteAsync(Guid classId, UpdateClassRequest request, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved ||
            _tenantContext.CenterId == null ||
            _tenantContext.UserId == null ||
            _tenantContext.CenterId == Guid.Empty ||
            _tenantContext.UserId == Guid.Empty)
        {
            return UpdateClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (_tenantContext.Role != nameof(UserRole.CenterManager))
        {
            return UpdateClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerId = _tenantContext.CenterId.Value;
        var currentUserId = _tenantContext.UserId.Value;

        if (classId == Guid.Empty || request.TeacherId == Guid.Empty)
        {
            return UpdateClassResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (string.IsNullOrWhiteSpace(request.ClassName) || request.ClassName.Length > 150)
        {
            return UpdateClassResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (!request.Status.HasValue || !Enum.IsDefined(typeof(ClassStatus), request.Status.Value))
        {
            return UpdateClassResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (request.GradeLevel.HasValue && (request.GradeLevel.Value < 10 || request.GradeLevel.Value > 12))
        {
            return UpdateClassResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (string.IsNullOrWhiteSpace(request.RowVersion) ||
            !ulong.TryParse(request.RowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var expectedRowVersion) ||
            expectedRowVersion == 0)
        {
            return UpdateClassResult.Failure(ErrorCodes.ValidationFailed);
        }

        var className = request.ClassName.Trim();

        var existingClass = await _context.Classes
            .Where(c => c.ClassId == classId && c.CenterId == centerId && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingClass == null)
        {
            return UpdateClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerAndTeacher = await _context.Centers
            .AsNoTracking()
            .Where(c => c.CenterId == centerId && c.Status == CenterStatus.Active && !c.IsDeleted)
            .Select(c => new
            {
                Teacher = _context.Teachers
                    .Where(t => t.TeacherId == request.TeacherId && t.CenterId == centerId && !t.IsDeleted &&
                                t.User != null && t.User.CenterId == centerId && !t.User.IsDeleted && t.User.RoleName == UserRole.Teacher)
                    .Select(t => new { t.TeacherId, UserDisplayName = t.User.DisplayName })
                    .FirstOrDefault(),
                Subject = _context.Subjects
                    .Where(s => s.SubjectId == existingClass.SubjectId && s.CenterId == centerId && !s.IsDeleted)
                    .Select(s => new { s.SubjectId, s.SubjectName })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (centerAndTeacher == null || centerAndTeacher.Teacher == null || centerAndTeacher.Subject == null)
        {
            return UpdateClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (existingClass.RowVersion != expectedRowVersion)
        {
            return UpdateClassResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        var duplicate = await _context.Classes
            .AnyAsync(c => c.CenterId == centerId && c.ClassId != classId && c.ClassName == className && c.AcademicYear == existingClass.AcademicYear, cancellationToken);

        if (duplicate)
        {
            return UpdateClassResult.Failure(ErrorCodes.DuplicateResource);
        }

        var studentCount = await _context.ClassStudents
            .Where(cs => cs.CenterId == centerId && cs.ClassId == classId && cs.Status == ClassStudentStatus.Active)
            .CountAsync(cancellationToken);

        if (existingClass.GradeLevel != request.GradeLevel)
        {
            // 1. Check attached Curricula
            var attachedCurricula = await _context.CurriculumClasses
                .Where(cc => cc.ClassId == classId && cc.CenterId == centerId)
                .Join(_context.Curriculums.Where(c => !c.IsDeleted),
                    cc => cc.CurriculumId,
                    c => c.CurriculumId,
                    (cc, c) => new { c.CurriculumId, c.GradeLevel })
                .ToListAsync(cancellationToken);

            if (attachedCurricula.Count > 0)
            {
                if (!request.GradeLevel.HasValue)
                {
                    if (attachedCurricula.Any(c => c.GradeLevel.HasValue))
                    {
                        return UpdateClassResult.Failure(ErrorCodes.InvalidStateTransition);
                    }
                }
                else
                {
                    if (attachedCurricula.Any(c => c.GradeLevel.HasValue && c.GradeLevel.Value != request.GradeLevel.Value))
                    {
                        return UpdateClassResult.Failure(ErrorCodes.InvalidStateTransition);
                    }
                }
            }

            // 2. Check attached Assignments
            var assignmentsWithGradeCheck = await _context.Assignments
                .Where(a => a.ClassId == classId && a.CenterId == centerId && !a.IsDeleted && !a.AllowGradeMismatch)
                .Select(a => new
                {
                    a.AssignmentId,
                    QuestionGrades = _context.AssignmentQuestions
                        .Where(aq => aq.AssignmentId == a.AssignmentId)
                        .Join(_context.Questions, aq => aq.QuestionId, q => q.QuestionId, (aq, q) => q.GradeLevel)
                        .Where(g => g.HasValue)
                        .Select(g => g!.Value)
                        .ToList()
                })
                .ToListAsync(cancellationToken);

            foreach (var a in assignmentsWithGradeCheck)
            {
                if (!request.GradeLevel.HasValue)
                {
                    if (a.QuestionGrades.Count > 0)
                    {
                        return UpdateClassResult.Failure(ErrorCodes.InvalidStateTransition);
                    }
                }
                else
                {
                    if (a.QuestionGrades.Any(g => g != request.GradeLevel.Value))
                    {
                        return UpdateClassResult.Failure(ErrorCodes.InvalidStateTransition);
                    }
                }
            }

            // 3. Check active enrolled students in the class
            if (request.GradeLevel.HasValue)
            {
                var hasStudentGradeMismatch = await _context.ClassStudents
                    .Where(cs => cs.ClassId == classId && cs.CenterId == centerId && cs.Status == ClassStudentStatus.Active)
                    .Join(_context.Students.Where(s => !s.IsDeleted),
                        cs => cs.StudentId,
                        s => s.StudentId,
                        (cs, s) => s.GradeLevel)
                    .AnyAsync(g => g != request.GradeLevel.Value, cancellationToken);

                if (hasStudentGradeMismatch)
                {
                    return UpdateClassResult.Failure(ErrorCodes.InvalidStateTransition);
                }
            }
            else
            {
                var hasActiveStudents = await _context.ClassStudents
                    .AnyAsync(cs => cs.ClassId == classId && cs.CenterId == centerId && cs.Status == ClassStudentStatus.Active, cancellationToken);

                if (hasActiveStudents)
                {
                    return UpdateClassResult.Failure(ErrorCodes.InvalidStateTransition);
                }
            }
        }

        _context.Entry(existingClass).Property(c => c.RowVersion).OriginalValue = expectedRowVersion;

        existingClass.ClassName = className;
        existingClass.TeacherId = request.TeacherId;
        existingClass.Status = request.Status.Value;
        existingClass.GradeLevel = request.GradeLevel;
        existingClass.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        existingClass.UpdatedBy = currentUserId;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            return UpdateClassResult.Failure(ErrorCodes.ConcurrencyConflict);
        }
        catch (DbUpdateException ex)
        {
            _context.ChangeTracker.Clear();
            if (IsUniqueConstraintViolation(ex))
            {
                return UpdateClassResult.Failure(ErrorCodes.DuplicateResource);
            }
            throw;
        }

        var dto = new ClassDto
        {
            ClassId = existingClass.ClassId.ToString().ToLowerInvariant(),
            ClassName = existingClass.ClassName,
            AcademicYear = existingClass.AcademicYear,
            GradeLevel = existingClass.GradeLevel,
            Subject = new ClassSubjectDto
            {
                SubjectId = existingClass.SubjectId.ToString().ToLowerInvariant(),
                SubjectName = centerAndTeacher.Subject.SubjectName
            },
            Teacher = new ClassTeacherDto
            {
                TeacherId = existingClass.TeacherId.ToString().ToLowerInvariant(),
                DisplayName = centerAndTeacher.Teacher.UserDisplayName ?? string.Empty
            },
            Status = existingClass.Status.ToString(),
            RowVersion = existingClass.RowVersion.ToString(CultureInfo.InvariantCulture),
            StudentCount = studentCount
        };

        return UpdateClassResult.Success(dto);
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
