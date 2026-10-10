using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.KnowledgeGraph;

public class DeleteKnowledgeNodeUseCase : IDeleteKnowledgeNodeUseCase
{
    private readonly EduTwinDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public DeleteKnowledgeNodeUseCase(
        EduTwinDbContext context,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _context = context;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<DeleteKnowledgeNodeResult> ExecuteAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        var tenantContext = _tenantContext;
        if (tenantContext == null || !tenantContext.IsResolved ||
            tenantContext.CenterId == Guid.Empty ||
            tenantContext.UserId == Guid.Empty ||
            string.IsNullOrEmpty(tenantContext.Role))
        {
            return DeleteKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (tenantContext.Role != nameof(UserRole.Teacher) &&
            tenantContext.Role != nameof(UserRole.CenterManager))
        {
            return DeleteKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound); // fail closed
        }

        if (string.IsNullOrWhiteSpace(nodeId) ||
            !ulong.TryParse(nodeId, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedNodeId) ||
            nodeId.Trim() != nodeId ||
            parsedNodeId == 0)
        {
            return DeleteKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed);
        }

        var center = await _context.Centers
            .FirstOrDefaultAsync(x => x.CenterId == tenantContext.CenterId && x.Status == CenterStatus.Active && !x.IsDeleted, cancellationToken);

        if (center == null)
        {
            return DeleteKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var node = await _context.KnowledgeNodes
            .FirstOrDefaultAsync(x => x.NodeId == parsedNodeId && x.CenterId == tenantContext.CenterId && !x.IsDeleted, cancellationToken);

        if (node == null)
        {
            return DeleteKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);
        }

        // Evidence Protection Checks
        await using var transaction = await GraphMutationTransaction.BeginAsync(_context, tenantContext.CenterId!.Value, node.SubjectId, cancellationToken);
        if (transaction is not null) await _context.Entry(node).ReloadAsync(cancellationToken);
        if (node.IsDeleted) return DeleteKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound);

        // 1. Child nodes
        var hasChildNodes = await _context.KnowledgeNodes
            .AnyAsync(x => x.CenterId == tenantContext.CenterId && !x.IsDeleted && x.ParentNodeId == parsedNodeId, cancellationToken);
        if (hasChildNodes) return DeleteKnowledgeNodeResult.Failure(ErrorCodes.InvalidStateTransition, "Không thể xóa chủ đề vì còn nút con. Không tự tắt hoạt động để vượt ràng buộc.");

        // 2. Edges
        var hasEdges = await _context.KnowledgeEdges
            .AnyAsync(x => x.CenterId == tenantContext.CenterId && !x.IsDeleted && (x.SourceNodeId == parsedNodeId || x.TargetNodeId == parsedNodeId), cancellationToken);
        if (hasEdges) return DeleteKnowledgeNodeResult.Failure(ErrorCodes.InvalidStateTransition, "Không thể xóa chủ đề vì còn liên kết tri thức. Không tự tắt hoạt động để vượt ràng buộc.");

        // 3. CurriculumNode
        var hasCurriculumNodes = await _context.CurriculumNodes
            .AnyAsync(x => x.CenterId == tenantContext.CenterId && x.NodeId == parsedNodeId, cancellationToken);
        if (hasCurriculumNodes) return DeleteKnowledgeNodeResult.Failure(ErrorCodes.InvalidStateTransition, "Không thể xóa chủ đề vì đang thuộc giáo trình (kể cả giáo trình đã lưu trữ). Không tự tắt hoạt động để vượt ràng buộc.");

        // 4. Question
        var hasQuestions = await _context.Questions
            .AnyAsync(x => x.CenterId == tenantContext.CenterId && !x.IsDeleted && x.PrimaryTopicNodeId == parsedNodeId, cancellationToken);
        if (hasQuestions) return DeleteKnowledgeNodeResult.Failure(ErrorCodes.InvalidStateTransition, "Không thể xóa chủ đề vì đang là chủ đề chính của câu hỏi. Không tự tắt hoạt động để vượt ràng buộc.");

        // 5. QuestionKnowledgeNode
        var hasQuestionNodes = await _context.QuestionKnowledgeNodes
            .AnyAsync(x => x.CenterId == tenantContext.CenterId && x.NodeId == parsedNodeId, cancellationToken);
        if (hasQuestionNodes) return DeleteKnowledgeNodeResult.Failure(ErrorCodes.InvalidStateTransition, "Không thể xóa chủ đề vì đang được liên kết với câu hỏi. Không tự tắt hoạt động để vượt ràng buộc.");

        // 6. KnowledgeTwin
        var hasKnowledgeTwins = await _context.KnowledgeTwins
            .AnyAsync(x => x.CenterId == tenantContext.CenterId && !x.IsDeleted && x.TopicNodeId == parsedNodeId, cancellationToken);
        if (hasKnowledgeTwins) return DeleteKnowledgeNodeResult.Failure(ErrorCodes.InvalidStateTransition, "Không thể xóa chủ đề vì đã có dữ liệu năng lực học sinh. Không tự tắt hoạt động để vượt ràng buộc.");

        // 7. TwinUpdateHistory
        var hasTwinHistory = await _context.TwinUpdateHistories
            .AnyAsync(x => x.CenterId == tenantContext.CenterId && x.TopicNodeId == parsedNodeId, cancellationToken);
        if (hasTwinHistory) return DeleteKnowledgeNodeResult.Failure(ErrorCodes.InvalidStateTransition, "Không thể xóa chủ đề vì đã có lịch sử cập nhật năng lực. Không tự tắt hoạt động để vượt ràng buộc.");

        // 8. LearningPathItem
        var hasLearningPathItems = await _context.LearningPathItems
            .AnyAsync(x => x.CenterId == tenantContext.CenterId && !x.IsDeleted && x.TopicNodeId == parsedNodeId, cancellationToken);
        if (hasLearningPathItems) return DeleteKnowledgeNodeResult.Failure(ErrorCodes.InvalidStateTransition, "Không thể xóa chủ đề vì đang được tham chiếu bởi lộ trình học tập. Không tự tắt hoạt động để vượt ràng buộc.");

        // 9. Recommendation
        var hasRecommendations = await _context.Recommendations
            .AnyAsync(x => x.CenterId == tenantContext.CenterId && !x.IsDeleted && x.TopicNodeId == parsedNodeId, cancellationToken);
        if (hasRecommendations) return DeleteKnowledgeNodeResult.Failure(ErrorCodes.InvalidStateTransition, "Không thể xóa chủ đề vì đang được tham chiếu bởi khuyến nghị học tập. Không tự tắt hoạt động để vượt ràng buộc.");

        // 10. ReasoningAnalysis.RootCauseNodeIds
        var analyses = await _context.ReasoningAnalyses
            .Where(x => x.CenterId == tenantContext.CenterId)
            .Select(x => x.RootCauseNodeIds)
            .ToListAsync(cancellationToken);

        foreach (var rootCauses in analyses)
        {
            if (rootCauses == null)
            {
                return DeleteKnowledgeNodeResult.Failure(ErrorCodes.InvalidStateTransition);
            }

            try
            {
                if (rootCauses.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return DeleteKnowledgeNodeResult.Failure(ErrorCodes.InvalidStateTransition);
                }

                foreach (var element in rootCauses.RootElement.EnumerateArray())
                {
                    ulong id;
                    if (element.ValueKind == JsonValueKind.Number)
                    {
                        if (!element.TryGetUInt64(out id) || id == 0)
                        {
                            return DeleteKnowledgeNodeResult.Failure(ErrorCodes.InvalidStateTransition);
                        }
                    }
                    else if (element.ValueKind == JsonValueKind.String)
                    {
                        var str = element.GetString();
                        if (string.IsNullOrEmpty(str) || !ulong.TryParse(str, NumberStyles.None, CultureInfo.InvariantCulture, out id) || id == 0)
                        {
                            return DeleteKnowledgeNodeResult.Failure(ErrorCodes.InvalidStateTransition);
                        }
                    }
                    else
                    {
                        return DeleteKnowledgeNodeResult.Failure(ErrorCodes.InvalidStateTransition);
                    }

                    if (id == parsedNodeId)
                    {
                        return DeleteKnowledgeNodeResult.Failure(ErrorCodes.InvalidStateTransition);
                    }
                }
            }
            catch (JsonException)
            {
                return DeleteKnowledgeNodeResult.Failure(ErrorCodes.InvalidStateTransition);
            }
            catch (InvalidOperationException)
            {
                return DeleteKnowledgeNodeResult.Failure(ErrorCodes.InvalidStateTransition);
            }
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        node.IsDeleted = true;
        node.DeletedAt = now;
        node.DeletedBy = tenantContext.UserId;
        node.UpdatedAt = now;
        node.UpdatedBy = tenantContext.UserId;
        AcademicDependencyGuards.Audit(_context, tenantContext.CenterId!.Value, tenantContext.UserId!.Value,
            "KnowledgeNodeDeleted", "KnowledgeNode", nodeId, new { node.NodeName, IsDeleted = false, node.RowVersion },
            new { node.NodeName, IsDeleted = true, RowVersion = node.RowVersion + 1 }, now, "Xóa mềm điểm tri thức không còn phụ thuộc.");

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return DeleteKnowledgeNodeResult.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            return DeleteKnowledgeNodeResult.Failure(ErrorCodes.ConcurrencyConflict);
        }
        catch (DbUpdateException ex) when (AcademicDependencyGuards.IsDatabaseGuard(ex))
        {
            _context.ChangeTracker.Clear();
            return DeleteKnowledgeNodeResult.Failure(ErrorCodes.InvalidStateTransition, AcademicDependencyGuards.ConcurrentDependencyMessage);
        }
    }
}
