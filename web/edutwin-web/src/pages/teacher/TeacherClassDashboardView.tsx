import { useState, useEffect } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { getClassDashboard } from "../../api/dashboardsApi";
import { organizationApi } from "../../api/organizationApi";
import { useAuthStore } from "../../stores/authStore";
import { permissions } from "../../auth/permissions";
import {
  TeacherPageHeader,
  TeacherMetricCard,
  TeacherSkeleton,
  TeacherSafeErrorPanel,
} from "../../components/teacher/TeacherPrimitives";
import type { ClassDashboardDataDto } from "../../types/dashboards";

export function TeacherClassDashboardView() {
  const { classId: routeClassId } = useParams<{ classId?: string }>();
  const navigate = useNavigate();
  const hasPermission = useAuthStore((state) => state.hasPermission);
  const canCreateAssignment = hasPermission(permissions.assignmentsCreate);

  // Load teacher assigned classes
  const { data: classesData, isLoading: classesLoading } = useQuery({
    queryKey: ["teacherClassesListForDashboard"],
    queryFn: () => organizationApi.listClasses({ page: 1, pageSize: 50, status: "Active" }),
  });

  const [selectedClassId, setSelectedClassId] = useState<string>(routeClassId || "");

  useEffect(() => {
    if (routeClassId) {
      setSelectedClassId(routeClassId);
    } else if (classesData?.data && classesData.data.length > 0 && !selectedClassId) {
      const firstId = classesData.data[0].classId;
      setSelectedClassId(firstId);
      navigate(`/giao-vien/lop-hoc/${firstId}`, { replace: true });
    }
  }, [routeClassId, classesData, selectedClassId, navigate]);

  const handleSelectClass = (newId: string) => {
    setSelectedClassId(newId);
    navigate(`/giao-vien/lop-hoc/${newId}`);
  };

  // Load class dashboard data
  const {
    data: dashboard,
    isLoading: dashboardLoading,
    isError,
    error,
    refetch,
  } = useQuery<ClassDashboardDataDto>({
    queryKey: ["teacherClassDashboard", selectedClassId],
    queryFn: () => getClassDashboard(selectedClassId),
    enabled: !!selectedClassId,
  });

  return (
    <div className="space-y-6">
      {/* Header with class picker */}
      <TeacherPageHeader
        eyebrow="GIẢNG DẠY & GIÁM SÁT"
        title="Tổng Quan Lớp Học & Phân Tích Năng Lực"
        description="Theo dõi sát sao tiến độ học sinh, phát hiện chuyên đề yếu và giao bài tập bổ trợ thích ứng cho nhóm hổng kiến thức."
        breadcrumbs={[
          { label: "Giáo viên", href: "/giao-vien/lop-hoc" },
          { label: "Lớp học phụ trách" },
        ]}
        actions={
          <div className="flex items-center gap-3">
            {classesLoading ? (
              <span className="text-xs text-[var(--th-text-muted)]">Đang tải lớp...</span>
            ) : (
              <select
                value={selectedClassId}
                onChange={(e) => handleSelectClass(e.target.value)}
                className="th-select text-xs min-w-[200px]"
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
              className="th-secondary-button text-xs py-1.5 px-3"
            >
              Làm mới
            </button>
          </div>
        }
      />

      {/* Loading / Error States */}
      {dashboardLoading && (
        <div className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <TeacherSkeleton className="h-28 rounded-2xl" />
            <TeacherSkeleton className="h-28 rounded-2xl" />
            <TeacherSkeleton className="h-28 rounded-2xl" />
            <TeacherSkeleton className="h-28 rounded-2xl" />
          </div>
          <TeacherSkeleton className="h-64 rounded-2xl" />
        </div>
      )}

      {isError && (
        <TeacherSafeErrorPanel
          error={error}
          fallback="Không thể tải dữ liệu Dashboard của lớp học."
          onRetry={() => refetch()}
        />
      )}

      {!dashboardLoading && !isError && dashboard && (
        <div className="space-y-6">
          {/* Overview Metric Cards */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <TeacherMetricCard
              label="Sĩ số lớp"
              value={dashboard.overview.studentCount}
              supportingText="Học sinh đang theo học"
              icon="👨‍🎓"
            />
            <TeacherMetricCard
              label="Tỷ lệ nộp bài tập"
              value={`${(dashboard.overview.assignmentCompletionRate * 100).toFixed(0)}%`}
              supportingText="Tính trên tổng số bài giao"
              icon="📑"
              trend={{
                label: dashboard.overview.assignmentCompletionRate >= 0.8 ? "Đạt chỉ tiêu" : "Cần nhắc nhở",
                tone: dashboard.overview.assignmentCompletionRate >= 0.8 ? "positive" : "negative",
              }}
            />
            <TeacherMetricCard
              label="Điểm dự báo trung bình"
              value={dashboard.overview.averagePredictedScore.toFixed(1)}
              supportingText="Thang điểm 10.0"
              icon="🎯"
              trend={{
                label: dashboard.overview.averagePredictedScore >= 7.0 ? "Khá giỏi" : "Cần củng cố",
                tone: dashboard.overview.averagePredictedScore >= 7.0 ? "positive" : "neutral",
              }}
            />
            <TeacherMetricCard
              label="Học sinh nguy cơ cao"
              value={dashboard.highRiskStudents.length}
              supportingText="Có rủi ro trượt mục tiêu"
              icon="⚠️"
              trend={{
                label: dashboard.highRiskStudents.length === 0 ? "An toàn" : "Cần hỗ trợ gấp",
                tone: dashboard.highRiskStudents.length === 0 ? "positive" : "negative",
              }}
            />
          </div>

          {/* GAP GROUPS (Adaptive Interventions) */}
          <section className="th-surface p-6 space-y-4">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 border-b border-[var(--th-border-subtle)] pb-4">
              <div>
                <h2 className="text-base font-bold text-[var(--th-text)] flex items-center gap-2">
                  <span>🎯</span>
                  <span>Nhóm Học Sinh Cần Can Thiệp Bổ Trợ (Gap Groups)</span>
                </h2>
                <p className="mt-1 text-xs text-[var(--th-text-secondary)]">
                  Hệ thống AI tự động gom nhóm học sinh có cùng lỗ hổng kiến thức để giáo viên giao bài tập thích ứng đúng trọng tâm.
                </p>
              </div>
              <span className="th-badge th-badge-info">
                {dashboard.gapGroups.length} nhóm phát hiện
              </span>
            </div>

            {dashboard.gapGroups.length === 0 ? (
              <div className="p-8 text-center text-xs text-[var(--th-text-muted)] border border-dashed border-[var(--th-border)] rounded-xl">
                Không có nhóm học sinh nào bị hổng kiến thức nghiêm trọng tại thời điểm này.
              </div>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {dashboard.gapGroups.map((group) => (
                  <div
                    key={group.topicNodeId}
                    className="rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface-subtle)] p-5 flex flex-col justify-between hover:border-teal-500/40 transition-colors"
                  >
                    <div>
                      <div className="flex justify-between items-start gap-3">
                        <h3 className="font-semibold text-sm text-[var(--th-text)]">
                          {group.topicName}
                        </h3>
                        <span className="th-badge th-badge-warning shrink-0">
                          {group.studentIds.length} học sinh
                        </span>
                      </div>
                      <p className="mt-2 text-xs text-[var(--th-text-secondary)] font-medium">
                        💡 Gợi ý bổ trợ: {group.suggestedAction}
                      </p>
                      <p className="mt-2 text-[11px] text-[var(--th-text-muted)]">
                        Ngưỡng kích hoạt: &lt; {group.threshold}% độ thuần thục
                      </p>
                    </div>

                    <div className="mt-4 pt-3 border-t border-[var(--th-border-subtle)] flex justify-end">
                      {canCreateAssignment ? (
                        <Link
                          to={`/giao-vien/bai-tap/tao-moi?classId=${selectedClassId}&topicNodeId=${group.topicNodeId}&studentIds=${encodeURIComponent(group.studentIds.join(","))}`}
                          className="th-primary-button text-xs py-1.5 px-3"
                        >
                          Giao bài tập bổ trợ cho nhóm →
                        </Link>
                      ) : (
                        <span className="text-xs text-[var(--th-text-muted)]">Cần quyền assignments.create</span>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </section>

          {/* High-Risk Students & Weak Topics Grid */}
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* High-risk students (2 cols) */}
            <div className="lg:col-span-2 th-surface p-6 space-y-4">
              <div className="flex items-center justify-between border-b border-[var(--th-border-subtle)] pb-3">
                <div>
                  <h2 className="text-base font-bold text-[var(--th-text)]">
                    Danh Sách Học Sinh Có Nguy Cơ Tụt Hậu
                  </h2>
                  <p className="text-xs text-[var(--th-text-secondary)]">
                    Dựa trên phân tích nguy cơ (Risk Score) và khoảng cách tới mục tiêu kỳ thi.
                  </p>
                </div>
                <span className="text-xs font-semibold text-[var(--th-text-muted)]">
                  {dashboard.highRiskStudents.length} học sinh
                </span>
              </div>

              {dashboard.highRiskStudents.length === 0 ? (
                <p className="text-xs text-[var(--th-text-muted)] py-6 text-center">
                  Không có học sinh nào nằm trong diện nguy cơ cao.
                </p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-xs border-collapse">
                    <thead>
                      <tr className="border-b border-[var(--th-border-subtle)] text-[var(--th-text-muted)]">
                        <th className="py-2.5 font-semibold">Học sinh</th>
                        <th className="py-2.5 font-semibold">Mức độ rủi ro</th>
                        <th className="py-2.5 font-semibold">Mục tiêu & Thời gian</th>
                        <th className="py-2.5 font-semibold text-right">Thao tác</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-[var(--th-border-subtle)]">
                      {dashboard.highRiskStudents.map((s) => (
                        <tr key={s.studentId} className="hover:bg-[var(--th-surface-subtle)] transition-colors">
                          <td className="py-3 font-semibold text-[var(--th-text)]">{s.fullName}</td>
                          <td className="py-3">
                            <span className="th-badge th-badge-danger">
                              Risk {(s.riskScore * 100).toFixed(0)}%
                            </span>
                          </td>
                          <td className="py-3 text-[var(--th-text-secondary)]">
                            Mục tiêu: {s.targetScore.toFixed(1)} (còn {s.remainingDays} ngày)
                          </td>
                          <td className="py-3 text-right">
                            <Link
                              to={`/giao-vien/hoc-sinh/${s.studentId}/twin?subjectId=${dashboard.class.subjectId}`}
                              className="th-secondary-button text-xs py-1 px-2.5"
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
            <div className="th-surface p-6 space-y-4">
              <div>
                <h2 className="text-base font-bold text-[var(--th-text)]">
                  Chuyên Đề Yếu Cần Củng Cố
                </h2>
                <p className="text-xs text-[var(--th-text-secondary)]">
                  Xếp hạng theo độ thuần thục trung bình của cả lớp từ thấp lên cao.
                </p>
              </div>

              {dashboard.weakTopics.length === 0 ? (
                <p className="text-xs text-[var(--th-text-muted)] py-6 text-center">
                  Chưa ghi nhận chuyên đề nào dưới ngưỡng.
                </p>
              ) : (
                <div className="space-y-4 pt-2">
                  {dashboard.weakTopics.map((topic) => (
                    <div key={topic.topicNodeId} className="space-y-1">
                      <div className="flex items-center justify-between text-xs">
                        <span className="font-semibold text-[var(--th-text)] truncate max-w-[160px]" title={topic.topicName}>
                          {topic.topicName}
                        </span>
                        <span className="font-bold text-[var(--th-text)]">
                          {topic.averageMastery.toFixed(0)}%
                        </span>
                      </div>
                      <div className="h-2 rounded-full bg-[var(--th-surface-muted)] overflow-hidden">
                        <div
                          className={`h-full rounded-full ${
                            topic.averageMastery < 50
                              ? "bg-rose-500"
                              : topic.averageMastery < 70
                              ? "bg-amber-500"
                              : "bg-teal-500"
                          }`}
                          style={{ width: `${Math.min(100, Math.max(0, topic.averageMastery))}%` }}
                        />
                      </div>
                      <p className="text-[11px] text-[var(--th-text-muted)]">
                        {topic.affectedStudentCount} học sinh chưa đạt yêu cầu
                      </p>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
