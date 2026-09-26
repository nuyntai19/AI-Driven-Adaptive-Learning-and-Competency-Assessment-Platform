export function formatPreliminaryResult(isCorrect: boolean | null | undefined): string {
  if (isCorrect === true) return "Kết quả: Đúng";
  if (isCorrect === false) return "Kết quả: Chưa đúng";
  return "Kết quả: Chờ giáo viên chấm";
}

export function formatAwardedScore(
  awardedScore: number | null,
  maxScore: number,
): string {
  return awardedScore === null
    ? "Điểm: Chưa chấm"
    : `Điểm: ${awardedScore} / ${maxScore}`;
}
