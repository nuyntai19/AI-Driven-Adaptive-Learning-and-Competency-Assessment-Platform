using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.KnowledgeGraph;

public class CreateKnowledgeEdgeUseCase : ICreateKnowledgeEdgeUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;
    private readonly KnowledgeGraphValidator _validator;

    public CreateKnowledgeEdgeUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider,
        KnowledgeGraphValidator validator)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
        _validator = validator;
    }

    public async Task<CreateKnowledgeEdgeResult> ExecuteAsync(CreateKnowledgeEdgeRequest request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved ||
            _tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty ||
            _tenantContext.UserId == null || _tenantContext.UserId == Guid.Empty ||
            (_tenantContext.Role != nameof(UserRole.Teacher) && _tenantContext.Role != nameof(UserRole.CenterManager)))
        {
            return CreateKnowledgeEdgeResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (request == null)
        {
            return CreateKnowledgeEdgeResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (request.SubjectId == Guid.Empty)
        {
            return CreateKnowledgeEdgeResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (!ulong.TryParse(request.SourceNodeId, NumberStyles.None, CultureInfo.InvariantCulture, out var sourceNodeId) || sourceNodeId == 0)
        {
            return CreateKnowledgeEdgeResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (!ulong.TryParse(request.TargetNodeId, NumberStyles.None, CultureInfo.InvariantCulture, out var targetNodeId) || targetNodeId == 0)
        {
            return CreateKnowledgeEdgeResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (sourceNodeId == targetNodeId)
        {
            return CreateKnowledgeEdgeResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (!Enum.TryParse<EduTwin.Contracts.KnowledgeGraph.RelationType>(request.RelationType, out var relationType) ||
            !Enum.IsDefined(typeof(EduTwin.Contracts.KnowledgeGraph.RelationType), relationType) ||
            request.RelationType != relationType.ToString())
        {
            return CreateKnowledgeEdgeResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (!request.Weight.HasValue || request.Weight.Value < 0m || request.Weight.Value > 1m)
        {
            return CreateKnowledgeEdgeResult.Failure(ErrorCodes.ValidationFailed);
        }

        var center = await _dbContext.Centers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == _tenantContext.CenterId!.Value && c.Status == EduTwin.Contracts.Organization.CenterStatus.Active && !c.IsDeleted, cancellationToken);

        if (center == null)
            return CreateKnowledgeEdgeResult.Failure(ErrorCodes.ResourceNotFound);

        var subject = await _dbContext.Subjects.AsNoTracking()
            .FirstOrDefaultAsync(s => s.CenterId == _tenantContext.CenterId!.Value && s.SubjectId == request.SubjectId && !s.IsDeleted, cancellationToken);
        if (subject == null)
            return CreateKnowledgeEdgeResult.Failure(ErrorCodes.ResourceNotFound);

        var sourceNode = await _dbContext.KnowledgeNodes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.CenterId == _tenantContext.CenterId!.Value && n.NodeId == sourceNodeId && n.SubjectId == request.SubjectId && !n.IsDeleted, cancellationToken);
        if (sourceNode == null)
            return CreateKnowledgeEdgeResult.Failure(ErrorCodes.ResourceNotFound);

        var targetNode = await _dbContext.KnowledgeNodes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.CenterId == _tenantContext.CenterId!.Value && n.NodeId == targetNodeId && n.SubjectId == request.SubjectId && !n.IsDeleted, cancellationToken);
        if (targetNode == null)
            return CreateKnowledgeEdgeResult.Failure(ErrorCodes.ResourceNotFound);

        var existingEdge = await _dbContext.KnowledgeEdges.AsNoTracking()
            .FirstOrDefaultAsync(e => e.CenterId == _tenantContext.CenterId!.Value &&
                                      e.SourceNodeId == sourceNodeId &&
                                      e.TargetNodeId == targetNodeId &&
                                      e.RelationType == relationType &&
                                      !e.IsDeleted, cancellationToken);

        if (existingEdge != null)
        {
            return CreateKnowledgeEdgeResult.Failure(ErrorCodes.DuplicateResource);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var edge = new KnowledgeEdge
        {
            CenterId = _tenantContext.CenterId!.Value,
            SubjectId = request.SubjectId,
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            RelationType = relationType,
            Weight = request.Weight.Value,
            CreatedAt = now,
            CreatedBy = _tenantContext.UserId!.Value,
            UpdatedAt = now,
            UpdatedBy = _tenantContext.UserId!.Value,
            IsDeleted = false
        };

        var existingGraphEdges = await _dbContext.KnowledgeEdges.AsNoTracking()
            .Where(e => e.CenterId == _tenantContext.CenterId!.Value && e.SubjectId == request.SubjectId && !e.IsDeleted)
            .OrderBy(e => e.SourceNodeId)
            .ThenBy(e => e.TargetNodeId)
            .ThenBy(e => e.RelationType)
            .ThenBy(e => e.EdgeId)
            .ToListAsync(cancellationToken);

        existingGraphEdges.Add(edge);

        try
        {
            _validator.ValidateDag(existingGraphEdges);
        }
        catch (InvalidOperationException)
        {
            return CreateKnowledgeEdgeResult.Failure(ErrorCodes.DagCycleDetected);
        }

        _dbContext.KnowledgeEdges.Add(edge);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (IsDuplicateConstraintViolation(ex))
            {
                _dbContext.ChangeTracker.Clear();
                return CreateKnowledgeEdgeResult.Failure(ErrorCodes.DuplicateResource);
            }

            throw;
        }

        return CreateKnowledgeEdgeResult.Success(new KnowledgeEdgeDto
        {
            EdgeId = edge.EdgeId.ToString(CultureInfo.InvariantCulture),
            SubjectId = edge.SubjectId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            SourceNodeId = edge.SourceNodeId.ToString(CultureInfo.InvariantCulture),
            TargetNodeId = edge.TargetNodeId.ToString(CultureInfo.InvariantCulture),
            RelationType = edge.RelationType.ToString(),
            Weight = edge.Weight,
            RowVersion = edge.RowVersion.ToString(CultureInfo.InvariantCulture)
        });
    }

    private static bool IsDuplicateConstraintViolation(Exception exception)
    {
        var current = exception;
        while (current != null)
        {
            if (current.Message.Contains("ux_knowledge_edges_center_id_source_node_id_target_node_id_relation_type", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("ux_knowledge_edges_center_id_source_id_target_id_relation_type", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            current = current.InnerException;
        }
        return false;
    }
}
