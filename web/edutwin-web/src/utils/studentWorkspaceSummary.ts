import type { StudentWorkspaceSummaryDto } from "../types/dashboards";

export function studentWorkspaceLabels(data?: StudentWorkspaceSummaryDto, isError = false) {
  const known = data && !isError;
  const count = known ? String(data.assignmentCount) : "—";
  const days = known ? String(data.dailyStreak) : "—";
  return {
    assignmentCount: count,
    streak: `Chuỗi ${days} ngày`,
    shortStreak: `${days} ngày`,
    streakTitle: isError ? "Không tải được chuỗi ngày học. Thử tải lại hoặc mở lại trang." :
      known ? `Ngày có nộp bài/luyện tập liên tiếp theo ${data.timezone}. ${data.studiedToday ? "Hôm nay đã học." : "Hôm nay chưa nộp bài."}` :
        "Đang tải chuỗi ngày học thực tế.",
  };
}
