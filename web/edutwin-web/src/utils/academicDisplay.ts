export function goalGapLabel(target: number, current: number) {
  const difference = target - current;
  return difference > 0 ? `Cần thêm ${difference.toFixed(1)} điểm` : difference < 0 ? `Đã vượt ${(-difference).toFixed(1)} điểm` : "Đã đạt mục tiêu";
}
export function questionTypeLabel(type: string) {
  return type === "MultipleChoice" ? "Trắc nghiệm" : type === "ShortAnswer" ? "Đáp án ngắn" : "Tự luận";
}
