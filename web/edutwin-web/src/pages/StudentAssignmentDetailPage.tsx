import { useParams, Link } from "react-router-dom";
import { isAxiosError } from "axios";
import { useStudentAssignment } from "../features/assignments/useStudentAssignment";

const getStudentDetailError = (error: unknown) => {
  if (isAxiosError(error)) return error.response?.data?.detail || error.message;
  return error instanceof Error ? error.message : "Không tìm thấy bài tập.";
};

export const StudentAssignmentDetailPage = () => {
  const { id } = useParams<{ id: string }>();
  const { data: assignmentData, isLoading, isError, error } = useStudentAssignment(id);

  const assignment = assignmentData?.data;

  if (isLoading) {
    return (
      <div className="flex justify-center items-center min-h-[50vh]">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
      </div>
    );
  }

  if (isError || !assignment) {
    return (
      <div className="p-6 max-w-4xl mx-auto">
        <div className="bg-red-50 text-red-600 p-4 rounded-lg border border-red-100">
          Đã có lỗi xảy ra: {getStudentDetailError(error)}
        </div>
        <Link to="/hoc-tap/bai-tap" className="text-blue-600 mt-4 inline-block hover:underline">
          &larr; Quay lại danh sách
        </Link>
      </div>
    );
  }

  const { progress } = assignment;
  const percent =
    progress.totalQuestionCount > 0
      ? Math.round((progress.completedQuestionCount / progress.totalQuestionCount) * 100)
      : 0;

  return (
    <div className="p-6 max-w-4xl mx-auto">
      <Link
        to="/hoc-tap/bai-tap"
        className="text-sm font-medium text-blue-600 hover:underline mb-4 inline-block"
      >
        &larr; Quay lại danh sách
      </Link>

      <div className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden mb-8">
        <div className="p-6 border-b border-slate-100">
          <div className="flex justify-between items-start mb-2">
            <h1 className="text-2xl font-bold text-slate-800">{assignment.title}</h1>
            <span
              className={`px-3 py-1 text-sm font-semibold rounded-full ${
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
          <p className="text-slate-600 mt-4 whitespace-pre-line">
            {assignment.instructions || "Không có hướng dẫn thêm từ giáo viên."}
          </p>
          {assignment.dueAt && (
            <p className="text-sm text-amber-700 mt-4 font-medium">
              Hạn chót: {new Date(assignment.dueAt).toLocaleString("vi-VN")}
            </p>
          )}
        </div>
        <div className="bg-slate-50 p-6">
          <div className="flex justify-between items-center mb-2">
            <h3 className="font-semibold text-slate-700">Tiến độ hoàn thành</h3>
            <span className="text-sm font-medium text-slate-600">
              {progress.completedQuestionCount} / {progress.totalQuestionCount} câu hỏi
            </span>
          </div>
          <div className="w-full bg-slate-200 rounded-full h-3 mb-1">
            <div
              className="bg-blue-600 h-3 rounded-full transition-all duration-500"
              style={{ width: `${percent}%` }}
            ></div>
          </div>
        </div>
      </div>

      <h2 className="text-xl font-bold text-slate-800 mb-4">Danh sách câu hỏi</h2>
      <div className="space-y-4">
        {assignment.questions.map((question, index) => (
          <div
            key={question.questionId}
            className="bg-white p-5 rounded-xl shadow-sm border border-slate-200"
          >
            <div className="flex justify-between items-start mb-3">
              <h3 className="font-semibold text-slate-800">
                Câu {index + 1}{" "}
                <span className="text-xs font-normal text-slate-500 ml-2 bg-slate-100 px-2 py-1 rounded">
                  ID: {question.questionId}
                </span>
              </h3>
              {question.attemptStatus && (
                <span className="text-xs px-2 py-1 bg-green-100 text-green-700 rounded-full font-medium">
                  {question.attemptStatus}
                </span>
              )}
            </div>

            <div className="prose prose-slate max-w-none text-sm text-slate-700 mb-4">
              {question.questionText}
            </div>

            {question.options && question.options.length > 0 && (
              <div className="space-y-2 mt-4">
                {question.options.map((opt) => (
                  <div
                    key={opt.optionId}
                    className="flex items-center gap-3 p-3 rounded-lg border border-slate-200 bg-slate-50"
                  >
                    <div className="font-medium text-slate-700 w-6 h-6 rounded-full bg-white border border-slate-300 flex items-center justify-center text-xs">
                      {opt.label}
                    </div>
                    <div className="text-sm text-slate-700">{opt.text}</div>
                  </div>
                ))}
              </div>
            )}

            <div className="mt-4 pt-4 border-t border-slate-100 flex justify-between items-center text-xs text-slate-500">
              <div className="flex gap-4">
                <span>Loại: {question.questionType}</span>
                <span>Mức độ: {question.difficulty}</span>
                <span>TG dự kiến: {question.estimatedTimeSeconds}s</span>
              </div>
              <button
                disabled
                className="px-4 py-2 bg-slate-200 text-slate-500 rounded font-medium cursor-not-allowed"
                title="Tính năng làm bài sẽ có trong bản cập nhật tới (P11)"
              >
                Vào làm bài
              </button>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};
