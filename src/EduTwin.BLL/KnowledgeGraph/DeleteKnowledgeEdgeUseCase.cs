using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.KnowledgeGraph;

public class DeleteKnowledgeEdgeUseCase : IDeleteKnowledgeEdgeUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public DeleteKnowledgeEdgeUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<DeleteKnowledgeEdgeResult> ExecuteAsync(
        string edgeId,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext == null || !_tenantContext.IsResolved ||
            _tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty ||
            _tenantContext.UserId == null || _tenantContext.UserId == Guid.Empty ||
            string.IsNullOrEmpty(_tenantContext.Role) ||
            (_tenantContext.Role != nameof(UserRole.Teacher) && _tenantContext.Role != nameof(UserRole.CenterManager)))
        {
            return DeleteKnowledgeEdgeResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (string.IsNullOrWhiteSpace(edgeId) ||
            edgeId.Trim() != edgeId ||
            !ulong.TryParse(edgeId, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedEdgeId) ||
            parsedEdgeId == 0)
        {
            return DeleteKnowledgeEdgeResult.Failure(ErrorCodes.ValidationFailed);
        }

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == _tenantContext.CenterId!.Value &&
                                      c.Status == CenterStatus.Active &&
                                      !c.IsDeleted, cancellationToken);

        if (center == null)
        {
            return DeleteKnowledgeEdgeResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var edge = await _dbContext.KnowledgeEdges
            .FirstOrDefaultAsync(e => e.EdgeId == parsedEdgeId &&
                                      e.CenterId == _tenantContext.CenterId!.Value &&
                                      !e.IsDeleted, cancellationToken);

        if (edge == null)
        {
            return DeleteKnowledgeEdgeResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var originalRowVersion = edge.RowVersion;

        edge.IsDeleted = true;
        edge.DeletedAt = now;
        edge.DeletedBy = _tenantContext.UserId;
        edge.UpdatedAt = now;
        edge.UpdatedBy = _tenantContext.UserId!.Value;
        edge.RowVersion = originalRowVersion + 1;

        _dbContext.Entry(edge).Property(x => x.RowVersion).OriginalValue = originalRowVersion;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return DeleteKnowledgeEdgeResult.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();
            return DeleteKnowledgeEdgeResult.Failure(ErrorCodes.ConcurrencyConflict);
        }
    }
}
