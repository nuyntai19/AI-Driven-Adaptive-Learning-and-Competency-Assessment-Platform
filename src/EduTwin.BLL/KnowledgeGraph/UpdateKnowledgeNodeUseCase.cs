using System;
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

public class UpdateKnowledgeNodeUseCase : IUpdateKnowledgeNodeUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;
    private readonly IKnowledgeNodeHierarchyCycleDetector _cycleDetector;

    public UpdateKnowledgeNodeUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider,
        IKnowledgeNodeHierarchyCycleDetector cycleDetector)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
        _cycleDetector = cycleDetector;
    }

    public async Task<UpdateKnowledgeNodeResult> ExecuteAsync(string nodeId, UpdateKnowledgeNodeRequest request, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved)
            return UpdateKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);

        if (_tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty)
            return UpdateKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);

        if (_tenantContext.UserId == null || _tenantContext.UserId == Guid.Empty)
            return UpdateKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);

        if (string.IsNullOrWhiteSpace(_tenantContext.Role))
            return UpdateKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);

        if (_tenantContext.Role != nameof(UserRole.Teacher) && _tenantContext.Role != nameof(UserRole.CenterManager))
            return UpdateKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);

        if (nodeId == null || !ulong.TryParse(nodeId, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedNodeId) || parsedNodeId == 0)
            return UpdateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);

        if (request.RowVersion == null || !ulong.TryParse(request.RowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedRowVersion) || parsedRowVersion == 0)
            return UpdateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);

        ulong? parsedParentNodeId = null;
        if (request.ParentNodeId != null)
        {
            if (!ulong.TryParse(request.ParentNodeId, NumberStyles.None, CultureInfo.InvariantCulture, out var pId) || pId == 0)
                return UpdateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);
            parsedParentNodeId = pId;
        }

        if (request.NodeName == null || request.NodeName.Length > 200)
            return UpdateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);

        var trimmedName = request.NodeName.Trim();
        if (string.IsNullOrEmpty(trimmedName))
            return UpdateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);

        if (!request.ExamImportance.HasValue || request.ExamImportance.Value < 0 || request.ExamImportance.Value > 100)
            return UpdateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);

        if (request.EstimatedLearningMinutes == 0)
            return UpdateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);

        if (!request.IsActive.HasValue)
            return UpdateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);

        var trimmedDesc = request.Description?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedDesc)) trimmedDesc = null;

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == _tenantContext.CenterId, cancellationToken);

        if (center == null || center.Status != EduTwin.Contracts.Organization.CenterStatus.Active || center.IsDeleted)
            return UpdateKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);

        var targetNode = await _dbContext.KnowledgeNodes
            .FirstOrDefaultAsync(n => n.NodeId == parsedNodeId && n.CenterId == _tenantContext.CenterId && !n.IsDeleted, cancellationToken);

        if (targetNode == null)
            return UpdateKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);

        if (targetNode.RowVersion != parsedRowVersion)
            return UpdateKnowledgeNodeResult.Failure(ErrorCodes.ConcurrencyConflict);

        if (parsedParentNodeId.HasValue)
        {
            var parentNode = await _dbContext.KnowledgeNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.NodeId == parsedParentNodeId.Value && n.CenterId == _tenantContext.CenterId && n.SubjectId == targetNode.SubjectId && !n.IsDeleted, cancellationToken);

            if (parentNode == null)
                return UpdateKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var parentMapList = await _dbContext.KnowledgeNodes
            .AsNoTracking()
            .Where(n => n.CenterId == _tenantContext.CenterId && n.SubjectId == targetNode.SubjectId && !n.IsDeleted)
            .Select(n => new { n.NodeId, n.ParentNodeId })
            .ToListAsync(cancellationToken);

        var parentMap = parentMapList.ToDictionary(k => k.NodeId, v => v.ParentNodeId);

        if (_cycleDetector.HasCycle(parsedNodeId, parsedParentNodeId, parentMap))
            return UpdateKnowledgeNodeResult.Failure(ErrorCodes.DagCycleDetected);

        targetNode.ParentNodeId = parsedParentNodeId;
        targetNode.NodeName = trimmedName;
        targetNode.Description = trimmedDesc;
        targetNode.OrderIndex = request.OrderIndex;
        targetNode.ExamImportance = request.ExamImportance.Value;
        targetNode.EstimatedLearningMinutes = request.EstimatedLearningMinutes;
        targetNode.IsActive = request.IsActive.Value;
        targetNode.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        targetNode.UpdatedBy = _tenantContext.UserId.Value;

        targetNode.RowVersion = parsedRowVersion + 1;
        _dbContext.Entry(targetNode).Property(x => x.RowVersion).OriginalValue = parsedRowVersion;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();
            return UpdateKnowledgeNodeResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        var dto = new KnowledgeNodeDto
        {
            NodeId = targetNode.NodeId.ToString(CultureInfo.InvariantCulture),
            SubjectId = targetNode.SubjectId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            ParentNodeId = targetNode.ParentNodeId?.ToString(CultureInfo.InvariantCulture),
            NodeType = targetNode.NodeType.ToString(),
            NodeCode = targetNode.NodeCode,
            NodeName = targetNode.NodeName,
            Description = targetNode.Description,
            OrderIndex = targetNode.OrderIndex,
            ExamImportance = targetNode.ExamImportance,
            EstimatedLearningMinutes = targetNode.EstimatedLearningMinutes,
            IsActive = targetNode.IsActive,
            RowVersion = targetNode.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return UpdateKnowledgeNodeResult.Success(dto);
    }
}
