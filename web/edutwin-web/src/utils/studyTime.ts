export const MIN_STUDY_MINUTES = 5;
export const MAX_STUDY_MINUTES = 300;

/** 90 -> "1 giờ 30 phút". */
export function formatStudyMinutes(minutes: number): string {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  if (h === 0) return `${m} phút`;
  return m === 0 ? `${h} giờ` : `${h} giờ ${m} phút`;
}

/** First sentence of a long explanation, for compact cards; the rest goes behind "Vì sao?". */
export function splitReason(text: string): { lead: string; rest: string } {
  const clean = (text ?? "").trim();
  const match = clean.match(/^(.+?[.!?])(\s+)(.+)$/s);
  if (!match || match[1].length < 12) return { lead: clean, rest: "" };
  return { lead: match[1], rest: match[3].trim() };
}
