import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { organizationApi } from "../api/organizationApi";
import { useAssignment } from "../features/assignments/useAssignment";
import { useCreateAssignment } from "../features/assignments/useCreateAssignment";
import { useUpdateAssignment } from "../features/assignments/useUpdateAssignment";
import { usePublishAssignment } from "../features/assignments/usePublishAssignment";
import {
  useAssignableQuestions,
  useAssignmentClasses,
  useAssignmentClassStudents,
} from "../features/assignments/useAssignmentWizardOptions";
import type {
  CreateAssignmentRequest,
  TargetMode,
  UpdateAssignmentRequest,
} from "../types/assignments";
import type { Question } from "../types/questions";
import type { ClassDto, StudentDto } from "../types/organization";
import { useAuthStore } from "../stores/authStore";
import { permissions } from "../auth/permissions";
import {
  mapSafeOperationalError,
  extractProblemDetails,
  isConcurrencyConflictError,
} from "../utils/problemDetails";
import { CenterManagerThemeScope } from "../components/centerManager/CenterManagerThemeScope";
import {
  PageHeader,
  StatusBadge,
  Skeleton,
  ConcurrencyBanner,
  SafeErrorPanel,
} from "../components/centerManager/CenterManagerPrimitives";
import { ConfirmDialog } from "../components/centerManager/CenterManagerOverlays";
import { MathFormulaPreview } from "../components/math/MathFormulaPreview";

const toLocalDateTime = (value: string | null) => {
  if (!value) return "";
  const date = new Date(value);
  const timezoneOffset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - timezoneOffset).toISOString().slice(0, 16);
};

// =============================================================================
// 1. CENTER MANAGER DARK SAAS WIZARD VIEW (GATE 6B)
// =============================================================================

const WIZARD_STEPS = [
  { id: 0, label: "1. Thông tin chung & Lớp học" },
  { id: 1, label: "2. Chọn câu hỏi" },
  { id: 2, label: "3. Đối tượng giao bài" },
  { id: 3, label: "4. Xem lại & Xuất bản" },
];

function CenterManagerAssignmentEditorView() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const queryClient = useQueryClient();
  const isEditing = !!id;

  const hasPermission = useAuthStore((state) => state.hasPermission);
  const canReadAssignments = hasPermission(permissions.assignmentsRead);
  const canCreate = hasPermission(permissions.assignmentsCreate);
  const canUpdate = hasPermission(permissions.assignmentsUpdate);
  const canPublish = hasPermission(permissions.assignmentsPublish);
  const canReadClasses = hasPermission(permissions.classesRead);
  const canReadQuestions = hasPermission(permissions.questionsRead);

  // Assignment data query
  const assignmentQuery = useAssignment(id);
  const assignment = assignmentQuery.data?.data;

  // Mutations
  const createMutation = useCreateAssignment();
  const updateMutation = useUpdateAssignment();
  const publishMutation = usePublishAssignment();

  // Wizard Step State
  const [step, setStep] = useState(0);

  // Form State
  const [classId, setClassId] = useState(() => (isEditing ? "" : searchParams.get("classId") || ""));
  const [title, setTitle] = useState("");
  const [instructions, setInstructions] = useState("");
  const [dueAt, setDueAt] = useState("");
  const [targetMode, setTargetMode] = useState<TargetMode>("WholeClass");
  const [questionIds, setQuestionIds] = useState<string[]>([]);
  const [studentIds, setStudentIds] = useState<string[]>([]);

  // Selection Caches (preserving selected items across server-side pagination & searches)
  const [cachedQuestions, setCachedQuestions] = useState<Map<string, Question>>(new Map());
  const [cachedStudents, setCachedStudents] = useState<Map<string, StudentDto>>(new Map());

  // Operational feedback states
  const [formError, setFormError] = useState<{ message: string; traceId?: string | null } | null>(null);
  const [concurrencyConflict, setConcurrencyConflict] = useState(false);
  const [publishDialogOpen, setPublishDialogOpen] = useState(false);

  // Filter & Pagination for Question Selector (matching backend QuestionListQuery: no search param)
  const [questionPage, setQuestionPage] = useState(1);
  const [questionDifficulty, setQuestionDifficulty] = useState<number | "">("");

  // Filter & Pagination for Class Selector
  const [classPage, setClassPage] = useState(1);
  const [cachedClasses, setCachedClasses] = useState<Map<string, ClassDto>>(new Map());

  // Filter & Pagination for Student Selector (matching backend ClassStudentListQuery: has search param)
  const [studentPage, setStudentPage] = useState(1);
  const [studentSearch, setStudentSearch] = useState("");

  // In edit mode: resolve specific class by ID regardless of page
  const classDetailQuery = useQuery({
    queryKey: ["class-detail-for-assignment", classId],
    queryFn: () => organizationApi.getClass(classId),
    enabled: canReadClasses && Boolean(classId),
    staleTime: 60_000,
  });

  // Classes Query - guarded with canReadClasses
  const classesQuery = useAssignmentClasses(
    { status: "Active", page: classPage, pageSize: 20 },
    { enabled: canReadClasses }
  );

  // Accumulate loaded classes into cache
  useEffect(() => {
    if (classesQuery.data?.data) {
      setCachedClasses((prev) => {
        const next = new Map(prev);
        for (const c of classesQuery.data.data) {
          next.set(c.classId, c);
        }
        return next;
      });
    }
  }, [classesQuery.data?.data]);

  const classList = useMemo(() => {
    const map = new Map(cachedClasses);
    if (classesQuery.data?.data) {
      for (const c of classesQuery.data.data) {
        map.set(c.classId, c);
      }
    }
    if (classDetailQuery.data) {
      map.set(classDetailQuery.data.classId, classDetailQuery.data);
    }
    return Array.from(map.values());
  }, [cachedClasses, classesQuery.data?.data, classDetailQuery.data]);

  const selectedClass = useMemo(() => {
    if (classDetailQuery.data) return classDetailQuery.data;
    if (classId && cachedClasses.has(classId)) return cachedClasses.get(classId);
    return classesQuery.data?.data.find((c) => c.classId === classId);
  }, [classId, classDetailQuery.data, cachedClasses, classesQuery.data?.data]);

  // Questions Query for the subject of the selected class - guarded with canReadQuestions
  const questionsQuery = useAssignableQuestions(
    selectedClass?.subject?.subjectId
      ? {
          subjectId: selectedClass.subject.subjectId,
          page: questionPage,
          pageSize: 10,
          difficulty: questionDifficulty !== "" ? Number(questionDifficulty) : undefined,
        }
      : undefined,
    { enabled: canReadQuestions && Boolean(selectedClass?.subject?.subjectId) }
  );

  // Students Query for the selected class - guarded with canReadClasses
  const studentsQuery = useAssignmentClassStudents(
    classId
      ? {
          classId,
          page: studentPage,
          pageSize: 20,
          search: studentSearch.trim() || undefined,
          status: "Active",
        }
      : undefined,
    { enabled: canReadClasses && Boolean(classId) }
  );

  // Accumulate questions into cache
  useEffect(() => {
    if (questionsQuery.data?.data) {
      setCachedQuestions((prev) => {
        const next = new Map(prev);
        for (const q of questionsQuery.data.data) {
          next.set(q.questionId, q);
        }
        return next;
      });
    }
  }, [questionsQuery.data?.data]);

  // Accumulate students into cache
  useEffect(() => {
    if (studentsQuery.data?.data) {
      setCachedStudents((prev) => {
        const next = new Map(prev);
        for (const s of studentsQuery.data.data) {
          next.set(s.studentId, s);
        }
        return next;
      });
    }
  }, [studentsQuery.data?.data]);

  // Populate data in Edit Mode
  useEffect(() => {
    if (!assignment) return;

    setClassId(assignment.classId);
    setTitle(assignment.title);
    setInstructions(assignment.instructions || "");
    setDueAt(toLocalDateTime(assignment.dueAt));
    setQuestionIds(assignment.questions.map((q) => q.questionId));

    const source = assignment.targets[0]?.targetSource;
    setTargetMode(source === "WholeClass" || !source ? "WholeClass" : "SelectedStudents");
    setStudentIds(source && source !== "WholeClass" ? assignment.targets.map((t) => t.studentId) : []);
  }, [assignment]);

  // Strict Read-Only Rule covering all non-Draft states: Published, Closed, Archived
  const isReadOnly = isEditing && (assignment?.status !== "Draft" || !canUpdate);

  // Composite Capability Fail-Closed Guard
  // In read-only view mode: assignments.read is sufficient to inspect the assignment record.
  // In create mode or writable edit mode: require editor supporting capabilities.
  const missingCapabilities = useMemo(() => {
    const missing: string[] = [];
    if (!isEditing) {
      if (!canCreate) missing.push("assignments.assignments.create");
      if (!canReadClasses) missing.push("organization.classes.read");
      if (!canReadQuestions) missing.push("curriculum.questions.read");
    } else {
      if (!canReadAssignments) missing.push("assignments.assignments.read");
      if (!isReadOnly) {
        if (!canUpdate) missing.push("assignments.assignments.update");
        if (!canReadClasses) missing.push("organization.classes.read");
        if (!canReadQuestions) missing.push("curriculum.questions.read");
      }
    }
    return missing;
  }, [isEditing, isReadOnly, canCreate, canReadAssignments, canUpdate, canReadClasses, canReadQuestions]);

  // Toggle question selection
  const toggleQuestion = (q: Question) => {
    if (isReadOnly) return;
    setCachedQuestions((prev) => new Map(prev).set(q.questionId, q));
    setQuestionIds((prev) =>
      prev.includes(q.questionId) ? prev.filter((id) => id !== q.questionId) : [...prev, q.questionId]
    );
  };

  // Toggle student selection
  const toggleStudent = (s: StudentDto) => {
    if (isReadOnly) return;
    setCachedStudents((prev) => new Map(prev).set(s.studentId, s));
    setStudentIds((prev) =>
      prev.includes(s.studentId) ? prev.filter((id) => id !== s.studentId) : [...prev, s.studentId]
    );
  };

  const handleClassChange = (nextClassId: string) => {
    if (isReadOnly) return;
    setClassId(nextClassId);
    setQuestionIds([]);
    setStudentIds([]);
    setTargetMode("WholeClass");
    setQuestionPage(1);
    setStudentPage(1);
  };

  // Save / Update Draft Handler
  const handleSaveDraft = (afterSuccess?: () => void) => {
    if (isReadOnly) return;
    setFormError(null);
    setConcurrencyConflict(false);

    if (!title.trim()) {
      setFormError({ message: "Vui lòng nhập tiêu đề bài tập." });
      setStep(0);
      return;
    }
    if (!classId) {
      setFormError({ message: "Vui lòng chọn lớp học tiếp nhận bài tập." });
      setStep(0);
      return;
    }
    if (questionIds.length === 0) {
      setFormError({ message: "Vui lòng chọn ít nhất 1 câu hỏi cho bài tập." });
      setStep(1);
      return;
    }
    if (targetMode === "SelectedStudents" && studentIds.length === 0) {
      setFormError({ message: "Vui lòng chọn ít nhất 1 học sinh nhận bài tập ở chế độ Chọn học sinh." });
      setStep(2);
      return;
    }

    const payloadDueAt = dueAt ? new Date(dueAt).toISOString() : null;

    if (!isEditing) {
      const createPayload: CreateAssignmentRequest = {
        classId,
        title: title.trim(),
        instructions: instructions.trim() || null,
        dueAt: payloadDueAt,
        questionIds,
        targetMode,
        studentIds: targetMode === "SelectedStudents" ? studentIds : undefined,
      };

      createMutation.mutate(createPayload, {
        onSuccess: (res) => {
          navigate(`/quan-ly/bai-tap/${res.data.assignmentId}`);
        },
        onError: (err: any) => {
          const details = extractProblemDetails(err);
          setFormError({
            message: mapSafeOperationalError(err, "Không thể tạo bài tập nháp."),
            traceId: details.traceId,
          });
        },
      });
    } else {
      if (!assignment?.rowVersion) {
        setFormError({
          message: "Không thể xác định phiên bản đồng thời (RowVersion) của bài tập. Vui lòng làm mới trang để thử lại.",
        });
        assignmentQuery.refetch();
        return;
      }

      const updatePayload: UpdateAssignmentRequest = {
        title: title.trim(),
        instructions: instructions.trim() || null,
        dueAt: payloadDueAt,
        questionIds,
        targetMode,
        studentIds: targetMode === "SelectedStudents" ? studentIds : undefined,
        rowVersion: assignment.rowVersion,
      };

      updateMutation.mutate(
        { id: id!, request: updatePayload },
        {
          onSuccess: (res) => {
            queryClient.setQueryData(["assignments", id], res);
            if (afterSuccess) {
              afterSuccess();
            } else {
              navigate("/quan-ly/bai-tap");
            }
          },
          onError: (err: any) => {
            const details = extractProblemDetails(err);
            if (isConcurrencyConflictError(err)) {
              setConcurrencyConflict(true);
            } else {
              setFormError({
                message: mapSafeOperationalError(err, "Không thể cập nhật bài tập."),
                traceId: details.traceId,
              });
            }
          },
        }
      );
    }
  };

  // Publish Confirm Handler
  const handleConfirmPublish = () => {
    if (!id || !assignment || !canPublish) return;
    setFormError(null);

    publishMutation.mutate(
      { id, request: { rowVersion: assignment.rowVersion } },
      {
        onSuccess: () => {
          setPublishDialogOpen(false);
          navigate("/quan-ly/bai-tap");
        },
        onError: (err: any) => {
          const details = extractProblemDetails(err);
          setPublishDialogOpen(false);
          if (isConcurrencyConflictError(err)) {
            setConcurrencyConflict(true);
          } else {
            setFormError({
              message: mapSafeOperationalError(err, "Không thể xuất bản bài tập."),
              traceId: details.traceId,
            });
          }
        },
      }
    );
  };

  // Target Summary helpers (Strictly following rule: never display fake student count)
  const renderTargetSummaryStudentCount = () => {
    if (targetMode === "SelectedStudents") {
      return `${studentIds.length} học sinh được chọn thủ công`;
    }
    // WholeClass mode
    const totalClassStudents = studentsQuery.data?.meta?.totalItems;
    if (totalClassStudents !== undefined && totalClassStudents > 0) {
      return `${totalClassStudents} học sinh (toàn bộ lớp tại thời điểm này)`;
    }
    return "Toàn bộ học sinh đang hoạt động trong lớp tại thời điểm xuất bản";
  };

  // Fail-Closed Check
  if (missingCapabilities.length > 0) {
    return (
      <CenterManagerThemeScope data-actor="center-manager">
        <div className="p-6 lg:p-8 space-y-6">
          <PageHeader
            eyebrow="NỘI DUNG HỌC THUẬT"
            title="Soạn Thảo Bài Tập"
            breadcrumbs={[
              { label: "CenterManager", href: "/quan-ly/tong-quan-trung-tam" },
              { label: "Quản lý bài tập", href: "/quan-ly/bai-tap" },
              { label: isEditing ? "Chi tiết" : "Tạo mới" },
            ]}
          />
          <section role="alert" className="rounded-2xl border border-rose-500/30 bg-rose-500/10 p-6 space-y-3">
            <h2 className="text-base font-semibold text-rose-200">Không đủ điều kiện phân quyền (Fail-closed Guard)</h2>
            <p className="text-sm text-rose-300">
              Quy trình thiết lập bài tập yêu cầu tối thiểu các quyền sau:
            </p>
            <ul className="list-disc pl-5 text-xs font-mono text-rose-200 space-y-1">
              <li>assignments.assignments.create (hoặc update)</li>
              <li>organization.classes.read</li>
              <li>curriculum.questions.read</li>
            </ul>
            <p className="text-xs text-rose-300/80 pt-2">
              Các quyền còn thiếu: <strong className="font-mono text-rose-100">{missingCapabilities.join(", ")}</strong>. Vui lòng liên hệ Quản trị viên để được hỗ trợ.
            </p>
          </section>
        </div>
      </CenterManagerThemeScope>
    );
  }

  // Loading & Error States
  if (isEditing && assignmentQuery.isLoading) {
    return (
      <CenterManagerThemeScope data-actor="center-manager">
        <div className="space-y-6 p-6 lg:p-8">
          <Skeleton className="h-10 w-64 rounded-lg" />
          <Skeleton className="h-96 w-full rounded-2xl" />
        </div>
      </CenterManagerThemeScope>
    );
  }

  if (isEditing && (assignmentQuery.isError || !assignment)) {
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
          eyebrow="QUẢN LÝ BÀI TẬP"
          title={isEditing ? `Bài tập: ${assignment?.title || title}` : "Tạo Bài tập Mới"}
          description="Thiết lập bài tập theo wizard 4 bước: Chọn lớp học, Lọc ngân hàng câu hỏi, Phân bổ đối tượng và Kiểm tra tóm tắt mục tiêu."
          breadcrumbs={[
            { label: "CenterManager", href: "/quan-ly/tong-quan-trung-tam" },
            { label: "Quản lý bài tập", href: "/quan-ly/bai-tap" },
            { label: isEditing ? assignment?.title || "Chi tiết" : "Tạo mới" },
          ]}
          actions={
            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                onClick={() => navigate("/quan-ly/bai-tap")}
                className="cm-secondary-button text-xs"
              >
                Hủy / Quay lại
              </button>

              {/* Publish Button (Only for Draft with assignments.publish) */}
              {isEditing && assignment?.status === "Draft" && canPublish && (
                <button
                  type="button"
                  onClick={() => setPublishDialogOpen(true)}
                  className="px-3 py-1.5 rounded-lg text-xs font-semibold text-emerald-300 bg-emerald-500/10 border border-emerald-500/30 hover:bg-emerald-500/20 transition"
                >
                  Xuất bản ngay
                </button>
              )}

              {/* Save Draft Button (Disabled if Read-Only) */}
              {!isReadOnly && (
                <button
                  type="button"
                  id="btn-save-assignment"
                  onClick={() => handleSaveDraft()}
                  disabled={createMutation.isPending || updateMutation.isPending}
                  className="cm-primary-button text-xs"
                >
                  {createMutation.isPending || updateMutation.isPending
                    ? "Đang lưu…"
                    : isEditing
                    ? "Lưu bài tập"
                    : "Lưu bản nháp"}
                </button>
              )}
            </div>
          }
        />

        {/* OCC Banner */}
        {concurrencyConflict && (
          <ConcurrencyBanner
            onReload={() => {
              setConcurrencyConflict(false);
              assignmentQuery.refetch();
            }}
          />
        )}

        {/* Form Error Alert */}
        {formError && (
          <div
            role="alert"
            className="flex items-start justify-between rounded-xl border border-rose-500/30 bg-rose-500/10 p-4 text-xs font-medium text-rose-300"
          >
            <div>
              <p>{formError.message}</p>
              {formError.traceId && (
                <p className="mt-1 font-mono text-[11px] text-rose-200/80">Trace ID: {formError.traceId}</p>
              )}
            </div>
            <button
              type="button"
              onClick={() => setFormError(null)}
              className="text-xs font-semibold text-rose-400 hover:text-rose-200 ml-4 shrink-0"
            >
              Đóng
            </button>
          </div>
        )}

        {/* Read-Only Status Banner */}
        {isReadOnly && (
          <div
            role="status"
            className="flex items-center justify-between rounded-xl border border-amber-500/30 bg-amber-500/10 p-4 text-xs font-medium text-amber-200"
          >
            <div className="flex items-center gap-2">
              <span className="text-base">👁</span>
              <span>
                <strong>Chế độ chỉ xem:</strong> Bài tập đang ở trạng thái{" "}
                <strong className="text-amber-100">{assignment?.status}</strong>. Chỉ bài tập ở trạng thái Bản nháp (Draft) và có quyền{" "}
                <code className="font-mono text-amber-300">assignments.assignments.update</code> mới được phép chỉnh sửa.
              </span>
            </div>
            {assignment?.status && <StatusBadge status={assignment.status} />}
          </div>
        )}

        {/* Wizard Step Navigation */}
        <nav aria-label="Các bước soạn bài tập" className="rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] p-3 shadow-lg">
          <ol className="flex flex-wrap items-center gap-2 sm:gap-4">
            {WIZARD_STEPS.map((s) => {
              const isActive = step === s.id;
              const isPast = step > s.id;
              return (
                <li key={s.id} className="flex-1 min-w-[140px]">
                  <button
                    type="button"
                    onClick={() => setStep(s.id)}
                    className={`w-full py-2.5 px-3 rounded-xl text-left text-xs font-medium transition border ${
                      isActive
                        ? "border-[var(--cm-cyan)] bg-cyan-950/40 text-[var(--cm-cyan)] shadow-sm"
                        : isPast
                        ? "border-[var(--cm-border-subtle)] bg-[var(--cm-surface-subtle)] text-[var(--cm-text)]"
                        : "border-transparent text-[var(--cm-text-muted)] hover:text-[var(--cm-text)]"
                    }`}
                  >
                    {s.label}
                  </button>
                </li>
              );
            })}
          </ol>
        </nav>

        {/* Workspace Step Container */}
        <div className="rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] p-6 lg:p-8 space-y-6 shadow-xl">
          {/* STEP 0: Thông tin chung & Lớp học */}
          {step === 0 && (
            <div className="max-w-2xl space-y-5">
              <div>
                <label htmlFor="assignment-title-input" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                  Tiêu đề bài tập <span className="text-rose-400">*</span>
                </label>
                <input
                  id="assignment-title-input"
                  type="text"
                  disabled={isReadOnly}
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  placeholder="VD: Ôn tập Đại số Chương 1 - Phương trình bậc hai"
                  className="cm-input w-full text-sm"
                />
              </div>

              <div>
                <label htmlFor="assignment-class-select" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                  Lớp học tiếp nhận <span className="text-rose-400">*</span>
                </label>
                {isEditing ? (
                  <input
                    type="text"
                    disabled
                    value={selectedClass ? `${selectedClass.className} (${selectedClass.academicYear})` : classId}
                    className="cm-input w-full text-sm opacity-70 cursor-not-allowed bg-[var(--cm-surface-subtle)]"
                  />
                ) : (
                  <div className="space-y-1.5">
                    <select
                      id="assignment-class-select"
                      disabled={isReadOnly || classesQuery.isLoading}
                      value={classId}
                      onChange={(e) => handleClassChange(e.target.value)}
                      className="cm-select w-full text-sm"
                    >
                      <option value="">-- Chọn lớp học --</option>
                      {classList.map((c) => (
                        <option key={c.classId} value={c.classId}>
                          {c.className} ({c.academicYear}) - Môn: {c.subject?.subjectName}
                        </option>
                      ))}
                    </select>
                    {classesQuery.data?.meta?.totalPages && classesQuery.data.meta.totalPages > 1 && (
                      <div className="flex items-center justify-between text-xs text-[var(--cm-text-muted)] pt-1">
                        <span>Trang {classPage} / {classesQuery.data.meta.totalPages} ({classesQuery.data.meta.totalItems} lớp)</span>
                        <div className="flex gap-1">
                          <button
                            type="button"
                            disabled={classPage <= 1 || classesQuery.isLoading}
                            onClick={() => setClassPage((p) => Math.max(1, p - 1))}
                            className="cm-secondary-button text-xs py-0.5 px-2"
                          >
                            ← Trước
                          </button>
                          <button
                            type="button"
                            disabled={classPage >= (classesQuery.data.meta.totalPages || 1) || classesQuery.isLoading}
                            onClick={() => setClassPage((p) => Math.min(classesQuery.data!.meta!.totalPages || 1, p + 1))}
                            className="cm-secondary-button text-xs py-0.5 px-2"
                          >
                            Sau →
                          </button>
                        </div>
                      </div>
                    )}
                  </div>
                )}
                {selectedClass?.subject && (
                  <p className="mt-1 text-xs text-[var(--cm-cyan)]">
                    Môn học liên kết: <strong>{selectedClass.subject.subjectName}</strong>
                  </p>
                )}
              </div>

              <div>
                <label htmlFor="assignment-dueat-input" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                  Hạn chót nộp bài (tùy chọn)
                </label>
                <input
                  id="assignment-dueat-input"
                  type="datetime-local"
                  disabled={isReadOnly}
                  value={dueAt}
                  onChange={(e) => setDueAt(e.target.value)}
                  className="cm-input w-full text-sm"
                />
              </div>

              <div>
                <label htmlFor="assignment-instructions-area" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                  Hướng dẫn làm bài
                </label>
                <textarea
                  id="assignment-instructions-area"
                  rows={4}
                  disabled={isReadOnly}
                  value={instructions}
                  onChange={(e) => setInstructions(e.target.value)}
                  placeholder="Nhập ghi chú hướng dẫn học sinh..."
                  className="cm-input w-full text-sm font-sans"
                />
              </div>

              <div className="pt-4 flex justify-end">
                <button
                  type="button"
                  onClick={() => setStep(1)}
                  className="cm-primary-button text-xs"
                >
                  Tiếp tục: Chọn câu hỏi →
                </button>
              </div>
            </div>
          )}

          {/* STEP 1: Chọn câu hỏi (Server-side pagination & difficulty filter) */}
          {step === 1 && (
            <div className="space-y-5">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-[var(--cm-border-subtle)] pb-4">
                <div>
                  <h3 className="text-sm font-semibold uppercase tracking-wider text-[var(--cm-cyan)]">
                    Danh sách Câu hỏi Phù hợp ({selectedClass?.subject?.subjectName || "Chưa chọn lớp"})
                  </h3>
                  <p className="text-xs text-[var(--cm-text-muted)]">
                    Đã tích chọn: <strong className="text-[var(--cm-text)]">{questionIds.length}</strong> câu hỏi
                  </p>
                </div>

                {/* Difficulty Filter (pure canonical filter: no search param) */}
                <div className="flex items-center gap-3">
                  <label htmlFor="q-diff-filter" className="text-xs text-[var(--cm-text-secondary)] whitespace-nowrap">
                    Lọc độ khó:
                  </label>
                  <select
                    id="q-diff-filter"
                    value={questionDifficulty}
                    onChange={(e) => {
                      setQuestionDifficulty(e.target.value ? Number(e.target.value) : "");
                      setQuestionPage(1);
                    }}
                    className="cm-select text-xs py-1.5"
                  >
                    <option value="">Tất cả độ khó</option>
                    <option value="1">1 - Rất dễ</option>
                    <option value="2">2 - Dễ</option>
                    <option value="3">3 - Trung bình</option>
                    <option value="4">4 - Khó</option>
                    <option value="5">5 - Rất khó</option>
                  </select>
                </div>
              </div>

              {!classId ? (
                <div className="p-6 rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-subtle)] text-center text-xs text-[var(--cm-text-muted)]">
                  Vui lòng quay lại Bước 1 để chọn lớp học trước khi chọn câu hỏi.
                </div>
              ) : questionsQuery.isLoading ? (
                <div className="space-y-3">
                  {Array.from({ length: 4 }).map((_, idx) => (
                    <Skeleton key={idx} className="h-16 w-full rounded-xl" />
                  ))}
                </div>
              ) : questionsQuery.isError ? (
                <SafeErrorPanel
                  error={questionsQuery.error}
                  fallback="Không thể tải danh sách câu hỏi phù hợp."
                  onRetry={() => questionsQuery.refetch()}
                />
              ) : (questionsQuery.data?.data?.length || 0) === 0 ? (
                <div className="p-8 rounded-xl border border-dashed border-[var(--cm-border)] text-center text-xs text-[var(--cm-text-muted)]">
                  Không tìm thấy câu hỏi khả dụng cho môn học này với bộ lọc hiện tại.
                </div>
              ) : (
                <div className="space-y-3">
                  {questionsQuery.data?.data?.map((q) => {
                    const isSelected = questionIds.includes(q.questionId);
                    return (
                      <div
                        key={q.questionId}
                        onClick={() => toggleQuestion(q)}
                        className={`p-4 rounded-xl border transition flex items-start gap-4 cursor-pointer select-none ${
                          isSelected
                            ? "border-[var(--cm-cyan)] bg-cyan-950/20"
                            : "border-[var(--cm-border-subtle)] bg-[var(--cm-surface-subtle)] hover:border-[var(--cm-cyan)]/40"
                        }`}
                      >
                        <input
                          type="checkbox"
                          disabled={isReadOnly}
                          checked={isSelected}
                          onChange={() => {}}
                          className="mt-1 h-4 w-4 rounded border-[var(--cm-border)] text-[var(--cm-cyan)] focus:ring-[var(--cm-cyan)]"
                        />
                        <div className="flex-1 min-w-0 space-y-1">
                          <div className="flex items-center gap-2">
                            <span className="text-xs font-semibold px-2 py-0.5 rounded bg-[var(--cm-surface)] border border-[var(--cm-border-subtle)] text-[var(--cm-text-secondary)]">
                              {q.questionType}
                            </span>
                            <span className="text-xs text-[var(--cm-text-muted)]">Độ khó: {q.difficulty}/5</span>
                            <span className="text-xs text-[var(--cm-text-muted)]">Điểm: {q.maxScore}</span>
                            {q.answerEvaluationMode && (
                              <span className="text-[10px] font-mono text-cyan-300 px-1.5 py-0.5 rounded bg-cyan-950/50">
                                {q.answerEvaluationMode}
                              </span>
                            )}
                          </div>
                          <p className="text-sm font-medium text-[var(--cm-text)] line-clamp-2">
                            {q.questionText}
                          </p>
                          {q.questionText.includes("\\") && (
                            <div className="pt-1">
                              <MathFormulaPreview formula={q.questionText} displayMode={false} />
                            </div>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}

              {/* Pagination for Questions */}
              {(() => {
                const totalPages = questionsQuery.data?.meta?.totalPages || 1;
                if (totalPages <= 1) return null;
                return (
                  <div className="flex items-center justify-between pt-3 text-xs text-[var(--cm-text-muted)]">
                    <span>
                      Trang {questionPage} / {totalPages} ({questionsQuery.data?.meta?.totalItems ?? 0} câu hỏi)
                    </span>
                    <div className="flex items-center gap-2">
                      <button
                        type="button"
                        onClick={() => setQuestionPage((p) => Math.max(1, p - 1))}
                        disabled={questionPage <= 1}
                        className="cm-secondary-button text-xs py-1 px-3"
                      >
                        ← Trang trước
                      </button>
                      <button
                        type="button"
                        onClick={() => setQuestionPage((p) => Math.min(totalPages, p + 1))}
                        disabled={questionPage >= totalPages}
                        className="cm-secondary-button text-xs py-1 px-3"
                      >
                        Trang sau →
                      </button>
                    </div>
                  </div>
                );
              })()}

              <div className="pt-4 flex justify-between">
                <button
                  type="button"
                  onClick={() => setStep(0)}
                  className="cm-secondary-button text-xs"
                >
                  ← Quay lại
                </button>
                <button
                  type="button"
                  onClick={() => setStep(2)}
                  className="cm-primary-button text-xs"
                >
                  Tiếp tục: Chọn đối tượng →
                </button>
              </div>
            </div>
          )}

          {/* STEP 2: Đối tượng giao bài (Target Mode) */}
          {step === 2 && (
            <div className="space-y-6">
              <div>
                <h3 className="text-sm font-semibold uppercase tracking-wider text-[var(--cm-cyan)] mb-3">
                  Chọn Đối tượng Tiếp nhận Bài tập
                </h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 max-w-2xl">
                  <div
                    onClick={() => {
                      if (!isReadOnly) setTargetMode("WholeClass");
                    }}
                    className={`p-4 rounded-xl border cursor-pointer select-none transition ${
                      targetMode === "WholeClass"
                        ? "border-[var(--cm-cyan)] bg-cyan-950/20 shadow-md"
                        : "border-[var(--cm-border-subtle)] bg-[var(--cm-surface-subtle)] hover:border-[var(--cm-border)]"
                    }`}
                  >
                    <label className="flex items-center gap-3 cursor-pointer">
                      <input
                        type="radio"
                        name="targetMode"
                        disabled={isReadOnly}
                        checked={targetMode === "WholeClass"}
                        onChange={() => setTargetMode("WholeClass")}
                        className="h-4 w-4 text-[var(--cm-cyan)] focus:ring-[var(--cm-cyan)]"
                      />
                      <span className="font-semibold text-sm text-[var(--cm-text)]">Toàn bộ Lớp học (WholeClass)</span>
                    </label>
                    <p className="mt-2 text-xs text-[var(--cm-text-muted)] pl-7 leading-relaxed">
                      Tất cả học sinh đang hoạt động trong lớp tại thời điểm xuất bản sẽ được chốt nhận bài tập tự động.
                    </p>
                  </div>

                  <div
                    onClick={() => {
                      if (!isReadOnly) setTargetMode("SelectedStudents");
                    }}
                    className={`p-4 rounded-xl border cursor-pointer select-none transition ${
                      targetMode === "SelectedStudents"
                        ? "border-[var(--cm-cyan)] bg-cyan-950/20 shadow-md"
                        : "border-[var(--cm-border-subtle)] bg-[var(--cm-surface-subtle)] hover:border-[var(--cm-border)]"
                    }`}
                  >
                    <label className="flex items-center gap-3 cursor-pointer">
                      <input
                        type="radio"
                        name="targetMode"
                        disabled={isReadOnly}
                        checked={targetMode === "SelectedStudents"}
                        onChange={() => setTargetMode("SelectedStudents")}
                        className="h-4 w-4 text-[var(--cm-cyan)] focus:ring-[var(--cm-cyan)]"
                      />
                      <span className="font-semibold text-sm text-[var(--cm-text)]">Học sinh Chọn lọc (SelectedStudents)</span>
                    </label>
                    <p className="mt-2 text-xs text-[var(--cm-text-muted)] pl-7 leading-relaxed">
                      Chỉ giao bài tập cho danh sách học sinh được tích chọn thủ công bên dưới.
                    </p>
                  </div>
                </div>
              </div>

              {/* Student Selector Table (Only visible when SelectedStudents is active) */}
              {targetMode === "SelectedStudents" && (
                <div className="space-y-4 border-t border-[var(--cm-border-subtle)] pt-6">
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                    <div>
                      <h4 className="text-sm font-semibold text-[var(--cm-text)]">
                        Danh sách Thành viên Lớp học
                      </h4>
                      <p className="text-xs text-[var(--cm-text-muted)]">
                        Đã chọn: <strong className="text-[var(--cm-cyan)]">{studentIds.length}</strong> học sinh
                      </p>
                    </div>

                    {/* Server-side Search for students (matching backend ClassStudentListQuery) */}
                    <div className="w-full sm:w-64">
                      <input
                        type="text"
                        placeholder="Tìm học sinh theo tên/mã..."
                        value={studentSearch}
                        onChange={(e) => {
                          setStudentSearch(e.target.value);
                          setStudentPage(1);
                        }}
                        className="cm-input w-full text-xs"
                      />
                    </div>
                  </div>

                  {studentsQuery.isLoading ? (
                    <div className="space-y-2">
                      {Array.from({ length: 4 }).map((_, idx) => (
                        <Skeleton key={idx} className="h-10 w-full rounded-lg" />
                      ))}
                    </div>
                  ) : studentsQuery.isError ? (
                    <SafeErrorPanel
                      error={studentsQuery.error}
                      fallback="Không thể tải danh sách học sinh của lớp."
                      onRetry={() => studentsQuery.refetch()}
                    />
                  ) : (studentsQuery.data?.data?.length || 0) === 0 ? (
                    <div className="p-6 rounded-xl border border-dashed border-[var(--cm-border)] text-center text-xs text-[var(--cm-text-muted)]">
                      Không tìm thấy học sinh nào trong lớp học này.
                    </div>
                  ) : (
                    <div className="rounded-xl border border-[var(--cm-border-subtle)] overflow-hidden">
                      <table className="w-full text-left text-xs">
                        <thead className="bg-[var(--cm-surface-subtle)] border-b border-[var(--cm-border-subtle)] text-[var(--cm-text-muted)] uppercase tracking-wider">
                          <tr>
                            <th className="p-3 w-10">Chọn</th>
                            <th className="p-3">Họ và tên</th>
                            <th className="p-3">Mã học sinh</th>
                            <th className="p-3">Trạng thái</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-[var(--cm-border-subtle)]">
                          {studentsQuery.data?.data?.map((s) => {
                            const isSelected = studentIds.includes(s.studentId);
                            return (
                              <tr
                                key={s.studentId}
                                onClick={() => toggleStudent(s)}
                                className={`cursor-pointer hover:bg-white/5 transition ${
                                  isSelected ? "bg-cyan-950/20" : ""
                                }`}
                              >
                                <td className="p-3">
                                  <input
                                    type="checkbox"
                                    disabled={isReadOnly}
                                    checked={isSelected}
                                    onChange={() => {}}
                                    className="h-4 w-4 rounded border-[var(--cm-border)] text-[var(--cm-cyan)] focus:ring-[var(--cm-cyan)]"
                                  />
                                </td>
                                <td className="p-3 font-medium text-[var(--cm-text)]">{s.fullName}</td>
                                <td className="p-3 font-mono text-[var(--cm-text-muted)]">{s.username || s.studentId}</td>
                                <td className="p-3">
                                  <StatusBadge status={s.status} />
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                  )}

                  {/* Pagination for Students */}
                  {(() => {
                    const totalPages = studentsQuery.data?.meta?.totalPages || 1;
                    if (totalPages <= 1) return null;
                    return (
                      <div className="flex items-center justify-between pt-2 text-xs text-[var(--cm-text-muted)]">
                        <span>
                          Trang {studentPage} / {totalPages} ({studentsQuery.data?.meta?.totalItems ?? 0} học sinh)
                        </span>
                        <div className="flex items-center gap-2">
                          <button
                            type="button"
                            onClick={() => setStudentPage((p) => Math.max(1, p - 1))}
                            disabled={studentPage <= 1}
                            className="cm-secondary-button text-xs py-1 px-3"
                          >
                            ← Trang trước
                          </button>
                          <button
                            type="button"
                            onClick={() => setStudentPage((p) => Math.min(totalPages, p + 1))}
                            disabled={studentPage >= totalPages}
                            className="cm-secondary-button text-xs py-1 px-3"
                          >
                            Trang sau →
                          </button>
                        </div>
                      </div>
                    );
                  })()}
                </div>
              )}

              <div className="pt-4 flex justify-between">
                <button
                  type="button"
                  onClick={() => setStep(1)}
                  className="cm-secondary-button text-xs"
                >
                  ← Quay lại
                </button>
                <button
                  type="button"
                  onClick={() => setStep(3)}
                  className="cm-primary-button text-xs"
                >
                  Tiếp tục: Xem lại & Tóm tắt →
                </button>
              </div>
            </div>
          )}

          {/* STEP 3: Xem lại & Tóm tắt mục tiêu (Target Summary) */}
          {step === 3 && (
            <div className="space-y-6 max-w-3xl">
              <div>
                <h3 className="text-sm font-semibold uppercase tracking-wider text-[var(--cm-cyan)] mb-3">
                  Tóm tắt Cấu hình Bài tập (Target Summary)
                </h3>
                <div className="rounded-2xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-subtle)] p-5 space-y-4">
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-xs">
                    <div>
                      <span className="text-[var(--cm-text-muted)]">Tiêu đề bài tập:</span>
                      <p className="text-sm font-semibold text-[var(--cm-text)] mt-0.5">{title || "(Chưa có)"}</p>
                    </div>
                    <div>
                      <span className="text-[var(--cm-text-muted)]">Lớp học:</span>
                      <p className="text-sm font-semibold text-[var(--cm-text)] mt-0.5">
                        {selectedClass ? `${selectedClass.className} (${selectedClass.academicYear})` : classId || "(Chưa chọn)"}
                      </p>
                    </div>
                    <div>
                      <span className="text-[var(--cm-text-muted)]">Hạn nộp:</span>
                      <p className="text-sm font-semibold text-[var(--cm-text)] mt-0.5">
                        {dueAt ? new Date(dueAt).toLocaleString("vi-VN") : "Không giới hạn"}
                      </p>
                    </div>
                    <div>
                      <span className="text-[var(--cm-text-muted)]">Chế độ giao bài:</span>
                      <p className="text-sm font-semibold text-[var(--cm-cyan)] mt-0.5">
                        {targetMode === "WholeClass" ? "Toàn bộ lớp học (WholeClass)" : "Học sinh chọn lọc (SelectedStudents)"}
                      </p>
                    </div>
                  </div>

                  {/* Target student count (Rule: No fake count) */}
                  <div className="border-t border-[var(--cm-border-subtle)] pt-3 text-xs">
                    <span className="text-[var(--cm-text-muted)]">Số lượng học sinh nhận bài:</span>
                    <p className="text-sm font-medium text-emerald-300 mt-1">
                      {renderTargetSummaryStudentCount()}
                    </p>
                    {targetMode === "SelectedStudents" && studentIds.length > 0 && (
                      <div className="flex flex-wrap gap-1.5 mt-2">
                        {studentIds.map((sId) => (
                          <span key={sId} className="px-2 py-0.5 rounded bg-[var(--cm-surface)] border border-[var(--cm-border-subtle)] text-[11px] text-[var(--cm-text-secondary)]">
                            {cachedStudents.get(sId)?.fullName || sId}
                          </span>
                        ))}
                      </div>
                    )}
                  </div>

                  {/* Question count & total points */}
                  <div className="border-t border-[var(--cm-border-subtle)] pt-3 flex flex-wrap gap-6 text-xs">
                    <div>
                      <span className="text-[var(--cm-text-muted)]">Số câu hỏi đã chọn:</span>
                      <p className="text-lg font-bold text-[var(--cm-text)]">{questionIds.length} câu</p>
                    </div>
                    <div>
                      <span className="text-[var(--cm-text-muted)]">Tổng điểm dự tính:</span>
                      <p className="text-lg font-bold text-[var(--cm-text)]">
                        {questionIds.reduce((sum, qId) => {
                          const q = cachedQuestions.get(qId);
                          return sum + (q?.maxScore || 1);
                        }, 0)}{" "}
                        điểm
                      </p>
                    </div>
                  </div>
                </div>
              </div>

              <div className="pt-4 flex justify-between items-center">
                <button
                  type="button"
                  onClick={() => setStep(2)}
                  className="cm-secondary-button text-xs"
                >
                  ← Quay lại
                </button>
                <div className="flex items-center gap-2">
                  {!isReadOnly && (
                    <button
                      type="button"
                      onClick={() => handleSaveDraft()}
                      disabled={createMutation.isPending || updateMutation.isPending}
                      className="cm-secondary-button text-xs"
                    >
                      {createMutation.isPending || updateMutation.isPending ? "Đang lưu…" : "Lưu bản nháp"}
                    </button>
                  )}
                  {isEditing && assignment?.status === "Draft" && canPublish && (
                    <button
                      type="button"
                      onClick={() => setPublishDialogOpen(true)}
                      className="cm-primary-button text-xs !bg-emerald-600 hover:!bg-emerald-500"
                    >
                      Xuất bản bài tập ngay
                    </button>
                  )}
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Publish Confirm Dialog */}
        <ConfirmDialog
          isOpen={publishDialogOpen}
          title="Xác nhận Xuất bản Bài tập"
          description={`Khi xuất bản, hệ thống sẽ chốt danh sách học sinh và phân bổ bài tập vào tài khoản của học sinh trong lớp. Cấu hình bài tập sẽ chuyển sang chế độ Chỉ đọc (Published). Bạn có chắc chắn muốn xuất bản?`}
          confirmLabel="Xuất bản ngay"
          isConfirming={publishMutation.isPending}
          onConfirm={handleConfirmPublish}
          onClose={() => setPublishDialogOpen(false)}
        />
      </div>
    </CenterManagerThemeScope>
  );
}

// =============================================================================
// 2. LEGACY ASSIGNMENT EDITOR VIEW (FOR TEACHER & OTHER ACTORS)
// =============================================================================

function LegacyAssignmentEditorPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const isEditing = !!id;

  const assignmentQuery = useAssignment(id);
  const createMutation = useCreateAssignment();
  const updateMutation = useUpdateAssignment();

  const [classId, setClassId] = useState(() => (isEditing ? "" : searchParams.get("classId") || ""));
  const [title, setTitle] = useState("");
  const [instructions, setInstructions] = useState("");
  const [dueAt, setDueAt] = useState("");
  const initialStudentIds = isEditing
    ? []
    : (searchParams.get("studentIds") || "").split(",").map((value) => value.trim()).filter(Boolean);
  const [targetMode, setTargetMode] = useState<TargetMode>(initialStudentIds.length > 0 ? "SelectedStudents" : "WholeClass");
  const [questionIds, setQuestionIds] = useState<string[]>([]);
  const [studentIds, setStudentIds] = useState<string[]>(initialStudentIds);

  const assignment = assignmentQuery.data?.data;

  useEffect(() => {
    if (!assignment) return;

    setClassId(assignment.classId);
    setTitle(assignment.title);
    setInstructions(assignment.instructions || "");
    setDueAt(toLocalDateTime(assignment.dueAt));
    setQuestionIds(assignment.questions.map((question) => question.questionId));

    const source = assignment.targets[0]?.targetSource;
    setTargetMode(source === "WholeClass" || !source ? "WholeClass" : "SelectedStudents");
    setStudentIds(source && source !== "WholeClass" ? assignment.targets.map((target) => target.studentId) : []);
  }, [assignment]);

  const handleSave = () => {
    const payloadDueAt = dueAt ? new Date(dueAt).toISOString() : null;

    if (!isEditing) {
      createMutation.mutate(
        {
          classId,
          title: title.trim(),
          instructions: instructions.trim() || null,
          dueAt: payloadDueAt,
          questionIds,
          targetMode,
          studentIds: targetMode === "SelectedStudents" ? studentIds : undefined,
        },
        {
          onSuccess: () => navigate("/quan-ly/bai-tap"),
          onError: (err: unknown) => alert(mapSafeOperationalError(err, "Không thể tạo bài tập.")),
        }
      );
    } else {
      if (!assignment?.rowVersion) {
        alert("Không thể xác định phiên bản đồng thời (RowVersion). Vui lòng làm mới trang.");
        return;
      }
      updateMutation.mutate(
        {
          id: id!,
          request: {
            title: title.trim(),
            instructions: instructions.trim() || null,
            dueAt: payloadDueAt,
            questionIds,
            targetMode,
            studentIds: targetMode === "SelectedStudents" ? studentIds : undefined,
            rowVersion: assignment.rowVersion,
          },
        },
        {
          onSuccess: () => navigate("/quan-ly/bai-tap"),
          onError: (err: unknown) => alert(mapSafeOperationalError(err, "Không thể cập nhật bài tập.")),
        }
      );
    }
  };

  return (
    <div className="p-6 max-w-4xl mx-auto space-y-6">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold text-slate-800">
          {isEditing ? "Chỉnh sửa Bài tập" : "Tạo Bài tập Mới"}
        </h1>
        <button
          onClick={handleSave}
          className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition font-medium"
        >
          Lưu bài tập
        </button>
      </div>
      <div className="bg-white p-6 rounded-xl border border-slate-200 space-y-4">
        <div>
          <label className="block text-sm font-medium text-slate-700 mb-1">Tiêu đề bài tập</label>
          <input
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            className="w-full border border-slate-300 rounded-lg p-2.5 text-sm"
          />
        </div>
      </div>
    </div>
  );
}

// =============================================================================
// 3. MAIN ROUTE EXPORT WITH ACTOR ISOLATION
// =============================================================================

export const AssignmentEditorPage = () => {
  const accountType = useAuthStore((state) => state.user?.accountType);
  if (accountType === "CenterManager") {
    return <CenterManagerAssignmentEditorView />;
  }
  return <LegacyAssignmentEditorPage />;
};
