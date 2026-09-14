import React, { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { organizationApi } from "../api/organizationApi";
import type { UserStatus } from "../types/auth";
import type {
  TeacherListParams,
  CreateTeacherRequest,
  UpdateTeacherRequest,
  ResetAccountPasswordRequest,
  TeacherDto,
} from "../types/organization";
import { useAuthStore } from "../stores/authStore";
import { permissions } from "../auth/permissions";
import { extractProblemDetails, isConcurrencyConflict } from "../utils/problemDetails";

const STATUS_LABELS: Record<string, string> = {
  Active: "Hoạt động",
  Locked: "Bị khóa",
  Disabled: "Vô hiệu hóa",
};

export const TeacherListPage: React.FC = () => {
  const queryClient = useQueryClient();
  const hasPermission = useAuthStore((state) => state.hasPermission);

  const canCreateTeacher = hasPermission(permissions.teachersCreate);
  const canUpdateTeacher = hasPermission(permissions.teachersUpdate);
  const canDeleteTeacher = hasPermission(permissions.teachersDelete);
  const canResetPassword = hasPermission(permissions.teachersResetPassword);

  const [page, setPage] = useState<number>(1);
  const pageSize = 20;
  const [search, setSearch] = useState<string>("");
  const [status, setStatus] = useState<UserStatus | "">("");

  // Search input state
  const [searchInput, setSearchInput] = useState<string>("");
  const [statusInput, setStatusInput] = useState<UserStatus | "">("");

  // Create modal state
  const [isCreating, setIsCreating] = useState(false);
  const [createUsername, setCreateUsername] = useState("");
  const [createPassword, setCreatePassword] = useState("");
  const [createDisplayName, setCreateDisplayName] = useState("");
  const [createDepartment, setCreateDepartment] = useState("");

  // Edit modal state
  const [editingTeacher, setEditingTeacher] = useState<TeacherDto | null>(null);
  const [editDisplayName, setEditDisplayName] = useState("");
  const [editDepartment, setEditDepartment] = useState("");
  const [editStatus, setEditStatus] = useState<UserStatus>("Active");

  // Reset password modal state
  const [resetTeacher, setResetTeacher] = useState<TeacherDto | null>(null);
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [resetReason, setResetReason] = useState("");

  // Delete modal state
  const [deletingTeacher, setDeletingTeacher] = useState<TeacherDto | null>(null);

  // Notifications
  const [feedback, setFeedback] = useState<{ type: "success" | "error" | "conflict"; message: string } | null>(null);

  const showFeedback = (type: "success" | "error" | "conflict", message: string) => {
    setFeedback({ type, message });
    if (type === "success") {
      setTimeout(() => setFeedback(null), 5000);
    }
  };

  const queryParams: TeacherListParams = {
    page,
    pageSize,
    search: search.trim() !== "" ? search.trim() : undefined,
    status: status !== "" ? status : undefined,
  };

  const { data, isLoading, isFetching, isError: isListError, refetch } = useQuery({
    queryKey: ["teachers", queryParams.page, queryParams.pageSize, queryParams.search, queryParams.status],
    queryFn: () => organizationApi.listTeachers(queryParams),
  });

  // Create mutation
  const createMutation = useMutation({
    mutationFn: async (request: CreateTeacherRequest) => {
      try {
        return await organizationApi.createTeacher(request);
      } finally {
        request.temporaryPassword = "";
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["teachers"] });
      setIsCreating(false);
      setCreateUsername("");
      setCreatePassword("");
      setCreateDisplayName("");
      setCreateDepartment("");
      showFeedback("success", "Đã tạo tài khoản giáo viên thành công.");
    },
    onError: (error) => {
      const details = extractProblemDetails(error);
      if (details.errorCode === "DUPLICATE_RESOURCE") {
        showFeedback("error", "Tên đăng nhập đã tồn tại trong trung tâm. Vui lòng chọn tên khác.");
      } else if (details.errorCode === "VALIDATION_FAILED") {
        showFeedback("error", details.message || "Dữ liệu giáo viên không hợp lệ.");
      } else {
        showFeedback("error", details.message || "Không thể tạo giáo viên. Vui lòng thử lại.");
      }
    },
  });

  // Update mutation
  const updateMutation = useMutation({
    mutationFn: async ({ teacherId, request }: { teacherId: string; request: UpdateTeacherRequest }) => {
      return await organizationApi.updateTeacher(teacherId, request);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["teachers"] });
      setEditingTeacher(null);
      showFeedback("success", "Đã cập nhật thông tin giáo viên thành công.");
    },
    onError: (error) => {
      if (isConcurrencyConflict(error)) {
        showFeedback("conflict", "Dữ liệu giáo viên đã bị thay đổi bởi một phiên làm việc khác. Đang tải lại dữ liệu mới nhất...");
        refetch();
      } else {
        const details = extractProblemDetails(error);
        showFeedback("error", details.message || "Không thể cập nhật giáo viên.");
      }
    },
  });

  // Reset password mutation
  const resetPasswordMutation = useMutation({
    mutationFn: async ({ teacherId, request }: { teacherId: string; request: ResetAccountPasswordRequest }) => {
      try {
        return await organizationApi.resetTeacherPassword(teacherId, request);
      } finally {
        request.newPassword = "";
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["teachers"] });
      setResetTeacher(null);
      setNewPassword("");
      setConfirmPassword("");
      setResetReason("");
      showFeedback("success", "Đã đặt lại mật khẩu giáo viên thành công. Mọi phiên đăng nhập cũ đã được thu hồi.");
    },
    onError: (error) => {
      if (isConcurrencyConflict(error)) {
        showFeedback("conflict", "Dữ liệu người dùng đã bị thay đổi bởi một phiên làm việc khác. Vui lòng tải lại.");
        refetch();
      } else {
        const details = extractProblemDetails(error);
        showFeedback("error", details.message || "Không thể đặt lại mật khẩu.");
      }
    },
  });

  // Delete mutation
  const deleteMutation = useMutation({
    mutationFn: async (teacherId: string) => {
      return await organizationApi.deleteTeacher(teacherId);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["teachers"] });
      setDeletingTeacher(null);
      showFeedback("success", "Đã xóa giáo viên thành công. Dữ liệu lịch sử được bảo toàn an toàn.");
    },
    onError: (error) => {
      const details = extractProblemDetails(error);
      if (details.errorCode === "INVALID_STATE_TRANSITION" || details.status === 409) {
        showFeedback("error", "Không thể xóa: Giáo viên hiện vẫn còn lớp học đang hoạt động.");
      } else {
        showFeedback("error", details.message || "Không thể xóa giáo viên. Vui lòng thử lại.");
      }
    },
  });

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setSearch(searchInput);
    setStatus(statusInput);
    setPage(1);
  };

  const openEditModal = (teacher: TeacherDto) => {
    setEditingTeacher(teacher);
    setEditDisplayName(teacher.displayName);
    setEditDepartment(teacher.department || "");
    setEditStatus(teacher.status);
    setFeedback(null);
  };

  const handleEditSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingTeacher) return;

    const trimmedName = editDisplayName.trim();
    if (!trimmedName) {
      showFeedback("error", "Họ tên giáo viên không được để trống.");
      return;
    }

    updateMutation.mutate({
      teacherId: editingTeacher.teacherId,
      request: {
        displayName: trimmedName,
        department: editDepartment.trim() || null,
        status: editStatus,
        rowVersion: editingTeacher.rowVersion,
      },
    });
  };

  const openResetPasswordModal = (teacher: TeacherDto) => {
    setResetTeacher(teacher);
    setNewPassword("");
    setConfirmPassword("");
    setResetReason("");
    setFeedback(null);
  };

  const handleResetPasswordSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!resetTeacher) return;

    if (newPassword.length < 12 || newPassword.length > 200) {
      showFeedback("error", "Mật khẩu mới phải từ 12 đến 200 ký tự.");
      return;
    }

    if (newPassword !== confirmPassword) {
      showFeedback("error", "Mật khẩu xác nhận không khớp.");
      return;
    }

    if (resetReason.trim().length < 5 || resetReason.trim().length > 500) {
      showFeedback("error", "Lý do đặt lại mật khẩu là bắt buộc (từ 5 đến 500 ký tự).");
      return;
    }

    resetPasswordMutation.mutate({
      teacherId: resetTeacher.teacherId,
      request: {
        newPassword,
        expectedUserRowVersion: resetTeacher.rowVersion,
        reason: resetReason.trim(),
      },
    });
  };

  const openDeleteModal = (teacher: TeacherDto) => {
    setDeletingTeacher(teacher);
    setFeedback(null);
  };

  const handleDeleteSubmit = () => {
    if (!deletingTeacher) return;
    deleteMutation.mutate(deletingTeacher.teacherId);
  };

  return (
    <div className="min-h-screen bg-gray-50 py-8">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="mb-6 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold leading-7 text-gray-900 sm:truncate sm:text-3xl sm:tracking-tight">
              Quản lý Giáo viên
            </h1>
            <p className="mt-1 text-sm text-gray-500">
              Quản lý tài khoản, phân quyền giảng dạy và bảo mật cho giáo viên thuộc trung tâm.
            </p>
          </div>
          <div className="flex gap-3">
            {canCreateTeacher && (
              <button
                type="button"
                id="btn-create-teacher"
                onClick={() => {
                  setIsCreating(true);
                  setFeedback(null);
                }}
                className="inline-flex items-center rounded-md bg-indigo-600 px-3.5 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600"
              >
                + Thêm giáo viên
              </button>
            )}
            <Link
              to="/"
              className="inline-flex items-center rounded-md bg-white px-3.5 py-2 text-sm font-semibold text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50"
            >
              Về trang chủ
            </Link>
          </div>
        </div>

        {/* Global Feedback Banner */}
        {feedback && (
          <div
            id="teacher-feedback-alert"
            role="alert"
            className={`mb-6 rounded-md p-4 flex items-start justify-between ${
              feedback.type === "success"
                ? "bg-green-50 text-green-800 border border-green-200"
                : feedback.type === "conflict"
                ? "bg-amber-50 text-amber-800 border border-amber-200"
                : "bg-red-50 text-red-800 border border-red-200"
            }`}
          >
            <div className="text-sm font-medium">{feedback.message}</div>
            <button
              type="button"
              onClick={() => setFeedback(null)}
              className="ml-4 text-gray-400 hover:text-gray-600"
              aria-label="Đóng thông báo"
            >
              ×
            </button>
          </div>
        )}

        {/* Create Modal */}
        {isCreating && canCreateTeacher && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black bg-opacity-50 flex items-center justify-center p-4" role="dialog" aria-modal="true" aria-labelledby="modal-create-teacher-title">
            <div className="bg-white rounded-lg shadow-xl max-w-lg w-full p-6">
              <h2 id="modal-create-teacher-title" className="text-lg font-bold text-gray-900 mb-4">
                Thêm giáo viên mới
              </h2>
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  createMutation.mutate({
                    username: createUsername.trim(),
                    temporaryPassword: createPassword,
                    displayName: createDisplayName.trim(),
                    department: createDepartment.trim() || undefined,
                  });
                }}
                className="space-y-4"
              >
                <div>
                  <label htmlFor="create-username" className="block text-sm font-medium text-gray-700">
                    Tên đăng nhập <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    id="create-username"
                    value={createUsername}
                    onChange={(e) => setCreateUsername(e.target.value)}
                    required
                    maxLength={100}
                    disabled={createMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>

                <div>
                  <label htmlFor="create-password" className="block text-sm font-medium text-gray-700">
                    Mật khẩu tạm thời (tối thiểu 12 ký tự) <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="password"
                    id="create-password"
                    autoComplete="new-password"
                    value={createPassword}
                    onChange={(e) => setCreatePassword(e.target.value)}
                    required
                    minLength={12}
                    maxLength={200}
                    disabled={createMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>

                <div>
                  <label htmlFor="create-display-name" className="block text-sm font-medium text-gray-700">
                    Họ tên giáo viên <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    id="create-display-name"
                    value={createDisplayName}
                    onChange={(e) => setCreateDisplayName(e.target.value)}
                    required
                    maxLength={200}
                    disabled={createMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>

                <div>
                  <label htmlFor="create-department" className="block text-sm font-medium text-gray-700">
                    Bộ môn / Phòng ban
                  </label>
                  <input
                    type="text"
                    id="create-department"
                    value={createDepartment}
                    onChange={(e) => setCreateDepartment(e.target.value)}
                    maxLength={150}
                    disabled={createMutation.isPending}
                    placeholder="Ví dụ: Tổ Toán - Tin"
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>

                <div className="flex justify-end gap-3 pt-4 border-t border-gray-200">
                  <button
                    type="button"
                    onClick={() => setIsCreating(false)}
                    disabled={createMutation.isPending}
                    className="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 shadow-sm hover:bg-gray-50"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-submit-create-teacher"
                    disabled={createMutation.isPending}
                    className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
                  >
                    {createMutation.isPending ? "Đang tạo..." : "Tạo giáo viên"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* Edit Teacher Modal */}
        {editingTeacher && canUpdateTeacher && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black bg-opacity-50 flex items-center justify-center p-4" role="dialog" aria-modal="true" aria-labelledby="modal-edit-teacher-title">
            <div className="bg-white rounded-lg shadow-xl max-w-lg w-full p-6">
              <h2 id="modal-edit-teacher-title" className="text-lg font-bold text-gray-900 mb-2">
                Chỉnh sửa thông tin giáo viên
              </h2>
              <p className="text-xs text-gray-500 mb-4">
                Tài khoản: <span className="font-semibold text-gray-700">{editingTeacher.username}</span>
              </p>

              <form onSubmit={handleEditSubmit} className="space-y-4">
                <div>
                  <label htmlFor="edit-display-name" className="block text-sm font-medium text-gray-700">
                    Họ tên <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    id="edit-display-name"
                    value={editDisplayName}
                    onChange={(e) => setEditDisplayName(e.target.value)}
                    required
                    maxLength={200}
                    disabled={updateMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>

                <div>
                  <label htmlFor="edit-department" className="block text-sm font-medium text-gray-700">
                    Bộ môn / Phòng ban
                  </label>
                  <input
                    type="text"
                    id="edit-department"
                    value={editDepartment}
                    onChange={(e) => setEditDepartment(e.target.value)}
                    maxLength={150}
                    disabled={updateMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>

                <div>
                  <label htmlFor="edit-status" className="block text-sm font-medium text-gray-700">
                    Trạng thái tài khoản
                  </label>
                  <select
                    id="edit-status"
                    value={editStatus}
                    onChange={(e) => setEditStatus(e.target.value as UserStatus)}
                    disabled={updateMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  >
                    <option value="Active">Hoạt động</option>
                    <option value="Locked">Bị khóa</option>
                    <option value="Disabled">Vô hiệu hóa</option>
                  </select>
                </div>

                <div className="flex justify-end gap-3 pt-4 border-t border-gray-200">
                  <button
                    type="button"
                    onClick={() => setEditingTeacher(null)}
                    disabled={updateMutation.isPending}
                    className="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 shadow-sm hover:bg-gray-50"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-save-edit-teacher"
                    disabled={updateMutation.isPending}
                    className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
                  >
                    {updateMutation.isPending ? "Đang lưu..." : "Lưu thay đổi"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* Reset Password Modal */}
        {resetTeacher && canResetPassword && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black bg-opacity-50 flex items-center justify-center p-4" role="dialog" aria-modal="true" aria-labelledby="modal-reset-teacher-password-title">
            <div className="bg-white rounded-lg shadow-xl max-w-lg w-full p-6">
              <h2 id="modal-reset-teacher-password-title" className="text-lg font-bold text-gray-900 mb-2">
                Đặt lại mật khẩu giáo viên
              </h2>
              <p className="text-sm text-gray-600 mb-4">
                Giáo viên: <span className="font-semibold text-gray-900">{resetTeacher.displayName}</span> ({resetTeacher.username})
              </p>

              <div className="rounded-md bg-amber-50 p-3 mb-4 text-xs text-amber-800 border border-amber-200">
                Lưu ý an toàn: Đặt lại mật khẩu sẽ lập tức thu hồi mọi phiên đăng nhập và refresh token đang hoạt động của giáo viên.
              </div>

              <form onSubmit={handleResetPasswordSubmit} className="space-y-4">
                <div>
                  <label htmlFor="reset-new-password" className="block text-sm font-medium text-gray-700">
                    Mật khẩu mới (tối thiểu 12 ký tự) <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="password"
                    id="reset-new-password"
                    autoComplete="new-password"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    required
                    minLength={12}
                    maxLength={200}
                    disabled={resetPasswordMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>

                <div>
                  <label htmlFor="reset-confirm-password" className="block text-sm font-medium text-gray-700">
                    Xác nhận mật khẩu mới <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="password"
                    id="reset-confirm-password"
                    autoComplete="new-password"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    required
                    minLength={12}
                    maxLength={200}
                    disabled={resetPasswordMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>

                <div>
                  <label htmlFor="reset-reason" className="block text-sm font-medium text-gray-700">
                    Lý do đặt lại mật khẩu <span className="text-red-500">*</span>
                  </label>
                  <textarea
                    id="reset-reason"
                    rows={3}
                    value={resetReason}
                    onChange={(e) => setResetReason(e.target.value)}
                    required
                    minLength={5}
                    maxLength={500}
                    placeholder="Ví dụ: Giáo viên yêu cầu cấp lại mật khẩu do quên mật khẩu"
                    disabled={resetPasswordMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>

                <div className="flex justify-end gap-3 pt-4 border-t border-gray-200">
                  <button
                    type="button"
                    onClick={() => setResetTeacher(null)}
                    disabled={resetPasswordMutation.isPending}
                    className="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 shadow-sm hover:bg-gray-50"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-submit-reset-teacher-password"
                    disabled={resetPasswordMutation.isPending}
                    className="rounded-md bg-amber-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-amber-500 disabled:opacity-50"
                  >
                    {resetPasswordMutation.isPending ? "Đang xử lý..." : "Xác nhận đặt lại mật khẩu"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* Delete Confirmation Modal */}
        {deletingTeacher && canDeleteTeacher && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black bg-opacity-50 flex items-center justify-center p-4" role="dialog" aria-modal="true" aria-labelledby="modal-delete-teacher-title">
            <div className="bg-white rounded-lg shadow-xl max-w-md w-full p-6">
              <h2 id="modal-delete-teacher-title" className="text-lg font-bold text-gray-900 mb-2">
                Xóa giáo viên
              </h2>

              {deletingTeacher.classCount > 0 ? (
                <div>
                  <div className="rounded-md bg-red-50 p-4 mb-4 text-sm text-red-800 border border-red-200">
                    <p className="font-semibold mb-1">Không thể xóa giáo viên này!</p>
                    <p>
                      Giáo viên <span className="font-bold">{deletingTeacher.displayName}</span> hiện đang phụ trách{" "}
                      <span className="font-bold">{deletingTeacher.classCount}</span> lớp học đang hoạt động.
                    </p>
                    <p className="mt-2 text-xs text-red-700">
                      Vui lòng chuyển giao người phụ trách hoặc đóng các lớp học tương ứng trước khi thực hiện xóa.
                    </p>
                  </div>
                  <div className="flex justify-end">
                    <button
                      type="button"
                      id="btn-close-delete-blocked"
                      onClick={() => setDeletingTeacher(null)}
                      className="rounded-md bg-gray-200 px-4 py-2 text-sm font-medium text-gray-800 hover:bg-gray-300"
                    >
                      Đã hiểu
                    </button>
                  </div>
                </div>
              ) : (
                <div>
                  <p className="text-sm text-gray-600 mb-4">
                    Bạn có chắc chắn muốn xóa giáo viên <span className="font-semibold text-gray-900">{deletingTeacher.displayName}</span> ({deletingTeacher.username})?
                  </p>
                  <div className="rounded-md bg-blue-50 p-3 mb-4 text-xs text-blue-800 border border-blue-200">
                    Chính sách bảo toàn dữ liệu: Tài khoản giáo viên sẽ được chuyển sang trạng thái đã xóa. Lịch sử phân quyền và hoạt động học thuật vẫn được lưu trữ toàn vẹn.
                  </div>
                  <div className="flex justify-end gap-3">
                    <button
                      type="button"
                      onClick={() => setDeletingTeacher(null)}
                      disabled={deleteMutation.isPending}
                      className="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 shadow-sm hover:bg-gray-50"
                    >
                      Hủy
                    </button>
                    <button
                      type="button"
                      id="btn-confirm-delete-teacher"
                      onClick={handleDeleteSubmit}
                      disabled={deleteMutation.isPending}
                      className="rounded-md bg-red-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-red-500 disabled:opacity-50"
                    >
                      {deleteMutation.isPending ? "Đang xóa..." : "Xác nhận xóa"}
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>
        )}

        {/* Filter Card */}
        <div className="mb-8 overflow-hidden rounded-lg bg-white shadow">
          <div className="p-6">
            <form onSubmit={handleSearch} className="flex flex-col gap-4 sm:flex-row sm:items-end">
              <div className="w-full sm:max-w-xs">
                <label htmlFor="search-teacher" className="block text-sm font-medium leading-6 text-gray-900">
                  Tìm kiếm
                </label>
                <div className="mt-1">
                  <input
                    type="text"
                    id="search-teacher"
                    value={searchInput}
                    onChange={(e) => setSearchInput(e.target.value)}
                    placeholder="Tên đăng nhập hoặc họ tên"
                    className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-gray-900 shadow-sm focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 sm:text-sm"
                  />
                </div>
              </div>

              <div className="w-full sm:max-w-xs">
                <label htmlFor="status-teacher" className="block text-sm font-medium leading-6 text-gray-900">
                  Trạng thái
                </label>
                <div className="mt-1">
                  <select
                    id="status-teacher"
                    value={statusInput}
                    onChange={(e) => setStatusInput(e.target.value as UserStatus | "")}
                    className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-gray-900 shadow-sm focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 sm:text-sm"
                  >
                    <option value="">Tất cả</option>
                    <option value="Active">Hoạt động</option>
                    <option value="Locked">Bị khóa</option>
                    <option value="Disabled">Vô hiệu hóa</option>
                  </select>
                </div>
              </div>

              <div>
                <button
                  type="submit"
                  id="btn-search-teachers"
                  disabled={isFetching}
                  className="inline-flex w-full items-center justify-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-indigo-600 disabled:opacity-50 sm:w-auto"
                >
                  Tìm kiếm
                </button>
              </div>
            </form>
          </div>
        </div>

        {/* Error Alert */}
        {isListError && (
          <div className="mb-6 rounded-md bg-red-50 p-4 border border-red-200" role="alert">
            <h3 className="text-sm font-medium text-red-800">Không thể tải danh sách giáo viên</h3>
            <p className="mt-1 text-sm text-red-700">Vui lòng thử lại sau hoặc làm mới trang.</p>
          </div>
        )}

        {/* Teachers Table */}
        <div className="overflow-hidden bg-white shadow sm:rounded-lg">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-300">
              <thead className="bg-gray-50">
                <tr>
                  <th scope="col" className="py-3.5 pl-4 pr-3 text-left text-sm font-semibold text-gray-900 sm:pl-6">
                    Tên đăng nhập
                  </th>
                  <th scope="col" className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">
                    Họ tên
                  </th>
                  <th scope="col" className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">
                    Bộ môn
                  </th>
                  <th scope="col" className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">
                    Trạng thái
                  </th>
                  <th scope="col" className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">
                    Số lớp
                  </th>
                  <th scope="col" className="relative py-3.5 pl-3 pr-4 sm:pr-6 text-right text-sm font-semibold text-gray-900">
                    Thao tác
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 bg-white">
                {isLoading ? (
                  <tr>
                    <td colSpan={6} className="py-10 text-center text-sm text-gray-500">
                      Đang tải danh sách giáo viên...
                    </td>
                  </tr>
                ) : !data || data.data.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="py-10 text-center text-sm text-gray-500">
                      Không tìm thấy giáo viên nào phù hợp.
                    </td>
                  </tr>
                ) : (
                  data.data.map((teacher) => (
                    <tr key={teacher.teacherId} className="hover:bg-gray-50">
                      <td className="whitespace-nowrap py-4 pl-4 pr-3 text-sm font-medium text-gray-900 sm:pl-6">
                        {teacher.username}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-700">
                        {teacher.displayName}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                        {teacher.department || "-"}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm">
                        <span
                          className={`inline-flex items-center rounded-md px-2 py-1 text-xs font-medium ring-1 ring-inset ${
                            teacher.status === "Active"
                              ? "bg-green-50 text-green-700 ring-green-600/20"
                              : teacher.status === "Locked"
                              ? "bg-yellow-50 text-yellow-800 ring-yellow-600/20"
                              : "bg-red-50 text-red-700 ring-red-600/10"
                          }`}
                        >
                          {STATUS_LABELS[teacher.status] || teacher.status}
                        </span>
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                        {teacher.classCount}
                      </td>
                      <td className="whitespace-nowrap py-4 pl-3 pr-4 sm:pr-6 text-right text-sm font-medium space-x-2">
                        {canUpdateTeacher && (
                          <button
                            type="button"
                            id={`btn-edit-teacher-${teacher.teacherId}`}
                            onClick={() => openEditModal(teacher)}
                            className="text-indigo-600 hover:text-indigo-900 text-xs font-semibold px-2 py-1 rounded hover:bg-indigo-50"
                          >
                            Sửa
                          </button>
                        )}
                        {canResetPassword && (
                          <button
                            type="button"
                            id={`btn-reset-password-${teacher.teacherId}`}
                            onClick={() => openResetPasswordModal(teacher)}
                            className="text-amber-600 hover:text-amber-900 text-xs font-semibold px-2 py-1 rounded hover:bg-amber-50"
                          >
                            Đổi mật khẩu
                          </button>
                        )}
                        {canDeleteTeacher && (
                          <button
                            type="button"
                            id={`btn-delete-teacher-${teacher.teacherId}`}
                            onClick={() => openDeleteModal(teacher)}
                            className="text-red-600 hover:text-red-900 text-xs font-semibold px-2 py-1 rounded hover:bg-red-50"
                          >
                            Xóa
                          </button>
                        )}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {data?.meta && data.meta.totalPages > 1 && (
            <div className="flex items-center justify-between border-t border-gray-200 bg-white px-4 py-3 sm:px-6">
              <div className="hidden sm:flex sm:flex-1 sm:items-center sm:justify-between">
                <div>
                  <p className="text-sm text-gray-700">
                    Trang <span className="font-medium">{data.meta.page}</span> /{" "}
                    <span className="font-medium">{data.meta.totalPages}</span> (Tổng cộng{" "}
                    <span className="font-medium">{data.meta.totalItems}</span> giáo viên)
                  </p>
                </div>
                <div>
                  <nav className="isolate inline-flex -space-x-px rounded-md shadow-sm" aria-label="Phân trang">
                    <button
                      type="button"
                      id="btn-prev-page"
                      onClick={() => setPage((p) => Math.max(1, p - 1))}
                      disabled={page === 1 || isFetching}
                      className="relative inline-flex items-center rounded-l-md px-2 py-2 text-gray-400 ring-1 ring-inset ring-gray-300 hover:bg-gray-50 disabled:opacity-50"
                    >
                      ‹
                    </button>
                    <button
                      type="button"
                      id="btn-next-page"
                      onClick={() => setPage((p) => Math.min(data.meta.totalPages, p + 1))}
                      disabled={page === data.meta.totalPages || isFetching}
                      className="relative inline-flex items-center rounded-r-md px-2 py-2 text-gray-400 ring-1 ring-inset ring-gray-300 hover:bg-gray-50 disabled:opacity-50"
                    >
                      ›
                    </button>
                  </nav>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
