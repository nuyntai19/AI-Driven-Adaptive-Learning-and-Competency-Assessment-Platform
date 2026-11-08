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
