using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class ListCurriculumsUseCase : IListCurriculumsUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public ListCurriculumsUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<ListCurriculumsResult> ExecuteAsync(CurriculumListQuery query, CancellationToken cancellationToken = default)
    {
        // 1. Fail-closed tenant and role gate
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role) ||
            (!string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal) &&
             !string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal)))
        {
            return ListCurriculumsResult.Failure(ErrorCodes.ResourceNotFound);
        }

        // 2. Query Validation
        if (query.SubjectId.HasValue && query.SubjectId.Value == Guid.Empty)
        {
            return ListCurriculumsResult.Failure(ErrorCodes.ValidationFailed);
        }

        ReviewStatus? filterStatus = null;
        if (query.Status != null)
        {
            if (string.IsNullOrWhiteSpace(query.Status))
            {
                return ListCurriculumsResult.Failure(ErrorCodes.ValidationFailed);
            }

            if (!Enum.TryParse<ReviewStatus>(query.Status, ignoreCase: false, out var parsedStatus) ||
                !Enum.IsDefined(typeof(ReviewStatus), parsedStatus) ||
                !string.Equals(query.Status, parsedStatus.ToString(), StringComparison.Ordinal))
            {
                return ListCurriculumsResult.Failure(ErrorCodes.ValidationFailed);
            }

            filterStatus = parsedStatus;
        }

        // 3. Center and Actor Validation
        var centerId = _tenantContext.CenterId.Value;
        var actorId = _tenantContext.UserId.Value;
        var isTeacher = string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal);

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == centerId && !c.IsDeleted && c.Status == CenterStatus.Active, cancellationToken);

        if (center == null)
        {
            return ListCurriculumsResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (isTeacher)
        {
            var teacherEntity = await _dbContext.Teachers
                .AsNoTracking()
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TeacherId == actorId && t.CenterId == centerId && !t.IsDeleted, cancellationToken);

            if (teacherEntity == null ||
                teacherEntity.User == null ||
                teacherEntity.User.CenterId != centerId ||
                teacherEntity.User.IsDeleted ||
                teacherEntity.User.RoleName != UserRole.Teacher ||
                teacherEntity.User.Status != UserStatus.Active)
            {
                return ListCurriculumsResult.Failure(ErrorCodes.ResourceNotFound);
            }
        }
        else
        {
            var managerUser = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == actorId && u.CenterId == centerId && !u.IsDeleted && u.RoleName == UserRole.CenterManager && u.Status == UserStatus.Active, cancellationToken);

            if (managerUser == null)
            {
                return ListCurriculumsResult.Failure(ErrorCodes.ResourceNotFound);
            }
        }

        // 4. Base Query & Filters
        var baseQuery = _dbContext.Curriculums
            .AsNoTracking()
            .Where(c => c.CenterId == centerId && !c.IsDeleted);

        if (isTeacher)
        {
            baseQuery = baseQuery.Where(c => c.TeacherId == actorId);
        }

        if (query.SubjectId.HasValue)
        {
            baseQuery = baseQuery.Where(c => c.SubjectId == query.SubjectId.Value);
        }

        if (filterStatus.HasValue)
        {
            baseQuery = baseQuery.Where(c => c.ReviewStatus == filterStatus.Value);
        }

        var curriculums = await baseQuery
            .OrderByDescending(c => c.UpdatedAt)
            .ThenBy(c => c.CurriculumId)
            .ToListAsync(cancellationToken);

        // 5. Projection & Joins
        var curriculumIds = curriculums.Select(c => c.CurriculumId).ToList();

        var classesMap = new Dictionary<Guid, List<string>>();
        var nodesMap = new Dictionary<Guid, List<string>>();

        if (curriculumIds.Count > 0)
        {
            var classes = await _dbContext.CurriculumClasses
                .AsNoTracking()
                .Where(cc => cc.CenterId == centerId && curriculumIds.Contains(cc.CurriculumId))
                .OrderBy(cc => cc.ClassId)
                .ToListAsync(cancellationToken);

            foreach (var cc in classes)
            {
                if (!classesMap.TryGetValue(cc.CurriculumId, out var list))
                {
                    list = new List<string>();
                    classesMap[cc.CurriculumId] = list;
                }
                list.Add(cc.ClassId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant());
            }

            var nodes = await _dbContext.CurriculumNodes
                .AsNoTracking()
                .Where(cn => cn.CenterId == centerId && curriculumIds.Contains(cn.CurriculumId))
                .OrderBy(cn => cn.OrderIndex)
                .ThenBy(cn => cn.NodeId)
                .ToListAsync(cancellationToken);

            foreach (var cn in nodes)
            {
                if (!nodesMap.TryGetValue(cn.CurriculumId, out var list))
                {
                    list = new List<string>();
                    nodesMap[cn.CurriculumId] = list;
                }
                list.Add(cn.NodeId.ToString(CultureInfo.InvariantCulture));
            }
        }

        var dtos = new List<CurriculumDto>();
        foreach (var c in curriculums)
        {
            dtos.Add(new CurriculumDto
            {
                CurriculumId = c.CurriculumId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
                TeacherId = c.TeacherId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
                SubjectId = c.SubjectId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
                Title = c.Title,
                Description = c.Description,
                SourceFile = c.SourceFile,
                ReviewStatus = c.ReviewStatus.ToString(),
                ClassIds = classesMap.TryGetValue(c.CurriculumId, out var classList) ? classList : new List<string>(),
                NodeIds = nodesMap.TryGetValue(c.CurriculumId, out var nodeList) ? nodeList : new List<string>(),
                RowVersion = c.RowVersion.ToString(CultureInfo.InvariantCulture)
            });
        }

        return ListCurriculumsResult.Success(dtos);
    }
}
