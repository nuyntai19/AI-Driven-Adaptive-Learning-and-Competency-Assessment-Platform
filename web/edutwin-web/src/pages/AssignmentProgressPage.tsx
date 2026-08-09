import { useParams, Link } from "react-router-dom";
import { isAxiosError } from "axios";
import { useAssignment } from "../features/assignments/useAssignment";
import { useAssignmentProgress } from "../features/assignments/useAssignmentProgress";

const progressErrorMessage = (error: unknown) => {
  if (isAxiosError(error)) {
    if (error.response?.status === 404) {
      return "Không tìm thấy dữ liệu tiến độ hoặc bạn không còn quyền truy cập bài tập này.";
    }
    return error.response?.data?.detail || error.message;
  }
  return error instanceof Error ? error.message : "Không thể tải tiến độ bài tập.";
};

export const AssignmentProgressPage = () => {
  const { id } = useParams<{ id: string }>();

  const assignmentQuery = useAssignment(id);
  const progressQuery = useAssignmentProgress(id);

  const assignment = assignmentQuery.data?.data;
  const progressList = progressQuery.data?.data || [];

  if (assignmentQuery.isLoading || progressQuery.isLoading) {
    return <div className="p-10 text-center">Đang tải dữ liệu...</div>;
  }

  if (assignmentQuery.isError || !assignment) {
    return <div className="p-10 text-center text-red-600">Không tìm thấy bài tập hoặc bạn không có quyền truy cập.</div>;
  }

  if (progressQuery.isError) {
    return (
      <div className="p-6 max-w-4xl mx-auto">
        <Link to="/quan-ly/bai-tap" className="mb-4 inline-block text-sm font-medium text-blue-600 hover:underline">
          &larr; Quay lại danh sách bài tập
        </Link>
        <div className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-amber-800">
          <h1 className="font-semibold">Chưa thể tải tiến độ giáo viên</h1>
          <p className="mt-1 text-sm">{progressErrorMessage(progressQuery.error)}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="p-6 max-w-6xl mx-auto">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <Link
            to="/quan-ly/bai-tap"
            className="text-sm font-medium text-blue-600 hover:underline mb-2 inline-block"
          >
            &larr; Quay lại danh sách bài tập
          </Link>
          <h1 className="text-3xl font-bold text-slate-800">
            Tiến độ: {assignment.title}
          </h1>
          <p className="text-slate-500 mt-1">Lớp học: {assignment.classId}</p>
        </div>
      </div>

      <div className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
        {progressList.length === 0 ? (
          <div className="p-10 text-center text-slate-500">
            Chưa có tiến độ học sinh cho bài tập này.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="bg-slate-50 border-b border-slate-200 text-slate-600">
                <tr>
                  <th className="px-6 py-4 font-semibold">Học sinh</th>
                  <th className="px-6 py-4 font-semibold">Trạng thái</th>
                  <th className="px-6 py-4 font-semibold">Số câu hoàn thành</th>
                  <th className="px-6 py-4 font-semibold">Tỷ lệ hoàn thành</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {progressList.map((p) => {
                  const percent =
                    p.totalQuestionCount > 0
                      ? Math.round((p.completedQuestionCount / p.totalQuestionCount) * 100)
                      : 0;

                  return (
                    <tr key={p.studentId} className="hover:bg-slate-50 transition">
                      <td className="px-6 py-4">
                        <div className="font-medium text-slate-800">{p.fullName}</div>
                        <div className="text-xs text-slate-500 mt-1">{p.studentId}</div>
                      </td>
                      <td className="px-6 py-4">
                        <span
                          className={`px-2 py-1 text-xs font-semibold rounded-full ${
                            p.status === "Completed"
                              ? "bg-green-100 text-green-700"
                              : p.status === "InProgress"
                              ? "bg-blue-100 text-blue-700"
                              : p.status === "Overdue"
                              ? "bg-red-100 text-red-700"
                              : "bg-slate-100 text-slate-700"
                          }`}
                        >
                          {p.status}
                        </span>
                      </td>
                      <td className="px-6 py-4 text-slate-600">
                        {p.completedQuestionCount} / {p.totalQuestionCount}
                      </td>
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-3">
                          <div className="w-full bg-slate-200 rounded-full h-2.5">
                            <div
                              className="bg-blue-600 h-2.5 rounded-full"
                              style={{ width: `${percent}%` }}
                            ></div>
                          </div>
                          <span className="text-xs font-medium text-slate-600 w-10">
                            {percent}%
                          </span>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};
