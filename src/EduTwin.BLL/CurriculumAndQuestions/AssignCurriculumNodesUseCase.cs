using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class AssignCurriculumNodesUseCase : IAssignCurriculumNodesUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public AssignCurriculumNodesUseCase(
        EduTwinDbContext dbContext, 
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<AssignCurriculumNodesResult> ExecuteAsync(Guid curriculumId, AssignCurriculumNodesRequest request, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return AssignCurriculumNodesResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerId = _tenantContext.CenterId.Value;
        var actorId = _tenantContext.UserId.Value;

        if (request.NodeIds == null || string.IsNullOrWhiteSpace(request.RowVersion) || !ulong.TryParse(request.RowVersion, out var rowVersion))
        {
            return AssignCurriculumNodesResult.Failure(ErrorCodes.ValidationFailed);
        }

        var parsedNodeIds = new List<ulong>();
        foreach (var nodeIdStr in request.NodeIds)
        {
            if (!ulong.TryParse(nodeIdStr, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedNodeId) || parsedNodeId == 0)
                return AssignCurriculumNodesResult.Failure(ErrorCodes.ValidationFailed);
            parsedNodeIds.Add(parsedNodeId);
        }

        var curriculum = await _dbContext.Curriculums
            .FirstOrDefaultAsync(c => c.CurriculumId == curriculumId && 
                                      c.CenterId == centerId && 
                                      !c.IsDeleted, cancellationToken);

        if (curriculum == null)
        {
            return AssignCurriculumNodesResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (curriculum.RowVersion != rowVersion || curriculum.ReviewStatus != ReviewStatus.Draft)
        {
            return AssignCurriculumNodesResult.Failure(ErrorCodes.ValidationFailed); 
        }

        if (parsedNodeIds.Count > 0)
        {
            var distinctNodeIds = parsedNodeIds.Distinct().ToList();
            var dbNodesCount = await _dbContext.KnowledgeNodes
                .Where(n => n.CenterId == centerId && 
                            n.SubjectId == curriculum.SubjectId && 
                            n.IsActive && 
                            !n.IsDeleted && 
                            distinctNodeIds.Contains(n.NodeId))
                .CountAsync(cancellationToken);

            if (dbNodesCount != distinctNodeIds.Count)
            {
                return AssignCurriculumNodesResult.Failure(ErrorCodes.ResourceNotFound);
            }
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var existingNodes = await _dbContext.CurriculumNodes
            .Where(cn => cn.CurriculumId == curriculumId && cn.CenterId == centerId)
            .ToListAsync(cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.CurriculumNodes.RemoveRange(existingNodes);

            var newNodes = new List<CurriculumNode>();
            for (int i = 0; i < parsedNodeIds.Count; i++)
            {
                newNodes.Add(new CurriculumNode
                {
                    CenterId = centerId,
                    CurriculumId = curriculumId,
                    NodeId = parsedNodeIds[i],
                    OrderIndex = (uint)(i + 1),
                    CreatedAt = now
                });
            }

            if (newNodes.Count > 0)
            {
                _dbContext.CurriculumNodes.AddRange(newNodes);
            }

            curriculum.UpdatedAt = now;
            curriculum.UpdatedBy = actorId;
            curriculum.RowVersion++;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var classes = await _dbContext.CurriculumClasses
            .AsNoTracking()
            .Where(cc => cc.CurriculumId == curriculumId && cc.CenterId == centerId)
            .Select(cc => cc.ClassId)
            .ToListAsync(cancellationToken);

        var dto = new CurriculumDto
        {
            CurriculumId = curriculum.CurriculumId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            TeacherId = curriculum.TeacherId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            SubjectId = curriculum.SubjectId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            Title = curriculum.Title,
            Description = curriculum.Description,
            SourceFile = curriculum.SourceFile,
            ReviewStatus = curriculum.ReviewStatus.ToString(),
            ClassIds = classes.Select(id => id.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant()).ToList(),
            NodeIds = parsedNodeIds.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList(),
            RowVersion = curriculum.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return AssignCurriculumNodesResult.Success(dto);
    }
}
