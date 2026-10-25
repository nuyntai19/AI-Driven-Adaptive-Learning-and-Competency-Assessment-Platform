export function getDifficultyLabel(difficulty = 1) {
  return difficulty >= 4 ? "Vận dụng cao" : difficulty >= 3 ? "Vận dụng" : difficulty >= 2 ? "Thông hiểu" : "Nhận biết";
}

export function getQuestionTypeLabel(type?: string | null) {
  switch (type?.toLowerCase()) {
    case "multiplechoice": return "Trắc nghiệm";
    case "shortanswer": return "Trả lời ngắn";
    case "numeric": case "numericrational": return "Điền số";
    case "essay": return "Tự luận";
    default: return "Câu hỏi";
  }
}

export function getErrorTypeLabel(type?: string | null) {
  const labels: Record<string, string> = { knowledge: "Kiến thức", skill: "Kỹ năng", reasoning: "Lập luận", behavior: "Thói quen làm bài", presentation: "Trình bày", unknown: "Chưa xác định", none: "Không có lỗi" };
  return labels[type?.toLowerCase() ?? ""] ?? "Cần kiểm tra thêm";
}

export function hasDeterminedGrade(question: { isVoided?: boolean; effectiveIsCorrect?: boolean | null; latestAttempt?: { awardedScore?: number | null; isCorrect?: boolean | null } | null }) {
  return Boolean(question.isVoided || question.latestAttempt?.awardedScore != null || question.latestAttempt?.isCorrect != null || question.effectiveIsCorrect != null);
}
