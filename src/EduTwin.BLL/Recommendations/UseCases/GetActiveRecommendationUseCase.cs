using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;

namespace EduTwin.BLL.Recommendations.UseCases;

public sealed record RecommendationUseCaseResult
{
    public bool IsSuccess { get; init; }
    public bool NotFound { get; init; }
    public bool Forbidden { get; init; }
    public bool Conflict { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public RecommendationDto? Data { get; init; }

    public static RecommendationUseCaseResult Success(RecommendationDto data) =>
        new() { IsSuccess = true, Data = data };

    public static RecommendationUseCaseResult FailNotFound(string message = "Recommendation not found.") =>
        new() { NotFound = true, ErrorMessage = message };

    public static RecommendationUseCaseResult FailForbidden(string message = "Forbidden.") =>
        new() { Forbidden = true, ErrorMessage = message };

    public static RecommendationUseCaseResult FailConflict(string code, string message) =>
        new() { Conflict = true, ErrorCode = code, ErrorMessage = message };
}

public interface IGetActiveRecommendationUseCase
{
    Task<RecommendationUseCaseResult> ExecuteAsync(Guid? subjectId, CancellationToken cancellationToken);
}

public sealed class GetActiveRecommendationUseCase : IGetActiveRecommendationUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IRecommendationEngine _recommendationEngine;

    public GetActiveRecommendationUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IRecommendationEngine recommendationEngine)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _recommendationEngine = recommendationEngine ?? throw new ArgumentNullException(nameof(recommendationEngine));
    }

    public async Task<RecommendationUseCaseResult> ExecuteAsync(Guid? subjectId, CancellationToken cancellationToken)
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

        if (subjectId.HasValue)
        {
            var subjectExists = await _dbContext.Subjects
                .AnyAsync(s => s.CenterId == centerId && s.SubjectId == subjectId.Value && !s.IsDeleted, cancellationToken);
            if (!subjectExists)
            {
                return RecommendationUseCaseResult.FailNotFound("Subject not found in center.");
            }
        }

        var rec = await _recommendationEngine.GetActiveRecommendationAsync(
            centerId,
            studentId,
            subjectId,
            cancellationToken);

        if (rec is null)
        {
            return RecommendationUseCaseResult.FailNotFound("No active recommendation found.");
        }

        var dto = new RecommendationDto
        {
            RecommendationId = rec.RecommendationId,
            Type = rec.RecommendationType,
            TopicNodeId = rec.TopicNodeId,
            TopicName = rec.TopicNode?.NodeName ?? "",
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
