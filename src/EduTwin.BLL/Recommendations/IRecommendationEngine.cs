using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Recommendations;

namespace EduTwin.BLL.Recommendations;

public sealed class RecommendationOperationResult
{
    public bool Success { get; init; }
    public bool NotFound { get; init; }
    public bool Conflict { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public Recommendation? Recommendation { get; init; }

    public static RecommendationOperationResult Ok(Recommendation recommendation) =>
        new() { Success = true, Recommendation = recommendation };

    public static RecommendationOperationResult FailNotFound(string message = "Recommendation not found.") =>
        new() { NotFound = true, ErrorCode = "NOT_FOUND", ErrorMessage = message };

    public static RecommendationOperationResult FailConflict(string code, string message) =>
        new() { Conflict = true, ErrorCode = code, ErrorMessage = message };
}

public interface IRecommendationEngine
{
    Task<Recommendation?> GenerateAndPersistAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        ulong? sourceAttemptId,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<RecommendationOperationResult> AcceptAsync(
        Guid centerId,
        Guid studentId,
        ulong recommendationId,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<RecommendationOperationResult> DismissAsync(
        Guid centerId,
        Guid studentId,
        ulong recommendationId,
        string? reason,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<NextQuestionDto?> GetNextQuestionAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<Recommendation?> GetActiveRecommendationAsync(
        Guid centerId,
        Guid studentId,
        Guid? subjectId,
        CancellationToken cancellationToken);

    Task<LearningPath?> GetActiveLearningPathAsync(
        Guid centerId,
        Guid studentId,
        Guid? subjectId,
        CancellationToken cancellationToken);
}
