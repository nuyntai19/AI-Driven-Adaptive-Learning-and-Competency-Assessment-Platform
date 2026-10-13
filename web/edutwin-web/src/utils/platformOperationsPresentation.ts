import type { AiUsageGroup } from '../types/platformOperations.ts';

export const jobLabels: Record<string, string> = { Pending: 'Đang chờ', Processing: 'Đang xử lý', Completed: 'Hoàn thành', FallbackCompleted: 'Hoàn thành bằng dự phòng', FailedTerminal: 'Lỗi kết thúc' };
export const stageLabels: Record<string, string> = { visual: 'Đọc ảnh độc lập', grading: 'Phân tích / chấm AI', learning_path: 'Diễn đạt lộ trình', other: 'Lượt gọi khác' };
export const alertLabels: Record<string, string> = { queue_backlog: 'Hàng đợi vượt ngưỡng', terminal_failures: 'Lỗi kết thúc tăng', worker_stale: 'Worker thiếu tín hiệu', volume_low: 'Volume API ít chỗ trống', backup_stale: 'Sao lưu lỗi hoặc quá cũ' };
export function utcDate(value: string): Date { return new Date(/[zZ]$|[+-]\d{2}:\d{2}$/.test(value) ? value : value + 'Z'); }
export function opsTime(value: string | null | undefined): string { return value ? utcDate(value).toLocaleString('vi-VN') : 'Chưa ghi nhận'; }
export function bytesLabel(bytes: number | null | undefined): string {
  if (bytes == null) return 'Chưa đo được';
  if (bytes === 0) return '0 B';
  const units = ['B', 'KiB', 'MiB', 'GiB', 'TiB']; const index = Math.min(4, Math.floor(Math.log(Math.max(1, bytes)) / Math.log(1024)));
  return `${(bytes / 1024 ** index).toLocaleString('vi-VN', {maximumFractionDigits: 1})} ${units[index]}`;
}
export function usageSummary(groups: AiUsageGroup[]) {
  const calls = groups.reduce((sum, g) => sum + g.calls, 0);
  const pricedCalls = groups.reduce((sum, g) => sum + g.pricedCalls, 0);
  const reportedCalls = groups.reduce((sum, g) => sum + g.tokenReportedCalls, 0);
  const failed = groups.reduce((sum, g) => sum + g.failedCalls, 0);
  const totalTokens = reportedCalls === 0 ? null : groups.reduce((sum, g) => sum + (g.totalTokens ?? 0), 0);
  const estimatedUsd = pricedCalls === 0 ? null : groups.reduce((sum, g) => sum + (g.estimatedUsd ?? 0), 0);
  const averageMs = calls === 0 ? null : groups.reduce((sum, g) => sum + g.averageDurationMs * g.calls, 0) / calls;
  return {calls, pricedCalls, reportedCalls, failed, totalTokens, estimatedUsd, averageMs};
}
export function workerStatus(last: string | null, now = Date.now()): string {
  if (!last) return 'Chưa có tín hiệu';
  return now - utcDate(last).getTime() > 300_000 ? 'Tín hiệu đã cũ' : 'Đang hoạt động';
}
export function usdLabel(value: number | null): string { return value == null ? 'Chưa xác minh giá' : value.toLocaleString('vi-VN', {style: 'currency', currency: 'USD', maximumFractionDigits: 6}); }
