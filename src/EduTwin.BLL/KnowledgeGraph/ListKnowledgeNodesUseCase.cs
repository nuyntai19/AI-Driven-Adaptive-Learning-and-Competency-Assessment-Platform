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

        if (query.SubjectId == Guid.Empty)
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

        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SubjectId == query.SubjectId && s.CenterId == _tenantContext.CenterId && !s.IsDeleted, cancellationToken);

        if (subject == null)
            return ListKnowledgeNodesResult.Failure(ErrorCodes.ResourceNotFound);

        var dbQuery = _dbContext.KnowledgeNodes
            .AsNoTracking()
            .Where(n => n.CenterId == _tenantContext.CenterId && n.SubjectId == query.SubjectId && !n.IsDeleted);

        if (parsedNodeType.HasValue)
        {
            dbQuery = dbQuery.Where(n => n.NodeType == parsedNodeType.Value);
        }

        if (parsedParentNodeId.HasValue)
        {
            dbQuery = dbQuery.Where(n => n.ParentNodeId == parsedParentNodeId.Value);
        }

        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(n => n.IsActive == query.IsActive.Value);
        }

        var results = await dbQuery
            .OrderBy(n => n.OrderIndex)
            .ThenBy(n => n.NodeId)
            .Select(n => new KnowledgeNodeDto
            {
                NodeId = n.NodeId.ToString(CultureInfo.InvariantCulture),
                SubjectId = n.SubjectId.ToString("D").ToLowerInvariant(),
                ParentNodeId = n.ParentNodeId.HasValue ? n.ParentNodeId.Value.ToString(CultureInfo.InvariantCulture) : null,
                NodeType = n.NodeType.ToString(),
                NodeCode = n.NodeCode,
                NodeName = n.NodeName,
                Description = n.Description,
                OrderIndex = n.OrderIndex,
                ExamImportance = n.ExamImportance,
                EstimatedLearningMinutes = n.EstimatedLearningMinutes,
                IsActive = n.IsActive,
                RowVersion = n.RowVersion.ToString(CultureInfo.InvariantCulture)
            })
            .ToListAsync(cancellationToken);

        return ListKnowledgeNodesResult.Success(results);
    }
}
