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
    (analysis?.confidence != null && analysis.confidence < 80) ||
    Boolean(analysis?.misconception?.trim()) || Boolean(analysis?.missingSteps?.length) ||
    Boolean(analysis?.errorType && !["None", "NONE", "NoError"].includes(analysis.errorType));
  const pendingTeacher = grading.source === "PendingTeacher" || status === "NeedsTeacherReview";
  const needsReview = pendingTeacher || (grading.source !== "Teacher" && Boolean(
    analysis?.needsTeacherReview || answerDisagreement || uncertain || analysis?.reasoningVerdict === "Invalid",
  ));
  const manualReview = pendingTeacher && grading.reasonCode === "MANUAL_MODE";
  const reviewExplanation = answerDisagreement
    ? "Phân tích nhận thấy đáp án có thể khác kết quả chấm theo quy tắc. Giáo viên cần đối chiếu để chốt kết quả."
    : hasReasoningConcerns
    ? "Có điểm cần kiểm tra trong đáp án hoặc lập luận. Giáo viên sẽ xem xét các điểm này trước khi chốt kết quả."
    : manualReview
    ? analysis?.reasoningVerdict === "Valid"
      ? "AI đánh giá lập luận hợp lệ. Câu trả lời này dùng chế độ chấm thủ công nên giáo viên cần xác nhận điểm cuối cùng; đây không phải cảnh báo AI thiếu tự tin."
      : "Câu trả lời này dùng chế độ chấm thủ công. Giáo viên cần xác nhận điểm cuối cùng; phân tích AI chỉ là thông tin hỗ trợ."
    : pendingTeacher
    ? "Bài làm đang chờ giáo viên xác nhận kết quả cuối cùng. Phân tích AI không tự thay đổi điểm."
    : "Bạn có thể gửi yêu cầu để giáo viên đối chiếu bài làm. Phân tích AI không tự thay đổi điểm.";
  return { isGemini, feedbackLabel, scoreSourceLabel, answerDisagreement, hasReasoningConcerns,
    needsReview, pendingTeacher, manualReview, reviewExplanation };
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
