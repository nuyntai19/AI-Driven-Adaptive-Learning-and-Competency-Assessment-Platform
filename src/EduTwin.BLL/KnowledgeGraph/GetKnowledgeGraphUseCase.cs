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

        if (subjectId == Guid.Empty)
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

        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SubjectId == subjectId && s.CenterId == _tenantContext.CenterId.Value && !s.IsDeleted, cancellationToken);

        if (subject == null)
        {
            return GetKnowledgeGraphResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var nodes = await _dbContext.KnowledgeNodes
            .AsNoTracking()
            .Where(n => n.CenterId == _tenantContext.CenterId.Value &&
                        n.SubjectId == subjectId &&
                        !n.IsDeleted)
            .OrderBy(n => n.OrderIndex)
            .ThenBy(n => n.NodeId)
            .Select(n => new KnowledgeGraphNodeDto
            {
                NodeId = n.NodeId.ToString(CultureInfo.InvariantCulture),
                NodeType = n.NodeType.ToString(),
                NodeCode = n.NodeCode,
                NodeName = n.NodeName,
                OrderIndex = n.OrderIndex,
                ExamImportance = n.ExamImportance
            })
            .ToListAsync(cancellationToken);

        var edges = await _dbContext.KnowledgeEdges
            .AsNoTracking()
            .Where(e => e.CenterId == _tenantContext.CenterId.Value &&
                        e.SubjectId == subjectId &&
                        !e.IsDeleted)
            .OrderBy(e => e.SourceNodeId)
            .ThenBy(e => e.TargetNodeId)
            .ThenBy(e => e.RelationType)
            .ThenBy(e => e.EdgeId)
            .Select(e => new KnowledgeGraphEdgeDto
            {
                EdgeId = e.EdgeId.ToString(CultureInfo.InvariantCulture),
                SourceNodeId = e.SourceNodeId.ToString(CultureInfo.InvariantCulture),
                TargetNodeId = e.TargetNodeId.ToString(CultureInfo.InvariantCulture),
                RelationType = e.RelationType.ToString(),
                Weight = e.Weight
            })
            .ToListAsync(cancellationToken);

        var resultDto = new KnowledgeGraphDto
        {
            SubjectId = subjectId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            Nodes = nodes,
            Edges = edges
        };

        return GetKnowledgeGraphResult.Success(resultDto);
    }
}
