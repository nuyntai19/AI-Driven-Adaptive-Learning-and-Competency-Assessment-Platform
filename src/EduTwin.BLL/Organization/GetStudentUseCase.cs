using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Organization;

public class GetStudentUseCase : IGetStudentUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IStudentOwnershipGuard _studentOwnershipGuard;

    public GetStudentUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IStudentOwnershipGuard studentOwnershipGuard)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _studentOwnershipGuard = studentOwnershipGuard;
    }

    public async Task<GetStudentResult> ExecuteAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        if (studentId == Guid.Empty)
            return GetStudentResult.Failure(ErrorCodes.ResourceNotFound);

        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue ||
            _tenantContext.UserId.Value == Guid.Empty ||
            (!string.Equals(_tenantContext.Role, nameof(UserRole.Student), StringComparison.Ordinal) &&
             !string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal) &&
             !string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal)))
        {
            return GetStudentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerId = _tenantContext.CenterId.Value;
        var userId = _tenantContext.UserId.Value;
        var isTeacher = string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal);

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == centerId, cancellationToken);

        if (center == null || center.IsDeleted || center.Status != CenterStatus.Active)
            return GetStudentResult.Failure(ErrorCodes.ResourceNotFound);

        var ownership = await _studentOwnershipGuard.CheckStudentAccessAsync(studentId, cancellationToken);
        if (ownership == OwnershipDecision.NotFound)
            return GetStudentResult.Failure(ErrorCodes.ResourceNotFound);
        if (ownership == OwnershipDecision.Forbidden)
            return GetStudentResult.Failure(ErrorCodes.ForbiddenResource);
        if (ownership != OwnershipDecision.Allowed)
            return GetStudentResult.Failure(ErrorCodes.ResourceNotFound);

        var studentEntity = await _dbContext.Students.AsNoTracking()
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

        if (studentEntity == null)
            return GetStudentResult.Failure(ErrorCodes.ResourceNotFound);

        var classesQuery = _dbContext.ClassStudents.AsNoTracking()
            .Where(cs => cs.CenterId == centerId &&
                         cs.StudentId == studentId &&
                         cs.Status == ClassStudentStatus.Active &&
                         cs.Class != null &&
                         cs.Class.CenterId == centerId &&
                         !cs.Class.IsDeleted &&
                         cs.Class.Status == ClassStatus.Active &&
                         cs.Class.Subject != null &&
                         cs.Class.Subject.CenterId == centerId &&
                         !cs.Class.Subject.IsDeleted &&
                         cs.Class.Teacher != null &&
                         cs.Class.Teacher.CenterId == centerId &&
                         !cs.Class.Teacher.IsDeleted &&
                         cs.Class.Teacher.User != null &&
                         cs.Class.Teacher.User.CenterId == centerId &&
                         !cs.Class.Teacher.User.IsDeleted &&
                         cs.Class.Teacher.User.RoleName == UserRole.Teacher);

        if (isTeacher)
        {
            classesQuery = classesQuery.Where(cs => cs.Class!.TeacherId == userId);
        }

        var classDtos = await classesQuery
            .Select(cs => new
            {
                Class = cs.Class!,
                SubjectName = cs.Class!.Subject!.SubjectName,
                DisplayName = cs.Class!.Teacher!.User!.DisplayName,
                StudentCount = _dbContext.ClassStudents.Count(innerCs =>
                    innerCs.CenterId == centerId &&
                    innerCs.ClassId == cs.ClassId &&
                    innerCs.Status == ClassStudentStatus.Active)
            })
            .OrderBy(x => x.Class.ClassName)
            .ThenBy(x => x.Class.ClassId)
            .Select(x => new ClassDto
            {
                ClassId = x.Class.ClassId.ToString("D"),
                ClassName = x.Class.ClassName,
                AcademicYear = x.Class.AcademicYear,
                Status = x.Class.Status.ToString(),
                RowVersion = x.Class.RowVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Subject = new ClassSubjectDto
                {
                    SubjectId = x.Class.SubjectId.ToString("D"),
                    SubjectName = x.SubjectName
                },
                Teacher = new ClassTeacherDto
                {
                    TeacherId = x.Class.TeacherId.ToString("D"),
                    DisplayName = x.DisplayName
                },
                StudentCount = x.StudentCount
            })
            .ToListAsync(cancellationToken);

        var goals = await _dbContext.StudentSubjectGoals.AsNoTracking()
            .Where(g =>
                g.CenterId == centerId &&
                g.StudentId == studentId &&
                !g.IsDeleted)
            .OrderBy(g => g.SubjectId)
            .ThenBy(g => g.GoalId)
            .Select(g => new StudentSubjectGoalDto
            {
                GoalId = g.GoalId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StudentId = g.StudentId.ToString("D"),
                SubjectId = g.SubjectId.ToString("D"),
                TargetScore = g.TargetScore,
                RemainingDays = (int)g.RemainingDays,
                CurrentPredictedScore = g.CurrentPredictedScore,
                RiskScore = g.RiskScore,
                RowVersion = g.RowVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
            })
            .ToListAsync(cancellationToken);

        var studentDto = new StudentDetailDto
        {
            StudentId = studentEntity.StudentId.ToString("D"),
            Username = studentEntity.User!.Username,
            FullName = studentEntity.FullName,
            GradeLevel = studentEntity.GradeLevel,
            Status = studentEntity.User.Status.ToString(),
            ActiveClassCount = classDtos.Count,
            RowVersion = studentEntity.RowVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Classes = classDtos,
            SubjectGoals = goals
        };

        return GetStudentResult.Success(studentDto);
    }
}
