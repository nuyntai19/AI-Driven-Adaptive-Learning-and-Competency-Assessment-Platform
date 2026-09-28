import { useState, useMemo } from "react";
import { useParams, Link } from "react-router-dom";
import { useAssignment } from "../../features/assignments/useAssignment";
import { useAssignmentProgress } from "../../features/assignments/useAssignmentProgress";
import { useCloseAssignment } from "../../features/assignments/useCloseAssignment";
import type { ProgressStatus } from "../../types/assignments";
import { useAuthStore } from "../../stores/authStore";
import { permissions } from "../../auth/permissions";
import { extractProblemDetails, mapSafeOperationalError } from "../../utils/problemDetails";
import {
  TeacherPageHeader,
  TeacherStatusBadge,
  TeacherSkeleton,
  TeacherMetricCard,
  TeacherSafeErrorPanel,
} from "../../components/teacher/TeacherPrimitives";
import { TeacherConfirmDialog } from "../../components/teacher/TeacherOverlays";
import { TeacherAssignmentQuickViewModal } from "../../components/teacher/TeacherAssignmentQuickViewModal";

export function TeacherAssignmentProgressView() {
  const { id } = useParams<{ id: string }>();

  const hasPermission = useAuthStore((state) => state.hasPermission);
  const canClose = hasPermission(permissions.assignmentsClose);

  const [searchFilter, setSearchFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState<ProgressStatus | "">("");

  const [isCloseDialogOpen, setIsCloseDialogOpen] = useState(false);
  const [isQuickViewOpen, setIsQuickViewOpen] = useState(false);
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
    if (!dateStr) return "Không giới hạn";
    try {
      const d = new Date(dateStr);
      return `${d.toLocaleDateString("vi-VN")} ${d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}`;
    } catch {
      return dateStr;
    }
  };

  const formatProgressStatus = (status: ProgressStatus) => {
    switch (status) {
      case "Completed":
        return <TeacherStatusBadge status="Completed" label="Đã nộp bài" tone="success" />;
      case "InProgress":
        return <TeacherStatusBadge status="InProgress" label="Đang làm bài" tone="info" />;
      case "Overdue":
        return <TeacherStatusBadge status="Overdue" label="Quá hạn" tone="danger" />;
      case "NotStarted":
      default:
        return <TeacherStatusBadge status="NotStarted" label="Chưa bắt đầu" tone="neutral" />;
    }
  };

  const isLoading = assignmentQuery.isLoading || progressQuery.isLoading;

  return (
    <div className="space-y-6">
      <TeacherPageHeader
        eyebrow="TIẾN ĐỘ BÀI TẬP"
        title={assignment ? `Tiến Độ: ${assignment.title}` : "Tiến Độ Làm Bài Của Lớp"}
        description={
          assignment
            ? `Hạn nộp: ${formatDueDate(assignment.dueAt)} · Tổng số ${assignment.questionCount} câu hỏi.`
            : "Theo dõi tình trạng làm bài và điểm số sơ bộ của từng học sinh."
        }
        breadcrumbs={[
          { label: "Giáo viên", href: "/giao-vien/lop-hoc" },
          { label: "Quản lý bài tập", href: "/giao-vien/bai-tap" },
          { label: "Tiến độ" },
        ]}
        actions={
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setIsQuickViewOpen(true)}
              className="th-secondary-button text-xs py-2 px-3.5 flex items-center gap-1.5"
              title="Xem lại câu hỏi và danh sách học sinh đã phân công"
            >
              <span>📚 Xem câu hỏi ({assignment?.questionCount || 0})</span>
            </button>
            {assignment?.status === "Published" && canClose && (
              <button
                type="button"
                onClick={() => setIsCloseDialogOpen(true)}
                className="th-danger-button text-xs py-2 px-4"
              >
                Đóng bài tập
              </button>
            )}
            {assignment?.status && <TeacherStatusBadge status={assignment.status} />}
          </div>
        }
      />

      {actionError && (
        <TeacherSafeErrorPanel
          error={actionError.message}
          title="Không thể thực hiện thao tác"
        />
      )}

      {/* KPI Cards */}
      {isLoading ? (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <TeacherSkeleton className="h-28 rounded-2xl" />
          <TeacherSkeleton className="h-28 rounded-2xl" />
          <TeacherSkeleton className="h-28 rounded-2xl" />
          <TeacherSkeleton className="h-28 rounded-2xl" />
        </div>
      ) : (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <TeacherMetricCard
            label="Đã hoàn thành"
            value={`${completedCount} / ${totalStudents}`}
            supportingText={totalStudents > 0 ? `${((completedCount / totalStudents) * 100).toFixed(0)}% nộp bài` : "0%"}
            icon="✅"
            trend={{ label: "Đã nộp", tone: "positive" }}
          />
          <TeacherMetricCard
            label="Đang làm bài"
            value={inProgressCount}
            supportingText="Đang trong tiến trình"
            icon="⏳"
          />
          <TeacherMetricCard
            label="Chưa bắt đầu"
            value={notStartedCount}
            supportingText="Chưa mở bài tập"
            icon="💤"
          />
          <TeacherMetricCard
            label="Quá hạn nộp"
            value={overdueCount}
            supportingText="Cần nhắc nhở"
            icon="⚠️"
            trend={{ label: overdueCount > 0 ? "Quá hạn" : "Tốt", tone: overdueCount > 0 ? "negative" : "positive" }}
          />
        </div>
      )}

      {/* Filter Bar */}
      <div className="th-surface p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <div className="flex flex-1 items-center gap-3">
          <input
            type="text"
            placeholder="Tìm tên học sinh hoặc ID..."
            value={searchFilter}
            onChange={(e) => setSearchFilter(e.target.value)}
            className="th-input w-full max-w-sm text-xs py-1.5"
          />
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as ProgressStatus | "")}
            className="th-select text-xs py-1.5"
          >
            <option value="">Tất cả trạng thái</option>
            <option value="Completed">Đã hoàn thành</option>
            <option value="InProgress">Đang làm bài</option>
            <option value="NotStarted">Chưa bắt đầu</option>
            <option value="Overdue">Quá hạn</option>
          </select>
        </div>

        <span className="text-xs text-[var(--th-text-muted)] shrink-0">
          Hiển thị <strong>{filteredList.length}</strong> / <strong>{totalStudents}</strong> học sinh
        </span>
      </div>

      {/* Progress Table */}
      <div className="th-surface overflow-x-auto">
        <table className="w-full text-left text-xs border-collapse">
          <thead>
            <tr className="border-b border-[var(--th-border-subtle)] bg-[var(--th-surface-subtle)] text-[var(--th-text-muted)] font-semibold uppercase tracking-wider">
              <th className="px-5 py-3.5">Học sinh</th>
              <th className="px-5 py-3.5">Trạng thái</th>
              <th className="px-5 py-3.5">Tiến độ câu</th>
              <th className="px-5 py-3.5">Tỷ lệ hoàn thành</th>
              <th className="px-5 py-3.5 text-right">Hồ sơ năng lực</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[var(--th-border-subtle)]">
            {filteredList.length === 0 ? (
              <tr>
                <td colSpan={5} className="p-8 text-center text-xs text-[var(--th-text-muted)]">
                  Không tìm thấy học sinh nào phù hợp.
                </td>
              </tr>
            ) : (
              filteredList.map((item) => {
                const completionPct = item.totalQuestionCount > 0
                  ? Math.round((item.completedQuestionCount / item.totalQuestionCount) * 100)
                  : 0;

                return (
                  <tr key={item.studentId} className="hover:bg-[var(--th-surface-subtle)] transition-colors">
                    <td className="px-5 py-4 font-semibold text-[var(--th-text)]">
                      {item.fullName}
                    </td>
                    <td className="px-5 py-4">
                      {formatProgressStatus(item.status)}
                    </td>
                    <td className="px-5 py-4 text-[var(--th-text-secondary)]">
                      {item.completedQuestionCount} / {item.totalQuestionCount} câu
                    </td>
                    <td className="px-5 py-4 font-mono font-bold text-[var(--th-teal)]">
                      {completionPct}%
                    </td>
                    <td className="px-5 py-4 text-right">
                      <Link
                        to={`/giao-vien/hoc-sinh/${item.studentId}/twin`}
                        className="th-secondary-button text-xs py-1 px-2.5"
                      >
                        Xem Twin →
                      </Link>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Quick View Modal to review questions and assigned students */}
      <TeacherAssignmentQuickViewModal
        assignmentId={id || null}
        isOpen={isQuickViewOpen}
        onClose={() => setIsQuickViewOpen(false)}
        initialTab="questions"
      />

      {/* Confirm Close Dialog */}
      <TeacherConfirmDialog
        isOpen={isCloseDialogOpen}
        title="Đóng bài tập"
        description="Khi đóng bài tập, học sinh sẽ không thể gửi bài làm mới. Trạng thái bài tập sẽ chuyển sang Đã đóng (Closed)."
        confirmLabel="Đóng bài tập"
        tone="danger"
        isConfirming={closeMutation.isPending}
        onConfirm={handleConfirmClose}
        onClose={() => setIsCloseDialogOpen(false)}
      />
    </div>
  );
}
