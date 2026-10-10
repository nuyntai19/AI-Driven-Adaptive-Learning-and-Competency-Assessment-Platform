using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.KnowledgeGraph;

public class UpdateKnowledgeEdgeUseCase : IUpdateKnowledgeEdgeUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public UpdateKnowledgeEdgeUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<UpdateKnowledgeEdgeResult> ExecuteAsync(
        string edgeId,
        UpdateKnowledgeEdgeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved ||
            _tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty ||
            _tenantContext.UserId == null || _tenantContext.UserId == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role) ||
            (_tenantContext.Role != nameof(UserRole.Teacher) && _tenantContext.Role != nameof(UserRole.CenterManager)))
        {
            return UpdateKnowledgeEdgeResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (request == null)
        {
            return UpdateKnowledgeEdgeResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (edgeId == null ||
            !ulong.TryParse(edgeId, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedEdgeId) ||
            parsedEdgeId == 0)
        {
            return UpdateKnowledgeEdgeResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (!request.Weight.HasValue || request.Weight.Value < 0m || request.Weight.Value > 1m)
        {
            return UpdateKnowledgeEdgeResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (request.RowVersion == null ||
            !ulong.TryParse(request.RowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedRowVersion) ||
            parsedRowVersion == 0)
        {
            return UpdateKnowledgeEdgeResult.Failure(ErrorCodes.ValidationFailed);
        }

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == _tenantContext.CenterId!.Value &&
                                      c.Status == CenterStatus.Active &&
                                      !c.IsDeleted, cancellationToken);

        if (center == null)
        {
            return UpdateKnowledgeEdgeResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var edge = await _dbContext.KnowledgeEdges
            .FirstOrDefaultAsync(e => e.EdgeId == parsedEdgeId &&
                                      e.CenterId == _tenantContext.CenterId!.Value &&
                                      !e.IsDeleted, cancellationToken);

        if (edge == null)
        {
            return UpdateKnowledgeEdgeResult.Failure(ErrorCodes.ResourceNotFound);
        }

        await using var transaction = await GraphMutationTransaction.BeginAsync(_dbContext, _tenantContext.CenterId.Value, edge.SubjectId, cancellationToken);
        if (transaction is not null) await _dbContext.Entry(edge).ReloadAsync(cancellationToken);
        if (edge.IsDeleted) return UpdateKnowledgeEdgeResult.Failure(ErrorCodes.ResourceNotFound);

        if (edge.RowVersion != parsedRowVersion)
        {
            return UpdateKnowledgeEdgeResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        if (edge.Weight != request.Weight.Value)
        {
            var usages = await AcademicDependencyGuards.NodeUsageAsync(_dbContext, _tenantContext.CenterId.Value, edge.SourceNodeId, edge.TargetNodeId, cancellationToken);
            if (usages.Count > 0)
                return UpdateKnowledgeEdgeResult.Failure(ErrorCodes.InvalidStateTransition, AcademicDependencyGuards.ClassBlockMessage(usages));
        }
        var before = new { edge.SourceNodeId, edge.TargetNodeId, RelationType = edge.RelationType.ToString(), edge.Weight, edge.IsDeleted, edge.RowVersion };
        edge.Weight = request.Weight.Value;
        edge.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        edge.UpdatedBy = _tenantContext.UserId!.Value;

        edge.RowVersion = parsedRowVersion + 1;
        _dbContext.Entry(edge).Property(x => x.RowVersion).OriginalValue = parsedRowVersion;
        AcademicDependencyGuards.Audit(_dbContext, _tenantContext.CenterId.Value, _tenantContext.UserId.Value,
            "KnowledgeEdgeUpdated", "KnowledgeEdge", edgeId, before,
            new { edge.SourceNodeId, edge.TargetNodeId, RelationType = edge.RelationType.ToString(), edge.Weight, edge.IsDeleted, edge.RowVersion },
            edge.UpdatedAt, "Cập nhật trọng số liên kết tri thức.");

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();
            return UpdateKnowledgeEdgeResult.Failure(ErrorCodes.ConcurrencyConflict);
        }
        catch (DbUpdateException ex) when (AcademicDependencyGuards.IsDatabaseGuard(ex))
        {
            _dbContext.ChangeTracker.Clear();
            return UpdateKnowledgeEdgeResult.Failure(ErrorCodes.InvalidStateTransition, AcademicDependencyGuards.ConcurrentDependencyMessage);
        }

        var dto = new KnowledgeEdgeDto
        {
            EdgeId = edge.EdgeId.ToString(CultureInfo.InvariantCulture),
            SubjectId = edge.SubjectId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            SourceNodeId = edge.SourceNodeId.ToString(CultureInfo.InvariantCulture),
            TargetNodeId = edge.TargetNodeId.ToString(CultureInfo.InvariantCulture),
            RelationType = edge.RelationType.ToString(),
            Weight = edge.Weight,
            RowVersion = edge.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return UpdateKnowledgeEdgeResult.Success(dto);
    }
}
