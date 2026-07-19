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

        var classStudents = await _dbContext.ClassStudents.AsNoTracking()
            .Where(cs => cs.CenterId == centerId && cs.StudentId == studentId && cs.Status == ClassStudentStatus.Active)
            .ToListAsync(cancellationToken);

        var classIds = classStudents.Select(cs => cs.ClassId).Distinct().ToList();
        
        var classes = new List<Class>();
        if (classIds.Any())
        {
            classes = await _dbContext.Classes.AsNoTracking()
                .Where(c => c.CenterId == centerId && !c.IsDeleted && c.Status == ClassStatus.Active && classIds.Contains(c.ClassId))
                .ToListAsync(cancellationToken);
        }

        if (isTeacher)
        {
            classes = classes.Where(c => c.TeacherId == userId).ToList();
        }

        var validClassIds = classes.Select(c => c.ClassId).ToList();
        
        var studentCounts = new Dictionary<Guid, int>();
        if (validClassIds.Any())
        {
            studentCounts = await _dbContext.ClassStudents.AsNoTracking()
                .Where(cs => cs.CenterId == centerId && cs.Status == ClassStudentStatus.Active && validClassIds.Contains(cs.ClassId))
                .GroupBy(cs => cs.ClassId)
                .Select(g => new { ClassId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ClassId, x => x.Count, cancellationToken);
        }
        
        var subjectIds = classes.Select(c => c.SubjectId).Distinct().ToList();
        var teacherIds = classes.Select(c => c.TeacherId).Distinct().ToList();
        
        // We use string mapping if not found, since tests might not seed subjects/teachers
        var subjectDict = new Dictionary<Guid, string>();
        if (subjectIds.Any())
        {
            var subjects = await _dbContext.Subjects.AsNoTracking()
                .Where(s => subjectIds.Contains(s.SubjectId))
                .Select(s => new { s.SubjectId, s.SubjectName })
                .ToListAsync(cancellationToken);
            foreach(var s in subjects) subjectDict[s.SubjectId] = s.SubjectName;
        }
        
        var teacherDict = new Dictionary<Guid, string>();
        if (teacherIds.Any())
        {
            var teachers = await _dbContext.Teachers.AsNoTracking()
                .Include(t => t.User)
                .Where(t => teacherIds.Contains(t.TeacherId))
                .Select(t => new { t.TeacherId, t.User!.DisplayName })
                .ToListAsync(cancellationToken);
            foreach(var t in teachers) teacherDict[t.TeacherId] = t.DisplayName;
        }

        var classDtos = classes
            .OrderBy(c => c.ClassName)
            .ThenBy(c => c.ClassId)
            .Select(c => new ClassDto
            {
                ClassId = c.ClassId.ToString("D"),
                ClassName = c.ClassName,
                AcademicYear = c.AcademicYear,
                Status = c.Status.ToString(),
                RowVersion = c.RowVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Subject = new ClassSubjectDto
                {
                    SubjectId = c.SubjectId.ToString("D"),
                    SubjectName = subjectDict.TryGetValue(c.SubjectId, out var sName) ? sName : string.Empty
                },
                Teacher = new ClassTeacherDto
                {
                    TeacherId = c.TeacherId.ToString("D"),
                    DisplayName = teacherDict.TryGetValue(c.TeacherId, out var tName) ? tName : string.Empty
                },
                StudentCount = studentCounts.TryGetValue(c.ClassId, out var count) ? count : 0
            }).ToList();



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
