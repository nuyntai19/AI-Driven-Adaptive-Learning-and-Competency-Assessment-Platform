using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Jobs;

/// <summary>
/// Applies deterministic in-memory transitions to an analysis job.
/// The caller remains responsible for persisting changes with optimistic
/// concurrency and an atomic lease claim. Error inputs must already be
/// redacted because this state machine only normalizes their storage shape.
/// </summary>
public interface IAIAnalysisJobStateMachine
{
    AIAnalysisJobTransitionResult Claim(
        AIAnalysisJob job,
        DateTime utcNow,
        string? leaseOwner,
        DateTime leaseUntil);

    AIAnalysisJobTransitionResult Retry(
        AIAnalysisJob job,
        DateTime utcNow,
        DateTime retryAvailableAt,
        string? lastErrorCode = null,
        string? lastErrorMessage = null);

    AIAnalysisJobTransitionResult RecoverExpiredLease(
        AIAnalysisJob job,
        DateTime utcNow);

    AIAnalysisJobTransitionResult Complete(
        AIAnalysisJob job,
        DateTime utcNow);

    AIAnalysisJobTransitionResult CompleteFallback(
        AIAnalysisJob job,
        DateTime utcNow,
        string? lastErrorCode = null,
        string? lastErrorMessage = null);

    AIAnalysisJobTransitionResult FailTerminal(
        AIAnalysisJob job,
        DateTime utcNow,
        string? lastErrorCode = null,
        string? lastErrorMessage = null);
}
