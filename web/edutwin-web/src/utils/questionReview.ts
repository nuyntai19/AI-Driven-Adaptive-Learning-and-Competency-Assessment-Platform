import type { StudentAssignmentQuestionDto } from "../types/assignments";

export const resolveQuestionReviewAttemptId = (
  question?: StudentAssignmentQuestionDto | null
): string | number | null =>
  question?.submittedAttemptId ?? question?.latestAttempt?.attemptId ?? null;

export const isFeedbackForQuestion = (
  expectedQuestionId: string | number,
  feedbackQuestionId: string | number
): boolean => String(expectedQuestionId) === String(feedbackQuestionId);
