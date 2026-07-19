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

public class ListStudentsUseCase : IListStudentsUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IClassOwnershipGuard _classOwnershipGuard;
    public ListStudentsUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IClassOwnershipGuard classOwnershipGuard)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _classOwnershipGuard = classOwnershipGuard;
    }

    public async Task<ListStudentsResult> ExecuteAsync(StudentListQuery query, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue ||
            _tenantContext.UserId.Value == Guid.Empty ||
            (!string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal) &&
             !string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal)))
        {
            return ListStudentsResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerId = _tenantContext.CenterId.Value;
        var userId = _tenantContext.UserId.Value;
        var isManager = string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal);

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == centerId, cancellationToken);

        if (center == null || center.IsDeleted || center.Status != CenterStatus.Active)
        {
            return ListStudentsResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (query.Page < 1 ||
            query.PageSize < 1 ||
            query.PageSize > 100 ||
            (query.Search != null && query.Search.Length > 200) ||
            (query.Status.HasValue && !Enum.IsDefined(typeof(UserStatus), query.Status.Value)) ||
            (query.GradeLevel.HasValue && (query.GradeLevel.Value < 10 || query.GradeLevel.Value > 12)) ||
            (query.ClassId.HasValue && query.ClassId.Value == Guid.Empty))
        {
            return ListStudentsResult.ValidationFailed();
        }

        var searchStr = query.Search?.Trim();
        if (string.IsNullOrWhiteSpace(searchStr))
        {
            searchStr = null;
        }

        if (query.ClassId.HasValue)
        {
            var ownership = await _classOwnershipGuard.CheckClassAccessAsync(query.ClassId.Value, cancellationToken);
            if (ownership == OwnershipDecision.NotFound)
                return ListStudentsResult.Failure(ErrorCodes.ResourceNotFound);
            if (ownership == OwnershipDecision.Forbidden)
                return ListStudentsResult.Failure(ErrorCodes.ForbiddenResource);
            if (ownership != OwnershipDecision.Allowed)
                return ListStudentsResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var studentQuery = _dbContext.Students.AsNoTracking()
            .Where(s => s.CenterId == centerId && !s.IsDeleted &&
                        s.User != null && s.User.CenterId == centerId && !s.User.IsDeleted &&
                        s.User.RoleName == UserRole.Student);

        if (searchStr != null)
        {
            studentQuery = studentQuery.Where(s =>
                s.User.Username.Contains(searchStr) ||
                s.FullName.Contains(searchStr));
        }

        if (query.Status.HasValue)
        {
            studentQuery = studentQuery.Where(s => s.User.Status == query.Status.Value);
        }

        if (query.GradeLevel.HasValue)
        {
            studentQuery = studentQuery.Where(s => s.GradeLevel == query.GradeLevel.Value);
        }

        if (!isManager)
        {
            if (query.ClassId.HasValue)
            {
                studentQuery = studentQuery.Where(s => _dbContext.ClassStudents.Any(cs =>
                    cs.CenterId == centerId &&
                    cs.StudentId == s.StudentId &&
                    cs.ClassId == query.ClassId.Value &&
                    cs.Status == ClassStudentStatus.Active &&
                    cs.Class != null &&
                    cs.Class.CenterId == centerId &&
                    !cs.Class.IsDeleted &&
                    cs.Class.Status == ClassStatus.Active));
            }
            else
            {
                studentQuery = studentQuery.Where(s => _dbContext.ClassStudents.Any(cs =>
                    cs.CenterId == centerId &&
                    cs.StudentId == s.StudentId &&
                    cs.Status == ClassStudentStatus.Active &&
                    cs.Class != null &&
                    cs.Class.CenterId == centerId &&
                    cs.Class.TeacherId == userId &&
                    !cs.Class.IsDeleted &&
                    cs.Class.Status == ClassStatus.Active));
            }
        }
        else
        {
            if (query.ClassId.HasValue)
            {
                studentQuery = studentQuery.Where(s => _dbContext.ClassStudents.Any(cs =>
                    cs.CenterId == centerId &&
                    cs.StudentId == s.StudentId &&
                    cs.ClassId == query.ClassId.Value &&
                    cs.Status == ClassStudentStatus.Active &&
                    cs.Class != null &&
                    cs.Class.CenterId == centerId &&
                    !cs.Class.IsDeleted &&
                    cs.Class.Status == ClassStatus.Active));
            }
        }

        var totalItems = await studentQuery.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)query.PageSize);

        long offset = (long)(query.Page - 1) * query.PageSize;
        if (query.Page > 1 && offset >= totalItems)
        {
            return ListStudentsResult.Success(new System.Collections.Generic.List<StudentDto>(), totalItems, totalPages);
        }

        var dataQuery = studentQuery
            .OrderBy(s => s.FullName)
            .ThenBy(s => s.StudentId)
            .Skip((int)offset)
            .Take(query.PageSize);

        var dtos = await dataQuery.Select(s => new StudentDto
        {
            StudentId = s.StudentId,
            Username = s.User.Username,
            FullName = s.FullName,
            GradeLevel = s.GradeLevel,
            Status = s.User.Status.ToString(),
            RowVersion = s.RowVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ActiveClassCount = isManager
                ? _dbContext.ClassStudents.Count(cs =>
                    cs.CenterId == centerId &&
                    cs.StudentId == s.StudentId &&
                    cs.Status == ClassStudentStatus.Active &&
                    cs.Class != null &&
                    cs.Class.CenterId == centerId &&
                    !cs.Class.IsDeleted &&
                    cs.Class.Status == ClassStatus.Active)
                : _dbContext.ClassStudents.Count(cs =>
                    cs.CenterId == centerId &&
                    cs.StudentId == s.StudentId &&
                    cs.Status == ClassStudentStatus.Active &&
                    cs.Class != null &&
                    cs.Class.CenterId == centerId &&
                    cs.Class.TeacherId == userId &&
                    !cs.Class.IsDeleted &&
                    cs.Class.Status == ClassStatus.Active)
        }).ToListAsync(cancellationToken);

        return ListStudentsResult.Success(dtos, totalItems, totalPages);
    }
}
