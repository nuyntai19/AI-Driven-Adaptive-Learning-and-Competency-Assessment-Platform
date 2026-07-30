import { useState } from "react";
import type { QuestionType, CreateQuestionRequest, QuestionOption } from "../types/questions";

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
    options: [
      { text: "", isCorrect: true, label: "A" },
      { text: "", isCorrect: false, label: "B" }
    ],
  });

  const handleInputChange = (field: keyof CreateQuestionRequest, value: any) => {
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
      text: "", 
      isCorrect: false, 
      label: labels[currentLength] || String.fromCharCode(65 + currentLength) 
    });
    setFormData(prev => ({ ...prev, options: newOptions }));
  };

  const removeOption = (index: number) => {
    if ((formData.options?.length || 0) <= 2) return;
    const newOptions = [...(formData.options || [])] as any[];
    newOptions.splice(index, 1);
    
    const labels = ["A", "B", "C", "D", "E", "F"];
    newOptions.forEach((opt, i) => {
      opt.label = labels[i] || String.fromCharCode(65 + i);
    });

    setFormData(prev => ({ ...prev, options: newOptions }));
  };

  const handleSave = () => {
    console.log("Saving...", formData);
  };

  return (
    <div className="p-6 max-w-4xl mx-auto">
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-3xl font-bold text-slate-800">Soạn thảo Câu hỏi</h1>
        <div className="flex gap-3">
          <button className="px-4 py-2 border border-slate-300 text-slate-700 rounded-lg hover:bg-slate-50 transition font-medium">
            Hủy
          </button>
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
            <label className="block text-sm font-medium text-slate-700 mb-2">Nội dung câu hỏi <span className="text-red-500">*</span></label>
            <textarea 
              rows={4}
              value={formData.questionText}
              onChange={e => handleInputChange("questionText", e.target.value)}
              className="w-full border border-slate-300 rounded-lg px-4 py-3 focus:ring-2 focus:ring-blue-500 outline-none resize-y"
              placeholder="Nhập nội dung câu hỏi..."
            ></textarea>
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700 mb-2">ID Môn học <span className="text-red-500">*</span></label>
            <input 
              type="text" 
              value={formData.subjectId}
              onChange={e => handleInputChange("subjectId", e.target.value)}
              className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none"
              placeholder="VD: 2ed34b81-..."
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700 mb-2">Topic Node ID <span className="text-red-500">*</span></label>
            <input 
              type="text" 
              value={formData.primaryTopicNodeId}
              onChange={e => handleInputChange("primaryTopicNodeId", e.target.value)}
              className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none"
              placeholder="VD: 101"
            />
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
                    <span className="font-bold text-slate-700 w-6 text-center">{(option as any).label}</span>
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
                      value={option.text}
                      onChange={e => handleOptionChange(idx, "text", e.target.value)}
                      placeholder={`Nhập đáp án ${(option as any).label}...`}
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
            </div>
          )}

          {formData.questionType === "ShortAnswer" && (
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-2">Đáp án chuẩn (Correct Answer) <span className="text-red-500">*</span></label>
              <input 
                type="text" 
                value={formData.correctAnswer || ""}
                onChange={e => handleInputChange("correctAnswer", e.target.value)}
                className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none"
                placeholder="Nhập đáp án chuẩn xác nhất..."
              />
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
