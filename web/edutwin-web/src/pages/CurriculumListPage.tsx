import { useState } from "react";
import { useCurriculums } from "../features/curriculum/useCurriculums";
import type { ReviewStatus } from "../types/curriculum";

export const CurriculumListPage = () => {
  const [subjectId, setSubjectId] = useState<string>("");
  const [status, setStatus] = useState<ReviewStatus | "">("");
  
  const { data: response, isLoading, isError, error } = useCurriculums(
    subjectId || undefined,
    (status as ReviewStatus) || undefined
  );

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-3xl font-bold text-slate-800">Quản lý Lộ trình học</h1>
        <button className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition shadow-sm font-medium">
          Tạo Lộ trình mới
        </button>
      </div>

      <div className="bg-white p-4 rounded-xl shadow-sm border border-slate-100 mb-6 flex gap-4">
        <div className="flex-1">
          <label className="block text-sm font-medium text-slate-600 mb-1">Môn học</label>
          <input 
            type="text" 
            placeholder="Nhập ID môn học..."
            value={subjectId}
            onChange={e => setSubjectId(e.target.value)}
            className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500 outline-none"
          />
        </div>
        <div className="flex-1">
          <label className="block text-sm font-medium text-slate-600 mb-1">Trạng thái</label>
          <select 
            value={status} 
            onChange={e => setStatus(e.target.value as any)}
            className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500 outline-none bg-white"
          >
            <option value="">Tất cả</option>
            <option value="Draft">Bản nháp (Draft)</option>
            <option value="Published">Đã xuất bản (Published)</option>
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
          <p className="text-slate-500 mb-4">Không tìm thấy lộ trình nào phù hợp.</p>
        </div>
      )}

      {!isLoading && !isError && response?.data && response.data.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {response.data.map((curriculum) => (
            <div key={curriculum.curriculumId} className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden hover:shadow-md transition">
              <div className="p-5">
                <div className="flex justify-between items-start mb-2">
                  <h3 className="font-bold text-lg text-slate-800 line-clamp-1">{curriculum.title}</h3>
                  <span className={`px-2 py-1 text-xs font-semibold rounded-full ${
                    curriculum.reviewStatus === 'Published' ? 'bg-green-100 text-green-700' :
                    curriculum.reviewStatus === 'Draft' ? 'bg-amber-100 text-amber-700' :
                    'bg-slate-100 text-slate-700'
                  }`}>
                    {curriculum.reviewStatus}
                  </span>
                </div>
                <p className="text-sm text-slate-500 mb-4 line-clamp-2">
                  {curriculum.description || "Chưa có mô tả"}
                </p>
                
                <div className="flex justify-between items-center text-sm text-slate-500 pt-4 border-t border-slate-100">
                  <span>Môn học: {curriculum.subjectId.substring(0, 8)}...</span>
                  <button className="text-blue-600 font-medium hover:underline">Chi tiết</button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
