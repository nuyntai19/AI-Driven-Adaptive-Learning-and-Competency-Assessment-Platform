import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { organizationApi } from "../api/organizationApi";
import { knowledgeGraphApi } from "../api/knowledgeGraphApi";
import { useQuestion, useCreateQuestion, useUpdateQuestion, useActivateQuestion, useArchiveQuestion } from "../features/questions/useQuestions";
import type { QuestionType, QuestionAnswerEvaluationMode, CreateQuestionRequest, QuestionOption } from "../types/questions";
import { MathInputToolbar } from "../components/math/MathInputToolbar";
import { MathFormulaPreview } from "../components/math/MathFormulaPreview";

export const QuestionEditorPage = () => {
  const [formData, setFormData] = useState<CreateQuestionRequest>({
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
      { optionText: "", isCorrect: false, optionLabel: "B", orderIndex: 1 }
    ],
  });

  const [showQuestionMathToolbar, setShowQuestionMathToolbar] = useState(false);
  const [showSolutionMathToolbar, setShowSolutionMathToolbar] = useState(false);

  const handleInputChange = (field: keyof CreateQuestionRequest, value: any) => {
    if (field === "questionType") {
      const qType = value as QuestionType;
      let newMode: QuestionAnswerEvaluationMode = "TextExact";
      if (qType === "MultipleChoice") {
        newMode = "TextExact";
      } else if (qType === "Essay") {
        newMode = "Manual";
      } else if (qType === "ShortAnswer") {
        newMode = formData.answerEvaluationMode === "TextExact" || formData.answerEvaluationMode === "NumericRational" || formData.answerEvaluationMode === "Manual"
          ? (formData.answerEvaluationMode || "NumericRational")
          : "NumericRational";
      }
      setFormData(prev => ({
        ...prev,
        questionType: qType,
        answerEvaluationMode: newMode,
      }));
      return;
    }
    setFormData(prev => ({ ...prev, [field]: value }));
  };

  const handleOptionChange = (index: number, field: keyof QuestionOption, value: any) => {
    const newOptions = [...(formData.options || [])] as any[];
    newOptions[index] = { ...newOptions[index], [field]: value };

    if (field === 'isCorrect' && value === true) {
      newOptions.forEach((opt, i) => {
        if (i !== index) opt.isCorrect = false;
      });
    }

    setFormData(prev => ({ ...prev, options: newOptions }));
  };

  const addOption = () => {
    const labels = ["A", "B", "C", "D", "E", "F"];
    const currentLength = formData.options?.length || 0;
    if (currentLength >= 6) return;

    const newOptions = [...(formData.options || [])] as any[];
    newOptions.push({
      optionText: "",
      isCorrect: false,
      optionLabel: labels[currentLength] || String.fromCharCode(65 + currentLength),
      orderIndex: currentLength
    });
    setFormData(prev => ({ ...prev, options: newOptions }));
  };

  const removeOption = (index: number) => {
    if ((formData.options?.length || 0) <= 2) return;
    const newOptions = [...(formData.options || [])] as any[];
    newOptions.splice(index, 1);

    const labels = ["A", "B", "C", "D", "E", "F"];
    newOptions.forEach((opt, i) => {
      opt.optionLabel = labels[i] || String.fromCharCode(65 + i);
      opt.orderIndex = i;
    });

    setFormData(prev => ({ ...prev, options: newOptions }));
  };

  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const isEditMode = !!id;

  const { data: questionData } = useQuestion(id || "");
  const createMutation = useCreateQuestion();
  const updateMutation = useUpdateQuestion();
  const activateMutation = useActivateQuestion();
  const archiveMutation = useArchiveQuestion();

  const currentStatus = questionData?.data?.status;

  const handleActivate = () => {
    if (!id || !questionData?.data?.rowVersion) return;
    if (!confirm("Bạn có chắc muốn kích hoạt câu hỏi này? Câu hỏi sẽ có thể được gán vào bài tập.")) return;
    activateMutation.mutate(
      { id, data: { rowVersion: questionData.data.rowVersion } },
      {
        onSuccess: () => {
          alert("Kích hoạt câu hỏi thành công!");
          navigate("/quan-ly/cau-hoi");
        },
        onError: (err: any) => alert("Lỗi: " + (err.response?.data?.detail || err.message))
      }
    );
  };

  const handleArchive = () => {
    if (!id || !questionData?.data?.rowVersion) return;
    if (!confirm("Bạn có chắc muốn lưu trữ câu hỏi này? Câu hỏi sẽ không thể được gán vào bài tập mới.")) return;
    archiveMutation.mutate(
      { id, data: { rowVersion: questionData.data.rowVersion } },
      {
        onSuccess: () => {
          alert("Lưu trữ câu hỏi thành công!");
          navigate("/quan-ly/cau-hoi");
        },
        onError: (err: any) => alert("Lỗi: " + (err.response?.data?.detail || err.message))
      }
    );
  };

  const { data: subjectsData, isLoading: isLoadingSubjects } = useQuery({
    queryKey: ["subjects", "active"],
    queryFn: () => organizationApi.listSubjects(true),
  });

  const { data: nodesData, isLoading: isLoadingNodes } = useQuery({
    queryKey: ["knowledge-nodes", formData.subjectId],
    queryFn: () => knowledgeGraphApi.listNodes(formData.subjectId),
    enabled: !!formData.subjectId,
  });

  // Populate data when in edit mode
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
        answerEvaluationMode: q.answerEvaluationMode || (q.questionType === "MultipleChoice" ? "TextExact" : q.questionType === "Essay" ? "Manual" : "NumericRational"),
        options: (q.options || []).map((opt: any) => ({
          ...opt,
          optionLabel: opt.optionLabel || opt.label,
          optionText: opt.optionText || opt.text,
        })),
        correctAnswer: q.correctAnswer,
        solution: q.solution,
        gradingCriteria: q.gradingCriteria
      });
    }
  }, [isEditMode, questionData]);

  const handleSave = () => {
    // Log payload để debug
    console.log("[QuestionEditor] Submitting payload:", JSON.stringify(formData, null, 2));

    const onError = (err: any) => {
      console.error("[QuestionEditor] API Error:", err);
      console.error("[QuestionEditor] Response data:", err.response?.data);
      console.error("[QuestionEditor] Response status:", err.response?.status);
      
      const serverDetail = err.response?.data?.detail;
      const serverTitle = err.response?.data?.title;
      const serverErrors = err.response?.data?.errors;
      const errorCode = err.response?.data?.extensions?.errorCode;
      
      let msg = "Lỗi không xác định.";
      if (serverDetail) msg = serverDetail;
      else if (serverTitle) msg = serverTitle;
      else if (serverErrors) msg = JSON.stringify(serverErrors);
      else if (err.message) msg = err.message;
      
      if (errorCode) msg += `\n[ErrorCode: ${errorCode}]`;
      
      alert(`Lỗi (${err.response?.status ?? "?"}): ${msg}\n\nXem Console (F12) để biết chi tiết.`);
    };

    if (!isEditMode) {
      // For create, make sure options are cleaned up
      const createData = {
        ...formData,
        answerEvaluationMode: formData.answerEvaluationMode || (formData.questionType === "MultipleChoice" ? "TextExact" : formData.questionType === "Essay" ? "Manual" : "NumericRational"),
        options: formData.options?.map(o => ({
          optionLabel: (o as any).optionLabel || (o as any).label,
          optionText: (o as any).optionText || (o as any).text,
          isCorrect: o.isCorrect,
          orderIndex: o.orderIndex
        }))
      };
      createMutation.mutate(createData as any, {
        onSuccess: () => {
          alert("Tạo câu hỏi thành công!");
          navigate("/quan-ly/cau-hoi");
        },
        onError
      });
    } else {
      const updateData = {
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
        answerEvaluationMode: formData.answerEvaluationMode || (formData.questionType === "MultipleChoice" ? "TextExact" : formData.questionType === "Essay" ? "Manual" : "NumericRational"),
        options: formData.options?.map(o => ({
          optionLabel: (o as any).optionLabel || (o as any).label,
          optionText: (o as any).optionText || (o as any).text,
          isCorrect: o.isCorrect,
          orderIndex: o.orderIndex
        })),
        rowVersion: questionData?.data?.rowVersion || "1"
      };

      updateMutation.mutate({ id: id!, data: updateData as any }, {
        onSuccess: () => {
          alert("Cập nhật câu hỏi thành công!");
          navigate("/quan-ly/cau-hoi");
        },
        onError
      });
    }
  };

  return (
    <div className="p-6 max-w-4xl mx-auto">
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-3xl font-bold text-slate-800">Soạn thảo Câu hỏi</h1>
        <div className="flex gap-3">
          <button
            onClick={() => navigate("/quan-ly/cau-hoi")}
            className="px-4 py-2 border border-slate-300 text-slate-700 rounded-lg hover:bg-slate-50 transition font-medium"
          >
            Hủy
          </button>
          {isEditMode && (currentStatus === 'Draft' || currentStatus === 'Archived') && (
            <button
              onClick={handleActivate}
              disabled={activateMutation.isPending}
              className="px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 transition shadow-sm font-medium disabled:opacity-50"
            >
              Kích hoạt
            </button>
          )}
          {isEditMode && (currentStatus === 'Draft' || currentStatus === 'Active') && (
            <button
              onClick={handleArchive}
              disabled={archiveMutation.isPending}
              className="px-4 py-2 bg-amber-500 text-white rounded-lg hover:bg-amber-600 transition shadow-sm font-medium disabled:opacity-50"
            >
              Lưu trữ
            </button>
          )}
          <button
            onClick={handleSave}
            className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition shadow-sm font-medium"
          >
            Lưu câu hỏi
          </button>
        </div>
      </div>

      <div className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden mb-6">
        <div className="p-6 border-b border-slate-100 bg-slate-50">
          <h2 className="text-lg font-semibold text-slate-800">Thông tin chung</h2>
        </div>
        <div className="p-6 grid grid-cols-1 md:grid-cols-2 gap-6">
          <div className="col-span-2">
            <div className="flex items-center justify-between mb-2">
              <label className="block text-sm font-medium text-slate-700">
                Nội dung câu hỏi <span className="text-red-500">*</span>
              </label>
              <button
                type="button"
                onClick={() => setShowQuestionMathToolbar(!showQuestionMathToolbar)}
                className="text-xs font-semibold px-2.5 py-1 rounded bg-blue-50 hover:bg-blue-100 text-blue-700 border border-blue-200 transition-colors flex items-center gap-1"
              >
                <span>∑ Bảng ký hiệu Toán học (LaTeX)</span>
                <span>{showQuestionMathToolbar ? "▲" : "▼"}</span>
              </button>
            </div>
            {showQuestionMathToolbar && (
              <div className="mb-2">
                <MathInputToolbar
                  onInsert={(sym) =>
                    setFormData((prev) => ({
                      ...prev,
                      questionText: prev.questionText + sym,
                    }))
                  }
                />
              </div>
            )}
            <textarea
              rows={4}
              value={formData.questionText}
              onChange={(e) => handleInputChange("questionText", e.target.value)}
              className="w-full border border-slate-300 rounded-lg px-4 py-3 focus:ring-2 focus:ring-blue-500 outline-none resize-y font-mono text-sm"
              placeholder="Nhập nội dung câu hỏi (hỗ trợ văn bản và công thức LaTeX)..."
            ></textarea>
            {formData.questionText.trim() && (
              <MathFormulaPreview
                formula={formData.questionText}
                label="Xem trước công thức (KaTeX)"
                className="mt-2"
              />
            )}
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700 mb-2">Môn học <span className="text-red-500">*</span></label>
            <select
              value={formData.subjectId}
              onChange={e => handleInputChange("subjectId", e.target.value)}
              disabled={isLoadingSubjects}
              className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none bg-white disabled:bg-slate-100"
            >
              <option value="">-- Chọn môn học --</option>
              {subjectsData?.data?.map((subject: any) => (
                <option key={subject.subjectId} value={subject.subjectId}>
                  {subject.subjectName} — {subject.subjectId}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700 mb-2">Chủ đề kiến thức (Topic Node) <span className="text-red-500">*</span></label>
            <select
              value={formData.primaryTopicNodeId}
              onChange={e => handleInputChange("primaryTopicNodeId", e.target.value)}
              disabled={!formData.subjectId || isLoadingNodes}
              className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none bg-white disabled:bg-slate-100"
            >
              <option value="">
                {!formData.subjectId ? "-- Chọn môn học trước --" : isLoadingNodes ? "Đang tải..." : "-- Chọn chủ đề --"}
              </option>
              {(nodesData ?? []).filter(n => n.isActive).map(node => (
                <option key={node.nodeId} value={node.nodeId}>
                  [{node.nodeType}] {node.nodeName} — ID: {node.nodeId}
                </option>
              ))}
            </select>
            {formData.subjectId && !isLoadingNodes && (nodesData ?? []).length === 0 && (
              <p className="mt-1 text-xs text-amber-600">⚠ Môn học này chưa có chủ đề kiến thức nào. Vui lòng tạo node trên trang Đồ thị kiến thức trước.</p>
            )}
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700 mb-2">Loại câu hỏi <span className="text-red-500">*</span></label>
            <select
              value={formData.questionType}
              onChange={e => handleInputChange("questionType", e.target.value as QuestionType)}
              className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none bg-white"
            >
              <option value="MultipleChoice">Trắc nghiệm (Multiple Choice)</option>
              <option value="ShortAnswer">Điền khuyết (Short Answer)</option>
              <option value="Essay">Tự luận (Essay)</option>
            </select>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700 mb-2">
              Chế độ chấm đáp án (Evaluation Mode) <span className="text-red-500">*</span>
            </label>
            {formData.questionType === "MultipleChoice" ? (
              <div>
                <select
                  disabled
                  value="TextExact"
                  className="w-full border border-slate-300 rounded-lg px-4 py-2 bg-slate-100 text-slate-600 outline-none cursor-not-allowed"
                >
                  <option value="TextExact">TextExact (So khớp chính xác)</option>
                </select>
                <p className="mt-1 text-xs text-slate-500">Trắc nghiệm bắt buộc chế độ TextExact.</p>
              </div>
            ) : formData.questionType === "Essay" ? (
              <div>
                <select
                  disabled
                  value="Manual"
                  className="w-full border border-slate-300 rounded-lg px-4 py-2 bg-slate-100 text-slate-600 outline-none cursor-not-allowed"
                >
                  <option value="Manual">Manual (Giáo viên chấm thủ công)</option>
                </select>
                <p className="mt-1 text-xs text-slate-500">Tự luận bắt buộc chế độ chấm thủ công (Manual).</p>
              </div>
            ) : (
              <div>
                <select
                  value={formData.answerEvaluationMode || "NumericRational"}
                  onChange={e => handleInputChange("answerEvaluationMode", e.target.value as QuestionAnswerEvaluationMode)}
                  className="w-full border border-blue-400 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none bg-white font-medium text-slate-800"
                >
                  <option value="NumericRational">NumericRational (Chuẩn hóa số học & phân số)</option>
                  <option value="TextExact">TextExact (So khớp chuỗi chính xác)</option>
                  <option value="Manual">Manual (Giáo viên chấm thủ công)</option>
                </select>
                <p className="mt-1 text-xs text-blue-600">
                  {formData.answerEvaluationMode === "NumericRational"
                    ? "Tự động quy chuẩn phân số, số thập phân, số hỗn số về dạng tối giản để so sánh chính xác."
                    : formData.answerEvaluationMode === "TextExact"
                    ? "So khớp chuỗi ký tự chính xác tuyệt đối (phân biệt ký tự, khoảng trắng)."
                    : "Giáo viên sẽ xem và chấm điểm trực tiếp."}
                </p>
              </div>
            )}
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700 mb-2">Độ khó (1-5)</label>
            <select
              value={formData.difficulty}
              onChange={e => handleInputChange("difficulty", Number(e.target.value))}
              className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none bg-white"
            >
              <option value="1">1 - Rất dễ</option>
              <option value="2">2 - Dễ</option>
              <option value="3">3 - Trung bình</option>
              <option value="4">4 - Khó</option>
              <option value="5">5 - Rất khó</option>
            </select>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-2">Điểm tối đa</label>
              <input
                type="number"
                min="0" step="0.5"
                value={formData.maxScore}
                onChange={e => handleInputChange("maxScore", Number(e.target.value))}
                className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-2">Thời gian (giây)</label>
              <input
                type="number"
                min="10" step="10"
                value={formData.estimatedTimeSeconds}
                onChange={e => handleInputChange("estimatedTimeSeconds", Number(e.target.value))}
                className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none"
              />
            </div>
          </div>

          <div className="flex items-center gap-3 mt-8">
            <input
              type="checkbox"
              id="reasoningRequired"
              checked={formData.reasoningRequired}
              onChange={e => handleInputChange("reasoningRequired", e.target.checked)}
              className="w-5 h-5 text-blue-600 rounded border-gray-300 focus:ring-blue-500"
            />
            <label htmlFor="reasoningRequired" className="text-sm font-medium text-slate-700 cursor-pointer">
              Bắt buộc học sinh giải thích (Reasoning Required)
            </label>
          </div>
        </div>
      </div>

      <div className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden mb-6">
        <div className="p-6 border-b border-slate-100 bg-slate-50">
          <h2 className="text-lg font-semibold text-slate-800">
            {formData.questionType === "MultipleChoice" && "Đáp án Trắc nghiệm"}
            {formData.questionType === "ShortAnswer" && "Đáp án Điền khuyết"}
            {formData.questionType === "Essay" && "Hướng dẫn & Tiêu chí Tự luận"}
          </h2>
        </div>
        <div className="p-6">
          {formData.questionType === "MultipleChoice" && (
            <div className="space-y-4">
              {formData.options?.map((option, idx) => (
                <div key={idx} className={`flex items-start gap-4 p-4 rounded-lg border ${(option as any).isCorrect ? 'border-green-300 bg-green-50' : 'border-slate-200'}`}>
                  <div className="flex flex-col items-center gap-2 pt-2">
                    <span className="font-bold text-slate-700 w-6 text-center">{(option as any).optionLabel}</span>
                    <input
                      type="radio"
                      name="correctOption"
                      checked={(option as any).isCorrect}
                      onChange={() => handleOptionChange(idx, "isCorrect", true)}
                      className="w-5 h-5 text-green-600 focus:ring-green-500 cursor-pointer"
                    />
                  </div>
                  <div className="flex-1">
                    <input
                      type="text"
                      value={(option as any).optionText}
                      onChange={e => handleOptionChange(idx, "optionText", e.target.value)}
                      placeholder={`Nhập đáp án ${(option as any).optionLabel}...`}
                      className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none bg-white"
                    />
                  </div>
                  <button
                    onClick={() => removeOption(idx)}
                    disabled={(formData.options?.length || 0) <= 2}
                    className="p-2 text-slate-400 hover:text-red-500 disabled:opacity-30 disabled:hover:text-slate-400 transition"
                  >
                    <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                    </svg>
                  </button>
                </div>
              ))}

              {(formData.options?.length || 0) < 6 && (
                <button
                  onClick={addOption}
                  className="mt-2 flex items-center gap-2 text-blue-600 font-medium hover:text-blue-700 px-2 py-2"
                >
                  <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                  </svg>
                  Thêm lựa chọn
                </button>
              )}
              {/* Backend yêu cầu correctAnswer và solution cho mọi loại câu hỏi */}
              <div className="mt-4 space-y-3 border-t border-slate-200 pt-4">
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">
                    Nhãn đáp án đúng (CorrectAnswer) <span className="text-red-500">*</span>
                    <span className="ml-1 text-xs text-slate-500">— VD: "A", "B", hoặc text đáp án đúng</span>
                  </label>
                  <input
                    type="text"
                    value={formData.correctAnswer || ""}
                    onChange={e => handleInputChange("correctAnswer", e.target.value)}
                    className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none"
                    placeholder="Nhập nhãn hoặc nội dung đáp án đúng..."
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">
                    Lời giải / Giải thích (Solution) <span className="text-red-500">*</span>
                  </label>
                  <textarea
                    rows={2}
                    value={formData.solution || ""}
                    onChange={e => handleInputChange("solution", e.target.value)}
                    className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none resize-y"
                    placeholder="Giải thích tại sao đáp án trên là đúng..."
                  ></textarea>
                </div>
              </div>
            </div>
          )}

          {formData.questionType === "ShortAnswer" && (
            <div className="space-y-4">
              {formData.answerEvaluationMode === "NumericRational" && (
                <div className="p-3 bg-blue-50 dark:bg-blue-950/40 rounded-lg border border-blue-200 dark:border-blue-800 text-xs text-blue-800 dark:text-blue-300">
                  <div className="font-semibold flex items-center gap-1 mb-1">
                    <span>💡 Quy chuẩn số học tự động (NumericRational Normalizer):</span>
                  </div>
                  <p>
                    Bạn có thể nhập đáp án chuẩn dưới dạng số nguyên (<code>42</code>), phân số (<code>-3/4</code>), số thập phân (<code>0.75</code> hoặc <code>0,75</code>), hoặc hỗn số (<code>1 1/2</code>). Hệ thống sẽ chuyển đổi chính xác sang phân số tối giản để so khớp với bài làm học sinh.
                  </p>
                </div>
              )}

              <div>
                <label className="block text-sm font-medium text-slate-700 mb-2">
                  Đáp án chuẩn (Correct Answer) <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={formData.correctAnswer || ""}
                  onChange={e => handleInputChange("correctAnswer", e.target.value)}
                  className="w-full border border-slate-300 rounded-lg px-4 py-2 font-mono focus:ring-2 focus:ring-blue-500 outline-none"
                  placeholder="Ví dụ: 3/4 hoặc 0.75 hoặc x^2 + 1..."
                />
              </div>

              <div>
                <div className="flex items-center justify-between mb-2">
                  <label className="block text-sm font-medium text-slate-700">
                    Lời giải (Solution) <span className="text-red-500">*</span>
                  </label>
                  <button
                    type="button"
                    onClick={() => setShowSolutionMathToolbar(!showSolutionMathToolbar)}
                    className="text-xs font-semibold px-2 py-0.5 rounded bg-blue-50 hover:bg-blue-100 text-blue-700 border border-blue-200 transition-colors flex items-center gap-1"
                  >
                    <span>∑ Bảng ký hiệu Toán</span>
                    <span>{showSolutionMathToolbar ? "▲" : "▼"}</span>
                  </button>
                </div>
                {showSolutionMathToolbar && (
                  <div className="mb-2">
                    <MathInputToolbar
                      onInsert={(sym) =>
                        setFormData((prev) => ({
                          ...prev,
                          solution: (prev.solution || "") + sym,
                        }))
                      }
                    />
                  </div>
                )}
                <textarea
                  rows={3}
                  value={formData.solution || ""}
                  onChange={e => handleInputChange("solution", e.target.value)}
                  className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none resize-y font-mono text-sm"
                  placeholder="Giải thích chi tiết các bước tìm ra đáp án..."
                ></textarea>
                {formData.solution && formData.solution.trim() && (
                  <MathFormulaPreview
                    formula={formData.solution}
                    label="Xem trước lời giải (KaTeX)"
                    className="mt-2"
                  />
                )}
              </div>
            </div>
          )}

          {formData.questionType === "Essay" && (
            <div className="space-y-6">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-2">Lời giải mẫu (Solution) <span className="text-red-500">*</span></label>
                <textarea
                  rows={4}
                  value={formData.solution || ""}
                  onChange={e => handleInputChange("solution", e.target.value)}
                  className="w-full border border-slate-300 rounded-lg px-4 py-3 focus:ring-2 focus:ring-blue-500 outline-none resize-y"
                  placeholder="Nhập lời giải hoặc hướng dẫn giải chi tiết..."
                ></textarea>
              </div>

              <div className="bg-slate-50 p-4 rounded-lg border border-slate-200">
                <h3 className="font-semibold text-slate-800 mb-4">Tiêu chí chấm điểm (Grading Criteria)</h3>

                <div className="space-y-4">
                  <div>
                    <label className="block text-sm text-slate-600 mb-1">Các ý bắt buộc (Required Ideas - cách nhau bằng dấu phẩy)</label>
                    <input
                      type="text"
                      value={formData.gradingCriteria?.requiredIdeas?.join(", ") || ""}
                      onChange={e => handleInputChange("gradingCriteria", {
                        ...formData.gradingCriteria,
                        schemaVersion: "1.0",
                        requiredIdeas: e.target.value.split(",").map(i => i.trim()).filter(i => i)
                      })}
                      className="w-full border border-slate-300 rounded-md px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500 outline-none"
                      placeholder="VD: Xác định điều kiện, Đổi cơ số..."
                    />
                  </div>

                  <div>
                    <label className="block text-sm text-slate-600 mb-1">Các lỗi thường gặp (Common Errors - cách nhau bằng dấu phẩy)</label>
                    <input
                      type="text"
                      value={formData.gradingCriteria?.commonErrors?.join(", ") || ""}
                      onChange={e => handleInputChange("gradingCriteria", {
                        ...formData.gradingCriteria,
                        schemaVersion: "1.0",
                        commonErrors: e.target.value.split(",").map(i => i.trim()).filter(i => i)
                      })}
                      className="w-full border border-slate-300 rounded-md px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500 outline-none"
                      placeholder="VD: Quên đặt điều kiện..."
                    />
                  </div>

                  <div>
                    <label className="block text-sm text-slate-600 mb-1">Ghi chú chấm điểm (Scoring Notes)</label>
                    <textarea
                      rows={2}
                      value={formData.gradingCriteria?.scoringNotes || ""}
                      onChange={e => handleInputChange("gradingCriteria", {
                        ...formData.gradingCriteria,
                        schemaVersion: "1.0",
                        scoringNotes: e.target.value
                      })}
                      className="w-full border border-slate-300 rounded-md px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500 outline-none"
                      placeholder="VD: Ưu tiên reasoning, trừ 0.5 nếu thiếu kết luận..."
                    ></textarea>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
