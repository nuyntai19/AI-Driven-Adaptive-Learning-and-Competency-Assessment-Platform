import { useState, useMemo } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAssignments } from "../features/assignments/useAssignments";
import { usePublishAssignment } from "../features/assignments/usePublishAssignment";
import { useCloseAssignment } from "../features/assignments/useCloseAssignment";
import { useAssignmentClasses } from "../features/assignments/useAssignmentWizardOptions";
import type { AssignmentDto, AssignmentStatus } from "../types/assignments";
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
  SafeErrorPanel,
} from "../components/centerManager/CenterManagerPrimitives";
import { ConfirmDialog } from "../components/centerManager/CenterManagerOverlays";

// =============================================================================
// 1. CENTER MANAGER DARK SAAS VIEW (GATE 6B)
// =============================================================================

function CenterManagerAssignmentListView() {
  const navigate = useNavigate();
  const hasPermission = useAuthStore((state) => state.hasPermission);

  const canCreate = hasPermission(permissions.assignmentsCreate);
  const canUpdate = hasPermission(permissions.assignmentsUpdate);
  const canPublish = hasPermission(permissions.assignmentsPublish);
  const canClose = hasPermission(permissions.assignmentsClose);
  const canReadClasses = hasPermission(permissions.classesRead);

  // Filters state (canonical: classId, status, page, pageSize - no search param to server)
  const [selectedClassId, setSelectedClassId] = useState<string>("");
  const [selectedStatus, setSelectedStatus] = useState<AssignmentStatus | "">("");
  const [page, setPage] = useState<number>(1);
  const pageSize = 12;

  // Operational feedback states
  const [actionError, setActionError] = useState<{ message: string; traceId?: string | null } | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  // Dialog states for state machine transitions
  const [targetAssignment, setTargetAssignment] = useState<AssignmentDto | null>(null);
  const [dialogAction, setDialogAction] = useState<"publish" | "close" | null>(null);

  // Canonical classes query for filter selector
  const { data: classesData, isLoading: isLoadingClasses } = useAssignmentClasses({
    status: "Active",
    page: 1,
    pageSize: 100,
  });

  const classMap = useMemo(() => {
    const map = new Map<string, string>();
    if (classesData?.data) {
      for (const c of classesData.data) {
        map.set(c.classId, `${c.className} (${c.academicYear})`);
      }
    }
    return map;
  }, [classesData?.data]);

  // Assignments query with server-side pagination & canonical filters
  const queryParams = useMemo(
    () => ({
      classId: selectedClassId || undefined,
      status: (selectedStatus as AssignmentStatus) || undefined,
      page,
      pageSize,
    }),
    [selectedClassId, selectedStatus, page, pageSize]
  );

  const {
    data: response,
    isLoading,
    isError,
    error,
    refetch,
  } = useAssignments(queryParams);

  const publishMutation = usePublishAssignment();
  const closeMutation = useCloseAssignment();

  const handleFilterChange = (setter: (val: any) => void, val: any) => {
    setter(val);
    setPage(1);
  };

  const pagedMeta = response?.meta as { page?: number; pageSize?: number; totalItems?: number; totalPages?: number; traceId?: string } | undefined;
  const totalItems = pagedMeta?.totalItems ?? response?.data?.length ?? 0;
  const totalPages = pagedMeta?.totalPages ?? Math.max(1, Math.ceil(totalItems / pageSize));

  // Dialog triggers
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
            setActionSuccess(`Đã xuất bản bài tập "${targetAssignment.title}" thành công.`);
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
            setActionSuccess(`Đã đóng bài tập "${targetAssignment.title}" thành công.`);
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

  const formatDueDate = (dateStr: string | null) => {
    if (!dateStr) return "Không giới hạn hạn nộp";
    try {
      const d = new Date(dateStr);
      return `Hạn nộp: ${d.toLocaleDateString("vi-VN")} ${d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}`;
    } catch {
      return dateStr;
    }
  };

  return (
    <CenterManagerThemeScope data-actor="center-manager">
      <div className="space-y-6 p-6 lg:p-8">
        <PageHeader
          eyebrow="NỘI DUNG HỌC THUẬT"
          title="Quản lý Bài tập"
          description="Điều phối giao bài tập, theo dõi tiến độ nộp bài và quản lý vòng đời bài tập cho từng lớp học trực thuộc trung tâm."
          breadcrumbs={[
            { label: "CenterManager", href: "/quan-ly/tong-quan-trung-tam" },
            { label: "Quản lý bài tập" },
          ]}
          actions={
            canCreate ? (
              <button
                type="button"
                id="btn-create-assignment"
                onClick={() => navigate("/quan-ly/bai-tap/tao-moi")}
                className="cm-primary-button flex items-center gap-2"
              >
                <span>+ Tạo bài tập mới</span>
              </button>
            ) : null
          }
        />

        {/* Action Alerts with Trace ID support */}
        {actionSuccess && (
          <div
            role="status"
            className="flex items-center justify-between rounded-xl border border-emerald-500/30 bg-emerald-500/10 p-4 text-xs font-medium text-emerald-300"
          >
            <span>{actionSuccess}</span>
            <button
              type="button"
              onClick={() => setActionSuccess(null)}
              className="text-xs font-semibold text-emerald-400 hover:text-emerald-200"
            >
              Đóng
            </button>
          </div>
        )}

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

        {/* Filter Bar */}
        <section
          aria-label="Bộ lọc bài tập"
          className="rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] p-5 shadow-xl"
        >
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {/* Class Selector */}
            <div>
              <label htmlFor="filter-class" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                Lớp học
              </label>
              {!canReadClasses ? (
                <div className="text-xs text-amber-300 p-2 rounded bg-amber-500/10 border border-amber-500/30">
                  Cần quyền đọc lớp học (organization.classes.read)
                </div>
              ) : (
                <select
                  id="filter-class"
                  value={selectedClassId}
                  onChange={(e) => handleFilterChange(setSelectedClassId, e.target.value)}
                  disabled={isLoadingClasses}
                  className="cm-select w-full text-sm"
                >
                  <option value="">-- Tất cả lớp học --</option>
                  {classesData?.data?.map((c) => (
                    <option key={c.classId} value={c.classId}>
                      {c.className} ({c.academicYear})
                    </option>
                  ))}
                </select>
              )}
            </div>

            {/* Status Selector */}
            <div>
              <label htmlFor="filter-status" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                Trạng thái bài tập
              </label>
              <select
                id="filter-status"
                value={selectedStatus}
                onChange={(e) => handleFilterChange(setSelectedStatus, e.target.value)}
                className="cm-select w-full text-sm"
              >
                <option value="">-- Tất cả trạng thái --</option>
                <option value="Draft">Bản nháp (Draft)</option>
                <option value="Published">Đã xuất bản (Published)</option>
                <option value="Closed">Đã đóng (Closed)</option>
                <option value="Archived">Đã lưu trữ (Archived)</option>
              </select>
            </div>
          </div>
        </section>

        {/* Loading State */}
        {isLoading && (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {Array.from({ length: 6 }).map((_, idx) => (
              <div key={idx} className="rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] p-5 space-y-4">
                <div className="flex justify-between">
                  <Skeleton className="h-5 w-32 rounded" />
                  <Skeleton className="h-5 w-16 rounded-full" />
                </div>
                <Skeleton className="h-10 w-full rounded" />
                <Skeleton className="h-4 w-3/4 rounded" />
              </div>
            ))}
          </div>
        )}

        {/* Error State */}
        {isError && (
          <SafeErrorPanel
            error={error}
            fallback="Không thể tải danh sách bài tập từ hệ thống."
            onRetry={() => refetch()}
          />
        )}

        {/* Empty State */}
        {!isLoading && !isError && (!response?.data || response.data.length === 0) && (
          <div className="rounded-2xl border border-dashed border-[var(--cm-border)] bg-[var(--cm-surface)] p-12 text-center">
            <p className="text-sm text-[var(--cm-text-secondary)]">Không tìm thấy bài tập nào phù hợp với bộ lọc hiện tại.</p>
            {canCreate && (
              <button
                type="button"
                onClick={() => navigate("/quan-ly/bai-tap/tao-moi")}
                className="cm-primary-button mt-4 text-xs inline-block"
              >
                + Soạn bài tập đầu tiên
              </button>
            )}
          </div>
        )}

        {/* Assignments Grid */}
        {!isLoading && !isError && response?.data && response.data.length > 0 && (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {response.data.map((assignment) => {
              const isDraft = assignment.status === "Draft";
              const isPublished = assignment.status === "Published";
              const isClosed = assignment.status === "Closed";
              const isArchived = assignment.status === "Archived";

              const classNameText = classMap.get(assignment.classId) || `Lớp: ${assignment.classId.slice(0, 8)}...`;

              return (
                <article
                  key={assignment.assignmentId}
                  className="rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] p-5 shadow-lg flex flex-col justify-between hover:border-[var(--cm-cyan)]/40 transition"
                >
                  <div className="space-y-3">
                    {/* Header: Class tag & Status badge */}
                    <div className="flex items-center justify-between gap-2">
                      <span className="text-xs font-semibold text-[var(--cm-cyan)] truncate max-w-[200px]">
                        {classNameText}
                      </span>
                      <StatusBadge status={assignment.status} />
                    </div>

                    {/* Title */}
                    <h2 className="text-base font-semibold text-[var(--cm-text)] line-clamp-2">
                      {assignment.title}
                    </h2>

                    {/* Due Date */}
                    <p className="text-xs text-[var(--cm-text-muted)]">
                      {formatDueDate(assignment.dueAt)}
                    </p>

                    {/* Metrics row: Question count & Target student count */}
                    <div className="flex flex-wrap items-center gap-2 pt-1 text-xs text-[var(--cm-text-muted)]">
                      <span className="rounded bg-[var(--cm-surface-subtle)] px-2.5 py-1 border border-[var(--cm-border-subtle)]">
                        Câu hỏi: <strong className="text-[var(--cm-text)]">{assignment.questionCount}</strong>
                      </span>
                      <span className="rounded bg-[var(--cm-surface-subtle)] px-2.5 py-1 border border-[var(--cm-border-subtle)]">
                        Học sinh:{" "}
                        <strong className="text-[var(--cm-text)]">
                          {isDraft
                            ? "Theo lớp (khi xuất bản)"
                            : `${assignment.targetStudentCount} học sinh`}
                        </strong>
                      </span>
                    </div>
                  </div>

                  {/* Actions according to canonical state machine */}
                  <div className="mt-5 flex flex-wrap items-center justify-between gap-2 border-t border-[var(--cm-border-subtle)] pt-4">
                    <div className="flex flex-wrap items-center gap-1.5">
                      {/* Draft actions: Edit & Publish */}
                      {isDraft && (
                        <>
                          {canUpdate && (
                            <Link
                              to={`/quan-ly/bai-tap/${assignment.assignmentId}`}
                              className="text-xs font-semibold text-[var(--cm-cyan)] hover:underline"
                            >
                              Chỉnh sửa →
                            </Link>
                          )}
                          {canPublish && (
                            <button
                              type="button"
                              onClick={() => handleOpenDialog(assignment, "publish")}
                              className="text-xs font-semibold text-emerald-400 hover:text-emerald-300 px-2 py-1 rounded bg-emerald-500/10 border border-emerald-500/20 transition"
                            >
                              Xuất bản
                            </button>
                          )}
                        </>
                      )}

                      {/* Published actions: View Progress & Close */}
                      {isPublished && (
                        <>
                          <Link
                            to={`/quan-ly/bai-tap/${assignment.assignmentId}/tien-do`}
                            className="text-xs font-semibold text-[var(--cm-cyan)] hover:underline"
                          >
                            Xem tiến độ →
                          </Link>
                          {canClose && (
                            <button
                              type="button"
                              onClick={() => handleOpenDialog(assignment, "close")}
                              className="text-xs font-semibold text-rose-400 hover:text-rose-300 px-2 py-1 rounded bg-rose-500/10 border border-rose-500/20 transition"
                            >
                              Đóng bài tập
                            </button>
                          )}
                        </>
                      )}

                      {/* Closed or Archived actions: View Progress only */}
                      {(isClosed || isArchived) && (
                        <Link
                          to={`/quan-ly/bai-tap/${assignment.assignmentId}/tien-do`}
                          className="text-xs font-semibold text-[var(--cm-cyan)] hover:underline"
                        >
                          Xem tiến độ →
                        </Link>
                      )}
                    </div>

                    {/* View Details Link for non-Draft */}
                    {!isDraft && (
                      <Link
                        to={`/quan-ly/bai-tap/${assignment.assignmentId}`}
                        className="text-xs text-[var(--cm-text-muted)] hover:text-[var(--cm-text)]"
                      >
                        Chi tiết
                      </Link>
                    )}
                  </div>
                </article>
              );
            })}
          </div>
        )}

        {/* Server-Side Pagination Bar */}
        {!isLoading && !isError && totalItems > 0 && (
          <nav
            aria-label="Phân trang bài tập"
            className="flex flex-col sm:flex-row items-center justify-between gap-4 rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] p-4 shadow-xl text-xs text-[var(--cm-text-secondary)]"
          >
            <div>
              Hiển thị trang <strong className="text-[var(--cm-text)]">{page}</strong> /{" "}
              <strong className="text-[var(--cm-text)]">{totalPages}</strong> ({totalItems} bài tập)
            </div>
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1}
                className="cm-secondary-button text-xs disabled:opacity-40 disabled:cursor-not-allowed"
              >
                ← Trang trước
              </button>
              <button
                type="button"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages}
                className="cm-secondary-button text-xs disabled:opacity-40 disabled:cursor-not-allowed"
              >
                Trang sau →
              </button>
            </div>
          </nav>
        )}

        {/* State Machine Transition Dialog */}
        <ConfirmDialog
          isOpen={dialogAction !== null}
          title={dialogAction === "publish" ? "Xuất bản bài tập" : "Đóng bài tập"}
          description={
            dialogAction === "publish"
              ? "Khi xuất bản, hệ thống sẽ chốt snapshot danh sách học sinh tham gia và học sinh sẽ nhìn thấy bài tập trong cổng làm bài. Thao tác này sẽ khóa cấu hình câu hỏi!"
              : "Khi đóng bài tập, học sinh sẽ không thể nộp bài mới. Trạng thái bài tập sẽ chuyển sang Đã đóng (Closed)."
          }
          confirmLabel={dialogAction === "publish" ? "Xuất bản ngay" : "Đóng bài tập"}
          tone={dialogAction === "close" ? "danger" : "default"}
          isConfirming={publishMutation.isPending || closeMutation.isPending}
          onConfirm={handleConfirmAction}
          onClose={handleCloseDialog}
        />
      </div>
    </CenterManagerThemeScope>
  );
}

// =============================================================================
// 2. LEGACY ASSIGNMENT LIST VIEW (FOR TEACHER & OTHER ACTORS)
// =============================================================================

function LegacyAssignmentListPage() {
  const [classId, setClassId] = useState<string>("");
  const [status, setStatus] = useState<AssignmentStatus | "">("");
  const canCreate = useAuthStore((state) => state.hasPermission(permissions.assignmentsCreate));
  const canUpdate = useAuthStore((state) => state.hasPermission(permissions.assignmentsUpdate));

  const { data: response, isLoading, isError, error } = useAssignments({
    classId: classId || undefined,
    status: (status as AssignmentStatus) || undefined,
  });

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-3xl font-bold text-slate-800">Quản lý Bài tập</h1>
        {canCreate && (
          <Link
            to="/quan-ly/bai-tap/tao-moi"
            className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition shadow-sm font-medium"
          >
            Tạo bài tập mới
          </Link>
        )}
      </div>

      <div className="bg-white p-4 rounded-xl shadow-sm border border-slate-100 mb-6 flex gap-4">
        <div className="flex-1">
          <label className="block text-sm font-medium text-slate-600 mb-1">ID Lớp học</label>
          <input
            type="text"
            placeholder="Nhập ID lớp học..."
            value={classId}
            onChange={(e) => setClassId(e.target.value)}
            className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500 outline-none"
          />
        </div>
        <div className="flex-1">
          <label className="block text-sm font-medium text-slate-600 mb-1">Trạng thái</label>
          <select
            value={status}
            onChange={(e) => setStatus(e.target.value as AssignmentStatus | "")}
            className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500 outline-none bg-white"
          >
            <option value="">Tất cả</option>
            <option value="Draft">Bản nháp (Draft)</option>
            <option value="Published">Đã xuất bản (Published)</option>
            <option value="Closed">Đã đóng (Closed)</option>
            <option value="Archived">Đã lưu trữ (Archived)</option>
          </select>
        </div>
      </div>

      {isLoading && (
        <div className="flex justify-center items-center py-20">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
        </div>
      )}

      {isError && (
        <div className="bg-red-50 text-red-600 p-4 rounded-lg border border-red-100">
          Đã có lỗi xảy ra: {mapSafeOperationalError(error, "Không thể tải danh sách bài tập.")}
        </div>
      )}

      {!isLoading && !isError && (!response?.data || response.data.length === 0) && (
        <div className="text-center py-20 bg-slate-50 rounded-xl border border-slate-100">
          <p className="text-slate-500 mb-4">Không tìm thấy bài tập nào phù hợp.</p>
        </div>
      )}

      {!isLoading && !isError && response?.data && response.data.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {response.data.map((assignment) => (
            <div
              key={assignment.assignmentId}
              className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden hover:shadow-md transition flex flex-col"
            >
              <div className="p-5 flex-1">
                <div className="flex justify-between items-start mb-2">
                  <h3 className="font-bold text-lg text-slate-800 line-clamp-1">{assignment.title}</h3>
                  <span
                    className={`px-2 py-1 text-xs font-semibold rounded-full ${
                      assignment.status === "Published"
                        ? "bg-green-100 text-green-700"
                        : assignment.status === "Closed"
                        ? "bg-red-100 text-red-700"
                        : assignment.status === "Draft"
                        ? "bg-amber-100 text-amber-700"
                        : "bg-slate-100 text-slate-700"
                    }`}
                  >
                    {assignment.status}
                  </span>
                </div>
                <p className="text-sm text-slate-500 mb-4">Lớp: {assignment.classId}</p>
                <div className="flex justify-between text-xs text-slate-600 mt-auto pt-4 border-t border-slate-100">
                  <span>{assignment.questionCount} câu hỏi</span>
                  <span>{assignment.targetStudentCount} học sinh</span>
                </div>
              </div>
              <div className="bg-slate-50 p-4 border-t border-slate-100 flex justify-between items-center">
                <Link
                  to={`/quan-ly/bai-tap/${assignment.assignmentId}/tien-do`}
                  className="text-blue-600 text-sm font-medium hover:underline"
                >
                  Xem tiến độ &rarr;
                </Link>
                {canUpdate && (
                  <Link
                    to={`/quan-ly/bai-tap/${assignment.assignmentId}`}
                    className="text-slate-600 text-sm font-medium hover:underline"
                  >
                    Chi tiết
                  </Link>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// =============================================================================
// 3. MAIN ROUTE EXPORT WITH ACTOR ISOLATION
// =============================================================================

export const AssignmentListPage = () => {
  const accountType = useAuthStore((state) => state.user?.accountType);
  if (accountType === "CenterManager") {
    return <CenterManagerAssignmentListView />;
  }
  return <LegacyAssignmentListPage />;
};
