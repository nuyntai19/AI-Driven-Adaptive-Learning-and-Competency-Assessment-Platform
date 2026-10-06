import type { AttemptFeedbackAnalysisDto, AttemptFeedbackGradingDto } from "../types/learning.ts";

/** Display only: never changes correctness or awards points based on AI confidence/quality. */
export function getAttemptFeedbackPresentation(
  grading: AttemptFeedbackGradingDto,
  analysis: AttemptFeedbackAnalysisDto | null | undefined,
  status: string,
) {
  // A raw, non-overridden analysis can still contain a legacy canned message.
  // Only explicit text provenance is sufficient to label the feedback as Gemini.
  const isGemini = Boolean(analysis && !analysis.isFallback && analysis.isRawAI !== false &&
    analysis.feedbackOrigin === "Gemini");
  const feedbackLabel = isGemini ? "Nhận xét từ AI (Gemini)"
    : analysis?.feedbackOrigin === "RuleBased" || analysis?.isFallback ? "Nhận xét theo quy tắc hệ thống"
    : analysis?.feedbackOrigin === "LegacySystem" ? "Thông báo hệ thống (dữ liệu cũ)"
    : "Nhận xét (chưa xác định nguồn)";
  const scoreSourceLabel = grading.source === "Teacher" ? "Giáo viên xác nhận"
    : grading.source === "Deterministic" ? "Bộ chấm tự động theo quy tắc"
    : grading.source === "PendingTeacher" ? "Chờ giáo viên chấm"
    : "Nguồn chấm chưa xác định";
  const answerDisagreement = !analysis?.isFallback && (
    (grading.isCorrect === true && analysis?.answerAssessment === "Incorrect") ||
    (grading.isCorrect === false && analysis?.answerAssessment === "Correct")
  );
  const uncertain = analysis?.answerAssessment === "Uncertain" || analysis?.reasoningVerdict === "Uncertain";
  const hasReasoningConcerns = analysis?.reasoningVerdict === "Invalid" || uncertain ||
    Boolean(analysis?.misconception?.trim()) || Boolean(analysis?.missingSteps?.length) ||
    Boolean(analysis?.errorType && !["None", "NONE", "NoError"].includes(analysis.errorType));
  const pendingTeacher = grading.source === "PendingTeacher" || status === "NeedsTeacherReview";
  const needsReview = pendingTeacher || (grading.source !== "Teacher" && Boolean(
    analysis?.needsTeacherReview || answerDisagreement || uncertain || analysis?.reasoningVerdict === "Invalid",
  ));
  return { isGemini, feedbackLabel, scoreSourceLabel, answerDisagreement, hasReasoningConcerns,
    needsReview, pendingTeacher };
}

/** Equal per-question normalization matches the assignment total of ten points. */
export function normalizeQuestionScore(score: number | null | undefined, maxScore: number, questionCount?: number) {
  const normalize = Number.isInteger(questionCount) && (questionCount ?? 0) > 0 &&
    Number.isFinite(maxScore) && maxScore > 0;
  const scale = normalize ? 10 / questionCount! / maxScore : 1;
  const round = (value: number) => Math.round(value * 100) / 100;
  return {
    maxScore: normalize ? round(10 / questionCount!) : maxScore,
    awardedScore: score == null ? null : round(score * scale),
  };
}
