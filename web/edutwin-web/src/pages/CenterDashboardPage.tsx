import { useQuery } from "@tanstack/react-query";
import { Link, useSearchParams } from "react-router-dom";
import { getCenterDashboard } from "../api/dashboardsApi";
import { organizationApi } from "../api/organizationApi";
import { listTeacherReviewQueue } from "../api/teacherReviewsApi";
import { SafeErrorPanel, Skeleton, StatusBadge } from "../components/centerManager";
import { useAuthStore } from "../stores/authStore";
import { permissions } from "../auth/permissions";
import type { CenterDashboardDataDto } from "../types/dashboards";

const clampPercentage = (value: number) => Math.min(100, Math.max(0, value));

const formatGeneratedAt = (value: string) => {
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? "Không xác định"
    : new Intl.DateTimeFormat("vi-VN", { dateStyle: "short", timeStyle: "short" }).format(date);
};

function DashboardSkeleton() {
  return (
    <div aria-label="Đang tải tổng quan trung tâm" role="status" className="space-y-6">
      <span className="sr-only">Đang tải tổng quan trung tâm</span>
      <Skeleton decorative className="h-44 w-full rounded-3xl" />
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {Array.from({ length: 4 }, (_, index) => (
          <Skeleton key={index} decorative className="h-36 w-full rounded-2xl" />
        ))}
      </div>
      <div className="grid gap-6 xl:grid-cols-[minmax(0,1.2fr)_minmax(22rem,0.8fr)]">
        <Skeleton decorative className="h-80 w-full rounded-2xl" />
        <Skeleton decorative className="h-80 w-full rounded-2xl" />
      </div>
    </div>
  );
}

export const CenterDashboardPage = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const selectedSubjectId = searchParams.get("subjectId") || "";

  const user = useAuthStore((state) => state.user);
  const hasPermission = useAuthStore((state) => state.hasPermission);

  const canCreateAssignment = hasPermission(permissions.assignmentsCreate);
  const canReadClasses = hasPermission(permissions.classesRead);
  const canReview = hasPermission(permissions.teacherReviewsRead);

  const centerName = user?.centerName || "EduTwin Center";

  const subjectsQuery = useQuery({
    queryKey: ["subjects", "center-dashboard", "active"],
    queryFn: () => organizationApi.listSubjects(true),
  });

  const dashboardQuery = useQuery<CenterDashboardDataDto>({
    queryKey: ["centerDashboard", selectedSubjectId],
    queryFn: () => getCenterDashboard(selectedSubjectId || undefined),
  });

  const reviewQueueQuery = useQuery({
    queryKey: ["centerDashboardReviewCount"],
    queryFn: () => listTeacherReviewQueue({ page: 1, pageSize: 1 }),
    enabled: canReview,
    staleTime: 30_000,
  });

  const reviewQueueCount = reviewQueueQuery.data?.meta?.totalItems ?? 0;
  const dashboard = dashboardQuery.data;

  return (
    <div className="px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
      <div className="mx-auto max-w-[96rem] space-y-6">
        {/* ✨ A. Hero Command Banner (Trung tâm điều hành trung tâm) */}
        <section
          aria-label="Trung tâm điều hành"
          className="relative overflow-hidden rounded-3xl border border-indigo-100 bg-gradient-to-br from-indigo-50/70 via-white to-blue-50/60 p-6 sm:p-8 shadow-xl shadow-slate-200/50 backdrop-blur-xl dark:border-indigo-500/20 dark:from-slate-900 dark:via-[#111827] dark:to-slate-900/95 dark:shadow-2xl"
        >
          {/* Ambient background glow */}
          <div
            aria-hidden="true"
            className="pointer-events-none absolute -top-24 -right-24 h-96 w-96 rounded-full bg-cyan-500/5 blur-3xl dark:bg-cyan-500/10"
          />
          <div
            aria-hidden="true"
            className="pointer-events-none absolute -bottom-24 -left-24 h-96 w-96 rounded-full bg-indigo-500/5 blur-3xl dark:bg-indigo-500/10"
          />

          <div className="relative z-10 flex flex-col gap-6">
            {/* Top row: Status indicator (solid neon green, no blinking) & timestamp */}
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div className="inline-flex items-center gap-2 rounded-full border border-emerald-500/25 bg-emerald-50 px-3.5 py-1 text-xs font-semibold text-emerald-800 shadow-sm dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-300 dark:shadow-[0_0_12px_rgba(16,185,129,0.15)]">
                {/* Solid green neon dot (no blinking per user requirement) */}
                <span
                  aria-hidden="true"
                  className="h-2 w-2 rounded-full bg-emerald-500 shadow-[0_0_6px_#10b981] dark:bg-emerald-400 dark:shadow-[0_0_8px_#34d399]"
                />
                <span>Hệ sinh thái AI đang hoạt động tối ưu • Đồng bộ thời gian thực</span>
              </div>

              {dashboard && (
                <span className="text-xs font-medium text-[var(--cm-text-muted)]">
                  Cập nhật lúc{" "}
                  <time dateTime={dashboard.generatedAt}>
                    {formatGeneratedAt(dashboard.generatedAt)}
                  </time>
                </span>
              )}
            </div>

            {/* Main Title & Description */}
            <div className="flex flex-col gap-2">
              <div className="flex flex-wrap items-center gap-3">
                <h1 className="text-2xl font-black tracking-tight text-[var(--cm-text)] sm:text-3xl">
                  Tổng quan trung tâm
                </h1>
                <span className="rounded-xl border border-indigo-200 bg-indigo-50 px-3 py-1 text-xs font-bold text-indigo-700 backdrop-blur-md dark:border-indigo-400/30 dark:bg-indigo-500/15 dark:text-indigo-300">
                  {centerName}
                </span>
              </div>
              <p className="max-w-3xl text-sm leading-relaxed text-[var(--cm-text-secondary)]">
                Theo dõi quy mô, mức độ thành thạo và các lớp cần ưu tiên trong phạm vi trung tâm hiện tại.
              </p>
            </div>

            {/* Quick Action Shortcuts (Glassmorphism) */}
            <div className="flex flex-wrap items-center gap-3 pt-4 border-t border-slate-200/80 dark:border-[var(--cm-border-subtle)]">
              <span className="text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mr-1">
                Thao tác nhanh:
              </span>

              {/* 1. Giao bài tập nhanh */}
              {canCreateAssignment && (
                <Link
                  to="/quan-ly/bai-tap/tao-moi"
                  className="group flex items-center gap-2 rounded-xl border border-cyan-500/30 bg-cyan-50 px-4 py-2 text-xs font-semibold text-cyan-800 backdrop-blur-md transition-all duration-200 hover:bg-cyan-100 hover:border-cyan-500/50 hover:shadow-md hover:shadow-cyan-500/10 active:scale-95 dark:bg-cyan-500/10 dark:text-cyan-200 dark:hover:bg-cyan-500/20 dark:hover:border-cyan-400/50 dark:hover:shadow-lg dark:hover:shadow-cyan-500/20"
                >
                  <svg
                    className="h-4 w-4 text-cyan-600 transition-transform group-hover:rotate-90 dark:text-cyan-300"
                    fill="none"
                    viewBox="0 0 24 24"
                    stroke="currentColor"
                  >
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M12 4v16m8-8H4" />
                  </svg>
                  <span>Giao bài tập nhanh</span>
                </Link>
              )}

              {/* 2. Mở lớp mới */}
              {canReadClasses && (
                <Link
                  to="/quan-ly/lop-hoc"
                  className="group flex items-center gap-2 rounded-xl border border-indigo-500/30 bg-indigo-50 px-4 py-2 text-xs font-semibold text-indigo-800 backdrop-blur-md transition-all duration-200 hover:bg-indigo-100 hover:border-indigo-500/50 hover:shadow-md hover:shadow-indigo-500/10 active:scale-95 dark:bg-indigo-500/10 dark:text-indigo-200 dark:hover:bg-indigo-500/20 dark:hover:border-indigo-400/50 dark:hover:shadow-lg dark:hover:shadow-indigo-500/20"
                >
                  <svg
                    className="h-4 w-4 text-indigo-600 transition-transform group-hover:scale-110 dark:text-indigo-300"
                    fill="none"
                    viewBox="0 0 24 24"
                    stroke="currentColor"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4"
                    />
                  </svg>
                  <span>Mở lớp mới</span>
                </Link>
              )}

              {/* 3. Duyệt bài cần can thiệp (kèm badge thông báo) */}
              {canReview && (
                <Link
                  to="/quan-ly/duyet-bai"
                  className="group flex items-center gap-2.5 rounded-xl border border-amber-500/30 bg-amber-50 px-4 py-2 text-xs font-semibold text-amber-900 backdrop-blur-md transition-all duration-200 hover:bg-amber-100 hover:border-amber-500/50 hover:shadow-md hover:shadow-amber-500/10 active:scale-95 dark:bg-amber-500/10 dark:text-amber-200 dark:hover:bg-amber-500/20 dark:hover:border-amber-400/50 dark:hover:shadow-lg dark:hover:shadow-amber-500/20"
                >
                  <svg
                    className="h-4 w-4 text-amber-600 transition-transform group-hover:scale-110 dark:text-amber-300"
                    fill="none"
                    viewBox="0 0 24 24"
                    stroke="currentColor"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4"
                    />
                  </svg>
                  <span>Duyệt bài cần can thiệp</span>
                  <span className="inline-flex items-center justify-center rounded-full border border-amber-500/30 bg-amber-200/60 px-2 py-0.5 text-[10px] font-bold text-amber-900 dark:border-amber-400/40 dark:bg-amber-400/20 dark:text-amber-300">
                    {reviewQueueCount > 0 ? `${reviewQueueCount} cần duyệt` : "Hàng đợi"}
                  </span>
                </Link>
              )}
            </div>
          </div>
        </section>

        <section aria-label="Bộ lọc tổng quan" className="cm-surface flex flex-col gap-3 p-4 sm:flex-row sm:items-end sm:justify-between">
          <label className="text-xs font-semibold text-[var(--cm-text-secondary)]">
            Môn học
            <select
              value={selectedSubjectId}
              onChange={(event) => setSearchParams(event.target.value ? { subjectId: event.target.value } : {})}
              disabled={subjectsQuery.isLoading || subjectsQuery.isError}
              className="cm-field mt-1 block min-w-64 px-3"
            >
              <option value="">Tất cả môn học</option>
              {subjectsQuery.data?.data.map((subject) => (
                <option key={subject.subjectId} value={subject.subjectId}>{subject.subjectName}</option>
              ))}
            </select>
          </label>
          <button type="button" className="cm-secondary-button" onClick={() => dashboardQuery.refetch()} disabled={dashboardQuery.isFetching}>
            {dashboardQuery.isFetching ? "Đang làm mới…" : "Làm mới dữ liệu"}
          </button>
        </section>

        {subjectsQuery.isError && <SafeErrorPanel error={subjectsQuery.error} fallback="Không thể tải danh sách môn học." onRetry={() => subjectsQuery.refetch()} />}
        {dashboardQuery.isLoading && <DashboardSkeleton />}
        {dashboardQuery.isError && <SafeErrorPanel error={dashboardQuery.error} fallback="Không thể tải dữ liệu tổng quan trung tâm." onRetry={() => dashboardQuery.refetch()} />}

        {!dashboardQuery.isLoading && !dashboardQuery.isError && dashboard && (
          <>
            {/* 🚀 B. Lột xác 4 Metric KPI Cards với Gradient & 3D Vector Glow */}
            <section aria-label="Chỉ số quy mô trung tâm" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
              {/* Card 1: Giáo viên */}
              <article className="group relative overflow-hidden rounded-2xl border border-slate-200 bg-[var(--cm-surface)] p-5 sm:p-6 shadow-sm transition-all duration-300 hover:-translate-y-1 hover:border-violet-500/40 hover:shadow-xl hover:shadow-violet-500/10 dark:border-[var(--cm-border-subtle)] dark:shadow-none">
                <div
                  aria-hidden="true"
                  className="pointer-events-none absolute -right-6 -bottom-6 h-28 w-28 rounded-full bg-violet-500/5 blur-2xl group-hover:scale-150 transition-transform duration-500 dark:bg-violet-500/10"
                />
                <div
                  aria-hidden="true"
                  className="absolute inset-x-0 bottom-0 h-1 bg-gradient-to-r from-violet-500 to-indigo-600 opacity-60 group-hover:opacity-100 transition-opacity"
                />

                <div className="flex items-start justify-between gap-4">
                  <div>
                    <p className="text-xs font-bold uppercase tracking-wider text-[var(--cm-text-muted)]">
                      Giáo viên
                    </p>
                    <p className="mt-2 text-3xl font-black tracking-tight text-[var(--cm-text)]">
                      {dashboard.summary.teacherCount}
                    </p>
                  </div>
                  <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-violet-500 to-indigo-600 text-white shadow-lg shadow-indigo-500/25 ring-1 ring-white/20 transition-transform duration-300 group-hover:scale-110">
                    <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z"
                      />
                    </svg>
                  </div>
                </div>

                <div className="mt-4 flex flex-wrap items-center gap-2">
                  <span className="inline-flex items-center gap-1.5 rounded-full border border-violet-500/20 bg-violet-50 px-2.5 py-0.5 text-[11px] font-semibold text-violet-700 dark:border-violet-500/30 dark:bg-violet-500/10 dark:text-violet-300">
                    <span className="h-1.5 w-1.5 rounded-full bg-violet-500 dark:bg-violet-400" />
                    100% hoạt động
                  </span>
                  <span className="text-xs text-[var(--cm-text-muted)]">Hồ sơ giảng dạy</span>
                </div>
              </article>

              {/* Card 2: Học sinh */}
              <article className="group relative overflow-hidden rounded-2xl border border-slate-200 bg-[var(--cm-surface)] p-5 sm:p-6 shadow-sm transition-all duration-300 hover:-translate-y-1 hover:border-cyan-500/40 hover:shadow-xl hover:shadow-cyan-500/10 dark:border-[var(--cm-border-subtle)] dark:shadow-none">
                <div
                  aria-hidden="true"
                  className="pointer-events-none absolute -right-6 -bottom-6 h-28 w-28 rounded-full bg-cyan-500/5 blur-2xl group-hover:scale-150 transition-transform duration-500 dark:bg-cyan-500/10"
                />
                <div
                  aria-hidden="true"
                  className="absolute inset-x-0 bottom-0 h-1 bg-gradient-to-r from-cyan-400 to-blue-600 opacity-60 group-hover:opacity-100 transition-opacity"
                />

                <div className="flex items-start justify-between gap-4">
                  <div>
                    <p className="text-xs font-bold uppercase tracking-wider text-[var(--cm-text-muted)]">
                      Học sinh
                    </p>
                    <p className="mt-2 text-3xl font-black tracking-tight text-[var(--cm-text)]">
                      {dashboard.summary.studentCount}
                    </p>
                  </div>
                  <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-cyan-400 to-blue-600 text-white shadow-lg shadow-cyan-500/25 ring-1 ring-white/20 transition-transform duration-300 group-hover:scale-110">
                    <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M12 14l9-5-9-5-9 5 9 5zm0 0l6.16-3.422a12.083 12.083 0 01.665 6.479A11.952 11.952 0 0012 20.055a11.952 11.952 0 00-6.824-2.998 12.078 12.078 0 01.665-6.479L12 14zm-4 6v-7.5"
                      />
                    </svg>
                  </div>
                </div>

                <div className="mt-4 flex flex-wrap items-center gap-2">
                  <span className="inline-flex items-center gap-1.5 rounded-full border border-cyan-500/20 bg-cyan-50 px-2.5 py-0.5 text-[11px] font-semibold text-cyan-700 dark:border-cyan-500/30 dark:bg-cyan-500/10 dark:text-cyan-300">
                    <span className="h-1.5 w-1.5 rounded-full bg-cyan-500 dark:bg-cyan-400" />
                    Đã kết nối Digital Twin
                  </span>
                  <span className="text-xs text-[var(--cm-text-muted)]">Hồ sơ năng lực</span>
                </div>
              </article>

              {/* Card 3: Lớp học */}
              <article className="group relative overflow-hidden rounded-2xl border border-slate-200 bg-[var(--cm-surface)] p-5 sm:p-6 shadow-sm transition-all duration-300 hover:-translate-y-1 hover:border-emerald-500/40 hover:shadow-xl hover:shadow-emerald-500/10 dark:border-[var(--cm-border-subtle)] dark:shadow-none">
                <div
                  aria-hidden="true"
                  className="pointer-events-none absolute -right-6 -bottom-6 h-28 w-28 rounded-full bg-emerald-500/5 blur-2xl group-hover:scale-150 transition-transform duration-500 dark:bg-emerald-500/10"
                />
                <div
                  aria-hidden="true"
                  className="absolute inset-x-0 bottom-0 h-1 bg-gradient-to-r from-emerald-400 to-teal-600 opacity-60 group-hover:opacity-100 transition-opacity"
                />

                <div className="flex items-start justify-between gap-4">
                  <div>
                    <p className="text-xs font-bold uppercase tracking-wider text-[var(--cm-text-muted)]">
                      Lớp học
                    </p>
                    <p className="mt-2 text-3xl font-black tracking-tight text-[var(--cm-text)]">
                      {dashboard.summary.classCount}
                    </p>
                  </div>
                  <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-emerald-400 to-teal-600 text-white shadow-lg shadow-emerald-500/25 ring-1 ring-white/20 transition-transform duration-300 group-hover:scale-110">
                    <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4"
                      />
                    </svg>
                  </div>
                </div>

                <div className="mt-4 flex flex-wrap items-center gap-2">
                  <span className="inline-flex items-center gap-1.5 rounded-full border border-emerald-500/20 bg-emerald-50 px-2.5 py-0.5 text-[11px] font-semibold text-emerald-700 dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-300">
                    <span className="h-1.5 w-1.5 rounded-full bg-emerald-500 dark:bg-emerald-400" />
                    Đang hoạt động
                  </span>
                  <span className="text-xs text-[var(--cm-text-muted)]">Phân bổ năm học</span>
                </div>
              </article>

              {/* Card 4: Môn học được đo lường */}
              <article className="group relative overflow-hidden rounded-2xl border border-slate-200 bg-[var(--cm-surface)] p-5 sm:p-6 shadow-sm transition-all duration-300 hover:-translate-y-1 hover:border-amber-500/40 hover:shadow-xl hover:shadow-amber-500/10 dark:border-[var(--cm-border-subtle)] dark:shadow-none">
                <div
                  aria-hidden="true"
                  className="pointer-events-none absolute -right-6 -bottom-6 h-28 w-28 rounded-full bg-amber-500/5 blur-2xl group-hover:scale-150 transition-transform duration-500 dark:bg-amber-500/10"
                />
                <div
                  aria-hidden="true"
                  className="absolute inset-x-0 bottom-0 h-1 bg-gradient-to-r from-amber-400 to-rose-500 opacity-60 group-hover:opacity-100 transition-opacity"
                />

                <div className="flex items-start justify-between gap-4">
                  <div>
                    <p className="text-xs font-bold uppercase tracking-wider text-[var(--cm-text-muted)]">
                      Môn học được đo lường
                    </p>
                    <p className="mt-2 text-3xl font-black tracking-tight text-[var(--cm-text)]">
                      {dashboard.masteryBySubject.length}
                    </p>
                  </div>
                  <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-amber-400 to-rose-500 text-white shadow-lg shadow-amber-500/25 ring-1 ring-white/20 transition-transform duration-300 group-hover:scale-110">
                    <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"
                      />
                    </svg>
                  </div>
                </div>

                <div className="mt-4 flex flex-wrap items-center gap-2">
                  <span className="inline-flex items-center gap-1.5 rounded-full border border-amber-500/20 bg-amber-50 px-2.5 py-0.5 text-[11px] font-semibold text-amber-800 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-300">
                    <span className="h-1.5 w-1.5 rounded-full bg-amber-500 dark:bg-amber-400" />
                    Đồ thị tri thức Knowledge Graph
                  </span>
                  <span className="text-xs text-[var(--cm-text-muted)]">Phân tích mastery</span>
                </div>
              </article>
            </section>

            <div className="grid gap-6 xl:grid-cols-[minmax(0,1.1fr)_minmax(22rem,0.9fr)]">
              <section className="cm-surface p-5 sm:p-6" aria-labelledby="mastery-heading">
                <div className="border-b border-[var(--cm-border-subtle)] pb-4">
                  <h2 id="mastery-heading" className="text-base font-semibold text-[var(--cm-text)]">Mức độ thành thạo theo môn</h2>
                  <p className="mt-1 text-xs text-[var(--cm-text-secondary)]">Giá trị trung bình từ dữ liệu học tập đã ghi nhận.</p>
                </div>
                {dashboard.masteryBySubject.length === 0 ? (
                  <p className="py-12 text-center text-sm text-[var(--cm-text-muted)]">Chưa có dữ liệu mastery cho bộ lọc hiện tại.</p>
                ) : (
                  <div className="mt-5 space-y-5">
                    {dashboard.masteryBySubject.map((subject) => {
                      const percentage = clampPercentage(subject.averageMastery);
                      return (
                        <div key={subject.subjectId}>
                          <div className="mb-2 flex items-center justify-between gap-4 text-sm">
                            <span className="truncate font-medium text-[var(--cm-text)]">{subject.subjectName}</span>
                            <span className="font-semibold text-cyan-300">{subject.averageMastery.toFixed(1)}%</span>
                          </div>
                          <div className="h-2 overflow-hidden rounded-full bg-slate-800" role="progressbar" aria-label={`Mastery ${subject.subjectName}`} aria-valuemin={0} aria-valuemax={100} aria-valuenow={percentage}>
                            <div className="h-full rounded-full bg-gradient-to-r from-indigo-500 to-cyan-400" style={{ width: `${percentage}%` }} />
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </section>

              <section className="cm-surface p-5 sm:p-6" aria-labelledby="risk-heading">
                <div className="border-b border-[var(--cm-border-subtle)] pb-4">
                  <h2 id="risk-heading" className="text-base font-semibold text-[var(--cm-text)]">Lớp cần chú ý</h2>
                  <p className="mt-1 text-xs text-[var(--cm-text-secondary)]">Số học sinh nguy cơ cao theo dữ liệu tổng hợp.</p>
                </div>
                {dashboard.highRiskByClass.length === 0 ? (
                  <p className="py-12 text-center text-sm text-[var(--cm-text-muted)]">Chưa ghi nhận lớp có dữ liệu nguy cơ.</p>
                ) : (
                  <ul className="mt-3 divide-y divide-[var(--cm-border-subtle)]">
                    {[...dashboard.highRiskByClass].sort((a, b) => b.highRiskStudentCount - a.highRiskStudentCount).map((item) => (
                      <li key={item.classId} className="flex items-center justify-between gap-4 py-3">
                        <div className="min-w-0">
                          <p className="truncate text-sm font-semibold text-[var(--cm-text)]">{item.className}</p>
                          <p className="text-xs text-[var(--cm-text-muted)]">Tổng số {item.totalStudentCount} học sinh</p>
                        </div>
                        <StatusBadge status={item.highRiskStudentCount > 0 ? "warning" : "active"} label={`${item.highRiskStudentCount} nguy cơ`} tone={item.highRiskStudentCount > 0 ? "warning" : "success"} />
                      </li>
                    ))}
                  </ul>
                )}
              </section>
            </div>

            <section className="cm-surface overflow-hidden" aria-labelledby="ranking-heading">
              <div className="flex flex-col gap-1 border-b border-[var(--cm-border-subtle)] p-5 sm:flex-row sm:items-center sm:justify-between sm:px-6">
                <div>
                  <h2 id="ranking-heading" className="text-base font-semibold text-[var(--cm-text)]">Xếp hạng lớp</h2>
                  <p className="mt-1 text-xs text-[var(--cm-text-secondary)]">Mastery và tiến độ hoàn thành theo phản hồi API.</p>
                </div>
                <span className="text-xs text-[var(--cm-text-muted)]">{dashboard.classRanking.length} lớp</span>
              </div>
              {dashboard.classRanking.length === 0 ? (
                <p className="p-10 text-center text-sm text-[var(--cm-text-muted)]">Chưa có dữ liệu xếp hạng lớp.</p>
              ) : (
                <div className="cm-table-scroll overflow-x-auto">
                  <table className="min-w-full text-left text-sm">
                    <caption className="sr-only">Bảng xếp hạng lớp trong trung tâm</caption>
                    <thead className="bg-white/[0.025] text-xs uppercase tracking-wide text-[var(--cm-text-muted)]">
                      <tr>
                        <th scope="col" className="px-5 py-3">Hạng</th>
                        <th scope="col" className="px-5 py-3">Lớp học</th>
                        <th scope="col" className="px-5 py-3">Môn học</th>
                        <th scope="col" className="px-5 py-3">Mastery</th>
                        <th scope="col" className="px-5 py-3">Hoàn thành</th>
                        <th scope="col" className="px-5 py-3 text-right">Thao tác</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-[var(--cm-border-subtle)]">
                      {dashboard.classRanking.map((item) => (
                        <tr key={item.classId} className="hover:bg-white/[0.025]">
                          <td className="px-5 py-4 font-semibold text-cyan-300">#{item.rank}</td>
                          <td className="px-5 py-4 font-semibold text-[var(--cm-text)]">{item.className}</td>
                          <td className="px-5 py-4 text-[var(--cm-text-secondary)]">{item.subjectName}</td>
                          <td className="px-5 py-4 text-[var(--cm-text)]">{item.averageMastery.toFixed(1)}%</td>
                          <td className="px-5 py-4 text-[var(--cm-text)]">{item.assignmentCompletionRate.toFixed(1)}%</td>
                          <td className="px-5 py-4 text-right">
                            <Link className="cm-focus-ring rounded-lg text-sm font-semibold text-cyan-300 hover:text-cyan-200" to={`/quan-ly/lop-hoc/${item.classId}/tong-quan`}>Xem lớp →</Link>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </section>
          </>
        )}
      </div>
    </div>
  );
};
