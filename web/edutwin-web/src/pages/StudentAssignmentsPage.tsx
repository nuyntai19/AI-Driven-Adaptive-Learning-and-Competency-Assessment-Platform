import { useState } from "react";
import { isAxiosError } from "axios";
import { Link } from "react-router-dom";
import { useStudentAssignments } from "../features/assignments/useStudentAssignments";
import type { ProgressStatus, StudentAssignmentListItemDto } from "../types/assignments";

const getStudentListError = (error: unknown) => {
  if (isAxiosError(error)) return error.response?.data?.detail || error.message;
  return error instanceof Error ? error.message : "Không thể tải danh sách bài tập.";
};

export const StudentAssignmentsPage = () => {
  const [status, setStatus] = useState<ProgressStatus | "">("");

  const { data: response, isLoading, isError, error } = useStudentAssignments({
    status: status || undefined,
  });

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <div className="mb-6">
        <h1 className="text-3xl font-bold text-slate-800">Bài tập của tôi</h1>
        <p className="text-slate-500 mt-2">Xem danh sách các bài tập được giao cho bạn.</p>
      </div>

      <div className="bg-white p-4 rounded-xl shadow-sm border border-slate-100 mb-6 w-full md:w-1/3">
        <label className="block text-sm font-medium text-slate-600 mb-1">Trạng thái</label>
        <select
          value={status}
          onChange={(e) => setStatus(e.target.value as ProgressStatus | "")}
          className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500 outline-none bg-white"
        >
          <option value="">Tất cả</option>
          <option value="NotStarted">Chưa bắt đầu</option>
          <option value="InProgress">Đang làm</option>
          <option value="Completed">Đã hoàn thành</option>
          <option value="Overdue">Quá hạn</option>
        </select>
      </div>

      {isLoading && (
        <div className="flex justify-center items-center py-20">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
        </div>
      )}

      {isError && (
        <div className="bg-red-50 text-red-600 p-4 rounded-lg border border-red-100">
          Đã có lỗi xảy ra: {getStudentListError(error)}
        </div>
      )}

      {!isLoading && !isError && (!response?.data || response.data.length === 0) && (
        <div className="text-center py-20 bg-slate-50 rounded-xl border border-slate-100">
          <p className="text-slate-500 mb-4">Bạn chưa có bài tập nào.</p>
        </div>
      )}

      {!isLoading && !isError && response?.data && response.data.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {response.data.map((assignment: StudentAssignmentListItemDto) => {
            const progress = assignment.progress;
            const percent =
              progress.totalQuestionCount > 0
                ? Math.round((progress.completedQuestionCount / progress.totalQuestionCount) * 100)
                : 0;

            return (
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
                        progress.status === "Completed"
                          ? "bg-green-100 text-green-700"
                          : progress.status === "Overdue"
                          ? "bg-red-100 text-red-700"
                          : progress.status === "InProgress"
                          ? "bg-blue-100 text-blue-700"
                          : "bg-slate-100 text-slate-700"
                      }`}
                    >
                      {progress.status}
                    </span>
                  </div>
                  <p className="text-sm text-slate-500 mb-4 line-clamp-2">
                    {assignment.instructions || "Không có hướng dẫn"}
                  </p>
                  <div className="text-xs text-slate-500 mb-4">
                    {assignment.dueAt && (
                      <p>Hạn chót: {new Date(assignment.dueAt).toLocaleString("vi-VN")}</p>
                    )}
                  </div>
                  <div className="mt-auto">
                    <div className="flex justify-between text-xs text-slate-600 mb-1">
                      <span>Tiến độ</span>
                      <span>
                        {progress.completedQuestionCount}/{progress.totalQuestionCount}
                      </span>
                    </div>
                    <div className="w-full bg-slate-200 rounded-full h-2">
                      <div
                        className="bg-blue-600 h-2 rounded-full"
                        style={{ width: `${percent}%` }}
                      ></div>
                    </div>
                  </div>
                </div>
                <div className="bg-slate-50 px-5 py-3 border-t border-slate-100 flex justify-end">
                  <Link
                    to={`/hoc-tap/bai-tap/${assignment.assignmentId}`}
                    className="text-sm font-medium text-blue-600 hover:text-blue-700"
                  >
                    Xem chi tiết &rarr;
                  </Link>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
