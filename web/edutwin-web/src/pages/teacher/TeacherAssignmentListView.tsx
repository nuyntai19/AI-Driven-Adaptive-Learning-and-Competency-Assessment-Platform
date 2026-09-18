import { useState, useMemo } from "react";
import { Link } from "react-router-dom";
import { useAssignments } from "../../features/assignments/useAssignments";
import { usePublishAssignment } from "../../features/assignments/usePublishAssignment";
import { useCloseAssignment } from "../../features/assignments/useCloseAssignment";
import { useAssignmentClasses } from "../../features/assignments/useAssignmentWizardOptions";
import type { AssignmentDto, AssignmentStatus } from "../../types/assignments";
import { useAuthStore } from "../../stores/authStore";
import { permissions } from "../../auth/permissions";
import { extractProblemDetails, mapSafeOperationalError } from "../../utils/problemDetails";
import {
  TeacherPageHeader,
  TeacherStatusBadge,
  TeacherSkeleton,
  TeacherSafeErrorPanel,
} from "../../components/teacher/TeacherPrimitives";
import { TeacherConfirmDialog } from "../../components/teacher/TeacherOverlays";

export function TeacherAssignmentListView() {
  const hasPermission = useAuthStore((state) => state.hasPermission);

  const canCreate = hasPermission(permissions.assignmentsCreate);
  const canUpdate = hasPermission(permissions.assignmentsUpdate);
  const canPublish = hasPermission(permissions.assignmentsPublish);
  const canClose = hasPermission(permissions.assignmentsClose);
  const canReadClasses = hasPermission(permissions.classesRead);

  // Filters state
  const [selectedClassId, setSelectedClassId] = useState<string>("");
  const [selectedStatus, setSelectedStatus] = useState<AssignmentStatus | "">("");
  const [page, setPage] = useState<number>(1);
  const pageSize = 12;

  // Operational feedback states
  const [actionError, setActionError] = useState<{ message: string; traceId?: string | null } | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  // Dialog states
  const [targetAssignment, setTargetAssignment] = useState<AssignmentDto | null>(null);
  const [dialogAction, setDialogAction] = useState<"publish" | "close" | null>(null);

  // Classes list for filter
  const { data: classesData, isLoading: isLoadingClasses } = useAssignmentClasses(
    { status: "Active", page: 1, pageSize: 50 },
    { enabled: canReadClasses }
  );

  const queryParams = useMemo(
    () => ({
      classId: selectedClassId || undefined,
      status: (selectedStatus as AssignmentStatus) || undefined,
      page,
      pageSize,
    }),
    [selectedClassId, selectedStatus, page, pageSize]
  );

  const { data: response, isLoading, isError, error, refetch } = useAssignments(queryParams);

  const publishMutation = usePublishAssignment();
  const closeMutation = useCloseAssignment();

  const handleOpenDialog = (assignment: AssignmentDto, action: "publish" | "close") => {
    setActionError(null);
    setActionSuccess(null);
    setTargetAssignment(assignment);
    setDialogAction(action);
  };

  const handleCloseDialog = () => {
    setTargetAssignment(null);
    setDialogAction(null);
  };

  const handleConfirmAction = () => {
    if (!targetAssignment || !dialogAction) return;

    if (dialogAction === "publish") {
      publishMutation.mutate(
        { id: targetAssignment.assignmentId, request: { rowVersion: targetAssignment.rowVersion } },
        {
          onSuccess: () => {
            setActionSuccess(`Đã xuất bản bài tập "${targetAssignment.title}" thành công. Học sinh đã có thể làm bài.`);
            handleCloseDialog();
          },
          onError: (err: any) => {
            const details = extractProblemDetails(err);
            setActionError({
              message: mapSafeOperationalError(err, "Không thể xuất bản bài tập."),
              traceId: details.traceId,
            });
            handleCloseDialog();
          },
        }
      );
    } else if (dialogAction === "close") {
      closeMutation.mutate(
        { id: targetAssignment.assignmentId, request: { rowVersion: targetAssignment.rowVersion } },
        {
          onSuccess: () => {
            setActionSuccess(`Đã đóng bài tập "${targetAssignment.title}".`);
            handleCloseDialog();
          },
          onError: (err: any) => {
            const details = extractProblemDetails(err);
            setActionError({
              message: mapSafeOperationalError(err, "Không thể đóng bài tập."),
              traceId: details.traceId,
            });
            handleCloseDialog();
          },
        }
      );
    }
  };

  const totalPages = response?.meta?.totalPages || 1;
  const totalItems = response?.meta?.totalItems || 0;

  return (
    <div className="space-y-6">
      <TeacherPageHeader
        eyebrow="GIAO BÀI & ĐÁNH GIÁ"
        title="Quản Lý Bài Tập Lớp Học"
        description="Giao bài tập từ ngân hàng câu hỏi chuẩn hóa, theo dõi tiến độ nộp bài và quản lý trạng thái xuất bản."
        breadcrumbs={[
          { label: "Giáo viên", href: "/giao-vien/lop-hoc" },
          { label: "Quản lý bài tập" },
        ]}
        actions={
          canCreate ? (
            <Link
              to="/giao-vien/bai-tap/tao-moi"
              className="th-primary-button text-xs py-2 px-4 shadow-md shadow-teal-500/20"
            >
              + Giao bài tập mới
            </Link>
          ) : null
        }
      />

      {/* Operational Feedback */}
      {actionSuccess && (
        <div className="rounded-xl border border-emerald-500/30 bg-emerald-500/10 p-4 text-xs font-semibold text-emerald-300 flex items-center justify-between">
          <span>{actionSuccess}</span>
          <button onClick={() => setActionSuccess(null)} className="text-emerald-400 hover:text-emerald-200">✕</button>
        </div>
      )}
      {actionError && (
        <TeacherSafeErrorPanel
          error={actionError.message}
          title="Không thể thực hiện thao tác"
          onRetry={() => refetch()}
        />
      )}

      {/* Filters */}
      <div className="rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface)] p-4 shadow-md">
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4">
          <div>
            <label className="block text-[10px] font-semibold uppercase text-[var(--th-text-muted)] mb-1">Lớp học</label>
            <select
              value={selectedClassId}
              onChange={(e) => {
                setSelectedClassId(e.target.value);
                setPage(1);
              }}
              disabled={isLoadingClasses}
              className="th-select w-full text-xs py-1.5"
            >
              <option value="">Tất cả các lớp phụ trách</option>
              {classesData?.data?.map((c) => (
                <option key={c.classId} value={c.classId}>
                  {c.className} ({c.academicYear})
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="block text-[10px] font-semibold uppercase text-[var(--th-text-muted)] mb-1">Trạng thái</label>
            <select
              value={selectedStatus}
              onChange={(e) => {
                setSelectedStatus(e.target.value as AssignmentStatus | "");
                setPage(1);
              }}
              className="th-select w-full text-xs py-1.5"
            >
              <option value="">Tất cả trạng thái</option>
              <option value="Draft">Bản nháp (Draft)</option>
              <option value="Published">Đã xuất bản (Published)</option>
              <option value="Closed">Đã đóng (Closed)</option>
            </select>
          </div>
        </div>
      </div>

      {/* Assignments Grid */}
      {isLoading && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {Array.from({ length: 6 }).map((_, i) => (
            <TeacherSkeleton key={i} className="h-44 rounded-2xl" />
          ))}
        </div>
      )}

      {isError && (
        <TeacherSafeErrorPanel
          error={error}
          fallback="Không thể tải danh sách bài tập."
          onRetry={() => refetch()}
        />
      )}

      {!isLoading && !isError && (!response?.data || response.data.length === 0) && (
        <div className="p-12 text-center text-xs text-[var(--th-text-muted)] th-surface border border-dashed border-[var(--th-border)]">
          <p className="text-base font-semibold text-[var(--th-text)] mb-1">Chưa có bài tập nào</p>
          <p>Bấm nút "Giao bài tập mới" ở góc trên để tạo đợt làm bài đầu tiên cho lớp.</p>
        </div>
      )}

      {!isLoading && !isError && response?.data && response.data.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {response.data.map((assignment) => (
            <div
              key={assignment.assignmentId}
              className="th-surface p-5 flex flex-col justify-between hover:border-teal-500/40 transition-all shadow-md"
            >
              <div className="space-y-3">
                <div className="flex items-start justify-between gap-2">
                  <h3 className="font-bold text-sm text-[var(--th-text)] line-clamp-1" title={assignment.title}>
                    {assignment.title}
                  </h3>
                  <TeacherStatusBadge status={assignment.status} />
                </div>

                <p className="text-xs text-[var(--th-text-secondary)] line-clamp-2">
                  {assignment.instructions || "Không có hướng dẫn bổ sung."}
                </p>

                <div className="flex items-center justify-between text-xs text-[var(--th-text-muted)] pt-2 border-t border-[var(--th-border-subtle)]">
                  <span>📚 <strong>{assignment.questionCount}</strong> câu hỏi</span>
                  <span>👥 <strong>{assignment.targetStudentCount}</strong> học sinh</span>
                </div>
              </div>

              {/* Action Buttons */}
              <div className="mt-4 pt-3 border-t border-[var(--th-border-subtle)] flex items-center justify-between gap-2">
                <div className="flex gap-2">
                  {canPublish && assignment.status === "Draft" && (
                    <button
                      type="button"
                      onClick={() => handleOpenDialog(assignment, "publish")}
                      className="th-primary-button text-xs py-1 px-2.5"
                    >
                      Xuất bản
                    </button>
                  )}
                  {canClose && assignment.status === "Published" && (
                    <button
                      type="button"
                      onClick={() => handleOpenDialog(assignment, "close")}
                      className="th-secondary-button text-xs py-1 px-2.5 border-rose-500/40 text-rose-300 hover:bg-rose-500/20"
                    >
                      Đóng bài
                    </button>
                  )}
                </div>

                <div className="flex items-center gap-2">
                  <Link
                    to={`/giao-vien/bai-tap/${assignment.assignmentId}/tien-do`}
                    className="th-secondary-button text-xs py-1 px-2.5"
                  >
                    Tiến độ →
                  </Link>
                  {canUpdate && assignment.status === "Draft" && (
                    <Link
                      to={`/giao-vien/bai-tap/${assignment.assignmentId}`}
                      className="th-secondary-button text-xs py-1 px-2.5"
                    >
                      Sửa
                    </Link>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Pagination */}
      {!isLoading && !isError && totalItems > 0 && (
        <div className="flex items-center justify-between rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface)] p-3 text-xs text-[var(--th-text-secondary)]">
          <span>
            Trang <strong>{page}</strong> / <strong>{totalPages}</strong> ({totalItems} bài tập)
          </span>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
              className="th-secondary-button text-xs py-1 px-3"
            >
              ← Trước
            </button>
            <button
              type="button"
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages}
              className="th-secondary-button text-xs py-1 px-3"
            >
              Sau →
            </button>
          </div>
        </div>
      )}

      {/* Confirm Action Dialog */}
      <TeacherConfirmDialog
        isOpen={dialogAction !== null}
        title={dialogAction === "publish" ? "Xuất bản bài tập" : "Đóng bài tập"}
        description={
          dialogAction === "publish"
            ? `Xuất bản bài tập "${targetAssignment?.title}"? Hệ thống sẽ chốt danh sách học sinh tham gia và học sinh có thể bắt đầu làm bài.`
            : `Đóng bài tập "${targetAssignment?.title}"? Học sinh sẽ không thể nộp bài làm mới sau khi đóng.`
        }
        confirmLabel={dialogAction === "publish" ? "Xuất bản ngay" : "Đóng bài tập"}
        tone={dialogAction === "close" ? "danger" : "default"}
        isConfirming={publishMutation.isPending || closeMutation.isPending}
        onConfirm={handleConfirmAction}
        onClose={handleCloseDialog}
      />
    </div>
  );
}
