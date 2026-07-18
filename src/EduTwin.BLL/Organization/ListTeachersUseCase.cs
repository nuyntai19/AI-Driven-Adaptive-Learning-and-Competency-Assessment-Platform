using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Organization;

public class ListTeachersUseCase : IListTeachersUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public ListTeachersUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<ListTeachersResult> ExecuteAsync(TeacherListQuery query, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty)
        {
            return ListTeachersResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal))
        {
            return ListTeachersResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (query.Page < 1 || query.PageSize < 1 || query.PageSize > 100)
        {
            return ListTeachersResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (query.Status.HasValue && !Enum.IsDefined(typeof(UserStatus), query.Status.Value))
        {
            return ListTeachersResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (query.Search?.Length > 200)
        {
            return ListTeachersResult.Failure(ErrorCodes.ValidationFailed);
        }

        var search = query.Search?.Trim();
        if (string.IsNullOrWhiteSpace(search))
        {
            search = null;
        }

        var centerId = _tenantContext.CenterId.Value;

        var teacherQuery = _dbContext.Teachers
            .AsNoTracking()
            .Where(t => t.CenterId == centerId && !t.IsDeleted)
            .Where(t => t.User != null && t.User.CenterId == centerId && !t.User.IsDeleted && t.User.RoleName == UserRole.Teacher);

        if (!string.IsNullOrEmpty(search))
        {
            teacherQuery = teacherQuery.Where(t =>
                (t.User!.Username != null && t.User.Username.Contains(search)) ||
                (t.User.DisplayName != null && t.User.DisplayName.Contains(search)) ||
                (t.Department != null && t.Department.Contains(search)));
        }

        if (query.Status.HasValue)
        {
            teacherQuery = teacherQuery.Where(t => t.User!.Status == query.Status.Value);
        }

        var totalItems = await teacherQuery.CountAsync(cancellationToken);

        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling((double)totalItems / query.PageSize);

        if (query.Page > 1 && (query.Page - 1) * (long)query.PageSize >= totalItems)
        {
            return ListTeachersResult.Success(new System.Collections.Generic.List<TeacherDto>(), totalItems, totalPages);
        }

        var teachers = await teacherQuery
            .OrderBy(t => t.User!.DisplayName)
            .ThenBy(t => t.TeacherId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => new TeacherDto
            {
                TeacherId = t.TeacherId.ToString("D").ToLowerInvariant(),
                Username = t.User!.Username,
                DisplayName = t.User.DisplayName,
                Department = t.Department,
                Status = t.User.Status.ToString(),
                ClassCount = _dbContext.Classes.Count(c => c.CenterId == centerId && c.TeacherId == t.TeacherId && !c.IsDeleted && c.Status == ClassStatus.Active),
                RowVersion = t.RowVersion.ToString(CultureInfo.InvariantCulture)
            })
            .ToListAsync(cancellationToken);

        return ListTeachersResult.Success(teachers, totalItems, totalPages);
    }
}
