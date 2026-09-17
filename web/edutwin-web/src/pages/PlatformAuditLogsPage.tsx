import React, { useState, useRef } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { platformApi } from "../api/platformApi";
import type { PlatformAuditItem, PlatformAuditQuery } from "../types/platform";
import { useModalAccessibility } from "../utils/useModalAccessibility";
import { AUDIT_ACTION_MAP } from "../utils/platformPresentation";

export const PlatformAuditLogsPage: React.FC = () => {
  const [page, setPage] = useState<number>(1);
  const pageSize = 20;

  // Filter states
  const [actionType, setActionType] = useState<string>("");
  const [targetType, setTargetType] = useState<string>("");
  const [search, setSearch] = useState<string>("");
  const [fromUtc, setFromUtc] = useState<string>("");
  const [toUtc, setToUtc] = useState<string>("");

  // Input states for form submission
  const [searchInput, setSearchInput] = useState<string>("");
  const [actionTypeInput, setActionTypeInput] = useState<string>("");
  const [targetTypeInput, setTargetTypeInput] = useState<string>("");
  const [fromUtcInput, setFromUtcInput] = useState<string>("");
  const [toUtcInput, setToUtcInput] = useState<string>("");

  // Selected item for detail view
  const [selectedAudit, setSelectedAudit] = useState<PlatformAuditItem | null>(null);
  const [copiedTraceId, setCopiedTraceId] = useState<string | null>(null);

  // Detail Modal Accessibility
  const detailModalRef = useRef<HTMLDivElement>(null);
  useModalAccessibility({
    isOpen: !!selectedAudit,
    onClose: () => setSelectedAudit(null),
    containerRef: detailModalRef,
  });

  const queryParams: PlatformAuditQuery = {
    page,
    pageSize,
    actionType: actionType || undefined,
    targetType: targetType || undefined,
    search: search || undefined,
    fromUtc: fromUtc ? new Date(fromUtc).toISOString() : undefined,
    toUtc: toUtc ? new Date(toUtc).toISOString() : undefined,
  };

  const { data, isLoading, isError, error, refetch } = useQuery({
    queryKey: ["platform-audit-logs", page, pageSize, actionType, targetType, search, fromUtc, toUtc],
    queryFn: () => platformApi.listAuditLogs(queryParams),
  });

  const isForbidden = isAxiosError(error) && error.response?.status === 403;
  const isRateLimit = isAxiosError(error) && error.response?.status === 429;

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setSearch(searchInput);
    setActionType(actionTypeInput);
    setTargetType(targetTypeInput);
    setFromUtc(fromUtcInput);
    setToUtc(toUtcInput);
    setPage(1);
  };

  const handleResetFilters = () => {
    setSearchInput("");
    setActionTypeInput("");
    setTargetTypeInput("");
    setFromUtcInput("");
    setToUtcInput("");
    setSearch("");
    setActionType("");
    setTargetType("");
    setFromUtc("");
    setToUtc("");
    setPage(1);
  };

  const copyToClipboard = (text: string, traceId: string) => {
    navigator.clipboard.writeText(text);
    setCopiedTraceId(traceId);
    setTimeout(() => {
      setCopiedTraceId(null);
    }, 2000);
  };

  const getActionBadgeColor = (action: string) => {
    if (action.includes("Created")) {
      return "bg-emerald-500/15 text-emerald-700 dark:text-emerald-400 border-emerald-500/20";
    }
    if (action.includes("StatusChanged") || action.includes("StatusUpdated")) {
      return "bg-amber-500/15 text-amber-700 dark:text-amber-400 border-amber-500/20";
    }
    if (action.includes("PrimaryManagerChanged")) {
      return "bg-indigo-500/15 text-indigo-700 dark:text-indigo-400 border-indigo-500/20";
    }
    if (action.includes("PasswordReset")) {
      return "bg-purple-500/15 text-purple-700 dark:text-purple-400 border-purple-500/20";
    }
    return "bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 border-slate-200 dark:border-slate-700";
  };

  const formatDate = (dateString: string) => {
    try {
      const d = new Date(dateString);
      return d.toLocaleString("vi-VN", {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
        hour12: false,
      });
    } catch {
      return dateString;
    }
  };

  const items = data?.items || [];
  const totalCount = data?.totalCount || 0;
  const totalPages = Math.ceil(totalCount / pageSize) || 1;

  const isFiltered = Boolean(search || actionType || targetType || fromUtc || toUtc);
  const kpiLabel = isFiltered ? "Kết quả theo bộ lọc" : "Tổng số bản ghi kiểm toán";

  return (
    <div className="max-w-7xl mx-auto space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-slate-900 dark:text-white">
          Nhật Ký Kiểm Toán Nền Tảng (Audit Logs)
        </h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
          Truy vết toàn bộ hoạt động quản trị cross-tenant, được bảo mật và khử khuẩn dữ liệu nhạy cảm.
        </p>
      </div>

      {/* Security & Audit Principles Banner */}
      <div className="p-4 rounded-2xl bg-indigo-500/10 border border-indigo-500/20 text-slate-800 dark:text-slate-200 flex items-start gap-3 shadow-xs">
        <div className="p-2 rounded-xl bg-indigo-500/15 text-indigo-600 dark:text-indigo-400 shrink-0 mt-0.5">
          <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
          </svg>
        </div>
        <div className="text-xs sm:text-sm">
          <span className="font-semibold text-indigo-900 dark:text-indigo-300">Tính Bất Biến & An Toàn: </span>
          <span className="text-slate-600 dark:text-slate-400">
            Mọi thao tác quản trị trên nền tảng đều sinh ra bản ghi kiểm toán bất biến (Immutable), gắn liền với Trace ID và tự động khử khuẩn dữ liệu nhạy cảm (mật khẩu, khóa bí mật).
          </span>
        </div>
      </div>

      {/* 403 Forbidden State */}
      {isForbidden && (
        <div className="p-8 rounded-2xl bg-rose-500/10 border border-rose-500/20 text-center">
          <span className="text-3xl">🚫</span>
          <h2 className="mt-2 text-lg font-bold text-rose-600 dark:text-rose-400">403 - Quyền truy cập bị từ chối</h2>
          <p className="mt-1 text-sm text-slate-600 dark:text-slate-400">
            Tài khoản của bạn không có quyền xem nhật ký kiểm toán (platform.audit.read).
          </p>
          <Link to="/" className="mt-4 inline-block px-4 py-2 bg-slate-800 text-white text-xs font-semibold rounded-lg">
            Về trang chủ
          </Link>
        </div>
      )}

      {/* 429 Rate Limit State */}
      {isRateLimit && (
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

      {/* Quick Stat Pill */}
      <div className="flex items-center justify-between p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs">
        <div className="flex items-center gap-3">
          <span className="text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400">
            {kpiLabel}:
          </span>
          <span className="text-xl font-bold text-slate-900 dark:text-white">
            {isLoading ? "..." : totalCount}
          </span>
          {isFiltered && (
            <span className="text-xs text-slate-400">
              (khớp bộ lọc)
            </span>
          )}
        </div>
        <div className="text-xs text-slate-400">
          Trang {page} / {totalPages}
        </div>
      </div>

      {/* Filter Bar */}
      <form onSubmit={handleSearchSubmit} className="bg-white dark:bg-slate-900 p-5 rounded-2xl shadow-xs border border-slate-200 dark:border-slate-800 space-y-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-5 gap-3">
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">
              Loại hành động
            </label>
            <select
              value={actionTypeInput}
              onChange={(e) => setActionTypeInput(e.target.value)}
              className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-slate-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="">-- Tất cả hành động --</option>
              <option value="CenterCreated">Khởi tạo trung tâm (CenterCreated)</option>
              <option value="CenterStatusUpdated">Đổi trạng thái trung tâm (CenterStatusUpdated)</option>
              <option value="CenterMetadataUpdated">Sửa thông tin trung tâm (CenterMetadataUpdated)</option>
              <option value="CenterManagerCreated">Thêm quản lý mới (CenterManagerCreated)</option>
              <option value="CenterManagerStatusUpdated">Đổi trạng thái quản lý (CenterManagerStatusUpdated)</option>
              <option value="CenterPrimaryManagerChanged">Chuyển quản lý chính (CenterPrimaryManagerChanged)</option>
              <option value="CenterManagerPasswordReset">Đặt lại mật khẩu quản lý (CenterManagerPasswordReset)</option>
              <option value="PlatformAdminPasswordChanged">Admin đổi mật khẩu (PlatformAdminPasswordChanged)</option>
              <option value="PlatformAdminSessionsRevoked">Admin thu hồi phiên (PlatformAdminSessionsRevoked)</option>
            </select>
          </div>

          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">
              Tài nguyên tác động
            </label>
            <select
              value={targetTypeInput}
              onChange={(e) => setTargetTypeInput(e.target.value)}
              className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-slate-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="">-- Tất cả tài nguyên --</option>
              <option value="Center">Trung tâm (Center)</option>
              <option value="User">Người dùng / Quản lý (User)</option>
              <option value="Session">Phiên đăng nhập (Session)</option>
            </select>
          </div>

          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">
              Từ khóa / Trace ID
            </label>
            <input
              type="text"
              placeholder="Username, lý do, TraceId..."
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-slate-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">
              Từ thời điểm (UTC)
            </label>
            <input
              type="datetime-local"
              value={fromUtcInput}
              onChange={(e) => setFromUtcInput(e.target.value)}
              className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-slate-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">
              Đến thời điểm (UTC)
            </label>
            <input
              type="datetime-local"
              value={toUtcInput}
              onChange={(e) => setToUtcInput(e.target.value)}
              className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-slate-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
        </div>

        <div className="flex justify-end gap-2 pt-2 border-t border-slate-100 dark:border-slate-800">
          <button
            type="button"
            onClick={handleResetFilters}
            className="px-4 py-2 text-xs font-semibold text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white rounded-xl hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
          >
            Đặt lại bộ lọc
          </button>
          <button
            type="submit"
            className="px-5 py-2 text-xs font-semibold text-white bg-slate-800 dark:bg-slate-700 hover:bg-slate-900 dark:hover:bg-slate-600 rounded-xl transition-colors cursor-pointer"
          >
            Áp dụng lọc
          </button>
        </div>
      </form>

      {/* Audit Logs Table */}
      <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-xs border border-slate-200 dark:border-slate-800 overflow-hidden">
        {isLoading ? (
          <div className="p-12 text-center text-slate-500 dark:text-slate-400">
            <svg className="animate-spin h-8 w-8 mx-auto mb-3 text-blue-600" viewBox="0 0 24 24" fill="none">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
            </svg>
            Đang tải dữ liệu kiểm toán...
          </div>
        ) : isError ? (
          <div className="p-12 text-center text-rose-500">
            Có lỗi xảy ra khi tải danh sách nhật ký kiểm toán.
          </div>
        ) : items.length === 0 ? (
          <div className="p-16 text-center text-slate-500 dark:text-slate-400">
            <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-indigo-50 dark:bg-indigo-900/30 flex items-center justify-center text-indigo-600 dark:text-indigo-400">
              <svg className="w-8 h-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
              </svg>
            </div>
            <h3 className="text-lg font-bold text-slate-900 dark:text-white mb-1">
              Không tìm thấy bản ghi kiểm toán nào
            </h3>
            <p className="text-slate-500 dark:text-slate-400 text-sm max-w-sm mx-auto">
              Không có sự kiện quản trị nào khớp với các tiêu chí tìm kiếm hiện tại.
            </p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 dark:border-slate-800 bg-slate-50/75 dark:bg-slate-800/50 text-[11px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  <th className="py-3.5 px-6">ID & Thời Gian</th>
                  <th className="py-3.5 px-6">Hành Động</th>
                  <th className="py-3.5 px-6">Người Thực Hiện</th>
                  <th className="py-3.5 px-6">Đối Tượng</th>
                  <th className="py-3.5 px-6">Lý Do</th>
                  <th className="py-3.5 px-6 text-right">Thao Tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800/60 text-sm">
                {items.map((audit) => (
                  <tr key={audit.auditId} className="hover:bg-slate-50/50 dark:hover:bg-slate-800/30 transition-colors">
                    <td className="py-3.5 px-6 whitespace-nowrap">
                      <div className="font-mono text-xs font-semibold text-slate-900 dark:text-white">
                        #{audit.auditId}
                      </div>
                      <div className="text-[11px] text-slate-400 mt-0.5">
                        {formatDate(audit.createdAt)}
                      </div>
                    </td>
                    <td className="py-3.5 px-6">
                      <div className="flex flex-col gap-1 min-w-[190px]">
                        <span className="font-semibold text-xs text-slate-900 dark:text-slate-100">
                          {AUDIT_ACTION_MAP[audit.actionType] || audit.actionType}
                        </span>
                        <span className={`inline-flex items-center w-fit px-2 py-0.5 rounded-full text-[10px] font-mono border ${getActionBadgeColor(audit.actionType)}`}>
                          {audit.actionType}
                        </span>
                      </div>
                    </td>
                    <td className="py-3.5 px-6">
                      <div className="font-medium text-slate-900 dark:text-white">
                        {audit.actorUsername ? `@${audit.actorUsername}` : "Hệ thống"}
                      </div>
                      <div className="text-[11px] font-mono text-slate-400 truncate max-w-[140px]" title={audit.actorUserId || ""}>
                        {audit.actorUserId ? audit.actorUserId.substring(0, 8) + "..." : "-"}
                      </div>
                    </td>
                    <td className="py-3.5 px-6">
                      <div className="font-medium text-slate-900 dark:text-white">
                        {audit.targetType}
                      </div>
                      <div className="text-[11px] text-slate-400">
                        {audit.targetCenterCode ? (
                          <span className="font-semibold text-blue-600 dark:text-blue-400">[{audit.targetCenterCode}]</span>
                        ) : null}
                        {audit.targetCenterName ? (
                          <span className="text-slate-700 dark:text-slate-300 font-medium ml-1">
                            {audit.targetCenterName}
                          </span>
                        ) : null}{" "}
                        <span className="font-mono">{audit.targetId ? audit.targetId.substring(0, 8) + "..." : ""}</span>
                      </div>
                    </td>
                    <td className="py-3.5 px-6 max-w-xs">
                      <div className="text-xs text-slate-600 dark:text-slate-300 truncate" title={audit.reason}>
                        {audit.reason || "-"}
                      </div>
                      <div className="text-[10px] font-mono text-slate-400 flex items-center gap-1 mt-0.5">
                        <span>Trace: {audit.traceId.substring(0, 10)}...</span>
                        <button
                          type="button"
                          onClick={() => copyToClipboard(audit.traceId, audit.auditId)}
                          className="hover:text-blue-500 cursor-pointer"
                          title="Sao chép TraceId"
                        >
                          {copiedTraceId === audit.auditId ? "✓" : "📋"}
                        </button>
                      </div>
                    </td>
                    <td className="py-3.5 px-6 text-right whitespace-nowrap">
                      <button
                        type="button"
                        onClick={() => setSelectedAudit(audit)}
                        className="px-3 py-1.5 text-xs font-semibold rounded-lg bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-200 transition-colors"
                      >
                        Chi tiết
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="py-3.5 px-6 border-t border-slate-200 dark:border-slate-800 flex items-center justify-between text-xs text-slate-500 dark:text-slate-400">
            <div>
              Hiển thị {items.length} trên tổng số {totalCount} bản ghi
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

      {/* Detail Modal with Focus Trap & Escape */}
      {selectedAudit && (
        <div
          ref={detailModalRef}
          role="dialog"
          aria-modal="true"
          aria-labelledby="audit-detail-modal-title"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
        >
          <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-2xl border border-slate-200 dark:border-slate-800 max-w-3xl w-full max-h-[90vh] flex flex-col overflow-hidden">
            {/* Modal Header */}
            <div className="p-5 border-b border-slate-200 dark:border-slate-800 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <span className={`px-2.5 py-1 rounded-full border text-xs font-bold ${getActionBadgeColor(selectedAudit.actionType)}`}>
                  {AUDIT_ACTION_MAP[selectedAudit.actionType] || selectedAudit.actionType} ({selectedAudit.actionType})
                </span>
                <h3 id="audit-detail-modal-title" className="text-base font-bold text-slate-900 dark:text-white">
                  Chi Tiết Bản Ghi Kiểm Toán #{selectedAudit.auditId}
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setSelectedAudit(null)}
                aria-label="Đóng"
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 text-lg p-1 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-800"
              >
                ✕
              </button>
            </div>

            {/* Modal Body */}
            <div className="p-6 overflow-y-auto space-y-4 text-xs">
              {/* Metadata Grid */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 p-4 bg-slate-50 dark:bg-slate-800/60 rounded-xl border border-slate-200 dark:border-slate-700">
                <div>
                  <span className="text-slate-500 dark:text-slate-400 block">Thời gian ghi nhận:</span>
                  <span className="font-semibold text-slate-900 dark:text-white">{formatDate(selectedAudit.createdAt)}</span>
                </div>
                <div>
                  <span className="text-slate-500 dark:text-slate-400 block">Người thực hiện:</span>
                  <span className="font-semibold text-slate-900 dark:text-white">
                    {selectedAudit.actorUsername || selectedAudit.actorUserId || "Hệ thống"}
                  </span>
                </div>
                <div>
                  <span className="text-slate-500 dark:text-slate-400 block">Trung tâm bị tác động:</span>
                  <span className="font-semibold text-slate-900 dark:text-white">
                    {selectedAudit.targetCenterName
                      ? `${selectedAudit.targetCenterName} [${selectedAudit.targetCenterCode || "-"}] (${selectedAudit.targetCenterId})`
                      : selectedAudit.targetCenterCode
                      ? `${selectedAudit.targetCenterCode} (${selectedAudit.targetCenterId})`
                      : selectedAudit.targetCenterId || "Root Tenant PLATFORM"}
                  </span>
                </div>
                <div>
                  <span className="text-slate-500 dark:text-slate-400 block">Tài nguyên tác động:</span>
                  <span className="font-semibold text-slate-900 dark:text-white">
                    {selectedAudit.targetType} ({selectedAudit.targetId})
                  </span>
                </div>
                <div className="sm:col-span-2">
                  <span className="text-slate-500 dark:text-slate-400 block">Lý do hành động:</span>
                  <p className="font-medium text-slate-900 dark:text-white mt-0.5">{selectedAudit.reason}</p>
                </div>
                <div className="sm:col-span-2 flex items-center justify-between">
                  <div>
                    <span className="text-slate-500 dark:text-slate-400 block">Trace ID:</span>
                    <span className="font-mono text-slate-700 dark:text-slate-300">{selectedAudit.traceId}</span>
                  </div>
                  <button
                    type="button"
                    onClick={() => copyToClipboard(selectedAudit.traceId, "modal")}
                    className="px-2.5 py-1 rounded-lg bg-slate-200 dark:bg-slate-700 hover:bg-slate-300 dark:hover:bg-slate-600 font-semibold"
                  >
                    {copiedTraceId === "modal" ? "Đã chép!" : "Sao chép Trace ID"}
                  </button>
                </div>
              </div>

              {/* Data Redaction Notice */}
              <div className="p-3 rounded-xl bg-blue-500/10 border border-blue-500/20 text-blue-800 dark:text-blue-300 flex items-center gap-2">
                <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
                </svg>
                <span>Dữ liệu kiểm toán được khử khuẩn tự động (mật khẩu, khóa bảo mật, token không bao giờ xuất hiện).</span>
              </div>

              {/* Before & After Data Diff */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                <div>
                  <h4 className="font-semibold text-slate-700 dark:text-slate-300 mb-1.5 flex items-center gap-1.5">
                    <span className="w-2 h-2 rounded-full bg-rose-400" />
                    Trạng thái trước thay đổi (BeforeData)
                  </h4>
                  <pre className="p-3 bg-slate-950 text-slate-200 rounded-xl overflow-x-auto text-[11px] font-mono max-h-56">
                    {selectedAudit.beforeData ? JSON.stringify(selectedAudit.beforeData, null, 2) : "// Không có dữ liệu trước thay đổi"}
                  </pre>
                </div>
                <div>
                  <h4 className="font-semibold text-slate-700 dark:text-slate-300 mb-1.5 flex items-center gap-1.5">
                    <span className="w-2 h-2 rounded-full bg-emerald-400" />
                    Trạng thái sau thay đổi (AfterData)
                  </h4>
                  <pre className="p-3 bg-slate-950 text-slate-200 rounded-xl overflow-x-auto text-[11px] font-mono max-h-56">
                    {selectedAudit.afterData ? JSON.stringify(selectedAudit.afterData, null, 2) : "// Không có dữ liệu sau thay đổi"}
                  </pre>
                </div>
              </div>
            </div>

            {/* Modal Footer */}
            <div className="p-4 border-t border-slate-200 dark:border-slate-800 flex items-center bg-slate-50 dark:bg-slate-800/80">
              <span className="text-[11px] text-slate-500 dark:text-slate-400 italic">
                Bản ghi kiểm toán có tính bất biến (Immutable), không thể sửa đổi hoặc xóa.
              </span>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
