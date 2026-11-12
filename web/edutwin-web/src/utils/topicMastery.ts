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

/**
 * Digital-twin mastery and behaviour rates (skip / change-answer) are stored as percentages 0–100
 * (MasteryCalculator, BehaviorTwinUpdater). Display them as is — never multiply by 100 again —
 * clamped to 0–100 and rounded; anything non-numeric reads as 0.
 */
export function twinPercent(value: number | null | undefined): number {
  const n = Number(value);
  return Number.isFinite(n) ? Math.round(Math.min(100, Math.max(0, n))) : 0;
}

/** A measured topic below this mastery (0–100) is reported as a knowledge gap to the teacher. */
export const TEACHER_WEAK_MASTERY = 60;

/** Measured (has evidence) and below the gap threshold. Unmeasured topics are not "weak". */
export function isWeakTopic(topic: { masteryPercentage: number; evidenceCount: number }, threshold = TEACHER_WEAK_MASTERY): boolean {
  return topic.evidenceCount > 0 && twinPercent(topic.masteryPercentage) < threshold;
}

const deltaFormatter = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 1 });

/** Mastery change in percentage points (0–100 scale), e.g. 2.5 → "2,5", -10 → "-10". */
export function formatMasteryDelta(delta: number | null | undefined): string {
  const n = Number(delta);
  return Number.isFinite(n) ? deltaFormatter.format(Math.max(-100, Math.min(100, n))) : "0";
}

/** Average mastery (0–100) over measured topics only; unmeasured topics are not 0%. */
export function averageMeasuredMastery(topics: { masteryPercentage: number; evidenceCount: number }[]): number | null {
  const measured = topics.filter((t) => t.evidenceCount > 0);
  if (!measured.length) return null;
  return twinPercent(measured.reduce((sum, t) => sum + twinPercent(t.masteryPercentage), 0) / measured.length);
}
