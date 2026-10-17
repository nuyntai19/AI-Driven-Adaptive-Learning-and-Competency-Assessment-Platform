export const MIN_STUDY_MINUTES = 5;
export const MAX_STUDY_MINUTES = 300;

/** 90 -> "1 giờ 30 phút". */
export function formatStudyMinutes(minutes: number): string {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  if (h === 0) return `${m} phút`;
  return m === 0 ? `${h} giờ` : `${h} giờ ${m} phút`;
}
