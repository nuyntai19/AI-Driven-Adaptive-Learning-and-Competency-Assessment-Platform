using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Recommendations.UseCases;

public sealed record LearningPathUseCaseResult
{
    public bool IsSuccess { get; init; }
    public bool NotFound { get; init; }
    public bool Forbidden { get; init; }
    public string? ErrorMessage { get; init; }
    public LearningPathDto? Data { get; init; }

    public static LearningPathUseCaseResult Success(LearningPathDto data) =>
        new() { IsSuccess = true, Data = data };

    public static LearningPathUseCaseResult FailNotFound(string message = "Learning path not found.") =>
        new() { NotFound = true, ErrorMessage = message };

    public static LearningPathUseCaseResult FailForbidden(string message = "Forbidden.") =>
        new() { Forbidden = true, ErrorMessage = message };
}

public interface IGetActiveLearningPathUseCase
{
    Task<LearningPathUseCaseResult> ExecuteAsync(Guid? subjectId, CancellationToken cancellationToken);
}

public sealed class GetActiveLearningPathUseCase : IGetActiveLearningPathUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IRecommendationEngine _recommendationEngine;

    public GetActiveLearningPathUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IRecommendationEngine recommendationEngine)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _recommendationEngine = recommendationEngine ?? throw new ArgumentNullException(nameof(recommendationEngine));
    }

    public async Task<LearningPathUseCaseResult> ExecuteAsync(Guid? subjectId, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return LearningPathUseCaseResult.FailForbidden("Tenant context is not resolved.");
        }

        var centerId = _tenantContext.CenterId.Value;
        var studentId = _tenantContext.UserId.Value;

        var studentExists = await _dbContext.Students
            .AnyAsync(s => s.CenterId == centerId && s.StudentId == studentId && !s.IsDeleted, cancellationToken);
        if (!studentExists)
        {
            return LearningPathUseCaseResult.FailNotFound("Student record not found in center.");
        }

        if (subjectId.HasValue)
        {
            var subjectExists = await _dbContext.Subjects
                .AnyAsync(s => s.CenterId == centerId && s.SubjectId == subjectId.Value && !s.IsDeleted, cancellationToken);
            if (!subjectExists)
            {
                return LearningPathUseCaseResult.FailNotFound("Subject not found in center.");
            }
        }

        var path = await _recommendationEngine.GetActiveLearningPathAsync(
            centerId,
            studentId,
            subjectId,
            cancellationToken);

        if (path is null)
        {
            return LearningPathUseCaseResult.FailNotFound("No active learning path found.");
        }

        var itemsDto = path.Items
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.RankOrder)
            .Select(i => new LearningPathItemDto
            {
                LearningPathItemId = i.LearningPathItemId,
                TopicNodeId = i.TopicNodeId,
                TopicName = i.TopicNode?.NodeName ?? "",
                RecommendedQuestionId = i.RecommendedQuestionId,
                RankOrder = i.RankOrder,
                OpportunityScore = i.OpportunityScore,
                Reason = i.Reason,
                Status = i.Status
            })
            .ToList();

        var dto = new LearningPathDto
        {
            LearningPathId = path.LearningPathId,
            StudentId = path.StudentId,
            SubjectId = path.SubjectId,
            Strategy = path.Strategy,
            Version = path.Version,
            Status = path.Status,
            GeneratedAt = path.GeneratedAt,
            Items = itemsDto
        };

        return LearningPathUseCaseResult.Success(dto);
    }
}
