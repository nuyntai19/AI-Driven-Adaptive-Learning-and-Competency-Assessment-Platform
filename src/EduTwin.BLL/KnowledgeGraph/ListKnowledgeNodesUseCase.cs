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

namespace EduTwin.BLL.KnowledgeGraph;

public class ListKnowledgeNodesUseCase : IListKnowledgeNodesUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public ListKnowledgeNodesUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<ListKnowledgeNodesResult> ExecuteAsync(KnowledgeNodeListQuery query, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved)
            return ListKnowledgeNodesResult.Failure(ErrorCodes.ResourceNotFound);

        if (_tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty)
            return ListKnowledgeNodesResult.Failure(ErrorCodes.ResourceNotFound);

        if (_tenantContext.UserId == null || _tenantContext.UserId == Guid.Empty)
            return ListKnowledgeNodesResult.Failure(ErrorCodes.ResourceNotFound);

        if (string.IsNullOrWhiteSpace(_tenantContext.Role))
            return ListKnowledgeNodesResult.Failure(ErrorCodes.ResourceNotFound);

        var validRoles = new[] { nameof(UserRole.Student), nameof(UserRole.Teacher), nameof(UserRole.CenterManager) };
        if (!validRoles.Contains(_tenantContext.Role))
            return ListKnowledgeNodesResult.Failure(ErrorCodes.ResourceNotFound);

        if (query.SubjectId == Guid.Empty && !query.CurriculumId.HasValue && !query.ClassId.HasValue && !query.GraphVersionId.HasValue)
            return ListKnowledgeNodesResult.Failure(ErrorCodes.ValidationFailed);

        NodeType? parsedNodeType = null;
        if (query.NodeType != null)
        {
            if (!Enum.TryParse<NodeType>(
                    query.NodeType,
                    ignoreCase: false,
                    out var parsed) ||
                !Enum.IsDefined(typeof(NodeType), parsed) ||
                !string.Equals(
                    query.NodeType,
                    parsed.ToString(),
                    StringComparison.Ordinal))
            {
                return ListKnowledgeNodesResult.Failure(ErrorCodes.ValidationFailed);
            }

            parsedNodeType = parsed;
        }

        ulong? parsedParentNodeId = null;
        if (query.ParentNodeId != null)
        {
            if (!ulong.TryParse(query.ParentNodeId, NumberStyles.None, CultureInfo.InvariantCulture, out var pId) ||
                pId == 0)
            {
                return ListKnowledgeNodesResult.Failure(ErrorCodes.ValidationFailed);
            }

            parsedParentNodeId = pId;
        }

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == _tenantContext.CenterId && !c.IsDeleted, cancellationToken);

        if (center == null || center.Status != EduTwin.Contracts.Organization.CenterStatus.Active)
            return ListKnowledgeNodesResult.Failure(ErrorCodes.ResourceNotFound);

        try
        {
            var graph = await KnowledgeGraphScopeReader.ReadAsync(_dbContext, _tenantContext, query, cancellationToken);
            var scoped = graph.Nodes.AsEnumerable();
            if (parsedNodeType.HasValue) scoped = scoped.Where(n => n.NodeType == parsedNodeType.Value.ToString());
            if (parsedParentNodeId.HasValue) scoped = scoped.Where(n => n.ParentNodeId == parsedParentNodeId.Value.ToString(CultureInfo.InvariantCulture));
            if (query.IsActive.HasValue) scoped = scoped.Where(n => n.IsActive == query.IsActive);
            return ListKnowledgeNodesResult.Success(scoped.Select(n => new KnowledgeNodeDto { NodeId = n.NodeId, SubjectId = graph.SubjectId,
                ParentNodeId = n.ParentNodeId, NodeType = n.NodeType, NodeCode = n.NodeCode, NodeName = n.NodeName, Description = n.Description,
                OrderIndex = n.OrderIndex, ExamImportance = n.ExamImportance, EstimatedLearningMinutes = n.EstimatedLearningMinutes,
                IsActive = n.IsActive, RowVersion = n.RowVersion, GradeLevels = n.GradeLevels, Provenance = n.Provenance }).ToList());
        }
        catch (GraphScopeException ex) { return ListKnowledgeNodesResult.Failure(ex.Code); }

    }
}
