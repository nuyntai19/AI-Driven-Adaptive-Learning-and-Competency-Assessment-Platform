import { useEffect, useMemo, useState } from "react";
import { isAxiosError } from "axios";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { authorizationApi } from "../api/authorizationApi";
import { organizationApi } from "../api/organizationApi";
import { getCurrentUser } from "../auth/authApi";
import { permissions } from "../auth/permissions";
import { useAuthStore } from "../stores/authStore";
import type { ProblemDetails } from "../types/auth";
import type {
  AccountType,
} from "../types/auth";
import type {
  AuthorizationRoleDto,
  AuthorizationUserOption,
  PermissionDto,
} from "../types/authorization";

type Tab = "roles" | "users" | "audit";

const accountTypeLabels: Record<AccountType, string> = {
  CenterManager: "Quản lý trung tâm",
  Teacher: "Giáo viên",
  Student: "Học sinh",
  PlatformAdmin: "Quản trị viên nền tảng",
};

const errorMessage = (error: unknown) => {
  if (!isAxiosError<ProblemDetails>(error)) {
    return "Không thể hoàn tất thao tác. Vui lòng thử lại.";
  }

  const code = error.response?.data?.errorCode;
  const messages: Record<string, string> = {
    AUTH_PRIVILEGE_ESCALATION: "Không thể cấp quyền cao hơn quyền hiện có của bạn.",
    ROLE_ACCOUNT_TYPE_MISMATCH: "Role hoặc permission không tương thích với loại tài khoản.",
    LAST_TENANT_ADMIN: "Phải giữ lại ít nhất một quản lý trung tâm có đủ quyền quản trị.",
    CONCURRENCY_CONFLICT: "Dữ liệu vừa được người khác cập nhật. Hãy tải lại rồi thử lại.",
    INVALID_STATE_TRANSITION: "Không thể thay đổi trạng thái role hệ thống theo cách này.",
    VALIDATION_FAILED: "Dữ liệu chưa hợp lệ. Hãy kiểm tra các trường bắt buộc.",
  };
  return (code && messages[code]) || error.response?.data?.detail || "Không thể hoàn tất thao tác.";
};

export const AuthorizationManagementPage = () => {
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const hasPermission = useAuthStore((state) => state.hasPermission);
  const [tab, setTab] = useState<Tab>("roles");
  const [notice, setNotice] = useState("");
  const [failure, setFailure] = useState("");

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

  const rolesQuery = useQuery({
    queryKey: ["authorization", "roles"],
    queryFn: authorizationApi.listRoles,
    enabled: canReadRoles || canReadUserRoles,
  });
  const permissionQuery = useQuery({
    queryKey: ["authorization", "permissions"],
    queryFn: authorizationApi.listPermissions,
    enabled: canReadPermissionCatalog,
  });
  const auditQuery = useQuery({
    queryKey: ["authorization", "audit"],
    queryFn: authorizationApi.listAudit,
    enabled: canReadAudit && tab === "audit",
  });

  const showSuccess = (message: string) => {
    setFailure("");
    setNotice(message);
  };
  const showError = (error: unknown) => {
    setNotice("");
    setFailure(errorMessage(error));
  };

  return (
    <main className="min-h-screen bg-slate-50 py-8">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <header className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="text-sm font-semibold text-indigo-600">Phân quyền động theo trung tâm</p>
            <h1 className="text-3xl font-bold text-slate-900">Vai trò và quyền truy cập</h1>
            <p className="mt-1 text-sm text-slate-600">
              Thay đổi quyền được ghi audit và làm mới phiên của người bị ảnh hưởng.
            </p>
          </div>
          <Link className="rounded-md bg-white px-4 py-2 text-sm font-semibold text-slate-700 ring-1 ring-slate-300" to="/">
            Về trang chính
          </Link>
        </header>

        {notice && <div role="status" className="mb-4 rounded-md bg-emerald-50 p-4 text-sm text-emerald-800">{notice}</div>}
        {failure && <div role="alert" className="mb-4 rounded-md bg-red-50 p-4 text-sm text-red-800">{failure}</div>}

        <nav aria-label="Khu vực phân quyền" className="mb-6 flex flex-wrap gap-2">
          {availableTabs.map((item) => (
            <button
              key={item}
              type="button"
              onClick={() => { setTab(item); setFailure(""); setNotice(""); }}
              className={`rounded-md px-4 py-2 text-sm font-semibold ${tab === item ? "bg-indigo-600 text-white" : "bg-white text-slate-700 ring-1 ring-slate-300"}`}
            >
              {item === "roles" ? "Role & permission" : item === "users" ? "Gán role người dùng" : "Nhật ký audit"}
            </button>
          ))}
        </nav>

        {tab === "roles" && (
          <RolePermissionPanel
            roles={rolesQuery.data?.data ?? []}
            roleLoading={rolesQuery.isLoading}
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
            roles={rolesQuery.data?.data ?? []}
            canAssign={canAssignUserRoles}
            canReadTeachers={hasPermission(permissions.teachersRead)}
            canReadStudents={hasPermission(permissions.studentsRead)}
            onSuccess={async (message, changedUserId) => {
              await queryClient.invalidateQueries({ queryKey: ["authorization"] });
              if (changedUserId === user.userId) await getCurrentUser();
              showSuccess(message);
            }}
            onError={showError}
          />
        )}

        {tab === "audit" && (
          <AuditPanel
            entries={auditQuery.data?.data ?? []}
            loading={auditQuery.isLoading}
            error={auditQuery.isError}
          />
        )}
      </div>
    </main>
  );
};

interface RolePermissionPanelProps {
  roles: AuthorizationRoleDto[];
  roleLoading: boolean;
  catalog: Awaited<ReturnType<typeof authorizationApi.listPermissions>>["data"];
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
  roles,
  roleLoading,
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
  const selected = roles.find((role) => role.roleId === selectedId) ?? roles[0];
  const [roleName, setRoleName] = useState("");
  const [description, setDescription] = useState("");
  const [status, setStatus] = useState<"Active" | "Archived">("Active");
  const [selectedPermissions, setSelectedPermissions] = useState<string[]>([]);
  const [reason, setReason] = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const [createCode, setCreateCode] = useState("");
  const [createName, setCreateName] = useState("");
  const [createType, setCreateType] = useState<AccountType>("Teacher");
  const [createDescription, setCreateDescription] = useState("");

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
      setCreateCode(""); setCreateName(""); setCreateDescription("");
      onSuccess("Đã tạo role. Hãy chọn các permission cần thiết rồi lưu ma trận quyền.");
    },
    onError,
  });
  const updateMutation = useMutation({
    mutationFn: () => authorizationApi.updateRole(selected!.roleId, {
      roleName: roleName.trim(),
      description: description.trim() || undefined,
      status,
      rowVersion: selected!.rowVersion,
      reason: reason.trim(),
    }),
    onSuccess: async () => { await invalidate(); onSuccess("Đã cập nhật thông tin role."); },
    onError,
  });
  const permissionsMutation = useMutation({
    mutationFn: () => authorizationApi.replaceRolePermissions(selected!.roleId, {
      permissionCodes: selectedPermissions,
      rowVersion: selected!.rowVersion,
      reason: reason.trim(),
    }),
    onSuccess: async () => { await invalidate(); onSuccess("Đã thay thế toàn bộ permission của role."); },
    onError,
  });

  const allowedCatalog = selected
    ? catalog.filter((permission) => permission.isDelegable && permission.allowedAccountTypes.includes(selected.accountType))
    : [];
  const groupedCatalog = Object.entries(
    allowedCatalog.reduce<Record<string, PermissionDto[]>>((groups, permission) => {
      (groups[permission.module] ??= []).push(permission);
      return groups;
    }, {}),
  );
  const reasonValid = reason.trim().length > 0 && reason.trim().length <= 1000;

  return (
    <section className="grid gap-6 lg:grid-cols-[20rem_1fr]">
      <div className="rounded-xl bg-white p-4 shadow-sm ring-1 ring-slate-200">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="font-bold text-slate-900">Danh sách role</h2>
          {canCreate && <button type="button" onClick={() => setShowCreate((value) => !value)} className="text-sm font-semibold text-indigo-600">+ Tạo role</button>}
        </div>
        {showCreate && (
          <form className="mb-4 space-y-3 rounded-lg bg-slate-50 p-3" onSubmit={(event) => {
            event.preventDefault();
            createMutation.mutate({ roleCode: createCode.trim().toUpperCase(), roleName: createName.trim(), accountType: createType, description: createDescription.trim() || undefined });
          }}>
            <input aria-label="Mã role" required maxLength={64} pattern="[A-Z][A-Z0-9_]*" value={createCode} onChange={(event) => setCreateCode(event.target.value.toUpperCase())} placeholder="TEACHER_EDITOR" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" />
            <input aria-label="Tên role" required maxLength={150} value={createName} onChange={(event) => setCreateName(event.target.value)} placeholder="Biên tập viên" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" />
            <select aria-label="Loại tài khoản" value={createType} onChange={(event) => setCreateType(event.target.value as AccountType)} className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm">
              <option value="CenterManager">Quản lý trung tâm</option><option value="Teacher">Giáo viên</option><option value="Student">Học sinh</option>
            </select>
            <textarea aria-label="Mô tả role" maxLength={500} value={createDescription} onChange={(event) => setCreateDescription(event.target.value)} placeholder="Mô tả phạm vi công việc" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" />
            <button disabled={createMutation.isPending} className="w-full rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white disabled:opacity-50">{createMutation.isPending ? "Đang tạo..." : "Tạo role"}</button>
          </form>
        )}
        {roleLoading ? <p className="text-sm text-slate-500">Đang tải role...</p> : (
          <div className="space-y-2">
            {roles.map((role) => (
              <button key={role.roleId} type="button" onClick={() => setSelectedId(role.roleId)} className={`w-full rounded-lg p-3 text-left text-sm ring-1 ${selected?.roleId === role.roleId ? "bg-indigo-50 ring-indigo-300" : "bg-white ring-slate-200"}`}>
                <span className="block font-semibold text-slate-900">{role.roleName}</span>
                <span className="text-xs text-slate-500">{role.roleCode} · {accountTypeLabels[role.accountType]} · {role.status}</span>
              </button>
            ))}
          </div>
        )}
      </div>

      <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
        {!selected ? <p className="text-slate-500">Chưa có role để hiển thị.</p> : (
          <div className="space-y-6">
            <div className="grid gap-4 sm:grid-cols-2">
              <label className="text-sm font-medium text-slate-700">Tên role<input disabled={!canUpdate} value={roleName} onChange={(event) => setRoleName(event.target.value)} className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 disabled:bg-slate-100" /></label>
              <label className="text-sm font-medium text-slate-700">Trạng thái<select disabled={!canUpdate || selected.isSystemRole} value={status} onChange={(event) => setStatus(event.target.value as "Active" | "Archived")} className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 disabled:bg-slate-100"><option value="Active">Active</option><option value="Archived" disabled={!canArchive}>Archived</option></select></label>
              <label className="text-sm font-medium text-slate-700 sm:col-span-2">Mô tả<textarea disabled={!canUpdate} value={description} onChange={(event) => setDescription(event.target.value)} maxLength={500} className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 disabled:bg-slate-100" /></label>
            </div>

            <div>
              <div className="mb-3 flex flex-wrap items-center justify-between gap-2"><h3 className="font-bold text-slate-900">Ma trận permission</h3><span className="text-xs text-slate-500">{selectedPermissions.length} quyền đã chọn · {selected.activeUserCount} người đang dùng</span></div>
              {catalogLoading ? <p className="text-sm text-slate-500">Đang tải permission...</p> : !canReadPermissionCatalog ? <p className="rounded-md bg-amber-50 p-3 text-sm text-amber-800">Bạn chưa có quyền đọc catalog permission.</p> : (
                <div className="max-h-[28rem] space-y-4 overflow-y-auto rounded-lg border border-slate-200 p-4">
                  {groupedCatalog.map(([moduleName, modulePermissions]) => (
                    <fieldset key={moduleName}><legend className="mb-2 font-semibold text-slate-800">{moduleName}</legend><div className="grid gap-2 md:grid-cols-2">
                      {modulePermissions.map((permission) => (
                        <label key={permission.permissionCode} className="flex gap-2 rounded-md p-2 text-sm hover:bg-slate-50">
                          <input type="checkbox" disabled={!canManagePermissions || selected.isSystemRole} checked={selectedPermissions.includes(permission.permissionCode)} onChange={() => setSelectedPermissions((current) => current.includes(permission.permissionCode) ? current.filter((code) => code !== permission.permissionCode) : [...current, permission.permissionCode])} />
                          <span><span className="block font-medium text-slate-800">{permission.permissionCode}{permission.isSensitive && <span className="ml-1 text-amber-700">• nhạy cảm</span>}</span><span className="text-xs text-slate-500">{permission.description}</span></span>
                        </label>
                      ))}
                    </div></fieldset>
                  ))}
                </div>
              )}
            </div>

            {(canUpdate || canManagePermissions) && (
              <div className="space-y-3 border-t border-slate-200 pt-4">
                <label className="block text-sm font-medium text-slate-700">Lý do thay đổi<input required maxLength={1000} value={reason} onChange={(event) => setReason(event.target.value)} placeholder="Ví dụ: Điều chỉnh nhiệm vụ học kỳ 1" className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2" /></label>
                <div className="flex flex-wrap gap-3">
                  {canUpdate && <button type="button" disabled={!reasonValid || updateMutation.isPending} onClick={() => updateMutation.mutate()} className="rounded-md bg-slate-800 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">Lưu thông tin role</button>}
                  {canManagePermissions && <button type="button" disabled={!reasonValid || permissionsMutation.isPending || selected.isSystemRole} onClick={() => permissionsMutation.mutate()} className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">Thay thế permission</button>}
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </section>
  );
};

interface UserRolePanelProps {
  currentUser: AuthorizationUserOption;
  roles: AuthorizationRoleDto[];
  canAssign: boolean;
  canReadTeachers: boolean;
  canReadStudents: boolean;
  onSuccess: (message: string, changedUserId: string) => Promise<void>;
  onError: (error: unknown) => void;
}

const UserRolePanel = ({ currentUser, roles, canAssign, canReadTeachers, canReadStudents, onSuccess, onError }: UserRolePanelProps) => {
  const [selectedUserId, setSelectedUserId] = useState(currentUser.userId);
  const [selectedRoleIds, setSelectedRoleIds] = useState<string[]>([]);
  const [reason, setReason] = useState("");
  const teachersQuery = useQuery({ queryKey: ["authorization", "teacher-options"], queryFn: () => organizationApi.listTeachers({ page: 1, pageSize: 100 }), enabled: canReadTeachers });
  const studentsQuery = useQuery({ queryKey: ["authorization", "student-options"], queryFn: () => organizationApi.listStudents({ page: 1, pageSize: 100 }), enabled: canReadStudents });
  const users = useMemo<AuthorizationUserOption[]>(() => {
    const result = [currentUser];
    for (const teacher of teachersQuery.data?.data ?? []) result.push({ userId: teacher.teacherId, displayName: teacher.displayName, username: teacher.username, accountType: "Teacher", status: teacher.status });
    for (const student of studentsQuery.data?.data ?? []) result.push({ userId: student.studentId, displayName: student.fullName, username: student.username, accountType: "Student", status: student.status });
    return Array.from(new Map(result.map((item) => [item.userId, item])).values());
  }, [currentUser, studentsQuery.data?.data, teachersQuery.data?.data]);
  const selectedUser = users.find((item) => item.userId === selectedUserId) ?? currentUser;
  const authorizationQuery = useQuery({ queryKey: ["authorization", "user", selectedUserId], queryFn: () => authorizationApi.getUserAuthorization(selectedUserId), enabled: Boolean(selectedUserId) });

  useEffect(() => {
    if (authorizationQuery.data) {
      setSelectedRoleIds(authorizationQuery.data.data.roles.filter((role) => role.assignmentStatus === "Active").map((role) => role.roleId));
      setReason("");
    }
  }, [authorizationQuery.data]);

  const replaceMutation = useMutation({
    mutationFn: () => authorizationApi.replaceUserRoles(selectedUserId, { roleIds: selectedRoleIds, rowVersion: authorizationQuery.data!.data.rowVersion, reason: reason.trim() }),
    onSuccess: async () => { await authorizationQuery.refetch(); await onSuccess("Đã thay thế role của người dùng và tăng phiên bản phân quyền.", selectedUserId); },
    onError,
  });
  const compatibleRoles = roles.filter((role) => role.accountType === selectedUser.accountType && role.status === "Active");

  return (
    <section className="grid gap-6 lg:grid-cols-[22rem_1fr]">
      <div className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
        <h2 className="mb-3 font-bold text-slate-900">Người dùng cùng trung tâm</h2>
        <label className="text-sm font-medium text-slate-700">Chọn người dùng<select value={selectedUserId} onChange={(event) => setSelectedUserId(event.target.value)} className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2">
          {users.map((option) => <option key={option.userId} value={option.userId}>{option.displayName} · {accountTypeLabels[option.accountType]}</option>)}
        </select></label>
        {(!canReadTeachers || !canReadStudents) && <p className="mt-3 text-xs text-amber-700">Danh sách chỉ gồm các nhóm người dùng bạn có quyền đọc.</p>}
      </div>
      <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
        <h2 className="text-xl font-bold text-slate-900">Role của {selectedUser.displayName}</h2>
        <p className="mt-1 text-sm text-slate-500">{selectedUser.username} · {accountTypeLabels[selectedUser.accountType]} · authorizationVersion {authorizationQuery.data?.data.authorizationVersion ?? "..."}</p>
        {authorizationQuery.isLoading ? <p className="mt-6 text-sm text-slate-500">Đang tải quyền người dùng...</p> : authorizationQuery.isError ? <p className="mt-6 text-sm text-red-700">Không thể tải quyền người dùng.</p> : (
          <div className="mt-6 space-y-3">
            {compatibleRoles.map((role) => <label key={role.roleId} className="flex items-start gap-3 rounded-lg border border-slate-200 p-3"><input type="checkbox" disabled={!canAssign} checked={selectedRoleIds.includes(role.roleId)} onChange={() => setSelectedRoleIds((current) => current.includes(role.roleId) ? current.filter((id) => id !== role.roleId) : [...current, role.roleId])} /><span><span className="block font-semibold text-slate-900">{role.roleName}</span><span className="text-xs text-slate-500">{role.roleCode} · {role.permissionCodes.length} permission</span></span></label>)}
            {compatibleRoles.length === 0 && <p className="text-sm text-slate-500">Không có role Active tương thích.</p>}
            {canAssign && <div className="border-t border-slate-200 pt-4"><label className="text-sm font-medium text-slate-700">Lý do thay đổi<input value={reason} onChange={(event) => setReason(event.target.value)} maxLength={1000} className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2" /></label><button type="button" disabled={!authorizationQuery.data || !reason.trim() || replaceMutation.isPending} onClick={() => replaceMutation.mutate()} className="mt-3 rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">Lưu role người dùng</button></div>}
          </div>
        )}
      </div>
    </section>
  );
};

const AuditPanel = ({ entries, loading, error }: { entries: Awaited<ReturnType<typeof authorizationApi.listAudit>>["data"]; loading: boolean; error: boolean }) => (
  <section className="overflow-hidden rounded-xl bg-white shadow-sm ring-1 ring-slate-200">
    <div className="border-b border-slate-200 p-5"><h2 className="text-xl font-bold text-slate-900">Nhật ký thay đổi phân quyền</h2><p className="text-sm text-slate-500">Tối đa 50 sự kiện mới nhất trong trung tâm hiện tại.</p></div>
    {loading ? <p className="p-6 text-sm text-slate-500">Đang tải audit...</p> : error ? <p className="p-6 text-sm text-red-700">Không thể tải nhật ký audit.</p> : (
      <div className="overflow-x-auto"><table className="min-w-full divide-y divide-slate-200 text-sm"><thead className="bg-slate-50"><tr><th className="px-4 py-3 text-left">Thời gian</th><th className="px-4 py-3 text-left">Hành động</th><th className="px-4 py-3 text-left">Đối tượng</th><th className="px-4 py-3 text-left">Lý do</th><th className="px-4 py-3 text-left">Trace</th></tr></thead><tbody className="divide-y divide-slate-100">{entries.map((entry) => <tr key={entry.authorizationAuditId}><td className="whitespace-nowrap px-4 py-3">{new Date(entry.createdAt).toLocaleString("vi-VN")}</td><td className="px-4 py-3 font-medium">{entry.actionType}</td><td className="px-4 py-3">{entry.targetType}: {entry.targetId}</td><td className="max-w-xs px-4 py-3">{entry.reason}</td><td className="max-w-xs truncate px-4 py-3 font-mono text-xs" title={entry.traceId}>{entry.traceId}</td></tr>)}</tbody></table>{entries.length === 0 && <p className="p-6 text-center text-slate-500">Chưa có sự kiện audit.</p>}</div>
    )}
  </section>
);
