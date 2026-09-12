import React, { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { platformApi } from "../api/platformApi";
import type {
  PlatformCenterListItem,
  CreatePlatformCenterRequest,
  UpdatePlatformCenterStatusRequest,
  ResetCenterManagerPasswordRequest,
} from "../types/platform";
import type { ProblemDetails } from "../types/auth";
import { useAuthStore } from "../stores/authStore";
import { permissions } from "../auth/permissions";

export const PlatformCentersPage: React.FC = () => {
  const queryClient = useQueryClient();
  const canManageCenters = useAuthStore((state) => state.hasPermission(permissions.platformCentersManage));
  const canManageManagers = useAuthStore((state) => state.hasPermission(permissions.platformManagersManage));

  const [page, setPage] = useState<number>(1);
  const pageSize = 20;
  const [search, setSearch] = useState<string>("");
  const [status, setStatus] = useState<string>("");

  const [searchInput, setSearchInput] = useState<string>("");
  const [statusInput, setStatusInput] = useState<string>("");

  // Modals state
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [statusModalCenter, setStatusModalCenter] = useState<PlatformCenterListItem | null>(null);
  const [statusReason, setStatusReason] = useState("");
  const [resetPasswordCenter, setResetPasswordCenter] = useState<PlatformCenterListItem | null>(null);

  // Create Form State
  const [newCenterCode, setNewCenterCode] = useState("");
  const [newCenterName, setNewCenterName] = useState("");
  const [newTimezone, setNewTimezone] = useState("Asia/Bangkok");
  const [newManagerUsername, setNewManagerUsername] = useState("");
  const [newManagerDisplayName, setNewManagerDisplayName] = useState("");
  const [newManagerPassword, setNewManagerPassword] = useState("");

  // Reset Password Form State
  const [newPassword, setNewPassword] = useState("");

  const [successMessage, setSuccessMessage] = useState("");
  const [errorMessage, setErrorMessage] = useState("");

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ["platform-centers", page, pageSize, search, status],
    queryFn: () => platformApi.listCenters({ page, pageSize, search, status }),
  });

  const createMutation = useMutation({
    mutationFn: (req: CreatePlatformCenterRequest) => platformApi.createCenter(req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["platform-centers"] });
      setIsCreateModalOpen(false);
      resetCreateForm();
      setSuccessMessage("Đã tạo trung tâm mới thành công.");
      setTimeout(() => setSuccessMessage(""), 5000);
    },
    onError: (error) => {
      if (isAxiosError<ProblemDetails>(error)) {
        const detail = error.response?.data?.detail;
        const code = error.response?.data?.errorCode;
        if (code === "DUPLICATE_RESOURCE") {
          setErrorMessage("Mã trung tâm đã tồn tại trong hệ thống.");
        } else if (code === "FORBIDDEN_RESOURCE") {
          setErrorMessage("Không được phép tạo trung tâm với mã PLATFORM.");
        } else {
          setErrorMessage(detail || "Không thể tạo trung tâm. Vui lòng kiểm tra lại thông tin.");
        }
      } else {
        setErrorMessage("Không thể tạo trung tâm. Vui lòng thử lại.");
      }
    },
  });

  const updateStatusMutation = useMutation({
    mutationFn: ({
      centerId,
      request,
    }: {
      centerId: string;
      request: UpdatePlatformCenterStatusRequest;
    }) => platformApi.updateCenterStatus(centerId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["platform-centers"] });
      setStatusModalCenter(null);
      setStatusReason("");
      setSuccessMessage("Đã cập nhật trạng thái trung tâm thành công.");
      setTimeout(() => setSuccessMessage(""), 5000);
    },
    onError: (error) => {
      if (isAxiosError<ProblemDetails>(error)) {
        const code = error.response?.data?.errorCode;
        if (code === "CONCURRENCY_CONFLICT" || error.response?.status === 409) {
          setErrorMessage("Dữ liệu trung tâm đã bị thay đổi bởi tác vụ khác. Vui lòng tải lại dữ liệu mới nhất.");
          refetch();
        } else {
          setErrorMessage(error.response?.data?.detail || "Không thể cập nhật trạng thái trung tâm.");
        }
      } else {
        setErrorMessage("Không thể cập nhật trạng thái. Vui lòng thử lại.");
      }
    },
  });

  const resetPasswordMutation = useMutation({
    mutationFn: ({
      centerId,
      managerUserId,
      request,
    }: {
      centerId: string;
      managerUserId: string;
      request: ResetCenterManagerPasswordRequest;
    }) => platformApi.resetCenterManagerPassword(centerId, managerUserId, request),
    onSuccess: (res) => {
      // Optimistically update the cached list item with the new row version if available
      queryClient.setQueriesData({ queryKey: ["platform-centers"] }, (old: any) => {
        if (!old || !old.items) return old;
        return {
          ...old,
          items: old.items.map((item: PlatformCenterListItem) =>
            item.centerId === resetPasswordCenter?.centerId
              ? { ...item, initialManagerUserRowVersion: res.newUserRowVersion }
              : item
          ),
        };
      });
      queryClient.invalidateQueries({ queryKey: ["platform-centers"] });
      setResetPasswordCenter(null);
      setNewPassword("");
      setSuccessMessage("Đã đặt lại mật khẩu cho tài khoản quản lý thành công. Tất cả phiên cũ đã bị thu hồi.");
      setTimeout(() => setSuccessMessage(""), 6000);
    },
    onError: (error) => {
      if (isAxiosError<ProblemDetails>(error)) {
        const code = error.response?.data?.errorCode;
        if (code === "CONCURRENCY_CONFLICT" || error.response?.status === 409) {
          setErrorMessage("Dữ liệu người dùng đã bị thay đổi bởi tác vụ khác. Vui lòng tải lại trang.");
          refetch();
        } else {
          setErrorMessage(error.response?.data?.detail || "Không thể đặt lại mật khẩu quản lý.");
        }
      } else {
        setErrorMessage("Không thể đặt lại mật khẩu. Vui lòng thử lại.");
      }
    },
  });

  const resetCreateForm = () => {
    setNewCenterCode("");
    setNewCenterName("");
    setNewTimezone("Asia/Bangkok");
    setNewManagerUsername("");
    setNewManagerDisplayName("");
    setNewManagerPassword("");
  };

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setSearch(searchInput);
    setStatus(statusInput);
    setPage(1);
  };

  const handleToggleStatusConfirm = () => {
    if (!statusModalCenter) return;
    const targetStatus = statusModalCenter.status === "Active" ? "Suspended" : "Active";
    updateStatusMutation.mutate({
      centerId: statusModalCenter.centerId,
      request: {
        status: targetStatus,
        rowVersion: statusModalCenter.rowVersion,
        reason: statusReason.trim() || undefined,
      },
    });
  };

  const handleResetPasswordConfirm = (e: React.FormEvent) => {
    e.preventDefault();
    if (!resetPasswordCenter || !resetPasswordCenter.initialManagerUserId) return;

    if (!resetPasswordCenter.initialManagerUserRowVersion) {
      setErrorMessage("Không có phiên bản dữ liệu quản lý hiện hành. Vui lòng tải lại danh sách.");
      refetch();
      return;
    }

    resetPasswordMutation.mutate({
      centerId: resetPasswordCenter.centerId,
      managerUserId: resetPasswordCenter.initialManagerUserId,
      request: {
        newPassword,
        expectedUserRowVersion: resetPasswordCenter.initialManagerUserRowVersion,
      },
    });
  };

  const centers = data?.items || [];
  const totalCount = data?.totalCount || 0;
  const totalPages = Math.ceil(totalCount / pageSize) || 1;

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      {/* Breadcrumb & Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 border-b border-gray-200 dark:border-gray-700 pb-5">
        <div>
          <nav className="text-sm font-medium text-gray-500 dark:text-gray-400 mb-1">
            <span className="hover:text-gray-700 dark:hover:text-gray-200">Quản trị Nền tảng</span>
            <span className="mx-2">/</span>
            <span className="text-blue-600 dark:text-blue-400">Danh sách Trung tâm Giáo dục</span>
          </nav>
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">
            Quản Lý Trung Tâm Đối Tác
          </h1>
        </div>
        {canManageCenters && (
          <button
            type="button"
            onClick={() => setIsCreateModalOpen(true)}
            className="inline-flex items-center px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-700 text-white font-medium text-sm shadow-sm transition-colors"
          >
            <svg className="w-5 h-5 mr-1.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
            Thêm trung tâm mới
          </button>
        )}
      </div>

      {/* Notifications */}
      {successMessage && (
        <div className="p-4 rounded-lg bg-green-50 dark:bg-green-900/30 text-green-800 dark:text-green-300 border border-green-200 dark:border-green-800 flex items-center justify-between">
          <span>{successMessage}</span>
          <button onClick={() => setSuccessMessage("")} className="text-green-600 dark:text-green-400 hover:underline">
            Đóng
          </button>
        </div>
      )}
      {errorMessage && (
        <div className="p-4 rounded-lg bg-red-50 dark:bg-red-900/30 text-red-800 dark:text-red-300 border border-red-200 dark:border-red-800 flex items-center justify-between">
          <span>{errorMessage}</span>
          <button onClick={() => setErrorMessage("")} className="text-red-600 dark:text-red-400 hover:underline">
            Đóng
          </button>
        </div>
      )}

      {/* Filter / Search Bar */}
      <form onSubmit={handleSearchSubmit} className="bg-white dark:bg-gray-800 p-4 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm flex flex-col md:flex-row gap-3 items-center">
        <div className="relative flex-1 w-full">
          <input
            type="text"
            placeholder="Tìm kiếm theo mã, tên trung tâm hoặc người quản lý..."
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            className="w-full pl-10 pr-4 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-gray-50 dark:bg-gray-700 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          <svg className="w-5 h-5 text-gray-400 absolute left-3 top-2.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>
        <select
          value={statusInput}
          onChange={(e) => setStatusInput(e.target.value)}
          className="w-full md:w-48 px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-gray-50 dark:bg-gray-700 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          <option value="">Tất cả trạng thái</option>
          <option value="Active">Đang hoạt động</option>
          <option value="Suspended">Tạm ngưng</option>
        </select>
        <button
          type="submit"
          className="w-full md:w-auto px-5 py-2 text-sm font-medium text-white bg-gray-800 dark:bg-gray-700 hover:bg-gray-900 dark:hover:bg-gray-600 rounded-lg transition-colors"
        >
          Tìm kiếm
        </button>
      </form>

      {/* Data Table or Empty State */}
      <div className="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
        {isLoading ? (
          <div className="p-12 text-center text-gray-500 dark:text-gray-400">
            <svg className="animate-spin h-8 w-8 mx-auto mb-3 text-blue-600" viewBox="0 0 24 24" fill="none">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"></path>
            </svg>
            Đang tải dữ liệu trung tâm...
          </div>
        ) : isError ? (
          <div className="p-12 text-center text-red-500">
            Có lỗi xảy ra khi tải danh sách trung tâm. Vui lòng tải lại trang.
          </div>
        ) : centers.length === 0 ? (
          /* Empty State */
          <div className="p-16 text-center">
            <div className="w-16 h-16 mx-auto mb-4 rounded-full bg-blue-50 dark:bg-blue-900/30 flex items-center justify-center text-blue-600 dark:text-blue-400">
              <svg className="w-8 h-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
              </svg>
            </div>
            <h3 className="text-lg font-medium text-gray-900 dark:text-white mb-1">
              Chưa có trung tâm giáo dục nào
            </h3>
            <p className="text-gray-500 dark:text-gray-400 text-sm max-w-sm mx-auto mb-6">
              Chưa có trung tâm giáo dục nào được khởi tạo trên nền tảng. Bắt đầu bằng việc tạo trung tâm đầu tiên.
            </p>
            {canManageCenters && (
              <button
                type="button"
                onClick={() => setIsCreateModalOpen(true)}
                className="inline-flex items-center px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-700 text-white font-medium text-sm shadow-sm transition-colors"
              >
                <svg className="w-5 h-5 mr-1.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                </svg>
                Tạo trung tâm đầu tiên
              </button>
            )}
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm text-gray-600 dark:text-gray-300">
              <thead className="bg-gray-50 dark:bg-gray-900/50 text-xs font-semibold text-gray-700 dark:text-gray-300 uppercase tracking-wider border-b border-gray-200 dark:border-gray-700">
                <tr>
                  <th className="px-6 py-3.5">Mã Trung Tâm</th>
                  <th className="px-6 py-3.5">Tên Trung Tâm</th>
                  <th className="px-6 py-3.5">Trạng Thái</th>
                  <th className="px-6 py-3.5">Múi Giờ</th>
                  <th className="px-6 py-3.5">Quản Lý Chính</th>
                  <th className="px-6 py-3.5">Ngày Tạo</th>
                  <th className="px-6 py-3.5 text-right">Thao Tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
                {centers.map((c) => (
                  <tr key={c.centerId} className="hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors">
                    <td className="px-6 py-4 font-mono font-semibold text-gray-900 dark:text-white">
                      {c.centerCode}
                    </td>
                    <td className="px-6 py-4 font-medium text-gray-900 dark:text-white">
                      {c.centerName}
                    </td>
                    <td className="px-6 py-4">
                      {c.status === "Active" ? (
                        <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300">
                          <span className="w-1.5 h-1.5 mr-1.5 rounded-full bg-green-600"></span>
                          Hoạt động
                        </span>
                      ) : (
                        <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300">
                          <span className="w-1.5 h-1.5 mr-1.5 rounded-full bg-amber-600"></span>
                          Tạm ngưng
                        </span>
                      )}
                    </td>
                    <td className="px-6 py-4 text-gray-500 dark:text-gray-400">
                      {c.timezone}
                    </td>
                    <td className="px-6 py-4">
                      {c.initialManagerUsername ? (
                        <div>
                          <div className="font-medium text-gray-900 dark:text-white">
                            {c.initialManagerDisplayName || c.initialManagerUsername}
                          </div>
                          <div className="text-xs text-gray-500 font-mono">
                            @{c.initialManagerUsername}
                          </div>
                        </div>
                      ) : (
                        <span className="text-gray-400 italic">Chưa xác định</span>
                      )}
                    </td>
                    <td className="px-6 py-4 text-gray-500 dark:text-gray-400 whitespace-nowrap">
                      {new Date(c.createdAt).toLocaleDateString("vi-VN", {
                        year: "numeric",
                        month: "2-digit",
                        day: "2-digit",
                      })}
                    </td>
                    <td className="px-6 py-4 text-right space-x-2 whitespace-nowrap">
                      {canManageCenters && (
                        <button
                          type="button"
                          onClick={() => {
                            setStatusModalCenter(c);
                            setStatusReason("");
                          }}
                          className="px-3 py-1 text-xs font-medium rounded-md border border-gray-300 dark:border-gray-600 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
                        >
                          {c.status === "Active" ? "Tạm ngưng" : "Kích hoạt"}
                        </button>
                      )}
                      {canManageManagers && c.initialManagerUserId && (
                        <button
                          type="button"
                          onClick={() => {
                            setResetPasswordCenter(c);
                            setNewPassword("");
                          }}
                          className="px-3 py-1 text-xs font-medium text-amber-700 dark:text-amber-400 rounded-md border border-amber-300 dark:border-amber-700 hover:bg-amber-50 dark:hover:bg-amber-900/30 transition-colors"
                        >
                          Đổi mật khẩu
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination */}
        {totalCount > pageSize && (
          <div className="px-6 py-4 border-t border-gray-200 dark:border-gray-700 flex items-center justify-between">
            <span className="text-sm text-gray-500 dark:text-gray-400">
              Trang {page} / {totalPages} (Tổng {totalCount} trung tâm)
            </span>
            <div className="flex gap-2">
              <button
                disabled={page <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                className="px-3 py-1 text-sm rounded border border-gray-300 dark:border-gray-600 disabled:opacity-50"
              >
                Trước
              </button>
              <button
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
                className="px-3 py-1 text-sm rounded border border-gray-300 dark:border-gray-600 disabled:opacity-50"
              >
                Sau
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Modal: Create Center */}
      {isCreateModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50">
          <div className="bg-white dark:bg-gray-800 rounded-2xl max-w-lg w-full p-6 shadow-xl border border-gray-200 dark:border-gray-700 space-y-4">
            <div className="flex justify-between items-center border-b border-gray-200 dark:border-gray-700 pb-3">
              <h3 className="text-lg font-bold text-gray-900 dark:text-white">
                Thêm Trung Tâm Giáo Dục Mới
              </h3>
              <button onClick={() => setIsCreateModalOpen(false)} className="text-gray-400 hover:text-gray-600">
                ✕
              </button>
            </div>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                createMutation.mutate({
                  centerCode: newCenterCode.trim().toUpperCase(),
                  centerName: newCenterName.trim(),
                  timezone: newTimezone,
                  initialManagerUsername: newManagerUsername.trim(),
                  initialManagerDisplayName: newManagerDisplayName.trim(),
                  initialManagerPassword: newManagerPassword,
                });
              }}
              className="space-y-3"
            >
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300 mb-1">
                    Mã Trung Tâm *
                  </label>
                  <input
                    type="text"
                    required
                    placeholder="VD: CENTER_A"
                    value={newCenterCode}
                    onChange={(e) => setNewCenterCode(e.target.value)}
                    className="w-full px-3 py-2 text-sm border rounded-lg dark:bg-gray-700 dark:border-gray-600 uppercase"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300 mb-1">
                    Múi Giờ
                  </label>
                  <select
                    value={newTimezone}
                    onChange={(e) => setNewTimezone(e.target.value)}
                    className="w-full px-3 py-2 text-sm border rounded-lg dark:bg-gray-700 dark:border-gray-600"
                  >
                    <option value="Asia/Bangkok">Asia/Bangkok (GMT+7)</option>
                    <option value="Asia/Ho_Chi_Minh">Asia/Ho_Chi_Minh (GMT+7)</option>
                    <option value="Asia/Singapore">Asia/Singapore (GMT+8)</option>
                  </select>
                </div>
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300 mb-1">
                  Tên Trung Tâm *
                </label>
                <input
                  type="text"
                  required
                  placeholder="VD: Trung tâm Giáo dục Thực nghiệm Ánh Dương"
                  value={newCenterName}
                  onChange={(e) => setNewCenterName(e.target.value)}
                  className="w-full px-3 py-2 text-sm border rounded-lg dark:bg-gray-700 dark:border-gray-600"
                />
              </div>

              <div className="border-t border-gray-200 dark:border-gray-700 pt-3">
                <h4 className="text-xs font-bold uppercase tracking-wider text-gray-500 mb-2">
                  Tài Khoản Quản Lý Ban Đầu
                </h4>
                <div className="space-y-3">
                  <div>
                    <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300 mb-1">
                      Tên Đăng Nhập Quản Lý *
                    </label>
                    <input
                      type="text"
                      required
                      placeholder="VD: manager_anhduong"
                      value={newManagerUsername}
                      onChange={(e) => setNewManagerUsername(e.target.value)}
                      className="w-full px-3 py-2 text-sm border rounded-lg dark:bg-gray-700 dark:border-gray-600"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300 mb-1">
                      Họ Và Tên Quản Lý *
                    </label>
                    <input
                      type="text"
                      required
                      placeholder="VD: Nguyễn Văn Quản Trị"
                      value={newManagerDisplayName}
                      onChange={(e) => setNewManagerDisplayName(e.target.value)}
                      className="w-full px-3 py-2 text-sm border rounded-lg dark:bg-gray-700 dark:border-gray-600"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300 mb-1">
                      Mật Khẩu Ban Đầu (tối thiểu 12 ký tự) *
                    </label>
                    <input
                      type="password"
                      required
                      minLength={12}
                      placeholder="••••••••••••"
                      value={newManagerPassword}
                      onChange={(e) => setNewManagerPassword(e.target.value)}
                      className="w-full px-3 py-2 text-sm border rounded-lg dark:bg-gray-700 dark:border-gray-600"
                    />
                  </div>
                </div>
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t border-gray-200 dark:border-gray-700">
                <button
                  type="button"
                  onClick={() => setIsCreateModalOpen(false)}
                  className="px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-100 rounded-lg"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={createMutation.isPending}
                  className="px-4 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg disabled:opacity-50"
                >
                  {createMutation.isPending ? "Đang tạo..." : "Xác nhận tạo"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: Status Transition */}
      {statusModalCenter && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50">
          <div className="bg-white dark:bg-gray-800 rounded-2xl max-w-md w-full p-6 shadow-xl border border-gray-200 dark:border-gray-700 space-y-4">
            <h3 className="text-lg font-bold text-gray-900 dark:text-white">
              Xác Nhận Đổi Trạng Thái
            </h3>
            <p className="text-sm text-gray-600 dark:text-gray-300">
              Bạn có chắc chắn muốn chuyển trạng thái trung tâm{" "}
              <strong>{statusModalCenter.centerName}</strong> ({statusModalCenter.centerCode}) từ{" "}
              <span className="font-semibold">{statusModalCenter.status}</span> sang{" "}
              <span className="font-semibold text-blue-600">
                {statusModalCenter.status === "Active" ? "Tạm ngưng (Suspended)" : "Kích hoạt (Active)"}
              </span>
              ?
            </p>
            <div>
              <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300 mb-1">
                Lý do thay đổi (tùy chọn)
              </label>
              <textarea
                value={statusReason}
                onChange={(e) => setStatusReason(e.target.value)}
                placeholder="Nhập lý do thay đổi trạng thái..."
                className="w-full px-3 py-2 text-sm border rounded-lg dark:bg-gray-700 dark:border-gray-600"
                rows={2}
              />
            </div>
            <div className="flex justify-end gap-3 pt-3 border-t border-gray-200 dark:border-gray-700">
              <button
                type="button"
                onClick={() => setStatusModalCenter(null)}
                className="px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-100 rounded-lg"
              >
                Hủy
              </button>
              <button
                type="button"
                onClick={handleToggleStatusConfirm}
                disabled={updateStatusMutation.isPending}
                className="px-4 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg disabled:opacity-50"
              >
                {updateStatusMutation.isPending ? "Đang cập nhật..." : "Xác nhận"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Reset Password */}
      {resetPasswordCenter && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50">
          <div className="bg-white dark:bg-gray-800 rounded-2xl max-w-md w-full p-6 shadow-xl border border-gray-200 dark:border-gray-700 space-y-4">
            <h3 className="text-lg font-bold text-gray-900 dark:text-white">
              Đặt Lại Mật Khẩu Quản Lý
            </h3>
            <p className="text-sm text-gray-600 dark:text-gray-300">
              Đặt lại mật khẩu cho tài khoản quản trị <strong>@{resetPasswordCenter.initialManagerUsername}</strong>{" "}
              thuộc trung tâm <strong>{resetPasswordCenter.centerName}</strong>.
            </p>
            <div className="p-3 rounded-lg bg-amber-50 dark:bg-amber-900/30 text-amber-800 dark:text-amber-300 text-xs border border-amber-200 dark:border-amber-800">
              ⚠️ <strong>Cảnh báo bảo mật:</strong> Sau khi đặt lại mật khẩu thành công, toàn bộ phiên đăng nhập (JWT và Refresh Token) của người dùng này sẽ lập tức bị thu hồi và buộc phải đăng nhập lại bằng mật khẩu mới.
            </div>
            <form onSubmit={handleResetPasswordConfirm} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-gray-700 dark:text-gray-300 mb-1">
                  Mật Khẩu Mới (tối thiểu 12 ký tự) *
                </label>
                <input
                  type="password"
                  required
                  minLength={12}
                  placeholder="Nhập mật khẩu mới (tối thiểu 12 ký tự)..."
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  className="w-full px-3 py-2 text-sm border rounded-lg dark:bg-gray-700 dark:border-gray-600"
                />
              </div>
              <div className="flex justify-end gap-3 pt-3 border-t border-gray-200 dark:border-gray-700">
                <button
                  type="button"
                  onClick={() => setResetPasswordCenter(null)}
                  className="px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-100 rounded-lg"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={resetPasswordMutation.isPending}
                  className="px-4 py-2 text-sm font-medium text-white bg-amber-600 hover:bg-amber-700 rounded-lg disabled:opacity-50"
                >
                  {resetPasswordMutation.isPending ? "Đang xử lý..." : "Đặt lại mật khẩu"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
