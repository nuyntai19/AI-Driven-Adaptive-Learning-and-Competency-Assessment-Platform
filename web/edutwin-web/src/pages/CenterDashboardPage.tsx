import { useQuery } from "@tanstack/react-query";
import { Link, useSearchParams } from "react-router-dom";
import { getCenterDashboard } from "../api/dashboardsApi";
import { organizationApi } from "../api/organizationApi";
import { MetricCard, PageHeader, SafeErrorPanel, Skeleton, StatusBadge } from "../components/centerManager";
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
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {Array.from({ length: 4 }, (_, index) => <Skeleton key={index} decorative className="h-32 w-full rounded-2xl" />)}
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
  const subjectsQuery = useQuery({
    queryKey: ["subjects", "center-dashboard", "active"],
    queryFn: () => organizationApi.listSubjects(true),
  });
  const dashboardQuery = useQuery<CenterDashboardDataDto>({
    queryKey: ["centerDashboard", selectedSubjectId],
    queryFn: () => getCenterDashboard(selectedSubjectId || undefined),
  });
  const dashboard = dashboardQuery.data;

  return (
    <div className="px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
      <div className="mx-auto max-w-[96rem] space-y-6">
        <PageHeader
          eyebrow="Center overview"
          title="Tổng quan trung tâm"
          description="Theo dõi quy mô, mức độ thành thạo và các lớp cần ưu tiên trong phạm vi trung tâm hiện tại."
          actions={dashboard && (
            <p className="text-xs text-[var(--cm-text-muted)]">
              Dữ liệu lúc <time dateTime={dashboard.generatedAt}>{formatGeneratedAt(dashboard.generatedAt)}</time>
            </p>
          )}
        />

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
            <section aria-label="Chỉ số quy mô trung tâm" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
              <MetricCard label="Giáo viên" value={dashboard.summary.teacherCount} supportingText="Tổng số hồ sơ trong dữ liệu tổng hợp" icon={<span aria-hidden="true">GV</span>} />
              <MetricCard label="Học sinh" value={dashboard.summary.studentCount} supportingText="Tổng số hồ sơ trong dữ liệu tổng hợp" icon={<span aria-hidden="true">HS</span>} />
              <MetricCard label="Lớp học" value={dashboard.summary.classCount} supportingText="Tổng số lớp trong dữ liệu tổng hợp" icon={<span aria-hidden="true">LH</span>} />
              <MetricCard label="Môn học được đo lường" value={dashboard.masteryBySubject.length} supportingText="Có dữ liệu mastery trong phản hồi hiện tại" icon={<span aria-hidden="true">MH</span>} />
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
