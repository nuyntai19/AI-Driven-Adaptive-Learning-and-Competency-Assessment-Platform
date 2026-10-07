namespace EduTwin.BLL.AssessmentAndReasoning.AI;

// Capacity/network retry is not a grading failure and must not consume the fallback retry budget.
public sealed class AIAnalysisDeferredException(TimeSpan retryAfter, string errorCode = "AI_PROVIDER_CAPACITY_WAIT")
    : Exception("AI analysis is waiting for provider capacity.")
{
    public TimeSpan RetryAfter { get; } = retryAfter;
    public string ErrorCode { get; } = errorCode;
}
