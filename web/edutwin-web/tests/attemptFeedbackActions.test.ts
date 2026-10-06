import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import { getAttemptFeedbackActions } from "../src/utils/attemptFeedbackActions.ts";
import type { AttemptFeedbackDataDto } from "../src/types/learning.ts";

const feedback = (patch: Partial<AttemptFeedbackDataDto> = {}): AttemptFeedbackDataDto => ({
  attemptId: "7", questionId: "10014", status: "NeedsTeacherReview",
  grading: { maxScore: 10, source: "PendingTeacher" },
  actions: { canRequestTeacherReview: false, canReportQuestion: true },
  retryQuota: { isEligible: false, canRetry: false, manualRetriesUsed: 0,
    manualRetriesRemaining: 3, cooldownRemainingSeconds: 0 }, ...patch,
});

test("waiting for first teacher grading only permits a question report, not an appeal", () => {
  const view = getAttemptFeedbackActions(feedback(), 0);
  assert.equal(view.canRequestTeacherReview, false);
  assert.equal(view.canReportQuestion, true);
  assert.equal(view.showRetryAI, false);
});
test("teacher approved results permit appeals using API eligibility", () => {
  const view = getAttemptFeedbackActions(feedback({ status: "Completed",
    actions: { canRequestTeacherReview: true, canReportQuestion: true } }), 0);
  assert.equal(view.canRequestTeacherReview, true);
});
for (const status of ["Resolved", "Rejected", "Pending"]) {
  test(`request ${status} does not permanently hide both actions`, () => {
    const view = getAttemptFeedbackActions(feedback({
      actions: { canRequestTeacherReview: true, canReportQuestion: true },
      reviewRequest: { requestId: 1, attemptId: 7, studentId: "s", questionId: 10014,
        studentComment: "Lý do cụ thể", status, createdAt: "2026-10-06" },
    }), 0);
    assert.equal(view.canRequestTeacherReview, status !== "Pending");
    assert.equal(view.canReportQuestion, status !== "Pending");
  });
}
test("failed provider and fallback recovery both display retry when server eligible", () => {
  for (const status of ["AnalysisFailed", "NeedsTeacherReview"]) {
    const view = getAttemptFeedbackActions(feedback({ status,
      retryQuota: { isEligible: true, canRetry: true, manualRetriesUsed: 1,
        manualRetriesRemaining: 2, cooldownRemainingSeconds: 0 } }), 0);
    assert.equal(view.showRetryAI, true); assert.equal(view.canRetryAI, true);
  }
});
test("cooldown is not quota exhaustion; countdown expiry enables retry without stale canRetry", () => {
  const data = feedback({ retryQuota: { isEligible: true, canRetry: false, manualRetriesUsed: 1,
    manualRetriesRemaining: 2, cooldownRemainingSeconds: 30 } });
  assert.equal(getAttemptFeedbackActions(data, 30).showRetryAI, true);
  assert.equal(getAttemptFeedbackActions(data, 30).retryExhausted, false);
  assert.equal(getAttemptFeedbackActions(data, 30).canRetryAI, false);
  assert.equal(getAttemptFeedbackActions(data, 0).canRetryAI, true);
});
test("exhausted retries never trigger AI and successful/reviewed results hide retry", () => {
  const quota = { isEligible: true, canRetry: false, manualRetriesUsed: 3,
    manualRetriesRemaining: 0, cooldownRemainingSeconds: 0 };
  assert.equal(getAttemptFeedbackActions(feedback({ retryQuota: quota }), 0).retryExhausted, true);
  assert.equal(getAttemptFeedbackActions(feedback({ retryQuota: quota }), 0).canRetryAI, false);
  quota.isEligible = false;
  assert.equal(getAttemptFeedbackActions(feedback({ retryQuota: quota }), 0).showRetryAI, false);
});
test("missing eligibility fails closed rather than treating analysis.isFallback as retryable", () => {
  const view = getAttemptFeedbackActions(feedback({ actions: undefined, retryQuota: null }), 0);
  assert.equal(view.canRequestTeacherReview, false); assert.equal(view.canReportQuestion, false);
  assert.equal(view.showRetryAI, false);
});
test("actual UI uses independent actions and the backend jobId contract", () => {
  const source = fs.readFileSync(new URL("../src/components/student/AttemptFeedbackHierarchy.tsx", import.meta.url), "utf8");
  assert.match(source, /getAttemptFeedbackActions\(feedbackData, cooldownSeconds\)/);
  assert.match(source, /actions.canRequestTeacherReview &&/);
  assert.match(source, /actions.canReportQuestion &&/);
  assert.match(source, /res.jobId && onPollJob/);
  assert.doesNotMatch(source, /res.analysisJobId|!reviewRequest &&/);
});
