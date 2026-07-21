using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;

namespace EduTwin.BLL.KnowledgeGraph;

public class ListSubjectsUseCase : IListSubjectsUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public ListSubjectsUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<ListSubjectsResult> ExecuteAsync(SubjectListQuery query, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved)
            return ListSubjectsResult.Failure(ErrorCodes.ResourceNotFound);

        if (_tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty)
            return ListSubjectsResult.Failure(ErrorCodes.ResourceNotFound);

        if (_tenantContext.UserId == null || _tenantContext.UserId == Guid.Empty)
            return ListSubjectsResult.Failure(ErrorCodes.ResourceNotFound);

        if (string.IsNullOrWhiteSpace(_tenantContext.Role))
            return ListSubjectsResult.Failure(ErrorCodes.ResourceNotFound);

        var validRoles = new[] { nameof(UserRole.Student), nameof(UserRole.Teacher), nameof(UserRole.CenterManager) };
        if (!validRoles.Contains(_tenantContext.Role))
            return ListSubjectsResult.Failure(ErrorCodes.ResourceNotFound);

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == _tenantContext.CenterId && !c.IsDeleted, cancellationToken);

        if (center == null || center.Status != EduTwin.Contracts.Organization.CenterStatus.Active)
            return ListSubjectsResult.Failure(ErrorCodes.ResourceNotFound);

        var dbQuery = _dbContext.Subjects
            .AsNoTracking()
            .Where(s => s.CenterId == _tenantContext.CenterId && !s.IsDeleted);

        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(s => s.IsActive == query.IsActive.Value);
        }

        var results = await dbQuery
            .OrderBy(s => s.SubjectCode)
            .ThenBy(s => s.SubjectId)
            .Select(s => new SubjectDto
            {
                SubjectId = s.SubjectId.ToString("D").ToLowerInvariant(),
                SubjectCode = s.SubjectCode,
                SubjectName = s.SubjectName,
                Description = s.Description,
                IsActive = s.IsActive,
                RowVersion = s.RowVersion.ToString(CultureInfo.InvariantCulture)
            })
            .ToListAsync(cancellationToken);

        return ListSubjectsResult.Success(results);
    }
}
