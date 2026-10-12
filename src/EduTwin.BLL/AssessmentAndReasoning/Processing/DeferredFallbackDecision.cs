using System.Text.Json;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Processing;

// A server-owned terminal decision, not a new provider retry. Kept in the job's
// durable diagnostic fields until the preceding evidence has been committed.
internal sealed record DeferredFallbackDecision(string FailureCode, string FailureDetail)
{
    internal const string WaitingCode = "AI_FALLBACK_WAITING_FOR_EARLIER_EVIDENCE";

    internal static DeferredFallbackDecision? Read(AIAnalysisJob job)
    {
        if (job.LastErrorCode != WaitingCode || job.RetryCount < 1) return null;
        try
        {
            var decision = JsonSerializer.Deserialize<DeferredFallbackDecision>(job.LastErrorMessage ?? "");
            return decision is not null && !string.IsNullOrWhiteSpace(decision.FailureCode)
                && !string.IsNullOrWhiteSpace(decision.FailureDetail)
                ? decision : new("AI_ANALYSIS_ATTEMPT_FAILED", "AI analysis attempt failed.");
        }
        catch (JsonException)
        {
            // Do not spend another provider call if a recovery diagnostic is damaged.
            return new("AI_ANALYSIS_ATTEMPT_FAILED", "AI analysis attempt failed.");
        }
    }

    internal static void Write(AIAnalysisJob job, string code, string detail)
    {
        job.LastErrorCode = WaitingCode;
        job.LastErrorMessage = JsonSerializer.Serialize(new DeferredFallbackDecision(
            Sanitize(code, 100), Sanitize(detail, 50)));
    }

    private static string Sanitize(string value, int limit) =>
        new(value.Where(c => !char.IsControl(c)).Take(limit).ToArray());
}
