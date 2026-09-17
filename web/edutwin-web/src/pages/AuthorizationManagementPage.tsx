import { useEffect, useMemo, useRef, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { authorizationApi } from "../api/authorizationApi";
import { getCurrentUser } from "../auth/authApi";
import { permissions } from "../auth/permissions";
import { useAuthStore } from "../stores/authStore";
import { CenterManagerThemeScope } from "../components/centerManager/CenterManagerThemeScope";
import type { AccountType } from "../types/auth";
import type {
  AuthorizationAuditDto,
  AuthorizationRoleDto,
  AuthorizationRoleStatus,
  AuthorizationUserItem,
  AuthorizationUserOption,
  PermissionDto,
} from "../types/authorization";
import {
  parseSafeError,
  buildRoleQueryParams,
  buildUserQueryParams,
  buildAuditQueryParams,
  filterCompatibleRoles,
  isSelfUser,
  hydrateKnownRolesFromUserAuth,
  computeEffectivePermissionsBreakdown,
  getMissingCanonicalRoleIds,
  type ParsedSafeError,
} from "./authorizationManagementHelpers";

type Tab = "roles" | "users" | "audit";

const accountTypeLabels: Record<AccountType, string> = {
  CenterManager: "Quản lý trung tâm",
  Teacher: "Giáo viên",
  Student: "Học sinh",
  PlatformAdmin: "Quản trị viên nền tảng",
};

const arraysEqual = (a: string[], b: string[]) => {
  if (a.length !== b.length) return false;
  const setA = new Set(a);
  return b.every((item) => setA.has(item));
};

export const AuthorizationManagementPage = () => {
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const hasPermission = useAuthStore((state) => state.hasPermission);
  const [tab, setTab] = useState<Tab>("roles");
  const [notice, setNotice] = useState("");
  const [failure, setFailure] = useState<ParsedSafeError | null>(null);

  const canReadRoles = hasPermission(permissions.rolesRead);
  const canCreateRoles = hasPermission(permissions.rolesCreate);
  const canUpdateRoles = hasPermission(permissions.rolesUpdate);
  const canArchiveRoles = hasPermission(permissions.rolesArchive);
  const canManagePermissions = hasPermission(permissions.rolesManagePermissions);
  const canReadPermissionCatalog = hasPermission(permissions.permissionsRead);
  const canReadUserRoles = hasPermission(permissions.userRolesRead);
  const canAssignUserRoles = hasPermission(permissions.userRolesAssign);
  const canReadAudit = hasPermission(permissions.auditRead);

  const availableTabs = useMemo(() => {
    const result: Tab[] = [];
    if (canReadRoles || canReadPermissionCatalog) result.push("roles");
    if (canReadUserRoles) result.push("users");
    if (canReadAudit) result.push("audit");
    return result;
  }, [canReadAudit, canReadPermissionCatalog, canReadRoles, canReadUserRoles]);

  useEffect(() => {
    if (!availableTabs.includes(tab) && availableTabs[0]) {
      setTab(availableTabs[0]);
    }
  }, [availableTabs, tab]);

  const permissionQuery = useQuery({
    queryKey: ["authorization", "permissions"],
    queryFn: authorizationApi.listPermissions,
    enabled: canReadPermissionCatalog,
  });

  const showSuccess = (message: string) => {
    setFailure(null);
    setNotice(message);
  };

  const showError = (error: unknown) => {
    setNotice("");
    setFailure(parseSafeError(error));
  };

  const copyToClipboard = (text: string) => {
    if (navigator.clipboard) {
      void navigator.clipboard.writeText(text);
    }
  };

  return (
    <CenterManagerThemeScope data-actor="center-manager">
      <div className="px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
        <div className="mx-auto max-w-[96rem] space-y-6">
          <header className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <div className="flex items-center gap-2">
                <span className="rounded-full bg-[var(--cm-primary)]/10 px-3 py-0.5 text-xs font-bold text-[var(--cm-primary)] border border-[var(--cm-primary)]/20">
                  Tenant RBAC Matrix
                </span>
                <p className="text-sm font-semibold text-[var(--cm-primary)]">Phân quyền động theo trung tâm</p>
              </div>
              <h1 className="mt-1 text-2xl sm:text-3xl font-extrabold text-[var(--cm-text)]">Vai trò và ma trận quyền truy cập</h1>
              <p className="mt-1 text-sm text-[var(--cm-text-secondary)]">
                Quản trị vai trò tùy chỉnh, ma trận quyền và phân công tài khoản theo mô hình Least Privilege. Mọi thay đổi đều được ghi nhật ký kiểm toán và tăng phiên bản xác thực.
              </p>
            </div>
            <Link
              className="cm-secondary-button inline-flex items-center text-sm font-semibold self-start sm:self-auto"
              to="/quan-ly/tong-quan-trung-tam"
            >
              ← Về tổng quan trung tâm
            </Link>
          </header>

          {notice && (
            <div role="status" className="flex items-start gap-3 rounded-xl border border-emerald-500/30 bg-emerald-500/10 p-4 text-sm text-emerald-400 shadow-sm">
              <span className="text-lg">✓</span>
              <div className="flex-1 font-medium">{notice}</div>
              <button type="button" onClick={() => setNotice("")} className="text-emerald-400 hover:text-emerald-300 text-xs font-bold">Đóng</button>
            </div>
          )}

          {failure && (
            <div role="alert" className="flex items-start gap-3 rounded-xl border border-rose-500/30 bg-rose-500/10 p-4 text-sm text-rose-400 shadow-sm">
              <span className="text-lg">⚠</span>
              <div className="flex-1">
                <p className="font-semibold">{failure.message}</p>
                {failure.traceId && (
                  <div className="mt-2 flex items-center gap-2 text-xs font-mono text-rose-300">
                    <span>Trace ID: {failure.traceId}</span>
                    <button
                      type="button"
                      onClick={() => copyToClipboard(failure.traceId!)}
                      className="cm-secondary-button rounded px-1.5 py-0.5 font-sans font-medium text-xs"
                    >
                      Sao chép
                    </button>
                  </div>
                )}
              </div>
              <button type="button" onClick={() => setFailure(null)} className="text-rose-400 hover:text-rose-300 text-xs font-bold">Đóng</button>
            </div>
          )}

          <nav aria-label="Khu vực phân quyền" className="flex flex-wrap gap-2 border-b border-[var(--cm-border-subtle)] pb-4">
            {availableTabs.map((item) => (
              <button
                key={item}
                type="button"
                id={`tab-btn-${item}`}
                onClick={() => {
                  setTab(item);
                  setFailure(null);
                  setNotice("");
                }}
                className={`rounded-xl px-4 py-2 text-sm font-semibold transition-all ${
                  tab === item
                    ? "cm-primary-button shadow-sm"
                    : "cm-secondary-button"
                }`}
              >
                {item === "roles" && "1. Vai trò & Ma trận quyền"}
                {item === "users" && "2. Gán vai trò người dùng"}
                {item === "audit" && "3. Nhật ký kiểm toán phân quyền"}
              </button>
            ))}
          </nav>

          {tab === "roles" && (
            <RolePermissionPanel
              currentUser={user}
              catalog={permissionQuery.data?.data ?? []}
              catalogLoading={permissionQuery.isLoading}
              canCreate={canCreateRoles}
              canUpdate={canUpdateRoles}
              canArchive={canArchiveRoles}
              canReadPermissionCatalog={canReadPermissionCatalog}
              canManagePermissions={canManagePermissions && canReadPermissionCatalog}
              onSuccess={showSuccess}
              onError={showError}
            />
          )}

          {tab === "users" && user && (
            <UserRolePanel
              currentUser={{
                userId: user.userId,
                displayName: user.displayName,
                username: user.username,
                accountType: user.accountType,
                status: user.status,
              }}
              actorPermissions={user.permissions}
              catalog={permissionQuery.data?.data ?? []}
              canRead={canReadUserRoles}
              canReadRoles={canReadRoles}
              canAssign={canAssignUserRoles}
              onSuccess={async (message, changedUserId) => {
                await queryClient.invalidateQueries({ queryKey: ["authorization"] });
                if (changedUserId === user.userId) {
                  await getCurrentUser();
                }
                showSuccess(message);
              }}
              onError={showError}
            />
          )}

          {tab === "audit" && (
            <AuditPanel
              canReadAudit={canReadAudit}
              onError={showError}
            />
          )}
        </div>
      </div>
    </CenterManagerThemeScope>
  );
};

// ==========================================
// 1. Role & Permission Management Panel
// ==========================================

interface RolePermissionPanelProps {
  currentUser: { permissions: string[]; accountType: AccountType } | null;
  catalog: PermissionDto[];
  catalogLoading: boolean;
  canCreate: boolean;
  canUpdate: boolean;
  canArchive: boolean;
  canReadPermissionCatalog: boolean;
  canManagePermissions: boolean;
  onSuccess: (message: string) => void;
  onError: (error: unknown) => void;
}

const RolePermissionPanel = ({
  currentUser,
  catalog,
  catalogLoading,
  canCreate,
  canUpdate,
  canArchive,
  canReadPermissionCatalog,
  canManagePermissions,
  onSuccess,
  onError,
}: RolePermissionPanelProps) => {
  const queryClient = useQueryClient();
  const [selectedId, setSelectedId] = useState("");
  const [rolePage, setRolePage] = useState(1);
  const [searchRole, setSearchRole] = useState("");
  const [filterAccountType, setFilterAccountType] = useState<string>("All");
  const [filterStatus, setFilterStatus] = useState<string>("All");

  const rolesQuery = useQuery({
    queryKey: [
      "authorization",
      "roles-list",
      searchRole,
      filterAccountType,
      filterStatus,
      rolePage,
    ],
    queryFn: () =>
      authorizationApi.listRoles(
        buildRoleQueryParams(
          rolePage,
          10,
          searchRole,
          filterAccountType === "All" ? undefined : (filterAccountType as AccountType),
          filterStatus === "All" ? undefined : (filterStatus as AuthorizationRoleStatus),
        ),
      ),
  });

  const roles = useMemo(() => rolesQuery.data?.data ?? [], [rolesQuery.data?.data]);
  const rolesMeta = rolesQuery.data?.meta;
  const roleLoading = rolesQuery.isLoading;

  // Form states for selected role
  const [roleName, setRoleName] = useState("");
  const [description, setDescription] = useState("");
  const [status, setStatus] = useState<"Active" | "Archived">("Active");
  const [selectedPermissions, setSelectedPermissions] = useState<string[]>([]);
  const [permissionSearch, setPermissionSearch] = useState("");
  const [roleInfoReason, setRoleInfoReason] = useState("");
  const [permissionsReason, setPermissionsReason] = useState("");

  // Create role modal/form
  const [showCreate, setShowCreate] = useState(false);
  const [createCode, setCreateCode] = useState("");
  const [createName, setCreateName] = useState("");
  const [createType, setCreateType] = useState<AccountType>("Teacher");
  const [createDescription, setCreateDescription] = useState("");
  const selected = useMemo(() => {
    return roles.find((role) => role.roleId === selectedId) ?? roles[0];
  }, [roles, selectedId]);

  useEffect(() => {
    if (!selected) return;
    setSelectedId(selected.roleId);
    setRoleName(selected.roleName);
    setDescription(selected.description ?? "");
    setStatus(selected.status);
    setSelectedPermissions(selected.permissionCodes);
    setRoleInfoReason("");
    setPermissionsReason("");
  }, [selected]);

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["authorization"] });

  const createMutation = useMutation({
    mutationFn: authorizationApi.createRole,
    onSuccess: async (response) => {
      await invalidate();
      setSelectedId(response.data.roleId);
      setShowCreate(false);
      setCreateCode("");
      setCreateName("");
      setCreateDescription("");
      onSuccess(`Đã khởi tạo vai trò [${response.data.roleCode}]. Hãy thiết lập ma trận quyền và nhấn "Thay thế permission".`);
    },
    onError,
  });

  const updateMutation = useMutation({
    mutationFn: () =>
      authorizationApi.updateRole(selected!.roleId, {
        roleName: roleName.trim(),
        description: description.trim() || undefined,
        status,
        rowVersion: selected!.rowVersion,
        reason: roleInfoReason.trim(),
      }),
    onSuccess: async () => {
      await invalidate();
      setRoleInfoReason("");
      onSuccess("Đã cập nhật thông tin vai trò thành công.");
    },
    onError: (err) => {
      // On OCC conflict (409), refetch canonical role
      void invalidate();
      onError(err);
    },
  });

  const permissionsMutation = useMutation({
    mutationFn: () =>
      authorizationApi.replaceRolePermissions(selected!.roleId, {
        permissionCodes: selectedPermissions,
        rowVersion: selected!.rowVersion,
        reason: permissionsReason.trim(),
      }),
    onSuccess: async () => {
      await invalidate();
      setPermissionsReason("");
      onSuccess("Đã thay thế toàn bộ ma trận quyền của vai trò thành công.");
    },
    onError: (err) => {
      // On OCC conflict (409), refetch canonical role
      void invalidate();
      onError(err);
    },
  });

  // Permissions categorized by module
  const groupedCatalog = useMemo(() => {
    if (!catalog.length) return [];
    const searchLower = permissionSearch.trim().toLowerCase();
    const filtered = catalog.filter((permission) => {
      if (searchLower) {
        return (
          permission.permissionCode.toLowerCase().includes(searchLower) ||
          permission.description.toLowerCase().includes(searchLower) ||
          permission.module.toLowerCase().includes(searchLower)
        );
      }
      return true;
    });

    const groups: Record<string, PermissionDto[]> = {};
    for (const permission of filtered) {
      (groups[permission.module] ??= []).push(permission);
    }
    return Object.entries(groups).sort(([a], [b]) => a.localeCompare(b));
  }, [catalog, permissionSearch]);

  const actorPermissions = useMemo(() => {
    return new Set(currentUser?.permissions ?? []);
  }, [currentUser?.permissions]);

  // Evaluates a permission against the current selected role and the current actor
  // Guardrail 2: Toggle is only allowed on Active ∩ Compatible ∩ Delegable ∩ Actor Effective Permissions
  // Existing assigned permissions outside this set remain visible read-only and preserved in payload
  const evaluatePermission = (permission: PermissionDto) => {
    const isActive = permission.status === "Active";
    const isOwnerByActor = actorPermissions.has(permission.permissionCode);
    const isDelegable = permission.isDelegable;
    const isCompatible = selected ? permission.allowedAccountTypes.includes(selected.accountType) : false;
    const isOutOfScope = !isOwnerByActor;
    const isWithinDelegableSet = isActive && isCompatible && isDelegable && isOwnerByActor;
    const isAssigned = selectedPermissions.includes(permission.permissionCode);
    const canToggle = canManagePermissions && !selected?.isSystemRole && isWithinDelegableSet;

    return {
      isActive,
      isOwnerByActor,
      isDelegable,
      isCompatible,
      isOutOfScope,
      isAssigned,
      canToggle,
    };
  };

  // Toggle single permission
  const handleTogglePermission = (code: string) => {
    setSelectedPermissions((current) =>
      current.includes(code) ? current.filter((c) => c !== code) : [...current, code],
    );
  };

  // Toggle all valid permissions in a module
  const handleToggleModuleAll = (modulePermissions: PermissionDto[], selectAll: boolean) => {
    const toggleableCodes = modulePermissions
      .filter((p) => {
        const evalRes = evaluatePermission(p);
        return evalRes.canToggle;
      })
      .map((p) => p.permissionCode);

    if (selectAll) {
      setSelectedPermissions((current) => Array.from(new Set([...current, ...toggleableCodes])));
    } else {
      setSelectedPermissions((current) => current.filter((c) => !toggleableCodes.includes(c)));
    }
  };

  const roleInfoReasonValid = roleInfoReason.trim().length >= 3 && roleInfoReason.trim().length <= 1000;
  const permissionsReasonValid = permissionsReason.trim().length >= 3 && permissionsReason.trim().length <= 1000;
  const isSystemRole = Boolean(selected?.isSystemRole);

  // No-op detection
  const roleInfoUnchanged =
    selected &&
    roleName.trim() === selected.roleName &&
    (description.trim() || null) === (selected.description ?? null) &&
    status === selected.status;

  const permissionsUnchanged =
    selected && arraysEqual(selectedPermissions, selected.permissionCodes);

  return (
    <section className="grid gap-6 lg:grid-cols-[22rem_1fr]">
      {/* Left Column: Role List and Filters */}
      <div className="cm-surface rounded-2xl p-5 shadow-sm space-y-4">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="font-bold text-[var(--cm-text)] text-sm">Danh sách vai trò</h2>
            <p className="text-xs text-[var(--cm-text-muted)]">{rolesMeta?.totalItems ?? roles.length} vai trò trong trung tâm</p>
          </div>
          {canCreate && (
            <button
              type="button"
              id="btn-open-create-role"
              onClick={() => setShowCreate((v) => !v)}
              className="cm-secondary-button rounded-lg px-2.5 py-1 text-xs font-bold"
            >
              {showCreate ? "Hủy tạo" : "+ Tạo vai trò"}
            </button>
          )}
        </div>

        {/* Create Role Form */}
        {showCreate && (
          <form
            className="space-y-3 rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-raised)] p-3.5"
            onSubmit={(event) => {
              event.preventDefault();
              createMutation.mutate({
                roleCode: createCode.trim().toUpperCase(),
                roleName: createName.trim(),
                accountType: createType,
                description: createDescription.trim() || undefined,
              });
            }}
          >
            <h3 className="text-xs font-bold uppercase tracking-wider text-[var(--cm-primary)]">Tạo vai trò tùy chỉnh mới</h3>
            <div>
              <label className="block text-xs font-medium text-[var(--cm-text-secondary)]">Mã vai trò (RoleCode)</label>
              <input
                aria-label="Mã role"
                required
                maxLength={64}
                pattern="^[A-Z][A-Z0-9_]*$"
                title="Bắt đầu bằng chữ in hoa, chỉ gồm chữ in hoa, số và dấu gạch dưới"
                value={createCode}
                onChange={(event) => setCreateCode(event.target.value.toUpperCase())}
                placeholder="TEACHER_MATH_LEAD"
                className="cm-input mt-1 w-full text-xs font-mono uppercase"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-[var(--cm-text-secondary)]">Tên vai trò</label>
              <input
                aria-label="Tên role"
                required
                maxLength={150}
                value={createName}
                onChange={(event) => setCreateName(event.target.value)}
                placeholder="Tổ trưởng bộ môn Toán"
                className="cm-input mt-1 w-full text-xs"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-[var(--cm-text-secondary)]">Loại tài khoản tương thích</label>
              <select
                aria-label="Loại tài khoản"
                value={createType}
                onChange={(event) => setCreateType(event.target.value as AccountType)}
                className="cm-select mt-1 w-full text-xs font-medium"
              >
                <option value="Teacher">Giáo viên (Teacher)</option>
                <option value="CenterManager">Quản lý trung tâm (CenterManager)</option>
                <option value="Student">Học sinh (Student)</option>
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-[var(--cm-text-secondary)]">Mô tả mục đích</label>
              <textarea
                aria-label="Mô tả role"
                maxLength={500}
                value={createDescription}
                onChange={(event) => setCreateDescription(event.target.value)}
                placeholder="Phạm vi công việc và nhiệm vụ được ủy quyền..."
                className="cm-input mt-1 w-full text-xs"
                rows={2}
              />
            </div>
            <button
              type="submit"
              disabled={createMutation.isPending || !createCode.trim() || !createName.trim()}
              className="cm-primary-button w-full py-2 text-xs font-bold shadow-sm disabled:opacity-50"
            >
              {createMutation.isPending ? "Đang xử lý..." : "Xác nhận tạo vai trò"}
            </button>
          </form>
        )}

        {/* Filters */}
        <div className="space-y-2">
          <input
            type="search"
            placeholder="Tìm theo mã hoặc tên vai trò..."
            value={searchRole}
            onChange={(e) => {
              setSearchRole(e.target.value);
              setRolePage(1);
            }}
            className="cm-input w-full text-xs"
          />
          <div className="grid grid-cols-2 gap-2">
            <select
              aria-label="Lọc theo loại tài khoản"
              value={filterAccountType}
              onChange={(e) => {
                setFilterAccountType(e.target.value);
                setRolePage(1);
              }}
              className="cm-select text-xs font-medium"
            >
              <option value="All">Tất cả tài khoản</option>
              <option value="CenterManager">Quản lý trung tâm</option>
              <option value="Teacher">Giáo viên</option>
              <option value="Student">Học sinh</option>
            </select>
            <select
              aria-label="Lọc theo trạng thái vai trò"
              value={filterStatus}
              onChange={(e) => {
                setFilterStatus(e.target.value);
                setRolePage(1);
              }}
              className="cm-select text-xs"
            >
              <option value="All">Tất cả trạng thái</option>
              <option value="Active">Đang hoạt động (Active)</option>
              <option value="Archived">Đã lưu trữ (Archived)</option>
            </select>
          </div>
        </div>

        {/* Role Items */}
        {roleLoading ? (
          <p className="py-6 text-center text-xs text-[var(--cm-text-muted)]">Đang tải danh sách vai trò...</p>
        ) : roles.length === 0 ? (
          <p className="py-6 text-center text-xs text-[var(--cm-text-muted)]">Không tìm thấy vai trò phù hợp bộ lọc.</p>
        ) : (
          <div className="max-h-[34rem] space-y-2 overflow-y-auto pr-1">
            {roles.map((role) => (
              <button
                key={role.roleId}
                type="button"
                id={`role-item-${role.roleCode}`}
                onClick={() => setSelectedId(role.roleId)}
                className={`w-full rounded-xl p-3 text-left transition-all ${
                  selected?.roleId === role.roleId
                    ? "bg-[var(--cm-primary)]/10 ring-2 ring-[var(--cm-primary)] border border-[var(--cm-primary)]/40"
                    : "bg-[var(--cm-surface-raised)] hover:border-[var(--cm-border)] border border-[var(--cm-border-subtle)]"
                }`}
              >
                <div className="flex items-start justify-between gap-1">
                  <span className="font-semibold text-[var(--cm-text)] text-sm">{role.roleName}</span>
                  {role.isSystemRole && (
                    <span className="rounded bg-blue-500/10 px-1.5 py-0.5 text-[10px] font-bold text-blue-400 border border-blue-500/20">
                      Hệ thống
                    </span>
                  )}
                </div>
                <div className="mt-1 flex flex-wrap items-center gap-1.5 text-xs text-[var(--cm-text-muted)]">
                  <span className="font-mono text-[11px] font-medium text-[var(--cm-text-secondary)]">{role.roleCode}</span>
                  <span>•</span>
                  <span>{accountTypeLabels[role.accountType]}</span>
                </div>
                <div className="mt-2 flex items-center justify-between text-[11px] text-[var(--cm-text-muted)]">
                  <span className="flex items-center gap-1">
                    <span className={`inline-block h-1.5 w-1.5 rounded-full ${role.status === "Active" ? "bg-emerald-500" : "bg-slate-400"}`} />
                    {role.status}
                  </span>
                  <span>{role.activeUserCount} người dùng</span>
                </div>
              </button>
            ))}
          </div>
        )}

        {/* Role Pagination Controls */}
        {rolesMeta && rolesMeta.totalPages > 1 && (
          <div className="flex items-center justify-between border-t border-[var(--cm-border-subtle)] pt-3 text-xs text-[var(--cm-text-secondary)]">
            <span>
              Trang {rolesMeta.page} / {rolesMeta.totalPages} ({rolesMeta.totalItems} vai trò)
            </span>
            <div className="flex gap-1">
              <button
                type="button"
                disabled={rolePage <= 1}
                onClick={() => setRolePage((p) => Math.max(1, p - 1))}
                className="cm-secondary-button rounded px-2 py-1 text-xs disabled:opacity-50"
              >
                Trước
              </button>
              <button
                type="button"
                disabled={rolePage >= rolesMeta.totalPages}
                onClick={() => setRolePage((p) => Math.min(rolesMeta.totalPages, p + 1))}
                className="cm-secondary-button rounded px-2 py-1 text-xs disabled:opacity-50"
              >
                Sau
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Right Column: Selected Role Details & Permission Matrix */}
      <div className="cm-surface rounded-2xl p-6 shadow-sm">
        {!selected ? (
          <div className="py-12 text-center text-[var(--cm-text-muted)]">
            <p className="text-sm font-medium">Chưa có vai trò nào được chọn.</p>
          </div>
        ) : (
          <div className="space-y-6">
            {/* System Role Notice Banner */}
            {isSystemRole && (
              <div className="rounded-xl border border-blue-500/30 bg-blue-500/10 p-4 text-blue-300">
                <div className="flex items-center gap-2 font-semibold">
                  <span className="text-base">🛡️</span>
                  <span>Vai trò hệ thống mặc định (Chỉ đọc)</span>
                </div>
                <p className="mt-1 text-xs text-blue-200">
                  Vai trò này là cấu hình hệ thống cố định cho trung tâm nhằm đảm bảo các quyền vận hành tối thiểu. Tên vai trò, trạng thái và ma trận quyền được khóa để ngăn ngừa xung đột chính sách.
                </p>
              </div>
            )}

            {/* Role Header & Metadata */}
            <div className="border-b border-[var(--cm-border-subtle)] pb-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div>
                  <div className="flex items-center gap-2">
                    <h2 className="text-xl font-bold text-[var(--cm-text)]">{selected.roleName}</h2>
                    <span className="rounded-lg bg-[var(--cm-surface-raised)] border border-[var(--cm-border-subtle)] px-2 py-0.5 font-mono text-xs text-[var(--cm-text-secondary)]">
                      {selected.roleCode}
                    </span>
                  </div>
                  <p className="mt-0.5 text-xs text-[var(--cm-text-muted)]">
                    Tương thích: <strong className="text-[var(--cm-text-secondary)]">{accountTypeLabels[selected.accountType]}</strong> · Phiên bản RowVersion: <span className="font-mono">{selected.rowVersion}</span>
                  </p>
                </div>
                <div className="flex items-center gap-2 text-xs">
                  <span className={`rounded-full px-2.5 py-0.5 font-bold ${selected.status === "Active" ? "bg-emerald-500/10 text-emerald-400 border border-emerald-500/20" : "bg-slate-500/10 text-slate-400 border border-slate-500/20"}`}>
                    {selected.status}
                  </span>
                  <span className="text-[var(--cm-text-muted)] font-medium">{selected.activeUserCount} tài khoản được gán</span>
                </div>
              </div>
            </div>

            {/* Basic Info Form */}
            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <label className="block text-xs font-semibold text-[var(--cm-text-secondary)]">Tên hiển thị vai trò</label>
                <input
                  disabled={!canUpdate || isSystemRole}
                  value={roleName}
                  onChange={(e) => setRoleName(e.target.value)}
                  maxLength={150}
                  className="cm-input mt-1 w-full text-sm disabled:opacity-50"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-[var(--cm-text-secondary)]">Trạng thái vận hành</label>
                <select
                  disabled={!canUpdate || isSystemRole}
                  value={status}
                  onChange={(e) => setStatus(e.target.value as "Active" | "Archived")}
                  className={`cm-select mt-1 w-full text-sm font-medium disabled:opacity-50 transition-colors ${
                    status === "Archived"
                      ? "!border-amber-500 !bg-amber-500/15 !text-amber-800 dark:!text-amber-300 font-bold ring-1 ring-amber-400/40"
                      : ""
                  }`}
                >
                  <option value="Active">Đang hoạt động (Active)</option>
                  <option value="Archived" disabled={!canArchive}>
                    Đã lưu trữ (Archived) {!canArchive && "(Cần quyền archive)"}
                  </option>
                </select>
              </div>

              {/* Warning when Archived is selected */}
              {status === "Archived" && (
                <div className="sm:col-span-2 flex items-start gap-2.5 rounded-xl border border-amber-500/40 bg-amber-500/10 p-3 text-xs text-amber-900 dark:text-amber-200">
                  <span className="text-base leading-none" aria-hidden="true">⚠️</span>
                  <div className="space-y-0.5">
                    <p className="font-semibold text-amber-950 dark:text-amber-100">Lưu ý khi lưu trữ vai trò:</p>
                    <p className="text-amber-800 dark:text-amber-300/90 leading-relaxed">
                      Vai trò này sẽ bị vô hiệu hóa, không thể gán cho người dùng mới và thu hồi quyền của các tài khoản đang giữ. Dữ liệu lịch sử & nhật ký kiểm toán trong quá khứ vẫn được bảo toàn 100%.
                    </p>
                  </div>
                </div>
              )}

              <div className="sm:col-span-2">
                <label className="block text-xs font-semibold text-[var(--cm-text-secondary)]">Mô tả trách nhiệm & thẩm quyền</label>
                <textarea
                  disabled={!canUpdate || isSystemRole}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  maxLength={500}
                  rows={2}
                  className="cm-input mt-1 w-full text-sm disabled:opacity-50"
                  placeholder="Mô tả phạm vi vai trò..."
                />
              </div>

              {/* Reason for updating Role Info */}
              {canUpdate && !isSystemRole && (
                <div className="sm:col-span-2">
                  <label className="block text-xs font-semibold text-[var(--cm-text-secondary)]">
                    Lý do chỉnh sửa thông tin vai trò <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    disabled={roleInfoUnchanged}
                    value={roleInfoReason}
                    onChange={(e) => setRoleInfoReason(e.target.value)}
                    maxLength={1000}
                    placeholder={
                      roleInfoUnchanged
                        ? "Thay đổi thông tin vai trò ở trên trước khi nhập lý do..."
                        : "Ví dụ: Điều chỉnh tên chức danh và mô tả nhiệm vụ mới (tối thiểu 3 ký tự)..."
                    }
                    className="cm-input mt-1 w-full text-xs disabled:opacity-50"
                  />
                  {!roleInfoUnchanged && roleInfoReason.trim().length > 0 && roleInfoReason.trim().length < 3 && (
                    <p className="mt-1 text-[11px] text-amber-600 dark:text-amber-400">Lý do phải có ít nhất 3 ký tự.</p>
                  )}
                </div>
              )}
            </div>

            {/* Save Role Info Action */}
            {canUpdate && !isSystemRole && (
              <div className="flex items-center justify-between border-t border-[var(--cm-border-subtle)] pt-3">
                <span className="text-xs text-[var(--cm-text-muted)]">
                  {roleInfoUnchanged
                    ? "Thông tin vai trò chưa thay đổi"
                    : !roleInfoReasonValid
                    ? "Vui lòng nhập lý do chỉnh sửa thông tin vai trò để lưu"
                    : "Sẵn sàng lưu thông tin vai trò"}
                </span>
                <button
                  type="button"
                  id="btn-save-role-info"
                  disabled={roleInfoUnchanged || !roleInfoReasonValid || updateMutation.isPending}
                  onClick={() => updateMutation.mutate()}
                  className="cm-primary-button rounded-lg px-4 py-1.5 text-xs font-bold shadow-sm disabled:opacity-40"
                >
                  {updateMutation.isPending ? "Đang lưu..." : "Lưu thông tin vai trò"}
                </button>
              </div>
            )}

            {/* Permission Matrix Section */}
            <div className="border-t border-[var(--cm-border-subtle)] pt-5">
              <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
                <div>
                  <h3 className="font-bold text-[var(--cm-text)] text-base">Ma trận quyền hạn (Dynamic Permission Matrix)</h3>
                  <p className="text-xs text-[var(--cm-text-muted)]">
                    Đã chọn <strong className="text-[var(--cm-primary)]">{selectedPermissions.length}</strong> quyền cho vai trò này.
                  </p>
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  <input
                    type="search"
                    placeholder="Lọc mã permission..."
                    value={permissionSearch}
                    onChange={(e) => setPermissionSearch(e.target.value)}
                    className="cm-input rounded-lg px-2.5 py-1 text-xs"
                  />
                </div>
              </div>

              {/* Legend of Granular Permissions */}
              <div className="mb-4 flex flex-wrap items-center gap-3 rounded-xl bg-[var(--cm-surface-raised)] border border-[var(--cm-border-subtle)] p-3 text-xs text-[var(--cm-text-secondary)]">
                <span className="font-bold text-[var(--cm-text)]">Chú giải ma trận:</span>
                <span className="flex items-center gap-1">
                  <span className="h-2 w-2 rounded-full bg-emerald-500" /> Bạn đang sở hữu
                </span>
                <span className="flex items-center gap-1">
                  <span className="h-2 w-2 rounded-full bg-amber-500" /> Nhạy cảm
                </span>
                <span className="flex items-center gap-1">
                  <span className="h-2 w-2 rounded-full bg-slate-400" /> Không tương thích
                </span>
                <span className="flex items-center gap-1">
                  <span className="h-2 w-2 rounded-full bg-red-500" /> Không thể ủy quyền / Vượt thẩm quyền
                </span>
              </div>

              {catalogLoading ? (
                <p className="py-8 text-center text-xs text-[var(--cm-text-muted)]">Đang tải danh mục permission...</p>
              ) : !canReadPermissionCatalog ? (
                <p className="rounded-xl bg-amber-500/10 border border-amber-500/30 p-3 text-xs text-amber-400">
                  Tài khoản của bạn chưa có quyền đọc danh mục permission (`authorization.permissions.read`).
                </p>
              ) : groupedCatalog.length === 0 ? (
                <p className="py-6 text-center text-xs text-[var(--cm-text-muted)]">Không tìm thấy permission phù hợp từ khóa.</p>
              ) : (
                <div className="max-h-[30rem] space-y-4 overflow-y-auto rounded-xl border border-[var(--cm-border-subtle)] p-4 bg-[var(--cm-surface-raised)]/30">
                  {groupedCatalog.map(([moduleName, modulePermissions]) => {
                    const toggleableInModule = modulePermissions.filter(
                      (p) => evaluatePermission(p).canToggle,
                    );
                    const allSelected =
                      toggleableInModule.length > 0 &&
                      toggleableInModule.every((p) => selectedPermissions.includes(p.permissionCode));

                    return (
                      <fieldset key={moduleName} className="rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-raised)] p-3.5">
                        <div className="mb-2 flex items-center justify-between border-b border-[var(--cm-border-subtle)] pb-2">
                          <legend className="font-bold uppercase tracking-wider text-xs text-[var(--cm-text)]">
                            {moduleName} ({modulePermissions.length})
                          </legend>
                          {canManagePermissions && !isSystemRole && toggleableInModule.length > 0 && (
                            <div className="flex items-center gap-2">
                              <button
                                type="button"
                                onClick={() => handleToggleModuleAll(modulePermissions, !allSelected)}
                                className="text-[11px] font-semibold text-[var(--cm-primary)] hover:underline"
                              >
                                {allSelected ? "Bỏ chọn tất cả module" : "Chọn tất cả hợp lệ"}
                              </button>
                            </div>
                          )}
                        </div>

                        <div className="grid gap-2 sm:grid-cols-2">
                          {modulePermissions.map((permission) => {
                            const evalResult = evaluatePermission(permission);
                            const checkboxId = `perm-chk-${permission.permissionCode}`;

                            return (
                              <label
                                key={permission.permissionCode}
                                htmlFor={checkboxId}
                                className={`flex items-start gap-2.5 rounded-lg p-2.5 text-xs transition-colors ${
                                  evalResult.isAssigned
                                    ? "bg-[var(--cm-primary)]/10 border border-[var(--cm-primary)]/40"
                                    : "bg-[var(--cm-surface)] border border-[var(--cm-border-subtle)] hover:border-[var(--cm-border)]"
                                } ${!evalResult.canToggle ? "opacity-60 cursor-not-allowed" : "cursor-pointer"}`}
                              >
                                <input
                                  type="checkbox"
                                  id={checkboxId}
                                  disabled={!evalResult.canToggle}
                                  checked={evalResult.isAssigned}
                                  onChange={() => handleTogglePermission(permission.permissionCode)}
                                  className="mt-0.5 rounded border-[var(--cm-border-subtle)] text-[var(--cm-primary)] focus:ring-[var(--cm-primary)] disabled:opacity-50"
                                />
                                <div className="flex-1">
                                  <div className="flex flex-wrap items-center gap-1.5">
                                    <span className="font-mono font-medium text-[var(--cm-text)]">
                                      {permission.permissionCode}
                                    </span>
                                    {evalResult.isOwnerByActor && (
                                      <span className="rounded bg-emerald-500/10 px-1 py-0.2 text-[9px] font-bold text-emerald-400 border border-emerald-500/20">
                                        Bạn có
                                      </span>
                                    )}
                                    {permission.isSensitive && (
                                      <span className="rounded bg-amber-500/10 px-1 py-0.2 text-[9px] font-bold text-amber-400 border border-amber-500/20">
                                        Nhạy cảm
                                      </span>
                                    )}
                                  </div>
                                  <p className="mt-0.5 text-[11px] text-[var(--cm-text-muted)]">{permission.description}</p>

                                  {/* Error/Guard Status Badges */}
                                  {!evalResult.isActive && (
                                    <span className="mt-1 inline-block text-[10px] font-semibold text-[var(--cm-text-muted)]">
                                      ✕ Không hoạt động (Inactive)
                                    </span>
                                  )}
                                  {!evalResult.isCompatible && (
                                    <span className="mt-1 inline-block text-[10px] font-semibold text-[var(--cm-text-muted)]">
                                      ✕ Không tương thích {accountTypeLabels[selected.accountType]}
                                    </span>
                                  )}
                                  {!evalResult.isDelegable && (
                                    <span className="mt-1 inline-block text-[10px] font-semibold text-rose-400">
                                      ✕ Non-delegable (Quyền không được phép ủy quyền)
                                    </span>
                                  )}
                                  {evalResult.isOutOfScope && (
                                    <span className="mt-1 inline-block text-[10px] font-semibold text-amber-400">
                                      ✕ Vượt thẩm quyền (Bạn không sở hữu quyền này)
                                    </span>
                                  )}
                                  {evalResult.isAssigned && !evalResult.canToggle && (
                                    <span className="mt-1 inline-block rounded bg-[var(--cm-surface-raised)] px-1.5 py-0.5 text-[10px] font-bold text-[var(--cm-text-secondary)] border border-[var(--cm-border-subtle)]">
                                      🔒 Đã gán trước đó (Chỉ xem)
                                    </span>
                                  )}
                                </div>
                              </label>
                            );
                          })}
                        </div>
                      </fieldset>
                    );
                  })}
                </div>
              )}
            </div>

            {/* Mutation Execution Footer */}
            {canManagePermissions && !isSystemRole && (
              <div className="rounded-xl bg-[var(--cm-surface-raised)] p-4 border border-[var(--cm-border-subtle)] space-y-3">
                <label className="block text-xs font-bold text-[var(--cm-text-secondary)]">
                  Lý do thay đổi phân quyền <span className="text-red-500">*</span>
                  <input
                    required
                    maxLength={1000}
                    disabled={permissionsUnchanged}
                    value={permissionsReason}
                    onChange={(e) => setPermissionsReason(e.target.value)}
                    placeholder={
                      permissionsUnchanged
                        ? "Thay đổi các checkbox quyền trong ma trận trước khi nhập lý do..."
                        : "Ví dụ: Cập nhật ma trận phân quyền phục vụ kỳ thi học kỳ 1 (tối thiểu 3 ký tự)..."
                    }
                    className="cm-input mt-1 w-full text-xs disabled:opacity-50"
                  />
                </label>
                {!permissionsUnchanged && permissionsReason.trim().length > 0 && permissionsReason.trim().length < 3 && (
                  <p className="text-[11px] text-amber-600 dark:text-amber-400">Lý do phải có ít nhất 3 ký tự.</p>
                )}

                <div className="flex flex-wrap items-center justify-between gap-3">
                  <span className="text-xs text-[var(--cm-text-muted)]">
                    {permissionsUnchanged
                      ? "Ma trận quyền chưa thay đổi"
                      : !permissionsReasonValid
                      ? "Vui lòng nhập lý do thay đổi phân quyền để lưu"
                      : "Ma trận quyền đã thay đổi và sẵn sàng cập nhật"}
                  </span>
                  <button
                    type="button"
                    id="btn-replace-role-permissions"
                    disabled={
                      permissionsUnchanged ||
                      !permissionsReasonValid ||
                      permissionsMutation.isPending ||
                      isSystemRole
                    }
                    onClick={() => permissionsMutation.mutate()}
                    className="cm-primary-button rounded-lg px-4 py-2 text-xs font-bold shadow-sm disabled:opacity-40"
                  >
                    {permissionsMutation.isPending ? "Đang thay thế..." : "Thay thế permission (Canonical)"}
                  </button>
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </section>
  );
};

// ==========================================
// 2. User-Role Assignment Panel
// ==========================================

interface UserRolePanelProps {
  currentUser: {
    userId: string;
    displayName: string;
    username: string;
    accountType: AccountType;
    status?: string;
  };
  actorPermissions: string[];
  catalog: PermissionDto[];
  canRead: boolean;
  canReadRoles: boolean;
  canAssign: boolean;
  onSuccess: (message: string, changedUserId: string) => Promise<void>;
  onError: (error: unknown) => void;
}

const UserRolePanel = ({
  currentUser,
  actorPermissions,
  catalog,
  canRead,
  canReadRoles,
  canAssign,
  onSuccess,
  onError,
}: UserRolePanelProps) => {
  const [selectedUser, setSelectedUser] = useState<AuthorizationUserItem | AuthorizationUserOption>(currentUser);
  const selectedUserId = selectedUser.userId;
  const [userSearch, setUserSearch] = useState("");
  const [userAccountTypeFilter, setUserAccountTypeFilter] = useState<string>("All");
  const [userStatusFilter, setUserStatusFilter] = useState<string>("All");
  const [userPage, setUserPage] = useState(1);
  const [selectedRoleIds, setSelectedRoleIds] = useState<string[]>([]);
  const [reason, setReason] = useState("");
  const [roleSearch, setRoleSearch] = useState("");
  const [rolePage, setRolePage] = useState(1);
  const [knownRoles, setKnownRoles] = useState<Map<string, AuthorizationRoleDto>>(new Map());

  const usersQuery = useQuery({
    queryKey: [
      "authorization",
      "users-list",
      userSearch,
      userAccountTypeFilter,
      userStatusFilter,
      userPage,
    ],
    queryFn: () =>
      authorizationApi.listUsers(
        buildUserQueryParams(
          userPage,
          10,
          userSearch,
          userAccountTypeFilter === "All" ? undefined : (userAccountTypeFilter as AccountType),
          userStatusFilter === "All" ? undefined : userStatusFilter,
        ),
      ),
    enabled: canRead,
  });

  const users = useMemo(() => usersQuery.data?.data ?? [], [usersQuery.data?.data]);
  const usersMeta = usersQuery.data?.meta;
  const userLoading = usersQuery.isLoading;

  // Ensure selection remains synchronized and never points to an outdated or unaligned user
  useEffect(() => {
    if (users.length > 0) {
      const match = users.find((item) => item.userId === selectedUser.userId);
      if (match) {
        setSelectedUser(match);
      } else {
        // When page changes or active user leaves current dataset, automatically align selection to the first user
        setSelectedUser(users[0]);
      }
    }
  }, [users, selectedUser.userId]);

  // Reset role pagination when target user account type changes
  useEffect(() => {
    setRolePage(1);
    setRoleSearch("");
  }, [selectedUser.accountType]);

  // Load active roles compatible with selected user's account type with server-side pagination & search
  const rolesQuery = useQuery({
    queryKey: [
      "authorization",
      "roles-for-user",
      selectedUser.accountType,
      roleSearch,
      rolePage,
    ],
    queryFn: () =>
      authorizationApi.listRoles(
        buildRoleQueryParams(
          rolePage,
          10,
          roleSearch,
          selectedUser.accountType,
          "Active",
        ),
      ),
    enabled: canRead && canReadRoles && Boolean(selectedUser.accountType),
  });

  const roles = useMemo(() => rolesQuery.data?.data ?? [], [rolesQuery.data?.data]);
  const rolesMeta = rolesQuery.data?.meta;

  // Accumulate known roles metadata across pages so selected roles and effective permissions retain attribution
  useEffect(() => {
    if (roles.length > 0) {
      setKnownRoles((prev) => {
        const next = new Map(prev);
        for (const r of roles) {
          next.set(r.roleId, r);
        }
        return next;
      });
    }
  }, [roles]);

  const compatibleRoles = useMemo(() => {
    return filterCompatibleRoles(roles, selectedUser.accountType);
  }, [roles, selectedUser.accountType]);

  const authorizationQuery = useQuery({
    queryKey: ["authorization", "user", selectedUserId],
    queryFn: () => authorizationApi.getUserAuthorization(selectedUserId),
    enabled: canRead && Boolean(selectedUserId),
  });

  const userAuth = authorizationQuery.data?.data;

  const fetchedMissingRolesRef = useRef<Set<string>>(new Set());

  useEffect(() => {
    if (userAuth) {
      const activeIds = userAuth.roles
        .filter((r) => r.assignmentStatus === "Active")
        .map((r) => r.roleId);
      setSelectedRoleIds(activeIds);
      setReason("");

      // Automatically register all assigned roles into knownRoles using shared helper
      setKnownRoles((prev) => hydrateKnownRolesFromUserAuth(prev, userAuth.roles));
    }
  }, [userAuth]);

  // Fetch canonical role details for any selected role ID that is missing from knownRoles or lacks permissions
  useEffect(() => {
    if (!canReadRoles) return;
    const missingRoleIds = getMissingCanonicalRoleIds(
      selectedRoleIds,
      knownRoles,
      fetchedMissingRolesRef.current,
    );
    if (missingRoleIds.length === 0) return;

    for (const missingId of missingRoleIds) {
      fetchedMissingRolesRef.current.add(missingId);
    }

    let isMounted = true;
    for (const missingId of missingRoleIds) {
      authorizationApi
        .getRole(missingId)
        .then((res) => {
          if (isMounted && res?.data) {
            setKnownRoles((prev) => {
              const next = new Map(prev);
              next.set(missingId, res.data);
              return next;
            });
          }
        })
        .catch(() => {
          // Ignore if role cannot be fetched (e.g. mock test or network)
        });
    }

    return () => {
      isMounted = false;
    };
  }, [selectedRoleIds, knownRoles, canReadRoles]);

  const replaceMutation = useMutation({
    mutationFn: () =>
      authorizationApi.replaceUserRoles(selectedUserId, {
        roleIds: selectedRoleIds,
        rowVersion: userAuth!.rowVersion,
        reason: reason.trim(),
      }),
    onSuccess: async () => {
      await authorizationQuery.refetch();
      await onSuccess(
        `Đã cập nhật vai trò cho [${selectedUser.displayName}]. Phiên xác thực của người dùng đã được làm mới.`,
        selectedUserId,
      );
    },
    onError: (err) => {
      // On OCC conflict, refetch
      void authorizationQuery.refetch();
      onError(err);
    },
  });

  // Compute effective permissions with source role attribution using shared helper
  const effectivePermissionsBreakdown = useMemo(() => {
    return computeEffectivePermissionsBreakdown(selectedRoleIds, knownRoles, catalog);
  }, [catalog, knownRoles, selectedRoleIds]);

  const actorPermSet = useMemo(() => new Set(actorPermissions), [actorPermissions]);

  // Check if assigning a role would cause over-grant for CenterManager
  const isRoleOutOfScopeForActor = (role: AuthorizationRoleDto) => {
    if (selectedUser.accountType !== "CenterManager") return false;
    return role.permissionCodes.some((code) => !actorPermSet.has(code));
  };

  const initialRoleIds = useMemo(() => {
    return (
      userAuth?.roles
        .filter((r) => r.assignmentStatus === "Active")
        .map((r) => r.roleId) ?? []
    );
  }, [userAuth]);

  const rolesUnchanged = arraysEqual(selectedRoleIds, initialRoleIds);
  const reasonValid = reason.trim().length >= 3 && reason.trim().length <= 1000;
  const isSelfChange = isSelfUser(currentUser.userId, selectedUserId);

  return (
    <section className="grid gap-6 lg:grid-cols-[22rem_1fr]">
      {/* Left Column: User Selection */}
      <div className="cm-surface rounded-2xl p-5 shadow-sm space-y-4">
        <div>
          <h2 className="font-bold text-[var(--cm-text)] text-sm">Người dùng cùng trung tâm</h2>
          <p className="mt-0.5 text-xs text-[var(--cm-text-muted)]">Chọn người dùng để gán hoặc thu hồi vai trò</p>
        </div>

        <div className="space-y-2">
          <input
            type="search"
            placeholder="Tìm theo tên hoặc username..."
            value={userSearch}
            onChange={(e) => {
              setUserSearch(e.target.value);
              setUserPage(1);
            }}
            className="cm-input w-full text-xs"
          />
          <div className="grid grid-cols-2 gap-2">
            <select
              aria-label="Lọc theo loại tài khoản người dùng"
              value={userAccountTypeFilter}
              onChange={(e) => {
                setUserAccountTypeFilter(e.target.value);
                setUserPage(1);
              }}
              className="cm-select w-full text-xs font-medium"
            >
              <option value="All">Tất cả tài khoản</option>
              <option value="CenterManager">Quản lý trung tâm</option>
              <option value="Teacher">Giáo viên</option>
              <option value="Student">Học sinh</option>
            </select>
            <select
              aria-label="Lọc theo trạng thái người dùng"
              value={userStatusFilter}
              onChange={(e) => {
                setUserStatusFilter(e.target.value);
                setUserPage(1);
              }}
              className="cm-select w-full text-xs"
            >
              <option value="All">Tất cả trạng thái</option>
              <option value="Active">Đang hoạt động</option>
              <option value="Locked">Bị khóa</option>
              <option value="Disabled">Vô hiệu hóa</option>
            </select>
          </div>
        </div>

        <div className="max-h-[34rem] space-y-2 overflow-y-auto pr-1">
          {userLoading ? (
            <p className="py-6 text-center text-xs text-[var(--cm-text-muted)]">Đang tải danh sách người dùng...</p>
          ) : users.length === 0 ? (
            <p className="py-6 text-center text-xs text-[var(--cm-text-muted)]">Không tìm thấy người dùng phù hợp.</p>
          ) : (
            users.map((u) => (
              <button
                key={u.userId}
                type="button"
                id={`user-item-${u.userId}`}
                onClick={() => setSelectedUser(u)}
                className={`w-full rounded-xl p-3 text-left transition-all ${
                  selectedUserId === u.userId
                    ? "bg-[var(--cm-primary)]/10 ring-2 ring-[var(--cm-primary)] border border-[var(--cm-primary)]/40"
                    : "bg-[var(--cm-surface-raised)] hover:border-[var(--cm-border)] border border-[var(--cm-border-subtle)]"
                }`}
              >
                <div className="flex items-center justify-between">
                  <span className="font-semibold text-[var(--cm-text)] text-xs">{u.displayName}</span>
                  {isSelfUser(currentUser.userId, u.userId) && (
                    <span className="rounded bg-indigo-500/10 px-1.5 py-0.2 text-[9px] font-bold text-indigo-400 border border-indigo-500/20">
                      Chính bạn
                    </span>
                  )}
                </div>
                <div className="mt-1 flex items-center justify-between text-[11px] text-[var(--cm-text-muted)]">
                  <span>{u.username}</span>
                  <span className="font-medium text-[var(--cm-text-secondary)]">{accountTypeLabels[u.accountType]}</span>
                </div>
              </button>
            ))
          )}
        </div>

        {/* User Pagination Controls */}
        {usersMeta && usersMeta.totalPages > 1 && (
          <div className="flex items-center justify-between border-t border-[var(--cm-border-subtle)] pt-3 text-xs text-[var(--cm-text-secondary)]">
            <span>
              Trang {usersMeta.page} / {usersMeta.totalPages} ({usersMeta.totalItems} người)
            </span>
            <div className="flex gap-1">
              <button
                type="button"
                disabled={userPage <= 1}
                onClick={() => setUserPage((p) => Math.max(1, p - 1))}
                className="cm-secondary-button rounded px-2.5 py-1 text-xs disabled:opacity-50"
              >
                Trước
              </button>
              <button
                type="button"
                disabled={userPage >= usersMeta.totalPages}
                onClick={() => setUserPage((p) => Math.min(usersMeta.totalPages, p + 1))}
                className="cm-secondary-button rounded px-2.5 py-1 text-xs disabled:opacity-50"
              >
                Sau
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Right Column: Roles Assignment and Effective Permissions */}
      <div className="cm-surface rounded-2xl p-6 shadow-sm">
        {/* User Identity Header */}
        <div className="border-b border-[var(--cm-border-subtle)] pb-4">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <div className="flex items-center gap-2">
                <h2 className="text-xl font-bold text-[var(--cm-text)]">{selectedUser.displayName}</h2>
                <span className="rounded-full bg-[var(--cm-surface-raised)] border border-[var(--cm-border-subtle)] px-2.5 py-0.5 text-xs font-semibold text-[var(--cm-text-secondary)]">
                  {accountTypeLabels[selectedUser.accountType]}
                </span>
              </div>
              <p className="mt-0.5 text-xs text-[var(--cm-text-muted)]">
                Tài khoản: <strong className="text-[var(--cm-text-secondary)]">{selectedUser.username}</strong> · AuthVersion: <span className="font-mono">{userAuth?.authorizationVersion ?? "..."}</span> · RowVersion: <span className="font-mono">{userAuth?.rowVersion ?? "..."}</span>
              </p>
            </div>
            {isSelfChange && (
              <span className="rounded-lg bg-amber-500/10 px-2.5 py-1 text-xs font-bold text-amber-400 border border-amber-500/20">
                Tài khoản đang đăng nhập
              </span>
            )}
          </div>
        </div>

        {/* Self-Change Warning Banner */}
        {isSelfChange && (
          <div className="mt-4 rounded-xl border border-amber-500/30 bg-amber-500/10 p-4 text-amber-300">
            <div className="flex items-center gap-2 font-semibold">
              <span className="text-base">⚠️</span>
              <span>Cảnh báo tự thay đổi vai trò (Self-change Warning)</span>
            </div>
            <p className="mt-1 text-xs text-amber-200">
              Bạn đang chỉnh sửa vai trò của chính tài khoản mình đang đăng nhập. Việc gỡ bỏ quyền hoặc thay đổi vai trò có thể làm thay đổi quyền hạn hiệu lực và tự động làm mới phiên đăng nhập của bạn.
            </p>
          </div>
        )}

        {authorizationQuery.isLoading ? (
          <p className="py-12 text-center text-xs text-[var(--cm-text-muted)]">Đang tải thông tin quyền người dùng...</p>
        ) : authorizationQuery.isError ? (
          <p className="py-8 text-center text-xs text-rose-400">Không thể tải thông tin quyền của người dùng được chọn.</p>
        ) : (
          <div className="mt-6 space-y-6">
            {/* Roles Selection Section */}
            <div>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div>
                  <h3 className="font-bold text-[var(--cm-text)] text-sm">
                    {canReadRoles
                      ? `Vai trò tương thích đang hoạt động (${rolesMeta?.totalItems ?? compatibleRoles.length})`
                      : `Các vai trò đang được gán (${userAuth?.roles.length ?? 0})`}
                  </h3>
                  <p className="text-xs text-[var(--cm-text-muted)]">
                    {canReadRoles
                      ? `Chọn các vai trò áp dụng cho ${accountTypeLabels[selectedUser.accountType]}.`
                      : `Danh sách vai trò hiện đang được phân công cho người dùng này.`}
                  </p>
                </div>
              </div>

              {!canReadRoles ? (
                <div className="mt-3 space-y-3">
                  <div className="rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-raised)] p-3.5 text-xs text-[var(--cm-text-secondary)]">
                    ℹ️ <strong>Chế độ chỉ đọc vai trò người dùng (Read-only):</strong> Bạn có quyền xem phân công vai trò (<code>authorization.user_roles.read</code>), không có quyền đọc danh mục vai trò hệ thống (<code>authorization.roles.read</code>). Dưới đây là danh sách các vai trò đang được gán cho người dùng này.
                  </div>
                  <div className="space-y-2">
                    {userAuth?.roles && userAuth.roles.length > 0 ? (
                      userAuth.roles.map((role) => (
                        <div
                          key={role.roleId}
                          className="flex items-start gap-3 rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-raised)] p-3.5 text-xs shadow-sm"
                        >
                          <span
                            className={`mt-0.5 rounded px-1.5 py-0.5 text-[10px] font-bold ${
                              role.assignmentStatus === "Active"
                                ? "bg-emerald-500/10 text-emerald-400 border border-emerald-500/20"
                                : "bg-slate-500/10 text-slate-400 border border-slate-500/20"
                            }`}
                          >
                            {role.assignmentStatus === "Active" ? "Đang hoạt động" : "Đã thu hồi"}
                          </span>
                          <div className="flex-1">
                            <div className="flex flex-wrap items-center gap-2">
                              <span className="font-semibold text-[var(--cm-text)] text-sm">{role.roleName}</span>
                              <span className="font-mono text-[var(--cm-text-muted)] text-[11px]">{role.roleCode}</span>
                            </div>
                            <p className="mt-1 text-[11px] text-[var(--cm-text-muted)]">
                              Gán lúc: {new Date(role.assignedAt).toLocaleString("vi-VN")}
                              {role.revokedAt && ` · Thu hồi: ${new Date(role.revokedAt).toLocaleString("vi-VN")}`}
                            </p>
                            {role.permissionCodes && role.permissionCodes.length > 0 && (
                              <p className="mt-0.5 text-[11px] font-medium text-[var(--cm-primary)]">
                                {role.permissionCodes.length} quyền vận hành
                              </p>
                            )}
                          </div>
                        </div>
                      ))
                    ) : (
                      <p className="rounded-xl bg-[var(--cm-surface-raised)] border border-[var(--cm-border-subtle)] p-4 text-center text-xs text-[var(--cm-text-muted)]">
                        Người dùng này chưa được gán vai trò nào.
                      </p>
                    )}
                  </div>
                </div>
              ) : (
                <>
                  {!canAssign && (
                    <div className="mt-3 rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-raised)] p-3 text-xs text-[var(--cm-text-secondary)]">
                      ℹ️ <strong>Chế độ chỉ đọc (Read-only):</strong> Bạn chỉ có quyền xem vai trò người dùng (<code>authorization.user_roles.read</code>), không có quyền gán hay thay đổi vai trò (<code>authorization.user_roles.assign</code>).
                    </div>
                  )}

                  {/* Role Search Filter */}
                  <div className="mt-3 mb-2">
                    <input
                      type="text"
                      placeholder="Tìm vai trò theo tên hoặc mã..."
                      aria-label="Tìm kiếm vai trò tương thích"
                      value={roleSearch}
                      onChange={(e) => {
                        setRoleSearch(e.target.value);
                        setRolePage(1);
                      }}
                      className="cm-input w-full text-xs"
                    />
                  </div>

                  <div className="space-y-2">
                    {compatibleRoles.map((role) => {
                      const outOfScope = isRoleOutOfScopeForActor(role);
                      const isChecked = selectedRoleIds.includes(role.roleId);
                      const checkboxId = `role-chk-${role.roleId}`;

                      return (
                        <label
                          key={role.roleId}
                          htmlFor={checkboxId}
                          className={`flex items-start gap-3 rounded-xl border p-3.5 text-xs transition-colors ${
                            isChecked
                              ? "border-[var(--cm-primary)]/50 bg-[var(--cm-primary)]/10"
                              : "border-[var(--cm-border-subtle)] bg-[var(--cm-surface-raised)] hover:border-[var(--cm-border)]"
                          } ${!canAssign || outOfScope ? "opacity-60 cursor-not-allowed" : "cursor-pointer"}`}
                        >
                          <input
                            type="checkbox"
                            id={checkboxId}
                            disabled={!canAssign || outOfScope}
                            checked={isChecked}
                            onChange={() =>
                              setSelectedRoleIds((current) =>
                                current.includes(role.roleId)
                                  ? current.filter((id) => id !== role.roleId)
                                  : [...current, role.roleId],
                              )
                            }
                            className="mt-0.5 rounded border-[var(--cm-border-subtle)] text-[var(--cm-primary)] focus:ring-[var(--cm-primary)] disabled:opacity-50"
                          />
                          <div className="flex-1">
                            <div className="flex flex-wrap items-center gap-2">
                              <span className="font-semibold text-[var(--cm-text)] text-sm">{role.roleName}</span>
                              <span className="font-mono text-[var(--cm-text-secondary)] text-[11px]">{role.roleCode}</span>
                              {role.isSystemRole && (
                                <span className="rounded bg-blue-500/10 px-1.5 py-0.5 text-[10px] font-bold text-blue-400 border border-blue-500/20">
                                  Hệ thống
                                </span>
                              )}
                            </div>
                            <p className="mt-1 text-[var(--cm-text-secondary)]">{role.description || "Không có mô tả chi tiết."}</p>
                            <p className="mt-0.5 text-[11px] text-[var(--cm-text-muted)]">
                              {role.permissionCodes.length} permissions · {role.activeUserCount} người đang dùng
                            </p>
                            {outOfScope && (
                              <span className="mt-1 inline-block font-semibold text-rose-400 text-[10px]">
                                ✕ Chứa quyền vượt thẩm quyền của bạn (Không thể gán vai trò này cho Quản lý khác)
                              </span>
                            )}
                          </div>
                        </label>
                      );
                    })}
                    {compatibleRoles.length === 0 && (
                      <p className="rounded-xl bg-[var(--cm-surface-raised)] border border-[var(--cm-border-subtle)] p-4 text-center text-xs text-[var(--cm-text-muted)]">
                        Không có vai trò Active nào tương thích với loại tài khoản {accountTypeLabels[selectedUser.accountType]}.
                      </p>
                    )}
                  </div>

                  {/* Role Pagination Controls */}
                  {rolesMeta && rolesMeta.totalPages > 1 && (
                    <div className="mt-3 flex items-center justify-between border-t border-[var(--cm-border-subtle)] pt-2 text-xs text-[var(--cm-text-secondary)]">
                      <span>
                        Trang {rolesMeta.page} / {rolesMeta.totalPages} ({rolesMeta.totalItems} vai trò)
                      </span>
                      <div className="flex gap-1">
                        <button
                          type="button"
                          disabled={rolePage <= 1}
                          onClick={() => setRolePage((p) => Math.max(1, p - 1))}
                          className="cm-secondary-button rounded px-2 py-1 text-xs disabled:opacity-50"
                        >
                          Trước
                        </button>
                        <button
                          type="button"
                          disabled={rolePage >= rolesMeta.totalPages}
                          onClick={() => setRolePage((p) => Math.min(rolesMeta.totalPages, p + 1))}
                          className="cm-secondary-button rounded px-2 py-1 text-xs disabled:opacity-50"
                        >
                          Sau
                        </button>
                      </div>
                    </div>
                  )}
                </>
              )}
            </div>

            {/* Effective Permissions Breakdown (Spec Requirement 7 & 564) */}
            <div className="border-t border-[var(--cm-border-subtle)] pt-5">
              <div className="mb-2">
                <h3 className="font-bold text-[var(--cm-text)] text-sm">
                  Quyền hạn hiệu lực (Effective Permissions)
                </h3>
                <p className="text-xs text-[var(--cm-text-muted)]">
                  Tổng hợp các capability người dùng sẽ sở hữu từ các vai trò được chọn kèm role nguồn cấp quyền.
                </p>
              </div>

              {effectivePermissionsBreakdown.length === 0 ? (
                <p className="rounded-xl bg-[var(--cm-surface-raised)] border border-[var(--cm-border-subtle)] p-3 text-center text-xs text-[var(--cm-text-muted)]">
                  Chưa có quyền hạn hiệu lực nào được gán cho người dùng này.
                </p>
              ) : (
                <div className="max-h-56 space-y-1.5 overflow-y-auto rounded-xl border border-[var(--cm-border-subtle)] p-3 bg-[var(--cm-surface-raised)]/30">
                  {effectivePermissionsBreakdown.map((item) => (
                    <div
                      key={item.code}
                      className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-[var(--cm-surface-raised)] border border-[var(--cm-border-subtle)] px-3 py-2 text-xs"
                    >
                      <div>
                        <span className="font-mono font-medium text-[var(--cm-text)]">{item.code}</span>
                        <span className="ml-2 text-[11px] text-[var(--cm-text-muted)]">{item.description}</span>
                      </div>
                      <div className="flex flex-wrap items-center gap-1">
                        {item.sourceRoles.map((roleName) => (
                          <span
                            key={roleName}
                            className="rounded bg-indigo-500/10 px-1.5 py-0.5 text-[10px] font-semibold text-indigo-400 border border-indigo-500/20"
                          >
                            ← {roleName}
                          </span>
                        ))}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* Mutation Execution Footer */}
            {canAssign && canReadRoles && (
              <div className="rounded-xl bg-[var(--cm-surface-raised)] p-4 border border-[var(--cm-border-subtle)] space-y-3">
                <label className="block text-xs font-bold text-[var(--cm-text-secondary)]">
                  Lý do thay đổi vai trò người dùng <span className="text-red-500">*</span>
                  <input
                    required
                    maxLength={1000}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    placeholder="Ví dụ: Bổ sung nhiệm vụ quản lý học thuật tuần 2 (tối thiểu 3 ký tự)..."
                    className="cm-input mt-1 w-full text-xs"
                  />
                </label>

                <div className="flex flex-wrap items-center justify-between gap-3">
                  <span className="text-xs text-[var(--cm-text-muted)]">
                    {rolesUnchanged
                      ? "Danh sách vai trò chưa thay đổi"
                      : "Danh sách vai trò đã thay đổi và cần lưu"}
                  </span>
                  <button
                    type="button"
                    id="btn-save-user-roles"
                    disabled={
                      rolesUnchanged ||
                      !reasonValid ||
                      replaceMutation.isPending ||
                      !authorizationQuery.data
                    }
                    onClick={() => replaceMutation.mutate()}
                    className="cm-primary-button rounded-lg px-4 py-2 text-xs font-bold shadow-sm disabled:opacity-40"
                  >
                    {replaceMutation.isPending ? "Đang lưu..." : "Lưu vai trò người dùng"}
                  </button>
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </section>
  );
};

// ==========================================
// 3. Authorization Audit Panel & Detail Modal
// ==========================================

interface AuditPanelProps {
  canReadAudit: boolean;
  onError: (error: unknown) => void;
}

const actionTypeLabels: Record<string, { label: string; color: string }> = {
  RoleCreated: { label: "Tạo vai trò", color: "bg-emerald-100 text-emerald-800" },
  RoleUpdated: { label: "Sửa vai trò", color: "bg-blue-100 text-blue-800" },
  RolePermissionsReplaced: { label: "Thay thế ma trận quyền", color: "bg-indigo-100 text-indigo-800" },
  UserRolesReplaced: { label: "Gán vai trò người dùng", color: "bg-purple-100 text-purple-800" },
  TEACHER_PASSWORD_RESET: { label: "Đặt lại mật khẩu giáo viên", color: "bg-amber-100 text-amber-800" },
  STUDENT_PASSWORD_RESET: { label: "Đặt lại mật khẩu học sinh", color: "bg-amber-100 text-amber-800" },
  STUDENT_SOFT_DELETED: { label: "Xóa mềm học sinh", color: "bg-red-100 text-red-800" },
};

const AuditPanel = ({ canReadAudit }: AuditPanelProps) => {
  const [page, setPage] = useState(1);
  const [pageSize] = useState(15);
  const [filterActionType, setFilterActionType] = useState<string>("");
  const [filterPermissionCode, setFilterPermissionCode] = useState<string>("");
  const [filterActorUserId, setFilterActorUserId] = useState<string>("");
  const [filterTargetUserId, setFilterTargetUserId] = useState<string>("");
  const [filterTargetId, setFilterTargetId] = useState<string>("");
  const [filterFrom, setFilterFrom] = useState<string>("");
  const [filterTo, setFilterTo] = useState<string>("");
  const [selectedAudit, setSelectedAudit] = useState<AuthorizationAuditDto | null>(null);
  const [copiedTraceId, setCopiedTraceId] = useState<string | null>(null);

  const queryParams = useMemo(() => {
    return buildAuditQueryParams(page, pageSize, {
      actionType: filterActionType,
      permissionCode: filterPermissionCode,
      actorUserId: filterActorUserId,
      targetUserId: filterTargetUserId,
      targetId: filterTargetId,
      from: filterFrom,
      to: filterTo,
    });
  }, [
    filterActionType,
    filterPermissionCode,
    filterActorUserId,
    filterTargetUserId,
    filterTargetId,
    filterFrom,
    filterTo,
    page,
    pageSize,
  ]);

  const auditQuery = useQuery({
    queryKey: ["authorization", "audit", queryParams],
    queryFn: () => authorizationApi.listAudit(queryParams),
    enabled: canReadAudit,
  });

  const copyTrace = (traceId: string) => {
    if (navigator.clipboard) {
      void navigator.clipboard.writeText(traceId);
      setCopiedTraceId(traceId);
      setTimeout(() => setCopiedTraceId(null), 2000);
    }
  };

  const resetFilters = () => {
    setFilterActionType("");
    setFilterPermissionCode("");
    setFilterActorUserId("");
    setFilterTargetUserId("");
    setFilterTargetId("");
    setFilterFrom("");
    setFilterTo("");
    setPage(1);
  };

  const entries = auditQuery.data?.data ?? [];
  const meta = auditQuery.data?.meta;

  return (
    <section className="overflow-hidden rounded-2xl cm-surface shadow-sm border border-[var(--cm-border-subtle)]">
      {/* Header & Filter Controls */}
      <div className="border-b border-[var(--cm-border-subtle)] p-5">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h2 className="text-xl font-bold text-[var(--cm-text)]">Nhật ký kiểm toán phân quyền</h2>
            <p className="text-xs text-[var(--cm-text-muted)]">
              Ghi nhận các đột biến phân quyền, thay đổi vai trò, mật khẩu và xóa mềm trong trung tâm hiện tại (Append-only).
            </p>
          </div>
          <span className="rounded-lg bg-emerald-500/10 px-2.5 py-1 text-xs font-semibold text-emerald-400 border border-emerald-500/20">
            Dữ liệu đã khử khuẩn (0 secrets)
          </span>
        </div>

        {/* Filter Toolbar */}
        <div className="mt-4 grid gap-3 sm:grid-cols-2 md:grid-cols-4 lg:grid-cols-4">
          <div>
            <label className="block text-[11px] font-semibold text-[var(--cm-text-secondary)]">Loại hành động</label>
            <select
              aria-label="Lọc theo loại hành động kiểm toán"
              value={filterActionType}
              onChange={(e) => {
                setFilterActionType(e.target.value);
                setPage(1);
              }}
              className="cm-select mt-1 w-full text-xs font-medium"
            >
              <option value="">Tất cả hành động</option>
              <option value="RoleCreated">Tạo vai trò</option>
              <option value="RoleUpdated">Sửa vai trò</option>
              <option value="RolePermissionsReplaced">Thay thế ma trận quyền</option>
              <option value="UserRolesReplaced">Gán vai trò người dùng</option>
              <option value="TEACHER_PASSWORD_RESET">Đặt lại mật khẩu giáo viên</option>
              <option value="STUDENT_PASSWORD_RESET">Đặt lại mật khẩu học sinh</option>
              <option value="STUDENT_SOFT_DELETED">Xóa mềm học sinh</option>
            </select>
          </div>

          <div>
            <label className="block text-[11px] font-semibold text-[var(--cm-text-secondary)]">Mã quyền hạn</label>
            <input
              type="text"
              placeholder="VD: PERM_ROLES_CREATE"
              aria-label="Lọc theo mã quyền hạn"
              value={filterPermissionCode}
              onChange={(e) => {
                setFilterPermissionCode(e.target.value);
                setPage(1);
              }}
              className="cm-input mt-1 w-full text-xs font-mono"
            />
          </div>

          <div>
            <label className="block text-[11px] font-semibold text-[var(--cm-text-secondary)]">Người thực hiện (Actor ID)</label>
            <input
              type="text"
              placeholder="Actor User ID"
              aria-label="Lọc theo người thực hiện"
              value={filterActorUserId}
              onChange={(e) => {
                setFilterActorUserId(e.target.value);
                setPage(1);
              }}
              className="cm-input mt-1 w-full text-xs font-mono"
            />
          </div>

          <div>
            <label className="block text-[11px] font-semibold text-[var(--cm-text-secondary)]">Người dùng đích (Target User)</label>
            <input
              type="text"
              placeholder="Target User ID"
              aria-label="Lọc theo người dùng đích"
              value={filterTargetUserId}
              onChange={(e) => {
                setFilterTargetUserId(e.target.value);
                setPage(1);
              }}
              className="cm-input mt-1 w-full text-xs font-mono"
            />
          </div>

          <div>
            <label className="block text-[11px] font-semibold text-[var(--cm-text-secondary)]">Đối tượng đích (Target ID)</label>
            <input
              type="text"
              placeholder="Role ID hoặc User ID"
              aria-label="Lọc theo đối tượng đích"
              value={filterTargetId}
              onChange={(e) => {
                setFilterTargetId(e.target.value);
                setPage(1);
              }}
              className="cm-input mt-1 w-full text-xs font-mono"
            />
          </div>

          <div>
            <label className="block text-[11px] font-semibold text-[var(--cm-text-secondary)]">Từ thời điểm</label>
            <input
              type="datetime-local"
              aria-label="Lọc từ thời điểm"
              value={filterFrom}
              onChange={(e) => {
                setFilterFrom(e.target.value);
                setPage(1);
              }}
              className="cm-input mt-1 w-full text-xs"
            />
          </div>

          <div>
            <label className="block text-[11px] font-semibold text-[var(--cm-text-secondary)]">Đến thời điểm</label>
            <input
              type="datetime-local"
              aria-label="Lọc đến thời điểm"
              value={filterTo}
              onChange={(e) => {
                setFilterTo(e.target.value);
                setPage(1);
              }}
              className="cm-input mt-1 w-full text-xs"
            />
          </div>

          <div className="flex items-end">
            <button
              type="button"
              onClick={resetFilters}
              className="cm-secondary-button w-full rounded-lg px-3 py-2 text-xs font-semibold"
            >
              Đặt lại bộ lọc
            </button>
          </div>
        </div>
      </div>

      {/* Table Content */}
      {auditQuery.isLoading ? (
        <p className="py-12 text-center text-xs text-[var(--cm-text-muted)]">Đang tải nhật ký kiểm toán...</p>
      ) : auditQuery.isError ? (
        <p className="py-10 text-center text-xs text-rose-400">Không thể tải dữ liệu nhật ký kiểm toán.</p>
      ) : entries.length === 0 ? (
        <p className="py-12 text-center text-xs text-[var(--cm-text-muted)]">Chưa có bản ghi kiểm toán phù hợp bộ lọc.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-[var(--cm-border-subtle)] text-xs">
            <thead className="bg-[var(--cm-surface-raised)] text-[var(--cm-text-secondary)] border-b border-[var(--cm-border-subtle)]">
              <tr>
                <th className="px-4 py-3 text-left font-semibold">Thời gian</th>
                <th className="px-4 py-3 text-left font-semibold">Hành động</th>
                <th className="px-4 py-3 text-left font-semibold">Đối tượng</th>
                <th className="px-4 py-3 text-left font-semibold">Lý do</th>
                <th className="px-4 py-3 text-left font-semibold">Trace ID</th>
                <th className="px-4 py-3 text-right font-semibold">Chi tiết</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[var(--cm-border-subtle)]">
              {entries.map((entry) => {
                const actionMeta = actionTypeLabels[entry.actionType] ?? {
                  label: entry.actionType,
                  color: "bg-[var(--cm-surface-raised)] text-[var(--cm-text-secondary)] border border-[var(--cm-border-subtle)]",
                };

                return (
                  <tr key={entry.authorizationAuditId} className="hover:bg-[var(--cm-surface-raised)]/50 transition-colors">
                    <td className="whitespace-nowrap px-4 py-3 text-[var(--cm-text-muted)] font-mono text-[11px]">
                      {new Date(entry.createdAt).toLocaleString("vi-VN")}
                    </td>
                    <td className="px-4 py-3">
                      <span className={`rounded-full px-2 py-0.5 text-[10px] font-bold ${actionMeta.color}`}>
                        {actionMeta.label}
                      </span>
                    </td>
                    <td className="px-4 py-3 font-medium text-[var(--cm-text)]">
                      <span>{entry.targetType}</span>
                      <span className="block font-mono text-[11px] text-[var(--cm-text-muted)] truncate max-w-xs" title={entry.targetId}>
                        {entry.targetId}
                      </span>
                    </td>
                    <td className="max-w-xs truncate px-4 py-3 text-[var(--cm-text-secondary)]" title={entry.reason}>
                      {entry.reason}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1.5 font-mono text-[11px] text-[var(--cm-text-muted)]">
                        <span className="truncate max-w-[8rem]" title={entry.traceId}>
                          {entry.traceId}
                        </span>
                        <button
                          type="button"
                          onClick={() => copyTrace(entry.traceId)}
                          title="Sao chép Trace ID"
                          className="rounded p-1 text-[var(--cm-text-muted)] hover:text-[var(--cm-text)] hover:bg-[var(--cm-surface-raised)] transition-colors"
                        >
                          {copiedTraceId === entry.traceId ? "✓" : "📋"}
                        </button>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-right">
                      <button
                        type="button"
                        id={`btn-view-audit-${entry.authorizationAuditId}`}
                        onClick={() => setSelectedAudit(entry)}
                        className="cm-secondary-button rounded-lg px-2.5 py-1 text-[11px] font-bold"
                      >
                        Xem chi tiết
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {/* Pagination Footer */}
      {meta && meta.totalPages > 1 && (
        <div className="flex items-center justify-between border-t border-[var(--cm-border-subtle)] px-4 py-3 text-xs text-[var(--cm-text-secondary)]">
          <span>
            Trang {meta.page} / {meta.totalPages} ({meta.totalItems} sự kiện)
          </span>
          <div className="flex items-center gap-2">
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              className="cm-secondary-button rounded-lg px-3 py-1 font-semibold disabled:opacity-40"
            >
              ← Trang trước
            </button>
            <button
              type="button"
              disabled={page >= meta.totalPages}
              onClick={() => setPage((p) => Math.min(meta.totalPages, p + 1))}
              className="cm-secondary-button rounded-lg px-3 py-1 font-semibold disabled:opacity-40"
            >
              Trang sau →
            </button>
          </div>
        </div>
      )}

      {/* Audit Detail Modal */}
      {selectedAudit && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4"
          onClick={() => setSelectedAudit(null)}
        >
          <div
            className="w-full max-w-3xl rounded-2xl cm-surface border border-[var(--cm-border-subtle)] p-6 shadow-2xl max-h-[90vh] overflow-y-auto"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between border-b border-[var(--cm-border-subtle)] pb-3">
              <div>
                <span className="rounded-lg bg-indigo-500/10 px-2 py-0.5 text-xs font-bold text-indigo-400 border border-indigo-500/20">
                  {actionTypeLabels[selectedAudit.actionType]?.label ?? selectedAudit.actionType}
                </span>
                <h3 className="mt-1 text-lg font-bold text-[var(--cm-text)]">
                  Chi tiết bản ghi kiểm toán #{selectedAudit.authorizationAuditId}
                </h3>
                <p className="text-xs text-[var(--cm-text-muted)] font-mono">
                  {new Date(selectedAudit.createdAt).toLocaleString("vi-VN")} UTC
                </p>
              </div>
              <button
                type="button"
                onClick={() => setSelectedAudit(null)}
                className="text-[var(--cm-text-muted)] hover:text-[var(--cm-text)] text-lg font-bold transition-colors"
              >
                ✕
              </button>
            </div>

            <div className="mt-4 grid gap-3 sm:grid-cols-2 text-xs">
              <div className="rounded-xl bg-[var(--cm-surface-raised)] border border-[var(--cm-border-subtle)] p-3">
                <span className="block font-semibold text-[var(--cm-text-muted)]">Đối tượng mục tiêu:</span>
                <span className="font-mono font-medium text-[var(--cm-text)]">{selectedAudit.targetType}: {selectedAudit.targetId}</span>
              </div>
              <div className="rounded-xl bg-[var(--cm-surface-raised)] border border-[var(--cm-border-subtle)] p-3">
                <span className="block font-semibold text-[var(--cm-text-muted)]">Người thực hiện (Actor User ID):</span>
                <span className="font-mono text-[var(--cm-text)]">{selectedAudit.actorUserId ?? "Hệ thống"}</span>
              </div>
              <div className="rounded-xl bg-[var(--cm-surface-raised)] border border-[var(--cm-border-subtle)] p-3 sm:col-span-2">
                <span className="block font-semibold text-[var(--cm-text-muted)]">Lý do thao tác:</span>
                <span className="text-[var(--cm-text)]">{selectedAudit.reason}</span>
              </div>
              <div className="rounded-xl bg-[var(--cm-surface-raised)] border border-[var(--cm-border-subtle)] p-3 sm:col-span-2 flex items-center justify-between">
                <div>
                  <span className="block font-semibold text-[var(--cm-text-muted)]">W3C Trace ID:</span>
                  <span className="font-mono text-[11px] text-[var(--cm-text)]">{selectedAudit.traceId}</span>
                </div>
                <button
                  type="button"
                  onClick={() => copyTrace(selectedAudit.traceId)}
                  className="cm-secondary-button rounded px-2 py-1 font-semibold text-xs"
                >
                  {copiedTraceId === selectedAudit.traceId ? "✓ Đã chép" : "Sao chép"}
                </button>
              </div>
            </div>

            {/* Before / After Data Inspection */}
            <div className="mt-4 space-y-3">
              <div className="flex items-center justify-between">
                <h4 className="font-bold text-xs uppercase tracking-wider text-[var(--cm-text-secondary)]">
                  Đối chiếu dữ liệu (Before / After Data)
                </h4>
                <span className="text-[11px] text-emerald-400 font-medium">
                  ✓ Dữ liệu khử khuẩn nghiêm ngặt
                </span>
              </div>

              <div className="grid gap-3 sm:grid-cols-2">
                <div>
                  <span className="block text-xs font-semibold text-[var(--cm-text-muted)] mb-1">Trước thay đổi (Before)</span>
                  <pre className="max-h-60 overflow-auto rounded-xl bg-slate-950 p-3.5 font-mono text-[11px] text-emerald-400 border border-[var(--cm-border-subtle)]">
                    {selectedAudit.before
                      ? JSON.stringify(selectedAudit.before, null, 2)
                      : "(Không có dữ liệu trước)"}
                  </pre>
                </div>
                <div>
                  <span className="block text-xs font-semibold text-[var(--cm-text-muted)] mb-1">Sau thay đổi (After)</span>
                  <pre className="max-h-60 overflow-auto rounded-xl bg-slate-950 p-3.5 font-mono text-[11px] text-emerald-400 border border-[var(--cm-border-subtle)]">
                    {selectedAudit.after
                      ? JSON.stringify(selectedAudit.after, null, 2)
                      : "(Không có dữ liệu sau)"}
                  </pre>
                </div>
              </div>
            </div>

            <div className="mt-6 flex justify-end border-t border-[var(--cm-border-subtle)] pt-3">
              <button
                type="button"
                onClick={() => setSelectedAudit(null)}
                className="cm-secondary-button rounded-lg px-4 py-2 text-xs font-bold"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  );
};
