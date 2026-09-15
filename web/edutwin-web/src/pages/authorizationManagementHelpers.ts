import { isAxiosError } from "axios";
import type { ProblemDetails } from "../types/auth";
import type { AccountType } from "../types/auth";
import type {
  AuthorizationAuditQueryParams,
  AuthorizationRoleDto,
  AuthorizationRoleQueryParams,
  AuthorizationRoleStatus,
  AuthorizationUserQueryParams,
} from "../types/authorization";

export interface ParsedSafeError {
  message: string;
  traceId?: string;
  code?: string;
}

export const parseSafeError = (error: unknown): ParsedSafeError => {
  if (!isAxiosError<ProblemDetails>(error)) {
    return { message: "Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối mạng và thử lại." };
  }

  const data = error.response?.data;
  const code = data?.errorCode;
  const traceId = data?.traceId;

  const messages: Record<string, string> = {
    AUTH_PRIVILEGE_ESCALATION:
      "Không thể cấp quyền cao hơn quyền hiện có của bạn hoặc gán quyền quản trị nền tảng.",
    ROLE_ACCOUNT_TYPE_MISMATCH:
      "Vai trò hoặc quyền hạn không tương thích với loại tài khoản của đối tượng.",
    LAST_TENANT_ADMIN:
      "Không thể thực hiện thao tác vì phải giữ lại ít nhất một quản lý trung tâm có đủ quyền quản trị hợp lệ.",
    CONCURRENCY_CONFLICT:
      "Dữ liệu đã được cập nhật bởi một phiên làm việc khác (OCC Concurrency Conflict). Vui lòng làm mới trang để nhận phiên bản mới nhất.",
    INVALID_STATE_TRANSITION:
      "Không thể thay đổi trạng thái vai trò hệ thống hoặc chuyển trạng thái không hợp lệ.",
    VALIDATION_FAILED:
      "Dữ liệu nhập vào chưa hợp lệ. Vui lòng kiểm tra lại các trường bắt buộc và định dạng.",
    RESOURCE_NOT_FOUND:
      "Không tìm thấy tài nguyên yêu cầu trong trung tâm hiện tại (Fail-closed).",
    DUPLICATE_RESOURCE:
      "Mã vai trò đã tồn tại trong trung tâm. Vui lòng chọn một mã vai trò khác.",
    AUTH_PERMISSION_REQUIRED:
      "Bạn không có đủ quyền hạn để thực hiện thao tác này.",
    CANNOT_DELEGATE_UNOWNED_PERMISSIONS:
      "Bạn chỉ có thể ủy quyền các quyền hạn mà chính bạn hiện đang sở hữu.",
    SYSTEM_ROLE_PROTECTED:
      "Vai trò hệ thống được bảo vệ và không được phép chỉnh sửa hoặc xóa trực tiếp.",
  };

  const safeMessage =
    (code && messages[code]) ||
    "Không thể hoàn tất thao tác do lỗi hệ thống. Vui lòng thử lại hoặc liên hệ quản trị viên.";

  return { message: safeMessage, traceId, code };
};

export const buildRoleQueryParams = (
  page: number,
  pageSize: number,
  search?: string,
  accountType?: AccountType | "all",
  status?: AuthorizationRoleStatus | "all",
): AuthorizationRoleQueryParams => {
  const query: AuthorizationRoleQueryParams = {
    page,
    pageSize,
  };
  const trimmedSearch = search?.trim();
  if (trimmedSearch) {
    query.search = trimmedSearch;
  }
  if (accountType && accountType !== "all") {
    query.accountType = accountType;
  }
  if (status && status !== "all") {
    query.status = status;
  }
  return query;
};

export const buildUserQueryParams = (
  page: number,
  pageSize: number,
  search?: string,
  accountType?: AccountType | "all",
  status?: string | "all",
): AuthorizationUserQueryParams => {
  const query: AuthorizationUserQueryParams = {
    page,
    pageSize,
  };
  const trimmedSearch = search?.trim();
  if (trimmedSearch) {
    query.search = trimmedSearch;
  }
  if (accountType && accountType !== "all") {
    query.accountType = accountType;
  }
  if (status && status !== "all") {
    query.status = status;
  }
  return query;
};

export interface AuditFilters {
  from?: string;
  to?: string;
  actorUserId?: string;
  targetUserId?: string;
  targetId?: string;
  permissionCode?: string;
  actionType?: string;
}

export const buildAuditQueryParams = (
  page: number,
  pageSize: number,
  filters: AuditFilters,
): AuthorizationAuditQueryParams => {
  const query: AuthorizationAuditQueryParams = {
    page,
    pageSize,
  };

  if (filters.from) {
    query.from = new Date(filters.from).toISOString();
  }
  if (filters.to) {
    const toDate = new Date(filters.to);
    toDate.setHours(23, 59, 59, 999);
    query.to = toDate.toISOString();
  }
  if (filters.actorUserId?.trim()) {
    query.actorUserId = filters.actorUserId.trim();
  }
  if (filters.targetUserId?.trim()) {
    query.targetUserId = filters.targetUserId.trim();
  }
  if (filters.targetId?.trim()) {
    query.targetId = filters.targetId.trim();
  }
  if (filters.permissionCode?.trim()) {
    query.permissionCode = filters.permissionCode.trim();
  }
  if (filters.actionType?.trim()) {
    query.actionType = filters.actionType.trim();
  }

  return query;
};

export const isSelfUser = (
  currentUserId: string | undefined,
  targetUserId: string | undefined,
): boolean => {
  if (!currentUserId || !targetUserId) return false;
  return currentUserId.toLowerCase() === targetUserId.toLowerCase();
};

export const filterCompatibleRoles = (
  roles: AuthorizationRoleDto[],
  accountType: AccountType | undefined,
): AuthorizationRoleDto[] => {
  if (!accountType) return [];
  return roles.filter(
    (role) => role.accountType === accountType && role.status === "Active",
  );
};

export const validateRoleCreation = (
  roleCode: string,
  roleName: string,
  accountType: string,
): { valid: boolean; error?: string } => {
  const trimmedCode = roleCode.trim();
  const trimmedName = roleName.trim();

  if (!trimmedCode) {
    return { valid: false, error: "Mã vai trò không được để trống." };
  }
  if (!/^[A-Z0-9_]{3,32}$/.test(trimmedCode)) {
    return {
      valid: false,
      error: "Mã vai trò chỉ gồm 3-32 ký tự chữ in hoa, chữ số và dấu gạch dưới.",
    };
  }
  if (!trimmedName) {
    return { valid: false, error: "Tên vai trò không được để trống." };
  }
  if (trimmedName.length > 100) {
    return { valid: false, error: "Tên vai trò không được vượt quá 100 ký tự." };
  }
  if (!["Teacher", "Student", "CenterManager"].includes(accountType)) {
    return { valid: false, error: "Loại tài khoản không hợp lệ." };
  }
  return { valid: true };
};
