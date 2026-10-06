import type { StudentAssignmentQuestionDto } from "../types/assignments";

export const resolveQuestionReviewAttemptId = (
  question?: StudentAssignmentQuestionDto | null
): string | number | null =>
  question?.submittedAttemptId ?? question?.latestAttempt?.attemptId ?? null;

export const isFeedbackForQuestion = (
  expectedQuestionId: string | number,
  feedbackQuestionId: string | number
): boolean => String(expectedQuestionId) === String(feedbackQuestionId);

/** A submitted question is immutable, including empty/skipped submissions. */
export const isQuestionSubmissionLocked = (
  question?: Partial<StudentAssignmentQuestionDto> | null
): boolean => Boolean(question && (
  question.isVoided || question.latestAttempt || question.submittedAttemptId != null ||
  question.submittedAnswer != null ||
  ["Completed", "NeedsTeacherReview", "PendingAnalysis", "Processing"].includes(question.attemptStatus ?? "")
));

export function canSubmitLearningWork(state: {
  submitted: boolean;
  submitting: boolean;
  pendingAnalysis: boolean;
  expired: boolean;
  autoSubmit: boolean;
}): boolean {
  return !state.submitted && !state.submitting && !state.pendingAnalysis &&
    (!state.expired || state.autoSubmit);
}

export const hasPersistedQuestionSubmission = (
  question: StudentAssignmentQuestionDto
): boolean =>
  Boolean(question.latestAttempt) ||
  question.submittedAttemptId !== null && question.submittedAttemptId !== undefined ||
  question.submittedAnswer !== null && question.submittedAnswer !== undefined ||
  question.attemptStatus !== null && question.attemptStatus !== undefined;

/**
 * Local drafts can be discarded only after the detail endpoint has returned a
 * persisted submission state for every question in the assignment.
 */
export const isAssignmentReviewHydrated = (
  questions: StudentAssignmentQuestionDto[],
  expectedQuestionCount: number
): boolean =>
  expectedQuestionCount > 0 &&
  questions.length === expectedQuestionCount &&
  questions.every(hasPersistedQuestionSubmission);
