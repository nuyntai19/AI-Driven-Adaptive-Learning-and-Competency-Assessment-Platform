import type { TeacherReviewQueueItemDto, ErrorType } from "../types/reviews";
import type { AssignmentProgressItemDto } from "../types/assignments";
import { normalizeQuestionScore } from "./attemptFeedbackPresentation.ts";

export interface QuestionDefaultFormValues {
  awardedScore: number;
  isCorrectVal: boolean | null;
  reasoningQuality: number;
  errorTypeVal: ErrorType;
  feedbackVal: string;
  overrideReasonVal: string;
}

/** Keep an AI opinion separate from the authoritative, nullable answer verdict. */
export function questionAnswerVerdictBadge(
  question: TeacherReviewQueueItemDto | null | undefined
): { label: string; tone: "correct" | "incorrect" | "pending" } {
  if (question?.isCorrect === true) {
    return { label: "✓ Học sinh làm đúng", tone: "correct" };
  }
  if (question?.isCorrect === false) {
    return { label: "✗ Học sinh làm sai", tone: "incorrect" };
  }
  if (question?.attemptStatus === "PendingAnalysis" || question?.attemptStatus === "Processing") {
    return { label: "Đang chờ chấm", tone: "pending" };
  }
  // Legacy analyses may have an answer assessment but no suggested score.
  // Show that opinion without inventing a grade or promoting it to a final verdict.
  if (question?.isFallback === false && question.answerAssessment === "Correct") {
    return { label: "AI đánh giá đúng · Chờ giáo viên duyệt", tone: "pending" };
  }
  if (question?.isFallback === false && question.answerAssessment === "Incorrect") {
    return { label: "AI đánh giá chưa đúng · Chờ giáo viên duyệt", tone: "pending" };
  }
  return { label: "Chưa có kết luận · Chờ giáo viên duyệt", tone: "pending" };
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

  const internalScore =
    currentQuestion.overrideAwardedScore ??
    currentQuestion.awardedScore ??
    (!currentQuestion.isFallback ? currentQuestion.suggestedScore : null) ??
    0;
  const awardedScore = normalizeQuestionScore(internalScore, currentQuestion.maxScore ?? 10).awardedScore ?? 0;

  const isCorrectVal =
    currentQuestion.isCorrect !== undefined && currentQuestion.isCorrect !== null
      ? currentQuestion.isCorrect
      : !currentQuestion.isFallback && currentQuestion.suggestedScore != null && currentQuestion.answerAssessment === "Correct" ? true
      : !currentQuestion.isFallback && currentQuestion.suggestedScore != null && currentQuestion.answerAssessment === "Incorrect" ? false : null;

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

export function questionGradingActions(question: TeacherReviewQueueItemDto | null) {
  const reviewed = Boolean(question?.hasTeacherOverride || question?.reviewDecision === "Approved" || question?.reviewDecision === "Overridden");
  const hasGrade = question?.isCorrect != null && (question.overrideAwardedScore ?? question.awardedScore) != null;
  const hasProposal = !question?.isFallback && question?.suggestedScore != null && ["Correct", "Incorrect"].includes(question.answerAssessment ?? "");
  const ready = question?.attemptStatus === "Completed" || question?.attemptStatus === "NeedsTeacherReview";
  const rubricPending = Boolean(question?.gradingCriteria?.criteria?.length && !question.rubricGrade && !question.suggestedRubricGrade);
  const canEdit = ready || question?.attemptStatus === "AnalysisFailed";
  const requestedReview = Boolean(question?.hasStudentReviewRequest);
  return { reviewed, canConfirm: ready && (hasGrade || hasProposal) && !rubricPending && (!reviewed || requestedReview),
    needsManualGrade: canEdit && ((!hasGrade && !hasProposal) || rubricPending), canEdit,
    needsReview: canEdit && (requestedReview || (!reviewed && question?.attemptStatus !== "Completed")) };
}

export function gradingFormIsDirty(values: QuestionDefaultFormValues, baseline: QuestionDefaultFormValues) {
  return (Object.keys(baseline) as (keyof QuestionDefaultFormValues)[]).some(key => values[key] !== baseline[key]);
}

export function finalApprovalBlockReason(student: AssignmentProgressItemDto | null | undefined, dirty: boolean, busy: boolean): string | null {
  if (busy) return "Đang lưu hoặc cập nhật kết quả, vui lòng chờ.";
  if (dirty) return "Có đánh giá câu hỏi chưa lưu. Hãy lưu hoặc hủy thay đổi trước khi chốt.";
  if (student?.teacherFinalReviewStatus === "Approved") return "Kết quả toàn bài đã được chốt.";
  const eligibility = student?.finalReviewEligibility;
  if (!eligibility) return "Chưa tải được điều kiện chốt bài từ server.";
  if (!eligibility.canApprove) return eligibility.blockReason || "Bài tập còn câu chưa xử lý xong.";
  return null;
}
