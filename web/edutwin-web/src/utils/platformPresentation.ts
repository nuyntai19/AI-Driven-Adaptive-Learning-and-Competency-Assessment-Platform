export const AUDIT_ACTION_MAP: Record<string, string> = {
  CenterCreated: "Khởi tạo trung tâm",
  CenterStatusUpdated: "Đổi trạng thái trung tâm",
  CenterMetadataUpdated: "Sửa thông tin trung tâm",
  CenterManagerCreated: "Thêm quản lý mới",
  CenterManagerStatusUpdated: "Đổi trạng thái quản lý",
  CenterPrimaryManagerChanged: "Chuyển quản lý chính",
  CenterManagerPasswordReset: "Đặt lại mật khẩu quản lý",
  PlatformAdminPasswordChanged: "Admin đổi mật khẩu",
  PlatformAdminSessionsRevoked: "Admin thu hồi phiên",
  PlatformDefaultPermissionsGranted: "Bổ sung quyền mặc định nền tảng",
};

export function auditActionLabel(action: string): string {
  return AUDIT_ACTION_MAP[action] || "Cập nhật quản trị";
}

export function auditTargetLabel(target: string): string {
  return ({ Center: "Trung tâm", User: "Tài khoản", Session: "Phiên đăng nhập", Role: "Vai trò", AuthorizationRole: "Vai trò", CenterManager: "Quản lý trung tâm", NotificationDispatch: "Lượt gửi thông báo" } as Record<string, string>)[target] || "Tài nguyên quản trị";
}
