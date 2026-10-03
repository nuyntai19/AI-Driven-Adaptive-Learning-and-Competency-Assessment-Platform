using System;
using System.Globalization;
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

public class CreateKnowledgeNodeUseCase : ICreateKnowledgeNodeUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public CreateKnowledgeNodeUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<CreateKnowledgeNodeResult> ExecuteAsync(CreateKnowledgeNodeRequest request, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved)
            return CreateKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);

        if (_tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty)
            return CreateKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);

        if (_tenantContext.UserId == null || _tenantContext.UserId == Guid.Empty)
            return CreateKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);

        if (string.IsNullOrWhiteSpace(_tenantContext.Role))
            return CreateKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);

        if (_tenantContext.Role != nameof(UserRole.Teacher) && _tenantContext.Role != nameof(UserRole.CenterManager))
            return CreateKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);

        if (request.SubjectId == Guid.Empty)
            return CreateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);

        if (request.NodeCode == null || request.NodeCode.Length > 64)
            return CreateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);

        var trimmedCode = request.NodeCode.Trim();
        if (string.IsNullOrEmpty(trimmedCode))
            return CreateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);

        if (request.NodeName == null || request.NodeName.Length > 200)
            return CreateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);

        var trimmedName = request.NodeName.Trim();
        if (string.IsNullOrEmpty(trimmedName))
            return CreateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);

        if (request.NodeType == null ||
            !Enum.TryParse<NodeType>(request.NodeType, ignoreCase: false, out var parsedType) ||
            !Enum.IsDefined(typeof(NodeType), parsedType) ||
            !string.Equals(request.NodeType, parsedType.ToString(), StringComparison.Ordinal))
        {
            return CreateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);
        }

        ulong? parsedParentNodeId = null;
        if (request.ParentNodeId != null)
        {
            if (!ulong.TryParse(request.ParentNodeId, NumberStyles.None, CultureInfo.InvariantCulture, out var pId) || pId == 0)
            {
                return CreateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);
            }
            parsedParentNodeId = pId;
        }

        if (!request.ExamImportance.HasValue || request.ExamImportance.Value < 0 || request.ExamImportance.Value > 100)
            return CreateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);

        if (request.EstimatedLearningMinutes == 0)
            return CreateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);

        if (!request.IsActive.HasValue)
            return CreateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);

        var trimmedDesc = request.Description?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedDesc)) trimmedDesc = null;

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == _tenantContext.CenterId, cancellationToken);

        if (center == null || center.Status != EduTwin.Contracts.Organization.CenterStatus.Active || center.IsDeleted)
            return CreateKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);

        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SubjectId == request.SubjectId && s.CenterId == _tenantContext.CenterId && !s.IsDeleted, cancellationToken);

        if (subject == null)
            return CreateKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);

        if (parsedParentNodeId.HasValue)
        {
            var parent = await _dbContext.KnowledgeNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.NodeId == parsedParentNodeId.Value && n.CenterId == _tenantContext.CenterId && n.SubjectId == request.SubjectId && !n.IsDeleted, cancellationToken);

            if (parent == null)
                return CreateKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var isDuplicate = await _dbContext.KnowledgeNodes
            .AsNoTracking()
            .AnyAsync(n => n.CenterId == _tenantContext.CenterId && n.SubjectId == request.SubjectId && n.NodeCode == trimmedCode, cancellationToken);

        if (isDuplicate)
            return CreateKnowledgeNodeResult.Failure(ErrorCodes.DuplicateResource);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var node = new KnowledgeNode
        {
            CenterId = _tenantContext.CenterId.Value,
            SubjectId = request.SubjectId,
            ParentNodeId = parsedParentNodeId,
            NodeType = parsedType,
            NodeCode = trimmedCode,
            NodeName = trimmedName,
            Description = trimmedDesc,
            OrderIndex = request.OrderIndex,
            ExamImportance = request.ExamImportance.Value,
            EstimatedLearningMinutes = request.EstimatedLearningMinutes,
            IsActive = request.IsActive.Value,
            IsDeleted = false,
            RowVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _tenantContext.UserId.Value,
            UpdatedBy = _tenantContext.UserId.Value
        };

        _dbContext.KnowledgeNodes.Add(node);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _dbContext.ChangeTracker.Clear();
            var isDuplicateRaceCondition = false;
            var currentException = (Exception)ex;
            while (currentException != null)
            {
                if (currentException.Message.Contains("ux_knowledge_nodes_center_id_subject_id_node_code", StringComparison.OrdinalIgnoreCase))
                {
                    isDuplicateRaceCondition = true;
                    break;
                }
                currentException = currentException.InnerException!;
            }

            if (isDuplicateRaceCondition)
                return CreateKnowledgeNodeResult.Failure(ErrorCodes.DuplicateResource);

            throw;
        }

        var dto = new KnowledgeNodeDto
        {
            NodeId = node.NodeId.ToString(CultureInfo.InvariantCulture),
            SubjectId = node.SubjectId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            ParentNodeId = node.ParentNodeId?.ToString(CultureInfo.InvariantCulture),
            NodeType = node.NodeType.ToString(),
            NodeCode = node.NodeCode,
            NodeName = node.NodeName,
            Description = node.Description,
            OrderIndex = node.OrderIndex,
            ExamImportance = node.ExamImportance,
            EstimatedLearningMinutes = node.EstimatedLearningMinutes,
            IsActive = node.IsActive,
            RowVersion = node.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return CreateKnowledgeNodeResult.Success(dto);
    }
}
