import { useEffect, useMemo, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { authorizationApi } from "../api/authorizationApi";
import { getCurrentUser } from "../auth/authApi";
import { permissions } from "../auth/permissions";
import { useAuthStore } from "../stores/authStore";
import type { AccountType } from "../types/auth";
import type {
  AuthorizationAuditDto,
  AuthorizationRoleDto,
  AuthorizationRoleStatus,
  PermissionDto,
} from "../types/authorization";
import {
  parseSafeError,
  buildRoleQueryParams,
  buildUserQueryParams,
  buildAuditQueryParams,
  filterCompatibleRoles,
  isSelfUser,
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
    <main className="min-h-screen bg-slate-50 py-8">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <header className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <div className="flex items-center gap-2">
              <span className="rounded-full bg-indigo-100 px-3 py-0.5 text-xs font-bold text-indigo-700">
                Tenant RBAC Matrix
              </span>
              <p className="text-sm font-semibold text-indigo-600">Phân quyền động theo trung tâm</p>
            </div>
            <h1 className="mt-1 text-3xl font-extrabold text-slate-900">Vai trò và ma trận quyền truy cập</h1>
            <p className="mt-1 text-sm text-slate-600">
              Quản trị vai trò tùy chỉnh, ma trận quyền và phân công tài khoản theo mô hình Least Privilege. Mọi thay đổi đều được ghi nhật ký kiểm toán và tăng phiên bản xác thực.
            </p>
          </div>
          <Link
            className="inline-flex items-center rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-slate-300 hover:bg-slate-50 transition-colors"
            to="/"
          >
            ← Về trang chính
          </Link>
        </header>

        {notice && (
          <div role="status" className="mb-6 flex items-start gap-3 rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800 shadow-sm">
            <span className="text-lg">✓</span>
            <div className="flex-1 font-medium">{notice}</div>
            <button type="button" onClick={() => setNotice("")} className="text-emerald-600 hover:text-emerald-900 text-xs font-bold">Đóng</button>
          </div>
        )}

        {failure && (
          <div role="alert" className="mb-6 flex items-start gap-3 rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-800 shadow-sm">
            <span className="text-lg">⚠</span>
            <div className="flex-1">
              <p className="font-semibold">{failure.message}</p>
              {failure.traceId && (
                <div className="mt-2 flex items-center gap-2 text-xs font-mono text-red-700">
                  <span>Trace ID: {failure.traceId}</span>
                  <button
                    type="button"
                    onClick={() => copyToClipboard(failure.traceId!)}
                    className="rounded bg-red-100 px-1.5 py-0.5 font-sans font-medium text-red-800 hover:bg-red-200"
                  >
                    Sao chép
                  </button>
                </div>
              )}
            </div>
            <button type="button" onClick={() => setFailure(null)} className="text-red-600 hover:text-red-900 text-xs font-bold">Đóng</button>
          </div>
        )}

        <nav aria-label="Khu vực phân quyền" className="mb-6 flex flex-wrap gap-2 border-b border-slate-200 pb-3">
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
              className={`rounded-lg px-4 py-2 text-sm font-semibold transition-all ${
                tab === item
                  ? "bg-indigo-600 text-white shadow-sm"
                  : "bg-white text-slate-700 ring-1 ring-slate-200 hover:bg-slate-100"
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
    </main>
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
  const [reason, setReason] = useState("");

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
    setReason("");
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
        reason: reason.trim(),
      }),
    onSuccess: async () => {
      await invalidate();
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
        reason: reason.trim(),
      }),
    onSuccess: async () => {
      await invalidate();
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
  const evaluatePermission = (permission: PermissionDto) => {
    const isOwnerByActor = actorPermissions.has(permission.permissionCode);
    const isDelegable = permission.isDelegable;
    const isCompatible = selected ? permission.allowedAccountTypes.includes(selected.accountType) : false;
    // For CenterManager target roles: actor can only grant permissions they themselves own
    const isOutOfScope = selected?.accountType === "CenterManager" && !isOwnerByActor;
    const isAssigned = selectedPermissions.includes(permission.permissionCode);
    const canToggle = canManagePermissions && !selected?.isSystemRole && isDelegable && isCompatible && !isOutOfScope;

    return {
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

  const reasonValid = reason.trim().length >= 3 && reason.trim().length <= 1000;
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
      <div className="rounded-xl bg-white p-4 shadow-sm ring-1 ring-slate-200">
        <div className="mb-3 flex items-center justify-between">
          <div>
            <h2 className="font-bold text-slate-900">Danh sách vai trò</h2>
            <p className="text-xs text-slate-500">{rolesMeta?.totalItems ?? roles.length} vai trò trong trung tâm</p>
          </div>
          {canCreate && (
            <button
              type="button"
              id="btn-open-create-role"
              onClick={() => setShowCreate((v) => !v)}
              className="rounded-md bg-indigo-50 px-2.5 py-1 text-xs font-bold text-indigo-700 hover:bg-indigo-100"
            >
              {showCreate ? "Hủy tạo" : "+ Tạo vai trò"}
            </button>
          )}
        </div>

        {/* Create Role Form */}
        {showCreate && (
          <form
            className="mb-4 space-y-3 rounded-lg border border-indigo-200 bg-indigo-50/50 p-3"
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
            <h3 className="text-xs font-bold uppercase tracking-wider text-indigo-900">Tạo vai trò tùy chỉnh mới</h3>
            <div>
              <label className="block text-xs font-medium text-slate-700">Mã vai trò (RoleCode)</label>
              <input
                aria-label="Mã role"
                required
                maxLength={64}
                pattern="^[A-Z][A-Z0-9_]*$"
                title="Bắt đầu bằng chữ in hoa, chỉ gồm chữ in hoa, số và dấu gạch dưới"
                value={createCode}
                onChange={(event) => setCreateCode(event.target.value.toUpperCase())}
                placeholder="TEACHER_MATH_LEAD"
                className="mt-1 w-full rounded-md border border-slate-300 bg-white px-2.5 py-1.5 font-mono text-xs uppercase"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-700">Tên vai trò</label>
              <input
                aria-label="Tên role"
                required
                maxLength={150}
                value={createName}
                onChange={(event) => setCreateName(event.target.value)}
                placeholder="Tổ trưởng bộ môn Toán"
                className="mt-1 w-full rounded-md border border-slate-300 bg-white px-2.5 py-1.5 text-xs"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-700">Loại tài khoản tương thích</label>
              <select
                aria-label="Loại tài khoản"
                value={createType}
                onChange={(event) => setCreateType(event.target.value as AccountType)}
                className="mt-1 w-full rounded-md border border-slate-300 bg-white px-2.5 py-1.5 text-xs font-medium"
              >
                <option value="Teacher">Giáo viên (Teacher)</option>
                <option value="CenterManager">Quản lý trung tâm (CenterManager)</option>
                <option value="Student">Học sinh (Student)</option>
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-700">Mô tả mục đích</label>
              <textarea
                aria-label="Mô tả role"
                maxLength={500}
                value={createDescription}
                onChange={(event) => setCreateDescription(event.target.value)}
                placeholder="Phạm vi công việc và nhiệm vụ được ủy quyền..."
                className="mt-1 w-full rounded-md border border-slate-300 bg-white px-2.5 py-1.5 text-xs"
                rows={2}
              />
            </div>
            <button
              type="submit"
              disabled={createMutation.isPending || !createCode.trim() || !createName.trim()}
              className="w-full rounded-md bg-indigo-600 px-3 py-2 text-xs font-bold text-white shadow-sm hover:bg-indigo-700 disabled:opacity-50"
            >
              {createMutation.isPending ? "Đang xử lý..." : "Xác nhận tạo vai trò"}
            </button>
          </form>
        )}

        {/* Filters */}
        <div className="mb-3 space-y-2">
          <input
            type="search"
            placeholder="Tìm theo mã hoặc tên vai trò..."
            value={searchRole}
            onChange={(e) => {
              setSearchRole(e.target.value);
              setRolePage(1);
            }}
            className="w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-xs"
          />
          <div className="grid grid-cols-2 gap-2">
            <select
              aria-label="Lọc theo loại tài khoản"
              value={filterAccountType}
              onChange={(e) => {
                setFilterAccountType(e.target.value);
                setRolePage(1);
              }}
              className="rounded-md border border-slate-300 px-2 py-1 text-xs font-medium"
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
              className="rounded-md border border-slate-300 px-2 py-1 text-xs"
            >
              <option value="All">Tất cả trạng thái</option>
              <option value="Active">Đang hoạt động (Active)</option>
              <option value="Archived">Đã lưu trữ (Archived)</option>
            </select>
          </div>
        </div>

        {/* Role Items */}
        {roleLoading ? (
          <p className="py-6 text-center text-xs text-slate-500">Đang tải danh sách vai trò...</p>
        ) : roles.length === 0 ? (
          <p className="py-6 text-center text-xs text-slate-500">Không tìm thấy vai trò phù hợp bộ lọc.</p>
        ) : (
          <div className="max-h-[34rem] space-y-2 overflow-y-auto pr-1">
            {roles.map((role) => (
              <button
                key={role.roleId}
                type="button"
                id={`role-item-${role.roleCode}`}
                onClick={() => setSelectedId(role.roleId)}
                className={`w-full rounded-lg p-3 text-left transition-all ${
                  selected?.roleId === role.roleId
                    ? "bg-indigo-50/80 ring-2 ring-indigo-500"
                    : "bg-slate-50 hover:bg-slate-100 ring-1 ring-slate-200"
                }`}
              >
                <div className="flex items-start justify-between gap-1">
                  <span className="font-semibold text-slate-900 text-sm">{role.roleName}</span>
                  {role.isSystemRole && (
                    <span className="rounded bg-blue-100 px-1.5 py-0.5 text-[10px] font-bold text-blue-700">
                      Hệ thống
                    </span>
                  )}
                </div>
                <div className="mt-1 flex flex-wrap items-center gap-1.5 text-xs text-slate-500">
                  <span className="font-mono text-[11px] font-medium text-slate-700">{role.roleCode}</span>
                  <span>•</span>
                  <span>{accountTypeLabels[role.accountType]}</span>
                </div>
                <div className="mt-2 flex items-center justify-between text-[11px] text-slate-500">
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
          <div className="mt-3 flex items-center justify-between border-t border-slate-200 pt-3 text-xs text-slate-600">
            <span>
              Trang {rolesMeta.page} / {rolesMeta.totalPages} ({rolesMeta.totalItems} vai trò)
            </span>
            <div className="flex gap-1">
              <button
                type="button"
                disabled={rolePage <= 1}
                onClick={() => setRolePage((p) => Math.max(1, p - 1))}
                className="rounded border border-slate-300 px-2 py-1 hover:bg-slate-50 disabled:opacity-50"
              >
                Trước
              </button>
              <button
                type="button"
                disabled={rolePage >= rolesMeta.totalPages}
                onClick={() => setRolePage((p) => Math.min(rolesMeta.totalPages, p + 1))}
                className="rounded border border-slate-300 px-2 py-1 hover:bg-slate-50 disabled:opacity-50"
              >
                Sau
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Right Column: Selected Role Details & Permission Matrix */}
      <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
        {!selected ? (
          <div className="py-12 text-center text-slate-500">
            <p className="text-sm font-medium">Chưa có vai trò nào được chọn.</p>
          </div>
        ) : (
          <div className="space-y-6">
            {/* System Role Notice Banner */}
            {isSystemRole && (
              <div className="rounded-lg border border-blue-200 bg-blue-50/70 p-4 text-blue-900">
                <div className="flex items-center gap-2 font-semibold">
                  <span className="text-base">🛡️</span>
                  <span>Vai trò hệ thống mặc định (Chỉ đọc)</span>
                </div>
                <p className="mt-1 text-xs text-blue-800">
                  Vai trò này là cấu hình hệ thống cố định cho trung tâm nhằm đảm bảo các quyền vận hành tối thiểu. Tên vai trò, trạng thái và ma trận quyền được khóa để ngăn ngừa xung đột chính sách.
                </p>
              </div>
            )}

            {/* Role Header & Metadata */}
            <div className="border-b border-slate-200 pb-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div>
                  <div className="flex items-center gap-2">
                    <h2 className="text-xl font-bold text-slate-900">{selected.roleName}</h2>
                    <span className="rounded bg-slate-100 px-2 py-0.5 font-mono text-xs text-slate-700">
                      {selected.roleCode}
                    </span>
                  </div>
                  <p className="mt-0.5 text-xs text-slate-500">
                    Tương thích: <strong className="text-slate-700">{accountTypeLabels[selected.accountType]}</strong> · Phiên bản RowVersion: <span className="font-mono">{selected.rowVersion}</span>
                  </p>
                </div>
                <div className="flex items-center gap-2 text-xs">
                  <span className={`rounded-full px-2.5 py-0.5 font-bold ${selected.status === "Active" ? "bg-emerald-100 text-emerald-800" : "bg-slate-100 text-slate-600"}`}>
                    {selected.status}
                  </span>
                  <span className="text-slate-500 font-medium">{selected.activeUserCount} tài khoản được gán</span>
                </div>
              </div>
            </div>

            {/* Basic Info Form */}
            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <label className="block text-xs font-semibold text-slate-700">Tên hiển thị vai trò</label>
                <input
                  disabled={!canUpdate || isSystemRole}
                  value={roleName}
                  onChange={(e) => setRoleName(e.target.value)}
                  maxLength={150}
                  className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm disabled:bg-slate-100 disabled:text-slate-500"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700">Trạng thái vận hành</label>
                <select
                  disabled={!canUpdate || isSystemRole}
                  value={status}
                  onChange={(e) => setStatus(e.target.value as "Active" | "Archived")}
                  className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm font-medium disabled:bg-slate-100 disabled:text-slate-500"
                >
                  <option value="Active">Đang hoạt động (Active)</option>
                  <option value="Archived" disabled={!canArchive}>
                    Đã lưu trữ (Archived) {!canArchive && "(Cần quyền archive)"}
                  </option>
                </select>
              </div>

              <div className="sm:col-span-2">
                <label className="block text-xs font-semibold text-slate-700">Mô tả trách nhiệm & thẩm quyền</label>
                <textarea
                  disabled={!canUpdate || isSystemRole}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  maxLength={500}
                  rows={2}
                  className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm disabled:bg-slate-100 disabled:text-slate-500"
                  placeholder="Mô tả phạm vi vai trò..."
                />
              </div>
            </div>

            {/* Save Role Info Action */}
            {canUpdate && !isSystemRole && (
              <div className="flex items-center justify-between border-t border-slate-100 pt-3">
                <span className="text-xs text-slate-500">
                  {roleInfoUnchanged ? "Thông tin vai trò chưa thay đổi" : "Có thay đổi thông tin vai trò chưa lưu"}
                </span>
                <button
                  type="button"
                  id="btn-save-role-info"
                  disabled={roleInfoUnchanged || !reasonValid || updateMutation.isPending}
                  onClick={() => updateMutation.mutate()}
                  className="rounded-md bg-slate-800 px-4 py-1.5 text-xs font-bold text-white shadow-sm hover:bg-slate-900 disabled:opacity-40"
                >
                  {updateMutation.isPending ? "Đang lưu..." : "Lưu thông tin vai trò"}
                </button>
              </div>
            )}

            {/* Permission Matrix Section */}
            <div className="border-t border-slate-200 pt-5">
              <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
                <div>
                  <h3 className="font-bold text-slate-900 text-base">Ma trận quyền hạn (Dynamic Permission Matrix)</h3>
                  <p className="text-xs text-slate-500">
                    Đã chọn <strong className="text-indigo-600">{selectedPermissions.length}</strong> quyền cho vai trò này.
                  </p>
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  <input
                    type="search"
                    placeholder="Lọc mã permission..."
                    value={permissionSearch}
                    onChange={(e) => setPermissionSearch(e.target.value)}
                    className="rounded-md border border-slate-300 px-2.5 py-1 text-xs"
                  />
                </div>
              </div>

              {/* Legend of Granular Permissions */}
              <div className="mb-4 flex flex-wrap items-center gap-3 rounded-lg bg-slate-50 p-3 text-xs text-slate-600">
                <span className="font-bold text-slate-700">Chú giải ma trận:</span>
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
                <p className="py-8 text-center text-xs text-slate-500">Đang tải danh mục permission...</p>
              ) : !canReadPermissionCatalog ? (
                <p className="rounded-md bg-amber-50 p-3 text-xs text-amber-800">
                  Tài khoản của bạn chưa có quyền đọc danh mục permission (`authorization.permissions.read`).
                </p>
              ) : groupedCatalog.length === 0 ? (
                <p className="py-6 text-center text-xs text-slate-500">Không tìm thấy permission phù hợp từ khóa.</p>
              ) : (
                <div className="max-h-[30rem] space-y-4 overflow-y-auto rounded-lg border border-slate-200 p-4">
                  {groupedCatalog.map(([moduleName, modulePermissions]) => {
                    const toggleableInModule = modulePermissions.filter(
                      (p) => evaluatePermission(p).canToggle,
                    );
                    const allSelected =
                      toggleableInModule.length > 0 &&
                      toggleableInModule.every((p) => selectedPermissions.includes(p.permissionCode));

                    return (
                      <fieldset key={moduleName} className="rounded-md border border-slate-100 bg-slate-50/50 p-3">
                        <div className="mb-2 flex items-center justify-between border-b border-slate-200 pb-2">
                          <legend className="font-bold uppercase tracking-wider text-xs text-slate-800">
                            {moduleName} ({modulePermissions.length})
                          </legend>
                          {canManagePermissions && !isSystemRole && toggleableInModule.length > 0 && (
                            <div className="flex items-center gap-2">
                              <button
                                type="button"
                                onClick={() => handleToggleModuleAll(modulePermissions, !allSelected)}
                                className="text-[11px] font-semibold text-indigo-600 hover:text-indigo-800"
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
                                className={`flex items-start gap-2.5 rounded-md p-2 text-xs transition-colors ${
                                  evalResult.isAssigned
                                    ? "bg-indigo-50/60 border border-indigo-200"
                                    : "bg-white border border-slate-200 hover:bg-slate-50"
                                } ${!evalResult.canToggle ? "opacity-60 cursor-not-allowed" : "cursor-pointer"}`}
                              >
                                <input
                                  type="checkbox"
                                  id={checkboxId}
                                  disabled={!evalResult.canToggle}
                                  checked={evalResult.isAssigned}
                                  onChange={() => handleTogglePermission(permission.permissionCode)}
                                  className="mt-0.5 rounded border-slate-300 text-indigo-600 focus:ring-indigo-500 disabled:opacity-50"
                                />
                                <div className="flex-1">
                                  <div className="flex flex-wrap items-center gap-1.5">
                                    <span className="font-mono font-medium text-slate-800">
                                      {permission.permissionCode}
                                    </span>
                                    {evalResult.isOwnerByActor && (
                                      <span className="rounded bg-emerald-100 px-1 py-0.2 text-[9px] font-bold text-emerald-800">
                                        Bạn có
                                      </span>
                                    )}
                                    {permission.isSensitive && (
                                      <span className="rounded bg-amber-100 px-1 py-0.2 text-[9px] font-bold text-amber-800">
                                        Nhạy cảm
                                      </span>
                                    )}
                                  </div>
                                  <p className="mt-0.5 text-[11px] text-slate-500">{permission.description}</p>

                                  {/* Error/Guard Status Badges */}
                                  {!evalResult.isCompatible && (
                                    <span className="mt-1 inline-block text-[10px] font-semibold text-slate-500">
                                      ✕ Không tương thích {accountTypeLabels[selected.accountType]}
                                    </span>
                                  )}
                                  {!evalResult.isDelegable && (
                                    <span className="mt-1 inline-block text-[10px] font-semibold text-red-600">
                                      ✕ Non-delegable (Quyền không được phép ủy quyền)
                                    </span>
                                  )}
                                  {evalResult.isOutOfScope && (
                                    <span className="mt-1 inline-block text-[10px] font-semibold text-red-600">
                                      ✕ Vượt thẩm quyền (Bạn không sở hữu quyền này)
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
            {(canUpdate || canManagePermissions) && (
              <div className="rounded-lg bg-slate-50 p-4 border border-slate-200 space-y-3">
                <label className="block text-xs font-bold text-slate-700">
                  Lý do thay đổi phân quyền <span className="text-red-500">*</span>
                  <input
                    required
                    maxLength={1000}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    placeholder="Ví dụ: Cập nhật ma trận phân quyền phục vụ kỳ thi học kỳ 1 (tối thiểu 3 ký tự)..."
                    className="mt-1 w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-xs"
                  />
                </label>

                <div className="flex flex-wrap items-center justify-between gap-3">
                  <span className="text-xs text-slate-500">
                    {permissionsUnchanged
                      ? "Ma trận quyền chưa thay đổi"
                      : "Ma trận quyền đã thay đổi và cần xác nhận"}
                  </span>
                  {canManagePermissions && (
                    <button
                      type="button"
                      id="btn-replace-role-permissions"
                      disabled={
                        permissionsUnchanged ||
                        !reasonValid ||
                        permissionsMutation.isPending ||
                        isSystemRole
                      }
                      onClick={() => permissionsMutation.mutate()}
                      className="rounded-md bg-indigo-600 px-4 py-2 text-xs font-bold text-white shadow-sm hover:bg-indigo-700 disabled:opacity-40"
                    >
                      {permissionsMutation.isPending ? "Đang thay thế..." : "Thay thế permission (Canonical)"}
                    </button>
                  )}
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
  canAssign: boolean;
  onSuccess: (message: string, changedUserId: string) => Promise<void>;
  onError: (error: unknown) => void;
}

const UserRolePanel = ({
  currentUser,
  actorPermissions,
  catalog,
  canAssign,
  onSuccess,
  onError,
}: UserRolePanelProps) => {
  const [selectedUserId, setSelectedUserId] = useState(currentUser.userId);
  const [userSearch, setUserSearch] = useState("");
  const [userAccountTypeFilter, setUserAccountTypeFilter] = useState<string>("All");
  const [userStatusFilter, setUserStatusFilter] = useState<string>("All");
  const [userPage, setUserPage] = useState(1);
  const [selectedRoleIds, setSelectedRoleIds] = useState<string[]>([]);
  const [reason, setReason] = useState("");

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
    enabled: canAssign,
  });

  const users = useMemo(() => usersQuery.data?.data ?? [], [usersQuery.data?.data]);
  const usersMeta = usersQuery.data?.meta;
  const userLoading = usersQuery.isLoading;

  const selectedUser = useMemo(() => {
    return users.find((item) => item.userId === selectedUserId) ?? currentUser;
  }, [currentUser, selectedUserId, users]);

  // Load all active roles compatible with selected user's account type
  const rolesQuery = useQuery({
    queryKey: ["authorization", "roles-for-user", selectedUser.accountType],
    queryFn: () =>
      authorizationApi.listRoles({
        accountType: selectedUser.accountType,
        status: "Active",
        page: 1,
        pageSize: 100,
      }),
    enabled: Boolean(selectedUser.accountType),
  });

  const compatibleRoles = useMemo(() => {
    return filterCompatibleRoles(rolesQuery.data?.data ?? [], selectedUser.accountType);
  }, [rolesQuery.data?.data, selectedUser.accountType]);

  const authorizationQuery = useQuery({
    queryKey: ["authorization", "user", selectedUserId],
    queryFn: () => authorizationApi.getUserAuthorization(selectedUserId),
    enabled: Boolean(selectedUserId),
  });

  const userAuth = authorizationQuery.data?.data;

  useEffect(() => {
    if (userAuth) {
      const activeIds = userAuth.roles
        .filter((r) => r.assignmentStatus === "Active")
        .map((r) => r.roleId);
      setSelectedRoleIds(activeIds);
      setReason("");
    }
  }, [userAuth]);

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

  // Compute effective permissions with source role attribution
  const effectivePermissionsBreakdown = useMemo(() => {
    const selectedRoles = compatibleRoles.filter((r) => selectedRoleIds.includes(r.roleId));
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
  }, [catalog, compatibleRoles, selectedRoleIds]);

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
      <div className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
        <h2 className="font-bold text-slate-900 text-sm">Người dùng cùng trung tâm</h2>
        <p className="mt-0.5 text-xs text-slate-500">Chọn người dùng để gán hoặc thu hồi vai trò</p>

        <div className="mt-3 space-y-2">
          <input
            type="search"
            placeholder="Tìm theo tên hoặc username..."
            value={userSearch}
            onChange={(e) => {
              setUserSearch(e.target.value);
              setUserPage(1);
            }}
            className="w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-xs"
          />
          <div className="grid grid-cols-2 gap-2">
            <select
              aria-label="Lọc theo loại tài khoản người dùng"
              value={userAccountTypeFilter}
              onChange={(e) => {
                setUserAccountTypeFilter(e.target.value);
                setUserPage(1);
              }}
              className="w-full rounded-md border border-slate-300 px-2 py-1 text-xs"
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
              className="w-full rounded-md border border-slate-300 px-2 py-1 text-xs"
            >
              <option value="All">Tất cả trạng thái</option>
              <option value="Active">Đang hoạt động</option>
              <option value="Locked">Bị khóa</option>
              <option value="Disabled">Vô hiệu hóa</option>
            </select>
          </div>
        </div>

        <div className="mt-3 max-h-[34rem] space-y-1.5 overflow-y-auto pr-1">
          {userLoading ? (
            <p className="py-6 text-center text-xs text-slate-500">Đang tải danh sách người dùng...</p>
          ) : users.length === 0 ? (
            <p className="py-6 text-center text-xs text-slate-500">Không tìm thấy người dùng phù hợp.</p>
          ) : (
            users.map((u) => (
              <button
                key={u.userId}
                type="button"
                id={`user-item-${u.userId}`}
                onClick={() => setSelectedUserId(u.userId)}
                className={`w-full rounded-lg p-2.5 text-left transition-all ${
                  selectedUserId === u.userId
                    ? "bg-indigo-50 ring-2 ring-indigo-500"
                    : "bg-slate-50 hover:bg-slate-100 ring-1 ring-slate-200"
                }`}
              >
                <div className="flex items-center justify-between">
                  <span className="font-semibold text-slate-900 text-xs">{u.displayName}</span>
                  {isSelfUser(currentUser.userId, u.userId) && (
                    <span className="rounded bg-indigo-100 px-1 py-0.2 text-[9px] font-bold text-indigo-700">
                      Chính bạn
                    </span>
                  )}
                </div>
                <div className="mt-0.5 flex items-center justify-between text-[11px] text-slate-500">
                  <span>{u.username}</span>
                  <span className="font-medium text-slate-600">{accountTypeLabels[u.accountType]}</span>
                </div>
              </button>
            ))
          )}
        </div>

        {/* User Pagination Controls */}
        {usersMeta && usersMeta.totalPages > 1 && (
          <div className="mt-3 flex items-center justify-between border-t border-slate-200 pt-3 text-xs text-slate-600">
            <span>
              Trang {usersMeta.page} / {usersMeta.totalPages} ({usersMeta.totalItems} người)
            </span>
            <div className="flex gap-1">
              <button
                type="button"
                disabled={userPage <= 1}
                onClick={() => setUserPage((p) => Math.max(1, p - 1))}
                className="rounded border border-slate-300 px-2 py-1 hover:bg-slate-50 disabled:opacity-50"
              >
                Trước
              </button>
              <button
                type="button"
                disabled={userPage >= usersMeta.totalPages}
                onClick={() => setUserPage((p) => Math.min(usersMeta.totalPages, p + 1))}
                className="rounded border border-slate-300 px-2 py-1 hover:bg-slate-50 disabled:opacity-50"
              >
                Sau
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Right Column: Roles Assignment and Effective Permissions */}
      <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
        {/* User Identity Header */}
        <div className="border-b border-slate-200 pb-4">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <div className="flex items-center gap-2">
                <h2 className="text-xl font-bold text-slate-900">{selectedUser.displayName}</h2>
                <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-semibold text-slate-700">
                  {accountTypeLabels[selectedUser.accountType]}
                </span>
              </div>
              <p className="mt-0.5 text-xs text-slate-500">
                Tài khoản: <strong className="text-slate-700">{selectedUser.username}</strong> · AuthVersion: <span className="font-mono">{userAuth?.authorizationVersion ?? "..."}</span> · RowVersion: <span className="font-mono">{userAuth?.rowVersion ?? "..."}</span>
              </p>
            </div>
            {isSelfChange && (
              <span className="rounded bg-amber-100 px-2.5 py-1 text-xs font-bold text-amber-800">
                Tài khoản đang đăng nhập
              </span>
            )}
          </div>
        </div>

        {/* Self-Change Warning Banner */}
        {isSelfChange && (
          <div className="mt-4 rounded-lg border border-amber-300 bg-amber-50 p-4 text-amber-900">
            <div className="flex items-center gap-2 font-semibold">
              <span className="text-base">⚠️</span>
              <span>Cảnh báo tự thay đổi vai trò (Self-change Warning)</span>
            </div>
            <p className="mt-1 text-xs text-amber-800">
              Bạn đang chỉnh sửa vai trò của chính tài khoản mình đang đăng nhập. Việc gỡ bỏ quyền hoặc thay đổi vai trò có thể làm thay đổi quyền hạn hiệu lực và tự động làm mới phiên đăng nhập của bạn.
            </p>
          </div>
        )}

        {authorizationQuery.isLoading ? (
          <p className="py-12 text-center text-xs text-slate-500">Đang tải thông tin quyền người dùng...</p>
        ) : authorizationQuery.isError ? (
          <p className="py-8 text-center text-xs text-red-700">Không thể tải thông tin quyền của người dùng được chọn.</p>
        ) : (
          <div className="mt-6 space-y-6">
            {/* Roles Selection Section */}
            <div>
              <h3 className="font-bold text-slate-900 text-sm">
                Vai trò tương thích đang hoạt động ({compatibleRoles.length})
              </h3>
              <p className="text-xs text-slate-500">
                Chọn các vai trò áp dụng cho {accountTypeLabels[selectedUser.accountType]}.
              </p>

              <div className="mt-3 space-y-2">
                {compatibleRoles.map((role) => {
                  const outOfScope = isRoleOutOfScopeForActor(role);
                  const isChecked = selectedRoleIds.includes(role.roleId);
                  const checkboxId = `role-chk-${role.roleId}`;

                  return (
                    <label
                      key={role.roleId}
                      htmlFor={checkboxId}
                      className={`flex items-start gap-3 rounded-lg border p-3 text-xs transition-colors ${
                        isChecked
                          ? "border-indigo-300 bg-indigo-50/60"
                          : "border-slate-200 bg-white hover:bg-slate-50"
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
                        className="mt-0.5 rounded border-slate-300 text-indigo-600 focus:ring-indigo-500 disabled:opacity-50"
                      />
                      <div className="flex-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="font-semibold text-slate-900 text-sm">{role.roleName}</span>
                          <span className="font-mono text-slate-600 text-[11px]">{role.roleCode}</span>
                          {role.isSystemRole && (
                            <span className="rounded bg-blue-100 px-1.5 py-0.5 text-[10px] font-bold text-blue-700">
                              Hệ thống
                            </span>
                          )}
                        </div>
                        <p className="mt-1 text-slate-500">{role.description || "Không có mô tả chi tiết."}</p>
                        <p className="mt-0.5 text-[11px] text-slate-400">
                          {role.permissionCodes.length} permissions · {role.activeUserCount} người đang dùng
                        </p>
                        {outOfScope && (
                          <span className="mt-1 inline-block font-semibold text-red-600 text-[10px]">
                            ✕ Chứa quyền vượt thẩm quyền của bạn (Không thể gán vai trò này cho Quản lý khác)
                          </span>
                        )}
                      </div>
                    </label>
                  );
                })}
                {compatibleRoles.length === 0 && (
                  <p className="rounded-lg bg-slate-50 p-4 text-center text-xs text-slate-500">
                    Không có vai trò Active nào tương thích với loại tài khoản {accountTypeLabels[selectedUser.accountType]}.
                  </p>
                )}
              </div>
            </div>

            {/* Effective Permissions Breakdown (Spec Requirement 7 & 564) */}
            <div className="border-t border-slate-200 pt-5">
              <div className="mb-2">
                <h3 className="font-bold text-slate-900 text-sm">
                  Quyền hạn hiệu lực (Effective Permissions)
                </h3>
                <p className="text-xs text-slate-500">
                  Tổng hợp các capability người dùng sẽ sở hữu từ các vai trò được chọn kèm role nguồn cấp quyền.
                </p>
              </div>

              {effectivePermissionsBreakdown.length === 0 ? (
                <p className="rounded-lg bg-slate-50 p-3 text-center text-xs text-slate-500">
                  Chưa có quyền hạn hiệu lực nào được gán cho người dùng này.
                </p>
              ) : (
                <div className="max-h-56 space-y-1.5 overflow-y-auto rounded-lg border border-slate-200 p-3">
                  {effectivePermissionsBreakdown.map((item) => (
                    <div
                      key={item.code}
                      className="flex flex-wrap items-center justify-between gap-2 rounded bg-slate-50 px-2.5 py-1.5 text-xs"
                    >
                      <div>
                        <span className="font-mono font-medium text-slate-800">{item.code}</span>
                        <span className="ml-2 text-[11px] text-slate-500">{item.description}</span>
                      </div>
                      <div className="flex flex-wrap items-center gap-1">
                        {item.sourceRoles.map((roleName) => (
                          <span
                            key={roleName}
                            className="rounded bg-indigo-100 px-1.5 py-0.5 text-[10px] font-semibold text-indigo-700"
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
            {canAssign && (
              <div className="rounded-lg bg-slate-50 p-4 border border-slate-200 space-y-3">
                <label className="block text-xs font-bold text-slate-700">
                  Lý do thay đổi vai trò người dùng <span className="text-red-500">*</span>
                  <input
                    required
                    maxLength={1000}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    placeholder="Ví dụ: Bổ sung nhiệm vụ quản lý học thuật tuần 2 (tối thiểu 3 ký tự)..."
                    className="mt-1 w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-xs"
                  />
                </label>

                <div className="flex flex-wrap items-center justify-between gap-3">
                  <span className="text-xs text-slate-500">
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
                    className="rounded-md bg-indigo-600 px-4 py-2 text-xs font-bold text-white shadow-sm hover:bg-indigo-700 disabled:opacity-40"
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
    <section className="overflow-hidden rounded-xl bg-white shadow-sm ring-1 ring-slate-200">
      {/* Header & Filter Controls */}
      <div className="border-b border-slate-200 p-5">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h2 className="text-xl font-bold text-slate-900">Nhật ký kiểm toán phân quyền</h2>
            <p className="text-xs text-slate-500">
              Ghi nhận các đột biến phân quyền, thay đổi vai trò, mật khẩu và xóa mềm trong trung tâm hiện tại (Append-only).
            </p>
          </div>
          <span className="rounded bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-800 ring-1 ring-emerald-200">
            Dữ liệu đã khử khuẩn (0 secrets)
          </span>
        </div>

        {/* Filter Toolbar */}
        <div className="mt-4 grid gap-3 sm:grid-cols-2 md:grid-cols-4 lg:grid-cols-4">
          <div>
            <label className="block text-[11px] font-semibold text-slate-700">Loại hành động</label>
            <select
              aria-label="Lọc theo loại hành động kiểm toán"
              value={filterActionType}
              onChange={(e) => {
                setFilterActionType(e.target.value);
                setPage(1);
              }}
              className="mt-1 w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-xs"
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
            <label className="block text-[11px] font-semibold text-slate-700">Mã quyền hạn</label>
            <input
              type="text"
              placeholder="VD: PERM_ROLES_CREATE"
              aria-label="Lọc theo mã quyền hạn"
              value={filterPermissionCode}
              onChange={(e) => {
                setFilterPermissionCode(e.target.value);
                setPage(1);
              }}
              className="mt-1 w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-xs font-mono"
            />
          </div>

          <div>
            <label className="block text-[11px] font-semibold text-slate-700">Người thực hiện (Actor ID)</label>
            <input
              type="text"
              placeholder="Actor User ID"
              aria-label="Lọc theo người thực hiện"
              value={filterActorUserId}
              onChange={(e) => {
                setFilterActorUserId(e.target.value);
                setPage(1);
              }}
              className="mt-1 w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-xs font-mono"
            />
          </div>

          <div>
            <label className="block text-[11px] font-semibold text-slate-700">Người dùng đích (Target User)</label>
            <input
              type="text"
              placeholder="Target User ID"
              aria-label="Lọc theo người dùng đích"
              value={filterTargetUserId}
              onChange={(e) => {
                setFilterTargetUserId(e.target.value);
                setPage(1);
              }}
              className="mt-1 w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-xs font-mono"
            />
          </div>

          <div>
            <label className="block text-[11px] font-semibold text-slate-700">Đối tượng đích (Target ID)</label>
            <input
              type="text"
              placeholder="Role ID hoặc User ID"
              aria-label="Lọc theo đối tượng đích"
              value={filterTargetId}
              onChange={(e) => {
                setFilterTargetId(e.target.value);
                setPage(1);
              }}
              className="mt-1 w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-xs font-mono"
            />
          </div>

          <div>
            <label className="block text-[11px] font-semibold text-slate-700">Từ thời điểm</label>
            <input
              type="datetime-local"
              aria-label="Lọc từ thời điểm"
              value={filterFrom}
              onChange={(e) => {
                setFilterFrom(e.target.value);
                setPage(1);
              }}
              className="mt-1 w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-xs"
            />
          </div>

          <div>
            <label className="block text-[11px] font-semibold text-slate-700">Đến thời điểm</label>
            <input
              type="datetime-local"
              aria-label="Lọc đến thời điểm"
              value={filterTo}
              onChange={(e) => {
                setFilterTo(e.target.value);
                setPage(1);
              }}
              className="mt-1 w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-xs"
            />
          </div>

          <div className="flex items-end">
            <button
              type="button"
              onClick={resetFilters}
              className="w-full rounded-md border border-slate-300 bg-slate-50 px-3 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-100"
            >
              Đặt lại bộ lọc
            </button>
          </div>
        </div>
      </div>

      {/* Table Content */}
      {auditQuery.isLoading ? (
        <p className="py-12 text-center text-xs text-slate-500">Đang tải nhật ký kiểm toán...</p>
      ) : auditQuery.isError ? (
        <p className="py-10 text-center text-xs text-red-700">Không thể tải dữ liệu nhật ký kiểm toán.</p>
      ) : entries.length === 0 ? (
        <p className="py-12 text-center text-xs text-slate-500">Chưa có bản ghi kiểm toán phù hợp bộ lọc.</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50 text-slate-700">
              <tr>
                <th className="px-4 py-3 text-left font-semibold">Thời gian</th>
                <th className="px-4 py-3 text-left font-semibold">Hành động</th>
                <th className="px-4 py-3 text-left font-semibold">Đối tượng</th>
                <th className="px-4 py-3 text-left font-semibold">Lý do</th>
                <th className="px-4 py-3 text-left font-semibold">Trace ID</th>
                <th className="px-4 py-3 text-right font-semibold">Chi tiết</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {entries.map((entry) => {
                const actionMeta = actionTypeLabels[entry.actionType] ?? {
                  label: entry.actionType,
                  color: "bg-slate-100 text-slate-700",
                };

                return (
                  <tr key={entry.authorizationAuditId} className="hover:bg-slate-50/80">
                    <td className="whitespace-nowrap px-4 py-3 text-slate-600 font-mono text-[11px]">
                      {new Date(entry.createdAt).toLocaleString("vi-VN")}
                    </td>
                    <td className="px-4 py-3">
                      <span className={`rounded-full px-2 py-0.5 text-[10px] font-bold ${actionMeta.color}`}>
                        {actionMeta.label}
                      </span>
                    </td>
                    <td className="px-4 py-3 font-medium text-slate-800">
                      <span>{entry.targetType}</span>
                      <span className="block font-mono text-[11px] text-slate-500 truncate max-w-xs" title={entry.targetId}>
                        {entry.targetId}
                      </span>
                    </td>
                    <td className="max-w-xs truncate px-4 py-3 text-slate-700" title={entry.reason}>
                      {entry.reason}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1.5 font-mono text-[11px] text-slate-600">
                        <span className="truncate max-w-[8rem]" title={entry.traceId}>
                          {entry.traceId}
                        </span>
                        <button
                          type="button"
                          onClick={() => copyTrace(entry.traceId)}
                          title="Sao chép Trace ID"
                          className="rounded p-1 text-slate-400 hover:text-slate-700 hover:bg-slate-100"
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
                        className="rounded-md bg-indigo-50 px-2.5 py-1 text-[11px] font-bold text-indigo-700 hover:bg-indigo-100"
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
        <div className="flex items-center justify-between border-t border-slate-200 px-4 py-3 text-xs text-slate-600">
          <span>
            Trang {meta.page} / {meta.totalPages} ({meta.totalItems} sự kiện)
          </span>
          <div className="flex items-center gap-2">
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              className="rounded-md border border-slate-300 px-3 py-1 font-semibold disabled:opacity-40 hover:bg-slate-50"
            >
              ← Trang trước
            </button>
            <button
              type="button"
              disabled={page >= meta.totalPages}
              onClick={() => setPage((p) => Math.min(meta.totalPages, p + 1))}
              className="rounded-md border border-slate-300 px-3 py-1 font-semibold disabled:opacity-40 hover:bg-slate-50"
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
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4"
          onClick={() => setSelectedAudit(null)}
        >
          <div
            className="w-full max-w-3xl rounded-xl bg-white p-6 shadow-xl max-h-[90vh] overflow-y-auto"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between border-b border-slate-200 pb-3">
              <div>
                <span className="rounded bg-indigo-100 px-2 py-0.5 text-xs font-bold text-indigo-800">
                  {actionTypeLabels[selectedAudit.actionType]?.label ?? selectedAudit.actionType}
                </span>
                <h3 className="mt-1 text-lg font-bold text-slate-900">
                  Chi tiết bản ghi kiểm toán #{selectedAudit.authorizationAuditId}
                </h3>
                <p className="text-xs text-slate-500 font-mono">
                  {new Date(selectedAudit.createdAt).toLocaleString("vi-VN")} UTC
                </p>
              </div>
              <button
                type="button"
                onClick={() => setSelectedAudit(null)}
                className="text-slate-400 hover:text-slate-700 text-lg font-bold"
              >
                ✕
              </button>
            </div>

            <div className="mt-4 grid gap-3 sm:grid-cols-2 text-xs">
              <div className="rounded bg-slate-50 p-2.5">
                <span className="block font-semibold text-slate-500">Đối tượng mục tiêu:</span>
                <span className="font-mono font-medium text-slate-800">{selectedAudit.targetType}: {selectedAudit.targetId}</span>
              </div>
              <div className="rounded bg-slate-50 p-2.5">
                <span className="block font-semibold text-slate-500">Người thực hiện (Actor User ID):</span>
                <span className="font-mono text-slate-800">{selectedAudit.actorUserId ?? "Hệ thống"}</span>
              </div>
              <div className="rounded bg-slate-50 p-2.5 sm:col-span-2">
                <span className="block font-semibold text-slate-500">Lý do thao tác:</span>
                <span className="text-slate-800">{selectedAudit.reason}</span>
              </div>
              <div className="rounded bg-slate-50 p-2.5 sm:col-span-2 flex items-center justify-between">
                <div>
                  <span className="block font-semibold text-slate-500">W3C Trace ID:</span>
                  <span className="font-mono text-[11px] text-slate-800">{selectedAudit.traceId}</span>
                </div>
                <button
                  type="button"
                  onClick={() => copyTrace(selectedAudit.traceId)}
                  className="rounded bg-white px-2 py-1 font-semibold text-slate-700 ring-1 ring-slate-300 hover:bg-slate-100"
                >
                  {copiedTraceId === selectedAudit.traceId ? "✓ Đã chép" : "Sao chép"}
                </button>
              </div>
            </div>

            {/* Before / After Data Inspection */}
            <div className="mt-4 space-y-3">
              <div className="flex items-center justify-between">
                <h4 className="font-bold text-xs uppercase tracking-wider text-slate-700">
                  Đối chiếu dữ liệu (Before / After Data)
                </h4>
                <span className="text-[11px] text-emerald-700 font-medium">
                  ✓ Dữ liệu khử khuẩn nghiêm ngặt
                </span>
              </div>

              <div className="grid gap-3 sm:grid-cols-2">
                <div>
                  <span className="block text-xs font-semibold text-slate-600 mb-1">Trước thay đổi (Before)</span>
                  <pre className="max-h-60 overflow-auto rounded-lg bg-slate-900 p-3 font-mono text-[11px] text-emerald-400">
                    {selectedAudit.before
                      ? JSON.stringify(selectedAudit.before, null, 2)
                      : "(Không có dữ liệu trước)"}
                  </pre>
                </div>
                <div>
                  <span className="block text-xs font-semibold text-slate-600 mb-1">Sau thay đổi (After)</span>
                  <pre className="max-h-60 overflow-auto rounded-lg bg-slate-900 p-3 font-mono text-[11px] text-emerald-400">
                    {selectedAudit.after
                      ? JSON.stringify(selectedAudit.after, null, 2)
                      : "(Không có dữ liệu sau)"}
                  </pre>
                </div>
              </div>
            </div>

            <div className="mt-6 flex justify-end border-t border-slate-200 pt-3">
              <button
                type="button"
                onClick={() => setSelectedAudit(null)}
                className="rounded-md bg-slate-800 px-4 py-2 text-xs font-bold text-white hover:bg-slate-900"
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
