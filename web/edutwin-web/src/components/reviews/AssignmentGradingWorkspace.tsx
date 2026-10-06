import React, { useState, useMemo, useEffect, useRef, useCallback } from "react";
import { useSearchParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  listTeacherReviewQueue,
  overrideReasoningAnalysis,
  approveReasoningAnalysis,
  approveAssignmentResult,
  reopenAssignmentResult,
  voidAssignmentQuestion,
} from "../../api/teacherReviewsApi";
import { getAssignments, getAssignmentProgress } from "../../api/assignmentsApi";
import { organizationApi } from "../../api/organizationApi";
import type {
  TeacherReviewQueueItemDto,
  ErrorType,
  TeacherOverrideRequest,
} from "../../types/reviews";
import type { AssignmentDto, AssignmentProgressItemDto } from "../../types/assignments";
import type { ClassDto } from "../../types/organization";
import { useAuthStore } from "../../stores/authStore";
import { permissions } from "../../auth/permissions";
import { RichMathText } from "../math/RichMathText";
import { ScratchpadAttachmentDrawer } from "../ScratchpadAttachmentDrawer";
import { extractProblemDetails } from "../../utils/problemDetails";
import { resolveQuestionDefaultFormValues, questionGradingActions, gradingFormIsDirty, finalApprovalBlockReason } from "../../utils/gradingWorkspaceHelpers";
import { hydrateRubricForm, buildRubricScores, type RubricForm } from "../../utils/rubric";
import { RubricGradeView } from "./RubricGradeView";
import { getAnalysisFeedbackLabel, normalizeQuestionScore, questionAssignmentContribution, toInternalQuestionScore } from "../../utils/attemptFeedbackPresentation";
import { normalizeAITextLineBreaks } from "../../utils/aiTextFormatting";

export interface AssignmentGradingWorkspaceProps {
  actor: "Teacher" | "CenterManager";
}

type DateFilterPreset = "all" | "today" | "7days" | "30days" | "custom";

const formatConfidence = (confidence?: number | string | null, isFallback?: boolean) => {
  if (isFallback) return "Không có phân tích AI";
  if (confidence !== null && confidence !== undefined && confidence !== "") {
    const num = Number(confidence);
    if (!isNaN(num)) {
      // If 0 < num <= 1 (e.g. 0.85 or 1.0), multiply by 100; otherwise num is already on 0..100 scale (e.g. 85 or 100)
      const pct = num > 0 && num <= 1 ? Math.round(num * 100) : Math.round(num);
      return `${pct}%`;
    }
  }
  return "N/A";
};

export const AssignmentGradingWorkspace: React.FC<AssignmentGradingWorkspaceProps> = ({ actor }) => {
  const [searchParams, setSearchParams] = useSearchParams();
  const queryClient = useQueryClient();

  const hasPermission = useAuthStore((state) => state.hasPermission);
  const user = useAuthStore((state) => state.user);
  const actorScope = `${user?.centerId ?? ""}:${user?.userId ?? ""}`;

  // Strictly enforce: ONLY Teacher with override permission can grade.
  // CenterManager NEVER has grading/override capabilities.
  const isManager = actor === "CenterManager";
  const canGrade = !isManager && hasPermission(permissions.twinReasoningOverride);

  // Search & Navigation params from URL
  const selectedClassId = searchParams.get("classId") || "";
  const selectedAssignmentId = searchParams.get("assignmentId") || "";
  const selectedStudentId = searchParams.get("studentId") || "";

  // Filter States
  const [dateFilter, setDateFilter] = useState<DateFilterPreset>("all");
  const [customDate, setCustomDate] = useState<string>("");
  const [searchQuery, setSearchQuery] = useState<string>("");
  const [studentStatusFilter, setStudentStatusFilter] = useState<"all" | "pending" | "approved">("all");

  // Detailed Workspace State
  const [activeQuestionSelection, setActiveQuestionSelection] = useState<{ scope: string; attemptId: string } | null>(null);
  const workspaceScope = `${actorScope}|${selectedAssignmentId}|${selectedStudentId}`;

  // Scratchpad Drawer State
  const [isScratchpadOpen, setIsScratchpadOpen] = useState(false);
  const [scratchpadAttemptId, setScratchpadAttemptId] = useState<string | null>(null);

  // Void Question Modal State (Teacher Only)
  const [isVoidModalOpen, setIsVoidModalOpen] = useState(false);
  const [voidReason, setVoidReason] = useState("Đề bài có sai sót kỹ thuật / thiếu dữ kiện.");
  const [quarantineInBank, setQuarantineInBank] = useState(true);
  const [voidImpactAcknowledged, setVoidImpactAcknowledged] = useState(false);
  const [isReopenModalOpen, setIsReopenModalOpen] = useState(false);
  const [reopenReason, setReopenReason] = useState("");

  // Final Review Modal State (Teacher Only)
  const [isFinalApproveModalOpen, setIsFinalApproveModalOpen] = useState(false);
  const [finalApproveNote, setFinalApproveNote] = useState("Đã hoàn tất kiểm tra và chấm điểm bài làm.");

  // Toast / Notification
  const [toast, setToast] = useState<{ message: string; type: "success" | "error" | "info" } | null>(null);

  const showToast = (message: string, type: "success" | "error" | "info" = "info") => {
    setToast({ message, type });
    setTimeout(() => {
      setToast(null);
    }, 4500);
  };

  // 1. Load Classes List
  const { data: classesData } = useQuery({
    queryKey: ["gradingClassesList", actorScope],
    queryFn: () => organizationApi.listClasses({ page: 1, pageSize: 100 }),
  });

  const classesList: ClassDto[] = useMemo(() => classesData?.data ?? [], [classesData]);

  // 2. Load Assignments List
  const { data: assignmentsData, isLoading: isLoadingAssignments } = useQuery({
    queryKey: ["gradingAssignmentsList", actorScope, selectedClassId],
    queryFn: () => getAssignments({ classId: selectedClassId || undefined, page: 1, pageSize: 100 }),
  });

  const allAssignments: AssignmentDto[] = useMemo(() => assignmentsData?.data ?? [], [assignmentsData]);

  // 3. Load general review queue stats for badge counters
  const { data: globalQueueData } = useQuery({
    queryKey: ["gradingGlobalQueue", actorScope, selectedClassId],
    queryFn: () => listTeacherReviewQueue({ classId: selectedClassId || undefined, pageSize: 100 }),
  });

  // Calculate pending items per assignment
  const pendingCountByAssignment = useMemo(() => {
    const map = new Map<string, number>();
    if (globalQueueData?.data) {
      for (const item of globalQueueData.data) {
        if (item.assignmentId) {
          map.set(item.assignmentId, (map.get(item.assignmentId) || 0) + 1);
        }
      }
    }
    return map;
  }, [globalQueueData]);

  // Filter assignments by Date Preset and Search Query
  const filteredAssignments = useMemo(() => {
    const now = new Date();
    return allAssignments.filter((a) => {
      // Date filter based on created date or due date
      const dateToCheck = a.dueAt ? new Date(a.dueAt) : null;

      if (dateFilter === "today") {
        if (!dateToCheck) return false;
        const isSameDay =
          dateToCheck.getDate() === now.getDate() &&
          dateToCheck.getMonth() === now.getMonth() &&
          dateToCheck.getFullYear() === now.getFullYear();
        if (!isSameDay) return false;
      } else if (dateFilter === "7days") {
        if (!dateToCheck) return false;
        const diffDays = (now.getTime() - dateToCheck.getTime()) / (1000 * 3600 * 24);
        if (diffDays > 7 || diffDays < -7) return false;
      } else if (dateFilter === "30days") {
        if (!dateToCheck) return false;
        const diffDays = (now.getTime() - dateToCheck.getTime()) / (1000 * 3600 * 24);
        if (diffDays > 30 || diffDays < -30) return false;
      } else if (dateFilter === "custom" && customDate) {
        if (!dateToCheck) return false;
        const custom = new Date(customDate);
        const isMatch =
          dateToCheck.getDate() === custom.getDate() &&
          dateToCheck.getMonth() === custom.getMonth() &&
          dateToCheck.getFullYear() === custom.getFullYear();
        if (!isMatch) return false;
      }

      // Title search filter
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase();
        if (!a.title.toLowerCase().includes(q)) return false;
      }

      return true;
    });
  }, [allAssignments, dateFilter, customDate, searchQuery]);

  // Current selected assignment
  const selectedAssignment = useMemo(() => {
    if (!selectedAssignmentId) return null;
    return allAssignments.find((a) => a.assignmentId === selectedAssignmentId) || null;
  }, [allAssignments, selectedAssignmentId]);

  // 4. Load Students Progress for the selected assignment
  const {
    data: progressData,
    isLoading: isLoadingProgress,
    refetch: refetchProgress,
  } = useQuery({
    queryKey: ["gradingAssignmentProgress", actorScope, selectedAssignmentId],
    queryFn: () => getAssignmentProgress(selectedAssignmentId),
    enabled: !!selectedAssignmentId,
  });

  const studentsProgressList: AssignmentProgressItemDto[] = useMemo(() => {
    return progressData?.data ?? [];
  }, [progressData]);

  // Filter students who did the assignment
  const filteredStudents = useMemo(() => {
    return studentsProgressList.filter((s) => {
      // Must have answered at least 1 question or completed/in-progress
      const hasActivity = s.completedQuestionCount > 0 || s.status === "Completed" || s.status === "InProgress";
      if (!hasActivity) return false;

      if (studentStatusFilter === "pending") {
        return s.teacherFinalReviewStatus !== "Approved";
      }
      if (studentStatusFilter === "approved") {
        return s.teacherFinalReviewStatus === "Approved";
      }
      return true;
    });
  }, [studentsProgressList, studentStatusFilter]);

  // Current selected student
  const selectedStudent = useMemo(() => {
    if (!selectedStudentId) return null;
    return studentsProgressList.find((s) => s.studentId === selectedStudentId) || null;
  }, [studentsProgressList, selectedStudentId]);

  // 5. Load questions answered by this student in this assignment
  const {
    data: studentQuestionsData,
    isLoading: isLoadingStudentQuestions,
    refetch: refetchStudentQuestions,
  } = useQuery({
    queryKey: ["gradingStudentQuestions", actorScope, selectedAssignmentId, selectedStudentId],
    queryFn: () =>
      listTeacherReviewQueue({
        assignmentId: selectedAssignmentId,
        studentId: selectedStudentId,
        includeAllQuestions: true,
        pageSize: 100,
      }),
    enabled: !!selectedAssignmentId && !!selectedStudentId,
  });

  const studentQuestions: TeacherReviewQueueItemDto[] = useMemo(() => {
    if (!selectedAssignmentId || !selectedStudentId) return [];
    return [...(studentQuestionsData?.data ?? [])].sort((a, b) =>
      (a.questionOrderIndex ?? Number.MAX_SAFE_INTEGER) - (b.questionOrderIndex ?? Number.MAX_SAFE_INTEGER));
  }, [studentQuestionsData, selectedAssignmentId, selectedStudentId]);

  // Preserve the selected attempt, even when refreshed evidence changes order.
  const selectedQuestionIndex = activeQuestionSelection?.scope === workspaceScope
    ? studentQuestions.findIndex(q => q.attemptId === activeQuestionSelection.attemptId) : -1;
  const activeQuestionIndex = Math.max(0, selectedQuestionIndex);
  const currentQuestion: TeacherReviewQueueItemDto | null = useMemo(() => {
    if (studentQuestions.length === 0) return null;
    return studentQuestions[activeQuestionIndex] || studentQuestions[0] || null;
  }, [studentQuestions, activeQuestionIndex]);
  useEffect(() => {
    if (currentQuestion && selectedQuestionIndex < 0) {
      setActiveQuestionSelection({ scope: workspaceScope, attemptId: currentQuestion.attemptId });
    }
  }, [currentQuestion, selectedQuestionIndex, workspaceScope]);
  useEffect(() => {
    setIsScratchpadOpen(false);
    setScratchpadAttemptId(null);
    setIsVoidModalOpen(false);
    setIsFinalApproveModalOpen(false);
    setIsReopenModalOpen(false);
    setReopenReason("");
    setVoidImpactAcknowledged(false);
  }, [workspaceScope]);

  // Resolve formatted student final answer (for multiple choice or math formulas)
  const resolvedStudentAnswer = useMemo(() => {
    if (!currentQuestion) return "";
    const qType = currentQuestion.questionType;
    const opts = currentQuestion.options || [];

    // For Multiple Choice questions:
    if (qType === "MultipleChoice") {
      const raw = (currentQuestion.finalAnswer || "").trim();
      const matched = opts.find(
        (o) =>
          o.optionId === raw ||
          o.optionLabel.toUpperCase() === raw.toUpperCase() ||
          raw.startsWith(o.optionLabel + ".") ||
          raw.startsWith(o.optionLabel + " ") ||
          o.optionText === raw
      );
      if (matched) {
        return matched.optionText
          ? `${matched.optionLabel}. ${matched.optionText}`
          : matched.optionLabel;
      }
      if (raw) {
        return "Phương án đã chọn";
      }
      return "";
    }

    // For Math / Short Answer / Essay questions:
    // If student has formatted LaTeX in answerDisplayLatex, use it; otherwise use finalAnswer
    const latexAns = currentQuestion.answerDisplayLatex?.trim();
    if (latexAns) {
      const isUuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(latexAns);
      const isRawOptionId = opts.some((o) => o.optionId === latexAns);
      if (!isRawOptionId && !isUuid) {
        return latexAns.replace(/\\placeholder(\[[^\]]*\])?(\{[^}]*\})?/g, "___");
      }
    }

    const finalAns = currentQuestion.finalAnswer?.trim() || "";
    return finalAns.replace(/\\placeholder(\[[^\]]*\])?(\{[^}]*\})?/g, "___");
  }, [currentQuestion]);

  // Resolve genuine student reasoning text
  const resolvedStudentReasoning = useMemo(() => {
    if (!currentQuestion) return null;
    const raw = currentQuestion.reasoningText?.trim();
    if (!raw) return null;

    // Filter out internal database optionId leaks (numeric IDs with 4+ digits matching an optionId)
    const opts = currentQuestion.options || [];
    if (/^\d{4,}$/.test(raw) && opts.some((o) => o.optionId === raw)) {
      return null;
    }

    return raw;
  }, [currentQuestion]);

  const resolvedTeacherAnswer = currentQuestion?.questionType === "MultipleChoice"
    ? currentQuestion.options?.filter(o => o.isCorrect).map(o => `${o.optionLabel}. ${o.optionText}`).join("; ") || "Chưa có phương án đúng."
    : currentQuestion?.correctAnswer || "Chưa có đáp án chuẩn.";

  // Override Form State for the active question
  const [awardedScore, setAwardedScore] = useState<number>(10);
  const [isCorrectVal, setIsCorrectVal] = useState<boolean | null>(null);
  const [reasoningQuality, setReasoningQuality] = useState<number>(80);
  const [errorTypeVal, setErrorTypeVal] = useState<ErrorType>("None");
  const [feedbackVal, setFeedbackVal] = useState<string>("");
  const [overrideReasonVal, setOverrideReasonVal] = useState<string>("");
  const [rubricForm, setRubricForm] = useState<RubricForm>({});
  const rubricBaseline = useRef<RubricForm>({});
  const rubricCriteria = currentQuestion?.gradingCriteria?.criteria || [];
  const rubricResult = buildRubricScores(rubricCriteria, rubricForm, currentQuestion?.maxScore ?? 10);
  const [isEditingGrade, setIsEditingGrade] = useState(false);
  const loadedFormKey = useRef("");
  const loadedFormContext = useRef("");
  const formBaseline = useRef(resolveQuestionDefaultFormValues(null));
  const formContext = currentQuestion ? `${workspaceScope}|${currentQuestion.attemptId}|${currentQuestion.analysisId}` : "";
  const formKey = currentQuestion ? `${formContext}|${currentQuestion.overrideVersion ?? currentQuestion.evidence?.analysisOverrideVersion ?? 0}` : "";
  const questionActions = questionGradingActions(currentQuestion);
  const finalResultLocked = selectedStudent?.teacherFinalReviewStatus === "Approved" || currentQuestion?.teacherFinalReviewStatus === "Approved";
  const displayedQuestionGrade = normalizeQuestionScore(currentQuestion?.overrideAwardedScore ?? currentQuestion?.awardedScore, currentQuestion?.maxScore ?? 10);
  const assignmentContribution = questionAssignmentContribution(currentQuestion?.overrideAwardedScore ?? currentQuestion?.awardedScore,
    currentQuestion?.maxScore ?? 10, currentQuestion?.assignmentQuestionCount);
  const originalReasoningQuality = currentQuestion?.hasTeacherOverride
    ? currentQuestion.originalReasoningQuality
    : currentQuestion?.originalReasoningQuality ?? currentQuestion?.reasoningQuality;
  const formDefaults = useMemo(() => resolveQuestionDefaultFormValues(currentQuestion), [currentQuestion]);
  const showGradeForm = !finalResultLocked && (isEditingGrade || questionActions.needsManualGrade);
  // Dirty means a visible, initialized form was actually edited. Never compare
  // retained input state with another student's/question's server defaults.
  const hasUnsavedGrade = Boolean(currentQuestion && showGradeForm && loadedFormContext.current === formContext &&
    (gradingFormIsDirty({ awardedScore, isCorrectVal, reasoningQuality, errorTypeVal, feedbackVal, overrideReasonVal }, formBaseline.current) ||
      JSON.stringify(rubricForm) !== JSON.stringify(rubricBaseline.current)));
  const gradeChangedOnServer = Boolean(hasUnsavedGrade && loadedFormKey.current !== formKey);
  const loadGradeForm = useCallback(() => {
    loadedFormKey.current = formKey;
    loadedFormContext.current = formContext;
    formBaseline.current = { ...formDefaults };
    setAwardedScore(formDefaults.awardedScore); setIsCorrectVal(formDefaults.isCorrectVal);
    setReasoningQuality(formDefaults.reasoningQuality); setErrorTypeVal(formDefaults.errorTypeVal);
    setFeedbackVal(formDefaults.feedbackVal); setOverrideReasonVal(formDefaults.overrideReasonVal);
    const initialRubric = hydrateRubricForm(currentQuestion?.gradingCriteria?.criteria || [], currentQuestion?.rubricGrade);
    rubricBaseline.current = initialRubric; setRubricForm(initialRubric);
    setIsEditingGrade(false);
  }, [formKey, formContext, formDefaults, currentQuestion]);

  // Populate override form when active question changes
  useEffect(() => {
    if (loadedFormKey.current === formKey && loadedFormContext.current === formContext) return;
    // Keep real edits if a concurrent teacher update arrives in this context.
    if (gradeChangedOnServer) return;
    loadGradeForm();
  }, [formKey, formContext, gradeChangedOnServer, loadGradeForm]);

  // MUTATIONS (Teacher Only)
  // A. Quick Approve AI Analysis
  const approveMutation = useMutation({
    mutationFn: ({ analysisId, version }: { analysisId: string; version: number }) =>
      approveReasoningAnalysis(analysisId, { overrideVersion: version, note: "Giáo viên đã kiểm tra và xác nhận kết quả câu hỏi." }),
    onSuccess: async () => {
      showToast("Đã xác nhận kết quả câu hỏi. Chốt toàn bài là bước riêng sau khi rà soát xong.", "success");
      loadGradeForm();
      loadedFormKey.current = "";
      await Promise.all([refetchStudentQuestions(), refetchProgress()]);
      queryClient.invalidateQueries({ queryKey: ["gradingAssignmentProgress", actorScope, selectedAssignmentId] });
      queryClient.invalidateQueries({ queryKey: ["gradingGlobalQueue"] });
    },
    onError: (err: any) => {
      const problem = extractProblemDetails(err);
      showToast(problem.detail || "Không thể duyệt kết quả câu hỏi.", "error");
    },
  });

  // B. Override Question Grade
  const overrideMutation = useMutation({
    mutationFn: ({ analysisId, payload }: { analysisId: string; payload: TeacherOverrideRequest }) =>
      overrideReasoningAnalysis(analysisId, payload),
    onSuccess: async () => {
      showToast("Đã lưu điểm và đánh giá cho câu hỏi thành công!", "success");
      loadGradeForm();
      loadedFormKey.current = "";
      await Promise.all([refetchStudentQuestions(), refetchProgress()]);
      queryClient.invalidateQueries({ queryKey: ["gradingAssignmentProgress", actorScope, selectedAssignmentId] });
      queryClient.invalidateQueries({ queryKey: ["gradingGlobalQueue"] });
    },
    onError: (err: any) => {
      const problem = extractProblemDetails(err);
      showToast(problem.detail || "Không thể lưu kết quả chấm câu hỏi.", "error");
    },
  });

  // C. Final Approve Entire Student Assignment Result
  const finalApproveMutation = useMutation({
    mutationFn: async () => {
      if (!selectedAssignment || !selectedStudent) throw new Error("Thiếu thông tin bài tập hoặc học sinh.");
      if (hasUnsavedGrade || approveMutation.isPending || overrideMutation.isPending || voidQuestionMutation.isPending || reopenMutation.isPending) throw new Error("Hãy lưu đánh giá đang sửa trước khi chốt bài.");
      // Fail closed: a failed refresh must never fall back to stale eligibility/version.
      const freshProgress = await getAssignmentProgress(selectedAssignment.assignmentId);
      const freshStudent = freshProgress.data.find((s) => s.studentId === selectedStudent.studentId);
      const blockReason = finalApprovalBlockReason(freshStudent, false, false);
      if (blockReason || freshStudent?.finalReviewVersion == null) throw new Error(blockReason || "Thiếu phiên bản kết quả.");
      return approveAssignmentResult(selectedAssignment.assignmentId, {
        studentId: selectedStudent.studentId,
        finalReviewVersion: freshStudent.finalReviewVersion,
        note: finalApproveNote,
      });
    },
    onSuccess: async () => {
      showToast("Đã phê duyệt toàn bộ kết quả bài tập cho học sinh này!", "success");
      setIsFinalApproveModalOpen(false);
      loadGradeForm();
      await Promise.all([refetchStudentQuestions(), refetchProgress()]);
      queryClient.invalidateQueries({ queryKey: ["gradingAssignmentProgress", actorScope, selectedAssignmentId] });
      queryClient.invalidateQueries({ queryKey: ["gradingGlobalQueue"] });
    },
    onError: (err: any) => {
      refetchProgress();
      const problem = extractProblemDetails(err);
      showToast(problem.detail || (err instanceof Error ? err.message : "Lỗi khi phê duyệt kết quả bài làm."), "error");
    },
  });

  // D. Void Question Mutation
  const voidQuestionMutation = useMutation({
    mutationFn: () => {
      if (!selectedAssignment || !currentQuestion) throw new Error("Thiếu thông tin câu hỏi.");
      if (finalResultLocked || !voidImpactAcknowledged) throw new Error("Hãy mở lại kết quả và xác nhận phạm vi ảnh hưởng trước khi hủy câu.");
      return voidAssignmentQuestion(selectedAssignment.assignmentId, currentQuestion.questionId, {
        reason: voidReason,
        quarantineInBank,
        reopenFinalizedResults: voidImpactAcknowledged,
      });
    },
    onSuccess: async () => {
      showToast("Đã hủy câu hỏi khỏi bài tập thành công!", "success");
      setIsVoidModalOpen(false);
      loadGradeForm();
      loadedFormKey.current = "";
      await Promise.all([refetchStudentQuestions(), refetchProgress()]);
    },
    onError: (err: any) => {
      const problem = extractProblemDetails(err);
      showToast(problem.detail || "Không thể hủy câu hỏi.", "error");
    },
  });

  const reopenMutation = useMutation({
    mutationFn: async () => {
      if (!canGrade || !selectedAssignment || !selectedStudent || reopenReason.trim().length < 5 || reopenReason.trim().length > 1000)
        throw new Error("Lý do mở lại phải từ 5 đến 1000 ký tự.");
      const fresh = await getAssignmentProgress(selectedAssignment.assignmentId);
      const target = fresh.data.find(s => s.studentId === selectedStudent.studentId);
      if (!target || target.teacherFinalReviewStatus !== "Approved" || target.finalReviewVersion == null)
        throw new Error("Kết quả đã thay đổi hoặc đã được mở lại. Hãy tải lại.");
      return reopenAssignmentResult(selectedAssignment.assignmentId, { studentId: target.studentId,
        finalReviewVersion: target.finalReviewVersion, reason: reopenReason.trim() });
    },
    onSuccess: async () => {
      setIsReopenModalOpen(false); setReopenReason(""); loadGradeForm(); loadedFormKey.current = "";
      await Promise.all([refetchStudentQuestions(), refetchProgress()]);
      queryClient.invalidateQueries({ queryKey: ["gradingGlobalQueue", actorScope] });
      showToast("Đã mở lại kết quả để điều chỉnh. Sau khi sửa, cần chốt lại toàn bài. Bài nộp của học sinh được giữ nguyên.", "success");
    },
    onError: (err: any) => { const problem = extractProblemDetails(err);
      showToast(problem.detail || (err instanceof Error ? err.message : "Không thể mở lại kết quả."), "error"); }
  });

  // Navigation Helpers
  const gradingBusy = approveMutation.isPending || overrideMutation.isPending || finalApproveMutation.isPending || voidQuestionMutation.isPending || reopenMutation.isPending;
  const finalBlockReason = finalApprovalBlockReason(selectedStudent, hasUnsavedGrade, gradingBusy);
  useEffect(() => {
    if (!hasUnsavedGrade && !gradingBusy) return;
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); event.returnValue = ""; };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [hasUnsavedGrade, gradingBusy]);
  const allowNavigation = () => {
    if (gradingBusy) { showToast("Đang cập nhật kết quả. Vui lòng chờ trước khi chuyển câu."); return false; }
    if (hasUnsavedGrade && !window.confirm("Đánh giá đang sửa chưa được lưu. Bạn muốn bỏ thay đổi và chuyển trang/câu?")) return false;
    loadedFormKey.current = "";
    loadedFormContext.current = "";
    setIsEditingGrade(false);
    return true;
  };
  const handleSelectQuestion = (index: number) => {
    if (index !== activeQuestionIndex && studentQuestions[index] && allowNavigation()) {
      setActiveQuestionSelection({ scope: workspaceScope, attemptId: studentQuestions[index].attemptId });
    }
  };
  const resetGradeForm = () => {
    if (gradingBusy || (hasUnsavedGrade && !window.confirm("Bỏ những thay đổi chưa lưu của câu này?"))) return;
    loadGradeForm();
  };
  const handleSelectAssignment = (assignmentId: string) => {
    if (!allowNavigation()) return;
    const params = new URLSearchParams(searchParams);
    params.set("assignmentId", assignmentId);
    params.delete("studentId");
    setSearchParams(params);
    setActiveQuestionSelection(null);
  };

  const handleBackToAssignments = () => {
    if (!allowNavigation()) return;
    const params = new URLSearchParams(searchParams);
    params.delete("assignmentId");
    params.delete("studentId");
    setSearchParams(params);
  };

  const handleSelectStudent = (studentId: string) => {
    if (!allowNavigation()) return;
    const params = new URLSearchParams(searchParams);
    params.set("studentId", studentId);
    setSearchParams(params);
    setActiveQuestionSelection(null);
  };

  const handleBackToStudents = () => {
    if (!allowNavigation()) return;
    const params = new URLSearchParams(searchParams);
    params.delete("studentId");
    setSearchParams(params);
  };

  const handleClassChange = (classId: string) => {
    if (!allowNavigation()) return;
    const params = new URLSearchParams(searchParams);
    if (classId) {
      params.set("classId", classId);
    } else {
      params.delete("classId");
    }
    params.delete("assignmentId");
    params.delete("studentId");
    setSearchParams(params);
  };

  // Submit Override Handler
  const handleSaveQuestionGrade = () => {
    if (!currentQuestion || !canGrade || finalResultLocked || !questionActions.canEdit || gradingBusy) return;
    if (gradeChangedOnServer) { showToast("Kết quả câu này đã được cập nhật trên server. Hãy hủy thay đổi và rà soát phiên bản mới trước khi lưu.", "error"); return; }
    if (!Number.isFinite(awardedScore) || awardedScore < 0 || awardedScore > 10) {
      showToast("Điểm phải nằm trong khoảng 0 đến điểm tối đa của câu hỏi.", "error"); return;
    }
    if (isCorrectVal === null) {
      showToast("Vui lòng chọn kết quả Đúng hoặc Sai cho câu hỏi trước khi lưu.", "error");
      return;
    }
    if (!feedbackVal.trim()) {
      showToast("Vui lòng nhập lời nhận xét của giáo viên gửi học sinh trước khi lưu.", "error");
      return;
    }
    if (!overrideReasonVal.trim() && !questionActions.needsManualGrade) {
      showToast("Vui lòng nhập lý do điều chỉnh điểm số (bắt buộc theo quy định kiểm tra).", "error");
      return;
    }

    if (rubricCriteria.length && rubricResult.error) { showToast(rubricResult.error, "error"); return; }

    const payload: TeacherOverrideRequest = {
      awardedScore: rubricCriteria.length ? rubricResult.total : toInternalQuestionScore(awardedScore, currentQuestion.maxScore ?? 10),
      rubricScores: rubricCriteria.length ? rubricResult.scores : undefined,
      isCorrect: isCorrectVal,
      reasoningQuality: reasoningQuality,
      errorType: errorTypeVal,
      feedback: feedbackVal.trim(),
      reason: overrideReasonVal.trim() || "Giáo viên chấm trực tiếp câu chưa có điểm xác định.",
      overrideVersion: currentQuestion.overrideVersion ?? currentQuestion.evidence?.analysisOverrideVersion ?? 0,
    };

    overrideMutation.mutate({ analysisId: currentQuestion.analysisId, payload });
  };

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-slate-950 text-slate-900 dark:text-slate-100 p-4 sm:p-6 lg:p-8 space-y-6">
      {/* Toast Alert */}
      {toast && (
        <div
          className={`fixed top-4 right-4 z-50 flex items-center gap-3 px-5 py-3 rounded-xl shadow-2xl border text-sm font-medium transition-all transform animate-in fade-in slide-in-from-top-4 duration-300 ${
            toast.type === "success"
              ? "bg-emerald-50 border-emerald-300 text-emerald-900 dark:bg-emerald-950 dark:border-emerald-700 dark:text-emerald-100"
              : toast.type === "error"
              ? "bg-rose-50 border-rose-300 text-rose-900 dark:bg-rose-950 dark:border-rose-700 dark:text-rose-100"
              : "bg-blue-50 border-blue-300 text-blue-900 dark:bg-blue-950 dark:border-blue-700 dark:text-blue-100"
          }`}
        >
          {toast.type === "success" && (
            <svg className="w-5 h-5 text-emerald-600 dark:text-emerald-400 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
            </svg>
          )}
          {toast.type === "error" && (
            <svg className="w-5 h-5 text-rose-600 dark:text-rose-400 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          )}
          <span>{toast.message}</span>
          <button onClick={() => setToast(null)} className="ml-2 hover:opacity-70 text-base font-bold">×</button>
        </div>
      )}

      {/* HEADER SECTION */}
      <header className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-6 shadow-sm">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <div className="flex items-center gap-2 mb-1">
              <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-indigo-100 text-indigo-800 dark:bg-indigo-900/60 dark:text-indigo-300">
                {isManager ? "Giám sát & Quản lý" : "Hệ thống Khảo thí & Chấm bài"}
              </span>
              {isManager ? (
                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-100 text-amber-800 dark:bg-amber-900/50 dark:text-amber-300">
                  Chế độ Xem (Không có quyền chấm bài)
                </span>
              ) : (
                <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800 dark:bg-emerald-900/50 dark:text-emerald-300">
                  Quyền Giáo viên chấm bài
                </span>
              )}
            </div>
            <h1 className="text-2xl sm:text-3xl font-extrabold tracking-tight bg-gradient-to-r from-slate-900 via-indigo-950 to-slate-800 dark:from-white dark:via-indigo-200 dark:to-slate-300 bg-clip-text text-transparent">
              {isManager ? "Hàng đợi Giám sát Bài tập Toàn Trung tâm" : "Không gian Chấm bài & Duyệt kết quả Bài tập"}
            </h1>
            <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
              {isManager
                ? "Theo dõi tiến độ nộp bài, đối chiếu kết quả làm bài của học sinh và phân tích đánh giá của AI trên toàn bộ các lớp học."
                : "Lọc bài tập theo ngày và theo lớp, chọn học sinh để kiểm tra chi tiết từng câu hỏi, duyệt AI hoặc can thiệp điểm số chuẩn xác."}
            </p>
          </div>

          {/* Role Status Tag */}
          <div className="flex items-center gap-3">
            {isManager && (
              <div className="flex items-center gap-2 bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800/60 px-4 py-2 rounded-xl text-xs text-amber-800 dark:text-amber-200">
                <svg className="w-4 h-4 shrink-0 text-amber-600 dark:text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <span>Chức năng sửa đổi/chấm điểm được giới hạn riêng cho giáo viên phụ trách lớp.</span>
              </div>
            )}
          </div>
        </div>

        {/* BREADCRUMBS NAVIGATION */}
        <div className="mt-5 pt-4 border-t border-slate-100 dark:border-slate-800 flex items-center flex-wrap gap-2 text-xs sm:text-sm font-medium">
          <button
            onClick={handleBackToAssignments}
            className={`flex items-center gap-1.5 transition-colors ${
              !selectedAssignment
                ? "text-indigo-600 dark:text-indigo-400 font-bold"
                : "text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-slate-200"
            }`}
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
            </svg>
            <span>Danh sách bài tập</span>
          </button>

          {selectedAssignment && (
            <>
              <span className="text-slate-400">/</span>
              <button
                onClick={handleBackToStudents}
                className={`flex items-center gap-1.5 transition-colors ${
                  !selectedStudent
                    ? "text-indigo-600 dark:text-indigo-400 font-bold"
                    : "text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-slate-200"
                }`}
              >
                <span>Bài tập: {selectedAssignment.title}</span>
              </button>
            </>
          )}

          {selectedStudent && (
            <>
              <span className="text-slate-400">/</span>
              <span className="text-indigo-600 dark:text-indigo-400 font-bold flex items-center gap-1.5">
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                </svg>
                <span>Chấm bài: {selectedStudent.fullName}</span>
              </span>
            </>
          )}
        </div>
      </header>

      {/* ========================================================================= */}
      {/* LEVEL 1: ASSIGNMENTS LIST (WITH CLASS & DATE FILTERS)                      */}
      {/* ========================================================================= */}
      {!selectedAssignment && (
        <section className="space-y-6 animate-in fade-in duration-300">
          {/* FILTER CONTROLS BAR */}
          <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-5 shadow-sm space-y-4">
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              {/* 1. Lọc theo lớp học */}
              <div>
                <label className="block text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1.5">
                  Lọc theo Lớp học
                </label>
                <select
                  value={selectedClassId}
                  onChange={(e) => handleClassChange(e.target.value)}
                  className="w-full bg-slate-50 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 rounded-xl px-3.5 py-2.5 text-sm font-medium focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                >
                  <option value="">Tất cả các lớp</option>
                  {classesList.map((cls) => (
                    <option key={cls.classId} value={cls.classId}>
                      {cls.className} ({cls.academicYear || "Lớp học"})
                    </option>
                  ))}
                </select>
              </div>

              {/* 2. Lọc theo ngày (Preset) */}
              <div>
                <label className="block text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1.5">
                  Thời gian làm / Hạn nộp
                </label>
                <div className="flex items-center gap-1 bg-slate-100 dark:bg-slate-800 p-1 rounded-xl">
                  {(["all", "today", "7days", "30days", "custom"] as DateFilterPreset[]).map((preset) => (
                    <button
                      key={preset}
                      type="button"
                      onClick={() => setDateFilter(preset)}
                      className={`flex-1 py-1.5 text-xs font-semibold rounded-lg transition-all ${
                        dateFilter === preset
                          ? "bg-white dark:bg-slate-900 text-indigo-600 dark:text-indigo-400 shadow-sm"
                          : "text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100"
                      }`}
                    >
                      {preset === "all" && "Tất cả"}
                      {preset === "today" && "Hôm nay"}
                      {preset === "7days" && "7 ngày"}
                      {preset === "30days" && "30 ngày"}
                      {preset === "custom" && "Chọn ngày"}
                    </button>
                  ))}
                </div>
              </div>

              {/* 3. Lọc theo ngày cụ thể (DatePicker nếu chọn custom) */}
              {dateFilter === "custom" ? (
                <div>
                  <label className="block text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1.5">
                    Chọn ngày cụ thể
                  </label>
                  <input
                    type="date"
                    value={customDate}
                    onChange={(e) => setCustomDate(e.target.value)}
                    className="w-full bg-slate-50 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 rounded-xl px-3.5 py-2 text-sm font-medium focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                  />
                </div>
              ) : (
                /* Tìm kiếm theo tên bài tập */
                <div>
                  <label className="block text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1.5">
                    Tìm kiếm bài tập
                  </label>
                  <div className="relative">
                    <input
                      type="text"
                      placeholder="Nhập tên bài tập..."
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      className="w-full bg-slate-50 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 rounded-xl pl-9 pr-3.5 py-2.5 text-sm font-medium focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                    />
                    <svg className="w-4 h-4 text-slate-400 absolute left-3 top-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                    </svg>
                  </div>
                </div>
              )}

              {/* 4. Thống kê bài tập nhanh */}
              <div className="flex items-center gap-3 bg-gradient-to-br from-indigo-50 to-blue-50 dark:from-indigo-950/40 dark:to-slate-900 p-3.5 rounded-xl border border-indigo-100 dark:border-indigo-900/50">
                <div className="w-10 h-10 rounded-xl bg-indigo-600 text-white flex items-center justify-center font-bold text-lg shadow-md shrink-0">
                  {filteredAssignments.length}
                </div>
                <div>
                  <div className="text-xs font-medium text-indigo-900 dark:text-indigo-300">Bài tập phù hợp</div>
                  <div className="text-xs text-slate-500 dark:text-slate-400">
                    {classesList.find((c) => c.classId === selectedClassId)?.className || "Toàn bộ lớp"}
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* ASSIGNMENTS GRID */}
          {isLoadingAssignments ? (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
              {[1, 2, 3, 4, 5, 6].map((i) => (
                <div key={i} className="h-44 bg-white dark:bg-slate-900 rounded-2xl border border-slate-200 dark:border-slate-800 animate-pulse p-6 space-y-4" />
              ))}
            </div>
          ) : filteredAssignments.length === 0 ? (
            <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-12 text-center max-w-xl mx-auto space-y-3">
              <div className="w-16 h-16 bg-slate-100 dark:bg-slate-800 rounded-full flex items-center justify-center mx-auto text-slate-400">
                <svg className="w-8 h-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                </svg>
              </div>
              <h3 className="text-lg font-bold text-slate-800 dark:text-slate-200">Không tìm thấy bài tập nào</h3>
              <p className="text-sm text-slate-500 dark:text-slate-400">
                Hãy thử thay đổi bộ lọc lớp học hoặc khoảng thời gian để hiển thị các bài tập cần chấm.
              </p>
              <button
                onClick={() => {
                  setDateFilter("all");
                  setCustomDate("");
                  setSearchQuery("");
                  handleClassChange("");
                }}
                className="inline-flex items-center gap-1.5 px-4 py-2 bg-indigo-50 dark:bg-indigo-950 text-indigo-600 dark:text-indigo-400 rounded-xl text-xs font-semibold hover:bg-indigo-100 transition-colors"
              >
                Đặt lại bộ lọc
              </button>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
              {filteredAssignments.map((assignment) => {
                const assignedClass = classesList.find((c) => c.classId === assignment.classId);
                const pendingCount = pendingCountByAssignment.get(assignment.assignmentId) || 0;

                return (
                  <div
                    key={assignment.assignmentId}
                    onClick={() => handleSelectAssignment(assignment.assignmentId)}
                    className="group bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 hover:border-indigo-400 dark:hover:border-indigo-600 rounded-2xl p-5 shadow-sm hover:shadow-md transition-all cursor-pointer flex flex-col justify-between"
                  >
                    <div>
                      {/* Top Badges */}
                      <div className="flex items-center justify-between gap-2 mb-3">
                        <span className="inline-flex items-center px-2.5 py-0.5 rounded-lg text-xs font-medium bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300">
                          {assignedClass?.className || "Lớp học"}
                        </span>
                        {pendingCount > 0 ? (
                          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-lg text-xs font-bold bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300 border border-amber-300 dark:border-amber-800 animate-pulse">
                            <span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
                            {pendingCount} câu cần duyệt
                          </span>
                        ) : (
                          <span className="inline-flex items-center px-2 py-0.5 rounded-lg text-[11px] font-medium bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-400">
                            Đã ổn định
                          </span>
                        )}
                      </div>

                      {/* Title */}
                      <h3 className="text-base font-bold text-slate-900 dark:text-slate-100 group-hover:text-indigo-600 dark:group-hover:text-indigo-400 transition-colors line-clamp-2">
                        {assignment.title}
                      </h3>

                      {/* Instructions / summary */}
                      {assignment.instructions && (
                        <p className="mt-1.5 text-xs text-slate-500 dark:text-slate-400 line-clamp-2">
                          {assignment.instructions}
                        </p>
                      )}
                    </div>

                    {/* Footer / Meta info */}
                    <div className="mt-5 pt-4 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between text-xs text-slate-500 dark:text-slate-400">
                      <div className="flex items-center gap-3">
                        <span title="Số câu hỏi">📝 {assignment.questionCount} câu</span>
                        <span title="Số học sinh mục tiêu">👥 {assignment.targetStudentCount} HS</span>
                      </div>
                      <span className="font-semibold text-indigo-600 dark:text-indigo-400 group-hover:translate-x-1 transition-transform flex items-center gap-1">
                        Xem bài làm →
                      </span>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </section>
      )}

      {/* ========================================================================= */}
      {/* LEVEL 2: STUDENTS LIST FOR SELECTED ASSIGNMENT                            */}
      {/* ========================================================================= */}
      {selectedAssignment && !selectedStudent && (
        <section className="space-y-6 animate-in fade-in duration-300">
          {/* Back button & Assignment Summary Header */}
          <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-6 shadow-sm flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div>
              <button
                onClick={handleBackToAssignments}
                className="inline-flex items-center gap-1.5 text-xs font-bold text-slate-500 hover:text-indigo-600 dark:text-slate-400 dark:hover:text-indigo-400 mb-2 transition-colors"
              >
                ← Quay lại danh sách bài tập
              </button>
              <h2 className="text-xl sm:text-2xl font-black text-slate-900 dark:text-slate-100">
                {selectedAssignment.title}
              </h2>
              <div className="mt-1.5 flex flex-wrap items-center gap-4 text-xs text-slate-500 dark:text-slate-400">
                <span>Lớp: <strong>{classesList.find((c) => c.classId === selectedAssignment.classId)?.className || "Chưa xác định"}</strong></span>
                {selectedAssignment.dueAt && (
                  <span>Hạn nộp: <strong>{new Date(selectedAssignment.dueAt).toLocaleDateString("vi-VN")}</strong></span>
                )}
                <span>Quy mô: <strong>{selectedAssignment.questionCount} câu hỏi</strong></span>
              </div>
            </div>

            {/* Quick Status Filter for Students */}
            <div className="flex items-center gap-2">
              <span className="text-xs font-bold uppercase text-slate-400">Lọc học sinh:</span>
              <div className="bg-slate-100 dark:bg-slate-800 p-1 rounded-xl flex items-center">
                <button
                  type="button"
                  onClick={() => setStudentStatusFilter("all")}
                  className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all ${
                    studentStatusFilter === "all"
                      ? "bg-white dark:bg-slate-900 text-indigo-600 dark:text-indigo-400 shadow-sm"
                      : "text-slate-600 dark:text-slate-400 hover:text-slate-900"
                  }`}
                >
                  Tất cả ({studentsProgressList.length})
                </button>
                <button
                  type="button"
                  onClick={() => setStudentStatusFilter("pending")}
                  className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all ${
                    studentStatusFilter === "pending"
                      ? "bg-white dark:bg-slate-900 text-amber-600 dark:text-amber-400 shadow-sm"
                      : "text-slate-600 dark:text-slate-400 hover:text-slate-900"
                  }`}
                >
                  Chờ chấm
                </button>
                <button
                  type="button"
                  onClick={() => setStudentStatusFilter("approved")}
                  className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-all ${
                    studentStatusFilter === "approved"
                      ? "bg-white dark:bg-slate-900 text-emerald-600 dark:text-emerald-400 shadow-sm"
                      : "text-slate-600 dark:text-slate-400 hover:text-slate-900"
                  }`}
                >
                  Đã duyệt
                </button>
              </div>
            </div>
          </div>

          {/* STUDENTS TABLE / LIST */}
          {isLoadingProgress ? (
            <div className="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200 dark:border-slate-800 p-8 text-center">
              <div className="inline-block animate-spin rounded-full h-8 w-8 border-4 border-indigo-600 border-t-transparent" />
              <p className="mt-2 text-sm text-slate-500">Đang tải danh sách học sinh nộp bài...</p>
            </div>
          ) : filteredStudents.length === 0 ? (
            <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-12 text-center max-w-lg mx-auto space-y-3">
              <div className="w-14 h-14 bg-slate-100 dark:bg-slate-800 rounded-full flex items-center justify-center mx-auto text-slate-400">
                👥
              </div>
              <h3 className="text-base font-bold text-slate-800 dark:text-slate-200">
                {studentStatusFilter !== "all"
                  ? "Không có học sinh nào ở trạng thái này."
                  : "Chưa có học sinh nào nộp hoặc làm bài tập này."}
              </h3>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                Khi học sinh trong lớp hoàn thành bài nộp, tên học sinh và kết quả sẽ hiển thị tại đây để chấm bài.
              </p>
            </div>
          ) : (
            <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl shadow-sm overflow-hidden">
              <div className="px-6 py-4 border-b border-slate-100 dark:border-slate-800 flex items-center justify-between">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500">
                  Danh sách học sinh đã làm bài ({filteredStudents.length} học sinh)
                </span>
                <span className="text-xs text-slate-400">Nhấn vào học sinh để xem chi tiết bài làm</span>
              </div>

              <div className="divide-y divide-slate-100 dark:divide-slate-800">
                {filteredStudents.map((student) => {
                  const isApproved = student.teacherFinalReviewStatus === "Approved";
                  const percentCompleted =
                    student.totalQuestionCount > 0
                      ? Math.round((student.completedQuestionCount / student.totalQuestionCount) * 100)
                      : 0;

                  return (
                    <div
                      key={student.studentId}
                      onClick={() => handleSelectStudent(student.studentId)}
                      className="group px-6 py-4.5 flex flex-col sm:flex-row sm:items-center justify-between gap-4 hover:bg-indigo-50/50 dark:hover:bg-indigo-950/20 cursor-pointer transition-colors"
                    >
                      <div className="flex items-center gap-3.5">
                        <div className="w-11 h-11 rounded-full bg-gradient-to-br from-indigo-500 to-purple-600 text-white font-bold flex items-center justify-center shadow-sm shrink-0">
                          {student.fullName.charAt(0).toUpperCase()}
                        </div>
                        <div>
                          <div className="flex items-center gap-2">
                            <h4 className="text-sm font-bold text-slate-900 dark:text-slate-100 group-hover:text-indigo-600 dark:group-hover:text-indigo-400 transition-colors">
                              {student.fullName}
                            </h4>
                            {isApproved ? (
                              <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">
                                ✓ Đã chấm xong
                              </span>
                            ) : (
                              <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300">
                                ⏳ Chờ giáo viên duyệt
                              </span>
                            )}
                          </div>
                          <div className="mt-0.5 text-xs text-slate-500 dark:text-slate-400 flex items-center gap-3">
                            <span>Đã làm: <strong>{student.completedQuestionCount}/{student.totalQuestionCount} câu ({percentCompleted}%)</strong></span>
                            {student.completedAt && (
                              <span>Nộp: {new Date(student.completedAt).toLocaleString("vi-VN")}</span>
                            )}
                          </div>
                        </div>
                      </div>

                      {/* Action Button */}
                      <div className="flex items-center gap-3 self-end sm:self-center">
                        <span
                          className={`inline-flex items-center gap-1 px-4 py-2 rounded-xl text-xs font-bold transition-all shadow-sm ${
                            isManager
                              ? "bg-slate-100 hover:bg-slate-200 text-slate-800 dark:bg-slate-800 dark:text-slate-200"
                              : isApproved
                              ? "bg-emerald-50 hover:bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300"
                              : "bg-indigo-600 hover:bg-indigo-700 text-white shadow-indigo-200 dark:shadow-none"
                          }`}
                        >
                          {isManager ? "Xem chi tiết bài làm →" : isApproved ? "Xem lại bài chấm →" : "Tiến hành chấm bài →"}
                        </span>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          )}
        </section>
      )}

      {/* ========================================================================= */}
      {/* LEVEL 3: QUESTION-BY-QUESTION GRADING WORKSPACE                           */}
      {/* ========================================================================= */}
      {selectedAssignment && selectedStudent && (
        <section className="space-y-6 animate-in fade-in duration-300">
          {/* Top Bar for Student Workspace */}
          <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-5 shadow-sm flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div>
              <button
                onClick={handleBackToStudents}
                className="inline-flex items-center gap-1.5 text-xs font-bold text-slate-500 hover:text-indigo-600 dark:text-slate-400 dark:hover:text-indigo-400 mb-1.5 transition-colors"
              >
                ← Quay lại danh sách học sinh ({selectedAssignment.title})
              </button>
              <div className="flex items-center gap-3">
                <h2 className="text-xl sm:text-2xl font-black text-slate-900 dark:text-slate-100">
                  {selectedStudent.fullName}
                </h2>
                {selectedStudent.teacherFinalReviewStatus === "Approved" ? (
                  <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-bold bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">
                    ✓ Đã duyệt toàn bộ kết quả
                  </span>
                ) : (
                  <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-bold bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300">
                    ⏳ Đang chờ giáo viên duyệt
                  </span>
                )}
              </div>
            </div>

            {/* Final Approve Action Button (Only Teacher with permissions) */}
            <div className="flex flex-col items-end gap-2 max-w-md">
              {canGrade && (
                <button
                  type="button"
                  onClick={() => { if (!finalBlockReason) setIsFinalApproveModalOpen(true); }}
                  disabled={Boolean(finalBlockReason)}
                  title={finalBlockReason ?? "Chốt kết quả cuối cùng của toàn bài"}
                  className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl font-bold text-xs sm:text-sm text-white bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed shadow-md transition-all"
                >
                  <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                  <span>{selectedStudent.teacherFinalReviewStatus === "Approved" ? "✓ Đã chốt kết quả toàn bài" : "Chốt kết quả toàn bài"}</span>
                </button>
              )}
              {canGrade && <p role="status" className="text-xs text-slate-500 dark:text-slate-400 text-right">{finalBlockReason ?? "Các câu đã được xử lý. Rà soát kết quả rồi chốt toàn bài."}</p>}
              {canGrade && finalResultLocked && <button type="button" disabled={gradingBusy} onClick={() => { setReopenReason(""); setIsReopenModalOpen(true); }} className="rounded-xl bg-amber-600 hover:bg-amber-500 px-4 py-2 font-bold text-sm text-white disabled:opacity-50">Mở lại để điều chỉnh</button>}
            </div>
          </div>

          {/* QUESTIONS LIST NAVIGATION TABS */}
          {isLoadingStudentQuestions ? (
            <div className="h-16 bg-white dark:bg-slate-900 rounded-2xl border border-slate-200 dark:border-slate-800 animate-pulse" />
          ) : studentQuestions.length === 0 ? (
            <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-8 text-center">
              <p className="text-sm text-slate-500 dark:text-slate-400">
                Chưa có dữ liệu câu trả lời nào được ghi nhận cho học sinh này trong bài tập.
              </p>
            </div>
          ) : (
            <div className="space-y-4">
              {/* Question selector tabs */}
              <div className="flex items-center gap-2 overflow-x-auto pb-2 scrollbar-thin">
                {studentQuestions.map((q, idx) => {
                  const isActive = idx === activeQuestionIndex;
                  const isCorrect = q.isCorrect;
                  const needsReview = questionGradingActions(q).needsReview;
                  const statusLabel = needsReview ? "Cần giáo viên chấm hoặc xác nhận"
                    : q.hasTeacherOverride ? "Giáo viên đã điều chỉnh điểm"
                    : isCorrect === true ? "Đáp án đúng" : isCorrect === false ? "Đáp án chưa đúng"
                    : questionGradingActions(q).reviewed ? "Giáo viên đã xác nhận" : "Chưa có kết quả";

                  return (
                    <button
                      key={q.attemptId}
                      title={statusLabel}
                      onClick={() => handleSelectQuestion(idx)}
                      className={`shrink-0 flex items-center gap-2 px-4 py-2.5 rounded-xl text-xs font-bold transition-all border ${
                        isActive
                          ? "bg-indigo-600 text-white border-indigo-600 shadow-md shadow-indigo-600/25"
                          : "bg-white dark:bg-slate-900 text-slate-700 dark:text-slate-300 border-slate-200 dark:border-slate-800 hover:border-indigo-300"
                      }`}
                    >
                      <span>Câu {q.questionOrderIndex ?? idx + 1}</span>
                      <span aria-label={statusLabel}>{needsReview ? "⚠️" : q.hasTeacherOverride ? "✏️" : isCorrect === true ? "✓" : isCorrect === false ? "✗" : questionGradingActions(q).reviewed ? "✓" : "…"}</span>
                    </button>
                  );
                })}
              </div>

              {/* MAIN QUESTION WORKSPACE: TWO COLUMNS */}
              {currentQuestion && (
                <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
                  {/* LEFT COLUMN (7 Cols): Question & Student Answer */}
                  <div className="lg:col-span-7 space-y-6">
                    {/* Question Card */}
                    <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-6 shadow-sm space-y-4">
                      <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
                        <div className="flex items-center gap-2">
                          <span className="w-8 h-8 rounded-lg bg-indigo-100 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300 font-extrabold flex items-center justify-center text-sm">
                            {currentQuestion.questionOrderIndex ?? activeQuestionIndex + 1}
                          </span>
                          <span className="text-xs font-bold uppercase tracking-wider text-slate-500">
                            Loại: {currentQuestion.questionType || "Trắc nghiệm"}
                          </span>
                        </div>
                        <div className="text-xs font-bold text-slate-600 dark:text-slate-300 bg-slate-100 dark:bg-slate-800 px-3 py-1 rounded-full">
                          Điểm câu hỏi: thang 10
                        </div>
                      </div>

                      {/* Question Text */}
                      <div className="text-base text-slate-900 dark:text-slate-100 leading-relaxed font-medium">
                        <RichMathText text={currentQuestion.questionText} />
                      </div>

                      {/* Multiple choice options if applicable */}
                      {currentQuestion.options && currentQuestion.options.length > 0 && (
                        <div className="space-y-2 pt-2">
                          <span className="text-xs font-bold uppercase text-slate-400">Các phương án lựa chọn:</span>
                          <div className="space-y-2">
                            {currentQuestion.options.map((opt) => (
                              <div
                                key={opt.optionId}
                                className={`flex items-start gap-2.5 p-3 rounded-xl border text-sm ${
                                  opt.isCorrect
                                    ? "bg-emerald-50/60 border-emerald-300 text-emerald-950 dark:bg-emerald-950/30 dark:border-emerald-700 dark:text-emerald-100 font-medium"
                                    : "bg-slate-50 dark:bg-slate-800/40 border-slate-200 dark:border-slate-800 text-slate-700 dark:text-slate-300"
                                }`}
                              >
                                <span className={`font-bold shrink-0 ${opt.isCorrect ? "text-emerald-600 dark:text-emerald-400" : ""}`}>
                                  {opt.optionLabel}.
                                </span>
                                <div className="flex-1">
                                  <RichMathText text={opt.optionText} />
                                </div>
                                {opt.isCorrect && (
                                  <span className="text-[11px] font-bold text-emerald-700 dark:text-emerald-400 uppercase tracking-wider shrink-0">
                                    [Đáp án đúng]
                                  </span>
                                )}
                              </div>
                            ))}
                          </div>
                        </div>
                      )}

                      {/* Standard correct answer for non-multiple-choice questions */}
                      {(!currentQuestion.options || currentQuestion.options.length === 0) && currentQuestion.correctAnswer && (
                        <div className="pt-2">
                          <span className="text-xs font-bold uppercase text-slate-400">Đáp án chuẩn của đề bài:</span>
                          <div className="mt-1.5 p-3 rounded-xl border border-emerald-200 bg-emerald-50/50 dark:border-emerald-800/60 dark:bg-emerald-950/30 text-emerald-900 dark:text-emerald-200 text-sm font-semibold">
                            <RichMathText text={currentQuestion.correctAnswer} />
                          </div>
                        </div>
                      )}
                    </div>

                    {/* Student's Answer & Reasoning Card */}
                    <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-6 shadow-sm space-y-4">
                      <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
                        <div className="flex items-center gap-2">
                          <span className="text-base">👤</span>
                          <h3 className="text-sm font-bold uppercase tracking-wider text-slate-800 dark:text-slate-200">
                            Bài làm của học sinh
                          </h3>
                        </div>
                        <div className="flex items-center gap-2">
                          {currentQuestion.isCorrect ? (
                            <span className="inline-flex items-center gap-1 px-3 py-1 rounded-full text-xs font-bold bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">
                              ✓ Học sinh làm đúng
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1 px-3 py-1 rounded-full text-xs font-bold bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-300">
                              ✗ Học sinh làm sai
                            </span>
                          )}
                        </div>
                      </div>

                      {/* Final Answer */}
                      <div className="space-y-1.5">
                        <span className="text-xs font-bold text-slate-500 uppercase">Đáp án học sinh đã chọn / nhập:</span>
                        <div className="p-3.5 bg-slate-50 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 rounded-xl font-semibold text-slate-900 dark:text-slate-100 text-base">
                          {resolvedStudentAnswer ? (
                            <RichMathText text={resolvedStudentAnswer} />
                          ) : (
                            <span className="text-slate-400 italic font-normal text-sm">(Chưa có câu trả lời)</span>
                          )}
                        </div>
                      </div>

                      {/* Reasoning Text / Solution Steps */}
                      <div className="space-y-1.5 pt-1">
                        <span className="text-xs font-bold text-slate-500 uppercase">Các bước giải & Lập luận của học sinh:</span>
                        {resolvedStudentReasoning ? (
                          <div className="p-4 bg-slate-50 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 rounded-xl text-sm text-slate-800 dark:text-slate-200 leading-relaxed font-sans whitespace-pre-wrap">
                            <RichMathText text={resolvedStudentReasoning} />
                          </div>
                        ) : (
                          <div className="p-3.5 bg-slate-50/60 dark:bg-slate-800/40 border border-dashed border-slate-200 dark:border-slate-700/80 rounded-xl text-xs text-slate-400 dark:text-slate-500 italic">
                            (Học sinh không đính kèm các bước giải hoặc lời giải thích bằng chữ cho câu hỏi này)
                          </div>
                        )}
                      </div>

                      {/* Scratchpad Button if attempt has attachments */}
                      <div className="pt-2 flex items-center justify-between">
                        {currentQuestion.hasAttachment ? (
                        <button
                          type="button"
                          onClick={() => {
                            setScratchpadAttemptId(currentQuestion.attemptId);
                            setIsScratchpadOpen(true);
                          }}
                          className="inline-flex items-center gap-2 px-3.5 py-2 rounded-xl text-xs font-bold bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-300 transition-colors"
                        >
                          <svg className="w-4 h-4 text-indigo-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
                          </svg>
                          <span>Xem bản nháp / vẽ tay (Scratchpad)</span>
                        </button>
                        ) : <span className="text-xs text-slate-500">Không có ảnh nháp đính kèm.</span>}

                        {canGrade && !finalResultLocked && (
                          <button
                            type="button"
                            disabled={gradingBusy}
                            onClick={() => { if (allowNavigation()) { setVoidImpactAcknowledged(false); setIsVoidModalOpen(true); } }}
                            className="inline-flex items-center gap-1.5 text-xs text-rose-600 hover:text-rose-700 dark:text-rose-400 font-bold"
                          >
                            Hủy câu hỏi do lỗi đề
                          </button>
                        )}
                      </div>

                      {/* Student Dispute / Challenge Notification */}
                      {currentQuestion.hasStudentReviewRequest && (
                        <div className="mt-3 p-4 bg-amber-50 dark:bg-amber-950/40 border border-amber-300 dark:border-amber-800 rounded-xl space-y-1">
                          <div className="flex items-center gap-2 text-xs font-bold text-amber-800 dark:text-amber-300">
                            <span>💬</span>
                            <span>Học sinh gửi yêu cầu xem xét lại câu hỏi này:</span>
                          </div>
                          <p className="text-xs text-amber-900 dark:text-amber-200 font-medium">
                            "{currentQuestion.studentReviewReason || "Học sinh yêu cầu giáo viên kiểm tra lại đáp án."}"
                          </p>
                        </div>
                      )}
                    </div>
                  </div>

                  {/* RIGHT COLUMN (5 Cols): AI Evaluation & Teacher Grading Workspace */}
                  <div className="lg:col-span-5 space-y-6">
                    {/* AI Assessment Breakdown Card */}
                    <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-6 shadow-sm space-y-4">
                      <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
                        <div className="flex items-center gap-2">
                          <span className="w-6 h-6 rounded-md bg-purple-100 dark:bg-purple-950 text-purple-600 dark:text-purple-400 flex items-center justify-center text-xs font-bold">
                            AI
                          </span>
                          <h3 className="text-sm font-bold uppercase tracking-wider text-slate-800 dark:text-slate-200">
                            Phân tích từ Trợ lý AI
                          </h3>
                        </div>
                        <span className="text-xs font-semibold px-2.5 py-0.5 rounded-full bg-purple-50 text-purple-700 dark:bg-purple-950 dark:text-purple-300">
                          {currentQuestion.isFallback ? "Quy tắc cơ bản (Fallback)" : "Mô hình Reasoning"}
                        </span>
                      </div>

                      {/* AI Metrics Grid */}
                      <div className="grid grid-cols-2 gap-3 text-xs">
                        <div className="p-3 bg-slate-50 dark:bg-slate-800/60 rounded-xl border border-slate-100 dark:border-slate-800">
                          <span className="text-slate-500 dark:text-slate-400 block mb-1">Độ tin cậy AI</span>
                          <span className="text-base font-extrabold text-slate-900 dark:text-slate-100">
                            {formatConfidence(currentQuestion.analysisConfidence, currentQuestion.isFallback)}
                          </span>
                        </div>
                        <div className="p-3 bg-slate-50 dark:bg-slate-800/60 rounded-xl border border-slate-100 dark:border-slate-800">
                          <span className="text-slate-500 dark:text-slate-400 block mb-1">Chỉ số chất lượng lập luận (không phải điểm bài)</span>
                          <span className="text-base font-extrabold text-slate-900 dark:text-slate-100">
                            {originalReasoningQuality !== null && originalReasoningQuality !== undefined
                              ? `${Math.round(Number(originalReasoningQuality))}/100`
                              : "N/A"}
                          </span>
                        </div>
                      </div>

                      {/* AI Feedback */}
                      {currentQuestion.methodDetected && <p className="text-xs text-slate-600 dark:text-slate-300">Phương pháp nhận diện: <strong>{currentQuestion.methodDetected}</strong></p>}
                      <p className="text-xs text-slate-500 dark:text-slate-400">Điểm câu hỏi: thang 10. Chỉ số lập luận: thang 100, không cộng vào điểm bài tập.</p>
                      {currentQuestion.analysisFeedback && (
                        <div className="space-y-1.5">
                          <span className="text-xs font-bold text-slate-500 dark:text-slate-300 uppercase">{getAnalysisFeedbackLabel(currentQuestion)}:</span>
                          <div className="p-3.5 bg-slate-50 dark:bg-slate-800/60 border border-slate-200 dark:border-slate-700 rounded-xl text-xs text-slate-700 dark:text-slate-300 leading-relaxed">
                            <RichMathText text={normalizeAITextLineBreaks(currentQuestion.analysisFeedback)} />
                          </div>
                        </div>
                      )}
                      <div className="space-y-2 rounded-xl border border-indigo-200 dark:border-indigo-800 bg-indigo-50/50 dark:bg-indigo-950/30 p-4">
                        <h4 className="text-xs font-bold text-indigo-700 dark:text-indigo-300">Lời giải đề xuất từ AI (AI Solution)</h4>
                        {currentQuestion.aiSolution?.trim()
                          ? <RichMathText text={normalizeAITextLineBreaks(currentQuestion.aiSolution)} />
                          : <p className="text-xs text-slate-500 dark:text-slate-400">Chưa có lời giải AI được lưu cho lượt làm này.</p>}
                      </div>
                    </div>

                    <details className="rounded-2xl border border-emerald-200 dark:border-emerald-900 bg-white dark:bg-slate-900 p-5 text-sm">
                      <summary className="cursor-pointer font-bold text-emerald-700 dark:text-emerald-300">Đáp án & lời giải tham khảo của giáo viên</summary>
                      <div className="mt-4 space-y-3">
                        <p className="text-xs text-slate-500">Lời giải mẫu không phải cách giải duy nhất. Công nhận phương pháp khác nếu các bước suy luận hợp lệ.</p>
                        <div><h4 className="text-xs font-bold mb-1">Đáp án chuẩn</h4><RichMathText text={resolvedTeacherAnswer} /></div>
                        <div><h4 className="text-xs font-bold mb-1">Lời giải tham khảo</h4><RichMathText text={currentQuestion.teacherSolution || "Chưa có lời giải tham khảo."} /></div>
                        {currentQuestion.expectedReasoning && <div><h4 className="text-xs font-bold mb-1">Gợi ý lập luận / tiêu chí</h4><RichMathText text={currentQuestion.expectedReasoning} /></div>}
                      </div>
                    </details>

                    {/* GRADING ACTION PANEL */}
                    <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-6 shadow-sm space-y-4">
                      <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
                        <div className="flex items-center gap-2">
                          <span className="text-base">📝</span>
                          <h3 className="text-sm font-bold uppercase tracking-wider text-slate-800 dark:text-slate-200">
                            {isManager ? "Thông tin Chấm điểm" : "Bảng Chấm điểm & Can thiệp"}
                          </h3>
                        </div>
                        {currentQuestion.hasTeacherOverride && (
                          <span className="text-[11px] font-bold text-purple-700 dark:text-purple-300 bg-purple-100 dark:bg-purple-950 px-2 py-0.5 rounded-full">
                            Đã can thiệp điểm
                          </span>
                        )}
                      </div>

                      {/* IF MANAGER: READ-ONLY DISPLAY */}
                      {!canGrade || finalResultLocked ? (
                        <div className="space-y-4">
                          <div className="p-4 bg-slate-50 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 rounded-xl space-y-2">
                            <div className="flex items-center justify-between text-xs">
                              <span className="text-slate-500">Điểm câu hỏi (thang 10):</span>
                              <span className="font-bold text-base text-indigo-600 dark:text-indigo-400">
                                {displayedQuestionGrade.awardedScore ?? "Chưa chấm"} / 10
                              </span>
                            </div>
                            {assignmentContribution && <p className="text-xs text-slate-500 dark:text-slate-400">Đóng góp vào tổng bài (thang 10): {assignmentContribution.awardedScore ?? "Chưa chấm"} / {assignmentContribution.maxScore} điểm. Mỗi câu có trọng số bằng nhau.</p>}
                            <div className="flex items-center justify-between text-xs">
                              <span className="text-slate-500">Kết luận:</span>
                              <span className="font-bold">
                                {currentQuestion.isCorrect == null ? "Chưa có kết luận" : currentQuestion.isCorrect ? "✓ Chính xác" : "✗ Chưa chính xác"}
                              </span>
                            </div>
                            {currentQuestion.teacherFeedback && (
                              <div className="text-xs pt-2 border-t border-slate-200 dark:border-slate-700">
                                <span className="text-slate-500 block mb-1">Nhận xét của giáo viên:</span>
                                <p className="font-medium text-slate-800 dark:text-slate-200">{currentQuestion.teacherFeedback}</p>
                              </div>
                            )}
                            {currentQuestion.overrideReason && (
                              <div className="text-xs pt-1">
                                <span className="text-slate-500 block mb-1">Lý do điều chỉnh:</span>
                                <p className="font-medium text-slate-700 dark:text-slate-300 italic">{currentQuestion.overrideReason}</p>
                              </div>
                            )}
                          </div>

                          <div className="p-3 bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800/60 rounded-xl text-xs text-amber-800 dark:text-amber-200 flex items-start gap-2">
                            <span>🔒</span>
                            <span>{finalResultLocked ? "Kết quả đã chốt, hiện chỉ xem. Muốn thay đổi, hãy chọn Mở lại để điều chỉnh và nhập lý do." : "Bạn không có quyền chấm hoặc điều chỉnh bài làm này."}</span>
                          </div>
                        </div>
                      ) : (
                        /* IF TEACHER: INTERACTIVE GRADING FORM */
                        <div className="space-y-4">
                          <p className="text-sm font-bold">Điểm câu hỏi (thang 10): {displayedQuestionGrade.awardedScore ?? "Chưa chấm"} / 10</p>
                          {assignmentContribution && <p className="text-xs text-slate-500 dark:text-slate-400">Đóng góp vào tổng bài (thang 10): {assignmentContribution.awardedScore ?? "Chưa chấm"} / {assignmentContribution.maxScore} điểm. Mỗi câu có trọng số bằng nhau.</p>}
                          {questionActions.reviewed && <p className="text-xs text-emerald-700 dark:text-emerald-300">✓ Giáo viên đã xác nhận/chấm câu này.</p>}
                          {!questionActions.canEdit && <p role="status" className="text-xs text-amber-700 dark:text-amber-300">Đang chờ dữ liệu phân tích. Tải lại kết quả trước khi chấm câu này.</p>}
                          {/* Quick Approve AI Button */}
                          {!showGradeForm && questionActions.canConfirm && (
                          <button
                            type="button"
                            onClick={() =>
                              approveMutation.mutate({
                                analysisId: currentQuestion.analysisId,
                                version: currentQuestion.overrideVersion ?? currentQuestion.evidence?.analysisOverrideVersion ?? 0,
                              })
                            }
                            disabled={gradingBusy || hasUnsavedGrade}
                            className="w-full py-2.5 px-4 rounded-xl text-xs font-bold bg-indigo-600 hover:bg-indigo-700 text-white disabled:opacity-50 transition-colors flex items-center justify-center gap-2"
                          >
                            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                            </svg>
                            <span>{approveMutation.isPending ? "Đang xác nhận…" : currentQuestion.hasStudentReviewRequest ? "Xác nhận sau khi xem xét yêu cầu" : "Xác nhận kết quả câu này"}</span>
                          </button>
                          )}
                          {currentQuestion.rubricGrade && !showGradeForm && <RubricGradeView grade={currentQuestion.rubricGrade} />}
                          {!showGradeForm && questionActions.canEdit && <button type="button" disabled={gradingBusy} onClick={() => { loadGradeForm(); setIsEditingGrade(true); }} className="text-xs font-bold text-indigo-600 dark:text-indigo-300 underline disabled:opacity-50">{questionActions.reviewed ? "Chỉnh sửa đánh giá đã lưu" : "Điều chỉnh điểm / chấm trực tiếp"}</button>}
                          {showGradeForm && <fieldset disabled={gradingBusy} className="space-y-4 disabled:opacity-60">
                          {gradeChangedOnServer && <p role="alert" className="text-xs text-amber-700 dark:text-amber-300">Kết quả trên server đã thay đổi. Nội dung đang sửa được giữ lại, nhưng không thể lưu đè. Hãy hủy thay đổi để xem phiên bản mới.</p>}
                          <p className="text-xs text-slate-500">{questionActions.needsManualGrade ? "Câu này chưa có điểm xác định. Giáo viên chấm trực tiếp dựa trên bài làm." : "Lưu ở đây thay cho thao tác xác nhận. Kết quả toàn bài sẽ cần được chốt lại sau khi chỉnh sửa."}</p>

                          {/* 1. Điểm số & Đúng/Sai */}
                          {rubricCriteria.length > 0 && <section className="rounded-xl border border-indigo-300 dark:border-indigo-700 p-4 space-y-3">
                            <h3 className="font-bold text-sm">Chấm từng tiêu chí (quy đổi thang 10)</h3>
                            <p className="text-sm text-slate-600 dark:text-slate-300">Công nhận mọi phương pháp hợp lệ đạt mục tiêu. Không trừ điểm chỉ vì khác lời giải mẫu.</p>
                            {rubricCriteria.map(c => <div key={c.criterionId} className="space-y-1">
                              <label className="flex justify-between gap-3 items-center text-sm font-semibold">{c.title}
                                <span className="flex items-center gap-2"><input aria-label={`Điểm tiêu chí ${c.title}`} type="number" min="0" max={Number((c.maxScore / (currentQuestion.maxScore ?? 10) * 10).toFixed(2))} step="0.01"
                                  value={rubricForm[c.criterionId]?.score ?? ''} className="w-24 rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-800 p-2"
                                  onChange={e => setRubricForm(prev => ({ ...prev, [c.criterionId]: { comment: prev[c.criterionId]?.comment || '', score: e.target.value } }))} />
                                  / {Number((c.maxScore / (currentQuestion.maxScore ?? 10) * 10).toFixed(2))}</span>
                              </label>
                              {c.description && <p className="text-sm text-slate-600 dark:text-slate-300">{c.description}</p>}
                              <input aria-label={`Nhận xét tiêu chí ${c.title}`} placeholder="Nhận xét tiêu chí (tùy chọn)" maxLength={2000} value={rubricForm[c.criterionId]?.comment || ''}
                                className="w-full rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-800 p-2 text-sm"
                                onChange={e => setRubricForm(prev => ({ ...prev, [c.criterionId]: { score: prev[c.criterionId]?.score || '', comment: e.target.value } }))} />
                            </div>)}
                            <p className="font-bold text-sm">Tổng điểm: {rubricResult.error ? 'Chưa chấm đủ' : Number((rubricResult.total / (currentQuestion.maxScore ?? 10) * 10).toFixed(2))} / 10</p>
                          </section>}
                          <p className="text-xs text-slate-500">Có thể chấm điểm từng phần khi đáp án chưa đúng; hãy nhập số điểm phù hợp với các bước giải hợp lệ.</p>
                          <div className="grid grid-cols-2 gap-3">
                            <div>
                              <label className="block text-xs font-bold text-slate-600 dark:text-slate-400 mb-1">
                                Điểm câu hỏi (thang 10)
                              </label>
                              <div className="flex items-center">
                                <input
                                  type="number"
                                  min={0}
                                  max={10}
                                  step={0.5}
                                  value={rubricCriteria.length ? (rubricResult.error ? "" : normalizeQuestionScore(rubricResult.total, currentQuestion.maxScore ?? 10).awardedScore ?? 0) : awardedScore}
                                  disabled={rubricCriteria.length > 0}
                                  onChange={(e) => setAwardedScore(Number(e.target.value))}
                                  className="w-full bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-3 py-2 text-sm font-bold text-indigo-600 dark:text-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-500"
                                />
                                <span className="ml-2 text-xs font-medium text-slate-400">/10</span>
                              </div>
                            </div>

                            <div>
                              <label className="block text-xs font-bold text-slate-600 dark:text-slate-400 mb-1">
                                Đánh giá kết quả
                              </label>
                              <div className="flex items-center gap-1 bg-slate-100 dark:bg-slate-800 p-1 rounded-xl">
                                <button
                                  type="button"
                                  onClick={() => {
                                    setIsCorrectVal(true);
                                    if (awardedScore === 0) {
                                      setAwardedScore(10);
                                    }
                                  }}
                                  className={`flex-1 py-1.5 text-xs font-bold rounded-lg transition-all ${
                                    isCorrectVal === true
                                      ? "bg-emerald-600 text-white shadow-sm"
                                      : "text-slate-600 dark:text-slate-400"
                                  }`}
                                >
                                  Đúng
                                </button>
                                <button
                                  type="button"
                                  onClick={() => {
                                    setIsCorrectVal(false);
                                    setAwardedScore(0);
                                  }}
                                  className={`flex-1 py-1.5 text-xs font-bold rounded-lg transition-all ${
                                    isCorrectVal === false
                                      ? "bg-rose-600 text-white shadow-sm"
                                      : "text-slate-600 dark:text-slate-400"
                                  }`}
                                >
                                  Sai
                                </button>
                              </div>
                            </div>
                          </div>

                          {/* 2. Điểm chất lượng lập luận & Phân loại lỗi */}
                          <details className="rounded-xl border border-slate-200 dark:border-slate-700 p-3">
                          <summary className="text-xs font-bold cursor-pointer">Chi tiết lập luận & phân loại lỗi</summary>
                          <div className="grid grid-cols-2 gap-3">
                            <div>
                              <div className="flex items-center justify-between mb-1">
                                <label className="text-xs font-bold text-slate-600 dark:text-slate-400">
                                  Chất lượng lập luận
                                </label>
                                <span className="text-xs font-bold text-indigo-600">{reasoningQuality}/100 (chỉ số lập luận)</span>
                              </div>
                              <input
                                type="range"
                                min={0}
                                max={100}
                                value={reasoningQuality}
                                onChange={(e) => setReasoningQuality(Number(e.target.value))}
                                className="w-full accent-indigo-600 cursor-pointer"
                              />
                            </div>

                            <div>
                              <label className="block text-xs font-bold text-slate-600 dark:text-slate-400 mb-1">
                                Phân loại lỗi
                              </label>
                              <select
                                value={errorTypeVal}
                                onChange={(e) => setErrorTypeVal(e.target.value as ErrorType)}
                                className="w-full bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-2.5 py-1.5 text-xs font-medium focus:outline-none"
                              >
                                <option value="None">Không có lỗi</option>
                                <option value="Knowledge">Lỗi kiến thức</option>
                                <option value="Skill">Lỗi kỹ năng tính toán</option>
                                <option value="Reasoning">Lỗi lập luận logic</option>
                                <option value="Presentation">Lỗi trình bày</option>
                                <option value="Behavior">Lỗi bất cẩn/hành vi</option>
                                <option value="Unknown">Lỗi khác</option>
                              </select>
                            </div>
                          </div>
                          </details>

                          {/* 3. Nhận xét của giáo viên cho học sinh */}
                          <div>
                            <label className="block text-xs font-bold text-slate-600 dark:text-slate-400 mb-1">
                              Lời nhận xét của giáo viên gửi học sinh <span className="text-rose-500">*</span>
                            </label>
                            <textarea
                              rows={2}
                              value={feedbackVal}
                              onChange={(e) => setFeedbackVal(e.target.value)}
                              placeholder="Nhập nhận xét động viên hoặc hướng dẫn sửa sai..."
                              className="w-full bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl p-2.5 text-xs focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                            />
                          </div>

                          {/* 4. Lý do điều chỉnh (OCC Reason) */}
                          <div>
                            <label className="block text-xs font-bold text-slate-600 dark:text-slate-400 mb-1">
                              Căn cứ chấm / lý do điều chỉnh {!questionActions.needsManualGrade && <span className="text-rose-500">*</span>}
                            </label>
                            <input
                              type="text"
                              value={overrideReasonVal}
                              onChange={(e) => setOverrideReasonVal(e.target.value)}
                              placeholder="Ghi rõ lý do can thiệp điểm..."
                              className="w-full bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl px-3 py-2 text-xs focus:ring-2 focus:ring-indigo-500 focus:outline-none mb-1.5"
                            />
                            {/* Preset Reason Pills */}
                            <div className="flex flex-wrap gap-1">
                              {[
                                "Học sinh lập luận đúng theo cách giải khác",
                                "AI chấm nhầm đáp án tương đương",
                                "Phát hiện bước biến đổi không hợp lệ",
                                "Chấm điểm từng phần theo tiêu chí của câu hỏi",
                              ].map((preset) => (
                                <button
                                  key={preset}
                                  type="button"
                                  onClick={() => setOverrideReasonVal(preset)}
                                  className="text-[10px] px-2 py-0.5 rounded-md bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 text-slate-600 dark:text-slate-400 transition-colors"
                                >
                                  + {preset}
                                </button>
                              ))}
                            </div>
                          </div>

                          {/* Save Button */}
                          <button
                            type="button"
                            onClick={handleSaveQuestionGrade}
                            disabled={gradingBusy || gradeChangedOnServer || !questionActions.canEdit}
                            className="w-full py-2.5 px-4 rounded-xl text-xs font-bold text-white bg-indigo-600 hover:bg-indigo-700 shadow-md shadow-indigo-600/25 active:scale-98 transition-all flex items-center justify-center gap-2"
                          >
                            {overrideMutation.isPending ? "Đang lưu..." : "Lưu đánh giá câu này"}
                          </button>
                          <button type="button" onClick={resetGradeForm} className="rounded-lg border border-slate-300 dark:border-slate-500 bg-slate-100 dark:bg-slate-700 px-4 py-2 text-xs font-bold text-slate-800 dark:text-slate-100 hover:bg-slate-200 dark:hover:bg-slate-600 focus-visible:outline-2 focus-visible:outline-indigo-400">{questionActions.needsManualGrade ? "Đặt lại form" : "Hủy thay đổi"}</button>
                          {hasUnsavedGrade && <p role="status" className="text-xs text-amber-700 dark:text-amber-300">Có thay đổi chưa lưu.</p>}
                          </fieldset>}
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              )}

              {/* Bottom Pagination for Questions */}
              <div className="flex items-center justify-between pt-4 border-t border-slate-200 dark:border-slate-800">
                <button
                  type="button"
                  disabled={activeQuestionIndex <= 0}
                  onClick={() => handleSelectQuestion(Math.max(0, activeQuestionIndex - 1))}
                  className="px-4 py-2 rounded-xl text-xs font-bold bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 disabled:opacity-40 disabled:cursor-not-allowed hover:bg-slate-50 transition-colors"
                >
                  ← Câu trước
                </button>

                <span className="text-xs font-bold text-slate-500">
                  Câu {currentQuestion?.questionOrderIndex ?? activeQuestionIndex + 1} · {activeQuestionIndex + 1} / {studentQuestions.length} câu có bài làm
                </span>

                <button
                  type="button"
                  disabled={activeQuestionIndex >= studentQuestions.length - 1}
                  onClick={() => handleSelectQuestion(Math.min(studentQuestions.length - 1, activeQuestionIndex + 1))}
                  className="px-4 py-2 rounded-xl text-xs font-bold bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 disabled:opacity-40 disabled:cursor-not-allowed hover:bg-slate-50 transition-colors"
                >
                  Câu tiếp theo →
                </button>
              </div>
            </div>
          )}
        </section>
      )}

      {/* SCRATCHPAD DRAWER MODAL */}
      <ScratchpadAttachmentDrawer
        attemptId={scratchpadAttemptId}
        isOpen={isScratchpadOpen}
        onClose={() => setIsScratchpadOpen(false)}
        studentName={selectedStudent?.fullName}
        questionText={currentQuestion?.questionText}
      />

      {/* VOID QUESTION CONFIRMATION MODAL */}
      {isVoidModalOpen && currentQuestion && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-in fade-in">
          <div className="bg-white dark:bg-slate-900 rounded-2xl max-w-md w-full p-6 border border-slate-200 dark:border-slate-800 shadow-2xl space-y-4">
            <h3 className="text-lg font-bold text-rose-600 dark:text-rose-400">
              Hủy câu hỏi do lỗi kỹ thuật
            </h3>
            <p className="text-xs text-slate-600 dark:text-slate-300">
              Thao tác ảnh hưởng TẤT CẢ học sinh trong bài tập này, không chỉ học sinh đang xem. Câu lỗi được tính trọn điểm, kể cả học sinh chưa làm. Các bài tập khác không bị hủy theo. Kết quả đã chốt sẽ được mở lại và cần chốt lại sau khi rà soát.
            </p>
            <div>
              <label className="block text-xs font-bold mb-1">Lý do hủy câu hỏi:</label>
              <textarea
                rows={2}
                value={voidReason}
                onChange={(e) => setVoidReason(e.target.value)}
                className="w-full bg-slate-50 dark:bg-slate-800 border rounded-xl p-2.5 text-xs focus:outline-none"
              />
            </div>
            <label className="flex items-center gap-2 text-xs cursor-pointer">
              <input
                type="checkbox"
                checked={quarantineInBank}
                onChange={(e) => setQuarantineInBank(e.target.checked)}
                className="rounded accent-rose-600"
              />
              <span>Cách ly câu hỏi trong ngân hàng câu hỏi để chỉnh sửa</span>
            </label>
            <label className="flex items-start gap-2 rounded-xl border border-amber-300 dark:border-amber-700 bg-amber-50 dark:bg-amber-950/40 p-3 text-xs text-amber-900 dark:text-amber-200">
              <input type="checkbox" checked={voidImpactAcknowledged} onChange={e => setVoidImpactAcknowledged(e.target.checked)} />
              <span>Tôi xác nhận phạm vi toàn bộ học sinh và đồng ý mở lại các kết quả đã chốt bị ảnh hưởng.</span>
            </label>
            <div className="flex items-center justify-end gap-2 pt-2">
              <button
                type="button"
                onClick={() => setIsVoidModalOpen(false)}
                className="px-4 py-2 text-xs font-bold text-slate-600 dark:text-slate-300 hover:bg-slate-100 rounded-xl"
              >
                Hủy bỏ
              </button>
              <button
                type="button"
                onClick={() => voidQuestionMutation.mutate()}
                disabled={gradingBusy || !voidImpactAcknowledged}
                className="px-4 py-2 text-xs font-bold text-white bg-rose-600 hover:bg-rose-700 rounded-xl"
              >
                {voidQuestionMutation.isPending ? "Đang xử lý..." : "Xác nhận hủy câu hỏi"}
              </button>
            </div>
          </div>
        </div>
      )}

      {isReopenModalOpen && selectedStudent && selectedAssignment && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm">
          <div role="dialog" aria-modal="true" aria-label="Mở lại kết quả đã chốt" className="max-w-md w-full rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 p-6 space-y-4">
            <h3 className="text-lg font-bold text-amber-700 dark:text-amber-300">Mở lại để điều chỉnh</h3>
            <p className="text-sm">Mở lại kết quả của <strong>{selectedStudent.fullName}</strong> trong <strong>{selectedAssignment.title}</strong>. Bài làm đã nộp được giữ nguyên; đây không phải yêu cầu học sinh làm lại. Sau khi sửa đánh giá, cần chốt lại toàn bài.</p>
            <label className="block text-sm font-bold">Lý do mở lại (bắt buộc)
              <textarea rows={3} maxLength={1000} value={reopenReason} onChange={e => setReopenReason(e.target.value)} className="mt-2 w-full rounded-xl border border-slate-300 dark:border-slate-600 bg-slate-50 dark:bg-slate-800 p-3 text-sm text-slate-900 dark:text-slate-100" />
            </label>
            <div className="flex justify-end gap-2">
              <button type="button" disabled={gradingBusy} onClick={() => setIsReopenModalOpen(false)} className="rounded-lg border border-slate-300 dark:border-slate-500 bg-slate-100 dark:bg-slate-700 px-4 py-2 text-sm">Hủy</button>
              <button type="button" disabled={gradingBusy || reopenReason.trim().length < 5} onClick={() => reopenMutation.mutate()} className="rounded-lg bg-amber-600 px-4 py-2 text-sm font-bold text-white disabled:opacity-50">Xác nhận mở lại</button>
            </div>
          </div>
        </div>
      )}

      {/* FINAL APPROVE ASSIGNMENT MODAL */}
      {isFinalApproveModalOpen && selectedStudent && selectedAssignment && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-in fade-in">
          <div className="bg-white dark:bg-slate-900 rounded-2xl max-w-md w-full p-6 border border-slate-200 dark:border-slate-800 shadow-2xl space-y-4">
            <h3 className="text-lg font-bold text-emerald-600 dark:text-emerald-400">
              Duyệt & Chốt kết quả bài tập
            </h3>
            <p className="text-xs text-slate-600 dark:text-slate-300">
              Bạn đang chốt kết quả bài tập <strong>"{selectedAssignment.title}"</strong> cho học sinh <strong>"{selectedStudent.fullName}"</strong>. Trạng thái sẽ được cập nhật thành Đã duyệt (Approved) và thông báo tới học sinh.
            </p>
            <p role="status" className="text-xs text-slate-500">{finalBlockReason ?? "Không còn câu chưa nộp, đang phân tích hoặc cần giáo viên xử lý. Server sẽ kiểm tra lại trước khi chốt."}</p>
            <div>
              <label className="block text-xs font-bold mb-1">Ghi chú tổng kết của giáo viên:</label>
              <textarea
                rows={3}
                value={finalApproveNote}
                onChange={(e) => setFinalApproveNote(e.target.value)}
                className="w-full bg-slate-50 dark:bg-slate-800 border rounded-xl p-2.5 text-xs focus:outline-none"
              />
            </div>
            <div className="flex items-center justify-end gap-2 pt-2">
              <button
                type="button"
                onClick={() => setIsFinalApproveModalOpen(false)}
                className="px-4 py-2 text-xs font-bold text-slate-600 dark:text-slate-300 hover:bg-slate-100 rounded-xl"
              >
                Đóng
              </button>
              <button
                type="button"
                onClick={() => finalApproveMutation.mutate()}
                disabled={Boolean(finalBlockReason)}
                className="px-4 py-2 text-xs font-bold text-white bg-emerald-600 hover:bg-emerald-700 rounded-xl shadow-md"
              >
                {finalApproveMutation.isPending ? "Đang duyệt..." : "Xác nhận duyệt bài"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
