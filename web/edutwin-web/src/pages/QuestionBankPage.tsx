import { useState, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  useQuestions,
  useActivateQuestion,
  useArchiveQuestion,
  useDeleteQuestion,
} from "../features/questions/useQuestions";
import { organizationApi } from "../api/organizationApi";
import type {
  Question,
  QuestionFilter,
  QuestionType,
  QuestionStatus,
} from "../types/questions";
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
import { MathFormulaPreview } from "../components/math/MathFormulaPreview";

// =============================================================================
// 1. CENTER MANAGER DARK SAAS VIEW (GATE 6B)
// =============================================================================

function CenterManagerQuestionBankView() {
  const navigate = useNavigate();
  const hasPermission = useAuthStore((state) => state.hasPermission);

  const canCreate = hasPermission(permissions.questionsCreate);
  const canUpdate = hasPermission(permissions.questionsUpdate);
  const canPublish = hasPermission(permissions.questionsPublish);
  const canDelete = hasPermission(permissions.questionsDelete);
  const canReadSubjects = hasPermission(permissions.subjectsRead);

  // Filters state (strictly matching backend QuestionListQuery contract: no search param)
  const [selectedSubjectId, setSelectedSubjectId] = useState<string>("");
  const [selectedType, setSelectedType] = useState<QuestionType | "">("");
  const [selectedDifficulty, setSelectedDifficulty] = useState<number | "">("");
  const [selectedStatus, setSelectedStatus] = useState<QuestionStatus | "">("");
  const [page, setPage] = useState<number>(1);
  const pageSize = 12;

  // Local search text for quick client-side filtering on the current page
  const [localSearchText, setLocalSearchText] = useState("");

  // Operational feedback states
  const [actionError, setActionError] = useState<{ message: string; traceId?: string | null } | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  // Dialog states for state machine transitions
  const [targetQuestion, setTargetQuestion] = useState<Question | null>(null);
  const [dialogAction, setDialogAction] = useState<"activate" | "archive" | "delete" | null>(null);

  // Canonical subject list query
  const {
    data: subjectsData,
    isLoading: isLoadingSubjects,
  } = useQuery({
    queryKey: ["subjects", "active-for-question-bank"],
    queryFn: () => organizationApi.listSubjects(true),
    enabled: canReadSubjects,
  });

  // Canonical questions list query with server-side pagination & filters
  const questionFilter: QuestionFilter = useMemo(
    () => ({
      subjectId: selectedSubjectId || undefined,
      type: (selectedType as QuestionType) || undefined,
      difficulty: selectedDifficulty !== "" ? Number(selectedDifficulty) : undefined,
      status: (selectedStatus as QuestionStatus) || undefined,
      page,
      pageSize,
    }),
    [selectedSubjectId, selectedType, selectedDifficulty, selectedStatus, page, pageSize]
  );

  const {
    data: response,
    isLoading,
    isError,
    error,
    refetch,
  } = useQuestions(questionFilter);

  const activateMutation = useActivateQuestion();
  const archiveMutation = useArchiveQuestion();
  const deleteMutation = useDeleteQuestion();

  // Reset page to 1 when filters change
  const handleFilterChange = (setter: (val: any) => void, val: any) => {
    setter(val);
    setPage(1);
  };

  // Filter questions on current page using localSearchText if provided
  const displayedQuestions = useMemo(() => {
    const list = response?.data || [];
    const term = localSearchText.trim().toLowerCase();
    if (!term) return list;
    return list.filter(
      (q) =>
        q.questionText.toLowerCase().includes(term) ||
        q.correctAnswer?.toLowerCase().includes(term)
    );
  }, [response?.data, localSearchText]);

  const pagedMeta = response?.meta as { page?: number; pageSize?: number; totalItems?: number; totalPages?: number; traceId?: string } | undefined;
  const totalItems = pagedMeta?.totalItems ?? response?.data?.length ?? 0;
  const totalPages = pagedMeta?.totalPages ?? Math.max(1, Math.ceil(totalItems / pageSize));

  // State machine confirmation handlers
  const handleOpenDialog = (question: Question, action: "activate" | "archive" | "delete") => {
    setActionError(null);
    setActionSuccess(null);
    setTargetQuestion(question);
    setDialogAction(action);
  };

  const handleCloseDialog = () => {
    setTargetQuestion(null);
    setDialogAction(null);
  };

  const handleConfirmAction = () => {
    if (!targetQuestion || !dialogAction) return;

    if (dialogAction === "activate") {
      activateMutation.mutate(
        { id: targetQuestion.questionId, data: { rowVersion: targetQuestion.rowVersion } },
        {
          onSuccess: () => {
            setActionSuccess(`Đã kích hoạt câu hỏi "${targetQuestion.questionId.slice(0, 8)}..." thành công.`);
            handleCloseDialog();
          },
          onError: (err: any) => {
            const details = extractProblemDetails(err);
            setActionError({
              message: mapSafeOperationalError(err, "Không thể kích hoạt câu hỏi."),
              traceId: details.traceId,
            });
            handleCloseDialog();
          },
        }
      );
    } else if (dialogAction === "archive") {
      archiveMutation.mutate(
        { id: targetQuestion.questionId, data: { rowVersion: targetQuestion.rowVersion } },
        {
          onSuccess: () => {
            setActionSuccess(`Đã lưu trữ câu hỏi "${targetQuestion.questionId.slice(0, 8)}..." thành công.`);
            handleCloseDialog();
          },
          onError: (err: any) => {
            const details = extractProblemDetails(err);
            setActionError({
              message: mapSafeOperationalError(err, "Không thể lưu trữ câu hỏi."),
              traceId: details.traceId,
            });
            handleCloseDialog();
          },
        }
      );
    } else if (dialogAction === "delete") {
      // DELETE /api/v1/questions/{id} without request body
      deleteMutation.mutate(targetQuestion.questionId, {
        onSuccess: () => {
          setActionSuccess(`Đã xóa vĩnh viễn câu hỏi bản nháp thành công.`);
          handleCloseDialog();
        },
        onError: (err: any) => {
          const details = extractProblemDetails(err);
          setActionError({
            message: mapSafeOperationalError(err, "Không thể xóa câu hỏi bản nháp. Vui lòng kiểm tra ràng buộc."),
            traceId: details.traceId,
          });
          handleCloseDialog();
        },
      });
    }
  };

  return (
    <CenterManagerThemeScope data-actor="center-manager">
      <div className="space-y-6 p-6 lg:p-8">
        <PageHeader
          eyebrow="NỘI DUNG HỌC THUẬT"
          title="Ngân hàng Câu hỏi"
          description="Kho học liệu câu hỏi chuẩn hóa đa định dạng (Trắc nghiệm, Điền khuyết, Tự luận) tích hợp biểu thức KaTeX và chế độ chấm điểm AI."
          breadcrumbs={[
            { label: "CenterManager", href: "/quan-ly/tong-quan-trung-tam" },
            { label: "Ngân hàng câu hỏi" },
          ]}
          actions={
            canCreate ? (
              <button
                type="button"
                id="btn-create-question"
                onClick={() => navigate("/quan-ly/cau-hoi/tao-moi")}
                className="cm-primary-button flex items-center gap-2"
              >
                <span>+ Tạo câu hỏi mới</span>
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

        {/* Server-Side Filter Bar (No search param to server, pure canonical filters) */}
        <section
          aria-label="Bộ lọc ngân hàng câu hỏi"
          className="rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] p-5 shadow-xl"
        >
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-5">
            {/* Subject Selector */}
            <div>
              <label htmlFor="filter-subject" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                Môn học
              </label>
              <select
                id="filter-subject"
                value={selectedSubjectId}
                onChange={(e) => handleFilterChange(setSelectedSubjectId, e.target.value)}
                disabled={isLoadingSubjects}
                className="cm-select w-full text-sm"
              >
                <option value="">-- Tất cả môn học --</option>
                {subjectsData?.data?.map((s) => (
                  <option key={s.subjectId} value={s.subjectId}>
                    {s.subjectName} ({s.subjectCode})
                  </option>
                ))}
              </select>
            </div>

            {/* Question Type */}
            <div>
              <label htmlFor="filter-type" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                Loại câu hỏi
              </label>
              <select
                id="filter-type"
                value={selectedType}
                onChange={(e) => handleFilterChange(setSelectedType, e.target.value)}
                className="cm-select w-full text-sm"
              >
                <option value="">-- Tất cả loại --</option>
                <option value="MultipleChoice">Trắc nghiệm (MultipleChoice)</option>
                <option value="ShortAnswer">Điền khuyết (ShortAnswer)</option>
                <option value="Essay">Tự luận (Essay)</option>
              </select>
            </div>

            {/* Difficulty */}
            <div>
              <label htmlFor="filter-difficulty" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                Độ khó (1-5)
              </label>
              <select
                id="filter-difficulty"
                value={selectedDifficulty}
                onChange={(e) => handleFilterChange(setSelectedDifficulty, e.target.value)}
                className="cm-select w-full text-sm"
              >
                <option value="">-- Tất cả độ khó --</option>
                <option value="1">1 - Rất dễ</option>
                <option value="2">2 - Dễ</option>
                <option value="3">3 - Trung bình</option>
                <option value="4">4 - Khó</option>
                <option value="5">5 - Rất khó</option>
              </select>
            </div>

            {/* Status */}
            <div>
              <label htmlFor="filter-status" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                Trạng thái
              </label>
              <select
                id="filter-status"
                value={selectedStatus}
                onChange={(e) => handleFilterChange(setSelectedStatus, e.target.value)}
                className="cm-select w-full text-sm"
              >
                <option value="">-- Tất cả trạng thái --</option>
                <option value="Draft">Bản nháp (Draft)</option>
                <option value="Active">Đang hoạt động (Active)</option>
                <option value="Archived">Đã lưu trữ (Archived)</option>
              </select>
            </div>

            {/* Quick Local Search Filter */}
            <div>
              <label htmlFor="filter-local-search" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                Lọc nội dung trang
              </label>
              <input
                id="filter-local-search"
                type="text"
                value={localSearchText}
                onChange={(e) => setLocalSearchText(e.target.value)}
                placeholder="Tìm nhanh trên trang..."
                className="cm-input w-full text-sm"
              />
            </div>
          </div>
        </section>

        {/* Content Loading State */}
        {isLoading && (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {Array.from({ length: 6 }).map((_, idx) => (
              <div key={idx} className="rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] p-5 space-y-4">
                <div className="flex justify-between">
                  <Skeleton className="h-5 w-24 rounded" />
                  <Skeleton className="h-5 w-16 rounded-full" />
                </div>
                <Skeleton className="h-16 w-full rounded" />
                <Skeleton className="h-4 w-3/4 rounded" />
              </div>
            ))}
          </div>
        )}

        {/* Content Error State */}
        {isError && (
          <SafeErrorPanel
            error={error}
            fallback="Không thể tải danh sách ngân hàng câu hỏi từ hệ thống."
            onRetry={() => refetch()}
          />
        )}

        {/* Empty State */}
        {!isLoading && !isError && displayedQuestions.length === 0 && (
          <div className="rounded-2xl border border-dashed border-[var(--cm-border)] bg-[var(--cm-surface)] p-12 text-center">
            <p className="text-sm text-[var(--cm-text-secondary)]">Không tìm thấy câu hỏi nào phù hợp với bộ lọc hiện tại.</p>
            {canCreate && (
              <button
                type="button"
                onClick={() => navigate("/quan-ly/cau-hoi/tao-moi")}
                className="cm-primary-button mt-4 text-xs inline-block"
              >
                + Soạn câu hỏi đầu tiên
              </button>
            )}
          </div>
        )}

        {/* Questions Grid */}
        {!isLoading && !isError && displayedQuestions.length > 0 && (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {displayedQuestions.map((q) => {
              const isDraft = q.status === "Draft";
              const isActive = q.status === "Active";
              const isArchived = q.status === "Archived";

              const typeBadge =
                q.questionType === "MultipleChoice"
                  ? "border-blue-500/30 bg-blue-500/10 text-blue-300"
                  : q.questionType === "Essay"
                  ? "border-purple-500/30 bg-purple-500/10 text-purple-300"
                  : "border-indigo-500/30 bg-indigo-500/10 text-indigo-300";

              return (
                <article
                  key={q.questionId}
                  className="rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] p-5 shadow-lg flex flex-col justify-between hover:border-[var(--cm-cyan)]/40 transition"
                >
                  <div className="space-y-3">
                    {/* Header badges */}
                    <div className="flex items-center justify-between gap-2">
                      <span className={`px-2 py-0.5 text-[11px] font-semibold rounded-md border ${typeBadge}`}>
                        {q.questionType}
                      </span>
                      <StatusBadge status={q.status} />
                    </div>

                    {/* Question text with KaTeX preview support */}
                    <div className="text-sm font-medium text-[var(--cm-text)] line-clamp-3">
                      {q.questionText}
                    </div>
                    {q.questionText.includes("\\") && (
                      <div className="text-xs">
                        <MathFormulaPreview formula={q.questionText} displayMode={false} label="KaTeX Preview" />
                      </div>
                    )}

                    {/* Metadata tags */}
                    <div className="flex flex-wrap items-center gap-2 pt-1 text-xs text-[var(--cm-text-muted)]">
                      <span className="rounded bg-[var(--cm-surface-subtle)] px-2 py-0.5 border border-[var(--cm-border-subtle)]">
                        Độ khó: <strong className="text-[var(--cm-text)]">{q.difficulty}</strong>/5
                      </span>
                      <span className="rounded bg-[var(--cm-surface-subtle)] px-2 py-0.5 border border-[var(--cm-border-subtle)]">
                        Điểm: <strong className="text-[var(--cm-text)]">{q.maxScore}</strong>
                      </span>
                      <span className="rounded bg-[var(--cm-surface-subtle)] px-2 py-0.5 border border-[var(--cm-border-subtle)]">
                        {q.estimatedTimeSeconds}s
                      </span>
                      {q.answerEvaluationMode && (
                        <span className="rounded bg-cyan-950/40 text-cyan-300 px-2 py-0.5 border border-cyan-500/20 text-[10px] font-mono">
                          {q.answerEvaluationMode}
                        </span>
                      )}
                      {q.reasoningRequired && (
                        <span className="rounded bg-amber-950/40 text-amber-300 px-2 py-0.5 border border-amber-500/20 text-[10px]">
                          ⚡ Yêu cầu lập luận
                        </span>
                      )}
                    </div>
                  </div>

                  {/* Actions according to canonical state machine */}
                  <div className="mt-5 flex items-center justify-between border-t border-[var(--cm-border-subtle)] pt-4">
                    <div className="flex flex-wrap items-center gap-1.5">
                      {/* Draft actions: Activate, Archive, Delete */}
                      {isDraft && (
                        <>
                          {canPublish && (
                            <button
                              type="button"
                              onClick={() => handleOpenDialog(q, "activate")}
                              className="text-xs font-semibold text-emerald-400 hover:text-emerald-300 px-2 py-1 rounded bg-emerald-500/10 border border-emerald-500/20 transition"
                            >
                              Kích hoạt
                            </button>
                          )}
                          {canPublish && (
                            <button
                              type="button"
                              onClick={() => handleOpenDialog(q, "archive")}
                              className="text-xs font-semibold text-slate-300 hover:text-slate-100 px-2 py-1 rounded bg-slate-500/10 border border-slate-500/20 transition"
                            >
                              Lưu trữ
                            </button>
                          )}
                          {canDelete && (
                            <button
                              type="button"
                              onClick={() => handleOpenDialog(q, "delete")}
                              className="text-xs font-semibold text-rose-400 hover:text-rose-300 px-2 py-1 rounded bg-rose-500/10 border border-rose-500/20 transition"
                            >
                              Xóa
                            </button>
                          )}
                        </>
                      )}

                      {/* Active actions: Archive only */}
                      {isActive && canPublish && (
                        <button
                          type="button"
                          onClick={() => handleOpenDialog(q, "archive")}
                          className="text-xs font-semibold text-slate-300 hover:text-slate-100 px-2 py-1 rounded bg-slate-500/10 border border-slate-500/20 transition"
                        >
                          Lưu trữ
                        </button>
                      )}

                      {/* Archived actions: Activate only */}
                      {isArchived && canPublish && (
                        <button
                          type="button"
                          onClick={() => handleOpenDialog(q, "activate")}
                          className="text-xs font-semibold text-emerald-400 hover:text-emerald-300 px-2 py-1 rounded bg-emerald-500/10 border border-emerald-500/20 transition"
                        >
                          Kích hoạt lại
                        </button>
                      )}
                    </div>

                    {/* Navigation / Edit / Detail */}
                    <button
                      type="button"
                      onClick={() => navigate(`/quan-ly/cau-hoi/${q.questionId}`)}
                      className="text-xs font-semibold text-[var(--cm-cyan)] hover:underline ml-auto"
                    >
                      {isDraft && canUpdate ? "Chỉnh sửa →" : "Xem chi tiết →"}
                    </button>
                  </div>
                </article>
              );
            })}
          </div>
        )}

        {/* Server-Side Pagination Bar */}
        {!isLoading && !isError && totalItems > 0 && (
          <nav
            aria-label="Phân trang câu hỏi"
            className="flex flex-col sm:flex-row items-center justify-between gap-4 rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] p-4 shadow-xl text-xs text-[var(--cm-text-secondary)]"
          >
            <div>
              Hiển thị trang <strong className="text-[var(--cm-text)]">{page}</strong> /{" "}
              <strong className="text-[var(--cm-text)]">{totalPages}</strong> ({totalItems} câu hỏi)
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

        {/* Dialog Xác nhận Chuyển trạng thái / Xóa */}
        <ConfirmDialog
          isOpen={dialogAction !== null}
          title={
            dialogAction === "activate"
              ? "Kích hoạt câu hỏi"
              : dialogAction === "archive"
              ? "Lưu trữ câu hỏi"
              : "Xóa câu hỏi bản nháp"
          }
          description={
            dialogAction === "activate"
              ? "Bạn có chắc muốn kích hoạt câu hỏi này? Câu hỏi sẽ chuyển sang trạng thái Hoạt động (Active) và có thể được chỉ định vào các bài tập."
              : dialogAction === "archive"
              ? "Bạn có chắc muốn lưu trữ câu hỏi này? Câu hỏi sẽ chuyển sang trạng thái Lưu trữ (Archived) và không thể gán vào bài tập mới."
              : "CẢNH BÁO: Hành động này sẽ xóa vĩnh viễn câu hỏi bản nháp khỏi hệ thống. Thao tác không thể hoàn tác!"
          }
          confirmLabel={
            dialogAction === "activate"
              ? "Kích hoạt"
              : dialogAction === "archive"
              ? "Lưu trữ"
              : "Xóa vĩnh viễn"
          }
          tone={dialogAction === "delete" ? "danger" : "default"}
          isConfirming={
            activateMutation.isPending || archiveMutation.isPending || deleteMutation.isPending
          }
          onConfirm={handleConfirmAction}
          onClose={handleCloseDialog}
        />
      </div>
    </CenterManagerThemeScope>
  );
}

// =============================================================================
// 2. LEGACY QUESTION BANK VIEW (FOR TEACHER & OTHER ACTORS)
// =============================================================================

function LegacyQuestionBankPage() {
  const [filter, setFilter] = useState<QuestionFilter>({});
  const hasPermission = useAuthStore((state) => state.hasPermission);
  const canCreate = hasPermission(permissions.questionsCreate);
  const canUpdate = hasPermission(permissions.questionsUpdate);
  const canPublish = hasPermission(permissions.questionsPublish);

  const { data: response, isLoading, isError, error } = useQuestions(filter);
  const activateMutation = useActivateQuestion();
  const archiveMutation = useArchiveQuestion();

  const handleActivate = (questionId: string, rowVersion: string) => {
    activateMutation.mutate(
      { id: questionId, data: { rowVersion } },
      {
        onSuccess: () => alert("Kích hoạt câu hỏi thành công!"),
        onError: (err: unknown) => alert(mapSafeOperationalError(err, "Không thể kích hoạt câu hỏi.")),
      }
    );
  };

  const handleArchive = (questionId: string, rowVersion: string) => {
    archiveMutation.mutate(
      { id: questionId, data: { rowVersion } },
      {
        onSuccess: () => alert("Lưu trữ câu hỏi thành công!"),
        onError: (err: unknown) => alert(mapSafeOperationalError(err, "Không thể lưu trữ câu hỏi.")),
      }
    );
  };

  const handleFilterChange = (key: keyof QuestionFilter, value: any) => {
    setFilter((prev) => ({
      ...prev,
      [key]: value === "" ? undefined : value,
    }));
  };

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-3xl font-bold text-slate-800">Ngân hàng Câu hỏi</h1>
        {canCreate && (
          <button
            onClick={() => (window.location.href = "/quan-ly/cau-hoi/tao-moi")}
            className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition shadow-sm font-medium"
          >
            Tạo Câu hỏi mới
          </button>
        )}
      </div>

      <div className="bg-white p-4 rounded-xl shadow-sm border border-slate-100 mb-6 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <div>
          <label className="block text-sm font-medium text-slate-600 mb-1">Môn học (Subject ID)</label>
          <input
            type="text"
            placeholder="Nhập ID môn học..."
            value={filter.subjectId || ""}
            onChange={(e) => handleFilterChange("subjectId", e.target.value)}
            className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500 outline-none"
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-slate-600 mb-1">Loại câu hỏi</label>
          <select
            value={filter.type || ""}
            onChange={(e) => handleFilterChange("type", e.target.value)}
            className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500 outline-none bg-white"
          >
            <option value="">Tất cả</option>
            <option value="MultipleChoice">Trắc nghiệm (Multiple Choice)</option>
            <option value="ShortAnswer">Điền khuyết (Short Answer)</option>
            <option value="Essay">Tự luận (Essay)</option>
          </select>
        </div>
        <div>
          <label className="block text-sm font-medium text-slate-600 mb-1">Độ khó (1-5)</label>
          <select
            value={filter.difficulty || ""}
            onChange={(e) => handleFilterChange("difficulty", e.target.value ? Number(e.target.value) : "")}
            className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500 outline-none bg-white"
          >
            <option value="">Tất cả</option>
            <option value="1">1 - Rất dễ</option>
            <option value="2">2 - Dễ</option>
            <option value="3">3 - Trung bình</option>
            <option value="4">4 - Khó</option>
            <option value="5">5 - Rất khó</option>
          </select>
        </div>
        <div>
          <label className="block text-sm font-medium text-slate-600 mb-1">Trạng thái</label>
          <select
            value={filter.status || ""}
            onChange={(e) => handleFilterChange("status", e.target.value)}
            className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500 outline-none bg-white"
          >
            <option value="">Tất cả</option>
            <option value="Draft">Bản nháp (Draft)</option>
            <option value="Active">Đang hoạt động (Active)</option>
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
          {mapSafeOperationalError(error, "Không thể tải ngân hàng câu hỏi.")}
        </div>
      )}

      {!isLoading && !isError && (!response?.data || response.data.length === 0) && (
        <div className="text-center py-20 bg-slate-50 rounded-xl border border-slate-100">
          <p className="text-slate-500 mb-4">Không tìm thấy câu hỏi nào phù hợp.</p>
        </div>
      )}

      {!isLoading && !isError && response?.data && response.data.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {response.data.map((question) => (
            <div
              key={question.questionId}
              className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden hover:shadow-md transition"
            >
              <div className="p-5">
                <div className="flex justify-between items-start mb-3">
                  <span
                    className={`px-2 py-1 text-xs font-semibold rounded-md ${
                      question.questionType === "MultipleChoice"
                        ? "bg-blue-50 text-blue-700"
                        : question.questionType === "Essay"
                        ? "bg-purple-50 text-purple-700"
                        : "bg-indigo-50 text-indigo-700"
                    }`}
                  >
                    {question.questionType}
                  </span>
                  <span
                    className={`px-2 py-1 text-xs font-semibold rounded-full ${
                      question.status === "Active"
                        ? "bg-green-100 text-green-700"
                        : question.status === "Draft"
                        ? "bg-amber-100 text-amber-700"
                        : "bg-slate-100 text-slate-700"
                    }`}
                  >
                    {question.status}
                  </span>
                </div>
                <h3 className="font-medium text-slate-800 line-clamp-2 mb-3">{question.questionText}</h3>

                <div className="flex flex-wrap gap-3 text-xs text-slate-500 mb-4">
                  <div className="flex items-center gap-1">
                    <span className="font-medium text-slate-700">Độ khó:</span> {question.difficulty}
                  </div>
                  <div className="flex items-center gap-1">
                    <span className="font-medium text-slate-700">Điểm:</span> {question.maxScore}
                  </div>
                  <div className="flex items-center gap-1">
                    <span className="font-medium text-slate-700">Thời gian:</span> {question.estimatedTimeSeconds}s
                  </div>
                </div>

                <div className="flex justify-between items-center pt-4 border-t border-slate-100">
                  <div className="flex gap-2">
                    {canPublish && (question.status === "Draft" || question.status === "Archived") && (
                      <button
                        onClick={() => handleActivate(question.questionId, question.rowVersion)}
                        disabled={activateMutation.isPending}
                        className="px-3 py-1 text-xs font-medium bg-green-50 text-green-700 border border-green-200 rounded-md hover:bg-green-100 transition disabled:opacity-50"
                      >
                        Kích hoạt
                      </button>
                    )}
                    {canPublish && (question.status === "Draft" || question.status === "Active") && (
                      <button
                        onClick={() => handleArchive(question.questionId, question.rowVersion)}
                        disabled={archiveMutation.isPending}
                        className="px-3 py-1 text-xs font-medium bg-slate-50 text-slate-600 border border-slate-200 rounded-md hover:bg-slate-100 transition disabled:opacity-50"
                      >
                        Lưu trữ
                      </button>
                    )}
                  </div>
                  {canUpdate && (
                    <button
                      onClick={() => (window.location.href = `/quan-ly/cau-hoi/${question.questionId}`)}
                      className="text-blue-600 text-sm font-medium hover:underline"
                    >
                      Chi tiết
                    </button>
                  )}
                </div>
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

export const QuestionBankPage = () => {
  const accountType = useAuthStore((state) => state.user?.accountType);
  if (accountType === "CenterManager") {
    return <CenterManagerQuestionBankView />;
  }
  return <LegacyQuestionBankPage />;
};
