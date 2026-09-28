import type { TeacherReviewQueueItemDto, ErrorType } from "../types/reviews";

export interface QuestionDefaultFormValues {
  awardedScore: number;
  isCorrectVal: boolean | null;
  reasoningQuality: number;
  errorTypeVal: ErrorType;
  feedbackVal: string;
  overrideReasonVal: string;
}

/**
 * Resolves form default values when switching the active question in AssignmentGradingWorkspace.
 * Preserves 0 as a valid reasoningQuality, preserves existing errorType,
 * and maintains null for isCorrectVal on unevaluated questions to avoid auto-biasing to True or False.
 */
export function resolveQuestionDefaultFormValues(
  currentQuestion: TeacherReviewQueueItemDto | null | undefined
): QuestionDefaultFormValues {
  if (!currentQuestion) {
    return {
      awardedScore: 10,
      isCorrectVal: null,
      reasoningQuality: 80,
      errorTypeVal: "None",
      feedbackVal: "",
      overrideReasonVal: "",
    };
  }

  const max = currentQuestion.maxScore ?? 10;
  const awardedScore =
    currentQuestion.overrideAwardedScore ??
    currentQuestion.awardedScore ??
    (currentQuestion.evidence?.trustLevel === "Trusted" ? max : 0);

  const isCorrectVal =
    currentQuestion.isCorrect !== undefined && currentQuestion.isCorrect !== null
      ? currentQuestion.isCorrect
      : null;

  const reasoningQuality =
    currentQuestion.reasoningQuality !== undefined && currentQuestion.reasoningQuality !== null
      ? Math.round(Number(currentQuestion.reasoningQuality))
      : 80;

  const errorTypeVal = (currentQuestion.errorType as ErrorType) || "None";
  const feedbackVal = currentQuestion.teacherFeedback || "";
  const overrideReasonVal = currentQuestion.overrideReason || "";

  return {
    awardedScore,
    isCorrectVal,
    reasoningQuality,
    errorTypeVal,
    feedbackVal,
    overrideReasonVal,
  };
}
