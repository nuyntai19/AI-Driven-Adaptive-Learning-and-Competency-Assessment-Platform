import React, { useState, useRef, useCallback } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { platformApi } from "../api/platformApi";
import type {
  PlatformCenterListItem,
  PlatformCenterManagerListItem,
  CreateCenterManagerRequest,
  UpdateCenterManagerStatusRequest,
  MakePrimaryCenterManagerRequest,
  ResetCenterManagerPasswordRequest,
} from "../types/platform";
import type { ProblemDetails } from "../types/auth";
import { useModalAccessibility } from "../utils/useModalAccessibility";
import { normalizeToAscii } from "../pages/PlatformCentersPage";

interface CenterManagersModalProps {
  isOpen: boolean;
  onClose: () => void;
  center: PlatformCenterListItem;
  onCenterUpdated: () => void;
}

export const CenterManagersModal: React.FC<CenterManagersModalProps> = ({
  isOpen,
  onClose,
  center,
  onCenterUpdated,
}) => {
  const queryClient = useQueryClient();
  const [activeTab, setActiveTab] = useState<"list" | "create">("list");
  const [page, setPage] = useState(1);
  const pageSize = 10;
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");

  // Sub-modals inside Manager modal
  const [statusTargetUser, setStatusTargetUser] = useState<PlatformCenterManagerListItem | null>(null);
  const [newStatus, setNewStatus] = useState<string>("Active");
  const [statusReason, setStatusReason] = useState<string>("");

  const [primaryTargetUser, setPrimaryTargetUser] = useState<PlatformCenterManagerListItem | null>(null);
  const [disablePreviousPrimary, setDisablePreviousPrimary] = useState(false);
  const [primaryReason, setPrimaryReason] = useState<string>("");

  const [passwordTargetUser, setPasswordTargetUser] = useState<PlatformCenterManagerListItem | null>(null);
  const [newPassword, setNewPassword] = useState("");
  const [passwordReason, setPasswordReason] = useState("");

  // Create Form State
  const [createUsername, setCreateUsername] = useState("");
  const [createDisplayName, setCreateDisplayName] = useState("");
  const [createPassword, setCreatePassword] = useState("");
  const [createReason, setCreateReason] = useState("");

  const [successMessage, setSuccessMessage] = useState("");
  const [errorMessage, setErrorMessage] = useState("");

  const { data, isLoading, refetch } = useQuery({
    queryKey: ["center-managers", center.centerId, page, pageSize, search, statusFilter],
    queryFn: () =>
      platformApi.listCenterManagers(center.centerId, {
        page,
        pageSize,
        search,
        status: statusFilter,
      }),
    enabled: isOpen,
  });

  const createMutation = useMutation({
    mutationFn: (req: CreateCenterManagerRequest) =>
      platformApi.createCenterManager(center.centerId, req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["center-managers", center.centerId] });
      queryClient.invalidateQueries({ queryKey: ["platform-centers"] });
      onCenterUpdated();
      setActiveTab("list");
      setCreateUsername("");
      setCreateDisplayName("");
      setCreatePassword("");
      setCreateReason("");
      setSuccessMessage("Đã tạo quản lý trung tâm bổ sung thành công.");
      setTimeout(() => setSuccessMessage(""), 5000);
    },
    onError: (error) => {
      if (isAxiosError<ProblemDetails>(error)) {
        const code = error.response?.data?.errorCode;
        if (code === "DUPLICATE_RESOURCE") {
          setErrorMessage("Tên người dùng đã tồn tại trong trung tâm này.");
        } else if (code === "CONCURRENCY_CONFLICT" || error.response?.status === 409) {
          setErrorMessage("Dữ liệu trung tâm đã bị thay đổi bởi thao tác khác. Vui lòng tải lại.");
          onCenterUpdated();
        } else {
          setErrorMessage(error.response?.data?.detail || "Không thể tạo quản lý. Vui lòng kiểm tra dữ liệu.");
        }
      } else {
        setErrorMessage("Không thể tạo quản lý. Vui lòng thử lại.");
      }
    },
  });

  const updateStatusMutation = useMutation({
    mutationFn: ({
      userId,
      request,
    }: {
      userId: string;
      request: UpdateCenterManagerStatusRequest;
    }) => platformApi.updateCenterManagerStatus(center.centerId, userId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["center-managers", center.centerId] });
      setStatusTargetUser(null);
      setStatusReason("");
      setSuccessMessage("Đã cập nhật trạng thái quản lý và thu hồi phiên liên quan.");
      setTimeout(() => setSuccessMessage(""), 5000);
    },
    onError: (error) => {
      if (isAxiosError<ProblemDetails>(error)) {
        const code = error.response?.data?.errorCode;
        if (code === "CONCURRENCY_CONFLICT" || error.response?.status === 409) {
          setErrorMessage("Dữ liệu người dùng đã bị thay đổi bởi thao tác khác. Vui lòng tải lại.");
          refetch();
        } else {
          setErrorMessage(error.response?.data?.detail || "Không thể cập nhật trạng thái quản lý.");
        }
      } else {
        setErrorMessage("Không thể cập nhật trạng thái quản lý.");
      }
    },
  });

  const makePrimaryMutation = useMutation({
    mutationFn: ({
      userId,
      request,
    }: {
      userId: string;
      request: MakePrimaryCenterManagerRequest;
    }) => platformApi.makePrimaryCenterManager(center.centerId, userId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["center-managers", center.centerId] });
      queryClient.invalidateQueries({ queryKey: ["platform-centers"] });
      onCenterUpdated();
      setPrimaryTargetUser(null);
      setPrimaryReason("");
      setDisablePreviousPrimary(false);
      setSuccessMessage("Đã chuyển quyền Quản lý chính (Primary Manager) thành công.");
      setTimeout(() => setSuccessMessage(""), 5000);
    },
    onError: (error) => {
      if (isAxiosError<ProblemDetails>(error)) {
        const code = error.response?.data?.errorCode;
        if (code === "CONCURRENCY_CONFLICT" || error.response?.status === 409) {
          setErrorMessage("Dữ liệu đã bị thay đổi bởi thao tác khác. Vui lòng tải lại trang.");
          refetch();
          onCenterUpdated();
        } else {
          setErrorMessage(error.response?.data?.detail || "Không thể chuyển quyền Quản lý chính.");
        }
      } else {
        setErrorMessage("Không thể chuyển quyền Quản lý chính.");
      }
    },
  });

  const resetPasswordMutation = useMutation({
    mutationFn: ({
      userId,
      request,
    }: {
      userId: string;
      request: ResetCenterManagerPasswordRequest;
    }) => platformApi.resetCenterManagerPassword(center.centerId, userId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["center-managers", center.centerId] });
      setPasswordTargetUser(null);
      setNewPassword("");
      setPasswordReason("");
      setSuccessMessage("Đã đặt lại mật khẩu và thu hồi toàn bộ token của quản lý.");
      setTimeout(() => setSuccessMessage(""), 5000);
    },
    onError: (error) => {
      if (isAxiosError<ProblemDetails>(error)) {
        const code = error.response?.data?.errorCode;
        if (code === "CONCURRENCY_CONFLICT" || error.response?.status === 409) {
          setErrorMessage("Dữ liệu người dùng đã bị thay đổi bởi thao tác khác. Vui lòng tải lại.");
          refetch();
        } else {
          setErrorMessage(error.response?.data?.detail || "Không thể đặt lại mật khẩu quản lý.");
        }
      } else {
        setErrorMessage("Không thể đặt lại mật khẩu. Vui lòng thử lại.");
      }
    },
  });

  const modalRef = useRef<HTMLDivElement>(null);

  const handleModalClose = useCallback(() => {
    if (primaryTargetUser) {
      setPrimaryTargetUser(null);
      return;
    }
    if (statusTargetUser) {
      setStatusTargetUser(null);
      return;
    }
    if (passwordTargetUser) {
      setPasswordTargetUser(null);
      return;
    }
    onClose();
  }, [primaryTargetUser, statusTargetUser, passwordTargetUser, onClose]);

  useModalAccessibility({
    isOpen,
    onClose: handleModalClose,
    containerRef: modalRef,
  });

  if (!isOpen) return null;

  return (
    <div
      ref={modalRef}
      role="dialog"
      aria-modal="true"
      aria-labelledby="center-managers-modal-title"
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 overflow-y-auto"
      data-testid="center-managers-modal"
    >
      <div className="bg-slate-900 border border-slate-800 rounded-2xl shadow-2xl max-w-4xl w-full max-h-[90vh] flex flex-col overflow-hidden text-slate-100">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-800 bg-slate-900/50">
          <div>
            <h2 id="center-managers-modal-title" className="text-xl font-bold flex items-center gap-2">
              <span>Quản lý nhân sự trung tâm</span>
              <span className="text-xs bg-indigo-500/20 text-indigo-400 border border-indigo-500/30 px-2 py-0.5 rounded-full">
                {center.centerCode}
              </span>
            </h2>
            <p className="text-sm text-slate-400 mt-0.5">{center.centerName}</p>
          </div>
          <button
            onClick={onClose}
            className="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-slate-800 transition"
            aria-label="Đóng"
          >
            ✕
          </button>
        </div>

        {/* Tab Navigation & Banners */}
        <div className="px-6 pt-4 pb-2 flex items-center justify-between border-b border-slate-800/60">
          <div className="flex gap-2">
            <button
              onClick={() => {
                setActiveTab("list");
                setErrorMessage("");
              }}
              className={`px-4 py-2 text-sm font-medium rounded-lg transition ${
                activeTab === "list"
                  ? "bg-indigo-600 text-white shadow-lg shadow-indigo-500/20"
                  : "text-slate-400 hover:text-white hover:bg-slate-800"
              }`}
            >
              Danh sách Quản lý ({data?.totalCount ?? 0})
            </button>
            <button
              onClick={() => {
                setActiveTab("create");
                setErrorMessage("");
              }}
              className={`px-4 py-2 text-sm font-medium rounded-lg transition ${
                activeTab === "create"
                  ? "bg-indigo-600 text-white shadow-lg shadow-indigo-500/20"
                  : "text-slate-400 hover:text-white hover:bg-slate-800"
              }`}
            >
              + Thêm Quản lý mới
            </button>
          </div>

          <div className="text-xs text-slate-400">
            RowVersion Trung tâm: <span className="font-mono text-slate-300">{center.rowVersion}</span>
          </div>
        </div>

        {/* Notifications */}
        {successMessage && (
          <div className="mx-6 mt-4 p-3 rounded-lg bg-emerald-500/10 border border-emerald-500/30 text-emerald-300 text-sm flex items-center justify-between">
            <span>{successMessage}</span>
            <button onClick={() => setSuccessMessage("")} className="text-emerald-400 hover:text-white">✕</button>
          </div>
        )}

        {errorMessage && (
          <div className="mx-6 mt-4 p-3 rounded-lg bg-rose-500/10 border border-rose-500/30 text-rose-300 text-sm flex items-center justify-between">
            <span>{errorMessage}</span>
            <button onClick={() => setErrorMessage("")} className="text-rose-400 hover:text-white">✕</button>
          </div>
        )}

        {/* Modal Content Body */}
        <div className="flex-1 overflow-y-auto p-6">
          {activeTab === "list" ? (
            <div>
              {/* Filter / Search Bar */}
              <div className="flex flex-wrap items-center gap-3 mb-4">
                <input
                  type="text"
                  placeholder="Tìm theo username, họ tên..."
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  className="bg-slate-800 border border-slate-700 text-sm rounded-lg px-3 py-2 text-slate-100 placeholder-slate-400 focus:outline-none focus:border-indigo-500 w-64"
                />
                <select
                  value={statusFilter}
                  onChange={(e) => setStatusFilter(e.target.value)}
                  className="bg-slate-800 border border-slate-700 text-sm rounded-lg px-3 py-2 text-slate-100 focus:outline-none focus:border-indigo-500"
                >
                  <option value="">Tất cả trạng thái</option>
                  <option value="Active">Đang hoạt động (Active)</option>
                  <option value="Locked">Đã khóa (Locked)</option>
                  <option value="Disabled">Vô hiệu hóa (Disabled)</option>
                </select>
                <button
                  onClick={() => refetch()}
                  className="px-3 py-2 text-xs font-medium bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg transition"
                >
                  Tải lại
                </button>
              </div>

              {/* Table */}
              {isLoading ? (
                <div className="text-center py-12 text-slate-400">Đang tải danh sách quản lý...</div>
              ) : !data || data.items.length === 0 ? (
                <div className="text-center py-12 border border-dashed border-slate-800 rounded-xl text-slate-500">
                  Không tìm thấy quản lý nào phù hợp tiêu chí.
                </div>
              ) : (
                <div className="border border-slate-800 rounded-xl overflow-hidden">
                  <table className="w-full text-left text-sm text-slate-300">
                    <thead className="bg-slate-800/80 text-xs uppercase tracking-wider text-slate-400 border-b border-slate-700/60">
                      <tr>
                        <th className="px-4 py-3">Tên đăng nhập / Họ tên</th>
                        <th className="px-4 py-3">Vai trò</th>
                        <th className="px-4 py-3">Trạng thái</th>
                        <th className="px-4 py-3">Ngày tạo</th>
                        <th className="px-4 py-3 text-right">Hành động</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-800/60">
                      {data.items.map((m) => (
                        <tr key={m.userId} className="hover:bg-slate-800/30 transition">
                          <td className="px-4 py-3">
                            <div className="font-semibold text-slate-100">{m.username}</div>
                            <div className="text-xs text-slate-400">{m.displayName}</div>
                          </td>
                          <td className="px-4 py-3">
                            {m.isPrimary ? (
                              <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold bg-amber-500/20 text-amber-300 border border-amber-500/40">
                                ★ Quản lý chính
                              </span>
                            ) : (
                              <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs text-slate-400 bg-slate-800 border border-slate-700">
                                Quản lý bổ sung
                              </span>
                            )}
                          </td>
                          <td className="px-4 py-3">
                            {m.status === "Active" ? (
                              <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                                Hoạt động
                              </span>
                            ) : m.status === "Locked" ? (
                              <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-amber-500/20 text-amber-300 border border-amber-500/30">
                                Tạm khóa
                              </span>
                            ) : (
                              <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-rose-500/20 text-rose-300 border border-rose-500/30">
                                Vô hiệu hóa
                              </span>
                            )}
                          </td>
                          <td className="px-4 py-3 text-xs text-slate-400">
                            {new Date(m.createdAt).toLocaleDateString("vi-VN")}
                          </td>
                          <td className="px-4 py-3 text-right">
                            <div className="flex items-center justify-end gap-1.5">
                              {!m.isPrimary && m.status === "Active" && (
                                <button
                                  onClick={() => {
                                    setPrimaryTargetUser(m);
                                    setPrimaryReason("");
                                    setDisablePreviousPrimary(false);
                                  }}
                                  className="text-xs font-medium text-amber-400 hover:text-amber-300 bg-amber-500/10 hover:bg-amber-500/20 border border-amber-500/30 px-2 py-1 rounded transition"
                                  title="Chỉ định làm Quản lý chính"
                                >
                                  Đặt làm Chính
                                </button>
                              )}
                              <button
                                onClick={() => {
                                  setStatusTargetUser(m);
                                  setNewStatus(m.status);
                                  setStatusReason("");
                                }}
                                className="text-xs font-medium text-slate-300 hover:text-white bg-slate-800 hover:bg-slate-700 px-2 py-1 rounded transition"
                              >
                                Trạng thái
                              </button>
                              <button
                                onClick={() => {
                                  setPasswordTargetUser(m);
                                  setNewPassword("");
                                  setPasswordReason("");
                                }}
                                className="text-xs font-medium text-indigo-400 hover:text-indigo-300 bg-indigo-500/10 hover:bg-indigo-500/20 border border-indigo-500/30 px-2 py-1 rounded transition"
                              >
                                Reset Pass
                              </button>
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              {data && data.totalCount > pageSize && (
                <div className="flex items-center justify-between mt-3 text-xs text-slate-400">
                  <span>
                    Trang {page} / {Math.ceil(data.totalCount / pageSize)} ({data.totalCount} quản lý)
                  </span>
                  <div className="flex gap-2">
                    <button
                      type="button"
                      disabled={page <= 1}
                      onClick={() => setPage((p) => Math.max(1, p - 1))}
                      className="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 disabled:opacity-40"
                    >
                      Trước
                    </button>
                    <button
                      type="button"
                      disabled={page >= Math.ceil(data.totalCount / pageSize)}
                      onClick={() => setPage((p) => p + 1)}
                      className="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 disabled:opacity-40"
                    >
                      Sau
                    </button>
                  </div>
                </div>
              )}
            </div>
          ) : (
            /* Create Manager Form */
            <form
              onSubmit={(e) => {
                e.preventDefault();
                setErrorMessage("");
                if (createPassword.length < 12) {
                  setErrorMessage("Mật khẩu quản lý phải có tối thiểu 12 ký tự.");
                  return;
                }
                if (!createReason.trim()) {
                  setErrorMessage("Vui lòng nhập lý do tạo quản lý trung tâm.");
                  return;
                }
                const rawUsername = createUsername.trim();
                const normalizedUsername = normalizeToAscii(rawUsername).replace(/\s+/g, "_");
                if (normalizedUsername.length < 2 || !/^[a-zA-Z0-9._-]+$/.test(normalizedUsername)) {
                  setErrorMessage("Tên đăng nhập quản lý không hợp lệ (tối thiểu 2 ký tự, gồm chữ cái, số, dấu '.', '-' hoặc '_').");
                  return;
                }
                createMutation.mutate({
                  username: normalizedUsername,
                  displayName: createDisplayName.trim(),
                  password: createPassword,
                  expectedCenterRowVersion: center.rowVersion,
                  reason: createReason.trim(),
                });
              }}
              className="max-w-xl mx-auto space-y-4"
            >
              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1">
                  Tên đăng nhập *
                </label>
                  <input
                    type="text"
                    required
                    maxLength={100}
                    value={createUsername}
                    onChange={(e) => setCreateUsername(e.target.value)}
                    placeholder="manager_secondary"
                    className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-100 focus:outline-none focus:border-indigo-500"
                  />
                  {createUsername.trim() && normalizeToAscii(createUsername.trim()).replace(/\s+/g, "_") !== createUsername.trim() && (
                    <p className="mt-1 text-[11px] text-indigo-400 font-medium">
                      ✓ Sẽ lưu chuẩn hóa: <strong className="font-mono">{normalizeToAscii(createUsername.trim()).replace(/\s+/g, "_")}</strong>
                    </p>
                  )}
              </div>

              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1">
                  Họ và tên hiển thị *
                </label>
                <input
                  type="text"
                  required
                  maxLength={200}
                  value={createDisplayName}
                  onChange={(e) => setCreateDisplayName(e.target.value)}
                  placeholder="Nguyễn Văn B"
                  className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-100 focus:outline-none focus:border-indigo-500"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1">
                  Mật khẩu khởi tạo (tối thiểu 12 ký tự) *
                </label>
                <input
                  type="password"
                  required
                  minLength={12}
                  value={createPassword}
                  onChange={(e) => setCreatePassword(e.target.value)}
                  placeholder="Mật khẩu bảo mật..."
                  className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-100 focus:outline-none focus:border-indigo-500"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1">
                  Lý do tạo tài khoản quản lý *
                </label>
                <textarea
                  required
                  rows={2}
                  value={createReason}
                  onChange={(e) => setCreateReason(e.target.value)}
                  placeholder="Bổ sung quản lý vận hành cơ sở mới..."
                  className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-100 focus:outline-none focus:border-indigo-500"
                />
              </div>

              <div className="pt-2 flex justify-end gap-3">
                <button
                  type="button"
                  onClick={() => setActiveTab("list")}
                  className="px-4 py-2 text-sm font-medium text-slate-300 hover:text-white bg-slate-800 hover:bg-slate-700 rounded-lg transition"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={createMutation.isPending}
                  className="px-5 py-2 text-sm font-semibold bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg shadow-lg shadow-indigo-600/30 transition disabled:opacity-50"
                >
                  {createMutation.isPending ? "Đang tạo..." : "Tạo Quản lý"}
                </button>
              </div>
            </form>
          )}
        </div>
      </div>

      {/* Sub-modal: Make Primary */}
      {primaryTargetUser && (
        <div className="fixed inset-0 z-60 flex items-center justify-center bg-black/70 backdrop-blur-sm p-4">
          <div className="bg-slate-900 border border-slate-700 rounded-2xl max-w-md w-full p-6 text-slate-100 shadow-2xl">
            <h3 className="text-lg font-bold text-amber-400 flex items-center gap-2 mb-2">
              <span>★ Chỉ định Quản lý chính</span>
            </h3>
            <p className="text-sm text-slate-300 mb-4">
              Bạn đang chuyển quyền Quản lý chính của trung tâm sang{" "}
              <span className="font-semibold text-white">{primaryTargetUser.displayName} ({primaryTargetUser.username})</span>.
            </p>

            <div className="space-y-4">
              <label className="flex items-center gap-2 text-sm text-slate-300 bg-slate-800/60 p-3 rounded-lg border border-slate-700/60 cursor-pointer">
                <input
                  type="checkbox"
                  checked={disablePreviousPrimary}
                  onChange={(e) => setDisablePreviousPrimary(e.target.checked)}
                  className="rounded border-slate-600 text-indigo-600 focus:ring-indigo-500"
                />
                <span>Đồng thời vô hiệu hóa Quản lý chính cũ và thu hồi toàn bộ phiên làm việc</span>
              </label>

              {disablePreviousPrimary && !center.primaryManagerUserRowVersion && (
                <div className="p-3 bg-amber-500/15 border border-amber-500/40 rounded-lg text-xs text-amber-300 flex items-start gap-2">
                  <span className="text-base leading-none">⚠️</span>
                  <div>
                    <span className="font-semibold">Không tìm thấy phiên bản dữ liệu (RowVersion) của Quản lý chính hiện tại từ thông tin trung tâm.</span>
                    <p className="mt-0.5 text-amber-400/90">Vui lòng đóng modal và tải lại danh sách trung tâm để đồng bộ trạng thái mới nhất trước khi thực hiện thao tác này.</p>
                  </div>
                </div>
              )}

              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1">
                  Lý do thay đổi Quản lý chính *
                </label>
                <textarea
                  required
                  rows={2}
                  value={primaryReason}
                  onChange={(e) => setPrimaryReason(e.target.value)}
                  placeholder="Thay đổi nhân sự cấp quản lý..."
                  className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-100 focus:outline-none focus:border-amber-500"
                />
              </div>

              <div className="flex justify-end gap-3 pt-2">
                <button
                  type="button"
                  onClick={() => setPrimaryTargetUser(null)}
                  className="px-4 py-2 text-sm font-medium text-slate-400 hover:text-white bg-slate-800 rounded-lg"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  disabled={
                    !primaryReason.trim() ||
                    makePrimaryMutation.isPending ||
                    (disablePreviousPrimary && !center.primaryManagerUserRowVersion)
                  }
                  onClick={() => {
                    makePrimaryMutation.mutate({
                      userId: primaryTargetUser.userId,
                      request: {
                        expectedCenterRowVersion: center.rowVersion,
                        expectedManagerUserRowVersion: primaryTargetUser.rowVersion,
                        disablePreviousPrimary,
                        expectedPreviousPrimaryUserRowVersion: disablePreviousPrimary
                          ? (center.primaryManagerUserRowVersion ?? undefined)
                          : undefined,
                        reason: primaryReason.trim(),
                      },
                    });
                  }}
                  className="px-4 py-2 text-sm font-semibold bg-amber-600 hover:bg-amber-500 text-white rounded-lg transition disabled:opacity-50"
                >
                  {makePrimaryMutation.isPending ? "Đang xử lý..." : "Xác nhận chuyển chính"}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Sub-modal: Change Status */}
      {statusTargetUser && (
        <div className="fixed inset-0 z-60 flex items-center justify-center bg-black/70 backdrop-blur-sm p-4">
          <div className="bg-slate-900 border border-slate-700 rounded-2xl max-w-md w-full p-6 text-slate-100 shadow-2xl">
            <h3 className="text-lg font-bold text-slate-100 mb-2">
              Đổi trạng thái quản lý
            </h3>
            <p className="text-sm text-slate-300 mb-4">
              Người dùng: <span className="font-semibold text-white">{statusTargetUser.displayName} ({statusTargetUser.username})</span>
            </p>

            <div className="space-y-4">
              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1">
                  Trạng thái mới
                </label>
                <select
                  value={newStatus}
                  onChange={(e) => setNewStatus(e.target.value)}
                  className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-100 focus:outline-none focus:border-indigo-500"
                >
                  <option value="Active">Đang hoạt động (Active)</option>
                  <option value="Locked">Khóa tạm thời (Locked)</option>
                  <option value="Disabled">Vô hiệu hóa vĩnh viễn (Disabled)</option>
                </select>
                {statusTargetUser.isPrimary && newStatus !== "Active" && (
                  <p className="text-xs text-rose-400 mt-1">
                    Lưu ý: Không thể khóa/vô hiệu hóa Quản lý chính nếu chưa chuyển quyền cho quản lý khác.
                  </p>
                )}
              </div>

              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1">
                  Lý do thay đổi trạng thái *
                </label>
                <textarea
                  required
                  rows={2}
                  value={statusReason}
                  onChange={(e) => setStatusReason(e.target.value)}
                  placeholder="Lý do khóa hoặc mở khóa..."
                  className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-100 focus:outline-none focus:border-indigo-500"
                />
              </div>

              <div className="flex justify-end gap-3 pt-2">
                <button
                  type="button"
                  onClick={() => setStatusTargetUser(null)}
                  className="px-4 py-2 text-sm font-medium text-slate-400 hover:text-white bg-slate-800 rounded-lg"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  disabled={!statusReason.trim() || updateStatusMutation.isPending}
                  onClick={() => {
                    updateStatusMutation.mutate({
                      userId: statusTargetUser.userId,
                      request: {
                        status: newStatus,
                        expectedUserRowVersion: statusTargetUser.rowVersion,
                        reason: statusReason.trim(),
                      },
                    });
                  }}
                  className="px-4 py-2 text-sm font-semibold bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg transition disabled:opacity-50"
                >
                  {updateStatusMutation.isPending ? "Đang lưu..." : "Cập nhật"}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Sub-modal: Reset Password */}
      {passwordTargetUser && (
        <div className="fixed inset-0 z-60 flex items-center justify-center bg-black/70 backdrop-blur-sm p-4">
          <div className="bg-slate-900 border border-slate-700 rounded-2xl max-w-md w-full p-6 text-slate-100 shadow-2xl">
            <h3 className="text-lg font-bold text-indigo-400 mb-2">
              Đặt lại mật khẩu quản lý
            </h3>
            <p className="text-sm text-slate-300 mb-4">
              Người dùng: <span className="font-semibold text-white">{passwordTargetUser.displayName} ({passwordTargetUser.username})</span>
            </p>

            <div className="space-y-4">
              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1">
                  Mật khẩu mới (tối thiểu 12 ký tự) *
                </label>
                <input
                  type="password"
                  required
                  minLength={12}
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  placeholder="Mật khẩu mới..."
                  className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-100 focus:outline-none focus:border-indigo-500"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold uppercase text-slate-400 mb-1">
                  Lý do đặt lại mật khẩu *
                </label>
                <textarea
                  required
                  rows={2}
                  value={passwordReason}
                  onChange={(e) => setPasswordReason(e.target.value)}
                  placeholder="Quản lý yêu cầu cấp lại mật khẩu..."
                  className="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-100 focus:outline-none focus:border-indigo-500"
                />
              </div>

              <div className="flex justify-end gap-3 pt-2">
                <button
                  type="button"
                  onClick={() => setPasswordTargetUser(null)}
                  className="px-4 py-2 text-sm font-medium text-slate-400 hover:text-white bg-slate-800 rounded-lg"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  disabled={newPassword.length < 12 || !passwordReason.trim() || resetPasswordMutation.isPending}
                  onClick={() => {
                    resetPasswordMutation.mutate({
                      userId: passwordTargetUser.userId,
                      request: {
                        newPassword,
                        expectedUserRowVersion: passwordTargetUser.rowVersion,
                        reason: passwordReason.trim(),
                      },
                    });
                  }}
                  className="px-4 py-2 text-sm font-semibold bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg transition disabled:opacity-50"
                >
                  {resetPasswordMutation.isPending ? "Đang đặt lại..." : "Xác nhận đặt lại"}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
