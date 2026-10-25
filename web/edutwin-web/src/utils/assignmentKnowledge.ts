import type { AssignmentResultSummaryDto } from "../types/assignments";

export function assignmentResultLabel(summary: AssignmentResultSummaryDto) {
  const evaluated = Math.max(0, summary.evaluatedQuestionCount - (summary.voidedQuestionCount ?? 0));
  if (!evaluated) return "Kết quả: đang chờ chấm";
  const label = summary.teacherFinalReviewStatus === "Approved" && summary.resultStatus === "Final" ? "Kết quả đã duyệt" : "Kết quả tạm";
  const pending = summary.pendingQuestionCount > 0 ? " · còn câu chờ chấm hoặc duyệt" : "";
  return `${label}: đúng ${summary.correctQuestionCount}/${evaluated} câu đã chấm${pending}`;
}

export function assignmentKnowledgeSearch(summary?: AssignmentResultSummaryDto | null) {
  return (summary?.knowledgeAreas ?? []).map(area => `${area.topicName} ${area.skillName}`).join(" ");
}
