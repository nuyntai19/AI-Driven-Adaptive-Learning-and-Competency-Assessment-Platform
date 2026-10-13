/**
 * Normalizes an assignment completion rate from the backend (0-100 percentage)
 * ensuring it stays strictly clamped to [0, 100] without erroneously multiplying small values.
 */
export function normalizeCompletionRate(rate: number | null | undefined): number {
  const numeric = typeof rate === "number" && Number.isFinite(rate) ? rate : Number(rate);
  if (!Number.isFinite(numeric)) return 0;
  return Math.min(100, Math.max(0, numeric));
}
