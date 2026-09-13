import React, { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { platformApi } from "../api/platformApi";
import type { PlatformAuditItem, PlatformAuditQuery } from "../types/platform";

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

  const queryParams: PlatformAuditQuery = {
    page,
    pageSize,
    actionType: actionType || undefined,
    targetType: targetType || undefined,
    search: search || undefined,
    fromUtc: fromUtc ? new Date(fromUtc).toISOString() : undefined,
    toUtc: toUtc ? new Date(toUtc).toISOString() : undefined,
  };

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ["platform-audit-logs", page, pageSize, actionType, targetType, search, fromUtc, toUtc],
    queryFn: () => platformApi.listAuditLogs(queryParams),
  });

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
      return "bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300 border-green-200 dark:border-green-800";
    }
    if (action.includes("StatusChanged") || action.includes("StatusUpdated")) {
      return "bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300 border-amber-200 dark:border-amber-800";
    }
    if (action.includes("PrimaryManagerChanged")) {
      return "bg-indigo-100 text-indigo-800 dark:bg-indigo-900/40 dark:text-indigo-300 border-indigo-200 dark:border-indigo-800";
    }
    if (action.includes("PasswordReset")) {
      return "bg-purple-100 text-purple-800 dark:bg-purple-900/40 dark:text-purple-300 border-purple-200 dark:border-purple-800";
    }
    return "bg-slate-100 text-slate-800 dark:bg-slate-800 dark:text-slate-300 border-slate-200 dark:border-slate-700";
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

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      {/* Breadcrumb & Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 border-b border-gray-200 dark:border-gray-700 pb-5">
        <div>
          <nav className="text-sm font-medium text-gray-500 dark:text-gray-400 mb-1">
            <span className="hover:text-gray-700 dark:hover:text-gray-200">Quản trị Nền tảng</span>
            <span className="mx-2">/</span>
            <span className="text-blue-600 dark:text-blue-400">Nhật ký Kiểm toán</span>
          </nav>
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">
            Nhật Ký Kiểm Toán Nền Tảng (Audit Logs)
          </h1>
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
            Truy vết toàn bộ hoạt động quản trị cross-tenant, được bảo mật và khử khuẩn dữ liệu nhạy cảm.
          </p>
        </div>

        {/* Navigation Tabs */}
        <div className="inline-flex rounded-lg p-1 bg-gray-100 dark:bg-gray-800 border border-gray-200 dark:border-gray-700">
          <Link
            to="/quan-tri-nen-tang/trung-tam"
            className="px-4 py-2 text-xs font-semibold rounded-md text-gray-700 dark:text-gray-300 hover:text-gray-900 dark:hover:text-white transition-colors"
          >
            Trung tâm đối tác
          </Link>
          <Link
            to="/quan-tri-nen-tang/nhat-ky"
            className="px-4 py-2 text-xs font-semibold rounded-md bg-white dark:bg-gray-700 text-blue-600 dark:text-blue-400 shadow-sm"
          >
            Nhật ký kiểm toán
          </Link>
        </div>
      </div>

      {/* Filter Bar */}
      <form onSubmit={handleSearchSubmit} className="bg-white dark:bg-gray-800 p-4 rounded-xl shadow-sm border border-gray-200 dark:border-gray-700 space-y-3">
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-5 gap-3">
          <div>
            <label className="block text-xs font-medium text-gray-700 dark:text-gray-300 mb-1">
              Loại hành động
            </label>
            <select
              value={actionTypeInput}
              onChange={(e) => setActionTypeInput(e.target.value)}
              className="w-full px-3 py-1.5 text-xs rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100"
            >
              <option value="">-- Tất cả hành động --</option>
              <option value="CenterCreated">Khởi tạo trung tâm (CenterCreated)</option>
              <option value="CenterStatusUpdated">Đổi trạng thái trung tâm (CenterStatusUpdated)</option>
              <option value="CenterManagerCreated">Tạo quản lý trung tâm (CenterManagerCreated)</option>
              <option value="CenterManagerStatusChanged:Active">Kích hoạt quản lý (StatusChanged:Active)</option>
              <option value="CenterManagerStatusChanged:Suspended">Khóa quản lý (StatusChanged:Suspended)</option>
              <option value="CenterManagerStatusChanged:Disabled">Vô hiệu hóa quản lý (StatusChanged:Disabled)</option>
              <option value="CenterPrimaryManagerChanged">Chuyển quản lý chính (PrimaryManagerChanged)</option>
              <option value="CenterManagerPasswordReset">Đặt lại mật khẩu quản lý (PasswordReset)</option>
            </select>
          </div>

          <div>
            <label className="block text-xs font-medium text-gray-700 dark:text-gray-300 mb-1">
              Đối tượng tác động
            </label>
            <select
              value={targetTypeInput}
              onChange={(e) => setTargetTypeInput(e.target.value)}
              className="w-full px-3 py-1.5 text-xs rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100"
            >
              <option value="">-- Tất cả đối tượng --</option>
              <option value="Center">Trung tâm (Center)</option>
              <option value="User">Người dùng (User)</option>
              <option value="CenterManager">Quản lý trung tâm (CenterManager)</option>
            </select>
          </div>

          <div>
            <label className="block text-xs font-medium text-gray-700 dark:text-gray-300 mb-1">
              Từ thời điểm
            </label>
            <input
              type="datetime-local"
              value={fromUtcInput}
              onChange={(e) => setFromUtcInput(e.target.value)}
              className="w-full px-3 py-1.5 text-xs rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100"
            />
          </div>

          <div>
            <label className="block text-xs font-medium text-gray-700 dark:text-gray-300 mb-1">
              Đến thời điểm
            </label>
            <input
              type="datetime-local"
              value={toUtcInput}
              onChange={(e) => setToUtcInput(e.target.value)}
              className="w-full px-3 py-1.5 text-xs rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100"
            />
          </div>

          <div>
            <label className="block text-xs font-medium text-gray-700 dark:text-gray-300 mb-1">
              Tìm kiếm từ khóa
            </label>
            <input
              type="text"
              placeholder="Lý do, targetId, mã TT..."
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              className="w-full px-3 py-1.5 text-xs rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100"
            />
          </div>
        </div>

        <div className="flex justify-end gap-2 pt-2 border-t border-gray-100 dark:border-gray-700">
          <button
            type="button"
            onClick={handleResetFilters}
            className="px-3 py-1.5 text-xs font-medium rounded-lg text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 border border-gray-300 dark:border-gray-600"
          >
            Đặt lại bộ lọc
          </button>
          <button
            type="submit"
            className="px-4 py-1.5 text-xs font-semibold rounded-lg bg-blue-600 hover:bg-blue-700 text-white shadow-sm transition-colors"
          >
            Áp dụng bộ lọc
          </button>
        </div>
      </form>

      {/* Main Table */}
      <div className="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-200 dark:border-gray-700 overflow-hidden">
        {isLoading ? (
          <div className="p-12 text-center text-gray-500 dark:text-gray-400">
            <div className="inline-block animate-spin rounded-full h-8 w-8 border-4 border-solid border-blue-600 border-r-transparent mb-3" />
            <p className="text-sm">Đang tải lịch sử kiểm toán...</p>
          </div>
        ) : isError ? (
          <div className="p-12 text-center">
            <p className="text-red-600 dark:text-red-400 text-sm font-medium mb-3">
              Không thể tải dữ liệu kiểm toán nền tảng.
            </p>
            <button
              onClick={() => refetch()}
              className="px-4 py-2 text-xs font-semibold rounded-lg bg-red-600 text-white hover:bg-red-700"
            >
              Thử lại
            </button>
          </div>
        ) : items.length === 0 ? (
          <div className="p-12 text-center text-gray-500 dark:text-gray-400">
            <svg className="w-12 h-12 mx-auto mb-3 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
            <p className="text-sm font-medium">Chưa có bản ghi kiểm toán nào phù hợp với bộ lọc.</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
              <thead className="bg-gray-50 dark:bg-gray-700/50 text-left text-xs font-semibold text-gray-600 dark:text-gray-300 uppercase tracking-wider">
                <tr>
                  <th className="py-3 px-4">Mã KT</th>
                  <th className="py-3 px-4">Thời gian</th>
                  <th className="py-3 px-4">Hành động</th>
                  <th className="py-3 px-4">Đối tượng</th>
                  <th className="py-3 px-4">Trung tâm đích</th>
                  <th className="py-3 px-4">Người thực hiện</th>
                  <th className="py-3 px-4">Lý do</th>
                  <th className="py-3 px-4">Trace ID</th>
                  <th className="py-3 px-4 text-right">Chi tiết</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 dark:divide-gray-700 text-xs">
                {items.map((log) => (
                  <tr key={log.auditId} className="hover:bg-gray-50/75 dark:hover:bg-gray-700/40 transition-colors">
                    <td className="py-3 px-4 font-mono font-medium text-gray-900 dark:text-white">
                      #{log.auditId}
                    </td>
                    <td className="py-3 px-4 text-gray-600 dark:text-gray-300 whitespace-nowrap">
                      {formatDate(log.createdAt)}
                    </td>
                    <td className="py-3 px-4">
                      <span className={`inline-block px-2 py-0.5 rounded-md border text-[11px] font-semibold ${getActionBadgeColor(log.actionType)}`}>
                        {log.actionType}
                      </span>
                    </td>
                    <td className="py-3 px-4">
                      <span className="font-semibold text-gray-800 dark:text-gray-200">{log.targetType}</span>
                      <span className="block text-[11px] font-mono text-gray-500 dark:text-gray-400 truncate max-w-[130px]" title={log.targetId}>
                        {log.targetId}
                      </span>
                    </td>
                    <td className="py-3 px-4">
                      {log.targetCenterCode ? (
                        <span className="font-semibold text-blue-700 dark:text-blue-300">
                          {log.targetCenterCode}
                        </span>
                      ) : log.targetCenterId ? (
                        <span className="font-mono text-gray-500 dark:text-gray-400 text-[11px] truncate block max-w-[100px]" title={log.targetCenterId}>
                          {log.targetCenterId.slice(0, 8)}...
                        </span>
                      ) : (
                        <span className="text-gray-400 italic">Hệ thống</span>
                      )}
                    </td>
                    <td className="py-3 px-4 text-gray-700 dark:text-gray-300">
                      {log.actorUsername || (log.actorUserId ? `${log.actorUserId.slice(0, 8)}...` : "Hệ thống")}
                    </td>
                    <td className="py-3 px-4 text-gray-600 dark:text-gray-300 max-w-[200px] truncate" title={log.reason}>
                      {log.reason}
                    </td>
                    <td className="py-3 px-4 font-mono text-[11px]">
                      <button
                        type="button"
                        onClick={() => copyToClipboard(log.traceId, log.traceId)}
                        className="text-gray-500 dark:text-gray-400 hover:text-blue-600 dark:hover:text-blue-400 inline-flex items-center gap-1"
                        title="Sao chép W3C Trace ID"
                      >
                        <span>{log.traceId.slice(0, 10)}...</span>
                        {copiedTraceId === log.traceId ? (
                          <span className="text-green-600 font-sans text-[10px]">Đã chép!</span>
                        ) : (
                          <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
                          </svg>
                        )}
                      </button>
                    </td>
                    <td className="py-3 px-4 text-right whitespace-nowrap">
                      <button
                        type="button"
                        onClick={() => setSelectedAudit(log)}
                        className="px-2.5 py-1 rounded bg-slate-100 dark:bg-slate-700 hover:bg-blue-50 dark:hover:bg-blue-900/30 text-blue-700 dark:text-blue-300 font-medium transition-colors"
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

        {/* Pagination Bar */}
        <div className="p-4 border-t border-gray-200 dark:border-gray-700 flex flex-col sm:flex-row items-center justify-between gap-3 text-xs text-gray-500 dark:text-gray-400">
          <div>
            Hiển thị {items.length} trên tổng số {totalCount} bản ghi (Trang {page}/{totalPages})
          </div>
          <div className="inline-flex gap-2">
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              className="px-3 py-1.5 rounded-lg border border-gray-300 dark:border-gray-600 disabled:opacity-50 disabled:cursor-not-allowed hover:bg-gray-100 dark:hover:bg-gray-700 font-medium"
            >
              ← Trang trước
            </button>
            <button
              type="button"
              disabled={page >= totalPages}
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              className="px-3 py-1.5 rounded-lg border border-gray-300 dark:border-gray-600 disabled:opacity-50 disabled:cursor-not-allowed hover:bg-gray-100 dark:hover:bg-gray-700 font-medium"
            >
              Trang sau →
            </button>
          </div>
        </div>
      </div>

      {/* Detail Modal */}
      {selectedAudit && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm">
          <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-xl border border-gray-200 dark:border-gray-700 max-w-3xl w-full max-h-[90vh] flex flex-col overflow-hidden animate-in fade-in zoom-in-95 duration-150">
            {/* Modal Header */}
            <div className="p-5 border-b border-gray-200 dark:border-gray-700 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <span className={`px-2.5 py-1 rounded-md border text-xs font-bold ${getActionBadgeColor(selectedAudit.actionType)}`}>
                  {selectedAudit.actionType}
                </span>
                <h3 className="text-base font-bold text-gray-900 dark:text-white">
                  Chi Tiết Bản Ghi Kiểm Toán #{selectedAudit.auditId}
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setSelectedAudit(null)}
                className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 text-xl font-bold p-1 leading-none"
              >
                ✕
              </button>
            </div>

            {/* Modal Body */}
            <div className="p-5 overflow-y-auto space-y-4 text-xs">
              {/* Metadata Grid */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 p-3 bg-gray-50 dark:bg-gray-700/50 rounded-lg border border-gray-200 dark:border-gray-600">
                <div>
                  <span className="text-gray-500 dark:text-gray-400 block">Thời gian ghi nhận:</span>
                  <span className="font-semibold text-gray-900 dark:text-white">{formatDate(selectedAudit.createdAt)}</span>
                </div>
                <div>
                  <span className="text-gray-500 dark:text-gray-400 block">Người thực hiện:</span>
                  <span className="font-semibold text-gray-900 dark:text-white">
                    {selectedAudit.actorUsername || selectedAudit.actorUserId || "Hệ thống"}
                  </span>
                </div>
                <div>
                  <span className="text-gray-500 dark:text-gray-400 block">Trung tâm bị tác động:</span>
                  <span className="font-semibold text-gray-900 dark:text-white">
                    {selectedAudit.targetCenterCode ? `${selectedAudit.targetCenterCode} (${selectedAudit.targetCenterId})` : selectedAudit.targetCenterId || "Root Tenant PLATFORM"}
                  </span>
                </div>
                <div>
                  <span className="text-gray-500 dark:text-gray-400 block">Tài nguyên tác động:</span>
                  <span className="font-semibold text-gray-900 dark:text-white">
                    {selectedAudit.targetType} ({selectedAudit.targetId})
                  </span>
                </div>
                <div className="sm:col-span-2">
                  <span className="text-gray-500 dark:text-gray-400 block">Lý do hành động:</span>
                  <p className="font-medium text-gray-900 dark:text-white mt-0.5">{selectedAudit.reason}</p>
                </div>
                <div className="sm:col-span-2 flex items-center justify-between">
                  <div>
                    <span className="text-gray-500 dark:text-gray-400 block">Trace ID:</span>
                    <span className="font-mono text-gray-700 dark:text-gray-300">{selectedAudit.traceId}</span>
                  </div>
                  <button
                    type="button"
                    onClick={() => copyToClipboard(selectedAudit.traceId, "modal")}
                    className="px-2 py-1 rounded bg-gray-200 dark:bg-gray-600 hover:bg-gray-300 dark:hover:bg-gray-500 font-semibold"
                  >
                    {copiedTraceId === "modal" ? "Đã chép!" : "Sao chép"}
                  </button>
                </div>
              </div>

              {/* Data Redaction Notice */}
              <div className="p-2.5 rounded-lg bg-blue-50 dark:bg-blue-900/30 border border-blue-200 dark:border-blue-800 text-blue-800 dark:text-blue-300 flex items-center gap-2">
                <svg className="w-4 h-4 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
                </svg>
                <span>Dữ liệu kiểm toán được khử khuẩn tự động (mật khẩu, khóa bảo mật, token không bao giờ xuất hiện).</span>
              </div>

              {/* Before & After Data Diff */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                <div>
                  <h4 className="font-semibold text-gray-700 dark:text-gray-300 mb-1.5 flex items-center gap-1.5">
                    <span className="w-2 h-2 rounded-full bg-red-400" />
                    Trạng thái trước thay đổi (BeforeData)
                  </h4>
                  <pre className="p-3 bg-gray-900 text-gray-100 rounded-lg overflow-x-auto text-[11px] font-mono max-h-56">
                    {selectedAudit.beforeData ? JSON.stringify(selectedAudit.beforeData, null, 2) : "// Không có dữ liệu trước thay đổi"}
                  </pre>
                </div>
                <div>
                  <h4 className="font-semibold text-gray-700 dark:text-gray-300 mb-1.5 flex items-center gap-1.5">
                    <span className="w-2 h-2 rounded-full bg-green-400" />
                    Trạng thái sau thay đổi (AfterData)
                  </h4>
                  <pre className="p-3 bg-gray-900 text-gray-100 rounded-lg overflow-x-auto text-[11px] font-mono max-h-56">
                    {selectedAudit.afterData ? JSON.stringify(selectedAudit.afterData, null, 2) : "// Không có dữ liệu sau thay đổi"}
                  </pre>
                </div>
              </div>
            </div>

            {/* Modal Footer */}
            <div className="p-4 border-t border-gray-200 dark:border-gray-700 flex justify-between items-center bg-gray-50 dark:bg-gray-800/80">
              <span className="text-[11px] text-gray-500 dark:text-gray-400 italic">
                Bản ghi kiểm toán có tính bất biến (Immutable), không thể sửa đổi hoặc xóa.
              </span>
              <button
                type="button"
                onClick={() => setSelectedAudit(null)}
                className="px-4 py-2 text-xs font-semibold rounded-lg bg-gray-200 dark:bg-gray-700 hover:bg-gray-300 dark:hover:bg-gray-600 text-gray-800 dark:text-gray-200 transition-colors"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
