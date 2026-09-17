import { useState, useEffect } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { getClassDashboard } from "../api/dashboardsApi";
import { organizationApi } from "../api/organizationApi";
import { useAuthStore } from "../stores/authStore";
import type { ClassDashboardDataDto } from "../types/dashboards";

export const TeacherClassDashboardPage = () => {
  const { classId: routeClassId } = useParams<{ classId?: string }>();
  const navigate = useNavigate();
  const user = useAuthStore((state) => state.user);
  const isCenterManager = user?.accountType === "CenterManager";

  // Load teacher classes
  const { data: classesData, isLoading: classesLoading } = useQuery({
    queryKey: ["teacherClassesList"],
    queryFn: () => organizationApi.listClasses({ page: 1, pageSize: 50 }),
  });

  const [selectedClassId, setSelectedClassId] = useState<string>(routeClassId || "");

  useEffect(() => {
    if (routeClassId) {
      setSelectedClassId(routeClassId);
    } else if (classesData?.data && classesData.data.length > 0 && !selectedClassId) {
      const firstId = classesData.data[0].classId;
      setSelectedClassId(firstId);
      navigate(`/quan-ly/lop-hoc/${firstId}/tong-quan`, { replace: true });
    }
  }, [routeClassId, classesData, selectedClassId, navigate]);

  const handleSelectClass = (newId: string) => {
    setSelectedClassId(newId);
    navigate(`/quan-ly/lop-hoc/${newId}/tong-quan`);
  };

  // Load class dashboard data
  const {
    data: dashboard,
    isLoading: dashboardLoading,
    isError,
    error,
    refetch,
  } = useQuery<ClassDashboardDataDto>({
    queryKey: ["classDashboard", selectedClassId],
    queryFn: () => getClassDashboard(selectedClassId),
    enabled: !!selectedClassId,
  });

  if (isCenterManager) {
    return (
      <div className="px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
        <div className="mx-auto max-w-[96rem] space-y-6">
          {/* Header Breadcrumbs and Class Picker */}
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-[var(--cm-border-subtle)] pb-4">
            <div>
              <div className="flex items-center gap-2 text-xs font-semibold text-[var(--cm-text-secondary)] mb-1">
                <Link to="/quan-ly/tong-quan-trung-tam" className="hover:text-cyan-400">Tổng quan</Link>
                <span>/</span>
                <span className="text-[var(--cm-text)]">Tiến độ và báo cáo</span>
              </div>
              <h1 className="text-2xl font-bold text-[var(--cm-text)]">
                Tổng Quan Lớp Học & Phân Tích Năng Lực
              </h1>
              <p className="mt-1 text-sm text-[var(--cm-text-secondary)]">
                Giám sát tiến độ học tập, phát hiện học sinh có nguy cơ và các nhóm hổng kiến thức.
              </p>
            </div>

            <div className="flex items-center gap-3">
              {classesLoading ? (
                <span className="text-xs text-[var(--cm-text-muted)]">Đang tải danh sách lớp...</span>
              ) : (
                <select
                  value={selectedClassId}
                  onChange={(e) => handleSelectClass(e.target.value)}
                  className="cm-select min-w-[200px]"
                >
                  {classesData?.data.map((c) => (
                    <option key={c.classId} value={c.classId}>
                      {c.className} ({c.academicYear})
                    </option>
                  ))}
                </select>
              )}

              <button
                type="button"
                onClick={() => refetch()}
                className="cm-secondary-button"
              >
                Làm mới
              </button>
            </div>
          </div>

          {/* Loading / Error States */}
          {dashboardLoading && (
            <div className="cm-surface flex items-center justify-center p-12">
              <span className="font-medium text-cyan-400 animate-pulse">
                Đang tải dữ liệu Dashboard lớp học...
              </span>
            </div>
          )}

          {isError && (
            <div className="cm-surface p-8 text-center border-rose-500/30">
              <h2 className="text-lg font-bold text-rose-400">Không thể tải Dashboard lớp học</h2>
              <p className="mt-2 text-sm text-[var(--cm-text-secondary)]">
                {(error as Error)?.message || "Vui lòng kiểm tra quyền truy cập lớp học này."}
              </p>
              <button
                type="button"
                onClick={() => refetch()}
                className="cm-primary-button mt-4"
              >
                Thử lại
              </button>
            </div>
          )}

          {!dashboardLoading && !isError && dashboard && (
            <>
              {/* Overview KPI Cards */}
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
                <div className="cm-surface p-5">
                  <span className="text-xs font-bold uppercase tracking-wider text-[var(--cm-text-muted)]">
                    Sĩ số lớp
                  </span>
                  <div className="mt-2 flex items-baseline gap-2">
                    <span className="text-3xl font-black text-[var(--cm-text)]">
                      {dashboard.overview.studentCount}
                    </span>
                    <span className="text-xs text-[var(--cm-text-muted)]">học sinh</span>
                  </div>
                  <p className="mt-1 text-xs text-[var(--cm-text-muted)]">Đã kích hoạt Hồ sơ Năng lực</p>
                </div>

                <div className="cm-surface p-5">
                  <span className="text-xs font-bold uppercase tracking-wider text-[var(--cm-text-muted)]">
                    Năng lực trung bình (Mastery)
                  </span>
                  <div className="mt-2 flex items-baseline gap-2">
                    <span className="text-3xl font-black text-cyan-400">
                      {dashboard.overview.averageMastery.toFixed(1)}%
                    </span>
                  </div>
                  <p className="mt-1 text-xs text-[var(--cm-text-muted)]">Toàn bộ chuyên đề môn học</p>
                </div>

                <div className="cm-surface p-5">
                  <span className="text-xs font-bold uppercase tracking-wider text-[var(--cm-text-muted)]">
                    Học sinh có nguy cơ cao
                  </span>
                  <div className="mt-2 flex items-baseline gap-2">
                    <span
                      className={`text-3xl font-black ${
                        dashboard.highRiskStudents.length > 0 ? "text-rose-400" : "text-emerald-400"
                      }`}
                    >
                      {dashboard.highRiskStudents.length}
                    </span>
                    <span className="text-xs text-[var(--cm-text-muted)]">em</span>
                  </div>
                  <p className="mt-1 text-xs text-[var(--cm-text-muted)]">Nguy cơ tụt hậu & điểm kém</p>
                </div>

                <div className="cm-surface p-5">
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-bold uppercase tracking-wider text-[var(--cm-text-muted)]">
                      Tỷ lệ hoàn thành bài
                    </span>
                    <Link
                      to={`/quan-ly/duyet-bai?classId=${selectedClassId}`}
                      className="text-xs font-semibold text-cyan-400 hover:underline"
                    >
                      Duyệt bài →
                    </Link>
                  </div>
                  <div className="mt-2 flex items-baseline gap-2">
                    <span className="text-3xl font-black text-[var(--cm-text)]">
                      {dashboard.overview.assignmentCompletionRate.toFixed(0)}%
                    </span>
                  </div>
                  <p className="mt-1 text-xs text-[var(--cm-text-muted)]">
                    Điểm dự báo TB: {dashboard.overview.averagePredictedScore.toFixed(1)}/10
                  </p>
                </div>
              </div>

              {/* Gap Groups & Remediation */}
              {dashboard.gapGroups && dashboard.gapGroups.length > 0 && (
                <div className="cm-surface p-6 border-indigo-500/30 bg-gradient-to-br from-indigo-950/40 to-purple-950/40">
                  <div className="flex items-center justify-between mb-4">
                    <div>
                      <h2 className="text-lg font-bold text-[var(--cm-text)]">
                        Nhóm Cần Bổ Trợ Kiến Thức (Gap Groups)
                      </h2>
                      <p className="text-xs text-[var(--cm-text-secondary)]">
                        Hệ thống tự động gom nhóm học sinh có cùng lỗ hổng kiến thức theo ngưỡng năng lực để giáo viên can thiệp tập trung.
                      </p>
                    </div>
                    <span className="rounded-full bg-indigo-500/20 border border-indigo-500/30 px-3 py-1 text-xs font-bold text-indigo-300">
                      {dashboard.gapGroups.length} nhóm cần can thiệp
                    </span>
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    {dashboard.gapGroups.map((group) => (
                      <div
                        key={group.groupKey}
                        className="rounded-xl bg-[var(--cm-surface-raised)] border border-[var(--cm-border-subtle)] p-5 flex flex-col justify-between"
                      >
                        <div>
                          <div className="flex items-center justify-between">
                            <span className="rounded bg-rose-500/10 border border-rose-500/20 px-2 py-0.5 text-xs font-bold text-rose-300">
                              Chuyên đề: {group.topicName}
                            </span>
                            <span className="text-xs font-semibold text-[var(--cm-text-muted)]">
                              {group.studentCount} học sinh
                            </span>
                          </div>
                          <p className="mt-2 text-xs text-[var(--cm-text)] font-medium leading-relaxed">
                            💡 Gợi ý bổ trợ: {group.suggestedAction}
                          </p>
                          <p className="mt-2 text-[11px] text-[var(--cm-text-muted)]">
                            Ngưỡng kích hoạt: &lt; {group.threshold}% độ thuần thục
                          </p>
                        </div>

                        <div className="mt-4 pt-3 border-t border-[var(--cm-border-subtle)] flex justify-end">
                          <Link
                            to={`/quan-ly/bai-tap/tao-moi?classId=${selectedClassId}&topicNodeId=${group.topicNodeId}&studentIds=${encodeURIComponent(group.studentIds.join(","))}`}
                            className="cm-primary-button text-xs py-1.5 px-3.5"
                          >
                            Giao bài tập bổ trợ cho nhóm →
                          </Link>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* High-Risk Students & Weak Topics */}
              <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                {/* High-risk students (2 cols) */}
                <div className="lg:col-span-2 cm-surface p-6">
                  <div className="flex items-center justify-between mb-4 border-b border-[var(--cm-border-subtle)] pb-3">
                    <div>
                      <h2 className="text-base font-bold text-[var(--cm-text)]">
                        Danh Sách Học Sinh Có Nguy Cơ Tụt Hậu
                      </h2>
                      <p className="text-xs text-[var(--cm-text-muted)]">
                        Dựa trên phân tích nguy cơ (Risk Score) và khoảng cách tới mục tiêu kỳ thi.
                      </p>
                    </div>
                    <span className="text-xs font-semibold text-[var(--cm-text-muted)]">
                      {dashboard.highRiskStudents.length} học sinh
                    </span>
                  </div>

                  {dashboard.highRiskStudents.length === 0 ? (
                    <div className="py-8 text-center text-sm text-[var(--cm-text-muted)]">
                      ✓ Lớp học không có học sinh nào vượt ngưỡng nguy cơ cao.
                    </div>
                  ) : (
                    <div className="overflow-x-auto">
                      <table className="min-w-full divide-y divide-[var(--cm-border-subtle)] text-left text-xs">
                        <thead className="text-[var(--cm-text-muted)] font-bold uppercase">
                          <tr>
                            <th className="py-2.5">Học sinh</th>
                            <th className="py-2.5">Mức nguy cơ</th>
                            <th className="py-2.5">Dự báo điểm</th>
                            <th className="py-2.5">Mục tiêu</th>
                            <th className="py-2.5 text-right">Hồ sơ Twin</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-[var(--cm-border-subtle)]">
                          {dashboard.highRiskStudents.map((s) => (
                            <tr key={s.studentId} className="hover:bg-[var(--cm-surface-muted)]/50">
                              <td className="py-3 font-semibold text-[var(--cm-text)] whitespace-nowrap">
                                {s.fullName}
                              </td>
                              <td className="py-3 whitespace-nowrap">
                                <span
                                  className={`rounded px-2 py-0.5 font-bold ${
                                    s.riskScore >= 70
                                      ? "bg-rose-500/10 text-rose-300 border border-rose-500/20"
                                      : "bg-amber-500/10 text-amber-300 border border-amber-500/20"
                                  }`}
                                >
                                  {s.riskScore.toFixed(1)}%
                                </span>
                              </td>
                              <td className="py-3 text-[var(--cm-text)] font-bold whitespace-nowrap">
                                {s.predictedScore.toFixed(1)} / 10
                              </td>
                              <td className="py-3 text-[var(--cm-text-secondary)] whitespace-nowrap">
                                Mục tiêu: {s.targetScore.toFixed(1)} (còn {s.remainingDays} ngày)
                              </td>
                              <td className="py-3 text-right whitespace-nowrap">
                                <Link
                                  to={`/quan-ly/hoc-sinh/${s.studentId}/nang-luc?subjectId=${dashboard.class.subjectId}`}
                                  className="rounded bg-indigo-500/10 border border-indigo-500/20 px-2.5 py-1 font-bold text-indigo-400 hover:bg-indigo-500/20"
                                >
                                  Xem Twin →
                                </Link>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>

                {/* Weak Topics (1 col) */}
                <div className="cm-surface p-6">
                  <h2 className="text-base font-bold text-[var(--cm-text)] mb-1">
                    Chuyên Đề Yếu Cần Củng Cố
                  </h2>
                  <p className="text-xs text-[var(--cm-text-muted)] mb-4 border-b border-[var(--cm-border-subtle)] pb-3">
                    Xếp hạng theo độ thuần thục trung bình của cả lớp từ thấp lên cao.
                  </p>

                  {dashboard.weakTopics.length === 0 ? (
                    <p className="text-xs text-[var(--cm-text-muted)] py-4 text-center">
                      Chưa ghi nhận chuyên đề nào dưới ngưỡng.
                    </p>
                  ) : (
                    <div className="space-y-4">
                      {dashboard.weakTopics.map((topic) => (
                        <div key={topic.topicNodeId} className="space-y-1">
                          <div className="flex items-center justify-between text-xs">
                            <span className="font-semibold text-[var(--cm-text)] truncate max-w-[180px]">
                              {topic.topicName}
                            </span>
                            <span className="font-bold text-[var(--cm-text)]">
                              {topic.averageMastery.toFixed(0)}%
                            </span>
                          </div>
                          <div className="h-2 rounded-full bg-[var(--cm-surface-muted)] overflow-hidden">
                            <div
                              className={`h-full rounded-full ${
                                topic.averageMastery < 50
                                  ? "bg-rose-500"
                                  : topic.averageMastery < 70
                                  ? "bg-amber-500"
                                  : "bg-indigo-500"
                              }`}
                              style={{ width: `${Math.min(100, Math.max(0, topic.averageMastery))}%` }}
                            />
                          </div>
                          <p className="text-[11px] text-[var(--cm-text-muted)]">
                            {topic.affectedStudentCount} học sinh chưa đạt yêu cầu
                          </p>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            </>
          )}
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-50 p-6">
      <div className="mx-auto max-w-7xl space-y-6">
        {/* Header Breadcrumbs and Class Picker */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-200 pb-4">
          <div>
            <div className="flex items-center gap-2 text-xs font-semibold text-slate-500 mb-1">
              <Link to="/" className="hover:text-indigo-600">Trang chủ</Link>
              <span>/</span>
              <span className="text-slate-900">Dashboard Lớp học</span>
            </div>
            <h1 className="text-2xl font-bold text-slate-900">
              Tổng Quan Lớp Học & Phân Tích Năng Lực
            </h1>
            <p className="mt-1 text-sm text-slate-500">
              Giám sát tiến độ học tập, phát hiện học sinh có nguy cơ và các nhóm hổng kiến thức.
            </p>
          </div>

          <div className="flex items-center gap-3">
            {classesLoading ? (
              <span className="text-xs text-slate-400">Đang tải danh sách lớp...</span>
            ) : (
              <select
                value={selectedClassId}
                onChange={(e) => handleSelectClass(e.target.value)}
                className="rounded-lg border border-slate-300 bg-white px-3.5 py-2 text-sm font-semibold text-slate-800 shadow-sm focus:border-indigo-500 focus:outline-none"
              >
                {classesData?.data.map((c) => (
                  <option key={c.classId} value={c.classId}>
                    {c.className} ({c.academicYear})
                  </option>
                ))}
              </select>
            )}

            <button
              onClick={() => refetch()}
              className="rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
            >
              Làm mới
            </button>
          </div>
        </div>

        {/* Loading / Error States */}
        {dashboardLoading && (
          <div className="flex items-center justify-center rounded-2xl bg-white p-12 shadow-sm ring-1 ring-slate-200">
            <span className="font-medium text-indigo-600 animate-pulse">
              Đang tải dữ liệu Dashboard lớp học...
            </span>
          </div>
        )}

        {isError && (
          <div className="rounded-2xl bg-white p-8 text-center shadow-sm ring-1 ring-slate-200">
            <h2 className="text-lg font-bold text-red-600">Không thể tải Dashboard lớp học</h2>
            <p className="mt-2 text-sm text-slate-500">
              {(error as Error)?.message || "Vui lòng kiểm tra quyền truy cập lớp học này."}
            </p>
            <button
              onClick={() => refetch()}
              className="mt-4 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500"
            >
              Thử lại
            </button>
          </div>
        )}

        {!dashboardLoading && !isError && dashboard && (
          <>
            {/* Overview KPI Cards */}
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              <div className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
                  Sĩ số lớp
                </span>
                <div className="mt-2 flex items-baseline gap-2">
                  <span className="text-3xl font-black text-slate-900">
                    {dashboard.overview.studentCount}
                  </span>
                  <span className="text-xs text-slate-500">học sinh</span>
                </div>
                <p className="mt-1 text-xs text-slate-500">Đã kích hoạt Hồ sơ Năng lực</p>
              </div>

              <div className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
                  Năng lực trung bình (Mastery)
                </span>
                <div className="mt-2 flex items-baseline gap-2">
                  <span className="text-3xl font-black text-indigo-600">
                    {dashboard.overview.averageMastery.toFixed(1)}%
                  </span>
                </div>
                <p className="mt-1 text-xs text-slate-500">Toàn bộ chuyên đề môn học</p>
              </div>

              <div className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
                  Học sinh có nguy cơ cao
                </span>
                <div className="mt-2 flex items-baseline gap-2">
                  <span
                    className={`text-3xl font-black ${
                      dashboard.highRiskStudents.length > 0 ? "text-red-600" : "text-emerald-600"
                    }`}
                  >
                    {dashboard.highRiskStudents.length}
                  </span>
                  <span className="text-xs text-slate-500">em</span>
                </div>
                <p className="mt-1 text-xs text-slate-500">Nguy cơ tụt hậu & điểm kém</p>
              </div>

              <div className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
                    Tỷ lệ hoàn thành bài
                  </span>
                  <Link
                    to={`/quan-ly/duyet-bai?classId=${selectedClassId}`}
                    className="text-xs font-semibold text-indigo-600 hover:underline"
                  >
                    Duyệt bài →
                  </Link>
                </div>
                <div className="mt-2 flex items-baseline gap-2">
                  <span className="text-3xl font-black text-slate-900">
                    {dashboard.overview.assignmentCompletionRate.toFixed(0)}%
                  </span>
                </div>
                <p className="mt-1 text-xs text-slate-500">
                  Điểm dự báo TB: {dashboard.overview.averagePredictedScore.toFixed(1)}/10
                </p>
              </div>
            </div>

            {/* Gap Groups & Remediation */}
            {dashboard.gapGroups && dashboard.gapGroups.length > 0 && (
              <div className="rounded-xl bg-gradient-to-br from-indigo-50/70 to-purple-50/70 p-6 shadow-sm ring-1 ring-indigo-200">
                <div className="flex items-center justify-between mb-4">
                  <div>
                    <h2 className="text-lg font-bold text-slate-900">
                      Nhóm Cần Bổ Trợ Kiến Thức (Gap Groups)
                    </h2>
                    <p className="text-xs text-slate-600">
                      Hệ thống tự động gom nhóm học sinh có cùng lỗ hổng kiến thức theo ngưỡng năng lực để giáo viên can thiệp tập trung.
                    </p>
                  </div>
                  <span className="rounded-full bg-indigo-100 px-3 py-1 text-xs font-bold text-indigo-700">
                    {dashboard.gapGroups.length} nhóm cần can thiệp
                  </span>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {dashboard.gapGroups.map((group) => (
                    <div
                      key={group.groupKey}
                      className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200 flex flex-col justify-between"
                    >
                      <div>
                        <div className="flex items-center justify-between">
                          <span className="rounded bg-rose-50 px-2 py-0.5 text-xs font-bold text-rose-700">
                            Chuyên đề: {group.topicName}
                          </span>
                          <span className="text-xs font-semibold text-slate-500">
                            {group.studentCount} học sinh
                          </span>
                        </div>
                        <p className="mt-2 text-xs text-slate-600 font-medium leading-relaxed">
                          💡 Gợi ý bổ trợ: {group.suggestedAction}
                        </p>
                        <p className="mt-2 text-[11px] text-slate-400">
                          Ngưỡng kích hoạt: &lt; {group.threshold}% độ thuần thục
                        </p>
                      </div>

                      <div className="mt-4 pt-3 border-t border-slate-100 flex justify-end">
                        <Link
                          to={`/quan-ly/bai-tap/tao-moi?classId=${selectedClassId}&topicNodeId=${group.topicNodeId}&studentIds=${encodeURIComponent(group.studentIds.join(","))}`}
                          className="rounded-lg bg-indigo-600 px-3.5 py-1.5 text-xs font-bold text-white shadow-sm hover:bg-indigo-500"
                        >
                          Giao bài tập bổ trợ cho nhóm →
                        </Link>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* High-Risk Students & Weak Topics */}
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
              {/* High-risk students (2 cols) */}
              <div className="lg:col-span-2 rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
                <div className="flex items-center justify-between mb-4 border-b border-slate-100 pb-3">
                  <div>
                    <h2 className="text-base font-bold text-slate-900">
                      Danh Sách Học Sinh Có Nguy Cơ Tụt Hậu
                    </h2>
                    <p className="text-xs text-slate-500">
                      Dựa trên phân tích nguy cơ (Risk Score) và khoảng cách tới mục tiêu kỳ thi.
                    </p>
                  </div>
                  <span className="text-xs font-semibold text-slate-500">
                    {dashboard.highRiskStudents.length} học sinh
                  </span>
                </div>

                {dashboard.highRiskStudents.length === 0 ? (
                  <div className="py-8 text-center text-sm text-slate-500">
                    ✓ Lớp học không có học sinh nào vượt ngưỡng nguy cơ cao.
                  </div>
                ) : (
                  <div className="overflow-x-auto">
                    <table className="min-w-full divide-y divide-slate-200 text-left text-xs">
                      <thead className="text-slate-400 font-bold uppercase">
                        <tr>
                          <th className="py-2.5">Học sinh</th>
                          <th className="py-2.5">Mức nguy cơ</th>
                          <th className="py-2.5">Dự báo điểm</th>
                          <th className="py-2.5">Mục tiêu</th>
                          <th className="py-2.5 text-right">Hồ sơ Twin</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-100">
                        {dashboard.highRiskStudents.map((s) => (
                          <tr key={s.studentId} className="hover:bg-slate-50">
                            <td className="py-3 font-semibold text-slate-900 whitespace-nowrap">
                              {s.fullName}
                            </td>
                            <td className="py-3 whitespace-nowrap">
                              <span
                                className={`rounded px-2 py-0.5 font-bold ${
                                  s.riskScore >= 70
                                    ? "bg-red-100 text-red-800"
                                    : "bg-amber-100 text-amber-800"
                                }`}
                              >
                                {s.riskScore.toFixed(1)}%
                              </span>
                            </td>
                            <td className="py-3 text-slate-900 font-bold whitespace-nowrap">
                              {s.predictedScore.toFixed(1)} / 10
                            </td>
                            <td className="py-3 text-slate-600 whitespace-nowrap">
                              Mục tiêu: {s.targetScore.toFixed(1)} (còn {s.remainingDays} ngày)
                            </td>
                            <td className="py-3 text-right whitespace-nowrap">
                              <Link
                                to={`/quan-ly/hoc-sinh/${s.studentId}/nang-luc?subjectId=${dashboard.class.subjectId}`}
                                className="rounded bg-indigo-50 px-2.5 py-1 font-bold text-indigo-700 hover:bg-indigo-100"
                              >
                                Xem Twin →
                              </Link>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>

              {/* Weak Topics (1 col) */}
              <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
                <h2 className="text-base font-bold text-slate-900 mb-1">
                  Chuyên Đề Yếu Cần Củng Cố
                </h2>
                <p className="text-xs text-slate-500 mb-4 border-b border-slate-100 pb-3">
                  Xếp hạng theo độ thuần thục trung bình của cả lớp từ thấp lên cao.
                </p>

                {dashboard.weakTopics.length === 0 ? (
                  <p className="text-xs text-slate-500 py-4 text-center">
                    Chưa ghi nhận chuyên đề nào dưới ngưỡng.
                  </p>
                ) : (
                  <div className="space-y-4">
                    {dashboard.weakTopics.map((topic) => (
                      <div key={topic.topicNodeId} className="space-y-1">
                        <div className="flex items-center justify-between text-xs">
                          <span className="font-semibold text-slate-800 truncate max-w-[180px]">
                            {topic.topicName}
                          </span>
                          <span className="font-bold text-slate-900">
                            {topic.averageMastery.toFixed(0)}%
                          </span>
                        </div>
                        <div className="h-2 rounded-full bg-slate-100 overflow-hidden">
                          <div
                            className={`h-full rounded-full ${
                              topic.averageMastery < 50
                                ? "bg-red-500"
                                : topic.averageMastery < 70
                                ? "bg-amber-500"
                                : "bg-indigo-500"
                            }`}
                            style={{ width: `${Math.min(100, Math.max(0, topic.averageMastery))}%` }}
                          />
                        </div>
                        <p className="text-[11px] text-slate-400">
                          {topic.affectedStudentCount} học sinh chưa đạt yêu cầu
                        </p>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
};
