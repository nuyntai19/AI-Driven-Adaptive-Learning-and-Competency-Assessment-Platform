import { useState, useEffect, useMemo } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { organizationApi } from "../api/organizationApi";
import { knowledgeGraphApi } from "../api/knowledgeGraphApi";
import {
  useQuestion,
  useCreateQuestion,
  useUpdateQuestion,
  useActivateQuestion,
  useArchiveQuestion,
  useDeleteQuestion,
} from "../features/questions/useQuestions";
import type {
  QuestionType,
  QuestionAnswerEvaluationMode,
  CreateQuestionRequest,
  UpdateQuestionRequest,
  QuestionOption,
} from "../types/questions";
import type { TeacherDto } from "../types/organization";
import { MathInputToolbar } from "../components/math/MathInputToolbar";
import { MathFormulaPreview } from "../components/math/MathFormulaPreview";
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

// =============================================================================
// 1. CENTER MANAGER DARK SAAS QUESTION EDITOR (GATE 6B)
// =============================================================================

function CenterManagerQuestionEditorView() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const isEditMode = !!id;

  const hasPermission = useAuthStore((state) => state.hasPermission);
  const canCreate = hasPermission(permissions.questionsCreate);
  const canUpdate = hasPermission(permissions.questionsUpdate);
  const canPublish = hasPermission(permissions.questionsPublish);
  const canDelete = hasPermission(permissions.questionsDelete);
  const canReadSubjects = hasPermission(permissions.subjectsRead);
  const canReadNodes = hasPermission(permissions.nodesRead);
  const canReadTeachers = hasPermission(permissions.teachersRead);

  // Form State
  const [formData, setFormData] = useState<CreateQuestionRequest>({
    teacherId: null,
    subjectId: "",
    primaryTopicNodeId: "",
    questionType: "MultipleChoice",
    difficulty: 3,
    questionText: "",
    maxScore: 1,
    estimatedTimeSeconds: 60,
    reasoningRequired: true,
    languageCode: "vi",
    answerEvaluationMode: "TextExact",
    options: [
      { optionText: "", isCorrect: true, optionLabel: "A", orderIndex: 0 },
      { optionText: "", isCorrect: false, optionLabel: "B", orderIndex: 1 },
    ],
    correctAnswer: "",
    solution: "",
    expectedReasoning: "",
    gradingCriteria: {
      schemaVersion: "1.0",
      requiredIdeas: [],
      commonErrors: [],
      scoringNotes: "",
    },
  });

  const [activeMathField, setActiveMathField] = useState<"questionText" | "correctAnswer" | "solution" | null>(null);
  const [showMathToolbar, setShowMathToolbar] = useState<boolean>(false);

  // Operational feedback states
  const [formError, setFormError] = useState<{ message: string; traceId?: string | null } | null>(null);
  const [concurrencyConflict, setConcurrencyConflict] = useState<boolean>(false);
  const [dialogAction, setDialogAction] = useState<"activate" | "archive" | "delete" | null>(null);

  // Essay grading criteria helper state
  const [requiredIdeaInput, setRequiredIdeaInput] = useState("");
  const [commonErrorInput, setCommonErrorInput] = useState("");

  // Existing question query for edit mode
  const {
    data: questionData,
    isLoading: isLoadingQuestion,
    isError: isErrorQuestion,
    error: questionError,
    refetch: refetchQuestion,
  } = useQuestion(id || "");

  const createMutation = useCreateQuestion();
  const updateMutation = useUpdateQuestion();
  const activateMutation = useActivateQuestion();
  const archiveMutation = useArchiveQuestion();
  const deleteMutation = useDeleteQuestion();

  // Populate data when in edit mode
  useEffect(() => {
    if (isEditMode && questionData?.data) {
      const q = questionData.data;
      setFormData({
        teacherId: q.createdByTeacherId || null,
        subjectId: q.subjectId,
        primaryTopicNodeId: q.primaryTopicNodeId || "",
        questionType: q.questionType,
        difficulty: q.difficulty,
        questionText: q.questionText,
        maxScore: q.maxScore,
        estimatedTimeSeconds: q.estimatedTimeSeconds,
        reasoningRequired: q.reasoningRequired || false,
        languageCode: q.languageCode || "vi",
        answerEvaluationMode:
          q.answerEvaluationMode ||
          (q.questionType === "MultipleChoice"
            ? "TextExact"
            : q.questionType === "Essay"
            ? "Manual"
            : "TextExact"),
        options: (q.options || []).map((opt: any) => ({
          optionLabel: opt.optionLabel || opt.label || "A",
          optionText: opt.optionText || opt.text || "",
          isCorrect: !!opt.isCorrect,
          orderIndex: opt.orderIndex ?? 0,
        })),
        correctAnswer: q.correctAnswer || "",
        solution: q.solution || "",
        expectedReasoning: q.expectedReasoning || "",
        gradingCriteria: q.gradingCriteria || {
          schemaVersion: "1.0",
          requiredIdeas: [],
          commonErrors: [],
          scoringNotes: "",
        },
      });
    }
  }, [isEditMode, questionData]);

  // Subjects query
  const {
    data: subjectsData,
    isLoading: isLoadingSubjects,
    isError: isErrorSubjects,
    error: subjectsError,
    refetch: refetchSubjects,
  } = useQuery({
    queryKey: ["subjects", "active-for-question-editor"],
    queryFn: () => organizationApi.listSubjects(true),
    enabled: canReadSubjects,
  });

  // Nodes query for the selected subject
  const {
    data: nodesData,
    isLoading: isLoadingNodes,
    isError: isErrorNodes,
    error: nodesError,
    refetch: refetchNodes,
  } = useQuery({
    queryKey: ["knowledge-nodes", formData.subjectId],
    queryFn: () => knowledgeGraphApi.listNodes(formData.subjectId),
    enabled: canReadNodes && !!formData.subjectId,
  });

  // Teacher selector for CenterManager in Create Mode (server-side pagination, search, cachedTeachers)
  const [teacherSearch, setTeacherSearch] = useState("");
  const [teacherPage, setTeacherPage] = useState(1);
  const [cachedTeachers, setCachedTeachers] = useState<Map<string, TeacherDto>>(new Map());

  const {
    data: teachersData,
    isLoading: isLoadingTeachers,
    isError: isErrorTeachers,
    error: teachersError,
    refetch: refetchTeachers,
  } = useQuery({
    queryKey: ["teachers", "active-for-question-editor", teacherPage, teacherSearch],
    queryFn: () =>
      organizationApi.listTeachers({
        page: teacherPage,
        pageSize: 20,
        search: teacherSearch.trim() || undefined,
        status: "Active",
      }),
    enabled: !isEditMode && canReadTeachers,
  });

  // Cache loaded teachers to preserve selection across pagination / search
  useEffect(() => {
    if (teachersData?.data) {
      setCachedTeachers((prev) => {
        const next = new Map(prev);
        for (const t of teachersData.data) {
          next.set(t.teacherId, t);
        }
        return next;
      });
    }
  }, [teachersData?.data]);

  const teacherList = useMemo(() => {
    const map = new Map(cachedTeachers);
    if (teachersData?.data) {
      for (const t of teachersData.data) {
        map.set(t.teacherId, t);
      }
    }
    return Array.from(map.values());
  }, [cachedTeachers, teachersData?.data]);

  // In edit mode: resolve teacher details from createdByTeacherId if available
  const createdByTeacherId = questionData?.data?.createdByTeacherId;
  const { data: teacherDetailData } = useQuery({
    queryKey: ["teacher-detail-for-question", createdByTeacherId],
    queryFn: () => organizationApi.getTeacher(createdByTeacherId!),
    enabled: isEditMode && canReadTeachers && !!createdByTeacherId,
    staleTime: 60_000,
  });

  // State machine & Read-only determination
  const currentStatus = questionData?.data?.status ?? "Draft";
  const isDraft = !isEditMode || currentStatus === "Draft";
  const isReadOnly = isEditMode && (!isDraft || !canUpdate);

  // Composite capability fail-closed check for Create Mode
  const missingCreateCapabilities = useMemo(() => {
    if (isEditMode) return [];
    const missing: string[] = [];
    if (!canCreate) missing.push("curriculum.questions.create");
    if (!canReadSubjects) missing.push("knowledge.subjects.read");
    if (!canReadNodes) missing.push("knowledge.nodes.read");
    if (!canReadTeachers) missing.push("organization.teachers.read");
    return missing;
  }, [isEditMode, canCreate, canReadSubjects, canReadNodes, canReadTeachers]);

  // Handlers
  const handleInputChange = (field: keyof CreateQuestionRequest, value: any) => {
    if (isReadOnly) return;

    if (field === "questionType") {
      const qType = value as QuestionType;
      let newMode: QuestionAnswerEvaluationMode = "TextExact";
      if (qType === "MultipleChoice") {
        newMode = "TextExact";
      } else if (qType === "Essay") {
        newMode = "Manual";
      } else if (qType === "ShortAnswer") {
        newMode =
          formData.answerEvaluationMode === "TextExact" ||
          formData.answerEvaluationMode === "NumericRational" ||
          formData.answerEvaluationMode === "Manual"
            ? formData.answerEvaluationMode
            : "TextExact";
      }
      setFormData((prev) => ({
        ...prev,
        questionType: qType,
        answerEvaluationMode: newMode,
      }));
      return;
    }

    if (field === "subjectId") {
      // Reset node when subject changes
      setFormData((prev) => ({
        ...prev,
        subjectId: value,
        primaryTopicNodeId: "",
      }));
      return;
    }

    setFormData((prev) => ({ ...prev, [field]: value }));
  };

  // Option handlers for MultipleChoice
  const handleOptionChange = (index: number, field: keyof QuestionOption, value: any) => {
    if (isReadOnly) return;
    const newOptions = [...(formData.options || [])];
    newOptions[index] = { ...newOptions[index], [field]: value };

    if (field === "isCorrect" && value === true) {
      newOptions.forEach((opt, i) => {
        if (i !== index) opt.isCorrect = false;
      });
    }

    setFormData((prev) => ({ ...prev, options: newOptions }));
  };

  const addOption = () => {
    if (isReadOnly) return;
    const labels = ["A", "B", "C", "D", "E", "F", "G", "H"];
    const currentLength = formData.options?.length || 0;
    if (currentLength >= 8) return;

    const newOptions = [...(formData.options || [])];
    newOptions.push({
      optionText: "",
      isCorrect: false,
      optionLabel: labels[currentLength] || String.fromCharCode(65 + currentLength),
      orderIndex: currentLength,
    });
    setFormData((prev) => ({ ...prev, options: newOptions }));
  };

  const removeOption = (index: number) => {
    if (isReadOnly) return;
    if ((formData.options?.length || 0) <= 2) return;
    const newOptions = [...(formData.options || [])];
    newOptions.splice(index, 1);

    const labels = ["A", "B", "C", "D", "E", "F", "G", "H"];
    newOptions.forEach((opt, i) => {
      opt.optionLabel = labels[i] || String.fromCharCode(65 + i);
      opt.orderIndex = i;
    });

    setFormData((prev) => ({ ...prev, options: newOptions }));
  };

  // Grading criteria item helpers for Essay
  const addRequiredIdea = () => {
    const trimmed = requiredIdeaInput.trim();
    if (!trimmed) return;
    setFormData((prev) => ({
      ...prev,
      gradingCriteria: {
        schemaVersion: "1.0",
        requiredIdeas: [...(prev.gradingCriteria?.requiredIdeas || []), trimmed],
        commonErrors: prev.gradingCriteria?.commonErrors || [],
        scoringNotes: prev.gradingCriteria?.scoringNotes || "",
      },
    }));
    setRequiredIdeaInput("");
  };

  const removeRequiredIdea = (index: number) => {
    setFormData((prev) => ({
      ...prev,
      gradingCriteria: {
        schemaVersion: "1.0",
        requiredIdeas: (prev.gradingCriteria?.requiredIdeas || []).filter((_, i) => i !== index),
        commonErrors: prev.gradingCriteria?.commonErrors || [],
        scoringNotes: prev.gradingCriteria?.scoringNotes || "",
      },
    }));
  };

  const addCommonError = () => {
    const trimmed = commonErrorInput.trim();
    if (!trimmed) return;
    setFormData((prev) => ({
      ...prev,
      gradingCriteria: {
        schemaVersion: "1.0",
        requiredIdeas: prev.gradingCriteria?.requiredIdeas || [],
        commonErrors: [...(prev.gradingCriteria?.commonErrors || []), trimmed],
        scoringNotes: prev.gradingCriteria?.scoringNotes || "",
      },
    }));
    setCommonErrorInput("");
  };

  const removeCommonError = (index: number) => {
    setFormData((prev) => ({
      ...prev,
      gradingCriteria: {
        schemaVersion: "1.0",
        requiredIdeas: prev.gradingCriteria?.requiredIdeas || [],
        commonErrors: (prev.gradingCriteria?.commonErrors || []).filter((_, i) => i !== index),
        scoringNotes: prev.gradingCriteria?.scoringNotes || "",
      },
    }));
  };

  // Math symbol insert handler
  const handleInsertMath = (latex: string) => {
    if (!activeMathField || isReadOnly) return;
    const currentVal = (formData[activeMathField] as string) || "";
    handleInputChange(activeMathField, currentVal + " " + latex);
  };

  // Form submission: Create vs Update (Strict contract compliance: no teacherId / subjectId in Update)
  const handleSave = () => {
    if (isReadOnly) return;
    setFormError(null);
    setConcurrencyConflict(false);

    // Validation
    if (!formData.questionText.trim()) {
      setFormError({ message: "Vui lòng nhập nội dung câu hỏi." });
      return;
    }
    if (!formData.primaryTopicNodeId) {
      setFormError({ message: "Vui lòng chọn nút kiến thức chính cho câu hỏi." });
      return;
    }

    if (!isEditMode) {
      // Create Mode validation
      if (!formData.subjectId) {
        setFormError({ message: "Vui lòng chọn môn học." });
        return;
      }
      if (!formData.teacherId) {
        setFormError({ message: "Vui lòng chỉ định giáo viên phụ trách câu hỏi." });
        return;
      }

      const createPayload: CreateQuestionRequest = {
        ...formData,
        options:
          formData.questionType === "MultipleChoice"
            ? formData.options?.map((o) => ({
                optionLabel: o.optionLabel,
                optionText: o.optionText,
                isCorrect: o.isCorrect,
                orderIndex: o.orderIndex,
              }))
            : undefined,
        correctAnswer: formData.questionType === "Essay" ? undefined : formData.correctAnswer,
      };

      createMutation.mutate(createPayload, {
        onSuccess: (res) => {
          navigate(`/quan-ly/cau-hoi/${res.data.questionId}`);
        },
        onError: (err: any) => {
          const details = extractProblemDetails(err);
          setFormError({
            message: mapSafeOperationalError(err, "Không thể tạo mới câu hỏi."),
            traceId: details.traceId,
          });
        },
      });
    } else {
      // Update Mode: Strict contract - NEVER send teacherId or subjectId!
      if (!questionData?.data?.rowVersion) {
        setFormError({
          message: "Không thể xác định phiên bản đồng thời (RowVersion) của câu hỏi. Vui lòng làm mới trang để thử lại.",
        });
        refetchQuestion();
        return;
      }

      const updatePayload: UpdateQuestionRequest = {
        primaryTopicNodeId: formData.primaryTopicNodeId,
        questionType: formData.questionType,
        difficulty: formData.difficulty,
        questionText: formData.questionText.trim(),
        correctAnswer: formData.correctAnswer?.trim() || undefined,
        solution: formData.solution?.trim() || undefined,
        expectedReasoning: formData.expectedReasoning?.trim() || undefined,
        gradingCriteria: formData.gradingCriteria,
        maxScore: formData.maxScore,
        estimatedTimeSeconds: formData.estimatedTimeSeconds,
        reasoningRequired: formData.reasoningRequired,
        languageCode: formData.languageCode,
        answerEvaluationMode: formData.answerEvaluationMode,
        options:
          formData.questionType === "MultipleChoice"
            ? formData.options?.map((o) => ({
                optionLabel: o.optionLabel,
                optionText: o.optionText,
                isCorrect: o.isCorrect,
                orderIndex: o.orderIndex,
              }))
            : undefined,
        knowledgeMappings: questionData?.data?.knowledgeMappings,
        rowVersion: questionData.data.rowVersion,
      };

      updateMutation.mutate(
        { id: id!, data: updatePayload },
        {
          onSuccess: (res) => {
            queryClient.setQueryData(["questions", id], res);
            setFormError(null);
            navigate("/quan-ly/cau-hoi");
          },
          onError: (err: any) => {
            const details = extractProblemDetails(err);
            if (isConcurrencyConflictError(err)) {
              setConcurrencyConflict(true);
            } else {
              setFormError({
                message: mapSafeOperationalError(err, "Không thể cập nhật câu hỏi."),
                traceId: details.traceId,
              });
            }
          },
        }
      );
    }
  };

  // State machine dialog confirm handler
  const handleConfirmDialogAction = () => {
    if (!id || !questionData?.data || !dialogAction) return;

    if (dialogAction === "activate") {
      activateMutation.mutate(
        { id, data: { rowVersion: questionData.data.rowVersion } },
        {
          onSuccess: () => {
            refetchQuestion();
            setDialogAction(null);
          },
          onError: (err: any) => {
            const details = extractProblemDetails(err);
            setFormError({
              message: mapSafeOperationalError(err, "Không thể kích hoạt câu hỏi."),
              traceId: details.traceId,
            });
            setDialogAction(null);
          },
        }
      );
    } else if (dialogAction === "archive") {
      archiveMutation.mutate(
        { id, data: { rowVersion: questionData.data.rowVersion } },
        {
          onSuccess: () => {
            refetchQuestion();
            setDialogAction(null);
          },
          onError: (err: any) => {
            const details = extractProblemDetails(err);
            setFormError({
              message: mapSafeOperationalError(err, "Không thể lưu trữ câu hỏi."),
              traceId: details.traceId,
            });
            setDialogAction(null);
          },
        }
      );
    } else if (dialogAction === "delete") {
      // DELETE /api/v1/questions/{id} without request body
      deleteMutation.mutate(id, {
        onSuccess: () => {
          navigate("/quan-ly/cau-hoi");
        },
        onError: (err: any) => {
          const details = extractProblemDetails(err);
          setFormError({
            message: mapSafeOperationalError(err, "Không thể xóa câu hỏi bản nháp. Vui lòng kiểm tra ràng buộc."),
            traceId: details.traceId,
          });
          setDialogAction(null);
        },
      });
    }
  };

  // Edit Mode Loading / Error States
  if (isEditMode && isLoadingQuestion) {
    return (
      <CenterManagerThemeScope data-actor="center-manager">
        <div className="space-y-6 p-6 lg:p-8">
          <Skeleton className="h-10 w-64 rounded-lg" />
          <Skeleton className="h-96 w-full rounded-2xl" />
        </div>
      </CenterManagerThemeScope>
    );
  }

  if (isEditMode && isErrorQuestion) {
    return (
      <CenterManagerThemeScope data-actor="center-manager">
        <div className="p-6 lg:p-8">
          <SafeErrorPanel
            error={questionError}
            fallback="Không thể tải thông tin câu hỏi từ hệ thống."
            onRetry={() => refetchQuestion()}
          />
        </div>
      </CenterManagerThemeScope>
    );
  }

  // Composite Capability Fail-Closed Guard (when creating question)
  if (!isEditMode && missingCreateCapabilities.length > 0) {
    return (
      <CenterManagerThemeScope data-actor="center-manager">
        <div className="p-6 lg:p-8 space-y-6">
          <PageHeader
            eyebrow="NỘI DUNG HỌC THUẬT"
            title="Tạo Câu hỏi Mới"
            breadcrumbs={[
              { label: "CenterManager", href: "/quan-ly/tong-quan-trung-tam" },
              { label: "Ngân hàng câu hỏi", href: "/quan-ly/cau-hoi" },
              { label: "Tạo mới" },
            ]}
          />
          <section role="alert" className="rounded-2xl border border-rose-500/30 bg-rose-500/10 p-6 space-y-3">
            <h2 className="text-base font-semibold text-rose-200">Không đủ điều kiện phân quyền (Fail-closed Guard)</h2>
            <p className="text-sm text-rose-300">
              Để tạo mới câu hỏi với tư cách CenterManager, tài khoản của bạn bắt buộc phải có đầy đủ 4 quyền sau:
            </p>
            <ul className="list-disc pl-5 text-xs font-mono text-rose-200 space-y-1">
              <li>curriculum.questions.create</li>
              <li>knowledge.subjects.read</li>
              <li>knowledge.nodes.read</li>
              <li>organization.teachers.read</li>
            </ul>
            <p className="text-xs text-rose-300/80 pt-2">
              Các quyền còn thiếu: <strong className="font-mono text-rose-100">{missingCreateCapabilities.join(", ")}</strong>. Vui lòng liên hệ Quản trị viên để được cấp quyền.
            </p>
          </section>
        </div>
      </CenterManagerThemeScope>
    );
  }

  return (
    <CenterManagerThemeScope data-actor="center-manager">
      <div className="space-y-6 p-6 lg:p-8">
        <PageHeader
          eyebrow="QUẢN LÝ CÂU HỎI"
          title={isEditMode ? "Chỉnh sửa Câu hỏi" : "Tạo Câu hỏi Mới"}
          description="Thiết lập nội dung câu hỏi, công thức toán KaTeX, độ khó, điểm số và chế độ chấm điểm AI tương ứng."
          breadcrumbs={[
            { label: "CenterManager", href: "/quan-ly/tong-quan-trung-tam" },
            { label: "Ngân hàng câu hỏi", href: "/quan-ly/cau-hoi" },
            { label: isEditMode ? questionData?.data?.questionId.slice(0, 8) || "Chi tiết" : "Tạo mới" },
          ]}
          actions={
            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                onClick={() => navigate("/quan-ly/cau-hoi")}
                className="cm-secondary-button text-xs"
              >
                Hủy / Quay lại
              </button>

              {/* State Machine Transition Actions */}
              {isEditMode && isDraft && canPublish && (
                <button
                  type="button"
                  onClick={() => setDialogAction("activate")}
                  className="px-3 py-1.5 rounded-lg text-xs font-semibold text-emerald-300 bg-emerald-500/10 border border-emerald-500/30 hover:bg-emerald-500/20 transition"
                >
                  Kích hoạt
                </button>
              )}

              {isEditMode && currentStatus === "Active" && canPublish && (
                <button
                  type="button"
                  onClick={() => setDialogAction("archive")}
                  className="px-3 py-1.5 rounded-lg text-xs font-semibold text-slate-300 bg-slate-500/10 border border-slate-500/30 hover:bg-slate-500/20 transition"
                >
                  Lưu trữ
                </button>
              )}

              {isEditMode && currentStatus === "Archived" && canPublish && (
                <button
                  type="button"
                  onClick={() => setDialogAction("activate")}
                  className="px-3 py-1.5 rounded-lg text-xs font-semibold text-emerald-300 bg-emerald-500/10 border border-emerald-500/30 hover:bg-emerald-500/20 transition"
                >
                  Kích hoạt lại
                </button>
              )}

              {/* Delete Draft Button */}
              {isEditMode && isDraft && canDelete && (
                <button
                  type="button"
                  onClick={() => setDialogAction("delete")}
                  className="px-3 py-1.5 rounded-lg text-xs font-semibold text-rose-300 bg-rose-500/10 border border-rose-500/30 hover:bg-rose-500/20 transition"
                >
                  Xóa câu hỏi
                </button>
              )}

              {/* Save Button (Disabled if Read-Only) */}
              {!isReadOnly && (
                <button
                  type="button"
                  id="btn-save-question"
                  onClick={handleSave}
                  disabled={createMutation.isPending || updateMutation.isPending}
                  className="cm-primary-button text-xs"
                >
                  {createMutation.isPending || updateMutation.isPending
                    ? "Đang lưu…"
                    : isEditMode
                    ? "Lưu thay đổi"
                    : "Tạo câu hỏi"}
                </button>
              )}
            </div>
          }
        />

        {/* OCC Concurrency Banner */}
        {concurrencyConflict && (
          <ConcurrencyBanner
            onReload={() => {
              setConcurrencyConflict(false);
              refetchQuestion();
            }}
          />
        )}

        {/* Form Error Alert with Trace ID */}
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
                <strong>Chế độ chỉ xem:</strong>{" "}
                {currentStatus !== "Draft"
                  ? `Câu hỏi đang ở trạng thái "${currentStatus}". Theo quy tắc nghiệp vụ, câu hỏi đã Kích hoạt hoặc Lưu trữ là bất biến đối với nội dung.`
                  : "Tài khoản của bạn chỉ có quyền đọc (thiếu quyền curriculum.questions.update)."}
              </span>
            </div>
            <StatusBadge status={currentStatus} />
          </div>
        )}

        {/* Main Editor Form Card */}
        <div className="rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] p-6 lg:p-8 space-y-6 shadow-xl">
          {/* Metadata Section: Subject, Topic Node, Teacher */}
          <div className="grid grid-cols-1 gap-5 md:grid-cols-3 border-b border-[var(--cm-border-subtle)] pb-6">
            {/* Subject Selector (Immutable in Edit Mode) */}
            <div>
              <label htmlFor="question-subject-select" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                Môn học <span className="text-rose-400">*</span>
              </label>
              {isEditMode ? (
                <input
                  type="text"
                  disabled
                  value={
                    subjectsData?.data?.find((s) => s.subjectId === formData.subjectId)
                      ? `${subjectsData?.data?.find((s) => s.subjectId === formData.subjectId)?.subjectName} (${subjectsData?.data?.find((s) => s.subjectId === formData.subjectId)?.subjectCode})`
                      : formData.subjectId
                  }
                  className="cm-input w-full text-sm opacity-70 cursor-not-allowed bg-[var(--cm-surface-subtle)]"
                />
              ) : isErrorSubjects ? (
                <SafeErrorPanel
                  error={subjectsError}
                  fallback="Không thể tải môn học."
                  onRetry={() => refetchSubjects()}
                />
              ) : (
                <select
                  id="question-subject-select"
                  disabled={isLoadingSubjects}
                  value={formData.subjectId}
                  onChange={(e) => handleInputChange("subjectId", e.target.value)}
                  className="cm-select w-full text-sm"
                >
                  <option value="">-- Chọn môn học --</option>
                  {subjectsData?.data?.map((s) => (
                    <option key={s.subjectId} value={s.subjectId}>
                      {s.subjectName} ({s.subjectCode})
                    </option>
                  ))}
                </select>
              )}
            </div>

            {/* Primary Topic Node Selector */}
            <div>
              <label htmlFor="question-node-select" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                Nút kiến thức chính <span className="text-rose-400">*</span>
              </label>
              {!formData.subjectId ? (
                <div className="text-xs text-[var(--cm-text-muted)] p-2.5 rounded bg-[var(--cm-surface-subtle)] border border-[var(--cm-border-subtle)]">
                  Vui lòng chọn môn học trước
                </div>
              ) : isErrorNodes ? (
                <SafeErrorPanel
                  error={nodesError}
                  fallback="Không thể tải nút kiến thức."
                  onRetry={() => refetchNodes()}
                />
              ) : (
                <select
                  id="question-node-select"
                  disabled={isReadOnly || isLoadingNodes}
                  value={formData.primaryTopicNodeId}
                  onChange={(e) => handleInputChange("primaryTopicNodeId", e.target.value)}
                  className="cm-select w-full text-sm"
                >
                  <option value="">-- Chọn nút kiến thức --</option>
                  {nodesData?.map((n) => (
                    <option key={n.nodeId} value={n.nodeId}>
                      {n.nodeCode} - {n.nodeName}
                    </option>
                  ))}
                </select>
              )}
            </div>

            {/* Teacher Selector (Only required when creating for CenterManager) */}
            <div>
              <label htmlFor="question-teacher-select" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                Giáo viên phụ trách <span className="text-rose-400">*</span>
              </label>
              {isEditMode ? (
                <input
                  type="text"
                  disabled
                  value={
                    teacherDetailData?.displayName
                      ? `${teacherDetailData.displayName} (${teacherDetailData.username})`
                      : createdByTeacherId
                      ? `Mã GV: ${createdByTeacherId}`
                      : ""
                  }
                  className="cm-input w-full text-sm opacity-70 cursor-not-allowed bg-[var(--cm-surface-subtle)]"
                />
              ) : isErrorTeachers ? (
                <SafeErrorPanel
                  error={teachersError}
                  fallback="Không thể tải danh sách giáo viên."
                  onRetry={() => refetchTeachers()}
                />
              ) : (
                <div className="space-y-1.5">
                  <select
                    id="question-teacher-select"
                    disabled={isLoadingTeachers}
                    value={formData.teacherId || ""}
                    onChange={(e) => handleInputChange("teacherId", e.target.value || null)}
                    className="cm-select w-full text-sm"
                  >
                    <option value="">-- Chọn giáo viên phụ trách --</option>
                    {teacherList.map((t) => (
                      <option key={t.teacherId} value={t.teacherId}>
                        {t.displayName} ({t.username})
                      </option>
                    ))}
                  </select>
                  {/* Quick teacher search & page */}
                  <div className="flex items-center gap-2">
                    <input
                      type="text"
                      placeholder="Tìm giáo viên..."
                      value={teacherSearch}
                      onChange={(e) => {
                        setTeacherSearch(e.target.value);
                        setTeacherPage(1);
                      }}
                      className="cm-input text-xs flex-1 py-1"
                    />
                    <button
                      type="button"
                      onClick={() => setTeacherPage((p) => Math.max(1, p - 1))}
                      disabled={teacherPage <= 1}
                      className="cm-secondary-button text-[10px] px-2 py-1"
                    >
                      ←
                    </button>
                    <span className="text-[10px] font-mono text-[var(--cm-text-muted)]">{teacherPage}</span>
                    <button
                      type="button"
                      onClick={() => setTeacherPage((p) => p + 1)}
                      disabled={!teachersData?.meta?.totalPages || teacherPage >= teachersData.meta.totalPages}
                      className="cm-secondary-button text-[10px] px-2 py-1"
                    >
                      →
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>

          {/* Question Configuration Row: Type, Difficulty, MaxScore, EstimatedTime */}
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-4 border-b border-[var(--cm-border-subtle)] pb-6">
            <div>
              <label htmlFor="question-type-select" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                Loại câu hỏi <span className="text-rose-400">*</span>
              </label>
              <select
                id="question-type-select"
                disabled={isReadOnly}
                value={formData.questionType}
                onChange={(e) => handleInputChange("questionType", e.target.value)}
                className="cm-select w-full text-sm"
              >
                <option value="MultipleChoice">Trắc nghiệm (MultipleChoice)</option>
                <option value="ShortAnswer">Điền khuyết (ShortAnswer)</option>
                <option value="Essay">Tự luận (Essay)</option>
              </select>
            </div>

            <div>
              <label htmlFor="question-difficulty-input" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                Độ khó (1 - 5) <span className="text-rose-400">*</span>
              </label>
              <select
                id="question-difficulty-input"
                disabled={isReadOnly}
                value={formData.difficulty}
                onChange={(e) => handleInputChange("difficulty", Number(e.target.value))}
                className="cm-select w-full text-sm"
              >
                <option value="1">1 - Rất dễ</option>
                <option value="2">2 - Dễ</option>
                <option value="3">3 - Trung bình</option>
                <option value="4">4 - Khó</option>
                <option value="5">5 - Rất khó</option>
              </select>
            </div>

            <div>
              <label htmlFor="question-maxscore-input" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                Điểm tối đa <span className="text-rose-400">*</span>
              </label>
              <input
                id="question-maxscore-input"
                type="number"
                min="0.25"
                max="100"
                step="0.25"
                disabled={isReadOnly}
                value={formData.maxScore}
                onChange={(e) => handleInputChange("maxScore", parseFloat(e.target.value) || 1)}
                className="cm-input w-full text-sm"
              />
            </div>

            <div>
              <label htmlFor="question-time-input" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                Thời gian dự tính (giây) <span className="text-rose-400">*</span>
              </label>
              <input
                id="question-time-input"
                type="number"
                min="10"
                max="3600"
                step="10"
                disabled={isReadOnly}
                value={formData.estimatedTimeSeconds}
                onChange={(e) => handleInputChange("estimatedTimeSeconds", parseInt(e.target.value) || 60)}
                className="cm-input w-full text-sm"
              />
            </div>
          </div>

          {/* Math Toolbar Toggle & Quick Helper */}
          <div className="flex items-center justify-between bg-[var(--cm-surface-subtle)] p-3 rounded-xl border border-[var(--cm-border-subtle)]">
            <div className="flex items-center gap-2">
              <span className="text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-secondary)]">
                Bảng ký hiệu Toán học KaTeX:
              </span>
              <span className="text-xs text-[var(--cm-text-muted)]">
                {activeMathField ? `Đang trỏ vào trường "${activeMathField}"` : "Nhấp vào ô văn bản để chèn ký hiệu"}
              </span>
            </div>
            <button
              type="button"
              onClick={() => setShowMathToolbar((prev) => !prev)}
              className="text-xs font-semibold text-[var(--cm-cyan)] hover:underline"
            >
              {showMathToolbar ? "Ẩn bảng ký hiệu ▲" : "Hiện bảng ký hiệu ▼"}
            </button>
          </div>

          {showMathToolbar && (
            <div className="p-4 rounded-xl border border-[var(--cm-border-subtle)] bg-slate-950/40">
              <MathInputToolbar onInsert={handleInsertMath} />
            </div>
          )}

          {/* Question Text Area with Real-time KaTeX Preview */}
          <div className="space-y-2">
            <label htmlFor="question-text-area" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)]">
              Nội dung câu hỏi (hỗ trợ LaTeX / KaTeX) <span className="text-rose-400">*</span>
            </label>
            <textarea
              id="question-text-area"
              rows={4}
              disabled={isReadOnly}
              value={formData.questionText}
              onFocus={() => setActiveMathField("questionText")}
              onChange={(e) => handleInputChange("questionText", e.target.value)}
              placeholder="Nhập đề bài câu hỏi. Để chèn công thức toán, hãy nhập mã LaTeX như \frac{a}{b} hoặc \sqrt{x}..."
              className="cm-input w-full text-sm font-sans"
            />
            {formData.questionText && (
              <div className="pt-1">
                <MathFormulaPreview formula={formData.questionText} displayMode={false} label="Xem trước đề bài (KaTeX)" />
              </div>
            )}
          </div>

          {/* Evaluation Mode Configuration (Aligned with backend contract) */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-5 border-t border-[var(--cm-border-subtle)] pt-6">
            <div>
              <label htmlFor="evaluation-mode-select" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                Chế độ đối soát đáp án (Evaluation Mode)
              </label>
              <select
                id="evaluation-mode-select"
                disabled={isReadOnly || formData.questionType === "MultipleChoice"}
                value={formData.answerEvaluationMode || "TextExact"}
                onChange={(e) => handleInputChange("answerEvaluationMode", e.target.value)}
                className="cm-select w-full text-sm"
              >
                <option value="TextExact">TextExact - Khớp chuỗi chính xác</option>
                <option value="NumericRational">NumericRational - Chuẩn hóa phân số & số thực</option>
                <option value="Manual">Manual - Chấm thủ công / AI Rubric</option>
              </select>
              <p className="mt-1.5 text-xs text-[var(--cm-text-muted)] leading-relaxed">
                {formData.answerEvaluationMode === "NumericRational"
                  ? "Chấp nhận các biểu diễn tương đương toán học như 3/4 = 0.75 hoặc 6/8."
                  : formData.answerEvaluationMode === "Manual"
                  ? "Dành cho câu hỏi tự luận cần đánh giá qua tiêu chí Rubric hoặc giáo viên duyệt."
                  : "So khớp chính xác ký tự chữ hoa/thường theo chuẩn trắc nghiệm."}
              </p>
            </div>

            {/* Independent Reasoning Required Flag */}
            <div className="flex flex-col justify-center">
              <label className="flex items-center gap-3 cursor-pointer select-none">
                <input
                  type="checkbox"
                  disabled={isReadOnly}
                  checked={formData.reasoningRequired}
                  onChange={(e) => handleInputChange("reasoningRequired", e.target.checked)}
                  className="h-4 w-4 rounded border-[var(--cm-border)] bg-[var(--cm-surface-subtle)] text-[var(--cm-cyan)] focus:ring-[var(--cm-cyan)]"
                />
                <span className="text-sm font-semibold text-[var(--cm-text)]">
                  Bắt buộc học sinh trình bày các bước lập luận (Reasoning Required)
                </span>
              </label>
              <p className="mt-1.5 text-xs text-[var(--cm-text-muted)] pl-7">
                Khi kích hoạt, hệ thống AI sẽ kiểm tra lập luận chi tiết của học sinh trước khi chốt năng lực kiến thức.
              </p>
            </div>
          </div>

          {/* Conditional Sub-form: MultipleChoice */}
          {formData.questionType === "MultipleChoice" && (
            <div className="border-t border-[var(--cm-border-subtle)] pt-6 space-y-4">
              <div className="flex items-center justify-between">
                <h3 className="text-sm font-semibold uppercase tracking-wider text-[var(--cm-cyan)]">
                  Danh sách Lựa chọn Trắc nghiệm
                </h3>
                {!isReadOnly && (formData.options?.length || 0) < 8 && (
                  <button
                    type="button"
                    onClick={addOption}
                    className="text-xs font-semibold text-[var(--cm-cyan)] hover:underline"
                  >
                    + Thêm phương án
                  </button>
                )}
              </div>

              <div className="space-y-3">
                {formData.options?.map((opt, idx) => (
                  <div
                    key={idx}
                    className="flex items-start gap-3 rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-subtle)] p-3"
                  >
                    <label className="flex items-center gap-2 pt-2.5 cursor-pointer">
                      <input
                        type="radio"
                        name="correct-option"
                        disabled={isReadOnly}
                        checked={opt.isCorrect}
                        onChange={() => handleOptionChange(idx, "isCorrect", true)}
                        className="h-4 w-4 text-[var(--cm-cyan)] focus:ring-[var(--cm-cyan)]"
                      />
                      <span className="text-xs font-bold text-[var(--cm-text)] font-mono">{opt.optionLabel}.</span>
                    </label>

                    <div className="flex-1 space-y-1">
                      <input
                        type="text"
                        disabled={isReadOnly}
                        value={opt.optionText}
                        onChange={(e) => handleOptionChange(idx, "optionText", e.target.value)}
                        placeholder={`Nội dung phương án ${opt.optionLabel}...`}
                        className="cm-input w-full text-sm"
                      />
                      {opt.optionText.includes("\\") && (
                        <MathFormulaPreview formula={opt.optionText} displayMode={false} label={`Xem trước ${opt.optionLabel}`} />
                      )}
                    </div>

                    {!isReadOnly && (formData.options?.length || 0) > 2 && (
                      <button
                        type="button"
                        onClick={() => removeOption(idx)}
                        className="text-xs text-rose-400 hover:text-rose-200 pt-2 px-2"
                        title="Xóa phương án này"
                      >
                        ✕
                      </button>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Conditional Sub-form: ShortAnswer */}
          {formData.questionType === "ShortAnswer" && (
            <div className="border-t border-[var(--cm-border-subtle)] pt-6 space-y-4">
              <h3 className="text-sm font-semibold uppercase tracking-wider text-[var(--cm-cyan)]">
                Đáp án Chuẩn (Short Answer)
              </h3>
              <div>
                <label htmlFor="question-correct-answer" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                  Giá trị chuẩn <span className="text-rose-400">*</span>
                </label>
                <input
                  id="question-correct-answer"
                  type="text"
                  disabled={isReadOnly}
                  value={formData.correctAnswer || ""}
                  onFocus={() => setActiveMathField("correctAnswer")}
                  onChange={(e) => handleInputChange("correctAnswer", e.target.value)}
                  placeholder="Nhập giá trị đúng (VD: 3/4, 12.5, x^2 + 1...)"
                  className="cm-input w-full text-sm font-mono"
                />
                {formData.correctAnswer && (
                  <div className="pt-2">
                    <MathFormulaPreview formula={formData.correctAnswer} displayMode={false} label="Xem trước đáp án chuẩn (KaTeX)" />
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Conditional Sub-form: Essay Rubric */}
          {formData.questionType === "Essay" && (
            <div className="border-t border-[var(--cm-border-subtle)] pt-6 space-y-5">
              <h3 className="text-sm font-semibold uppercase tracking-wider text-[var(--cm-cyan)]">
                Tiêu chí Đánh giá Tự luận (Grading Rubric)
              </h3>

              {/* Required Ideas */}
              <div className="space-y-2">
                <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)]">
                  Ý bắt buộc trong bài làm (Required Ideas)
                </label>
                {!isReadOnly && (
                  <div className="flex items-center gap-2">
                    <input
                      type="text"
                      value={requiredIdeaInput}
                      onChange={(e) => setRequiredIdeaInput(e.target.value)}
                      placeholder="Thêm ý quan trọng cần có trong bài giải..."
                      className="cm-input flex-1 text-sm"
                    />
                    <button
                      type="button"
                      onClick={addRequiredIdea}
                      className="cm-secondary-button text-xs shrink-0"
                    >
                      + Thêm ý
                    </button>
                  </div>
                )}
                <div className="flex flex-wrap gap-2 pt-1">
                  {formData.gradingCriteria?.requiredIdeas?.map((idea, i) => (
                    <span
                      key={i}
                      className="inline-flex items-center gap-1.5 rounded-lg bg-emerald-500/10 border border-emerald-500/30 px-3 py-1 text-xs text-emerald-300"
                    >
                      <span>✓ {idea}</span>
                      {!isReadOnly && (
                        <button
                          type="button"
                          onClick={() => removeRequiredIdea(i)}
                          className="hover:text-emerald-100"
                        >
                          ✕
                        </button>
                      )}
                    </span>
                  ))}
                </div>
              </div>

              {/* Common Errors */}
              <div className="space-y-2">
                <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)]">
                  Lỗi thường gặp cần cảnh báo (Common Errors)
                </label>
                {!isReadOnly && (
                  <div className="flex items-center gap-2">
                    <input
                      type="text"
                      value={commonErrorInput}
                      onChange={(e) => setCommonErrorInput(e.target.value)}
                      placeholder="Thêm lỗi sai học sinh hay mắc..."
                      className="cm-input flex-1 text-sm"
                    />
                    <button
                      type="button"
                      onClick={addCommonError}
                      className="cm-secondary-button text-xs shrink-0"
                    >
                      + Thêm lỗi
                    </button>
                  </div>
                )}
                <div className="flex flex-wrap gap-2 pt-1">
                  {formData.gradingCriteria?.commonErrors?.map((errItem, i) => (
                    <span
                      key={i}
                      className="inline-flex items-center gap-1.5 rounded-lg bg-rose-500/10 border border-rose-500/30 px-3 py-1 text-xs text-rose-300"
                    >
                      <span>⚠ {errItem}</span>
                      {!isReadOnly && (
                        <button
                          type="button"
                          onClick={() => removeCommonError(i)}
                          className="hover:text-rose-100"
                        >
                          ✕
                        </button>
                      )}
                    </span>
                  ))}
                </div>
              </div>
            </div>
          )}

          {/* Solution & Explanation Area */}
          <div className="border-t border-[var(--cm-border-subtle)] pt-6 space-y-2">
            <label htmlFor="question-solution-area" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)]">
              Lời giải chi tiết / Hướng dẫn chấm
            </label>
            <textarea
              id="question-solution-area"
              rows={3}
              disabled={isReadOnly}
              value={formData.solution || ""}
              onFocus={() => setActiveMathField("solution")}
              onChange={(e) => handleInputChange("solution", e.target.value)}
              placeholder="Nhập lời giải chi tiết giải thích cho học sinh hoặc hướng dẫn cho giáo viên chấm..."
              className="cm-input w-full text-sm font-sans"
            />
            {formData.solution && (
              <div className="pt-1">
                <MathFormulaPreview formula={formData.solution} displayMode={false} label="Xem trước lời giải (KaTeX)" />
              </div>
            )}
          </div>
        </div>

        {/* State Machine Transition Confirm Dialog */}
        <ConfirmDialog
          isOpen={dialogAction !== null}
          title={
            dialogAction === "activate"
              ? "Kích hoạt câu hỏi"
              : dialogAction === "archive"
              ? "Lưu trữ câu hỏi"
              : "Xóa vĩnh viễn câu hỏi bản nháp"
          }
          description={
            dialogAction === "activate"
              ? "Kích hoạt câu hỏi này để có thể phân bổ vào các bài tập. Sau khi kích hoạt, nội dung câu hỏi sẽ chuyển sang chế độ Chỉ đọc."
              : dialogAction === "archive"
              ? "Lưu trữ câu hỏi này để ngừng phân bổ vào bài tập mới. Trạng thái chỉ đọc sẽ được giữ nguyên."
              : "CẢNH BÁO: Thao tác này sẽ xóa vĩnh viễn câu hỏi bản nháp khỏi hệ thống. Không thể khôi phục!"
          }
          confirmLabel={
            dialogAction === "activate"
              ? "Kích hoạt ngay"
              : dialogAction === "archive"
              ? "Lưu trữ"
              : "Xóa vĩnh viễn"
          }
          tone={dialogAction === "delete" ? "danger" : "default"}
          isConfirming={
            activateMutation.isPending || archiveMutation.isPending || deleteMutation.isPending
          }
          onConfirm={handleConfirmDialogAction}
          onClose={() => setDialogAction(null)}
        />
      </div>
    </CenterManagerThemeScope>
  );
}

// =============================================================================
// 2. LEGACY QUESTION EDITOR VIEW (FOR TEACHERS & OTHER ACTORS)
// =============================================================================

function LegacyQuestionEditorPage() {
  const [formData, setFormData] = useState<CreateQuestionRequest>({
    teacherId: null,
    subjectId: "",
    primaryTopicNodeId: "",
    questionType: "MultipleChoice",
    difficulty: 3,
    questionText: "",
    maxScore: 1,
    estimatedTimeSeconds: 60,
    reasoningRequired: true,
    languageCode: "vi",
    answerEvaluationMode: "TextExact",
    options: [
      { optionText: "", isCorrect: true, optionLabel: "A", orderIndex: 0 },
      { optionText: "", isCorrect: false, optionLabel: "B", orderIndex: 1 },
    ],
  });

  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const isEditMode = !!id;

  const { data: questionData } = useQuestion(id || "");
  const createMutation = useCreateQuestion();
  const updateMutation = useUpdateQuestion();

  useEffect(() => {
    if (isEditMode && questionData?.data) {
      const q = questionData.data;
      setFormData({
        subjectId: q.subjectId,
        primaryTopicNodeId: q.primaryTopicNodeId || "",
        questionType: q.questionType,
        difficulty: q.difficulty,
        questionText: q.questionText,
        maxScore: q.maxScore,
        estimatedTimeSeconds: q.estimatedTimeSeconds,
        reasoningRequired: q.reasoningRequired || false,
        languageCode: q.languageCode || "vi",
        answerEvaluationMode:
          q.answerEvaluationMode ||
          (q.questionType === "MultipleChoice"
            ? "TextExact"
            : q.questionType === "Essay"
            ? "Manual"
            : "TextExact"),
        options: (q.options || []).map((opt: any) => ({
          ...opt,
          optionLabel: opt.optionLabel || opt.label,
          optionText: opt.optionText || opt.text,
        })),
        correctAnswer: q.correctAnswer,
        solution: q.solution,
        gradingCriteria: q.gradingCriteria,
      });
    }
  }, [isEditMode, questionData]);

  const handleSave = () => {
    if (!isEditMode) {
      createMutation.mutate(formData, {
        onSuccess: () => navigate("/quan-ly/cau-hoi"),
        onError: (err: unknown) => alert(mapSafeOperationalError(err, "Không thể tạo câu hỏi.")),
      });
    } else {
      if (!questionData?.data?.rowVersion) {
        alert("Không thể xác định phiên bản đồng thời (RowVersion). Vui lòng làm mới trang.");
        return;
      }
      const updateData: UpdateQuestionRequest = {
        primaryTopicNodeId: formData.primaryTopicNodeId,
        questionType: formData.questionType,
        difficulty: formData.difficulty,
        questionText: formData.questionText,
        correctAnswer: formData.correctAnswer,
        solution: formData.solution,
        expectedReasoning: formData.expectedReasoning,
        gradingCriteria: formData.gradingCriteria,
        maxScore: formData.maxScore,
        estimatedTimeSeconds: formData.estimatedTimeSeconds,
        reasoningRequired: formData.reasoningRequired,
        languageCode: formData.languageCode,
        answerEvaluationMode: formData.answerEvaluationMode,
        options: formData.options,
        rowVersion: questionData.data.rowVersion,
      };
      updateMutation.mutate(
        { id: id!, data: updateData },
        {
          onSuccess: () => navigate("/quan-ly/cau-hoi"),
          onError: (err: unknown) => alert(mapSafeOperationalError(err, "Không thể cập nhật câu hỏi.")),
        }
      );
    }
  };

  return (
    <div className="p-6 max-w-4xl mx-auto space-y-6">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold text-slate-800">
          {isEditMode ? "Chỉnh sửa Câu hỏi" : "Tạo Câu hỏi Mới"}
        </h1>
        <button
          onClick={handleSave}
          className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition font-medium"
        >
          Lưu
        </button>
      </div>
      <div className="bg-white p-6 rounded-xl border border-slate-200 space-y-4">
        <div>
          <label className="block text-sm font-medium text-slate-700 mb-1">Nội dung câu hỏi</label>
          <textarea
            rows={4}
            value={formData.questionText}
            onChange={(e) => setFormData({ ...formData, questionText: e.target.value })}
            className="w-full border border-slate-300 rounded-lg p-3 text-sm"
          />
        </div>
      </div>
    </div>
  );
}

// =============================================================================
// 3. MAIN ROUTE EXPORT WITH ACTOR ISOLATION
// =============================================================================

export const QuestionEditorPage = () => {
  const accountType = useAuthStore((state) => state.user?.accountType);
  if (accountType === "CenterManager") {
    return <CenterManagerQuestionEditorView />;
  }
  return <LegacyQuestionEditorPage />;
};
