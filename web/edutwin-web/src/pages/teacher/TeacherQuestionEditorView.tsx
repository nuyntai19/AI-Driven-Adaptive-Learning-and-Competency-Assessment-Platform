import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
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
  TeacherMathFormulaPreview,
} from "../../components/teacher";
import { MathInputToolbar } from "../../components/math/MathInputToolbar";

interface QuestionEditorOption {
  optionId?: string;
  label: string;
  text: string;
  isCorrect: boolean;
  orderIndex: number;
}

export function TeacherQuestionEditorView() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const isEditing = Boolean(id);

  const hasPermission = useAuthStore((state) => state.hasPermission);
  const canReadSubjects = hasPermission(permissions.subjectsRead);

  // Form State
  const [subjectId, setSubjectId] = useState("");
  const [primaryTopicNodeId, setPrimaryTopicNodeId] = useState("");
  const [questionType, setQuestionType] = useState<QuestionType>("MultipleChoice");
  const [difficulty, setDifficulty] = useState<number>(3);
  const [questionText, setQuestionText] = useState("");
  const [maxScore, setMaxScore] = useState<number>(10);
  const [estimatedTimeSeconds, setEstimatedTimeSeconds] = useState<number>(120);
  const [reasoningRequired, setReasoningRequired] = useState<boolean>(true);
  const [languageCode] = useState("vi");
  const [answerEvaluationMode, setAnswerEvaluationMode] = useState<QuestionAnswerEvaluationMode>("TextExact");
  const [options, setOptions] = useState<QuestionEditorOption[]>([
    { label: "A", text: "", isCorrect: true, orderIndex: 0 },
    { label: "B", text: "", isCorrect: false, orderIndex: 1 },
    { label: "C", text: "", isCorrect: false, orderIndex: 2 },
    { label: "D", text: "", isCorrect: false, orderIndex: 3 },
  ]);
  const [correctAnswer, setCorrectAnswer] = useState("");
  const [solution, setSolution] = useState("");
  const [expectedReasoning, setExpectedReasoning] = useState("");
  const [gradingCriteria, setGradingCriteria] = useState("");

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
  const { data: questionData, isLoading: questionLoading, refetch: refetchQuestion } = useQuestion(id || "");

  useEffect(() => {
    if (isEditing && questionData?.data) {
      const q = questionData.data;
      setSubjectId(q.subjectId || "");
      setPrimaryTopicNodeId(q.primaryTopicNodeId ? String(q.primaryTopicNodeId) : "");
      setQuestionType(q.questionType);
      setDifficulty(q.difficulty);
      setQuestionText(q.questionText || "");
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
            };
          })
        );
      }
      setCorrectAnswer(q.correctAnswer || "");
      setSolution(q.solution || "");
      setExpectedReasoning(q.expectedReasoning || "");
      setGradingCriteria(
        typeof q.gradingCriteria === "string"
          ? q.gradingCriteria
          : q.gradingCriteria?.scoringNotes || ""
      );
    }
  }, [isEditing, questionData]);

  const createMutation = useCreateQuestion();
  const updateMutation = useUpdateQuestion();

  const handleOptionTextChange = (index: number, text: string) => {
    setOptions((prev) => prev.map((opt, i) => (i === index ? { ...opt, text } : opt)));
  };

  const handleOptionCorrectChange = (index: number) => {
    setOptions((prev) =>
      prev.map((opt, i) => ({
        ...opt,
        isCorrect: i === index,
      }))
    );
  };

  const handleInsertMathToQuestion = (latex: string) => {
    setQuestionText((prev) => prev + latex);
  };

  const handleInsertMathToSolution = (latex: string) => {
    setSolution((prev) => prev + latex);
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
    if (!questionText.trim()) {
      setFormError({ message: "Vui lòng nhập nội dung đề bài câu hỏi." });
      return;
    }
    if (difficulty < 1 || difficulty > 5) {
      setFormError({ message: "Độ khó phải nằm trong khoảng từ 1 đến 5." });
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
    } else if (questionType === "ShortAnswer") {
      if (!correctAnswer.trim()) {
        setFormError({ message: "Vui lòng nhập đáp án chuẩn cho câu hỏi trả lời ngắn." });
        return;
      }
      computedCorrectAnswer = correctAnswer.trim();
    } else if (questionType === "Essay") {
      if (!correctAnswer.trim()) {
        setFormError({ message: "Vui lòng nhập đáp án chuẩn hoặc kết quả mẫu cho câu hỏi tự luận." });
        return;
      }
      computedCorrectAnswer = correctAnswer.trim();
    }

    if (!solution.trim()) {
      setFormError({ message: "Vui lòng nhập lời giải chi tiết (Solution) cho câu hỏi." });
      return;
    }

    const evalMode: QuestionAnswerEvaluationMode =
      questionType === "MultipleChoice"
        ? "TextExact"
        : questionType === "Essay"
        ? "Manual"
        : answerEvaluationMode || "TextExact";

    const parsedGradingCriteria = gradingCriteria.trim()
      ? {
          schemaVersion: "1.0",
          requiredIdeas: [gradingCriteria.trim()],
          commonErrors: [],
          scoringNotes: gradingCriteria.trim(),
        }
      : undefined;

    if (!isEditing) {
      const payload: CreateQuestionRequest = {
        subjectId,
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
                message: mapSafeOperationalError(err, "Không thể cập nhật câu hỏi."),
                traceId: details.traceId,
              });
            }
          },
        }
      );
    }
  };

  const isPending = createMutation.isPending || updateMutation.isPending;

  if (isEditing && questionLoading) {
    return (
      <div className="space-y-4">
        <TeacherSkeleton className="h-10 w-1/3" />
        <TeacherSkeleton className="h-64 rounded-2xl" />
      </div>
    );
  }

  return (
    <div className="space-y-6 max-w-5xl mx-auto">
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
            <select
              value={primaryTopicNodeId}
              disabled={!subjectId || topicsLoading}
              onChange={(e) => setPrimaryTopicNodeId(e.target.value)}
              className="th-select w-full text-xs"
            >
              <option value="">-- Chọn chủ đề kiến thức --</option>
              {topicsData?.nodes?.map((node: any) => (
                <option key={node.nodeId} value={node.nodeId}>
                  {node.nodeName} ({node.nodeCode})
                </option>
              ))}
            </select>
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
                if (newType === "MultipleChoice") {
                  setAnswerEvaluationMode("TextExact");
                } else if (newType === "Essay") {
                  setAnswerEvaluationMode("Manual");
                } else if (newType === "ShortAnswer" && answerEvaluationMode === "Manual") {
                  setAnswerEvaluationMode("TextExact");
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

        {/* Row 2: Độ khó, Điểm số, Thời gian dự kiến */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 border-t border-[var(--th-border-subtle)] pt-4">
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

        {/* Row 3: Đề bài & Bộ công cụ Toán học */}
        <div className="space-y-3 border-t border-[var(--th-border-subtle)] pt-4">
          <div className="flex items-center justify-between">
            <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)]">
              Nội dung Đề bài <span className="text-rose-400">*</span>
            </label>
            <span className="text-[11px] text-[var(--th-teal)]">Hỗ trợ LaTeX: $công\_thức$ hoặc $$khối\_toán$$</span>
          </div>

          {/* Math toolbar */}
          <MathInputToolbar onInsert={handleInsertMathToQuestion} />

          <textarea
            rows={4}
            value={questionText}
            onChange={(e) => setQuestionText(e.target.value)}
            placeholder="Nhập nội dung đề bài. Ví dụ: Cho hàm số $f(x) = x^3 - 3x^2 + 2$. Tìm giá trị cực đại của hàm số."
            className="th-input w-full text-sm font-mono leading-relaxed"
          />

          {/* KaTeX Live Preview */}
          {questionText.trim() && (
            <div className="rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface-subtle)] p-4 space-y-1.5">
              <p className="text-[10px] font-semibold uppercase tracking-wider text-[var(--th-teal)]">
                Xem trước đề bài (KaTeX Live Preview):
              </p>
              <div className="text-sm text-[var(--th-text)]">
                <TeacherMathFormulaPreview content={questionText} />
              </div>
            </div>
          )}
        </div>

        {/* Row 4: Multiple choice options OR Essay criteria */}
        {questionType === "MultipleChoice" ? (
          <div className="space-y-4 border-t border-[var(--th-border-subtle)] pt-4">
            <h3 className="text-xs font-semibold uppercase tracking-wider text-[var(--th-text)]">
              Các Phương Án Lựa Chọn (Tích chọn 1 phương án đúng) <span className="text-rose-400">*</span>
            </h3>

            <div className="space-y-3">
              {options.map((opt, idx) => (
                <div
                  key={opt.label}
                  className={`p-3 rounded-xl border flex items-center gap-3 transition-colors ${
                    opt.isCorrect
                      ? "border-emerald-500/50 bg-emerald-500/10"
                      : "border-[var(--th-border-subtle)] bg-[var(--th-surface-subtle)]"
                  }`}
                >
                  <label className="flex items-center gap-2 cursor-pointer font-bold text-xs">
                    <input
                      type="radio"
                      name="correctOption"
                      checked={opt.isCorrect}
                      onChange={() => handleOptionCorrectChange(idx)}
                      className="accent-teal-500 h-4 w-4"
                    />
                    <span>Phương án {opt.label}:</span>
                  </label>
                  <input
                    type="text"
                    value={opt.text}
                    onChange={(e) => handleOptionTextChange(idx, e.target.value)}
                    placeholder={`Nội dung đáp án ${opt.label} (hỗ trợ LaTeX)...`}
                    className="th-input flex-1 text-xs"
                  />
                  {opt.text.trim() && (
                    <div className="min-w-[120px] text-xs text-[var(--th-text-secondary)]">
                      <TeacherMathFormulaPreview content={opt.text} />
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
                  onChange={(e) => setAnswerEvaluationMode(e.target.value as QuestionAnswerEvaluationMode)}
                  className="th-select w-full text-xs"
                >
                  <option value="TextExact">So khớp chính xác chuỗi (TextExact)</option>
                  <option value="NumericRational">Tương đương số học / đại số / phân số (NumericRational)</option>
                </select>
              </div>
            )}

            <div>
              <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)] mb-1.5">
                Đáp án chuẩn / Kết quả cuối cùng <span className="text-rose-400">*</span>
              </label>
              <input
                type="text"
                value={correctAnswer}
                onChange={(e) => setCorrectAnswer(e.target.value)}
                placeholder="VD: x = 2 hoặc 4/3 hoặc phân số tối giản..."
                className="th-input w-full text-xs"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)] mb-1.5">
                Tiêu chí chấm điểm (Grading Criteria / Rubric)
              </label>
              <textarea
                rows={3}
                value={gradingCriteria}
                onChange={(e) => setGradingCriteria(e.target.value)}
                placeholder="Mô tả các bước tính điểm chi tiết..."
                className="th-input w-full text-xs"
              />
            </div>
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
            <MathInputToolbar onInsert={handleInsertMathToSolution} />
            <textarea
              rows={3}
              value={solution}
              onChange={(e) => setSolution(e.target.value)}
              placeholder="Nhập lời giải chuẩn từng bước để hỗ trợ học sinh..."
              className="th-input w-full text-xs font-mono mt-2"
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
              placeholder="Các bước lập luận dự kiến mà AI hoặc giáo viên cần kiểm tra..."
              className="th-input w-full text-xs"
            />
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
