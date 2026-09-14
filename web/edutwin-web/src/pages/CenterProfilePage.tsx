import React, { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { organizationApi } from "../api/organizationApi";
import { useAuthStore } from "../stores/authStore";
import { extractProblemDetails, isConcurrencyConflict, isForbidden } from "../utils/problemDetails";
import type { CenterProfileDto, UpdateCenterProfileRequest } from "../types/organization";

const TIMEZONE_OPTIONS = [
  { value: "Asia/Ho_Chi_Minh", label: "Asia/Ho_Chi_Minh (GMT+7 - Việt Nam)" },
  { value: "Asia/Bangkok", label: "Asia/Bangkok (GMT+7 - Thái Lan)" },
  { value: "Asia/Singapore", label: "Asia/Singapore (GMT+8 - Singapore)" },
  { value: "Asia/Tokyo", label: "Asia/Tokyo (GMT+9 - Nhật Bản)" },
  { value: "Asia/Seoul", label: "Asia/Seoul (GMT+9 - Hàn Quốc)" },
  { value: "UTC", label: "UTC (GMT+0 - Giờ quốc tế chuẩn)" },
  { value: "Europe/London", label: "Europe/London (GMT+0/+1 - Vương quốc Anh)" },
  { value: "Europe/Paris", label: "Europe/Paris (GMT+1/+2 - Pháp)" },
  { value: "America/New_York", label: "America/New_York (GMT-5/-4 - New York)" },
  { value: "America/Los_Angeles", label: "America/Los_Angeles (GMT-8/-7 - Los Angeles)" },
];

export const CenterProfilePage: React.FC = () => {
  const queryClient = useQueryClient();

  // Form states
  const [centerName, setCenterName] = useState("");
  const [timezone, setTimezone] = useState("Asia/Ho_Chi_Minh");
  const [rowVersion, setRowVersion] = useState("");
  const [statusMessage, setStatusMessage] = useState<{ type: "success" | "error" | "conflict"; text: string; traceId?: string } | null>(null);

  const {
    data: center,
    isLoading,
    isError,
    error,
    refetch,
  } = useQuery<CenterProfileDto>({
    queryKey: ["currentCenter"],
    queryFn: organizationApi.getCurrentCenter,
  });

  // Sync form state when data is loaded/updated
  useEffect(() => {
    if (center) {
      setCenterName(center.centerName);
      setTimezone(center.timezone || "Asia/Ho_Chi_Minh");
      setRowVersion(center.rowVersion);
    }
  }, [center]);

  const updateMutation = useMutation({
    mutationFn: (req: UpdateCenterProfileRequest) => organizationApi.updateCurrentCenter(req),
    onSuccess: (updated) => {
      // Immediate cache sync
      queryClient.setQueryData(["currentCenter"], updated);
      setCenterName(updated.centerName);
      setTimezone(updated.timezone);
      setRowVersion(updated.rowVersion);

      // Update authStore user centerName so navbar/breadcrumbs update immediately without F5
      const currentUser = useAuthStore.getState().user;
      if (currentUser && currentUser.centerName !== updated.centerName) {
        useAuthStore.setState({
          user: {
            ...currentUser,
            centerName: updated.centerName,
          },
        });
      }

      setStatusMessage({
        type: "success",
        text: "Cập nhật thông tin trung tâm thành công!",
      });
    },
    onError: (err: unknown) => {
      if (isConcurrencyConflict(err)) {
        const details = extractProblemDetails(err);
        setStatusMessage({
          type: "conflict",
          text: "Dữ liệu trung tâm đã bị thay đổi bởi phiên làm việc khác. Hệ thống đang tự động tải lại dữ liệu mới nhất...",
          traceId: details.traceId ?? undefined,
        });
        // Invalidate and refetch immediately
        queryClient.invalidateQueries({ queryKey: ["currentCenter"] });
      } else if (isForbidden(err)) {
        const details = extractProblemDetails(err);
        setStatusMessage({
          type: "error",
          text: "Bạn không có quyền quản lý thông tin trung tâm (yêu cầu quyền organization.center.update).",
          traceId: details.traceId ?? undefined,
        });
      } else {
        const details = extractProblemDetails(err);
        setStatusMessage({
          type: "error",
          text: details.message || "Đã xảy ra lỗi khi cập nhật thông tin trung tâm. Vui lòng thử lại.",
          traceId: details.traceId ?? undefined,
        });
      }
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setStatusMessage(null);

    const trimmedName = centerName.trim();
    if (!trimmedName) {
      setStatusMessage({
        type: "error",
        text: "Tên trung tâm không được để trống.",
      });
      return;
    }
    if (trimmedName.length > 200) {
      setStatusMessage({
        type: "error",
        text: "Tên trung tâm không được vượt quá 200 ký tự.",
      });
      return;
    }

    updateMutation.mutate({
      centerName: trimmedName,
      timezone,
      rowVersion,
    });
  };

  const getStatusBadge = (statusStr: string) => {
    switch (statusStr.toLowerCase()) {
      case "active":
        return (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-700 ring-1 ring-inset ring-emerald-600/20">
            <span className="h-1.5 w-1.5 rounded-full bg-emerald-600" />
            Đang hoạt động
          </span>
        );
      case "suspended":
        return (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-red-50 px-3 py-1 text-xs font-semibold text-red-700 ring-1 ring-inset ring-red-600/20">
            <span className="h-1.5 w-1.5 rounded-full bg-red-600" />
            Tạm ngưng
          </span>
        );
      case "pendingverification":
        return (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-50 px-3 py-1 text-xs font-semibold text-amber-700 ring-1 ring-inset ring-amber-600/20">
            <span className="h-1.5 w-1.5 rounded-full bg-amber-600" />
            Chờ xác thực
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center rounded-full bg-slate-100 px-3 py-1 text-xs font-semibold text-slate-700">
            {statusStr}
          </span>
        );
    }
  };

  return (
    <div className="min-h-screen bg-slate-50 p-6">
      <div className="mx-auto max-w-5xl space-y-6">
        {/* Header and Breadcrumbs */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-200 pb-4">
          <div>
            <div className="flex items-center gap-2 text-xs font-semibold text-slate-500 mb-1">
              <Link to="/" className="hover:text-indigo-600">Trang chủ</Link>
              <span>/</span>
              <span className="text-slate-500">Quản lý</span>
              <span>/</span>
              <span className="text-slate-900">Hồ sơ Trung tâm</span>
            </div>
            <h1 className="text-2xl font-bold text-slate-900">
              Hồ Sơ & Cấu Hình Trung Tâm
            </h1>
            <p className="mt-1 text-sm text-slate-500">
              Quản lý thông tin định danh, tên đại diện pháp lý và múi giờ vận hành của trung tâm.
            </p>
          </div>

          {/* Navigation Links */}
          <nav className="flex flex-wrap items-center gap-2" aria-label="Điều hướng quản trị trung tâm">
            <Link
              to="/quan-ly/trung-tam"
              className="rounded-lg bg-indigo-600 px-3 py-2 text-xs font-semibold text-white shadow-sm"
              aria-current="page"
            >
              Hồ sơ trung tâm
            </Link>
            <Link
              to="/quan-ly/tong-quan-trung-tam"
              className="rounded-lg bg-white px-3 py-2 text-xs font-semibold text-slate-700 ring-1 ring-slate-300 hover:bg-slate-50"
            >
              Dashboard
            </Link>
            <Link
              to="/quan-ly/giao-vien"
              className="rounded-lg bg-white px-3 py-2 text-xs font-semibold text-slate-700 ring-1 ring-slate-300 hover:bg-slate-50"
            >
              Giáo viên
            </Link>
            <Link
              to="/quan-ly/lop-hoc"
              className="rounded-lg bg-white px-3 py-2 text-xs font-semibold text-slate-700 ring-1 ring-slate-300 hover:bg-slate-50"
            >
              Lớp học
            </Link>
            <Link
              to="/quan-ly/hoc-sinh"
              className="rounded-lg bg-white px-3 py-2 text-xs font-semibold text-slate-700 ring-1 ring-slate-300 hover:bg-slate-50"
            >
              Học sinh
            </Link>
          </nav>
        </div>

        {/* Loading State */}
        {isLoading && (
          <div className="flex items-center justify-center rounded-2xl bg-white p-12 shadow-sm ring-1 ring-slate-200">
            <div className="flex flex-col items-center gap-3">
              <div className="h-8 w-8 animate-spin rounded-full border-4 border-indigo-600 border-t-transparent" />
              <span className="text-sm font-medium text-slate-600">Đang tải thông tin hồ sơ trung tâm...</span>
            </div>
          </div>
        )}

        {/* Error State */}
        {isError && (
          <div className="rounded-2xl bg-white p-8 text-center shadow-sm ring-1 ring-red-200" role="alert">
            <h2 className="text-lg font-bold text-red-600">Không thể tải thông tin trung tâm</h2>
            <p className="mt-2 text-sm text-slate-600">
              {(error as Error)?.message || "Vui lòng kiểm tra lại kết nối hoặc quyền truy cập."}
            </p>
            <button
              onClick={() => refetch()}
              className="mt-4 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500"
            >
              Thử lại
            </button>
          </div>
        )}

        {/* Main Content Form */}
        {!isLoading && !isError && center && (
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Left Column: Summary Card */}
            <div className="space-y-6">
              <div className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
                <div className="flex items-center justify-between pb-4 border-b border-slate-100">
                  <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
                    Trạng thái vận hành
                  </span>
                  {getStatusBadge(center.status)}
                </div>

                <div className="mt-4 space-y-4 text-sm">
                  <div>
                    <span className="text-xs font-medium text-slate-500">Mã trung tâm (Read-only)</span>
                    <div className="mt-1 flex items-center justify-between rounded-lg bg-slate-50 px-3 py-2 ring-1 ring-slate-200 font-mono text-xs font-bold text-slate-800">
                      <span>{center.centerCode}</span>
                      <span className="text-slate-400 text-xs" title="Mã trung tâm không thể thay đổi">🔒 Khóa</span>
                    </div>
                  </div>

                  <div>
                    <span className="text-xs font-medium text-slate-500">Mã định danh ID</span>
                    <div className="mt-1 font-mono text-xs text-slate-500 break-all">
                      {center.centerId}
                    </div>
                  </div>

                  <div className="rounded-lg bg-amber-50/80 p-3 text-xs text-amber-800 ring-1 ring-amber-200">
                    <strong>Lưu ý quản trị:</strong> Mã trung tâm và Trạng thái do Ban Quản trị Nền tảng kiểm soát nhằm bảo đảm tính toàn vẹn dữ liệu hợp đồng đối tác.
                  </div>
                </div>
              </div>
            </div>

            {/* Right Column: Edit Profile Form */}
            <div className="lg:col-span-2">
              <div className="rounded-2xl bg-white p-8 shadow-sm ring-1 ring-slate-200">
                <h2 className="text-lg font-bold text-slate-900 border-b border-slate-100 pb-4">
                  Cập Nhật Thông Tin Vận Hành
                </h2>

                {/* Status Banners */}
                {statusMessage && (
                  <div
                    className={`mt-4 rounded-xl p-4 text-sm font-medium ${
                      statusMessage.type === "success"
                        ? "bg-emerald-50 text-emerald-800 ring-1 ring-emerald-200"
                        : statusMessage.type === "conflict"
                        ? "bg-amber-50 text-amber-800 ring-1 ring-amber-200"
                        : "bg-red-50 text-red-800 ring-1 ring-red-200"
                    }`}
                    role="alert"
                    aria-live="polite"
                  >
                    <div className="flex items-start justify-between">
                      <div>
                        <p>{statusMessage.text}</p>
                        {statusMessage.traceId && (
                          <p className="mt-1 font-mono text-xs opacity-75">
                            Trace ID: {statusMessage.traceId}
                          </p>
                        )}
                      </div>
                      <button
                        type="button"
                        onClick={() => setStatusMessage(null)}
                        className="text-xs underline ml-4 hover:opacity-75"
                      >
                        Đóng
                      </button>
                    </div>
                  </div>
                )}

                <form onSubmit={handleSubmit} className="mt-6 space-y-6">
                  <div>
                    <label htmlFor="centerName" className="block text-sm font-semibold text-slate-800">
                      Tên trung tâm <span className="text-red-500">*</span>
                    </label>
                    <div className="mt-1.5">
                      <input
                        type="text"
                        id="centerName"
                        value={centerName}
                        onChange={(e) => setCenterName(e.target.value)}
                        maxLength={200}
                        required
                        className="w-full rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm text-slate-900 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                        placeholder="Nhập tên đại diện của trung tâm..."
                      />
                    </div>
                    <div className="mt-1 flex justify-between text-xs text-slate-500">
                      <span>Tên hiển thị chính thức trên toàn hệ thống học tập</span>
                      <span>{centerName.length}/200 ký tự</span>
                    </div>
                  </div>

                  <div>
                    <label htmlFor="timezone" className="block text-sm font-semibold text-slate-800">
                      Múi giờ vận hành <span className="text-red-500">*</span>
                    </label>
                    <div className="mt-1.5">
                      <select
                        id="timezone"
                        value={timezone}
                        onChange={(e) => setTimezone(e.target.value)}
                        required
                        className="w-full rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm text-slate-900 shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                      >
                        {TIMEZONE_OPTIONS.map((opt) => (
                          <option key={opt.value} value={opt.value}>
                            {opt.label}
                          </option>
                        ))}
                      </select>
                    </div>
                    <p className="mt-1 text-xs text-slate-500">
                      Múi giờ chuẩn xác định hạn nộp bài tập, thống kê học tập và nhật ký kiểm toán.
                    </p>
                  </div>

                  <div className="flex items-center justify-end gap-3 border-t border-slate-100 pt-6">
                    <button
                      type="button"
                      onClick={() => {
                        if (center) {
                          setCenterName(center.centerName);
                          setTimezone(center.timezone || "Asia/Ho_Chi_Minh");
                          setStatusMessage(null);
                        }
                      }}
                      disabled={updateMutation.isPending}
                      className="rounded-xl bg-white px-5 py-2.5 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50 disabled:opacity-50"
                    >
                      Hủy thay đổi
                    </button>
                    <button
                      type="submit"
                      disabled={updateMutation.isPending}
                      className="inline-flex items-center gap-2 rounded-xl bg-indigo-600 px-6 py-2.5 text-sm font-bold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 disabled:opacity-50"
                    >
                      {updateMutation.isPending ? (
                        <>
                          <div className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
                          <span>Đang lưu...</span>
                        </>
                      ) : (
                        <span>Lưu thay đổi</span>
                      )}
                    </button>
                  </div>
                </form>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
