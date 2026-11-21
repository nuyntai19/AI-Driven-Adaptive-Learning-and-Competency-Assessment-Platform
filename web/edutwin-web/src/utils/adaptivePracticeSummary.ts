/** Questions the student actually answered; a question shown but left unanswered does not count. */
export function answeredQuestionCount(summary: { correctCount: number; incorrectCount: number; partialCount: number; pendingCount: number }): number {
  return summary.correctCount + summary.incorrectCount + summary.partialCount + summary.pendingCount;
}

export function formatPracticeDuration(totalSeconds: number): string {
  const seconds = Math.max(0, Math.round(totalSeconds));
  if (seconds < 60) return `${seconds} giây`;
  const remainder = seconds % 60;
  return remainder ? `${Math.floor(seconds / 60)} phút ${remainder} giây` : `${seconds / 60} phút`;
}

const END_LABELS: Record<string, string> = {
  GoalAchievedWithEvidence: "Đạt mục tiêu",
  RemediationNeeded: "Cần củng cố kiến thức nền",
  QuestionLimitReached: "Đã làm đủ số câu",
  TimeBudgetReached: "Hết thời gian",
  OutOfQuestions: "Hết câu hỏi",
  StudentStopped: "Bạn đã dừng phiên",
};

/** Says how a session actually ended instead of calling every ending "completed". */
export function practiceEndLabel(closeReason?: string | null): string {
  return (closeReason && END_LABELS[closeReason]) || "Đã kết thúc";
}

export function practiceGoalLabel(status: string, requiresIntervention = false): string {
  if (requiresIntervention || status === "RemediationRecommended") return "Cần bổ trợ";
  switch (status) {
    case "Achieved": return "Đạt mục tiêu";
    case "PartiallyAchieved": return "Đạt một phần";
    case "NotAchieved": return "Chưa đạt";
    default: return "Chưa xác định";
  }
}

export function teacherPracticeEndLabel(reason?: string | null): string {
  if (reason === "StudentStopped") return "Học sinh dừng";
  if (reason === "GoalAchievedWithEvidence") return "Đủ bằng chứng";
  if (reason === "RemediationNeeded") return "Khó khăn lặp lại";
  if (reason === "Expired") return "Phiên hết hiệu lực";
  if (reason === "EvaluationFailed") return "Không hoàn tất đánh giá";
  return practiceEndLabel(reason);
}
