import type { AttemptFeedbackDataDto } from "../types/learning";

export function getAttemptFeedbackActions(feedback: AttemptFeedbackDataDto, cooldown: number) {
  const pendingRequest = feedback.reviewRequest?.status === "Pending";
  const quota = feedback.retryQuota;
  // Eligibility comes from the API; cooldown is kept separate so its timer can
  // expire without leaving the button hidden behind a stale canRetry snapshot.
  const showRetryAI = quota?.isEligible === true;
  return {
    showRetryAI,
    retryExhausted: showRetryAI && quota.manualRetriesRemaining <= 0,
    canRetryAI: showRetryAI && quota.manualRetriesRemaining > 0 && cooldown <= 0,
    canRequestTeacherReview: !pendingRequest && feedback.actions?.canRequestTeacherReview === true,
    canReportQuestion: !pendingRequest && feedback.actions?.canReportQuestion === true,
  };
}
