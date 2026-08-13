namespace EduTwin.BLL.AssessmentAndReasoning.Jobs;

public enum AIAnalysisJobTransitionResult
{
    Success,
    InvalidTransition,
    NotAvailable,
    InvalidLease,
    InvalidInput,
    RetryExhausted
}
