import { useState, useMemo } from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import { useAssignment } from "../features/assignments/useAssignment";
import { useAssignmentProgress } from "../features/assignments/useAssignmentProgress";
import { useCloseAssignment } from "../features/assignments/useCloseAssignment";
import type { ProgressStatus } from "../types/assignments";
import { useAuthStore } from "../stores/authStore";
import { permissions } from "../auth/permissions";
import {
  mapSafeOperationalError,
  extractProblemDetails,
} from "../utils/problemDetails";
import { CenterManagerThemeScope } from "../components/centerManager/CenterManagerThemeScope";
import {
  PageHeader,
  StatusBadge,
  Skeleton,
  MetricCard,
  SafeErrorPanel,
} from "../components/centerManager/CenterManagerPrimitives";
import { ConfirmDialog } from "../components/centerManager/CenterManagerOverlays";

// =============================================================================
// 1. CENTER MANAGER DARK SAAS VIEW (GATE 6B)
// =============================================================================

function CenterManagerAssignmentProgressView() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const hasPermission = useAuthStore((state) => state.hasPermission);
  const canClose = hasPermission(permissions.assignmentsClose);

  const [searchFilter, setSearchFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState<ProgressStatus | "">("");

  // Dialog state for closing assignment
  const [isCloseDialogOpen, setIsCloseDialogOpen] = useState(false);
  const [actionError, setActionError] = useState<{ message: string; traceId?: string | null } | null>(null);

  const assignmentQuery = useAssignment(id);
  const progressQuery = useAssignmentProgress(id);
  const closeMutation = useCloseAssignment();

  const assignment = assignmentQuery.data?.data;
  const progressList = useMemo(() => progressQuery.data?.data || [], [progressQuery.data?.data]);

  // Filtered students
  const filteredList = useMemo(() => {
    return progressList.filter((item) => {
      const matchesSearch =
        !searchFilter.trim() ||
        item.fullName.toLowerCase().includes(searchFilter.trim().toLowerCase()) ||
        item.studentId.toLowerCase().includes(searchFilter.trim().toLowerCase());

      const matchesStatus = !statusFilter || item.status === statusFilter;

      return matchesSearch && matchesStatus;
    });
  }, [progressList, searchFilter, statusFilter]);

  // Accurate counts derived directly from AssignmentProgressItemDto (NO FAKE METRICS)
  const totalStudents = progressList.length;
  const completedCount = progressList.filter((p) => p.status === "Completed").length;
  const inProgressCount = progressList.filter((p) => p.status === "InProgress").length;
  const notStartedCount = progressList.filter((p) => p.status === "NotStarted").length;
  const overdueCount = progressList.filter((p) => p.status === "Overdue").length;

  const handleConfirmClose = () => {
    if (!id || !assignment) return;
    setActionError(null);

    closeMutation.mutate(
      { id, request: { rowVersion: assignment.rowVersion } },
      {
        onSuccess: () => {
          setIsCloseDialogOpen(false);
          assignmentQuery.refetch();
        },
        onError: (err: any) => {
          const details = extractProblemDetails(err);
          setIsCloseDialogOpen(false);
          setActionError({
            message: mapSafeOperationalError(err, "Không thể đóng bài tập."),
            traceId: details.traceId,
          });
        },
      }
    );
  };

  const formatDueDate = (dateStr: string | null) => {
    if (!dateStr) return "Không giới hạn hạn nộp";
    try {
      const d = new Date(dateStr);
      return `${d.toLocaleDateString("vi-VN")} ${d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}`;
    } catch {
      return dateStr;
    }
  };

  if (assignmentQuery.isLoading || progressQuery.isLoading) {
    return (
      <CenterManagerThemeScope data-actor="center-manager">
        <div className="space-y-6 p-6 lg:p-8">
          <Skeleton className="h-10 w-64 rounded-lg" />
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <Skeleton key={i} className="h-28 w-full rounded-2xl" />
            ))}
          </div>
          <Skeleton className="h-96 w-full rounded-2xl" />
        </div>
      </CenterManagerThemeScope>
    );
  }

  if (assignmentQuery.isError || !assignment) {
    return (
      <CenterManagerThemeScope data-actor="center-manager">
        <div className="p-6 lg:p-8">
          <SafeErrorPanel
            error={assignmentQuery.error}
            fallback="Không tìm thấy bài tập hoặc bạn không có quyền truy cập."
            onRetry={() => assignmentQuery.refetch()}
          />
        </div>
      </CenterManagerThemeScope>
    );
  }

  return (
    <CenterManagerThemeScope data-actor="center-manager">
      <div className="space-y-6 p-6 lg:p-8">
        <PageHeader
          eyebrow="TIẾN ĐỘ BÀI TẬP"
          title={`Tiến độ: ${assignment.title}`}
          description={`Theo dõi mức độ hoàn thành câu hỏi của từng học sinh trong lớp. Hạn chót: ${formatDueDate(assignment.dueAt)}.`}
          breadcrumbs={[
            { label: "CenterManager", href: "/quan-ly/tong-quan-trung-tam" },
            { label: "Quản lý bài tập", href: "/quan-ly/bai-tap" },
            { label: "Tiến độ bài tập" },
          ]}
          actions={
            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                onClick={() => navigate("/quan-ly/bai-tap")}
                className="cm-secondary-button text-xs"
              >
                ← Quay lại danh sách bài tập
              </button>

              {assignment.status === "Published" && canClose && (
                <button
                  type="button"
                  onClick={() => setIsCloseDialogOpen(true)}
                  className="px-3 py-1.5 rounded-lg text-xs font-semibold text-rose-300 bg-rose-500/10 border border-rose-500/30 hover:bg-rose-500/20 transition"
                >
                  Đóng bài tập
                </button>
              )}
            </div>
          }
        />

        {/* Action Error Alert */}
        {actionError && (
          <div
            role="alert"
            className="flex items-start justify-between rounded-xl border border-rose-500/30 bg-rose-500/10 p-4 text-xs font-medium text-rose-300"
          >
            <div>
              <p>{actionError.message}</p>
              {actionError.traceId && (
                <p className="mt-1 font-mono text-[11px] text-rose-200/80">Trace ID: {actionError.traceId}</p>
              )}
            </div>
            <button
              type="button"
              onClick={() => setActionError(null)}
              className="text-xs font-semibold text-rose-400 hover:text-rose-200 ml-4 shrink-0"
            >
              Đóng
            </button>
          </div>
        )}

        {/* Progress Error State */}
        {progressQuery.isError && (
          <SafeErrorPanel
            error={progressQuery.error}
            fallback="Không thể tải danh sách tiến độ học sinh."
            onRetry={() => progressQuery.refetch()}
          />
        )}

        {/* Strict Metric Cards derived purely from DTO (Zero Simulated Class Averages) */}
        <section aria-label="Tổng quan tiến độ" className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MetricCard
            label="Tổng học sinh nhận bài"
            value={totalStudents}
            supportingText={`Trạng thái bài tập: ${assignment.status}`}
          />
          <MetricCard
            label="Đã hoàn thành"
            value={completedCount}
            supportingText={totalStudents > 0 ? `${Math.round((completedCount / totalStudents) * 100)}% tổng số học sinh` : "0%"}
            trend={{ label: "Completed", tone: "positive" }}
          />
          <MetricCard
            label="Đang làm bài"
            value={inProgressCount}
            supportingText="Đang thực hiện các câu hỏi"
            trend={{ label: "InProgress", tone: "neutral" }}
          />
          <MetricCard
            label="Chưa bắt đầu / Quá hạn"
            value={notStartedCount + overdueCount}
            supportingText={`${notStartedCount} chưa làm, ${overdueCount} quá hạn`}
            trend={{ label: overdueCount > 0 ? "Cần nhắc nhở" : "NotStarted", tone: overdueCount > 0 ? "negative" : "neutral" }}
          />
        </section>

        {/* Main Table Card */}
        <div className="rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] p-5 lg:p-6 space-y-4 shadow-xl">
          {/* Table Filters */}
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-[var(--cm-border-subtle)] pb-4">
            <div className="w-full sm:w-72">
              <input
                type="text"
                placeholder="Tìm học sinh theo tên hoặc mã..."
                value={searchFilter}
                onChange={(e) => setSearchFilter(e.target.value)}
                className="cm-input w-full text-xs"
              />
            </div>

            <div className="flex items-center gap-2">
              <label htmlFor="progress-status-filter" className="text-xs text-[var(--cm-text-muted)] whitespace-nowrap">
                Lọc trạng thái:
              </label>
              <select
                id="progress-status-filter"
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value as ProgressStatus | "")}
                className="cm-select text-xs py-1.5"
              >
                <option value="">Tất cả trạng thái</option>
                <option value="NotStarted">Chưa bắt đầu (NotStarted)</option>
                <option value="InProgress">Đang làm bài (InProgress)</option>
                <option value="Completed">Đã hoàn thành (Completed)</option>
                <option value="Overdue">Quá hạn (Overdue)</option>
              </select>
            </div>
          </div>

          {/* Table Content */}
          {filteredList.length === 0 ? (
            <div className="p-8 text-center text-xs text-[var(--cm-text-muted)]">
              Không tìm thấy học sinh nào phù hợp với điều kiện tìm kiếm.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="bg-[var(--cm-surface-subtle)] border-b border-[var(--cm-border-subtle)] text-[var(--cm-text-muted)] uppercase tracking-wider">
                  <tr>
                    <th className="p-3">Học sinh</th>
                    <th className="p-3">Trạng thái</th>
                    <th className="p-3">Số câu hoàn thành</th>
                    <th className="p-3 min-w-[180px]">Tiến độ làm bài</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--cm-border-subtle)]">
                  {filteredList.map((item) => {
                    const percent =
                      item.totalQuestionCount > 0
                        ? Math.round((item.completedQuestionCount / item.totalQuestionCount) * 100)
                        : 0;

                    const statusTone =
                      item.status === "Completed"
                        ? "success"
                        : item.status === "InProgress"
                        ? "info"
                        : item.status === "Overdue"
                        ? "danger"
                        : "neutral";

                    return (
                      <tr key={item.studentId} className="hover:bg-white/5 transition">
                        <td className="p-3">
                          <p className="font-semibold text-[var(--cm-text)]">{item.fullName}</p>
                          <p className="font-mono text-[11px] text-[var(--cm-text-muted)]">{item.studentId}</p>
                        </td>
                        <td className="p-3">
                          <StatusBadge status={item.status} tone={statusTone} />
                        </td>
                        <td className="p-3 font-medium text-[var(--cm-text)]">
                          <span className="text-sm font-bold text-[var(--cm-cyan)]">{item.completedQuestionCount}</span>
                          <span className="text-[var(--cm-text-muted)]"> / {item.totalQuestionCount} câu</span>
                        </td>
                        <td className="p-3">
                          <div className="space-y-1">
                            <div className="flex justify-between text-[11px] text-[var(--cm-text-muted)]">
                              <span>{percent}%</span>
                              <span>{item.completedQuestionCount}/{item.totalQuestionCount}</span>
                            </div>
                            <div className="h-2 w-full rounded-full bg-[var(--cm-surface-subtle)] border border-[var(--cm-border-subtle)] overflow-hidden">
                              <div
                                className={`h-full transition-all duration-300 ${
                                  item.status === "Completed"
                                    ? "bg-emerald-400"
                                    : item.status === "InProgress"
                                    ? "bg-[var(--cm-cyan)]"
                                    : item.status === "Overdue"
                                    ? "bg-rose-400"
                                    : "bg-slate-500"
                                }`}
                                style={{ width: `${percent}%` }}
                              />
                            </div>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Close Dialog */}
        <ConfirmDialog
          isOpen={isCloseDialogOpen}
          title="Đóng bài tập"
          description="Khi đóng bài tập, học sinh sẽ không thể gửi thêm bài làm mới. Trạng thái bài tập sẽ chuyển sang Đã đóng (Closed). Bạn có chắc chắn muốn đóng bài tập này?"
          confirmLabel="Đóng bài tập"
          tone="danger"
          isConfirming={closeMutation.isPending}
          onConfirm={handleConfirmClose}
          onClose={() => setIsCloseDialogOpen(false)}
        />
      </div>
    </CenterManagerThemeScope>
  );
}

// =============================================================================
// 2. LEGACY ASSIGNMENT PROGRESS VIEW (FOR TEACHERS & OTHER ACTORS)
// =============================================================================

function LegacyAssignmentProgressPage() {
  const { id } = useParams<{ id: string }>();

  const assignmentQuery = useAssignment(id);
  const progressQuery = useAssignmentProgress(id);

  const assignment = assignmentQuery.data?.data;
  const progressList = progressQuery.data?.data || [];

  if (assignmentQuery.isLoading || progressQuery.isLoading) {
    return <div className="p-10 text-center">Đang tải dữ liệu...</div>;
  }

  if (assignmentQuery.isError || !assignment) {
    return <div className="p-10 text-center text-red-600">Không tìm thấy bài tập hoặc bạn không có quyền truy cập.</div>;
  }

  if (progressQuery.isError) {
    return (
      <div className="p-6 max-w-4xl mx-auto">
        <Link to="/quan-ly/bai-tap" className="mb-4 inline-block text-sm font-medium text-blue-600 hover:underline">
          &larr; Quay lại danh sách bài tập
        </Link>
        <div className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-amber-800">
          <h1 className="font-semibold">Chưa thể tải tiến độ giáo viên</h1>
          <p className="mt-1 text-sm">{mapSafeOperationalError(progressQuery.error, "Không thể tải tiến độ bài tập.")}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="p-6 max-w-6xl mx-auto">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <Link
            to="/quan-ly/bai-tap"
            className="text-sm font-medium text-blue-600 hover:underline mb-2 inline-block"
          >
            &larr; Quay lại danh sách bài tập
          </Link>
          <h1 className="text-3xl font-bold text-slate-800">
            Tiến độ: {assignment.title}
          </h1>
          <p className="text-slate-500 mt-1">Lớp học: {assignment.classId}</p>
        </div>
      </div>

      <div className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
        {progressList.length === 0 ? (
          <div className="p-10 text-center text-slate-500">
            Chưa có tiến độ học sinh cho bài tập này.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="bg-slate-50 border-b border-slate-200 text-slate-600">
                <tr>
                  <th className="px-6 py-4 font-semibold">Học sinh</th>
                  <th className="px-6 py-4 font-semibold">Trạng thái</th>
                  <th className="px-6 py-4 font-semibold">Số câu hoàn thành</th>
                  <th className="px-6 py-4 font-semibold">Tỷ lệ hoàn thành</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {progressList.map((p) => {
                  const percent =
                    p.totalQuestionCount > 0
                      ? Math.round((p.completedQuestionCount / p.totalQuestionCount) * 100)
                      : 0;

                  return (
                    <tr key={p.studentId} className="hover:bg-slate-50 transition">
                      <td className="px-6 py-4">
                        <div className="font-medium text-slate-800">{p.fullName}</div>
                        <div className="text-xs text-slate-500 mt-1">{p.studentId}</div>
                      </td>
                      <td className="px-6 py-4">
                        <span
                          className={`px-2 py-1 text-xs font-semibold rounded-full ${
                            p.status === "Completed"
                              ? "bg-green-100 text-green-700"
                              : p.status === "InProgress"
                              ? "bg-blue-100 text-blue-700"
                              : p.status === "Overdue"
                              ? "bg-red-100 text-red-700"
                              : "bg-slate-100 text-slate-700"
                          }`}
                        >
                          {p.status}
                        </span>
                      </td>
                      <td className="px-6 py-4 text-slate-600">
                        {p.completedQuestionCount} / {p.totalQuestionCount} câu
                      </td>
                      <td className="px-6 py-4">
                        <div className="w-full bg-slate-200 rounded-full h-2.5">
                          <div
                            className="bg-blue-600 h-2.5 rounded-full"
                            style={{ width: `${percent}%` }}
                          ></div>
                        </div>
                        <span className="text-xs text-slate-500 mt-1 inline-block">
                          {percent}%
                        </span>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}

// =============================================================================
// 3. MAIN ROUTE EXPORT WITH ACTOR ISOLATION
// =============================================================================

export const AssignmentProgressPage = () => {
  const accountType = useAuthStore((state) => state.user?.accountType);
  if (accountType === "CenterManager") {
    return <CenterManagerAssignmentProgressView />;
  }
  return <LegacyAssignmentProgressPage />;
};
