using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Recommendations.UseCases;

public interface IDismissRecommendationUseCase
{
    Task<RecommendationUseCaseResult> ExecuteAsync(
        ulong recommendationId,
        string? reason,
        CancellationToken cancellationToken);
}

public sealed class DismissRecommendationUseCase : IDismissRecommendationUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IRecommendationEngine _recommendationEngine;
    private readonly TimeProvider _timeProvider;

    public DismissRecommendationUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IRecommendationEngine recommendationEngine,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _recommendationEngine = recommendationEngine ?? throw new ArgumentNullException(nameof(recommendationEngine));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<RecommendationUseCaseResult> ExecuteAsync(
        ulong recommendationId,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return RecommendationUseCaseResult.FailForbidden("Tenant context is not resolved.");
        }

        var centerId = _tenantContext.CenterId.Value;
        var studentId = _tenantContext.UserId.Value;

        var studentExists = await _dbContext.Students
            .AnyAsync(s => s.CenterId == centerId && s.StudentId == studentId && !s.IsDeleted, cancellationToken);
        if (!studentExists)
        {
            return RecommendationUseCaseResult.FailNotFound("Student record not found in center.");
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var result = await _recommendationEngine.DismissAsync(
            centerId,
            studentId,
            recommendationId,
            reason,
            utcNow,
            cancellationToken);

        if (result.NotFound)
        {
            return RecommendationUseCaseResult.FailNotFound(result.ErrorMessage ?? "Recommendation not found.");
        }

        if (result.Conflict)
        {
            return RecommendationUseCaseResult.FailConflict(
                result.ErrorCode ?? "CONFLICT",
                result.ErrorMessage ?? "Cannot dismiss recommendation.");
        }

        var rec = result.Recommendation!;
        var topicNode = await _dbContext.KnowledgeNodes
            .AsNoTracking()
            .SingleOrDefaultAsync(n => n.CenterId == centerId && n.NodeId == rec.TopicNodeId, cancellationToken);

        var dto = new RecommendationDto
        {
            RecommendationId = rec.RecommendationId,
            Type = rec.RecommendationType,
            TopicNodeId = rec.TopicNodeId,
            TopicName = topicNode?.NodeName ?? "",
            QuestionId = rec.QuestionId,
            OpportunityScore = rec.OpportunityScore,
            Explanation = rec.Explanation,
            Status = rec.Status,
            GeneratedAt = rec.GeneratedAt,
            ExpiresAt = rec.ExpiresAt
        };

        return RecommendationUseCaseResult.Success(dto);
    }
}
