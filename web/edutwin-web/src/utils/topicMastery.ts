export type TopicMasteryStatus = "noData" | "needsWork" | "developing" | "mastered";

/** A topic with no evidence is unmeasured, which is different from measured at 0%. */
export function topicMasteryStatus(mastery: number, evidenceCount: number): TopicMasteryStatus {
  if (evidenceCount <= 0) return "noData";
  if (mastery >= 75) return "mastered";
  return mastery >= 50 ? "developing" : "needsWork";
}

export const TOPIC_STATUS_LABELS: Record<TopicMasteryStatus, string> = {
  needsWork: "Cần củng cố",
  developing: "Đang phát triển",
  mastered: "Đã vững",
  noData: "Chưa có dữ liệu",
};
