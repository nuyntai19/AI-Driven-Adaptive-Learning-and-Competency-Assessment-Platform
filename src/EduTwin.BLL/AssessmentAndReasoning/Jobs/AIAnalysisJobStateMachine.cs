using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Jobs;

public sealed class AIAnalysisJobStateMachine : IAIAnalysisJobStateMachine
{
    private const int MaxLeaseOwnerLength = 100;
    private const int MaxErrorCodeLength = 100;
    private const int MaxErrorMessageLength = 1000;

    public AIAnalysisJobTransitionResult Claim(
        AIAnalysisJob job,
        DateTime utcNow,
        string? leaseOwner,
        DateTime leaseUntil)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (job.Status != AIJobStatus.Pending)
        {
            return AIAnalysisJobTransitionResult.InvalidTransition;
        }

        if (job.AvailableAt > utcNow)
        {
            return AIAnalysisJobTransitionResult.NotAvailable;
        }

        var normalizedLeaseOwner = Normalize(leaseOwner);
        if (normalizedLeaseOwner is null || normalizedLeaseOwner.Length > MaxLeaseOwnerLength)
        {
            return AIAnalysisJobTransitionResult.InvalidLease;
        }

        if (leaseUntil <= utcNow)
        {
            return AIAnalysisJobTransitionResult.InvalidLease;
        }

        job.Status = AIJobStatus.Processing;
        job.StartedAt = utcNow;
        job.CompletedAt = null;
        job.LeaseOwner = normalizedLeaseOwner;
        job.LeaseUntil = leaseUntil;
        job.UpdatedAt = utcNow;

        return AIAnalysisJobTransitionResult.Success;
    }

    public AIAnalysisJobTransitionResult Retry(
        AIAnalysisJob job,
        DateTime utcNow,
        DateTime retryAvailableAt,
        string? lastErrorCode = null,
        string? lastErrorMessage = null)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (job.Status != AIJobStatus.Processing)
        {
            return AIAnalysisJobTransitionResult.InvalidTransition;
        }

        if (job.RetryCount >= 1)
        {
            return AIAnalysisJobTransitionResult.RetryExhausted;
        }

        if (retryAvailableAt < utcNow)
        {
            return AIAnalysisJobTransitionResult.InvalidInput;
        }

        var normalizedErrorCode = SanitizeError(lastErrorCode, MaxErrorCodeLength);
        var normalizedErrorMessage = SanitizeError(lastErrorMessage, MaxErrorMessageLength);

        job.Status = AIJobStatus.Pending;
        job.RetryCount = 1;
        job.AvailableAt = retryAvailableAt;
        job.StartedAt = null;
        job.CompletedAt = null;
        job.LeaseOwner = null;
        job.LeaseUntil = null;
        job.LastErrorCode = normalizedErrorCode;
        job.LastErrorMessage = normalizedErrorMessage;
        job.UpdatedAt = utcNow;

        return AIAnalysisJobTransitionResult.Success;
    }

    public AIAnalysisJobTransitionResult RecoverExpiredLease(
        AIAnalysisJob job,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (job.Status != AIJobStatus.Processing)
        {
            return AIAnalysisJobTransitionResult.InvalidTransition;
        }

        if (job.LeaseUntil is null)
        {
            return AIAnalysisJobTransitionResult.InvalidLease;
        }

        if (job.LeaseUntil >= utcNow)
        {
            return AIAnalysisJobTransitionResult.NotAvailable;
        }

        job.Status = AIJobStatus.Pending;
        job.AvailableAt = utcNow;
        job.StartedAt = null;
        job.CompletedAt = null;
        job.LeaseOwner = null;
        job.LeaseUntil = null;
        job.UpdatedAt = utcNow;

        return AIAnalysisJobTransitionResult.Success;
    }

    public AIAnalysisJobTransitionResult Complete(
        AIAnalysisJob job,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (job.Status != AIJobStatus.Processing)
        {
            return AIAnalysisJobTransitionResult.InvalidTransition;
        }

        ApplyTerminalTransition(job, utcNow, AIJobStatus.Completed, null, null);
        return AIAnalysisJobTransitionResult.Success;
    }

    public AIAnalysisJobTransitionResult CompleteFallback(
        AIAnalysisJob job,
        DateTime utcNow,
        string? lastErrorCode = null,
        string? lastErrorMessage = null)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (job.Status != AIJobStatus.Processing)
        {
            return AIAnalysisJobTransitionResult.InvalidTransition;
        }

        ApplyTerminalTransition(
            job,
            utcNow,
            AIJobStatus.FallbackCompleted,
            SanitizeError(lastErrorCode, MaxErrorCodeLength),
            SanitizeError(lastErrorMessage, MaxErrorMessageLength));
        return AIAnalysisJobTransitionResult.Success;
    }

    public AIAnalysisJobTransitionResult FailTerminal(
        AIAnalysisJob job,
        DateTime utcNow,
        string? lastErrorCode = null,
        string? lastErrorMessage = null)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (job.Status != AIJobStatus.Processing)
        {
            return AIAnalysisJobTransitionResult.InvalidTransition;
        }

        ApplyTerminalTransition(
            job,
            utcNow,
            AIJobStatus.FailedTerminal,
            SanitizeError(lastErrorCode, MaxErrorCodeLength),
            SanitizeError(lastErrorMessage, MaxErrorMessageLength));
        return AIAnalysisJobTransitionResult.Success;
    }

    private static void ApplyTerminalTransition(
        AIAnalysisJob job,
        DateTime utcNow,
        AIJobStatus status,
        string? lastErrorCode,
        string? lastErrorMessage)
    {
        job.Status = status;
        job.CompletedAt = utcNow;
        job.LeaseOwner = null;
        job.LeaseUntil = null;
        job.LastErrorCode = lastErrorCode;
        job.LastErrorMessage = lastErrorMessage;
        job.UpdatedAt = utcNow;
    }

    private static string? Normalize(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = new string(value.Where(character => !char.IsControl(character)).ToArray())
            .Trim();
        return normalized.Length == 0 ? null : normalized;
    }

    private static string? SanitizeError(string? value, int maximumLength)
    {
        var normalized = Normalize(value);
        if (normalized is null)
        {
            return null;
        }

        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength].Trim();
    }
}
