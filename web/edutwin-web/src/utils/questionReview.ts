import type { StudentAssignmentQuestionDto } from "../types/assignments";

export const resolveQuestionReviewAttemptId = (
  question?: StudentAssignmentQuestionDto | null
): string | number | null =>
  question?.submittedAttemptId ?? question?.latestAttempt?.attemptId ?? null;

export const isFeedbackForQuestion = (
  expectedQuestionId: string | number,
  feedbackQuestionId: string | number
): boolean => String(expectedQuestionId) === String(feedbackQuestionId);

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
