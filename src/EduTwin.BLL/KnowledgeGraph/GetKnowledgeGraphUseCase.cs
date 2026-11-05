using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.KnowledgeGraph;

public class GetKnowledgeGraphUseCase : IGetKnowledgeGraphUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetKnowledgeGraphUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<GetKnowledgeGraphResult> ExecuteAsync(
        Guid subjectId,
        CancellationToken cancellationToken = default)
        => await ExecuteAsync(new KnowledgeGraphQuery { SubjectId = subjectId }, cancellationToken);

    public async Task<GetKnowledgeGraphResult> ExecuteAsync(KnowledgeGraphQuery query, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role) ||
            (_tenantContext.Role != nameof(UserRole.Student) &&
             _tenantContext.Role != nameof(UserRole.Teacher) &&
             _tenantContext.Role != nameof(UserRole.CenterManager)))
        {
            return GetKnowledgeGraphResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (query.SubjectId == Guid.Empty && !query.CurriculumId.HasValue && !query.ClassId.HasValue && !query.GraphVersionId.HasValue)
        {
            return GetKnowledgeGraphResult.Failure(ErrorCodes.ValidationFailed);
        }

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == _tenantContext.CenterId.Value && !c.IsDeleted, cancellationToken);

        if (center == null || center.Status != CenterStatus.Active)
        {
            return GetKnowledgeGraphResult.Failure(ErrorCodes.ResourceNotFound);
        }

        try
        {
            return GetKnowledgeGraphResult.Success(await KnowledgeGraphScopeReader.ReadAsync(_dbContext, _tenantContext, query, cancellationToken));
        }
        catch (GraphScopeException ex) { return GetKnowledgeGraphResult.Failure(ex.Code); }

    }
}
