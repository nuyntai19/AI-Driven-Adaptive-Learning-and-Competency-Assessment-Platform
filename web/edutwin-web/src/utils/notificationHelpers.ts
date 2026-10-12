import type { AccountType, AuthUser } from "../types/auth";
import { isAxiosError } from "axios";

export function notificationContentError(error: unknown): string | null {
  return isAxiosError(error) && error.response?.data?.errorCode === "NOTIFICATION_CONTENT_RESTRICTED"
    ? "Nội dung có dấu hiệu không được phép gửi qua kênh này. Hãy bỏ mật khẩu/token, link/tệp và dữ liệu riêng; với kênh nền tảng, chỉ mô tả lỗi vận hành ở mức khái quát. Nội dung chưa được lưu."
    : null;
}

export const notificationRoutes: Record<AccountType, string> = {
  Student: "/hoc-tap/thong-bao",
  Teacher: "/giao-vien/thong-bao",
  CenterManager: "/quan-ly/thong-bao",
  PlatformAdmin: "/quan-tri-nen-tang/thong-bao",
};
export function notificationAccess(user: Pick<AuthUser, "accountType" | "permissions"> | null) {
  const prefix = user?.accountType === "PlatformAdmin" ? "platform.notifications" : "notifications.inbox";
  return {
    read: user?.permissions.includes(`${prefix}.read_own`) ?? false,
    update: user?.permissions.includes(`${prefix}.update_own`) ?? false,
    acknowledge: user?.permissions.includes(`${prefix}.acknowledge_own`) ?? false,
    reply: user?.permissions.includes(`${prefix}.reply_own`) ?? false,
  };
}
export function notificationBadge(count: number | undefined): string | null {
  return count === undefined || count <= 0 ? null : count > 99 ? "99+" : String(count);
}
export function notificationSendAccess(user: Pick<AuthUser, "accountType" | "permissions"> | null) {
  const prefix = user?.accountType === "PlatformAdmin" ? "platform.notifications" : "notifications.messages";
  return { send: user?.accountType !== "Student" && (user?.permissions.includes(`${prefix}.send_scoped`) ?? false),
    sent: user?.accountType !== "Student" && (user?.permissions.includes(`${prefix}.read_sent_own`) ?? false),
    retract: user?.accountType !== "Student" && (user?.permissions.includes(`${prefix}.retract_own`) ?? false) };
}
export function notificationPollInterval(active: boolean, status?: number, failures = 0, jitter = 0): number | false {
  if (!active || status === 401 || status === 403) return false;
  return Math.min(120_000, 30_000 * 2 ** Math.min(failures, 2)) + Math.max(0, Math.min(3_000, jitter));
}
export function notificationPopoverPosition(anchor: { right: number; bottom: number }, viewport: { width: number; height: number }) {
  const width = Math.max(0, Math.min(400, viewport.width - 24));
  const top = Math.max(12, Math.min(anchor.bottom + 10, viewport.height - 160));
  return { width, left: Math.max(12, Math.min(anchor.right - width, viewport.width - width - 12)), top,
    maxHeight: Math.max(80, viewport.height - top - 12) };
}
export function notificationDateInput(iso?: string): string {
  if (!iso) return "";
  const date = new Date(iso);
  if (!Number.isFinite(date.getTime())) return "";
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}
export function notificationDateBoundary(day: string, end = false): string | undefined {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(day)) return undefined;
  const date = new Date(`${day}T${end ? "23:59:59.999" : "00:00:00"}`);
  if (!Number.isFinite(date.getTime()) || notificationDateInput(date.toISOString()) !== day) return undefined;
  return date.toISOString();
}

export function notificationPreviewStorageKey(centerId: string, userId: string): string {
  return `edutwin:notification-preview:${encodeURIComponent(centerId)}:${encodeURIComponent(userId)}`;
}
export function notificationPreviewActions(visibleCount: number, unreadCount: number, canUpdate: boolean, busy: boolean) {
  const readDisabled = !canUpdate || unreadCount <= 0 || busy;
  const clearDisabled = visibleCount <= 0 || busy;
  const readTitle = !canUpdate ? "Tài khoản không có quyền đánh dấu đã xem"
    : busy ? "Đang cập nhật danh sách, vui lòng chờ"
    : visibleCount <= 0 ? "Chưa có thông báo trong box để đánh dấu"
    : unreadCount <= 0 ? "Các thông báo đang hiện đều đã được xem"
    : "Đánh dấu đã đọc các thông báo đang hiện; không xác nhận thay bạn";
  const hint = busy ? "Đang cập nhật danh sách…"
    : !canUpdate ? "Tài khoản chưa có quyền đánh dấu đã xem. Bạn có thể dọn box khi có thông báo."
    : visibleCount <= 0 ? "Hai nút sẽ dùng được khi có thông báo phù hợp trong box."
    : unreadCount <= 0 ? "Tất cả thông báo đang hiện đã được xem. Bạn vẫn có thể dọn box."
    : `Có ${unreadCount} thông báo chưa xem trong box.`;
  return { readDisabled, clearDisabled, readTitle, hint };
}
export function notificationDeliveryNumber(id: string | null): bigint | null {
  if (!id || !/^\d{1,20}$/.test(id)) return null;
  const value = BigInt(id);
  return value > 0n && value <= 18446744073709551615n ? value : null;
}
export function notificationPreviewVisible(id: string, dismissedThrough: string | null): boolean {
  const delivery = notificationDeliveryNumber(id); const dismissed = notificationDeliveryNumber(dismissedThrough);
  return delivery !== null && (dismissed === null || delivery > dismissed);
}
export function notificationPreviewWatermark(ids: string[], previous: string | null): string | null {
  let latest = notificationDeliveryNumber(previous);
  for (const id of ids) { const value = notificationDeliveryNumber(id); if (value !== null && (latest === null || value > latest)) latest = value; }
  return latest?.toString() ?? null;
}
