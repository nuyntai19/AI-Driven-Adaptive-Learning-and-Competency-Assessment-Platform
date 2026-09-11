using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Recommendations.UseCases;

public sealed record GetNextQuestionResult
{
    public bool IsSuccess { get; init; }
    public bool NotFound { get; init; }
    public bool Forbidden { get; init; }
    public string? ErrorMessage { get; init; }
    public NextQuestionDto? Data { get; init; }

    public static GetNextQuestionResult Success(NextQuestionDto data) =>
        new() { IsSuccess = true, Data = data };

    public static GetNextQuestionResult FailNotFound(string message = "Resource not found.") =>
        new() { NotFound = true, ErrorMessage = message };

    public static GetNextQuestionResult FailForbidden(string message = "Forbidden.") =>
        new() { Forbidden = true, ErrorMessage = message };
}

public interface IGetNextQuestionUseCase
{
    Task<GetNextQuestionResult> ExecuteAsync(Guid subjectId, CancellationToken cancellationToken);
}

public sealed class GetNextQuestionUseCase : IGetNextQuestionUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IRecommendationEngine _recommendationEngine;
    private readonly TimeProvider _timeProvider;

    public GetNextQuestionUseCase(
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

    public async Task<GetNextQuestionResult> ExecuteAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return GetNextQuestionResult.FailForbidden("Tenant context is not resolved.");
        }

        var centerId = _tenantContext.CenterId.Value;
        var studentId = _tenantContext.UserId.Value;

        var studentExists = await _dbContext.Students
            .AnyAsync(s => s.CenterId == centerId && s.StudentId == studentId && !s.IsDeleted, cancellationToken);
        if (!studentExists)
        {
            return GetNextQuestionResult.FailNotFound("Student record not found in center.");
        }

        var subjectExists = await _dbContext.Subjects
            .AnyAsync(s => s.CenterId == centerId && s.SubjectId == subjectId && !s.IsDeleted, cancellationToken);
        if (!subjectExists)
        {
            return GetNextQuestionResult.FailNotFound("Subject not found in center.");
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var nextQuestion = await _recommendationEngine.GetNextQuestionAsync(
            centerId,
            studentId,
            subjectId,
            utcNow,
            cancellationToken);

        if (nextQuestion is null)
        {
            return GetNextQuestionResult.FailNotFound("No next question or recommendation available for this subject.");
        }

        return GetNextQuestionResult.Success(nextQuestion);
    }
}
