export function dashboardGreeting(hour: number): string {
  if (hour < 11) return "Chào buổi sáng";
  if (hour < 13) return "Chào buổi trưa";
  return hour < 18 ? "Chào buổi chiều" : "Chào buổi tối";
}

export interface NudgeInput {
  streak?: number;
  studiedToday?: boolean;
  gainToday?: { topicName: string; delta: number } | null;
}

/** One encouraging sentence, always grounded in what the student actually did. */
export function dashboardNudge({ streak = 0, studiedToday = false, gainToday }: NudgeInput): string {
  if (!studiedToday && streak > 0) return `Học một chút hôm nay để giữ chuỗi ${streak} ngày của bạn nhé.`;
  if (gainToday) return `${gainToday.topicName} vừa tăng ${gainToday.delta.toFixed(1)}% hôm nay. Giữ đà này nhé!`;
  if (studiedToday && streak >= 2) return `Chuỗi ${streak} ngày liên tiếp rồi. Làm tốt lắm!`;
  if (studiedToday) return "Hôm nay bạn đã bắt đầu rồi. Làm thêm một chút nữa nhé!";
  return "Mỗi ngày một bài ngắn là đủ để tiến bộ. Bắt đầu thôi!";
}
