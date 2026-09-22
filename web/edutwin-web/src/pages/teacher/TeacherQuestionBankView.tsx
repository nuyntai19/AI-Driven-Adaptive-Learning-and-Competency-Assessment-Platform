import { useState, useMemo } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  useQuestions,
  useActivateQuestion,
  useArchiveQuestion,
  useDeleteQuestion,
} from "../../features/questions/useQuestions";
import { organizationApi } from "../../api/organizationApi";
import type { Question, QuestionFilter, QuestionType, QuestionStatus } from "../../types/questions";
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
import { RichMathText } from "../../components/math/RichMathText";

export function TeacherQuestionBankView() {
  const navigate = useNavigate();
  const hasPermission = useAuthStore((state) => state.hasPermission);

  const canCreate = hasPermission(permissions.questionsCreate);
  const canUpdate = hasPermission(permissions.questionsUpdate);
  const canPublish = hasPermission(permissions.questionsPublish);
  const canDelete = hasPermission(permissions.questionsDelete);
  const canReadSubjects = hasPermission(permissions.subjectsRead);

  // Filters state
  const [selectedSubjectId, setSelectedSubjectId] = useState<string>("");
  const [selectedType, setSelectedType] = useState<QuestionType | "">("");
  const [selectedDifficulty, setSelectedDifficulty] = useState<number | "">("");
  const [selectedStatus, setSelectedStatus] = useState<QuestionStatus | "">("");
  const [page, setPage] = useState<number>(1);
  const pageSize = 12;

  // Local text search
  const [localSearchText, setLocalSearchText] = useState("");

  // Feedback states
  const [actionError, setActionError] = useState<{ message: string; traceId?: string | null } | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  // Dialog states
  const [targetQuestion, setTargetQuestion] = useState<Question | null>(null);
  const [dialogAction, setDialogAction] = useState<"activate" | "archive" | "delete" | null>(null);

  // Canonical subject list query
  const { data: subjectsData, isLoading: isLoadingSubjects } = useQuery({
    queryKey: ["subjects", "active-for-teacher-question-bank"],
    queryFn: () => organizationApi.listSubjects(true),
    enabled: canReadSubjects,
  });

  const subjectMap = useMemo(() => {
    const map = new Map<string, string>();
    subjectsData?.data?.forEach((s) => {
      map.set(s.subjectId, s.subjectName);
    });
    return map;
  }, [subjectsData?.data]);

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

  const { data: response, isLoading, isError, error, refetch } = useQuestions(questionFilter);

  const activateMutation = useActivateQuestion();
  const archiveMutation = useArchiveQuestion();
  const deleteMutation = useDeleteQuestion();

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
            setActionSuccess(`Đã kích hoạt câu hỏi #${targetQuestion.questionId} thành công.`);
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
            setActionSuccess(`Đã lưu trữ câu hỏi #${targetQuestion.questionId}.`);
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
      deleteMutation.mutate(
        targetQuestion.questionId,
        {
          onSuccess: () => {
            setActionSuccess(`Đã xóa câu hỏi #${targetQuestion.questionId}.`);
            handleCloseDialog();
          },
          onError: (err: any) => {
            const details = extractProblemDetails(err);
            setActionError({
              message: mapSafeOperationalError(err, "Không thể xóa câu hỏi."),
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

  // Filter items locally by search text
  const displayedQuestions = useMemo(() => {
    if (!response?.data) return [];
    if (!localSearchText.trim()) return response.data;
    const lower = localSearchText.toLowerCase();
    return response.data.filter(
      (q) =>
        q.questionText.toLowerCase().includes(lower) ||
        (q.solution && q.solution.toLowerCase().includes(lower))
    );
  }, [response?.data, localSearchText]);

  return (
    <div className="space-y-6">
      <TeacherPageHeader
        eyebrow="HỌC THUẬT & NỘI DUNG"
        title="Ngân Hàng Câu Hỏi & Đánh Giá"
        description="Quản trị câu hỏi trắc nghiệm, tự luận, công thức toán KaTeX và các tiêu chí đánh giá reasoning chuẩn hóa."
        breadcrumbs={[
          { label: "Giáo viên", href: "/giao-vien/lop-hoc" },
          { label: "Ngân hàng câu hỏi" },
        ]}
        actions={
          canCreate ? (
            <Link
              to="/giao-vien/cau-hoi/tao-moi"
              className="th-primary-button text-xs py-2 px-4 shadow-md shadow-teal-500/20"
            >
              + Soạn câu hỏi mới
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

      {/* Filter Bar */}
      <div className="rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface)] p-4 shadow-md space-y-3">
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-5 gap-3">
          {/* Subject Filter */}
          <div>
            <label className="block text-[10px] font-semibold uppercase text-[var(--th-text-muted)] mb-1">Môn học</label>
            <select
              value={selectedSubjectId}
              onChange={(e) => {
                setSelectedSubjectId(e.target.value);
                setPage(1);
              }}
              disabled={isLoadingSubjects}
              className="th-select w-full text-xs py-1.5"
            >
              <option value="">Tất cả môn học</option>
              {subjectsData?.data?.map((sub) => (
                <option key={sub.subjectId} value={sub.subjectId}>
                  {sub.subjectName} ({sub.subjectCode})
                </option>
              ))}
            </select>
          </div>

          {/* Type Filter */}
          <div>
            <label className="block text-[10px] font-semibold uppercase text-[var(--th-text-muted)] mb-1">Dạng câu hỏi</label>
            <select
              value={selectedType}
              onChange={(e) => {
                setSelectedType(e.target.value as QuestionType | "");
                setPage(1);
              }}
              className="th-select w-full text-xs py-1.5"
            >
              <option value="">Tất cả dạng</option>
              <option value="MultipleChoice">Trắc nghiệm</option>
              <option value="Essay">Tự luận</option>
              <option value="ShortAnswer">Trả lời ngắn</option>
            </select>
          </div>

          {/* Difficulty Filter */}
          <div>
            <label className="block text-[10px] font-semibold uppercase text-[var(--th-text-muted)] mb-1">Độ khó</label>
            <select
              value={selectedDifficulty}
              onChange={(e) => {
                setSelectedDifficulty(e.target.value ? Number(e.target.value) : "");
                setPage(1);
              }}
              className="th-select w-full text-xs py-1.5"
            >
              <option value="">Tất cả độ khó</option>
              <option value="1">1 - Rất dễ</option>
              <option value="2">2 - Dễ</option>
              <option value="3">3 - Trung bình</option>
              <option value="4">4 - Khó</option>
              <option value="5">5 - Rất khó</option>
            </select>
          </div>

          {/* Status Filter */}
          <div>
            <label className="block text-[10px] font-semibold uppercase text-[var(--th-text-muted)] mb-1">Trạng thái</label>
            <select
              value={selectedStatus}
              onChange={(e) => {
                setSelectedStatus(e.target.value as QuestionStatus | "");
                setPage(1);
              }}
              className="th-select w-full text-xs py-1.5"
            >
              <option value="">Tất cả trạng thái</option>
              <option value="Active">Đang hoạt động (Active)</option>
              <option value="Draft">Bản nháp (Draft)</option>
              <option value="Archived">Đã lưu trữ (Archived)</option>
            </select>
          </div>

          {/* Local search input */}
          <div>
            <label className="block text-[10px] font-semibold uppercase text-[var(--th-text-muted)] mb-1">Tìm kiếm nội dung</label>
            <input
              type="text"
              placeholder="Nhập từ khóa..."
              value={localSearchText}
              onChange={(e) => setLocalSearchText(e.target.value)}
              className="th-input w-full text-xs py-1.5"
            />
          </div>
        </div>
      </div>

      {/* Main Questions Grid */}
      {isLoading && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <TeacherSkeleton key={i} className="h-48 rounded-2xl" />
          ))}
        </div>
      )}

      {isError && (
        <TeacherSafeErrorPanel
          error={error}
          fallback="Không thể tải danh sách câu hỏi."
          onRetry={() => refetch()}
        />
      )}

      {!isLoading && !isError && displayedQuestions.length === 0 && (
        <div className="p-12 text-center text-xs text-[var(--th-text-muted)] th-surface border border-dashed border-[var(--th-border)]">
          <p className="text-base font-semibold text-[var(--th-text)] mb-1">Chưa tìm thấy câu hỏi nào</p>
          <p>Thử điều chỉnh bộ lọc hoặc bấm nút "Soạn câu hỏi mới" ở phía trên.</p>
        </div>
      )}

      {!isLoading && !isError && displayedQuestions.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {displayedQuestions.map((q) => {
            const subjectName = subjectMap.get(q.subjectId) || "Môn học";
            const initial = subjectName.slice(0, 1).toUpperCase();
            const typeLabel =
              q.questionType === "MultipleChoice"
                ? "TRẮC NGHIỆM"
                : q.questionType === "Essay"
                ? "TỰ LUẬN"
                : "TRẢ LỜI NGẮN";
            
            const difficultyLabel =
              q.difficulty === 1
                ? "RẤT DỄ"
                : q.difficulty === 2
                ? "DỄ"
                : q.difficulty === 3
                ? "TRUNG BÌNH"
                : q.difficulty === 4
                ? "KHÓ"
                : "RẤT KHÓ";

            const subjectLabel = subjectName.toUpperCase();

            return (
              <div
                key={q.questionId}
                className="th-surface p-5 flex flex-col justify-between hover:-translate-y-0.5 transition-all shadow-md"
              >
                <div className="space-y-3">
                  {/* Top header row with avatar & meta */}
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex items-center gap-3 min-w-0">
                      <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full border-[1.5px] border-[var(--th-border)] bg-[var(--at-secondary)] text-xs font-black text-slate-950 shadow-sm">
                        {initial}
                      </span>
                      <div className="min-w-0">
                        <div className="flex items-center gap-2">
                          <span className="font-mono text-xs font-bold text-[var(--at-accent-text)]">
                            #{q.questionId}
                          </span>
                          <span className="text-xs font-bold text-[var(--th-text)] truncate">
                            {subjectName}
                          </span>
                        </div>
                        <p className="text-[11px] text-[var(--th-text-muted)]">
                          Độ khó: <strong className="text-[var(--th-text)]">{q.difficulty}/5</strong>
                        </p>
                      </div>
                    </div>
                    <TeacherStatusBadge status={q.status} />
                  </div>

                  {/* Question text with math formulas */}
                  <div className="text-sm font-semibold text-[var(--th-text)] leading-relaxed pt-1">
                    <RichMathText text={q.questionText} />
                  </div>

                  {/* Options & Answers preview for all question types */}
                  {q.questionType === "MultipleChoice" && q.options && q.options.length > 0 && (
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 pt-2 border-t border-[var(--th-border-subtle)] text-xs">
                      {q.options.map((opt, idx) => {
                        const optLabel = opt.label || opt.optionLabel || String.fromCharCode(65 + idx);
                        const optText = opt.text || opt.optionText || "";
                        const isCorrect = Boolean(
                          opt.isCorrect ||
                            (q.correctAnswer && optLabel.trim().toUpperCase() === q.correctAnswer.trim().toUpperCase())
                        );

                        return (
                          <div
                            key={opt.optionId ?? `${optLabel}-${idx}`}
                            className={`p-2.5 rounded-lg border text-xs flex items-start gap-2 transition-colors ${
                              isCorrect
                                ? "border-emerald-500/50 bg-emerald-500/10 text-emerald-300 font-medium ring-1 ring-emerald-500/20"
                                : "border-[var(--th-border-subtle)] bg-[var(--th-surface-muted)]/60 text-[var(--th-text)]"
                            }`}
                          >
                            <span
                              className={`shrink-0 font-bold px-1.5 py-0.5 rounded text-[11px] ${
                                isCorrect
                                  ? "bg-emerald-500/20 text-emerald-300"
                                  : "bg-[var(--th-surface)] border border-[var(--th-border-subtle)] text-[var(--th-text-muted)]"
                              }`}
                            >
                              {optLabel}.
                            </span>
                            <span className="min-w-0 flex-1 leading-relaxed">
                              <RichMathText text={optText} />
                            </span>
                            {isCorrect && (
                              <span className="shrink-0 text-[10px] font-bold text-emerald-400 uppercase tracking-wider">
                                ✓ Đúng
                              </span>
                            )}
                          </div>
                        );
                      })}
                    </div>
                  )}

                  {/* Short Answer Display */}
                  {q.questionType === "ShortAnswer" && (
                    <div className="pt-2 border-t border-[var(--th-border-subtle)]">
                      <div className="p-2.5 rounded-lg border border-emerald-500/40 bg-emerald-500/10 text-xs flex items-start gap-2">
                        <span className="shrink-0 font-bold text-[11px] uppercase tracking-wider text-emerald-400 bg-emerald-500/20 px-2 py-0.5 rounded">
                          Đáp án đúng:
                        </span>
                        <div className="flex-1 font-semibold text-xs text-[var(--th-text)] leading-relaxed pt-0.5">
                          <RichMathText text={q.correctAnswer || "Chưa thiết lập đáp án"} />
                        </div>
                      </div>
                    </div>
                  )}

                  {/* Essay Display */}
                  {q.questionType === "Essay" && (
                    <div className="pt-2 border-t border-[var(--th-border-subtle)]">
                      <div className="p-2.5 rounded-lg border border-purple-500/40 bg-purple-500/10 text-xs flex items-start gap-2">
                        <span className="shrink-0 font-bold text-[11px] uppercase tracking-wider text-purple-300 bg-purple-500/20 px-2 py-0.5 rounded">
                          Đáp án mẫu / Thang điểm:
                        </span>
                        <div className="flex-1 font-medium text-xs text-[var(--th-text)] leading-relaxed pt-0.5">
                          <RichMathText text={q.correctAnswer || "Chấm tự luận theo rubric"} />
                        </div>
                      </div>
                    </div>
                  )}

                  {/* Optional Solution Preview */}
                  {q.solution && (
                    <details className="group rounded-lg border border-[var(--th-border-subtle)] bg-[var(--th-surface-subtle)] p-2.5 text-xs transition-colors">
                      <summary className="cursor-pointer font-semibold text-[11px] uppercase tracking-wider text-[var(--th-teal)] flex items-center justify-between select-none">
                        <span>💡 Xem lời giải chi tiết (Solution)</span>
                        <span className="text-[10px] text-[var(--th-text-muted)] group-open:rotate-180 transition-transform">▼</span>
                      </summary>
                      <div className="mt-2 pt-2 border-t border-[var(--th-border-subtle)] font-normal text-xs text-[var(--th-text)] leading-relaxed">
                        <RichMathText text={q.solution} />
                      </div>
                    </details>
                  )}
                </div>

                {/* Bottom row: Styled Tag Pills & Action Buttons */}
                <div className="mt-4 pt-3 border-t border-[var(--th-border-subtle)] flex flex-wrap items-center justify-between gap-3">
                  {/* Tag Group with Custom Specified Color Tokens */}
                  <div className="flex flex-wrap items-center gap-1.5">
                    {/* Tag 1: Type */}
                    <span className="th-tag-accent px-2 py-0.5 rounded text-[10px] font-black tracking-wider uppercase">
                      {typeLabel}
                    </span>

                    {/* Tag 2: Difficulty */}
                    <span className="th-tag-secondary-wash px-2 py-0.5 rounded text-[10px] font-bold tracking-wider uppercase">
                      {difficultyLabel}
                    </span>

                    {/* Tag 3: Subject */}
                    <span className="th-tag-tertiary-wash px-2 py-0.5 rounded text-[10px] font-bold tracking-wider uppercase">
                      {subjectLabel}
                    </span>

                    {/* Tag 4: Extra meta */}
                    {q.options && q.options.length > 0 && (
                      <span className="th-tag-neutral px-2 py-0.5 rounded text-[10px] font-bold tracking-wider uppercase">
                        {q.options.length} ĐÁP ÁN
                      </span>
                    )}
                  </div>

                  {/* Action Buttons */}
                  <div className="flex items-center gap-2">
                    {canPublish && q.status === "Draft" && (
                      <button
                        type="button"
                        onClick={() => handleOpenDialog(q, "activate")}
                        className="th-secondary-button text-xs py-1 px-2.5 font-bold"
                      >
                        Kích hoạt
                      </button>
                    )}
                    {canPublish && q.status === "Active" && (
                      <button
                        type="button"
                        onClick={() => handleOpenDialog(q, "archive")}
                        className="th-secondary-button text-xs py-1 px-2.5 font-bold"
                      >
                        Lưu trữ
                      </button>
                    )}
                    {canDelete && q.status !== "Active" && (
                      <button
                        type="button"
                        onClick={() => handleOpenDialog(q, "delete")}
                        className="th-danger-button text-xs py-1 px-2.5 font-bold"
                      >
                        Xóa
                      </button>
                    )}

                    {canUpdate && (
                      <button
                        type="button"
                        onClick={() => navigate(`/giao-vien/cau-hoi/${q.questionId}`)}
                        className="th-secondary-button text-xs py-1 px-3 font-bold hover:bg-[var(--at-secondary-wash)]"
                      >
                        Chỉnh sửa →
                      </button>
                    )}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Pagination Styled like Reference Mockup */}
      {!isLoading && !isError && totalItems > 0 && (
        <div className="flex flex-col sm:flex-row items-center justify-between gap-3 rounded-xl border border-[var(--th-border)] bg-[var(--th-surface)] p-3 text-xs text-[var(--th-text-secondary)] shadow-sm">
          <span className="font-semibold text-[var(--th-text)]">
            Hiển thị <strong>{displayedQuestions.length}</strong> / <strong>{totalItems}</strong> câu hỏi
          </span>

          <div className="flex items-center gap-1.5">
            <button
              type="button"
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
              className="th-secondary-button text-xs py-1 px-2.5 font-bold"
            >
              ← Trước
            </button>

            {Array.from({ length: Math.min(5, totalPages) }, (_, idx) => {
              const pageNum = idx + 1;
              const isCurrentPage = page === pageNum;
              return (
                <button
                  key={pageNum}
                  type="button"
                  onClick={() => setPage(pageNum)}
                  className={`grid h-7 w-7 place-items-center rounded-md border-[1.5px] border-[var(--th-border)] text-xs font-black transition-all ${
                    isCurrentPage
                      ? "bg-[var(--at-accent)] text-slate-950 shadow-sm"
                      : "bg-[var(--th-surface)] text-[var(--th-text)] hover:bg-[var(--at-secondary-wash)]"
                  }`}
                >
                  {pageNum}
                </button>
              );
            })}

            {totalPages > 5 && (
              <>
                <span className="px-1 text-[var(--th-text-muted)] font-bold">...</span>
                <button
                  type="button"
                  onClick={() => setPage(totalPages)}
                  className={`grid h-7 w-7 place-items-center rounded-md border-[1.5px] border-[var(--th-border)] text-xs font-black transition-all ${
                    page === totalPages
                      ? "bg-[var(--at-accent)] text-slate-950 shadow-sm"
                      : "bg-[var(--th-surface)] text-[var(--th-text)] hover:bg-[var(--at-secondary-wash)]"
                  }`}
                >
                  {totalPages}
                </button>
              </>
            )}

            <button
              type="button"
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages}
              className="th-secondary-button text-xs py-1 px-2.5 font-bold"
            >
              Sau →
            </button>
          </div>
        </div>
      )}

      {/* Confirm Action Dialog */}
      <TeacherConfirmDialog
        isOpen={dialogAction !== null}
        title={
          dialogAction === "activate"
            ? "Kích hoạt câu hỏi"
            : dialogAction === "archive"
            ? "Lưu trữ câu hỏi"
            : "Xóa câu hỏi"
        }
        description={
          dialogAction === "activate"
            ? `Bạn có chắc muốn kích hoạt câu hỏi #${targetQuestion?.questionId}? Câu hỏi này sẽ khả dụng để đưa vào bài tập cho học sinh.`
            : dialogAction === "archive"
            ? `Lưu trữ câu hỏi #${targetQuestion?.questionId}? Câu hỏi sẽ không xuất hiện trong danh sách giao bài mới.`
            : `Xóa vĩnh viễn câu hỏi #${targetQuestion?.questionId}? Thao tác này không thể hoàn tác.`
        }
        confirmLabel={
          dialogAction === "activate" ? "Kích hoạt" : dialogAction === "archive" ? "Lưu trữ" : "Xóa câu hỏi"
        }
        tone={dialogAction === "delete" ? "danger" : "default"}
        isConfirming={activateMutation.isPending || archiveMutation.isPending || deleteMutation.isPending}
        onConfirm={handleConfirmAction}
        onClose={handleCloseDialog}
      />
    </div>
  );
}
