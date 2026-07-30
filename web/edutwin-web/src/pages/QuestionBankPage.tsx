import { useState } from "react";
import { useQuestions } from "../features/questions/useQuestions";
import type { QuestionFilter } from "../types/questions";

export const QuestionBankPage = () => {
  const [filter, setFilter] = useState<QuestionFilter>({});

  const { data: response, isLoading, isError, error } = useQuestions(filter);

  const handleFilterChange = (key: keyof QuestionFilter, value: any) => {
    setFilter(prev => ({
      ...prev,
      [key]: value === "" ? undefined : value
    }));
  };

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-3xl font-bold text-slate-800">Ngân hàng Câu hỏi</h1>
        <button className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition shadow-sm font-medium">
          Tạo Câu hỏi mới
        </button>
      </div>

      <div className="bg-white p-4 rounded-xl shadow-sm border border-slate-100 mb-6 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <div>
          <label className="block text-sm font-medium text-slate-600 mb-1">Môn học (Subject ID)</label>
          <input 
            type="text" 
            placeholder="Nhập ID môn học..."
            value={filter.subjectId || ""}
            onChange={e => handleFilterChange("subjectId", e.target.value)}
            className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500 outline-none"
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-slate-600 mb-1">Loại câu hỏi</label>
          <select 
            value={filter.type || ""} 
            onChange={e => handleFilterChange("type", e.target.value)}
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
            onChange={e => handleFilterChange("difficulty", e.target.value ? Number(e.target.value) : "")}
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
            onChange={e => handleFilterChange("status", e.target.value)}
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
          Đã có lỗi xảy ra: {(error as any)?.response?.data?.detail || error.message}
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
            <div key={question.questionId} className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden hover:shadow-md transition">
              <div className="p-5">
                <div className="flex justify-between items-start mb-3">
                  <span className={`px-2 py-1 text-xs font-semibold rounded-md ${
                    question.questionType === 'MultipleChoice' ? 'bg-blue-50 text-blue-700' :
                    question.questionType === 'Essay' ? 'bg-purple-50 text-purple-700' :
                    'bg-indigo-50 text-indigo-700'
                  }`}>
                    {question.questionType}
                  </span>
                  <span className={`px-2 py-1 text-xs font-semibold rounded-full ${
                    question.status === 'Active' ? 'bg-green-100 text-green-700' :
                    question.status === 'Draft' ? 'bg-amber-100 text-amber-700' :
                    'bg-slate-100 text-slate-700'
                  }`}>
                    {question.status}
                  </span>
                </div>
                <h3 className="font-medium text-slate-800 line-clamp-2 mb-3">
                  {question.questionText}
                </h3>
                
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

                <div className="flex justify-end pt-4 border-t border-slate-100">
                  <button className="text-blue-600 text-sm font-medium hover:underline">Chi tiết</button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
