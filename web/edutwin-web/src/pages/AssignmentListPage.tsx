import { useState } from "react";
import { isAxiosError } from "axios";
import { Link } from "react-router-dom";
import { useAssignments } from "../features/assignments/useAssignments";
import type { AssignmentStatus } from "../types/assignments";
import { useAuthStore } from "../stores/authStore";
import { permissions } from "../auth/permissions";

const getListError = (error: unknown) => {
  if (isAxiosError(error)) return error.response?.data?.detail || error.message;
  return error instanceof Error ? error.message : "Không thể tải danh sách bài tập.";
};

export const AssignmentListPage = () => {
  const [classId, setClassId] = useState<string>("");
  const [status, setStatus] = useState<AssignmentStatus | "">("");
  const canCreate = useAuthStore((state) => state.hasPermission(permissions.assignmentsCreate));
  const canUpdate = useAuthStore((state) => state.hasPermission(permissions.assignmentsUpdate));

  const { data: response, isLoading, isError, error } = useAssignments({
    classId: classId || undefined,
    status: (status as AssignmentStatus) || undefined,
  });

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-3xl font-bold text-slate-800">Quản lý Bài tập</h1>
        {canCreate && <Link
          to="/quan-ly/bai-tap/tao-moi"
          className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition shadow-sm font-medium"
        >
          Tạo bài tập mới
        </Link>}
      </div>

      <div className="bg-white p-4 rounded-xl shadow-sm border border-slate-100 mb-6 flex gap-4">
        <div className="flex-1">
          <label className="block text-sm font-medium text-slate-600 mb-1">ID Lớp học</label>
          <input
            type="text"
            placeholder="Nhập ID lớp học..."
            value={classId}
            onChange={(e) => setClassId(e.target.value)}
            className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500 outline-none"
          />
        </div>
        <div className="flex-1">
          <label className="block text-sm font-medium text-slate-600 mb-1">Trạng thái</label>
          <select
            value={status}
            onChange={(e) => setStatus(e.target.value as AssignmentStatus | "")}
            className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500 outline-none bg-white"
          >
            <option value="">Tất cả</option>
            <option value="Draft">Bản nháp (Draft)</option>
            <option value="Published">Đã xuất bản (Published)</option>
            <option value="Closed">Đã đóng (Closed)</option>
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
          Đã có lỗi xảy ra: {getListError(error)}
        </div>
      )}

      {!isLoading && !isError && (!response?.data || response.data.length === 0) && (
        <div className="text-center py-20 bg-slate-50 rounded-xl border border-slate-100">
          <p className="text-slate-500 mb-4">Không tìm thấy bài tập nào phù hợp.</p>
        </div>
      )}

      {!isLoading && !isError && response?.data && response.data.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {response.data.map((assignment) => (
            <div
              key={assignment.assignmentId}
              className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden hover:shadow-md transition flex flex-col"
            >
              <div className="p-5 flex-1">
                <div className="flex justify-between items-start mb-2">
                  <h3 className="font-bold text-lg text-slate-800 line-clamp-1">
                    {assignment.title}
                  </h3>
                  <span
                    className={`px-2 py-1 text-xs font-semibold rounded-full ${
                      assignment.status === "Published"
                        ? "bg-green-100 text-green-700"
                        : assignment.status === "Closed"
                        ? "bg-red-100 text-red-700"
                        : assignment.status === "Draft"
                        ? "bg-amber-100 text-amber-700"
                        : "bg-slate-100 text-slate-700"
                    }`}
                  >
                    {assignment.status}
                  </span>
                </div>
                <p className="text-sm text-slate-500 mb-2 line-clamp-2">
                  {assignment.instructions || "Chưa có hướng dẫn"}
                </p>
                <div className="text-xs text-slate-500 space-y-1">
                  <p>Lớp học: {assignment.classId}</p>
                  <p>Số câu hỏi: {assignment.questionCount}</p>
                  <p>Học sinh được giao: {assignment.targetStudentCount}</p>
                  {assignment.dueAt && (
                    <p>Hạn chót: {new Date(assignment.dueAt).toLocaleString("vi-VN")}</p>
                  )}
                </div>
              </div>
              <div className="bg-slate-50 px-5 py-3 border-t border-slate-100 flex justify-end gap-3">
                {canUpdate && <Link
                  to={`/quan-ly/bai-tap/${assignment.assignmentId}`}
                  className="text-sm font-medium text-blue-600 hover:text-blue-700"
                >
                  Chi tiết
                </Link>}
                {(assignment.status === "Published" || assignment.status === "Closed") && (
                  <Link
                    to={`/quan-ly/bai-tap/${assignment.assignmentId}/tien-do`}
                    className="text-sm font-medium text-indigo-600 hover:text-indigo-700"
                  >
                    Tiến độ
                  </Link>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
