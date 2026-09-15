import { useEffect, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { organizationApi } from "../api/organizationApi";
import { permissions } from "../auth/permissions";
import { ConcurrencyBanner, PageHeader, SafeErrorPanel, Skeleton, StatusBadge } from "../components/centerManager";
import { useAuthStore } from "../stores/authStore";
import type { CenterProfileDto, UpdateCenterProfileRequest } from "../types/organization";
import { extractProblemDetails, isConcurrencyConflict, isForbidden, mapSafeOperationalError } from "../utils/problemDetails";

const CENTER_PROFILE_QUERY_KEY = ["center-profile"] as const;

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

type Feedback = { type: "success" | "error" | "conflict"; text: string; traceId?: string };

const statusPresentation = (status: string) => {
  switch (status.toLowerCase()) {
    case "active": return { label: "Đang hoạt động", tone: "success" as const };
    case "suspended": return { label: "Tạm ngưng", tone: "danger" as const };
    case "pendingverification": return { label: "Chờ xác thực", tone: "warning" as const };
    default: return { label: status, tone: "neutral" as const };
  }
};

export const CenterProfilePage = () => {
  const queryClient = useQueryClient();
  const canManage = useAuthStore((state) => state.hasPermission(permissions.centerManage));
  const [centerName, setCenterName] = useState("");
  const [timezone, setTimezone] = useState("Asia/Ho_Chi_Minh");
  const [rowVersion, setRowVersion] = useState("");
  const [feedback, setFeedback] = useState<Feedback | null>(null);

  const centerQuery = useQuery<CenterProfileDto>({
    queryKey: CENTER_PROFILE_QUERY_KEY,
    queryFn: organizationApi.getCurrentCenter,
  });

  useEffect(() => {
    if (!centerQuery.data) return;
    setCenterName(centerQuery.data.centerName);
    setTimezone(centerQuery.data.timezone || "Asia/Ho_Chi_Minh");
    setRowVersion(centerQuery.data.rowVersion);
  }, [centerQuery.data]);

  const updateMutation = useMutation({
    mutationFn: (request: UpdateCenterProfileRequest) => organizationApi.updateCurrentCenter(request),
    onSuccess: (updated) => {
      queryClient.setQueryData(CENTER_PROFILE_QUERY_KEY, updated);
      const currentUser = useAuthStore.getState().user;
      if (currentUser && currentUser.centerName !== updated.centerName) {
        useAuthStore.getState().updateCurrentUser({ ...currentUser, centerName: updated.centerName });
      }
      setFeedback({ type: "success", text: "Đã cập nhật thông tin vận hành của trung tâm." });
    },
    onError: (error: unknown) => {
      const details = extractProblemDetails(error);
      if (isConcurrencyConflict(error)) {
        setFeedback({
          type: "conflict",
          text: "Dữ liệu đã được cập nhật ở phiên khác. Hãy tải bản mới nhất trước khi tiếp tục.",
          traceId: details.traceId ?? undefined,
        });
        return;
      }
      setFeedback({
        type: "error",
        text: isForbidden(error)
          ? "Bạn không có quyền cập nhật hồ sơ trung tâm."
          : mapSafeOperationalError(error, "Không thể cập nhật hồ sơ trung tâm. Vui lòng thử lại."),
        traceId: details.traceId ?? undefined,
      });
    },
  });

  const resetForm = () => {
    const center = centerQuery.data;
    if (!center) return;
    setCenterName(center.centerName);
    setTimezone(center.timezone || "Asia/Ho_Chi_Minh");
    setRowVersion(center.rowVersion);
    setFeedback(null);
  };

  const reloadAfterConflict = async () => {
    await centerQuery.refetch();
    setFeedback(null);
  };

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    setFeedback(null);
    if (!canManage) {
      setFeedback({ type: "error", text: "Bạn chỉ có quyền xem hồ sơ trung tâm." });
      return;
    }
    const trimmedName = centerName.trim();
    if (!trimmedName || trimmedName.length > 200) {
      setFeedback({ type: "error", text: "Tên trung tâm phải có từ 1 đến 200 ký tự." });
      return;
    }
    updateMutation.mutate({ centerName: trimmedName, timezone, rowVersion });
  };

  const center = centerQuery.data;
  const status = center ? statusPresentation(center.status) : null;

  return (
    <div className="px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
      <div className="mx-auto max-w-6xl space-y-6">
        <PageHeader
          eyebrow="Center profile"
          title="Hồ sơ trung tâm"
          description="Thông tin định danh và cấu hình thời gian áp dụng trong phạm vi trung tâm hiện tại."
        />

        {centerQuery.isLoading && (
          <div role="status" aria-label="Đang tải hồ sơ trung tâm" className="grid gap-6 lg:grid-cols-[20rem_minmax(0,1fr)]">
            <Skeleton decorative className="h-72 w-full rounded-2xl" />
            <Skeleton decorative className="h-96 w-full rounded-2xl" />
            <span className="sr-only">Đang tải hồ sơ trung tâm</span>
          </div>
        )}
        {centerQuery.isError && (
          <SafeErrorPanel error={centerQuery.error} fallback="Không thể tải hồ sơ trung tâm." onRetry={() => centerQuery.refetch()} />
        )}

        {!centerQuery.isLoading && !centerQuery.isError && center && status && (
          <div className="grid gap-6 lg:grid-cols-[20rem_minmax(0,1fr)]">
            <aside className="cm-surface self-start p-5 sm:p-6" aria-labelledby="center-identity-heading">
              <div className="flex items-center justify-between gap-3 border-b border-[var(--cm-border-subtle)] pb-4">
                <h2 id="center-identity-heading" className="text-sm font-semibold text-[var(--cm-text)]">Định danh trung tâm</h2>
                <StatusBadge status={center.status} label={status.label} tone={status.tone} />
              </div>
              <dl className="mt-5 space-y-5">
                <div>
                  <dt className="text-xs font-medium text-[var(--cm-text-muted)]">Mã trung tâm</dt>
                  <dd className="mt-1 rounded-lg border border-[var(--cm-border-subtle)] bg-slate-950/35 px-3 py-2 font-mono text-sm font-semibold text-[var(--cm-text)]">{center.centerCode}</dd>
                </div>
                <div>
                  <dt className="text-xs font-medium text-[var(--cm-text-muted)]">Mã định danh</dt>
                  <dd className="mt-1 break-all font-mono text-xs text-[var(--cm-text-secondary)]">{center.centerId}</dd>
                </div>
              </dl>
              <p className="mt-6 rounded-xl border border-amber-400/25 bg-amber-400/10 p-3 text-xs leading-5 text-amber-100/80">
                Mã và trạng thái trung tâm do Quản trị viên nền tảng kiểm soát và chỉ được hiển thị tại đây.
              </p>
            </aside>

            <section className="cm-surface p-5 sm:p-7" aria-labelledby="center-settings-heading">
              <div className="border-b border-[var(--cm-border-subtle)] pb-4">
                <h2 id="center-settings-heading" className="text-lg font-semibold text-[var(--cm-text)]">Thông tin vận hành</h2>
                <p className="mt-1 text-sm text-[var(--cm-text-secondary)]">
                  {canManage ? "Các thay đổi được bảo vệ bằng phiên bản dữ liệu hiện tại." : "Tài khoản hiện tại chỉ có quyền xem."}
                </p>
              </div>

              {feedback?.type === "conflict" && (
                <div className="mt-5">
                  <ConcurrencyBanner onReload={reloadAfterConflict} isReloading={centerQuery.isFetching} message={feedback.text} />
                  {feedback.traceId && <p className="mt-2 break-all font-mono text-xs text-[var(--cm-text-muted)]">Trace ID: {feedback.traceId}</p>}
                </div>
              )}
              {feedback && feedback.type !== "conflict" && (
                <div role="status" aria-live="polite" className={`mt-5 rounded-xl border p-4 text-sm ${feedback.type === "success" ? "border-emerald-400/30 bg-emerald-400/10 text-emerald-100" : "border-rose-400/30 bg-rose-400/10 text-rose-100"}`}>
                  <p>{feedback.text}</p>
                  {feedback.traceId && <p className="mt-2 break-all font-mono text-xs opacity-70">Trace ID: {feedback.traceId}</p>}
                </div>
              )}

              <form onSubmit={handleSubmit} className="mt-6 space-y-6">
                <label className="block text-sm font-semibold text-[var(--cm-text)]" htmlFor="center-name">
                  Tên trung tâm
                  <input id="center-name" className="cm-field mt-2 w-full px-4" value={centerName} onChange={(event) => setCenterName(event.target.value)} maxLength={200} required disabled={!canManage || updateMutation.isPending} />
                  <span className="mt-1 flex justify-between gap-4 text-xs font-normal text-[var(--cm-text-muted)]">
                    <span>Tên hiển thị trong không gian của trung tâm.</span>
                    <span>{centerName.length}/200</span>
                  </span>
                </label>

                <label className="block text-sm font-semibold text-[var(--cm-text)]" htmlFor="center-timezone">
                  Múi giờ vận hành
                  <select id="center-timezone" className="cm-field mt-2 w-full px-4" value={timezone} onChange={(event) => setTimezone(event.target.value)} required disabled={!canManage || updateMutation.isPending}>
                    {TIMEZONE_OPTIONS.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                  <span className="mt-1 block text-xs font-normal text-[var(--cm-text-muted)]">Dùng cho hạn nộp bài, báo cáo và nhật ký.</span>
                </label>

                {canManage && (
                  <div className="flex flex-col-reverse gap-3 border-t border-[var(--cm-border-subtle)] pt-5 sm:flex-row sm:justify-end">
                    <button type="button" className="cm-secondary-button" onClick={resetForm} disabled={updateMutation.isPending}>Hủy thay đổi</button>
                    <button type="submit" className="cm-primary-button" disabled={updateMutation.isPending}>{updateMutation.isPending ? "Đang lưu…" : "Lưu thay đổi"}</button>
                  </div>
                )}
              </form>
            </section>
          </div>
        )}
      </div>
    </div>
  );
};
