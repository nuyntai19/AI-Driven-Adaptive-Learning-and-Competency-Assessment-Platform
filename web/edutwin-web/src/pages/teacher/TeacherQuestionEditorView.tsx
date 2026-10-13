import { useState, useEffect } from "react";
import { useParams, useNavigate, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  useQuestion,
  useCreateQuestion,
  useUpdateQuestion,
} from "../../features/questions/useQuestions";
import { organizationApi } from "../../api/organizationApi";
import { knowledgeGraphApi } from "../../api/knowledgeGraphApi";
import type {
  CreateQuestionRequest,
  UpdateQuestionRequest,
  QuestionType,
  QuestionAnswerEvaluationMode,
} from "../../types/questions";
import { useAuthStore } from "../../stores/authStore";
import { permissions } from "../../auth/permissions";
import {
  extractProblemDetails,
  mapSafeOperationalError,
  isConcurrencyConflictError,
} from "../../utils/problemDetails";
import {
  TeacherPageHeader,
  TeacherSkeleton,
  TeacherSafeErrorPanel,
  TeacherConcurrencyBanner,
} from "../../components/teacher";
import { RichMathEditor } from "../../components/math/RichMathEditor";
import { ModeAwareAnswerEditor } from "../../components/math/answer-editor/ModeAwareAnswerEditor";
import {
  validateTextMathFormulas,
  validateAnswerMathFormulas,
  formatFormulaDiagnosticMessage,
  getAnswerDraftKey,
  resetAndHydrateDraftStore,
  hydrateAnswerEditorValue,
  serializeAnswerEditorValue,
  type DraftStore,
} from "../centerManagerQuestionEditorHelpers";
import type { AnswerEditorValue } from "../../components/math/answer-editor/answerEditorHelpers";
import { MATH_EQUIVALENT_HELP } from "../../utils/questionEvaluationModes";
import { hydrateGradingCriteria, rubricDefinitionError } from "../../utils/rubric";
import { GradingCriteriaEditor } from "../../components/teacher/GradingCriteriaEditor";
import { RichMathText } from "../../components/math/RichMathText";
import { QuestionImageEditor } from "../../components/teacher/QuestionImageEditor";
import { QuestionImage } from "../../components/QuestionImage";
import { IMAGE_ONLY_QUESTION_TEXT } from "../../utils/questionImage";
import { KnowledgeTopicPicker } from "../../components/teacher/KnowledgeTopicPicker";
import type { MaterialVisibility } from "../../types/questions";

interface QuestionEditorOption {
  optionId?: string;
  label: string;
  text: string;
  isCorrect: boolean;
  orderIndex: number;
  misconception?: string;
}

export function TeacherQuestionEditorView() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const isEditing = Boolean(id);
  const [searchParams] = useSearchParams();
  const copyFrom = !isEditing ? searchParams.get("copyFrom") : null;
  const actorId = useAuthStore(state => state.user?.userId);
  const [visibility, setVisibility] = useState<MaterialVisibility>("Private");

  const hasPermission = useAuthStore((state) => state.hasPermission);
  const canCreate = hasPermission(permissions.questionsCreate);
  const canReadSubjects = hasPermission(permissions.subjectsRead);

  // Form State
  const [subjectId, setSubjectId] = useState("");
  const [gradeLevel, setGradeLevel] = useState<number | "">("");
  const [primaryTopicNodeId, setPrimaryTopicNodeId] = useState("");
  const [questionType, setQuestionType] = useState<QuestionType>("MultipleChoice");
  const [difficulty, setDifficulty] = useState<number>(3);
  const [questionText, setQuestionText] = useState("");
  const [imageDataUrl, setImageDataUrl] = useState<string | undefined>();
  const [removeImage, setRemoveImage] = useState(false);
  const [imageBusy, setImageBusy] = useState(false);
  const [maxScore, setMaxScore] = useState<number>(10);
  const [estimatedTimeSeconds, setEstimatedTimeSeconds] = useState<number>(120);
  const [reasoningRequired, setReasoningRequired] = useState<boolean>(false);
  const [languageCode] = useState("vi");
  const [answerEvaluationMode, setAnswerEvaluationMode] = useState<QuestionAnswerEvaluationMode>("TextExact");
  const [options, setOptions] = useState<QuestionEditorOption[]>([
    { label: "A", text: "", isCorrect: true, orderIndex: 0, misconception: "" },
    { label: "B", text: "", isCorrect: false, orderIndex: 1, misconception: "" },
    { label: "C", text: "", isCorrect: false, orderIndex: 2, misconception: "" },
    { label: "D", text: "", isCorrect: false, orderIndex: 3, misconception: "" },
  ]);
  const [correctAnswer, setCorrectAnswer] = useState("");
  const [activeDraftValue, setActiveDraftValue] = useState<AnswerEditorValue | null>(null);
  const [modeDrafts, setModeDrafts] = useState<DraftStore>({});
  const [solution, setSolution] = useState("");
  const [expectedReasoning, setExpectedReasoning] = useState("");
  const [gradingCriteria, setGradingCriteria] = useState(() => hydrateGradingCriteria());

  // Feedback States
  const [formError, setFormError] = useState<{ message: string; traceId?: string | null } | null>(null);
  const [concurrencyConflict, setConcurrencyConflict] = useState(false);

  // Active subjects query
  const { data: subjectsData, isLoading: subjectsLoading } = useQuery({
    queryKey: ["subjects", "for-teacher-question-editor"],
    queryFn: () => organizationApi.listSubjects(true),
    enabled: canReadSubjects,
  });

  // Topics for selected subject
  const { data: topicsData, isLoading: topicsLoading } = useQuery({
    queryKey: ["topics", "for-subject", subjectId],
    queryFn: () => (subjectId ? knowledgeGraphApi.getGraph(subjectId) : null),
    enabled: Boolean(subjectId),
  });

  // Load existing question for editing
  const { data: questionData, isLoading: questionLoading, error: questionError, refetch: refetchQuestion } = useQuestion(id || copyFrom || "");
  const hasExistingImage = Boolean(questionData?.data?.hasImage && !removeImage);

  useEffect(() => {
    if ((isEditing || copyFrom) && questionData?.data) {
      const q = questionData.data;
      setSubjectId(q.subjectId || "");
      setGradeLevel(q.gradeLevel ?? "");
      setPrimaryTopicNodeId(q.primaryTopicNodeId ? String(q.primaryTopicNodeId) : "");
      setQuestionType(q.questionType);
      setDifficulty(q.difficulty);
      setQuestionText(q.questionText || "");
      setImageDataUrl(undefined); setRemoveImage(false);
      setMaxScore(q.maxScore || 10);
      setEstimatedTimeSeconds(q.estimatedTimeSeconds || 120);
      setReasoningRequired(Boolean(q.reasoningRequired));
      setAnswerEvaluationMode(
        q.answerEvaluationMode ||
          (q.questionType === "MultipleChoice" ? "TextExact" : q.questionType === "Essay" ? "Manual" : "TextExact")
      );
      if (q.options && q.options.length > 0) {
        const hasAnyCorrect = q.options.some((opt: any) => Boolean(opt.isCorrect));
        setOptions(
          q.options.map((opt: any, idx: number) => {
            const label = opt.label || opt.optionLabel || String.fromCharCode(65 + idx);
            const isCorrect = hasAnyCorrect
              ? Boolean(opt.isCorrect)
              : label.trim().toUpperCase() === (q.correctAnswer || "").trim().toUpperCase();
            return {
              optionId: opt.optionId,
              label,
              text: opt.text || opt.optionText || "",
              isCorrect,
              orderIndex: opt.orderIndex ?? idx,
              misconception: opt.misconception || "",
            };
          })
        );
      }
      setCorrectAnswer(q.correctAnswer || "");
      const hydratedStore = resetAndHydrateDraftStore(q, true);
      setModeDrafts(hydratedStore);
      const evalMode =
        q.answerEvaluationMode ||
        (q.questionType === "MultipleChoice" ? "TextExact" : q.questionType === "Essay" ? "Manual" : "TextExact");
      const key = getAnswerDraftKey(q.questionType, evalMode);
      setActiveDraftValue((key ? hydratedStore[key] : null) ?? hydrateAnswerEditorValue(q.correctAnswer, evalMode));
      setSolution(q.solution || "");
      setExpectedReasoning(q.expectedReasoning || "");
      setGradingCriteria(hydrateGradingCriteria(q.gradingCriteria));
      setVisibility(copyFrom ? "Private" : q.visibility || "Private");
    }
  }, [isEditing, copyFrom, questionData]);

  const createMutation = useCreateQuestion();
  const updateMutation = useUpdateQuestion();

  const handleOptionTextChange = (index: number, text: string) => {
    setOptions((prev) => prev.map((opt, i) => (i === index ? { ...opt, text } : opt)));
  };

  const handleOptionMisconceptionChange = (index: number, misconception: string) => {
    setOptions((prev) => prev.map((opt, i) => (i === index ? { ...opt, misconception } : opt)));
  };

  const handleOptionCorrectChange = (index: number) => {
    setOptions((prev) =>
      prev.map((opt, i) => ({
        ...opt,
        isCorrect: i === index,
        misconception: i === index ? "" : opt.misconception,
      }))
    );
  };

  const handleSave = () => {
    setFormError(null);
    setConcurrencyConflict(false);

    if (!subjectId) {
      setFormError({ message: "Vui lòng chọn môn học cho câu hỏi." });
      return;
    }
    if (!primaryTopicNodeId.trim()) {
      setFormError({ message: "Vui lòng chọn chủ đề Cây Tri thức (Topic Node) cho câu hỏi." });
      return;
    }
    if (imageBusy) return;
    if (!questionText.trim() && !imageDataUrl && !hasExistingImage) {
      setFormError({ message: "Vui lòng nhập nội dung đề bài hoặc đính kèm ảnh chứa đề." });
      return;
    }
    if (difficulty < 1 || difficulty > 5) {
      setFormError({ message: "Độ khó phải nằm trong khoảng từ 1 đến 5." });
      return;
    }
    if (!isEditing && (gradeLevel === "" || Number(gradeLevel) < 10 || Number(gradeLevel) > 12)) {
      setFormError({ message: "Vui lòng chọn khối lớp hợp lệ (Khối 10, 11 hoặc 12) cho câu hỏi mới." });
      return;
    }
    if (maxScore <= 0) {
      setFormError({ message: "Điểm tối đa phải lớn hơn 0." });
      return;
    }
    if (estimatedTimeSeconds <= 0) {
      setFormError({ message: "Thời gian ước tính phải lớn hơn 0 giây." });
      return;
    }

    let computedCorrectAnswer = "";

    if (questionType === "MultipleChoice") {
      const emptyOption = options.find((opt) => !opt.text.trim());
      if (emptyOption) {
        setFormError({ message: `Vui lòng nhập đầy đủ nội dung cho phương án ${emptyOption.label}.` });
        return;
      }
      const correctOpt = options.find((opt) => opt.isCorrect);
      if (!correctOpt) {
        setFormError({ message: "Vui lòng chọn ít nhất 1 phương án đúng cho câu hỏi trắc nghiệm." });
        return;
      }
      computedCorrectAnswer = correctOpt.label;
    } else {
      const evalMode: QuestionAnswerEvaluationMode =
        questionType === "Essay" ? "Manual" : answerEvaluationMode || "TextExact";
      const key = getAnswerDraftKey(questionType, evalMode);
      const draft = (key ? modeDrafts[key] : null) ?? activeDraftValue;
      computedCorrectAnswer = draft
        ? serializeAnswerEditorValue(draft, evalMode)
        : correctAnswer.trim();

      if (!computedCorrectAnswer) {
        setFormError({
          message:
            questionType === "Essay"
              ? "Vui lòng nhập đáp án chuẩn hoặc hướng dẫn chấm chuẩn cho câu hỏi tự luận."
              : "Vui lòng nhập đáp án chuẩn cho câu hỏi.",
        });
        return;
      }
    }

    if (!solution.trim()) {
      setFormError({ message: "Vui lòng nhập lời giải chi tiết (Solution) cho câu hỏi." });
      return;
    }

    // Defensive check against unclosed delimiter, empty, placeholder, or invalid syntax
    const qTextDiag = validateTextMathFormulas(questionText)[0];
    if (qTextDiag) {
      setFormError({
        message: formatFormulaDiagnosticMessage("Nội dung đề bài", qTextDiag),
      });
      return;
    }

    const solDiag = validateTextMathFormulas(solution)[0];
    if (solDiag) {
      setFormError({
        message: formatFormulaDiagnosticMessage("Lời giải", solDiag),
      });
      return;
    }

    if (questionType === "MultipleChoice") {
      for (const opt of options) {
        const optDiag = validateTextMathFormulas(opt.text)[0];
        if (optDiag) {
          setFormError({
            message: formatFormulaDiagnosticMessage(`Phương án ${opt.label}`, optDiag),
          });
          return;
        }
      }
    }

    if (computedCorrectAnswer) {
      const ansDiag = validateAnswerMathFormulas(computedCorrectAnswer, answerEvaluationMode)[0];
      if (ansDiag) {
        setFormError({
          message: formatFormulaDiagnosticMessage("Đáp án chuẩn", ansDiag),
        });
        return;
      }
    }

    const evalMode: QuestionAnswerEvaluationMode =
      questionType === "MultipleChoice"
        ? "TextExact"
        : questionType === "Essay"
        ? "Manual"
        : answerEvaluationMode || "TextExact";

    const rubricError = rubricDefinitionError(gradingCriteria.criteria || [], maxScore);
    if (rubricError) { setFormError({ message: rubricError }); return; }
    const parsedGradingCriteria = gradingCriteria;

    if (!isEditing) {
      const payload: CreateQuestionRequest = {
        imageDataUrl,
        copyImageFromQuestionId: !imageDataUrl && hasExistingImage ? copyFrom || undefined : undefined,
        visibility,
        subjectId,
        gradeLevel: Number(gradeLevel),
        primaryTopicNodeId: primaryTopicNodeId.trim(),
        questionType,
        difficulty,
        questionText: questionText.trim(),
        maxScore,
        estimatedTimeSeconds,
        reasoningRequired,
        languageCode,
        answerEvaluationMode: evalMode,
        options:
          questionType === "MultipleChoice"
            ? options.map((opt) => ({
                optionLabel: opt.label,
                optionText: opt.text.trim(),
                isCorrect: opt.isCorrect,
                orderIndex: opt.orderIndex,
                misconception: opt.isCorrect ? undefined : opt.misconception?.trim() || undefined,
              }))
            : undefined,
        correctAnswer: computedCorrectAnswer,
        solution: solution.trim(),
        expectedReasoning: expectedReasoning.trim() || undefined,
        gradingCriteria: parsedGradingCriteria,
        knowledgeMappings: [
          {
            nodeId: primaryTopicNodeId.trim(),
            mappingRole: "Primary",
          },
        ],
      };

      createMutation.mutate(payload, {
        onSuccess: () => {
          navigate("/giao-vien/cau-hoi");
        },
        onError: (err: any) => {
          const details = extractProblemDetails(err);
          setFormError({
            message: mapSafeOperationalError(err, "Không thể tạo câu hỏi mới."),
            traceId: details.traceId,
          });
        },
      });
    } else {
      if (!questionData?.data?.rowVersion) {
        setFormError({ message: "Không xác định được phiên bản bản ghi (RowVersion). Vui lòng tải lại trang." });
        return;
      }

      const updatePayload: UpdateQuestionRequest = {
        imageDataUrl,
        removeImage,
        visibility,
        primaryTopicNodeId: primaryTopicNodeId.trim(),
        gradeLevel: gradeLevel !== "" ? Number(gradeLevel) : null,
        questionType,
        difficulty,
        questionText: questionText.trim(),
        maxScore,
        estimatedTimeSeconds,
        reasoningRequired,
        languageCode,
        answerEvaluationMode: evalMode,
        options:
          questionType === "MultipleChoice"
            ? options.map((opt) => ({
                optionLabel: opt.label,
                optionText: opt.text.trim(),
                isCorrect: opt.isCorrect,
                orderIndex: opt.orderIndex,
                misconception: opt.isCorrect ? undefined : opt.misconception?.trim() || undefined,
              }))
            : undefined,
        correctAnswer: computedCorrectAnswer,
        solution: solution.trim(),
        expectedReasoning: expectedReasoning.trim() || undefined,
        gradingCriteria: parsedGradingCriteria,
        knowledgeMappings: questionData?.data?.knowledgeMappings?.length
          ? questionData.data.knowledgeMappings
          : [
              {
                nodeId: primaryTopicNodeId.trim(),
                mappingRole: "Primary",
              },
            ],
        rowVersion: questionData.data.rowVersion,
      };

      updateMutation.mutate(
        { id: id!, data: updatePayload },
        {
          onSuccess: () => {
            navigate("/giao-vien/cau-hoi");
          },
          onError: (err: any) => {
            const details = extractProblemDetails(err);
            if (isConcurrencyConflictError(err)) {
              setConcurrencyConflict(true);
            } else {
              setFormError({
                message: details.errorCode === "INVALID_STATE_TRANSITION"
                  ? "Tiêu chí và điểm gốc đã được dùng để chấm hoặc xuất bản. Hãy tạo bản sao để thay đổi thang chấm."
                  : mapSafeOperationalError(err, "Không thể cập nhật câu hỏi."),
                traceId: details.traceId,
              });
            }
          },
        }
      );
    }
  };

  const isPending = createMutation.isPending || updateMutation.isPending || imageBusy;

  if ((isEditing || copyFrom) && questionLoading) {
    return (
      <div className="th-page-container max-w-5xl">
        <TeacherSkeleton className="h-10 w-1/3" />
        <TeacherSkeleton className="h-64 rounded-2xl" />
      </div>
    );
  }

  if ((isEditing || copyFrom) && (questionError || !questionData?.data)) {
    return <div className="th-page-container max-w-5xl"><TeacherSafeErrorPanel error="Không thể tải câu hỏi nguồn hoặc bạn không có quyền truy cập." onRetry={() => refetchQuestion()} /></div>;
  }

  if (isEditing && questionData?.data && questionData.data.createdByTeacherId !== actorId) {
    const q = questionData.data;
    return <div className="th-page-container max-w-5xl space-y-4">
      <TeacherPageHeader title={`Câu hỏi dùng chung #${q.questionId}`} description="Chỉ xem bản gốc. Sao chép để biên soạn phiên bản của bạn." />
      <div className="th-surface p-6 space-y-4"><RichMathText text={q.questionText} />
        <QuestionImage questionId={q.questionId} hasImage={q.hasImage} />
        {q.options?.map(o => <div key={o.optionId}><RichMathText text={`${o.label || o.optionLabel}. ${o.text || o.optionText}`} /></div>)}
        <h3 className="font-bold">Đáp án & lời giải tham khảo</h3><RichMathText text={q.solution || ""} />
        {canCreate && <button type="button" className="th-primary-button" onClick={() => navigate(`/giao-vien/cau-hoi/tao-moi?copyFrom=${q.questionId}`)}>Sao chép & biên soạn</button>}
      </div>
    </div>;
  }

  return (
    <div className="th-page-container max-w-5xl">
      <TeacherPageHeader
        eyebrow="NGÂN HÀNG CÂU HỎI"
        title={isEditing ? `Chỉnh Sửa Câu Hỏi #${id}` : "Soạn Thảo Câu Hỏi Mới"}
        description="Nhập đề bài, công thức toán KaTeX, các phương án lựa chọn và thiết lập tiêu chí đánh giá năng lực suy luận."
        breadcrumbs={[
          { label: "Giáo viên", href: "/giao-vien/lop-hoc" },
          { label: "Ngân hàng câu hỏi", href: "/giao-vien/cau-hoi" },
          { label: isEditing ? "Chỉnh sửa" : "Soạn mới" },
        ]}
      />

      {concurrencyConflict && (
        <TeacherConcurrencyBanner
          onReload={() => {
            setConcurrencyConflict(false);
            refetchQuestion();
          }}
        />
      )}

      {formError && (
        <TeacherSafeErrorPanel
          error={formError.message}
          title="Không thể lưu câu hỏi"
        />
      )}

      <div className="th-surface p-6 space-y-6">
        <div className="flex flex-wrap items-center gap-3">
          <label className="text-sm font-semibold">Phạm vi học liệu
            <select className="th-select ml-3 text-sm" value={visibility} onChange={e => setVisibility(e.target.value as MaterialVisibility)}>
              <option value="Private">Private — của tôi</option><option value="Shared">Shared — dùng chung trong trung tâm</option>
            </select>
          </label>
          {isEditing && canCreate && <button type="button" className="th-secondary-button" onClick={() => navigate(`/giao-vien/cau-hoi/tao-moi?copyFrom=${id}`)}>Tạo bản sao mới</button>}
          <p className="text-sm text-[var(--th-text-secondary)]">Shared chỉ được giáo viên khác xem/dùng khi câu hỏi đã kích hoạt. Tiêu chí/điểm gốc đã dùng trong bài xuất bản hoặc bài nộp được khóa; hãy tạo bản sao để thay đổi.</p>
        </div>
        {/* Row 1: Môn học & Chủ đề kiến thức */}
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4">
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)] mb-1.5">
              Môn học <span className="text-rose-400">*</span>
            </label>
            <select
              value={subjectId}
              disabled={isEditing || subjectsLoading}
              onChange={(e) => {
                setSubjectId(e.target.value);
                setPrimaryTopicNodeId("");
              }}
              className="th-select w-full text-xs"
            >
              <option value="">-- Chọn môn học --</option>
              {subjectsData?.data?.map((sub) => (
                <option key={sub.subjectId} value={sub.subjectId}>
                  {sub.subjectName} ({sub.subjectCode})
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)] mb-1.5">
              Chủ đề Cây Tri thức (Topic Node) <span className="text-rose-400">*</span>
            </label>
            <KnowledgeTopicPicker
              nodes={topicsData?.nodes || []}
              value={primaryTopicNodeId}
              disabled={!subjectId || topicsLoading}
              onChange={setPrimaryTopicNodeId}
              placeholder={topicsLoading ? "Đang tải chủ đề…" : "Chọn chủ đề kiến thức"}
            />
          </div>

          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)] mb-1.5">
              Dạng câu hỏi <span className="text-rose-400">*</span>
            </label>
            <select
              value={questionType}
              onChange={(e) => {
                const newType = e.target.value as QuestionType;
                setQuestionType(newType);
                let newMode = answerEvaluationMode;
                if (newType === "MultipleChoice") {
                  newMode = "TextExact";
                  setAnswerEvaluationMode("TextExact");
                } else if (newType === "Essay") {
                  newMode = "Manual";
                  setAnswerEvaluationMode("Manual");
                } else if (newType === "ShortAnswer" && answerEvaluationMode === "Manual") {
                  newMode = "TextExact";
                  setAnswerEvaluationMode("TextExact");
                }
                const key = getAnswerDraftKey(newType, newMode);
                if (key && modeDrafts[key]) {
                  setActiveDraftValue(modeDrafts[key]!);
                  setCorrectAnswer(serializeAnswerEditorValue(modeDrafts[key]!, newMode));
                } else {
                  const draft = hydrateAnswerEditorValue("", newMode);
                  setActiveDraftValue(draft);
                  if (key) {
                    setModeDrafts((prev) => ({ ...prev, [key]: draft }));
                  }
                  if (newType !== "MultipleChoice") {
                    setCorrectAnswer("");
                  }
                }
              }}
              className="th-select w-full text-xs"
            >
              <option value="MultipleChoice">Trắc nghiệm nhiều lựa chọn</option>
              <option value="Essay">Tự luận / Trình bày suy luận</option>
              <option value="ShortAnswer">Điền đáp án ngắn</option>
            </select>
          </div>
        </div>

        {/* Row 2: Độ khó, Khối học, Điểm số, Thời gian dự kiến */}
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4 border-t border-[var(--th-border-subtle)] pt-4">
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)] mb-1.5">
              Độ khó (1 - Rất dễ → 5 - Rất khó)
            </label>
            <select
              value={difficulty}
              onChange={(e) => setDifficulty(Number(e.target.value))}
              className="th-select w-full text-xs"
            >
              <option value="1">1 - Rất dễ</option>
              <option value="2">2 - Dễ</option>
              <option value="3">3 - Trung bình</option>
              <option value="4">4 - Khó</option>
              <option value="5">5 - Rất khó / Nâng cao</option>
            </select>
          </div>

          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)] mb-1.5">
              Khối học áp dụng
            </label>
            <select
              value={gradeLevel}
              onChange={(e) => setGradeLevel(e.target.value ? Number(e.target.value) : "")}
              className="th-select w-full text-xs"
            >
              <option value="">{isEditing ? "-- Chưa phân loại --" : "-- Chọn khối lớp (bắt buộc) --"}</option>
              <option value="10">Khối 10</option>
              <option value="11">Khối 11</option>
              <option value="12">Khối 12</option>
            </select>
          </div>

          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)] mb-1.5">
              Điểm tối đa <span className="text-rose-400">*</span>
            </label>
            <input
              type="number"
              min={1}
              max={100}
              value={maxScore}
              onChange={(e) => setMaxScore(Number(e.target.value))}
              className="th-input w-full text-xs"
            />
          </div>

          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)] mb-1.5">
              Thời gian ước tính (giây) <span className="text-rose-400">*</span>
            </label>
            <input
              type="number"
              min={10}
              max={3600}
              step={10}
              value={estimatedTimeSeconds}
              onChange={(e) => setEstimatedTimeSeconds(Number(e.target.value))}
              className="th-input w-full text-xs"
            />
          </div>
        </div>

        {/* Reasoning Required Toggle */}
        <div className="p-3.5 rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface-subtle)] flex items-center justify-between gap-4">
          <div className="flex items-start gap-3">
            <input
              type="checkbox"
              id="reasoningRequiredToggle"
              checked={reasoningRequired}
              onChange={(e) => setReasoningRequired(e.target.checked)}
              className="accent-teal-500 h-4 w-4 mt-0.5 cursor-pointer"
            />
            <label htmlFor="reasoningRequiredToggle" className="cursor-pointer select-none">
              <span className="text-xs font-semibold text-[var(--th-text)]">
                Yêu cầu học sinh trình bày lời giải
              </span>
              <p className="text-[11px] text-[var(--th-text-muted)] mt-0.5">
                Khi bật, học sinh bắt buộc phải nhập các bước lập luận trước khi nộp bài. Mặc định tắt (chỉ nộp đáp án; nếu học sinh tự nguyện nhập lời giải thì AI vẫn phân tích và cập nhật Digital Twin).
              </p>
            </label>
          </div>
          <span className={`text-[10px] uppercase font-bold px-2.5 py-1 rounded-full border whitespace-nowrap ${
            reasoningRequired
              ? "bg-teal-500/10 text-[var(--th-teal)] border-teal-500/30"
              : "bg-slate-500/10 text-slate-400 border-slate-500/30"
          }`}>
            {reasoningRequired ? "Bắt buộc giải trình" : "Tùy chọn lời giải"}
          </span>
        </div>

        {/* Row 3: Đề bài & Bộ công cụ Toán học */}
        <div className="space-y-3 border-t border-[var(--th-border-subtle)] pt-4">
          <div className="flex items-center justify-between">
            <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)]">
              Nội dung Đề bài {imageDataUrl || hasExistingImage ? <span className="normal-case">(tùy chọn khi có ảnh)</span> : <span className="text-rose-400">*</span>}
            </label>
            <span className="text-[11px] text-[var(--th-teal)]">Hỗ trợ WYSIWYG: Gõ $ hoặc Ctrl+M để nhập công thức</span>
          </div>

          <RichMathEditor
            value={questionText}
            onChange={setQuestionText}
            placeholder="Nhập nội dung đề bài. Ví dụ: Cho hàm số $f(x) = x^3 - 3x^2 + 2$. Tìm giá trị cực đại của hàm số."
            minHeight="110px"
            variant="teacher"
          />
          <QuestionImageEditor value={imageDataUrl} existingQuestionId={id || copyFrom || undefined} hasExistingImage={hasExistingImage}
            onChange={(value, remove) => {
              setImageDataUrl(value); setRemoveImage(remove);
              if (remove && questionText.trim() === IMAGE_ONLY_QUESTION_TEXT) setQuestionText("");
            }} onBusyChange={setImageBusy} />
        </div>

        {/* Row 4: Multiple choice options OR Essay criteria */}
        {questionType === "MultipleChoice" ? (
          <div className="space-y-4 border-t border-[var(--th-border-subtle)] pt-4">
            <div className="flex items-center justify-between">
              <h3 className="text-xs font-semibold uppercase tracking-wider text-[var(--th-text)]">
                Các Phương Án Lựa Chọn (Tích chọn 1 phương án đúng) <span className="text-rose-400">*</span>
              </h3>
              <span className="text-[11px] text-[var(--th-text-muted)]">
                Gợi ý: Nhập khái niệm sai cho các phương án nhiễu để hỗ trợ chẩn đoán AI
              </span>
            </div>

            <div className="space-y-3">
              {options.map((opt, idx) => (
                <div
                  key={opt.label}
                  className={`p-3 rounded-xl border flex flex-col gap-2.5 transition-colors ${
                    opt.isCorrect
                      ? "border-emerald-500/50 bg-emerald-500/10"
                      : "border-[var(--th-border-subtle)] bg-[var(--th-surface-subtle)]"
                  }`}
                >
                  <div className="flex items-center gap-3">
                    <label className="flex items-center gap-2 cursor-pointer font-bold text-xs shrink-0">
                      <input
                        type="radio"
                        name="correctOption"
                        checked={opt.isCorrect}
                        onChange={() => handleOptionCorrectChange(idx)}
                        className="accent-teal-500 h-4 w-4"
                      />
                      <span>Phương án {opt.label}:</span>
                    </label>
                    <RichMathEditor
                      value={opt.text}
                      onChange={(val) => handleOptionTextChange(idx, val)}
                      placeholder={`Nội dung đáp án ${opt.label} (gõ $ để chèn công thức)...`}
                      singleLine={true}
                      minHeight="38px"
                      className="flex-1"
                      variant="teacher"
                    />
                  </div>

                  {!opt.isCorrect && (
                    <div className="flex items-center gap-2 pl-6 pt-1.5 border-t border-[var(--th-border-subtle)]/40">
                      <span className="text-[11px] font-semibold text-sky-500 dark:text-sky-300 whitespace-nowrap flex items-center gap-1">
                        <span>💡</span> Quan niệm sai (Misconception):
                      </span>
                      <input
                        type="text"
                        value={opt.misconception || ""}
                        onChange={(e) => handleOptionMisconceptionChange(idx, e.target.value)}
                        placeholder="VD: Nhầm dấu khi rút gọn, Quên điều kiện xác định, Nhầm định lý..."
                        className="th-input flex-1 text-xs py-1 px-2.5 bg-sky-500/5 border-sky-500/25 placeholder:text-[var(--th-text-muted)]"
                      />
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>
        ) : (
          <div className="space-y-4 border-t border-[var(--th-border-subtle)] pt-4">
            {questionType === "ShortAnswer" && (
              <div>
                <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)] mb-1.5">
                  Chế độ so khớp đáp án (Evaluation Mode)
                </label>
                <select
                  value={answerEvaluationMode}
                  onChange={(e) => {
                    const newMode = e.target.value as QuestionAnswerEvaluationMode;
                    setAnswerEvaluationMode(newMode);
                    const key = getAnswerDraftKey("ShortAnswer", newMode);
                    if (key && modeDrafts[key]) {
                      setActiveDraftValue(modeDrafts[key]!);
                      setCorrectAnswer(serializeAnswerEditorValue(modeDrafts[key]!, newMode));
                    } else {
                      const draft = hydrateAnswerEditorValue("", newMode);
                      setActiveDraftValue(draft);
                      if (key) {
                        setModeDrafts((prev) => ({ ...prev, [key]: draft }));
                      }
                      setCorrectAnswer("");
                    }
                  }}
                  className="th-select w-full text-xs"
                >
                   <option value="TextExact">So khớp chính xác chuỗi (TextExact)</option>
                   <option value="NumericRational">Số hữu tỉ / phân số tương đương (NumericRational)</option>
                   <option value="Coordinate2D">Tọa độ 2D — chấp nhận (1,1), (1;1) và dạng tương đương</option>
                   <option value="MathEquivalent">So khớp toán học giới hạn (MathEquivalent)</option>
                   <option value="Manual">Chấm thủ công / AI Rubric (Manual)</option>
                 </select>
                 <p className="mt-1.5 text-[11px] text-[var(--th-text-muted)]">
                   {answerEvaluationMode === "MathEquivalent" ? MATH_EQUIVALENT_HELP : answerEvaluationMode === "TextExact" ? "So khớp văn bản theo ký tự, không kiểm tra tương đương toán học. Với đáp án số hoặc tập hợp, hãy chọn chế độ toán phù hợp." : null}
                 </p>
                 {answerEvaluationMode === "Coordinate2D" && (
                   <p className="mt-1.5 text-[11px] text-[var(--th-text-muted)]">
                     Đáp án chuẩn phải là một cặp tọa độ, ví dụ (1, 1) hoặc (1/2; 3/4).
                   </p>
                 )}
                 {answerEvaluationMode === "Manual" && (
                   <p className="mt-1.5 text-[11px] text-[var(--th-text-muted)]">
                     Dành cho câu hỏi cần đánh giá qua tiêu chí Rubric hoặc giáo viên chấm duyệt.
                   </p>
                 )}
              </div>
            )}

            <div>
              <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)] mb-1.5">
                Đáp án chuẩn / Kết quả cuối cùng <span className="text-rose-400">*</span>
              </label>
              <div className="rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface-subtle)] p-4">
                <ModeAwareAnswerEditor
                  profile="authoring"
                  variant="teacher"
                  questionType={questionType}
                  evaluationMode={questionType === "Essay" ? "Manual" : (answerEvaluationMode || "TextExact")}
                  value={
                    activeDraftValue ??
                    hydrateAnswerEditorValue(
                      correctAnswer,
                      questionType === "Essay" ? "Manual" : (answerEvaluationMode || "TextExact")
                    )
                  }
                  onChange={(val) => {
                    setActiveDraftValue(val);
                    const evalMode = questionType === "Essay" ? "Manual" : (answerEvaluationMode || "TextExact");
                    const key = getAnswerDraftKey(questionType, evalMode);
                    if (key) {
                      setModeDrafts((prev) => ({ ...prev, [key]: val }));
                    }
                    const serialized = serializeAnswerEditorValue(val, evalMode);
                    setCorrectAnswer(serialized);
                  }}
                />
              </div>
            </div>

            <GradingCriteriaEditor value={gradingCriteria} onChange={setGradingCriteria} maxScore={maxScore} />
          </div>
        )}

        {/* Row 5: Lời giải chi tiết & Dự kiến suy luận */}
        <div className="space-y-4 border-t border-[var(--th-border-subtle)] pt-4">
          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)]">
                Lời giải chi tiết (Solution) <span className="text-rose-400">*</span>
              </label>
            </div>
            <RichMathEditor
              value={solution}
              onChange={setSolution}
              placeholder="Nhập lời giải chuẩn từng bước để hỗ trợ học sinh... (Gõ $ để chèn công thức)"
              minHeight="96px"
              variant="teacher"
            />
          </div>

          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)] mb-1.5">
              Dự kiến suy luận của học sinh (Expected Reasoning)
            </label>
            <textarea
              rows={2}
              value={expectedReasoning}
              onChange={(e) => setExpectedReasoning(e.target.value)}
              placeholder="Giải thích bằng tiếng Việt; có thể giữ từ/câu tiếng Anh. Ví dụ: Dùng thì hiện tại đơn, chủ ngữ she nên chọn goes."
              className="th-input w-full text-xs"
            />
            <p className="mt-1.5 text-xs text-[var(--th-text-muted)]">Đáp án có thể bằng tiếng Anh. Lời giải và lập luận nên giải thích bằng tiếng Việt, giữ nguyên các từ tiếng Anh cần phân tích. AI luôn nhận xét bằng tiếng Việt và chấp nhận cách giải hợp lệ khác lời giải mẫu.</p>
          </div>
        </div>

        {/* Action Buttons */}
        <div className="flex items-center justify-end gap-3 pt-4 border-t border-[var(--th-border-subtle)]">
          <button
            type="button"
            onClick={() => navigate("/giao-vien/cau-hoi")}
            disabled={isPending}
            className="th-secondary-button text-xs py-2 px-4"
          >
            Hủy bỏ
          </button>
          <button
            type="button"
            onClick={handleSave}
            disabled={isPending}
            className="th-primary-button text-xs py-2 px-6 shadow-md shadow-teal-500/20"
          >
            {isPending ? "Đang lưu câu hỏi..." : isEditing ? "Cập nhật câu hỏi" : "Lưu vào Ngân hàng câu hỏi"}
          </button>
        </div>
      </div>
    </div>
  );
}
