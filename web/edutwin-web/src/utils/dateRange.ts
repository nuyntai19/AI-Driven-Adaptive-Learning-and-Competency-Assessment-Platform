/** DateInput values are normalized yyyy-mm-dd; equal dates form a valid one-day range. */
export function dateRangeError(from?: string | null, until?: string | null): string | null {
  return from && until && from > until ? "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc." : null;
}
