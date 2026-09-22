using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Recommendations.UseCases;

public interface IGetLearningPathTopicsUseCase
{
    Task<List<LearningPathTopicNodeDto>> ExecuteAsync(Guid subjectId, CancellationToken cancellationToken);
}

public sealed class GetLearningPathTopicsUseCase : IGetLearningPathTopicsUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetLearningPathTopicsUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public async Task<List<LearningPathTopicNodeDto>> ExecuteAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            throw new InvalidOperationException("Tenant context is not resolved.");
        }

        var centerId = _tenantContext.CenterId.Value;
        var studentId = _tenantContext.UserId.Value;

        var knowledgeNodes = await _dbContext.KnowledgeNodes
            .AsNoTracking()
            .Where(k => k.CenterId == centerId && k.SubjectId == subjectId && !k.IsDeleted)
            .OrderBy(k => k.OrderIndex)
            .ThenBy(k => k.NodeName)
            .ToListAsync(cancellationToken);

        var nodeIds = knowledgeNodes.Select(k => k.NodeId).ToList();
        if (nodeIds.Count == 0)
        {
            return new List<LearningPathTopicNodeDto>();
        }

        var twins = await _dbContext.KnowledgeTwins
            .AsNoTracking()
            .Where(kt => kt.CenterId == centerId && kt.StudentId == studentId && nodeIds.Contains(kt.TopicNodeId) && !kt.IsDeleted)
            .ToDictionaryAsync(kt => kt.TopicNodeId, cancellationToken);

        return knowledgeNodes.Select(k =>
        {
            twins.TryGetValue(k.NodeId, out var twin);
            return new LearningPathTopicNodeDto
            {
                TopicNodeId = k.NodeId.ToString(),
                NodeName = k.NodeName,
                CurrentMastery = twin != null ? Math.Round(twin.MasteryPercentage, 1) : 0m,
                EvidenceCount = twin?.EvidenceCount ?? 0,
                Description = k.Description
            };
        }).ToList();
    }
}
