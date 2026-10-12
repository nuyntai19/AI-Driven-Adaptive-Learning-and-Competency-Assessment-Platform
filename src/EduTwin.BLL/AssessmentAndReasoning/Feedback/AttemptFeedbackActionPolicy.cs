using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Feedback;

public static class AttemptFeedbackActionPolicy
{
    public static bool HasTeacherEvaluation(ReasoningAnalysis? analysis, bool assignmentApproved) =>
        assignmentApproved || analysis?.OverrideVersion > 0 ||
        analysis?.ReviewDecision is TeacherReviewDecision.Approved or TeacherReviewDecision.Adjusted;

    public static bool CanRequestReview(Attempt attempt, ReasoningAnalysis? analysis,
        bool assignmentApproved, bool pendingRequest, bool voided) =>
        !voided && !pendingRequest && attempt.Status == AttemptStatus.Completed &&
        HasTeacherEvaluation(analysis, assignmentApproved);

    public static bool CanReportQuestion(Attempt attempt, bool pendingRequest, bool voided) =>
        !voided && !pendingRequest && attempt.Status is
            AttemptStatus.Completed or AttemptStatus.NeedsTeacherReview or AttemptStatus.AnalysisFailed;

    public static bool CanRetryAI(Attempt attempt, AIAnalysisJob? job, ReasoningAnalysis? analysis,
        bool assignmentApproved, bool pendingRequest, bool voided) =>
        !attempt.Skipped && !voided && !pendingRequest &&
        !HasTeacherEvaluation(analysis, assignmentApproved) &&
        (analysis is null || analysis.IsFallback) &&
        (job?.Status is AIJobStatus.FailedTerminal or AIJobStatus.FallbackCompleted ||
            (job is null && attempt.Status == AttemptStatus.AnalysisFailed));

    public static bool CanRecoverFallback(Attempt attempt, ReasoningAnalysis analysis) =>
        attempt.ManualRetryCount > 0 && !attempt.Skipped && analysis.IsFallback &&
        !HasTeacherEvaluation(analysis, false);
}
