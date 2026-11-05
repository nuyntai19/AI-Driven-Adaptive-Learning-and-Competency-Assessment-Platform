using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Recommendations;

namespace EduTwin.BLL.Recommendations;

/// <summary>Production boundary: preserve historical reads/actions, but never create a legacy route from grading callbacks.</summary>
public sealed class V4RecommendationEngine(IRecommendationEngine history) : IRecommendationEngine
{
    public Task<RecommendationGenerationResult> GenerateAndPersistAsync(Guid centerId, Guid studentId, Guid subjectId,
        ulong? sourceAttemptId, DateTime utcNow, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(RecommendationGenerationResult.StaleIgnored("Legacy route generation is retired. V4 uses the new evidence in its accepted goal and agenda."));
    }
    public Task<RecommendationGenerationResult> GenerateForClassAsync(Guid centerId, Guid studentId, Guid subjectId, Guid classId, DateTime utcNow, CancellationToken ct)
        => GenerateAndPersistAsync(centerId, studentId, subjectId, null, utcNow, ct);
    public Task<RecommendationOperationResult> AcceptAsync(Guid centerId, Guid studentId, ulong recommendationId, DateTime utcNow, CancellationToken ct)
        => history.AcceptAsync(centerId, studentId, recommendationId, utcNow, ct);
    public Task<RecommendationOperationResult> DismissAsync(Guid centerId, Guid studentId, ulong recommendationId, string? reason, DateTime utcNow, CancellationToken ct)
        => history.DismissAsync(centerId, studentId, recommendationId, reason, utcNow, ct);
    public Task<NextQuestionDto?> GetNextQuestionAsync(Guid centerId, Guid studentId, Guid subjectId, DateTime utcNow, CancellationToken ct)
        => history.GetNextQuestionAsync(centerId, studentId, subjectId, utcNow, ct);
    public Task<NextQuestionDto?> GetNextForClassAsync(Guid centerId, Guid studentId, Guid subjectId, Guid? classId, DateTime utcNow, CancellationToken ct)
        => history.GetNextForClassAsync(centerId, studentId, subjectId, classId, utcNow, ct);
    public Task<Recommendation?> GetActiveRecommendationAsync(Guid centerId, Guid studentId, Guid? subjectId, CancellationToken ct)
        => history.GetActiveRecommendationAsync(centerId, studentId, subjectId, ct);
    public Task<LearningPath?> GetActiveLearningPathAsync(Guid centerId, Guid studentId, Guid? subjectId, CancellationToken ct)
        => history.GetActiveLearningPathAsync(centerId, studentId, subjectId, ct);
}
