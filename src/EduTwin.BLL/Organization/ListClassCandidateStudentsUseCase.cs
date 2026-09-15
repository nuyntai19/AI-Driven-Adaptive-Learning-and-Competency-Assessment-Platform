using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Organization;

public class ListClassCandidateStudentsUseCase : IListClassCandidateStudentsUseCase
{
    private readonly ITenantContext _tenantContext;
    private readonly EduTwinDbContext _dbContext;
    private readonly IClassOwnershipGuard _classOwnershipGuard;

    public ListClassCandidateStudentsUseCase(
        ITenantContext tenantContext,
        EduTwinDbContext dbContext,
        IClassOwnershipGuard classOwnershipGuard)
    {
        _tenantContext = tenantContext;
        _dbContext = dbContext;
        _classOwnershipGuard = classOwnershipGuard;
    }

    public async Task<ListStudentsResult> ExecuteAsync(Guid classId, CandidateStudentListQuery query, CancellationToken cancellationToken)
    {
        if (!_tenantContext.CenterId.HasValue ||
            !_tenantContext.UserId.HasValue ||
            string.IsNullOrWhiteSpace(_tenantContext.Role))
        {
            return ListStudentsResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerId = _tenantContext.CenterId.Value;

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == centerId, cancellationToken);

        if (center == null || center.IsDeleted || center.Status != CenterStatus.Active)
        {
            return ListStudentsResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (classId == Guid.Empty ||
            query.Page < 1 ||
            query.PageSize < 1 ||
            query.PageSize > 100 ||
            (query.Search != null && query.Search.Length > 200))
        {
            return ListStudentsResult.ValidationFailed();
        }

        var ownership = await _classOwnershipGuard.CheckClassAccessAsync(classId, cancellationToken);
        if (ownership == OwnershipDecision.NotFound)
            return ListStudentsResult.Failure(ErrorCodes.ResourceNotFound);
        if (ownership == OwnershipDecision.Forbidden)
            return ListStudentsResult.Failure(ErrorCodes.ForbiddenResource);
        if (ownership != OwnershipDecision.Allowed)
            return ListStudentsResult.Failure(ErrorCodes.ResourceNotFound);

        var targetClass = await _dbContext.Classes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClassId == classId && c.CenterId == centerId, cancellationToken);

        if (targetClass == null || targetClass.IsDeleted)
        {
            return ListStudentsResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var searchStr = query.Search?.Trim();
        if (string.IsNullOrWhiteSpace(searchStr))
        {
            searchStr = null;
        }

        // Candidates must be:
        // 1. In the same center
        // 2. Active User account (strict Active-only business rule)
        // 3. Not already an active member of this class
        var studentQuery = _dbContext.Students.AsNoTracking()
            .Where(s => s.CenterId == centerId && !s.IsDeleted &&
                        s.User != null && s.User.CenterId == centerId && !s.User.IsDeleted &&
                        s.User.RoleName == UserRole.Student &&
                        s.User.Status == UserStatus.Active &&
                        !_dbContext.ClassStudents.Any(cs =>
                            cs.CenterId == centerId &&
                            cs.StudentId == s.StudentId &&
                            cs.ClassId == classId &&
                            cs.Status == ClassStudentStatus.Active));

        if (searchStr != null)
        {
            studentQuery = studentQuery.Where(s =>
                s.User.Username.Contains(searchStr) ||
                s.FullName.Contains(searchStr));
        }

        studentQuery = studentQuery
            .OrderBy(s => s.FullName)
            .ThenBy(s => s.StudentId);

        var totalItems = await studentQuery.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)query.PageSize);

        long offset = (long)(query.Page - 1) * query.PageSize;
        if (query.Page > 1 && offset >= totalItems)
        {
            return ListStudentsResult.Success(new List<StudentDto>(), totalItems, totalPages);
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
            ActiveClassCount = _dbContext.ClassStudents.Count(cs =>
                cs.CenterId == centerId &&
                cs.StudentId == s.StudentId &&
                cs.Status == ClassStudentStatus.Active &&
                cs.Class != null &&
                cs.Class.CenterId == centerId &&
                !cs.Class.IsDeleted &&
                cs.Class.Status == ClassStatus.Active)
        }).ToListAsync(cancellationToken);

        return ListStudentsResult.Success(dtos, totalItems, totalPages);
    }
}
