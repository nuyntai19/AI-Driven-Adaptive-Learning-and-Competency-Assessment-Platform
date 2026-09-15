import { isAxiosError } from "axios";
import type { ProblemDetails } from "../types/auth";
import type { AccountType } from "../types/auth";
import type {
  AuthorizationAuditQueryParams,
  AuthorizationRoleDto,
  AuthorizationRoleQueryParams,
  AuthorizationRoleStatus,
  AuthorizationUserQueryParams,
  AssignedAuthorizationRoleDto,
  PermissionDto,
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
    if (!filters.to.includes("T")) {
      toDate.setHours(23, 59, 59, 999);
    }
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
  description?: string,
): { valid: boolean; error?: string } => {
  const trimmedCode = roleCode.trim();
  const trimmedName = roleName.trim();

  if (!trimmedCode) {
    return { valid: false, error: "Mã vai trò không được để trống." };
  }
  if (trimmedCode.length > 64 || !/^[A-Z][A-Z0-9_]*$/.test(trimmedCode)) {
    return {
      valid: false,
      error: "Mã vai trò phải từ 1-64 ký tự, bắt đầu bằng chữ in hoa và chỉ chứa chữ in hoa, chữ số hoặc dấu gạch dưới.",
    };
  }
  if (!trimmedName) {
    return { valid: false, error: "Tên vai trò không được để trống." };
  }
  if (trimmedName.length > 150) {
    return { valid: false, error: "Tên vai trò không được vượt quá 150 ký tự." };
  }
  if (description && description.length > 500) {
    return { valid: false, error: "Mô tả vai trò không được vượt quá 500 ký tự." };
  }
  if (!["Teacher", "Student", "CenterManager"].includes(accountType)) {
    return { valid: false, error: "Loại tài khoản không hợp lệ." };
  }
  return { valid: true };
};

export interface EffectivePermissionItem {
  code: string;
  description: string;
  sourceRoles: string[];
}

export function hydrateKnownRolesFromUserAuth(
  prevKnownRoles: Map<string, AuthorizationRoleDto>,
  assignedRoles: AssignedAuthorizationRoleDto[],
): Map<string, AuthorizationRoleDto> {
  const next = new Map(prevKnownRoles);
  for (const r of assignedRoles) {
    const existing = next.get(r.roleId);
    if (!existing) {
      next.set(r.roleId, {
        roleId: r.roleId,
        roleCode: r.roleCode,
        roleName: r.roleName,
        accountType: r.accountType,
        description: null,
        isSystemRole: false,
        status: "Active",
        permissionCodes: r.permissionCodes ?? [],
        activeUserCount: 1,
        rowVersion: "1",
      });
    } else if (r.permissionCodes && r.permissionCodes.length > 0 && existing.permissionCodes.length === 0) {
      next.set(r.roleId, {
        ...existing,
        permissionCodes: r.permissionCodes,
      });
    }
  }
  return next;
}

export function computeEffectivePermissionsBreakdown(
  selectedRoleIds: string[],
  knownRoles: Map<string, AuthorizationRoleDto>,
  catalog: PermissionDto[],
): EffectivePermissionItem[] {
  const selectedRoles = selectedRoleIds
    .map((id) => knownRoles.get(id))
    .filter((r): r is AuthorizationRoleDto => r !== undefined);
  const permissionSources: Record<string, string[]> = {};

  for (const role of selectedRoles) {
    for (const code of role.permissionCodes) {
      (permissionSources[code] ??= []).push(role.roleName);
    }
  }

  const permissionDescriptions = new Map(
    catalog.map((p) => [p.permissionCode, p.description]),
  );

  return Object.entries(permissionSources)
    .map(([code, sourceRoles]) => ({
      code,
      description: permissionDescriptions.get(code) ?? "Quyền vận hành",
      sourceRoles,
    }))
    .sort((a, b) => a.code.localeCompare(b.code));
}

export function getMissingCanonicalRoleIds(
  selectedRoleIds: string[],
  knownRoles: Map<string, AuthorizationRoleDto>,
  fetchedRoleIds: Set<string>,
): string[] {
  return selectedRoleIds.filter((id) => {
    const r = knownRoles.get(id);
    return (!r || r.permissionCodes.length === 0) && !fetchedRoleIds.has(id);
  });
}
