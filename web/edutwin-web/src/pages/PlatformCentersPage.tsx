import React, { useState, useRef } from "react";
import { Link } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { platformApi } from "../api/platformApi";
import type {
  PlatformCenterListItem,
  CreatePlatformCenterRequest,
  UpdatePlatformCenterStatusRequest,
  UpdateCenterMetadataRequest,
} from "../types/platform";
import type { ProblemDetails } from "../types/auth";
import { useAuthStore } from "../stores/authStore";
import { permissions } from "../auth/permissions";
import { CenterManagersModal } from "../components/CenterManagersModal";
import { useModalAccessibility } from "../utils/useModalAccessibility";
import { AUDIT_ACTION_MAP } from "../utils/platformPresentation";
import { normalizeToAscii } from "../utils/identifierNormalization";

export const PlatformCentersPage: React.FC = () => {
  const queryClient = useQueryClient();
  const canManageCenters = useAuthStore((state) => state.hasPermission(permissions.platformCentersManage));
  const canAccessAudit = useAuthStore((state) => state.hasPermission(permissions.platformAuditRead));

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
  const [managersModalCenter, setManagersModalCenter] = useState<PlatformCenterListItem | null>(null);
  const [editMetadataCenter, setEditMetadataCenter] = useState<PlatformCenterListItem | null>(null);

  // Edit Metadata Form State
  const [editCenterName, setEditCenterName] = useState("");
  const [editTimezone, setEditTimezone] = useState("Asia/Bangkok");
  const [editReason, setEditReason] = useState("");

  // Create Form State
  const [newCenterCode, setNewCenterCode] = useState("");
  const [newCenterName, setNewCenterName] = useState("");
  const [newTimezone, setNewTimezone] = useState("Asia/Bangkok");
  const [newManagerUsername, setNewManagerUsername] = useState("");
  const [newManagerDisplayName, setNewManagerDisplayName] = useState("");
  const [newManagerPassword, setNewManagerPassword] = useState("");

  const [successMessage, setSuccessMessage] = useState("");
  const [errorMessage, setErrorMessage] = useState("");
  const [createModalError, setCreateModalError] = useState("");
  const [showCreatePassword, setShowCreatePassword] = useState(false);
  const [editModalError, setEditModalError] = useState("");

  // Accessibility refs for modals
  const createModalRef = useRef<HTMLDivElement>(null);
  const statusModalRef = useRef<HTMLDivElement>(null);
  const editMetadataModalRef = useRef<HTMLDivElement>(null);

  useModalAccessibility({
    isOpen: isCreateModalOpen,
    onClose: () => setIsCreateModalOpen(false),
    containerRef: createModalRef,
  });

  useModalAccessibility({
    isOpen: !!statusModalCenter,
    onClose: () => setStatusModalCenter(null),
    containerRef: statusModalRef,
  });

  useModalAccessibility({
    isOpen: !!editMetadataCenter,
    onClose: () => setEditMetadataCenter(null),
    containerRef: editMetadataModalRef,
  });

  // Main Centers Query
  const { data, isLoading, isError, error, refetch } = useQuery({
    queryKey: ["platform-centers", page, pageSize, search, status],
    queryFn: () => platformApi.listCenters({ page, pageSize, search, status }),
  });

  const isQueryForbidden = isAxiosError(error) && error.response?.status === 403;
  const isQueryRateLimit = isAxiosError(error) && error.response?.status === 429;

  // Recent Audits Query (capability-guarded)
  const { data: recentAuditsData } = useQuery({
    queryKey: ["platform-recent-audits"],
    queryFn: () => platformApi.listAuditLogs({ page: 1, pageSize: 5 }),
    enabled: canAccessAudit,
  });

  const createMutation = useMutation({
    mutationFn: (req: CreatePlatformCenterRequest) => platformApi.createCenter(req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["platform-centers"] });
      queryClient.invalidateQueries({ queryKey: ["platform-recent-audits"] });
      setIsCreateModalOpen(false);
      resetCreateForm();
      setSuccessMessage("Đã tạo trung tâm mới thành công.");
      setTimeout(() => setSuccessMessage(""), 5000);
    },
    onError: (err) => {
      if (isAxiosError<ProblemDetails>(err)) {
        const detail = err.response?.data?.detail;
        const code = err.response?.data?.errorCode;
        let msg = detail || "Không thể tạo trung tâm. Vui lòng kiểm tra lại thông tin.";
        if (code === "DUPLICATE_RESOURCE") {
          msg = "Mã trung tâm đã tồn tại trong hệ thống.";
        } else if (code === "FORBIDDEN_RESOURCE") {
          msg = "Không được phép tạo trung tâm với mã PLATFORM.";
        } else if (err.response?.status === 429) {
          msg = "Hệ thống ghi nhận quá nhiều yêu cầu. Vui lòng chờ giây lát.";
        }
        setCreateModalError(msg);
        setErrorMessage(msg);
      } else {
        setCreateModalError("Không thể tạo trung tâm. Vui lòng thử lại.");
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
      queryClient.invalidateQueries({ queryKey: ["platform-recent-audits"] });
      setStatusModalCenter(null);
      setStatusReason("");
      setSuccessMessage("Đã cập nhật trạng thái trung tâm thành công.");
      setTimeout(() => setSuccessMessage(""), 5000);
    },
    onError: (err) => {
      if (isAxiosError<ProblemDetails>(err)) {
        const code = err.response?.data?.errorCode;
        if (code === "CONCURRENCY_CONFLICT" || err.response?.status === 409) {
          setErrorMessage("Dữ liệu trung tâm đã bị thay đổi bởi tác vụ khác (409 Conflict). Đang tải lại dữ liệu mới nhất...");
          refetch();
          setStatusModalCenter(null);
        } else if (err.response?.status === 429) {
          setErrorMessage("Hệ thống ghi nhận quá nhiều yêu cầu (429). Vui lòng thử lại sau ít phút.");
        } else {
          setErrorMessage(err.response?.data?.detail || "Không thể cập nhật trạng thái trung tâm.");
        }
      } else {
        setErrorMessage("Không thể cập nhật trạng thái. Vui lòng thử lại.");
      }
    },
  });

  const updateMetadataMutation = useMutation({
    mutationFn: ({
      centerId,
      request,
    }: {
      centerId: string;
      request: UpdateCenterMetadataRequest;
    }) => platformApi.updateCenterMetadata(centerId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["platform-centers"] });
      queryClient.invalidateQueries({ queryKey: ["platform-recent-audits"] });
      setEditMetadataCenter(null);
      setEditReason("");
      setSuccessMessage("Đã cập nhật thông tin trung tâm thành công.");
      setTimeout(() => setSuccessMessage(""), 5000);
    },
    onError: (err) => {
      if (isAxiosError<ProblemDetails>(err)) {
        const code = err.response?.data?.errorCode;
        let msg = err.response?.data?.detail || "Không thể cập nhật thông tin trung tâm.";
        if (code === "CONCURRENCY_CONFLICT" || err.response?.status === 409) {
          msg = "Dữ liệu trung tâm đã bị thay đổi bởi tác vụ khác (409 Conflict). Đang tải lại dữ liệu mới nhất...";
          refetch();
          setEditMetadataCenter(null);
        } else if (err.response?.status === 429) {
          msg = "Hệ thống ghi nhận quá nhiều yêu cầu (429). Vui lòng thử lại sau ít phút.";
        }
        setEditModalError(msg);
        setErrorMessage(msg);
      } else {
        setEditModalError("Không thể cập nhật thông tin trung tâm. Vui lòng thử lại.");
        setErrorMessage("Không thể cập nhật thông tin trung tâm. Vui lòng thử lại.");
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
    setCreateModalError("");
    setShowCreatePassword(false);
  };

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setSearch(searchInput);
    setStatus(statusInput);
    setPage(1);
  };

  const handleToggleStatusConfirm = () => {
    if (!statusModalCenter) return;
    if (!statusReason.trim() || statusReason.trim().length < 5) {
      setErrorMessage("Vui lòng nhập lý do thay đổi trạng thái trung tâm (tối thiểu 5 ký tự).");
      return;
    }
    const targetStatus = statusModalCenter.status === "Active" ? "Suspended" : "Active";
    updateStatusMutation.mutate({
      centerId: statusModalCenter.centerId,
      request: {
        status: targetStatus,
        rowVersion: statusModalCenter.rowVersion,
        reason: statusReason.trim(),
      },
    });
  };

  const handleEditMetadataConfirm = (e: React.FormEvent) => {
    e.preventDefault();
    if (!editMetadataCenter) return;
    if (!editReason.trim()) {
      setErrorMessage("Vui lòng nhập lý do thay đổi thông tin trung tâm.");
      return;
    }
    updateMetadataMutation.mutate({
      centerId: editMetadataCenter.centerId,
      request: {
        centerName: editCenterName.trim(),
        timezone: editTimezone,
        expectedRowVersion: editMetadataCenter.rowVersion,
        reason: editReason.trim(),
      },
    });
  };

  const centers = data?.items || [];
  const totalCount = data?.totalCount || 0;
  const totalPages = Math.ceil(totalCount / pageSize) || 1;

  const totalStudents = centers.reduce((sum, c) => sum + (c.activeStudentCount || 0), 0);
  const totalTeachers = centers.reduce((sum, c) => sum + (c.activeTeacherCount || 0), 0);
  const totalClasses = centers.reduce((sum, c) => sum + (c.classCount || 0), 0);
  const totalManagers = centers.reduce((sum, c) => sum + (c.activeManagerCount || 0), 0);
  const activeCenters = centers.filter((c) => c.status === "Active").length;
  const suspendedCenters = centers.filter((c) => c.status === "Suspended").length;
  const activePrimaryManagers = centers.filter((c) => c.hasActivePrimaryManager || c.primaryManagerUserId).length;

  const isFiltered = Boolean(search || status);
  const kpiLabel = isFiltered ? "Kết quả theo bộ lọc" : "Tổng số trung tâm";

  return (
    <div className="max-w-7xl mx-auto space-y-6">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-slate-900 dark:text-white">
            Quản Lý Trung Tâm Đối Tác
          </h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
            Khởi tạo, kích hoạt và quản lý phân bổ nhân sự quản lý trung tâm đối tác.
          </p>
        </div>

        {canManageCenters && (
          <button
            type="button"
            onClick={() => {
              setErrorMessage("");
              setCreateModalError("");
              setIsCreateModalOpen(true);
            }}
            className="inline-flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-500 text-white text-sm font-semibold shadow-sm shadow-blue-600/20 transition-all cursor-pointer shrink-0"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
            <span>Thêm trung tâm mới</span>
          </button>
        )}
      </div>

      {/* Control Plane Boundary Banner */}
      <div className="p-4 rounded-2xl bg-blue-500/10 border border-blue-500/20 text-slate-800 dark:text-slate-200 flex items-start gap-3 shadow-xs">
        <div className="p-2 rounded-xl bg-blue-500/15 text-blue-600 dark:text-blue-400 shrink-0 mt-0.5">
          <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
          </svg>
        </div>
        <div className="text-xs sm:text-sm">
          <span className="font-semibold text-blue-900 dark:text-blue-300">Ranh giới Control Plane: </span>
          <span className="text-slate-600 dark:text-slate-400">
            Khu vực quản lý hạ tầng multi-tenant và vòng đời trung tâm. Mọi thay đổi trạng thái và chuyển quyền quản lý được kiểm toán chặt chẽ và không can thiệp vào nội dung giáo dục nội bộ.
          </span>
        </div>
      </div>

      {/* Notifications */}
      {successMessage && (
        <div className="p-4 rounded-xl bg-emerald-500/10 border border-emerald-500/20 text-emerald-800 dark:text-emerald-300 text-sm flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span>✓</span>
            <span>{successMessage}</span>
          </div>
          <button
            type="button"
            onClick={() => setSuccessMessage("")}
            className="text-emerald-600 dark:text-emerald-400 hover:underline text-xs"
          >
            Đóng
          </button>
        </div>
      )}
      {errorMessage && (
        <div className="p-4 rounded-xl bg-rose-500/10 border border-rose-500/20 text-rose-800 dark:text-rose-300 text-sm flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span>⚠️</span>
            <span>{errorMessage}</span>
          </div>
          <button
            type="button"
            onClick={() => setErrorMessage("")}
            className="text-rose-600 dark:text-rose-400 hover:underline text-xs"
          >
            Đóng
          </button>
        </div>
      )}

      {/* 403 Forbidden State */}
      {isQueryForbidden && (
        <div className="p-8 rounded-2xl bg-rose-500/10 border border-rose-500/20 text-center">
          <span className="text-3xl">🚫</span>
          <h2 className="mt-2 text-lg font-bold text-rose-600 dark:text-rose-400">403 - Quyền truy cập bị từ chối</h2>
          <p className="mt-1 text-sm text-slate-600 dark:text-slate-400">
            Tài khoản của bạn không có quyền xem hoặc quản lý trung tâm (platform.centers.read / manage).
          </p>
          <Link to="/" className="mt-4 inline-block px-4 py-2 bg-slate-800 text-white text-xs font-semibold rounded-lg">
            Về trang chủ
          </Link>
        </div>
      )}

      {/* 429 Rate Limit State */}
      {isQueryRateLimit && (
        <div className="p-4 rounded-xl bg-amber-500/15 border border-amber-500/30 text-amber-800 dark:text-amber-300 text-sm flex items-center justify-between">
          <span>Hệ thống ghi nhận quá nhiều yêu cầu (429). Vui lòng chờ ít phút trước khi thử lại.</span>
          <button
            type="button"
            onClick={() => refetch()}
            className="px-3 py-1 bg-amber-600 text-white rounded-lg text-xs font-semibold hover:bg-amber-500"
          >
            Thử lại
          </button>
        </div>
      )}

      {/* KPI Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        {/* Card 1: Total Centers & Operational Status */}
        <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              {kpiLabel}
            </span>
            <span className="p-2 rounded-xl bg-blue-50 dark:bg-blue-950/50 text-blue-600 dark:text-blue-400">
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
              </svg>
            </span>
          </div>
          <div className="mt-3 flex items-baseline gap-2">
            <span className="text-3xl font-bold text-slate-900 dark:text-white">
              {isLoading ? "..." : totalCount}
            </span>
            <span className="text-xs text-slate-500 dark:text-slate-400">
              {isFiltered ? "khớp bộ lọc" : "trên nền tảng"}
            </span>
          </div>
          <div className="mt-3 pt-3 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between text-xs text-slate-600 dark:text-slate-400">
            <span className="flex items-center gap-1.5">
              <span className="w-2 h-2 rounded-full bg-emerald-500"></span>
              <span>{activeCenters} Hoạt động</span>
            </span>
            <span className="flex items-center gap-1.5">
              <span className={`w-2 h-2 rounded-full ${suspendedCenters > 0 ? "bg-amber-500" : "bg-slate-300 dark:bg-slate-700"}`}></span>
              <span>{suspendedCenters} Tạm ngưng</span>
            </span>
          </div>
        </div>

        {/* Card 2: Educational Scale (Students, Teachers, Classes) */}
        <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              Quy Mô Đào Tạo
            </span>
            <span className="p-2 rounded-xl bg-emerald-50 dark:bg-emerald-950/50 text-emerald-600 dark:text-emerald-400">
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 14l9-5-9-5-9 5 9 5z" />
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 14l6.16-3.422a12.083 12.083 0 01.665 6.479A11.952 11.952 0 0012 20.055a11.952 11.952 0 00-6.824-2.998 12.078 12.078 0 01.665-6.479L12 14z" />
              </svg>
            </span>
          </div>
          <div className="mt-3 flex items-baseline gap-2">
            <span className="text-3xl font-bold text-slate-900 dark:text-white">
              {isLoading ? "..." : totalStudents.toLocaleString("vi-VN")}
            </span>
            <span className="text-xs text-slate-500 dark:text-slate-400">
              học viên hoạt động
            </span>
          </div>
          <div className="mt-3 pt-3 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between text-xs text-slate-600 dark:text-slate-400">
            <span>👨‍🏫 {totalTeachers.toLocaleString("vi-VN")} Giáo viên</span>
            <span>🏫 {totalClasses.toLocaleString("vi-VN")} Lớp học</span>
          </div>
        </div>

        {/* Card 3: Partner Center Administrative Staff */}
        <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              Nhân Lực Quản Trị
            </span>
            <span className="p-2 rounded-xl bg-purple-50 dark:bg-purple-950/50 text-purple-600 dark:text-purple-400">
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
              </svg>
            </span>
          </div>
          <div className="mt-3 flex items-baseline gap-2">
            <span className="text-3xl font-bold text-slate-900 dark:text-white">
              {isLoading ? "..." : totalManagers.toLocaleString("vi-VN")}
            </span>
            <span className="text-xs text-slate-500 dark:text-slate-400">
              quản lý cơ sở
            </span>
          </div>
          <div className="mt-3 pt-3 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between text-xs text-slate-600 dark:text-slate-400">
            <span>⭐ {activePrimaryManagers} Quản lý chính</span>
            <span className="text-slate-400 font-mono text-[11px]">Multi-Tenant</span>
          </div>
        </div>
      </div>

      {/* Recent Audit Logs Widget (capability-guarded) */}
      {canAccessAudit && recentAuditsData?.items && recentAuditsData.items.length > 0 && (
        <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs">
          <div className="flex items-center justify-between mb-3">
            <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-2">
              <span>Hoạt động kiểm toán gần đây</span>
              <span className="text-[10px] px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 font-normal">
                platform.audit.read
              </span>
            </h3>
            <Link
              to="/quan-tri-nen-tang/nhat-ky"
              className="text-xs text-blue-600 dark:text-blue-400 hover:underline font-medium"
            >
              Xem tất cả nhật ký →
            </Link>
          </div>
          <div className="divide-y divide-slate-100 dark:divide-slate-800">
            {recentAuditsData.items.map((audit) => (
              <div key={audit.auditId} className="py-2.5 flex items-center justify-between text-xs flex-wrap gap-2">
                <div className="flex items-center gap-3">
                  <span className="px-2 py-0.5 rounded-md text-[11px] bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 flex items-center gap-1">
                    <span className="font-semibold text-slate-900 dark:text-slate-100">
                      {AUDIT_ACTION_MAP[audit.actionType] || audit.actionType}
                    </span>
                    <span className="font-mono text-[10px] text-slate-400">
                      ({audit.actionType})
                    </span>
                  </span>
                  <span className="text-slate-900 dark:text-white font-medium">
                    {audit.actorUsername ? `@${audit.actorUsername}` : "Hệ thống"}
                  </span>
                  {(audit.targetCenterCode || audit.targetCenterName) && (
                    <span
                      className="text-slate-500 dark:text-slate-400 truncate max-w-[340px]"
                      title={[audit.targetCenterCode ? `[${audit.targetCenterCode}]` : "", audit.targetCenterName].filter(Boolean).join(" ")}
                    >
                      → {audit.targetCenterCode ? `[${audit.targetCenterCode}]` : ""} {audit.targetCenterName || ""}
                    </span>
                  )}
                </div>
                <span className="text-slate-400 text-[11px]">
                  {new Date(audit.createdAt).toLocaleString("vi-VN")}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Filter / Search Bar */}
      <form onSubmit={handleSearchSubmit} className="bg-white dark:bg-slate-900 p-4 rounded-2xl border border-slate-200 dark:border-slate-800 shadow-xs flex flex-col md:flex-row gap-3 items-center">
        <div className="relative flex-1 w-full">
          <input
            type="text"
            placeholder="Tìm kiếm theo mã, tên trung tâm hoặc người quản lý..."
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            className="w-full pl-10 pr-4 py-2.5 text-sm border border-slate-200 dark:border-slate-700 rounded-xl bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          <svg className="w-5 h-5 text-slate-400 absolute left-3 top-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>
        <select
          value={statusInput}
          onChange={(e) => setStatusInput(e.target.value)}
          className="w-full md:w-48 px-3.5 py-2.5 text-sm border border-slate-200 dark:border-slate-700 rounded-xl bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          <option value="">Tất cả trạng thái</option>
          <option value="Active">Đang hoạt động</option>
          <option value="Suspended">Tạm ngưng</option>
        </select>
        <button
          type="submit"
          className="w-full md:w-auto px-6 py-2.5 text-sm font-semibold text-white bg-slate-800 dark:bg-slate-700 hover:bg-slate-900 dark:hover:bg-slate-600 rounded-xl transition-colors cursor-pointer"
        >
          Tìm kiếm
        </button>
      </form>

      {/* Data Table or Empty State */}
      <div className="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200 dark:border-slate-800 shadow-xs overflow-hidden">
        {isLoading ? (
          <div className="p-12 text-center text-slate-500 dark:text-slate-400">
            <svg className="animate-spin h-8 w-8 mx-auto mb-3 text-blue-600" viewBox="0 0 24 24" fill="none">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
            </svg>
            Đang tải dữ liệu trung tâm...
          </div>
        ) : isError ? (
          <div className="p-12 text-center text-rose-500">
            Có lỗi xảy ra khi tải danh sách trung tâm. Vui lòng thử lại.
          </div>
        ) : centers.length === 0 ? (
          /* Empty State */
          <div className="p-16 text-center">
            <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-blue-50 dark:bg-blue-900/30 flex items-center justify-center text-blue-600 dark:text-blue-400">
              <svg className="w-8 h-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
              </svg>
            </div>
            <h3 className="text-lg font-bold text-slate-900 dark:text-white mb-1">
              Chưa có trung tâm giáo dục nào
            </h3>
            <p className="text-slate-500 dark:text-slate-400 text-sm max-w-sm mx-auto mb-6">
              {isFiltered ? "Không tìm thấy trung tâm nào khớp với điều kiện lọc." : "Chưa có trung tâm giáo dục nào được khởi tạo trên nền tảng."}
            </p>
            {canManageCenters && !isFiltered && (
              <button
                type="button"
                onClick={() => {
                  setErrorMessage("");
                  setIsCreateModalOpen(true);
                }}
                className="inline-flex items-center px-4 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-500 text-white font-semibold text-sm shadow-sm transition-colors cursor-pointer"
              >
                <svg className="w-4 h-4 mr-1.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                </svg>
                Tạo trung tâm đầu tiên
              </button>
            )}
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 dark:border-slate-800 bg-slate-50/75 dark:bg-slate-800/50 text-[11px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  <th className="py-3.5 px-6">Mã & Tên Trung Tâm</th>
                  <th className="py-3.5 px-6">Trạng Thái</th>
                  <th className="py-3.5 px-6">Quy Mô</th>
                  <th className="py-3.5 px-6">Quản Lý Chính</th>
                  <th className="py-3.5 px-6">Múi Giờ</th>
                  <th className="py-3.5 px-6">Ngày Tạo</th>
                  <th className="py-3.5 px-6">OCC Version</th>
                  <th className="py-3.5 px-6 text-right">Thao Tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800/60 text-sm">
                {centers.map((center) => (
                  <tr key={center.centerId} className="hover:bg-slate-50/50 dark:hover:bg-slate-800/30 transition-colors">
                    <td className="py-4 px-6">
                      <div className="font-semibold text-slate-900 dark:text-white">
                        {center.centerName}
                      </div>
                      <div className="text-xs font-mono text-slate-400 mt-0.5">
                        {center.centerCode}
                      </div>
                    </td>
                    <td className="py-4 px-6">
                      {center.status === "Active" ? (
                        <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold bg-emerald-500/15 text-emerald-700 dark:text-emerald-400 border border-emerald-500/20">
                          <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 mr-1.5" />
                          Hoạt động
                        </span>
                      ) : (
                        <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold bg-amber-500/15 text-amber-700 dark:text-amber-400 border border-amber-500/20">
                          <span className="w-1.5 h-1.5 rounded-full bg-amber-500 mr-1.5" />
                          Tạm ngưng
                        </span>
                      )}
                    </td>
                    <td className="py-4 px-6 whitespace-nowrap">
                      <div className="flex flex-wrap items-center gap-1.5">
                        <span
                          className="inline-flex items-center gap-1 px-2 py-0.5 rounded-lg text-xs font-semibold bg-blue-500/10 text-blue-700 dark:text-blue-300 border border-blue-500/20"
                          title="Học viên hoạt động"
                        >
                          <span>🎓</span>
                          <span>{center.activeStudentCount ?? 0}</span>
                        </span>
                        <span
                          className="inline-flex items-center gap-1 px-2 py-0.5 rounded-lg text-xs font-semibold bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border border-emerald-500/20"
                          title="Giáo viên hoạt động"
                        >
                          <span>👨‍🏫</span>
                          <span>{center.activeTeacherCount ?? 0}</span>
                        </span>
                        <span
                          className="inline-flex items-center gap-1 px-2 py-0.5 rounded-lg text-xs font-semibold bg-purple-500/10 text-purple-700 dark:text-purple-300 border border-purple-500/20"
                          title="Lớp học"
                        >
                          <span>🏫</span>
                          <span>{center.classCount ?? 0}</span>
                        </span>
                        <span
                          className="inline-flex items-center gap-1 px-2 py-0.5 rounded-lg text-xs font-semibold bg-amber-500/10 text-amber-700 dark:text-amber-300 border border-amber-500/20"
                          title="Quản lý hoạt động"
                        >
                          <span>👥</span>
                          <span>{center.activeManagerCount ?? 0}</span>
                        </span>
                      </div>
                    </td>
                    <td className="py-4 px-6">
                      {center.primaryManagerDisplayName ? (
                        <div>
                          <div className="font-medium text-slate-800 dark:text-slate-200">
                            {center.primaryManagerDisplayName}
                          </div>
                          <div className="text-xs font-mono text-slate-400">
                            @{center.primaryManagerUsername}
                          </div>
                        </div>
                      ) : (
                        <span className="text-xs text-rose-500 font-medium">Chưa có QL chính</span>
                      )}
                    </td>
                    <td className="py-4 px-6 text-xs text-slate-500 dark:text-slate-400 font-mono">
                      {center.timezone}
                    </td>
                    <td className="py-4 px-6 text-xs text-slate-500 dark:text-slate-400 whitespace-nowrap font-mono">
                      {new Date(center.createdAt).toLocaleDateString("vi-VN", {
                        year: "numeric",
                        month: "2-digit",
                        day: "2-digit",
                      })}
                    </td>
                    <td className="py-4 px-6 text-xs font-mono text-slate-400">
                      v{center.rowVersion}
                    </td>
                    <td className="py-4 px-6 text-right">
                      <div className="flex items-center justify-end gap-2">
                        {/* Manage Personnel Button */}
                        <button
                          type="button"
                          onClick={() => setManagersModalCenter(center)}
                          className="px-3 py-1.5 text-xs font-semibold rounded-lg text-blue-600 dark:text-blue-400 hover:bg-blue-50 dark:hover:bg-blue-900/30 border border-blue-200 dark:border-blue-800/60 transition-colors"
                          title="Xem và quản lý danh sách CenterManager"
                        >
                          Nhân sự ({center.activeManagerCount ?? 0})
                        </button>

                        {/* Edit Metadata Button */}
                        {canManageCenters && (
                          <button
                            type="button"
                            onClick={() => {
                              setEditMetadataCenter(center);
                              setEditCenterName(center.centerName);
                              setEditTimezone(center.timezone);
                              setEditReason("");
                              setEditModalError("");
                              setErrorMessage("");
                            }}
                            className="px-3 py-1.5 text-xs font-semibold rounded-lg text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 border border-slate-200 dark:border-slate-700 transition-colors"
                            title="Chỉnh sửa tên và múi giờ trung tâm"
                          >
                            Sửa
                          </button>
                        )}

                        {/* Status Toggle Button */}
                        {canManageCenters && (
                          <button
                            type="button"
                            onClick={() => {
                              setStatusModalCenter(center);
                              setStatusReason("");
                              setErrorMessage("");
                            }}
                            className={`px-3 py-1.5 text-xs font-semibold rounded-lg border transition-colors ${
                              center.status === "Active"
                                ? "text-amber-700 dark:text-amber-400 hover:bg-amber-50 dark:hover:bg-amber-900/30 border-amber-200 dark:border-amber-800/60"
                                : "text-emerald-700 dark:text-emerald-400 hover:bg-emerald-50 dark:hover:bg-emerald-900/30 border-emerald-200 dark:border-emerald-800/60"
                            }`}
                          >
                            {center.status === "Active" ? "Tạm ngưng" : "Kích hoạt"}
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination Bar */}
        {totalPages > 1 && (
          <div className="py-3 px-6 border-t border-slate-200 dark:border-slate-800 flex items-center justify-between text-xs text-slate-500 dark:text-slate-400">
            <div>
              Hiển thị {centers.length} trên tổng số {totalCount} trung tâm
            </div>
            <div className="flex items-center gap-2">
              <button
                type="button"
                disabled={page <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                className="px-3 py-1.5 rounded-lg border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-800 disabled:opacity-40"
              >
                Trước
              </button>
              <span className="font-semibold text-slate-700 dark:text-slate-200">
                {page} / {totalPages}
              </span>
              <button
                type="button"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                className="px-3 py-1.5 rounded-lg border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-800 disabled:opacity-40"
              >
                Sau
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Modal: Create Center */}
      {isCreateModalOpen && (
        <div
          ref={createModalRef}
          role="dialog"
          aria-modal="true"
          aria-labelledby="create-center-modal-title"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
        >
          <div className="bg-white dark:bg-slate-900 rounded-2xl max-w-lg w-full p-6 shadow-2xl border border-slate-200 dark:border-slate-800 space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="border-b border-slate-200 dark:border-slate-800 pb-3">
              <h3 id="create-center-modal-title" className="text-lg font-bold text-slate-900 dark:text-white">
                Thêm Trung Tâm Giáo Dục Mới
              </h3>
            </div>

            {createModalError && (
              <div
                role="alert"
                className="p-3 rounded-xl bg-rose-500/10 border border-rose-500/20 text-rose-700 dark:text-rose-400 text-xs flex items-start gap-2"
              >
                <span className="shrink-0 text-sm">⚠️</span>
                <span className="flex-1 leading-relaxed">{createModalError}</span>
              </div>
            )}

            <form
              onSubmit={(e) => {
                e.preventDefault();
                setCreateModalError("");
                setErrorMessage("");

                const rawCode = newCenterCode.trim();
                const rawUsername = newManagerUsername.trim();
                const trimmedName = newCenterName.trim();
                const trimmedDisplayName = newManagerDisplayName.trim();

                const normalizedCode = normalizeToAscii(rawCode, true).replace(/\s+/g, "-");
                const normalizedUsername = normalizeToAscii(rawUsername).replace(/\s+/g, "_");

                if (!normalizedCode || normalizedCode.length < 2 || !/^[A-Z0-9_-]+$/.test(normalizedCode)) {
                  setCreateModalError(
                    "Mã trung tâm không hợp lệ (tối thiểu 2 ký tự, chỉ gồm chữ cái, chữ số, gạch nối '-' hoặc gạch dưới '_')."
                  );
                  return;
                }
                if (trimmedName.length < 2) {
                  setCreateModalError("Tên trung tâm không được để trống (tối thiểu 2 ký tự).");
                  return;
                }
                if (!normalizedUsername || normalizedUsername.length < 2 || !/^[a-zA-Z0-9._-]+$/.test(normalizedUsername)) {
                  setCreateModalError(
                    "Tên đăng nhập quản lý không hợp lệ (tối thiểu 2 ký tự, chỉ gồm chữ cái, số, dấu '.', '-' hoặc '_')."
                  );
                  return;
                }
                if (trimmedDisplayName.length < 2) {
                  setCreateModalError("Họ và tên hiển thị quản lý không được để trống (tối thiểu 2 ký tự).");
                  return;
                }
                if (!newManagerPassword || newManagerPassword.length < 12) {
                  setCreateModalError("Mật khẩu ban đầu phải có tối thiểu 12 ký tự.");
                  return;
                }

                createMutation.mutate({
                  centerCode: normalizedCode,
                  centerName: trimmedName,
                  timezone: newTimezone,
                  initialManagerUsername: normalizedUsername,
                  initialManagerDisplayName: trimmedDisplayName,
                  initialManagerPassword: newManagerPassword,
                });
              }}
              className="space-y-4"
            >
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                    Mã Trung Tâm *
                  </label>
                  <input
                    type="text"
                    required
                    placeholder="VD: CENTER_A, HN-01"
                    value={newCenterCode}
                    onChange={(e) => {
                      setNewCenterCode(e.target.value);
                      if (createModalError) setCreateModalError("");
                    }}
                    className="w-full px-3 py-2 text-sm border border-slate-300 dark:border-slate-700 rounded-xl bg-slate-50 dark:bg-slate-800 uppercase font-mono text-slate-900 dark:text-white"
                  />
                  {newCenterCode.trim() && normalizeToAscii(newCenterCode.trim(), true).replace(/\s+/g, "-") !== newCenterCode.trim() ? (
                    <p className="mt-1 text-[11px] text-blue-600 dark:text-blue-400 font-medium">
                      ✓ Sẽ lưu chuẩn hóa: <strong className="font-mono">{normalizeToAscii(newCenterCode.trim(), true).replace(/\s+/g, "-")}</strong>
                    </p>
                  ) : (
                    <p className="mt-1 text-[11px] text-slate-500 dark:text-slate-400">
                      Chữ in hoa không dấu, số, gạch nối (-), gạch dưới (_)
                    </p>
                  )}
                </div>
                <div>
                  <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                    Múi Giờ
                  </label>
                  <select
                    value={newTimezone}
                    onChange={(e) => setNewTimezone(e.target.value)}
                    className="w-full px-3 py-2 text-sm border border-slate-300 dark:border-slate-700 rounded-xl bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white"
                  >
                    <option value="Asia/Bangkok">Asia/Bangkok (GMT+7)</option>
                    <option value="Asia/Ho_Chi_Minh">Asia/Ho_Chi_Minh (GMT+7)</option>
                    <option value="Asia/Singapore">Asia/Singapore (GMT+8)</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                  Tên Trung Tâm *
                </label>
                <input
                  type="text"
                  required
                  placeholder="VD: Trung tâm Ngoại ngữ Tin học EduTwin"
                  value={newCenterName}
                  onChange={(e) => {
                    setNewCenterName(e.target.value);
                    if (createModalError) setCreateModalError("");
                  }}
                  className="w-full px-3 py-2 text-sm border border-slate-300 dark:border-slate-700 rounded-xl bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white"
                />
              </div>

              <div className="border-t border-slate-200 dark:border-slate-800 pt-3">
                <h4 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-2">
                  Tài Khoản Quản Lý Ban Đầu
                </h4>
                <div className="space-y-3">
                  <div>
                    <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                      Tên Đăng Nhập Quản Lý *
                    </label>
                    <input
                      type="text"
                      required
                      placeholder="VD: manager_primary, db"
                      value={newManagerUsername}
                      onChange={(e) => {
                        setNewManagerUsername(e.target.value);
                        if (createModalError) setCreateModalError("");
                      }}
                      className="w-full px-3 py-2 text-sm border border-slate-300 dark:border-slate-700 rounded-xl bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white"
                    />
                    {newManagerUsername.trim() && normalizeToAscii(newManagerUsername.trim()).replace(/\s+/g, "_") !== newManagerUsername.trim() ? (
                      <p className="mt-1 text-[11px] text-blue-600 dark:text-blue-400 font-medium">
                        ✓ Sẽ lưu chuẩn hóa: <strong className="font-mono">{normalizeToAscii(newManagerUsername.trim()).replace(/\s+/g, "_")}</strong>
                      </p>
                    ) : (
                      <p className="mt-1 text-[11px] text-slate-500 dark:text-slate-400">
                        Chữ cái, số, dấu chấm (.), gạch nối (-), gạch dưới (_)
                      </p>
                    )}
                  </div>
                  <div>
                    <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                      Họ Và Tên Quản Lý *
                    </label>
                    <input
                      type="text"
                      required
                      placeholder="VD: Nguyễn Văn Quản Trị"
                      value={newManagerDisplayName}
                      onChange={(e) => {
                        setNewManagerDisplayName(e.target.value);
                        if (createModalError) setCreateModalError("");
                      }}
                      className="w-full px-3 py-2 text-sm border border-slate-300 dark:border-slate-700 rounded-xl bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                      Mật Khẩu Ban Đầu (tối thiểu 12 ký tự) *
                    </label>
                    <div className="relative">
                      <input
                        type={showCreatePassword ? "text" : "password"}
                        required
                        minLength={12}
                        placeholder="Mật khẩu bảo mật tối thiểu 12 ký tự..."
                        value={newManagerPassword}
                        onChange={(e) => {
                          setNewManagerPassword(e.target.value);
                          if (createModalError) setCreateModalError("");
                        }}
                        className="w-full px-3 py-2 pr-16 text-sm border border-slate-300 dark:border-slate-700 rounded-xl bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white"
                      />
                      <button
                        type="button"
                        onClick={() => setShowCreatePassword(!showCreatePassword)}
                        aria-label={showCreatePassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                        className="absolute right-2.5 top-1/2 -translate-y-1/2 px-2 py-0.5 text-xs text-slate-500 hover:text-slate-700 dark:hover:text-slate-300 bg-slate-200/60 dark:bg-slate-700/60 rounded-md transition-colors"
                      >
                        {showCreatePassword ? "Ẩn" : "Hiện"}
                      </button>
                    </div>
                    <div className="mt-1 flex justify-between items-center text-[11px]">
                      <span className="text-slate-500 dark:text-slate-400">
                        Bảo mật cấp Control Plane: tối thiểu 12 ký tự
                      </span>
                      <span
                        className={
                          newManagerPassword.length >= 12
                            ? "text-emerald-600 dark:text-emerald-400 font-semibold"
                            : "text-amber-600 dark:text-amber-400"
                        }
                      >
                        {newManagerPassword.length}/12 ký tự
                      </span>
                    </div>
                  </div>
                </div>
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t border-slate-200 dark:border-slate-800">
                <button
                  type="button"
                  onClick={() => {
                    setIsCreateModalOpen(false);
                    setCreateModalError("");
                  }}
                  className="px-4 py-2 text-sm font-medium text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-xl"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={createMutation.isPending}
                  className="px-5 py-2 text-sm font-semibold text-white bg-blue-600 hover:bg-blue-500 rounded-xl disabled:opacity-50 transition-colors shadow-sm cursor-pointer"
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
        <div
          ref={statusModalRef}
          role="dialog"
          aria-modal="true"
          aria-labelledby="status-modal-title"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
        >
          <div className="bg-white dark:bg-slate-900 rounded-2xl max-w-md w-full p-6 shadow-2xl border border-slate-200 dark:border-slate-800 space-y-4">
            <h3 id="status-modal-title" className="text-lg font-bold text-slate-900 dark:text-white">
              Xác Nhận Đổi Trạng Thái
            </h3>
            <p className="text-sm text-slate-600 dark:text-slate-300">
              Bạn có chắc chắn muốn chuyển trạng thái trung tâm{" "}
              <strong>{statusModalCenter.centerName}</strong> ({statusModalCenter.centerCode}) từ{" "}
              <span className="font-semibold">{statusModalCenter.status}</span> sang{" "}
              <span className="font-semibold text-blue-600 dark:text-blue-400">
                {statusModalCenter.status === "Active" ? "Tạm ngưng (Suspended)" : "Kích hoạt (Active)"}
              </span>
              ?
            </p>
            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                Lý do thay đổi trạng thái (tối thiểu 5 ký tự) *
              </label>
              <textarea
                required
                minLength={5}
                value={statusReason}
                onChange={(e) => setStatusReason(e.target.value)}
                placeholder="Nhập lý do thay đổi theo quyết định ban quản trị..."
                rows={3}
                className="w-full px-3 py-2 text-sm border border-slate-300 dark:border-slate-700 rounded-xl bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white"
              />
            </div>
            <div className="flex justify-end gap-3 pt-3 border-t border-slate-200 dark:border-slate-800">
              <button
                type="button"
                onClick={() => setStatusModalCenter(null)}
                className="px-4 py-2 text-sm font-medium text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-xl"
              >
                Hủy
              </button>
              <button
                type="button"
                onClick={handleToggleStatusConfirm}
                disabled={updateStatusMutation.isPending || statusReason.trim().length < 5}
                className="px-5 py-2 text-sm font-semibold text-white bg-blue-600 hover:bg-blue-500 rounded-xl disabled:opacity-50 transition-colors"
              >
                {updateStatusMutation.isPending ? "Đang cập nhật..." : "Xác nhận"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Center Managers Lifecycle */}
      {managersModalCenter && (
        <CenterManagersModal
          isOpen={!!managersModalCenter}
          onClose={() => setManagersModalCenter(null)}
          center={centers.find((c) => c.centerId === managersModalCenter.centerId) || managersModalCenter}
          onCenterUpdated={() => {
            refetch();
          }}
        />
      )}

      {/* Modal: Edit Metadata */}
      {editMetadataCenter && (
        <div
          ref={editMetadataModalRef}
          role="dialog"
          aria-modal="true"
          aria-labelledby="edit-metadata-modal-title"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
        >
          <div className="bg-white dark:bg-slate-900 rounded-2xl max-w-lg w-full p-6 shadow-2xl border border-slate-200 dark:border-slate-800 space-y-4">
            <div className="border-b border-slate-200 dark:border-slate-800 pb-3">
              <h3 id="edit-metadata-modal-title" className="text-lg font-bold text-slate-900 dark:text-white">
                Sửa Thông Tin Trung Tâm
              </h3>
            </div>

            {editModalError && (
              <div
                role="alert"
                className="p-3 rounded-xl bg-rose-500/10 border border-rose-500/20 text-rose-700 dark:text-rose-400 text-xs flex items-start gap-2"
              >
                <span className="shrink-0 text-sm">⚠️</span>
                <span className="flex-1 leading-relaxed">{editModalError}</span>
              </div>
            )}
            <form onSubmit={handleEditMetadataConfirm} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                  Mã Trung Tâm (Bất biến)
                </label>
                <input
                  type="text"
                  disabled
                  value={editMetadataCenter.centerCode}
                  className="w-full px-3 py-2 text-sm border rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-400 font-mono cursor-not-allowed"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                  Tên Trung Tâm *
                </label>
                <input
                  type="text"
                  required
                  minLength={3}
                  maxLength={200}
                  value={editCenterName}
                  onChange={(e) => setEditCenterName(e.target.value)}
                  placeholder="Nhập tên trung tâm..."
                  className="w-full px-3 py-2 text-sm border border-slate-300 dark:border-slate-700 rounded-xl bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                  Múi Giờ *
                </label>
                <select
                  value={editTimezone}
                  onChange={(e) => setEditTimezone(e.target.value)}
                  className="w-full px-3 py-2 text-sm border border-slate-300 dark:border-slate-700 rounded-xl bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white"
                >
                  <option value="Asia/Bangkok">Asia/Bangkok (GMT+7)</option>
                  <option value="Asia/Ho_Chi_Minh">Asia/Ho_Chi_Minh (GMT+7)</option>
                  <option value="Asia/Singapore">Asia/Singapore (GMT+8)</option>
                  <option value="Asia/Tokyo">Asia/Tokyo (GMT+9)</option>
                  <option value="UTC">UTC (GMT+0)</option>
                </select>
              </div>
              <div>
                <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                  Lý do cập nhật *
                </label>
                <textarea
                  required
                  minLength={3}
                  value={editReason}
                  onChange={(e) => setEditReason(e.target.value)}
                  placeholder="Nhập lý do thay đổi thông tin theo quyết định quản trị..."
                  rows={2}
                  className="w-full px-3 py-2 text-sm border border-slate-300 dark:border-slate-700 rounded-xl bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white"
                />
              </div>
              <div className="flex justify-end gap-3 pt-3 border-t border-slate-200 dark:border-slate-800">
                <button
                  type="button"
                  onClick={() => setEditMetadataCenter(null)}
                  className="px-4 py-2 text-sm font-medium text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-xl"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={updateMetadataMutation.isPending}
                  className="px-5 py-2 text-sm font-semibold text-white bg-blue-600 hover:bg-blue-500 rounded-xl disabled:opacity-50 transition-colors shadow-sm"
                >
                  {updateMetadataMutation.isPending ? "Đang lưu..." : "Lưu thay đổi"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
