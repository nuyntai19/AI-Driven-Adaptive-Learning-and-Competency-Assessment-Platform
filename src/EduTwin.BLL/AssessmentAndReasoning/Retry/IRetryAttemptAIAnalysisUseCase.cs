using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Retry;

public interface IRetryAttemptAIAnalysisUseCase
{
    Task<RetryAIAnalysisResult> ExecuteAsync(ulong attemptId, CancellationToken cancellationToken);
}

public sealed class RetryAIAnalysisResult
{
    public bool IsSuccess { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }
    public RetryAIAnalysisDataDto? Data { get; private init; }

    public static RetryAIAnalysisResult Success(RetryAIAnalysisDataDto data) =>
        new() { IsSuccess = true, Data = data };

    public static RetryAIAnalysisResult Failure(string errorCode, string errorMessage) =>
        new() { IsSuccess = false, ErrorCode = errorCode, ErrorMessage = errorMessage };

    public static RetryAIAnalysisResult NotFound() =>
        Failure("NOT_FOUND", "Attempt not found.");

    public static RetryAIAnalysisResult Forbidden() =>
        Failure("FORBIDDEN", "You do not have permission to retry analysis for this attempt.");

    public static RetryAIAnalysisResult QuotaExceeded() =>
        Failure("RETRY_QUOTA_EXCEEDED", "Maximum manual AI analysis retries (3) reached for this attempt.");

    public static RetryAIAnalysisResult CooldownActive(int remainingSeconds) =>
        Failure("RETRY_COOLDOWN_ACTIVE", $"Please wait {remainingSeconds} seconds before requesting another analysis.");
}
