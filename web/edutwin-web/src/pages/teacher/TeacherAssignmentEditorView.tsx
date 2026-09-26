import { useEffect, useMemo, useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { organizationApi } from "../../api/organizationApi";
import { useAssignment } from "../../features/assignments/useAssignment";
import { useCreateAssignment } from "../../features/assignments/useCreateAssignment";
import { useUpdateAssignment } from "../../features/assignments/useUpdateAssignment";
import { usePublishAssignment } from "../../features/assignments/usePublishAssignment";
import {
  useAssignableQuestions,
  useAssignmentClasses,
  useAssignmentClassStudents,
} from "../../features/assignments/useAssignmentWizardOptions";
import type {
  CreateAssignmentRequest,
  TargetMode,
  UpdateAssignmentRequest,
} from "../../types/assignments";
import type { Question } from "../../types/questions";
import type { StudentDto } from "../../types/organization";
import { useAuthStore } from "../../stores/authStore";
import { permissions } from "../../auth/permissions";
import {
  mapSafeOperationalError,
  extractProblemDetails,
  isConcurrencyConflictError,
} from "../../utils/problemDetails";
import {
  TeacherPageHeader,
  TeacherStatusBadge,
  TeacherSkeleton,
  TeacherConcurrencyBanner,
  TeacherSafeErrorPanel,
} from "../../components/teacher/TeacherPrimitives";
import { TeacherConfirmDialog } from "../../components/teacher/TeacherOverlays";
import { TeacherAssignmentQuickViewModal } from "../../components/teacher/TeacherAssignmentQuickViewModal";
import { questionsApi } from "../../api/questionsApi";
import { RichMathText } from "../../components/math/RichMathText";

const toLocalDateTime = (value: string | null) => {
  if (!value) return "";
  const date = new Date(value);
  const timezoneOffset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - timezoneOffset).toISOString().slice(0, 16);
};

export const getMinLocalDateTime = () => {
  const d = new Date(Date.now() + 60_000);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

const WIZARD_STEPS = [
  { id: 0, label: "1. Thông tin chung & Lớp học" },
  { id: 1, label: "2. Chọn câu hỏi từ Ngân hàng" },
  { id: 2, label: "3. Đối tượng giao bài" },
  { id: 3, label: "4. Xem lại & Xuất bản" },
];

export function TeacherAssignmentEditorView() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const queryClient = useQueryClient();
  const isEditing = Boolean(id);

  const hasPermission = useAuthStore((state) => state.hasPermission);
  const canUpdate = hasPermission(permissions.assignmentsUpdate);
  const canPublish = hasPermission(permissions.assignmentsPublish);
  const canReadClasses = hasPermission(permissions.classesRead);
  const canReadQuestions = hasPermission(permissions.questionsRead);

  // Assignment query in edit mode
  const assignmentQuery = useAssignment(id);
  const assignment = assignmentQuery.data?.data;

  // Mutations
  const createMutation = useCreateAssignment();
  const updateMutation = useUpdateAssignment();
  const publishMutation = usePublishAssignment();

  // Wizard Step State
  const [step, setStep] = useState(0);

  // Gap group URL params
  const paramClassId = searchParams.get("classId") || "";
  const paramStudentIds = searchParams.get("studentIds")
    ? searchParams.get("studentIds")!.split(",").map((s) => s.trim()).filter(Boolean)
    : [];

  // Form State
  const [classId, setClassId] = useState<string>(() => (isEditing ? "" : paramClassId));
  const [title, setTitle] = useState("");
  const [instructions, setInstructions] = useState("");
  const [dueAt, setDueAt] = useState("");
  const [dueDateError, setDueDateError] = useState<string | null>(null);
  const [minDateTime, setMinDateTime] = useState(getMinLocalDateTime);

  const refreshMinDateTime = () => {
    setMinDateTime(getMinLocalDateTime());
  };
  const [targetMode, setTargetMode] = useState<TargetMode>(paramStudentIds.length > 0 ? "SelectedStudents" : "WholeClass");
  const [questionIds, setQuestionIds] = useState<string[]>([]);
  const [studentIds, setStudentIds] = useState<string[]>(() => paramStudentIds);

  // Caches for pagination preservation
  const [cachedQuestions, setCachedQuestions] = useState<Map<string, Question>>(new Map());
  const [cachedStudents, setCachedStudents] = useState<Map<string, StudentDto>>(new Map());

  // Operational feedback states
  const [formError, setFormError] = useState<{ message: string; traceId?: string | null } | null>(null);
  const [concurrencyConflict, setConcurrencyConflict] = useState(false);
  const [publishDialogOpen, setPublishDialogOpen] = useState(false);
  const [isQuickViewOpen, setIsQuickViewOpen] = useState(false);

  // Question Selector filter & page
  const [questionPage, setQuestionPage] = useState(1);
  const [questionDifficulty, setQuestionDifficulty] = useState<number | "">("");

  // Student Selector filter & page
  const [studentPage, setStudentPage] = useState(1);
  const [studentSearch, setStudentSearch] = useState("");

  // Classes query
  const { data: classesData, isLoading: isLoadingClasses } = useAssignmentClasses(
    { status: "Active", page: 1, pageSize: 50 },
    { enabled: canReadClasses }
  );

  // Specific class details query
  const classDetailQuery = useQuery({
    queryKey: ["class-detail-for-teacher-assignment", classId],
    queryFn: () => organizationApi.getClass(classId),
    enabled: canReadClasses && Boolean(classId),
    staleTime: 60_000,
  });

  const selectedClass = useMemo(() => {
    if (classDetailQuery.data) return classDetailQuery.data;
    return classesData?.data?.find((c) => c.classId === classId);
  }, [classId, classDetailQuery.data, classesData?.data]);

  // Questions query for the subject of the selected class
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

  // Students query for selected class
  const studentsQuery = useAssignmentClassStudents(
    classId
      ? {
          classId,
          page: studentPage,
          pageSize: 100,
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

  // Pre-fetch details for questions already belonging to this assignment
  const assignedQuestionIds = useMemo(() => {
    return assignment?.questions?.map((q) => q.questionId) || [];
  }, [assignment?.questions]);

  const existingQuestionsQuery = useQuery({
    queryKey: ["editor-assigned-questions-details", assignedQuestionIds],
    queryFn: async () => {
      if (assignedQuestionIds.length === 0) return [];
      const results = await Promise.allSettled(
        assignedQuestionIds.map((qid) => questionsApi.getById(qid))
      );
      const list: Question[] = [];
      results.forEach((r) => {
        if (r.status === "fulfilled" && r.value?.data) {
          list.push(r.value.data);
        }
      });
      return list;
    },
    enabled: assignedQuestionIds.length > 0,
    staleTime: 120_000,
  });

  useEffect(() => {
    if (existingQuestionsQuery.data && existingQuestionsQuery.data.length > 0) {
      setCachedQuestions((prev) => {
        const next = new Map(prev);
        for (const q of existingQuestionsQuery.data) {
          next.set(q.questionId, q);
        }
        return next;
      });
    }
  }, [existingQuestionsQuery.data]);

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
    const localDue = toLocalDateTime(assignment.dueAt);
    setDueAt(localDue);
    if (localDue && new Date(localDue).getTime() <= Date.now()) {
      setDueDateError("Hạn chót nộp bài của bản nháp này đã qua thời điểm hiện tại. Vui lòng chọn thời gian mới trong tươnglai hoặc xóa hạn chót.");
    } else {
      setDueDateError(null);
    }
    setQuestionIds(assignment.questions.map((q) => q.questionId));

    const source = assignment.targets[0]?.targetSource;
    setTargetMode(source === "WholeClass" || !source ? "WholeClass" : "SelectedStudents");
    setStudentIds(source && source !== "WholeClass" ? assignment.targets.map((t) => t.studentId) : []);
  }, [assignment]);

  const isReadOnly = isEditing && (assignment?.status !== "Draft" || !canUpdate);

  const toggleQuestion = (q: Question) => {
    if (isReadOnly) return;
    setCachedQuestions((prev) => new Map(prev).set(q.questionId, q));
    setQuestionIds((prev) =>
      prev.includes(q.questionId) ? prev.filter((i) => i !== q.questionId) : [...prev, q.questionId]
    );
  };

  const toggleStudent = (s: StudentDto) => {
    if (isReadOnly) return;
    setCachedStudents((prev) => new Map(prev).set(s.studentId, s));
    setStudentIds((prev) =>
      prev.includes(s.studentId) ? prev.filter((i) => i !== s.studentId) : [...prev, s.studentId]
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

  const handleDueAtChange = (val: string) => {
    setDueAt(val);
    if (!val) {
      setDueDateError(null);
      if (formError?.message.includes("Hạn chót nộp bài")) {
        setFormError(null);
      }
      return;
    }
    const dueTime = new Date(val).getTime();
    if (isNaN(dueTime)) {
      setDueDateError("Thời gian hạn chót nộp bài không hợp lệ.");
    } else if (dueTime <= Date.now()) {
      setDueDateError("Hạn chót nộp bài phải ở thời điểm tương lai (sau thời điểm hiện tại).");
    } else {
      setDueDateError(null);
      if (formError?.message.includes("Hạn chót nộp bài")) {
        setFormError(null);
      }
    }
  };

  const validateStep0 = (): boolean => {
    if (!title.trim()) {
      setFormError({ message: "Vui lòng nhập tiêu đề bài tập." });
      setStep(0);
      return false;
    }
    if (title.trim().length > 200) {
      setFormError({ message: "Tiêu đề bài tập không được vượt quá 200 ký tự." });
      setStep(0);
      return false;
    }
    if (!classId) {
      setFormError({ message: "Vui lòng chọn lớp học tiếp nhận bài tập." });
      setStep(0);
      return false;
    }
    if (dueAt) {
      const dueTime = new Date(dueAt).getTime();
      if (isNaN(dueTime)) {
        setFormError({ message: "Thời gian hạn chót nộp bài không hợp lệ." });
        setDueDateError("Thời gian hạn chót nộp bài không hợp lệ.");
        setStep(0);
        return false;
      }
      if (dueTime <= Date.now()) {
        setFormError({ message: "Hạn chót nộp bài phải ở thời điểm tương lai (sau thời điểm hiện tại)." });
        setDueDateError("Hạn chót nộp bài phải ở thời điểm tương lai (sau thời điểm hiện tại).");
        setStep(0);
        return false;
      }
    }
    setDueDateError(null);
    setFormError(null);
    return true;
  };

  const validateStep1 = (): boolean => {
    if (!validateStep0()) return false;
    if (questionIds.length === 0) {
      setFormError({ message: "Vui lòng chọn ít nhất 1 câu hỏi cho bài tập." });
      return false;
    }
    setFormError(null);
    return true;
  };

  const validateStep2 = (): boolean => {
    if (!validateStep1()) return false;
    if (targetMode === "SelectedStudents" && studentIds.length === 0) {
      setFormError({ message: "Vui lòng chọn ít nhất 1 học sinh nhận bài tập ở chế độ Chỉ định nhóm học sinh." });
      return false;
    }
    setFormError(null);
    return true;
  };

  const handleStepChange = (targetStep: number) => {
    if (targetStep <= step) {
      setStep(targetStep);
      setFormError(null);
      return;
    }
    if (targetStep === 1) {
      if (validateStep0()) setStep(1);
    } else if (targetStep === 2) {
      if (validateStep1()) setStep(2);
    } else if (targetStep === 3) {
      if (validateStep2()) setStep(3);
    }
  };

  const handleSaveDraft = (afterSuccess?: (resId?: string) => void) => {
    if (isReadOnly) return;
    setFormError(null);
    setConcurrencyConflict(false);

    if (!validateStep2()) {
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
          if (afterSuccess) {
            afterSuccess(res.data.assignmentId);
          } else {
            navigate("/giao-vien/bai-tap");
          }
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
        setFormError({ message: "Không xác định được phiên bản đồng thời (RowVersion). Vui lòng tải lại trang." });
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
              afterSuccess(id);
            } else {
              navigate("/giao-vien/bai-tap");
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

  const handleConfirmPublish = () => {
    if (!id || !assignment || !canPublish) return;
    setFormError(null);

    publishMutation.mutate(
      { id, request: { rowVersion: assignment.rowVersion } },
      {
        onSuccess: () => {
          setPublishDialogOpen(false);
          navigate("/giao-vien/bai-tap");
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

  const isPending = createMutation.isPending || updateMutation.isPending || publishMutation.isPending;

  return (
    <div className="space-y-6 max-w-5xl mx-auto">
      <TeacherPageHeader
        eyebrow="GIAO BÀI & ĐÁNH GIÁ"
        title={isEditing ? `Chỉnh Sửa Bài Tập: ${assignment?.title || ""}` : "Soạn Thảo & Giao Bài Tập Mới"}
        description="Quy trình 4 bước chuẩn hóa: chọn lớp, tải câu hỏi từ ngân hàng, phân công đối tượng và xuất bản."
        breadcrumbs={[
          { label: "Giáo viên", href: "/giao-vien/lop-hoc" },
          { label: "Quản lý bài tập", href: "/giao-vien/bai-tap" },
          { label: isEditing ? "Chỉnh sửa" : "Tạo mới" },
        ]}
        actions={
          <div className="flex items-center gap-2">
            {isEditing && (
              <button
                type="button"
                onClick={() => setIsQuickViewOpen(true)}
                className="th-secondary-button text-xs py-1.5 px-3 flex items-center gap-1.5"
                title="Xem lại chi tiết câu hỏi và danh sách học sinh đã phân công"
              >
                <span>👁️ Xem lại câu hỏi & học sinh</span>
              </button>
            )}
            {assignment?.status && (
              <>
                <TeacherStatusBadge status={assignment.status} />
                {isReadOnly && (
                  <span className="text-xs text-[var(--th-text-muted)] italic">
                    (Đã khóa cấu hình)
                  </span>
                )}
              </>
            )}
          </div>
        }
      />

      {concurrencyConflict && (
        <TeacherConcurrencyBanner
          onReload={() => {
            setConcurrencyConflict(false);
            assignmentQuery.refetch();
          }}
        />
      )}

      {formError && (
        <TeacherSafeErrorPanel
          error={formError.message}
          title="Không thể lưu cấu hình bài tập"
        />
      )}

      {/* Wizard Step Progress Bar */}
      <nav aria-label="Các bước thiết lập bài tập" className="th-surface p-2.5 sm:p-3">
        <ol className="grid grid-cols-2 sm:grid-cols-4 gap-2 sm:gap-3">
          {WIZARD_STEPS.map((s) => {
            const isCurrent = step === s.id;
            const isDone = step > s.id;
            return (
              <li key={s.id}>
                <button
                  type="button"
                  onClick={() => handleStepChange(s.id)}
                  className={`w-full text-left p-2.5 sm:p-3 rounded-xl transition text-xs sm:text-sm flex items-center gap-2.5 ${
                    isCurrent
                      ? "bg-[var(--at-accent-wash)] border-[1.5px] border-[var(--at-accent)] shadow-sm text-[var(--th-text-primary)] font-black"
                      : isDone
                      ? "bg-emerald-50 dark:bg-emerald-950/30 border border-emerald-500/40 text-emerald-950 dark:text-emerald-200 font-bold hover:bg-emerald-100/50"
                      : "border border-transparent text-[var(--th-text)] font-bold hover:bg-[var(--th-surface-muted)] hover:border-[var(--th-border-subtle)]"
                  }`}
                >
                  <span
                    className={`grid h-7 w-7 shrink-0 place-items-center rounded-full text-xs font-black shadow-sm ${
                      isCurrent
                        ? "bg-[var(--at-accent)] text-white"
                        : isDone
                        ? "bg-emerald-600 text-white"
                        : "border-[1.5px] border-[var(--th-border)] bg-[var(--th-surface-muted)] text-[var(--th-text)]"
                    }`}
                  >
                    {isDone ? "✓" : s.id + 1}
                  </span>
                  <span className="truncate font-black tracking-tight">{s.label}</span>
                </button>
              </li>
            );
          })}
        </ol>
      </nav>

      {/* Workspace Step Container */}
      <div className="th-surface p-6 space-y-6">
        {/* STEP 0: Thông tin chung & Lớp học */}
        {step === 0 && (
          <div className="max-w-2xl space-y-5">
            <div>
              <div className="flex justify-between items-center mb-1">
                <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)]">
                  Tiêu đề bài tập <span className="text-rose-400">*</span>
                </label>
                <span className="text-[10px] text-[var(--th-text-muted)]">{title.length}/200 ký tự</span>
              </div>
              <input
                type="text"
                disabled={isReadOnly}
                value={title}
                maxLength={200}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="VD: Ôn tập Chuyên đề Hàm số & Cực trị - Tuần 3"
                className="th-input w-full text-sm"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)] mb-1">
                Lớp học tiếp nhận bài tập <span className="text-rose-400">*</span>
              </label>
              {isEditing ? (
                <input
                  type="text"
                  disabled
                  value={selectedClass ? `${selectedClass.className} (${selectedClass.academicYear})` : classId}
                  className="th-input w-full text-sm opacity-70 cursor-not-allowed bg-[var(--th-surface-subtle)]"
                />
              ) : (
                <select
                  disabled={isReadOnly || isLoadingClasses}
                  value={classId}
                  onChange={(e) => handleClassChange(e.target.value)}
                  className="th-select w-full text-sm"
                >
                  <option value="">-- Chọn lớp học phụ trách --</option>
                  {classesData?.data?.map((c) => (
                    <option key={c.classId} value={c.classId}>
                      {c.className} ({c.academicYear}) - Môn: {c.subject?.subjectName}
                    </option>
                  ))}
                </select>
              )}
              {selectedClass?.subject && (
                <p className="mt-1.5 text-xs text-[var(--th-teal)] font-medium">
                  ✓ Môn học liên kết: <strong>{selectedClass.subject.subjectName}</strong>
                </p>
              )}
            </div>

            <div>
              <label htmlFor="teacher-assignment-dueat-input" className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)] mb-1">
                Hạn chót nộp bài (tùy chọn)
              </label>
              <input
                id="teacher-assignment-dueat-input"
                type="datetime-local"
                min={minDateTime}
                onFocus={refreshMinDateTime}
                onPointerDown={refreshMinDateTime}
                disabled={isReadOnly}
                value={dueAt}
                onChange={(e) => handleDueAtChange(e.target.value)}
                className={`th-input w-full text-sm ${dueDateError ? "!border-rose-500 focus:!border-rose-500" : ""}`}
              />
              {dueDateError ? (
                <p className="mt-1 text-[11px] text-rose-500 font-medium">
                  ⚠ {dueDateError}
                </p>
              ) : (
                <p className="mt-1 text-[11px] text-[var(--th-text-muted)]">
                  Nếu đặt hạn nộp, thời gian phải sau thời điểm hiện tại.
                </p>
              )}
            </div>

            <div>
              <div className="flex justify-between items-center mb-1">
                <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)]">
                  Hướng dẫn làm bài cho học sinh
                </label>
                <span className="text-[10px] text-[var(--th-text-muted)]">{instructions.length}/2000 ký tự</span>
              </div>
              <textarea
                rows={4}
                disabled={isReadOnly}
                maxLength={2000}
                value={instructions}
                onChange={(e) => setInstructions(e.target.value)}
                placeholder="Nhập ghi chú hoặc nhắc nhở học sinh (VD: Yêu cầu trình bày rõ từng bước giải ra giấy nháp)..."
                className="th-input w-full text-sm"
              />
            </div>

            <div className="pt-4 flex justify-end">
              <button
                type="button"
                onClick={() => handleStepChange(1)}
                className="th-primary-button text-xs py-2 px-5"
              >
                Tiếp tục: Chọn câu hỏi từ Ngân hàng →
              </button>
            </div>
          </div>
        )}

        {/* STEP 1: Chọn câu hỏi từ Ngân hàng */}
        {step === 1 && (
          <div className="space-y-5">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-[var(--th-border-subtle)] pb-4">
              <div>
                <h3 className="text-sm font-semibold uppercase tracking-wider text-[var(--th-teal)]">
                  Danh Sách Câu Hỏi Phù Hợp ({selectedClass?.subject?.subjectName || "Chưa chọn lớp"}) <span className="text-rose-400">*</span>
                </h3>
                <p className="text-xs text-[var(--th-text-secondary)]">
                  Đã chọn: <strong className="text-[var(--th-text)]">{questionIds.length}</strong> câu hỏi cho bài tập (Yêu cầu tối thiểu: 1 câu)
                </p>
              </div>

              {/* Difficulty Filter */}
              <div className="flex items-center gap-2">
                <label className="text-xs text-[var(--th-text-secondary)]">Lọc độ khó:</label>
                <select
                  value={questionDifficulty}
                  onChange={(e) => {
                    setQuestionDifficulty(e.target.value ? Number(e.target.value) : "");
                    setQuestionPage(1);
                  }}
                  className="th-select text-xs py-1"
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
              <div className="p-8 rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface-subtle)] text-center text-xs text-[var(--th-text-muted)]">
                Vui lòng quay lại Bước 1 để chọn Lớp học trước khi chọn câu hỏi.
              </div>
            ) : questionsQuery.isLoading ? (
              <div className="space-y-3">
                <TeacherSkeleton className="h-20 w-full rounded-xl" />
                <TeacherSkeleton className="h-20 w-full rounded-xl" />
                <TeacherSkeleton className="h-20 w-full rounded-xl" />
              </div>
            ) : questionsQuery.isError ? (
              <TeacherSafeErrorPanel
                error={questionsQuery.error}
                fallback="Không thể tải danh sách câu hỏi phù hợp từ ngân hàng."
                onRetry={() => questionsQuery.refetch()}
              />
            ) : (questionsQuery.data?.data?.length || 0) === 0 ? (
              <div className="p-10 rounded-xl border border-dashed border-[var(--th-border)] text-center text-xs text-[var(--th-text-muted)] space-y-2">
                <p className="text-sm font-semibold text-[var(--th-text)]">Chưa có câu hỏi Active nào cho môn học này</p>
                <p>Hãy vào mục Ngân hàng câu hỏi để soạn hoặc kích hoạt câu hỏi mới.</p>
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
                          ? "border-teal-500/60 bg-teal-500/10 dark:bg-teal-950/40"
                          : "border-[var(--th-border-subtle)] bg-[var(--th-surface-subtle)] hover:border-teal-500/40"
                      }`}
                    >
                      <input
                        type="checkbox"
                        disabled={isReadOnly}
                        checked={isSelected}
                        onChange={() => {}}
                        className="mt-1 h-4 w-4 accent-teal-500 rounded"
                      />
                      <div className="flex-1 space-y-1.5">
                        <div className="flex items-center gap-2 text-xs">
                          <span className="font-mono font-bold text-[var(--th-teal)]">#{q.questionId}</span>
                          <span className="th-badge th-badge-info py-0 px-2 text-[10px]">
                            {q.questionType === "MultipleChoice" ? "Trắc nghiệm" : "Tự luận"}
                          </span>
                          <span className="text-[var(--th-text-muted)]">Độ khó: {q.difficulty}/5</span>
                          <span className="text-[var(--th-text-muted)]">Điểm: {q.maxScore}</span>
                        </div>
                        <div className="text-xs text-[var(--th-text)] leading-relaxed font-medium">
                          <RichMathText text={q.questionText} />
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}

            {/* Pagination for questions */}
            {questionsQuery.data?.meta?.totalPages && questionsQuery.data.meta.totalPages > 1 && (
              <div className="flex items-center justify-between text-xs text-[var(--th-text-secondary)] pt-2">
                <span>
                  Trang {questionPage} / {questionsQuery.data.meta.totalPages} ({questionsQuery.data.meta.totalItems} câu hỏi)
                </span>
                <div className="flex gap-1.5">
                  <button
                    type="button"
                    disabled={questionPage <= 1 || questionsQuery.isLoading}
                    onClick={() => setQuestionPage((p) => Math.max(1, p - 1))}
                    className="th-secondary-button text-xs py-1 px-3"
                  >
                    ← Trước
                  </button>
                  <button
                    type="button"
                    disabled={questionPage >= questionsQuery.data.meta.totalPages || questionsQuery.isLoading}
                    onClick={() => setQuestionPage((p) => Math.min(questionsQuery.data!.meta!.totalPages || 1, p + 1))}
                    className="th-secondary-button text-xs py-1 px-3"
                  >
                    Sau →
                  </button>
                </div>
              </div>
            )}

            <div className="pt-4 flex justify-between">
              <button
                type="button"
                onClick={() => setStep(0)}
                className="th-secondary-button text-xs py-2 px-4"
              >
                ← Quay lại
              </button>
              <button
                type="button"
                onClick={() => handleStepChange(2)}
                className="th-primary-button text-xs py-2 px-5"
              >
                Tiếp tục: Đối tượng giao bài →
              </button>
            </div>
          </div>
        )}

        {/* STEP 2: Đối tượng giao bài */}
        {step === 2 && (
          <div className="space-y-5">
            <div className="border-b border-[var(--th-border-subtle)] pb-4">
              <h3 className="text-sm font-semibold uppercase tracking-wider text-[var(--th-teal)]">
                Phân Bổ Đối Tượng Làm Bài
              </h3>
              <p className="text-xs text-[var(--th-text-secondary)]">
                Chọn giao cho toàn bộ học sinh trong lớp hoặc chỉ định nhóm học sinh cần bổ trợ.
              </p>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <label
                className={`p-4 rounded-xl border cursor-pointer flex items-start gap-3 transition-colors ${
                  targetMode === "WholeClass"
                    ? "border-teal-500/60 bg-teal-500/10"
                    : "border-[var(--th-border-subtle)] bg-[var(--th-surface-subtle)]"
                }`}
              >
                <input
                  type="radio"
                  name="targetMode"
                  value="WholeClass"
                  disabled={isReadOnly}
                  checked={targetMode === "WholeClass"}
                  onChange={() => setTargetMode("WholeClass")}
                  className="mt-1 h-4 w-4 accent-teal-500"
                />
                <div>
                  <p className="text-sm font-semibold text-[var(--th-text)]">Toàn bộ lớp học (WholeClass)</p>
                  <p className="text-xs text-[var(--th-text-secondary)] mt-1">
                    Giao bài cho tất cả học sinh đang hoạt động trong lớp tại thời điểm xuất bản.
                  </p>
                </div>
              </label>

              <label
                className={`p-4 rounded-xl border cursor-pointer flex items-start gap-3 transition-colors ${
                  targetMode === "SelectedStudents"
                    ? "border-teal-500/60 bg-teal-500/10"
                    : "border-[var(--th-border-subtle)] bg-[var(--th-surface-subtle)]"
                }`}
              >
                <input
                  type="radio"
                  name="targetMode"
                  value="SelectedStudents"
                  disabled={isReadOnly}
                  checked={targetMode === "SelectedStudents"}
                  onChange={() => setTargetMode("SelectedStudents")}
                  className="mt-1 h-4 w-4 accent-teal-500"
                />
                <div>
                  <p className="text-sm font-semibold text-[var(--th-text)]">Chỉ định nhóm học sinh (SelectedStudents) <span className="text-rose-400">*</span></p>
                  <p className="text-xs text-[var(--th-text-secondary)] mt-1">
                    Giao bài tập riêng biệt cho nhóm học sinh hổng kiến thức (Gap Group). Yêu cầu chọn ít nhất 1 học sinh.
                  </p>
                </div>
              </label>
            </div>

            {/* Student selection checklist if SelectedStudents */}
            {targetMode === "SelectedStudents" && (
              <div className="space-y-4 pt-4 border-t border-[var(--th-border-subtle)]">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-semibold text-[var(--th-text)]">
                    Danh sách học sinh trong lớp (Đã chọn: {studentIds.length})
                  </span>
                  <input
                    type="text"
                    placeholder="Tìm tên học sinh..."
                    value={studentSearch}
                    onChange={(e) => setStudentSearch(e.target.value)}
                    className="th-input text-xs py-1 px-3 max-w-xs"
                  />
                </div>

                {studentsQuery.isLoading ? (
                  <TeacherSkeleton className="h-32 w-full rounded-xl" />
                ) : (studentsQuery.data?.data?.length || 0) === 0 ? (
                  <p className="p-6 text-center text-xs text-[var(--th-text-muted)] th-surface border border-dashed border-[var(--th-border)] rounded-xl">
                    Không tìm thấy học sinh nào trong lớp này.
                  </p>
                ) : (
                  <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3 max-h-80 overflow-y-auto p-1">
                    {studentsQuery.data?.data?.map((s) => {
                      const isSelected = studentIds.includes(s.studentId);
                      return (
                        <div
                          key={s.studentId}
                          onClick={() => toggleStudent(s)}
                          className={`p-3 rounded-xl border flex items-center gap-3 cursor-pointer select-none transition-colors ${
                            isSelected
                              ? "border-teal-500/60 bg-teal-500/10 text-teal-300 font-semibold"
                              : "border-[var(--th-border-subtle)] bg-[var(--th-surface-subtle)] text-[var(--th-text-secondary)]"
                          }`}
                        >
                          <input
                            type="checkbox"
                            disabled={isReadOnly}
                            checked={isSelected}
                            onChange={() => {}}
                            className="h-4 w-4 accent-teal-500 rounded"
                          />
                          <span className="text-xs truncate">{s.fullName}</span>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            )}

            <div className="pt-4 flex justify-between">
              <button
                type="button"
                onClick={() => setStep(1)}
                className="th-secondary-button text-xs py-2 px-4"
              >
                ← Quay lại: Chọn câu hỏi
              </button>
              <button
                type="button"
                onClick={() => handleStepChange(3)}
                className="th-primary-button text-xs py-2 px-5"
              >
                Tiếp tục: Xem lại & Xuất bản →
              </button>
            </div>
          </div>
        )}

        {/* STEP 3: Xem lại & Xuất bản */}
        {step === 3 && (
          <div className="space-y-6">
            <div className="border-b border-[var(--th-border-subtle)] pb-4">
              <h3 className="text-sm font-semibold uppercase tracking-wider text-[var(--th-teal)]">
                Xem Lại Tổng Quan Bài Tập
              </h3>
              <p className="text-xs text-[var(--th-text-secondary)]">
                Kiểm tra thông tin trước khi lưu nháp hoặc xuất bản bài tập cho học sinh.
              </p>
            </div>

            <div className="rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface-subtle)] p-5 space-y-4 text-xs">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <span className="text-[var(--th-text-muted)] uppercase tracking-wider text-[10px] font-semibold block mb-1">
                    Tiêu đề bài tập:
                  </span>
                  <p className="text-sm font-bold text-[var(--th-text)]">{title}</p>
                </div>
                <div>
                  <span className="text-[var(--th-text-muted)] uppercase tracking-wider text-[10px] font-semibold block mb-1">
                    Lớp học & Môn học:
                  </span>
                  <p className="font-semibold text-[var(--th-text)]">
                    {selectedClass ? `${selectedClass.className} (${selectedClass.academicYear}) - Môn: ${selectedClass.subject?.subjectName}` : classId}
                  </p>
                </div>
                <div>
                  <span className="text-[var(--th-text-muted)] uppercase tracking-wider text-[10px] font-semibold block mb-1">
                    Số lượng câu hỏi chọn từ Ngân hàng:
                  </span>
                  <p className="font-semibold text-teal-400 text-sm">
                    {questionIds.length} câu hỏi
                  </p>
                </div>
                <div>
                  <span className="text-[var(--th-text-muted)] uppercase tracking-wider text-[10px] font-semibold block mb-1">
                    Đối tượng giao bài:
                  </span>
                  <p className="font-semibold text-[var(--th-text)]">
                    {targetMode === "WholeClass" ? "Toàn bộ học sinh trong lớp" : `${studentIds.length} học sinh được chọn`}
                  </p>
                </div>
                {dueAt && (
                  <div>
                    <span className="text-[var(--th-text-muted)] uppercase tracking-wider text-[10px] font-semibold block mb-1">
                      Hạn chót nộp bài:
                    </span>
                    <p className="font-semibold text-[var(--th-text)]">{new Date(dueAt).toLocaleString("vi-VN")}</p>
                  </div>
                )}
              </div>

              {instructions && (
                <div className="pt-3 border-t border-[var(--th-border-subtle)]">
                  <span className="text-[var(--th-text-muted)] uppercase tracking-wider text-[10px] font-semibold block mb-1">
                    Hướng dẫn làm bài:
                  </span>
                  <p className="text-[var(--th-text-secondary)] italic">{instructions}</p>
                </div>
              )}

              {/* Selected Questions Preview */}
              <div className="pt-3 border-t border-[var(--th-border-subtle)]">
                <span className="text-[var(--th-text-muted)] uppercase tracking-wider text-[10px] font-semibold block mb-2">
                  Danh sách câu hỏi đã chọn ({questionIds.length}):
                </span>
                <div className="space-y-2 max-h-48 overflow-y-auto">
                  {questionIds.map((qid, idx) => {
                    const qObj = cachedQuestions.get(qid);
                    return (
                      <div key={qid} className="p-2 rounded-lg bg-[var(--th-surface-ground)] border border-[var(--th-border-subtle)] text-xs flex items-center justify-between">
                        <span className="font-semibold text-[var(--th-text)]">
                          {idx + 1}. {qObj?.questionText ? (qObj.questionText.length > 60 ? qObj.questionText.slice(0, 58) + "..." : qObj.questionText) : `Câu hỏi #${qid}`}
                        </span>
                        <span className="text-[10px] font-mono text-[var(--th-text-muted)]">
                          {qObj?.maxScore ?? 10}đ • {qObj?.questionType ?? "Trắc nghiệm"}
                        </span>
                      </div>
                    );
                  })}
                </div>
              </div>

              {/* Target Students */}
              <div className="pt-3 border-t border-[var(--th-border-subtle)]">
                <span className="text-[var(--th-text-muted)] uppercase tracking-wider text-[10px] font-semibold block mb-2">
                  {targetMode === "WholeClass"
                    ? `Học sinh nhận bài (Toàn bộ lớp: ${studentsQuery.data?.data?.length || 0} học sinh):`
                    : `Học sinh nhận bài (${studentIds.length} học sinh được chọn):`}
                </span>
                <div className="flex flex-wrap gap-1.5 max-h-36 overflow-y-auto">
                  {targetMode === "WholeClass" ? (
                    studentsQuery.data?.data && studentsQuery.data.data.length > 0 ? (
                      studentsQuery.data.data.map((s) => (
                        <span key={s.studentId} className="th-badge th-badge-neutral text-[10px]">
                          {s.fullName} (@{s.username})
                        </span>
                      ))
                    ) : (
                      <span className="text-xs text-[var(--th-text-muted)] italic">
                        Đang tải danh sách học sinh của lớp...
                      </span>
                    )
                  ) : studentIds.length > 0 ? (
                    studentIds.map((sid) => {
                      const sObj = cachedStudents.get(sid);
                      return (
                        <span key={sid} className="th-badge th-badge-neutral text-[10px]">
                          {sObj?.fullName || `Học sinh #${sid.slice(0, 6)}`}
                        </span>
                      );
                    })
                  ) : (
                    <span className="text-xs text-rose-400 italic">
                      Chưa chọn học sinh nào
                    </span>
                  )}
                </div>
              </div>
            </div>

            <div className="pt-4 flex items-center justify-between border-t border-[var(--th-border-subtle)]">
              <button
                type="button"
                onClick={() => setStep(2)}
                className="th-secondary-button text-xs py-2 px-4"
              >
                ← Quay lại: Đối tượng
              </button>

              <div className="flex gap-3">
                {!isReadOnly && (
                  <button
                    type="button"
                    onClick={() => handleSaveDraft()}
                    disabled={isPending}
                    className="th-secondary-button text-xs py-2 px-5"
                  >
                    {isPending ? "Đang lưu..." : isEditing ? "Lưu thay đổi" : "Lưu Bản Nháp (Draft)"}
                  </button>
                )}

                {canPublish && (
                  <button
                    type="button"
                    onClick={() => {
                      if (!isEditing) {
                        handleSaveDraft((createdId) => {
                          if (createdId) {
                            navigate(`/giao-vien/bai-tap`);
                          }
                        });
                      } else {
                        setPublishDialogOpen(true);
                      }
                    }}
                    disabled={isPending}
                    className="th-primary-button text-xs py-2 px-6 shadow-md shadow-teal-500/20"
                  >
                    {isEditing ? "Xuất Bản Bài Tập Ngay" : "Lưu & Xuất Bản"}
                  </button>
                )}
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Quick View Modal to review questions and assigned students */}
      <TeacherAssignmentQuickViewModal
        assignmentId={id || null}
        isOpen={isQuickViewOpen}
        onClose={() => setIsQuickViewOpen(false)}
      />

      {/* Confirm Publish Dialog */}
      <TeacherConfirmDialog
        isOpen={publishDialogOpen}
        title="Xác nhận xuất bản bài tập"
        description={`Xuất bản bài tập "${title}"? Hệ thống sẽ tạo bài tập và học sinh trong lớp sẽ nhìn thấy bài tập ngay lập tức.`}
        confirmLabel="Xuất bản ngay"
        isConfirming={publishMutation.isPending}
        onConfirm={handleConfirmPublish}
        onClose={() => setPublishDialogOpen(false)}
      />
    </div>
  );
}
