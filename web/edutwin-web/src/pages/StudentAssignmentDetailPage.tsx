import React from "react";
import { useParams, Link, useSearchParams } from "react-router-dom";
import { isAxiosError } from "axios";
import { useStudentAssignment } from "../features/assignments/useStudentAssignment";
import { MathFormulaPreview } from "../components/math/MathFormulaPreview";
import type { ProgressStatus, StudentAssignmentQuestionDto } from "../types/assignments";

const getStudentDetailError = (error: unknown) => {
  if (isAxiosError(error)) return error.response?.data?.detail || error.message;
  return error instanceof Error ? error.message : "Không tìm thấy bài tập.";
};

const getDifficultyLabel = (difficulty?: number) => {
  if (!difficulty) return "Nhận biết";
  if (difficulty >= 4) return "Vận dụng cao";
  if (difficulty >= 3) return "Vận dụng";
  if (difficulty >= 2) return "Thông hiểu";
  return "Nhận biết";
};

const getQuestionTypeLabel = (qType?: string) => {
  switch (qType) {
    case "MultipleChoice":
      return "Trắc nghiệm";
    case "Numeric":
    case "NumericRational":
      return "Điền đáp án";
    case "Essay":
      return "Tự luận";
    default:
      return "Trắc nghiệm";
  }
};

export const StudentAssignmentDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const [searchParams] = useSearchParams();
  const selectedSubjectId = searchParams.get("subjectId") || "";

  const { data: assignmentData, isLoading, isError, error } = useStudentAssignment(id);
  const assignment = assignmentData?.data;

  if (isLoading) {
    return (
      <div className="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
        <div className="h-6 w-40 bg-slate-200 dark:bg-slate-800 animate-pulse rounded-lg" />
        <div className="h-56 bg-white dark:bg-[#0f172a] animate-pulse rounded-2xl border border-slate-200 dark:border-slate-800" />
        <div className="space-y-4">
          {[1, 2, 3].map((i) => (
            <div key={i} className="h-36 bg-white dark:bg-[#0f172a] animate-pulse rounded-2xl border border-slate-200 dark:border-slate-800" />
          ))}
        </div>
      </div>
    );
  }

  if (isError || !assignment) {
    return (
      <div className="max-w-4xl mx-auto px-4 py-12">
        <div className="rounded-2xl bg-white dark:bg-[#0f172a] p-8 border border-slate-200 dark:border-slate-800 text-center shadow-sm">
          <p className="text-sm font-bold text-rose-600 dark:text-rose-400 mb-4">
            Đã có lỗi xảy ra: {getStudentDetailError(error)}
          </p>
          <Link
            to={`/hoc-tap/bai-tap${selectedSubjectId ? `?subjectId=${selectedSubjectId}` : ""}`}
            className="inline-flex items-center gap-1.5 text-xs font-bold text-indigo-600 dark:text-indigo-400 hover:text-indigo-800"
          >
            <span>← Quay lại danh sách bài tập</span>
          </Link>
        </div>
      </div>
    );
  }

  const { progress, questions } = assignment;
  const percent =
    progress.totalQuestionCount > 0
      ? Math.round((progress.completedQuestionCount / progress.totalQuestionCount) * 100)
      : 0;

  const isAssignmentFinished =
    progress.status === "Completed" ||
    (questions.length > 0 &&
      questions.every(
        (q) => Boolean(q.latestAttempt) || q.attemptStatus === "Completed" || q.attemptStatus === "NeedsTeacherReview"
      ));

  // Find first uncompleted question for "Bắt đầu làm bài tập ngay"
  const firstUnfinishedQuestion = questions.find(
    (q) => !q.latestAttempt && q.attemptStatus !== "Completed" && q.attemptStatus !== "NeedsTeacherReview"
  );

  const getStatusBadge = (status: ProgressStatus) => {
    switch (status) {
      case "Completed":
        return (
          <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800">
            Đã hoàn thành
          </span>
        );
      case "InProgress":
        return (
          <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold bg-indigo-50 dark:bg-indigo-950/40 text-indigo-700 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-800">
            Đang làm
          </span>
        );
      case "Overdue":
        return (
          <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold bg-rose-50 dark:bg-rose-950/40 text-rose-700 dark:text-rose-400 border border-rose-200 dark:border-rose-800">
            Quá hạn
          </span>
        );
      case "NotStarted":
      default:
        return (
          <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 border border-slate-200 dark:border-slate-700">
            Chưa bắt đầu
          </span>
        );
    }
  };

  const getAttemptStatusBadge = (question: StudentAssignmentQuestionDto) => {
    const status = question.attemptStatus;
    const attempt = question.latestAttempt;

    if (attempt?.skipped) {
      return (
        <span className="inline-flex items-center gap-1 text-xs font-bold text-amber-700 dark:text-amber-400 bg-amber-50 dark:bg-amber-950/40 px-2.5 py-1 rounded-full border border-amber-200 dark:border-amber-800">
          <span>Đã bỏ qua</span>
        </span>
      );
    }

    if (!status && !attempt) {
      return (
        <span className="inline-flex items-center text-xs font-semibold text-slate-400 dark:text-slate-500">
          Chưa làm
        </span>
      );
    }

    if (status === "Completed") {
      return (
        <span className="inline-flex items-center gap-1 text-xs font-bold text-emerald-600 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-950/40 px-2.5 py-1 rounded-full border border-emerald-200 dark:border-emerald-800">
          <span>Đã hoàn thành</span>
        </span>
      );
    }

    if (status === "NeedsTeacherReview") {
      return (
        <span className="inline-flex items-center gap-1 text-xs font-bold text-amber-700 dark:text-amber-400 bg-amber-50 dark:bg-amber-950/40 px-2.5 py-1 rounded-full border border-amber-200 dark:border-amber-800">
          <span>Chờ giáo viên duyệt</span>
        </span>
      );
    }

    return (
      <span className="inline-flex items-center gap-1 text-xs font-bold text-indigo-700 dark:text-indigo-300 bg-indigo-50 dark:bg-indigo-950/40 px-2.5 py-1 rounded-full border border-indigo-200 dark:border-indigo-800">
        <span className="inline-block w-1.5 h-1.5 rounded-full bg-indigo-500 animate-pulse" />
        <span>Đang phân tích AI</span>
      </span>
    );
  };

  return (
    <div className="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6">
      {/* Back link */}
      <div>
        <Link
          to={`/hoc-tap/bai-tap${selectedSubjectId ? `?subjectId=${selectedSubjectId}` : ""}`}
          className="inline-flex items-center gap-2 px-5 py-2.5 rounded-2xl bg-white dark:bg-[#0f172a] border border-slate-300 dark:border-slate-700 text-sm font-extrabold text-slate-900 dark:text-slate-100 hover:text-indigo-600 dark:hover:text-indigo-400 hover:border-indigo-400 dark:hover:border-indigo-500 hover:bg-indigo-50/50 dark:hover:bg-indigo-950/40 shadow-xs transition-all group cursor-pointer"
        >
          <span className="text-base group-hover:-translate-x-1 transition-transform">‹</span>
          <span>Quay lại danh sách bài tập</span>
        </Link>
      </div>

      {/* Assignment Overview Hero Box */}
      <div className="rounded-3xl bg-white dark:bg-[#0f172a] border border-slate-200/90 dark:border-slate-800 p-7 sm:p-9 shadow-xs space-y-6">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div className="flex flex-wrap items-center gap-2.5">
            {getStatusBadge(progress.status)}
            <span className="rounded-xl bg-indigo-50 dark:bg-indigo-950/70 border border-indigo-200 dark:border-indigo-800 px-3 py-1 text-xs font-black text-indigo-700 dark:text-indigo-300">
              Bài tập rèn luyện năng lực
            </span>
          </div>

          {isAssignmentFinished ? (
            <Link
              to={`/hoc-tap/luyen-tap/${questions[0]?.questionId || ""}?assignmentId=${assignment.assignmentId}${selectedSubjectId ? `&subjectId=${selectedSubjectId}` : ""}`}
              className="inline-flex items-center justify-center gap-2 rounded-2xl bg-slate-900 hover:bg-slate-800 dark:bg-slate-700 dark:hover:bg-slate-600 px-7 py-3 text-sm font-black text-white shadow-xs transition-all cursor-pointer self-start sm:self-auto"
            >
              <span>👁️ Xem lại toàn bộ bài làm</span>
            </Link>
          ) : firstUnfinishedQuestion ? (
            <Link
              to={`/hoc-tap/luyen-tap/${firstUnfinishedQuestion.questionId}?assignmentId=${assignment.assignmentId}${selectedSubjectId ? `&subjectId=${selectedSubjectId}` : ""}`}
              className="inline-flex items-center justify-center gap-2 rounded-2xl bg-indigo-600 hover:bg-indigo-500 px-7 py-3 text-sm font-black text-white shadow-md shadow-indigo-600/25 transition-all cursor-pointer self-start sm:self-auto"
            >
              <span>{progress.completedQuestionCount > 0 ? "Tiếp tục làm bài tập →" : "🚀 Bắt đầu làm bài tập ngay"}</span>
            </Link>
          ) : null}
        </div>

        <div>
          <h1 className="text-2xl sm:text-3xl font-black text-slate-900 dark:text-white tracking-tight">
            {assignment.title}
          </h1>
          <p className="mt-2.5 text-sm sm:text-base text-slate-700 dark:text-slate-300 leading-relaxed font-medium">
            {assignment.instructions ||
              "Hoàn thành các câu hỏi dưới đây và trình bày đầy đủ các bước suy luận để EduTwin phân tích chất lượng tư duy của bạn."}
          </p>
        </div>

        {/* Progress & Due date row */}
        <div className="pt-4 border-t border-slate-100 dark:border-slate-800 space-y-2.5">
          <div className="flex items-center justify-between text-sm mb-1">
            <span className="font-extrabold text-slate-900 dark:text-slate-100">
              Tiến độ: {progress.completedQuestionCount}/{progress.totalQuestionCount} câu ({percent}%)
            </span>
            {assignment.dueAt && (
              <span className="font-bold text-slate-600 dark:text-slate-400">
                Hạn chót:{" "}
                {new Date(assignment.dueAt).toLocaleDateString("vi-VN", {
                  hour: "2-digit",
                  minute: "2-digit",
                  day: "2-digit",
                  month: "2-digit",
                  year: "numeric",
                })}
              </span>
            )}
          </div>
          <div className="w-full bg-slate-100 dark:bg-slate-800 rounded-full h-3 overflow-hidden">
            <div
              className={`h-full rounded-full transition-all duration-500 ${
                progress.status === "Completed"
                  ? "bg-emerald-500"
                  : progress.status === "Overdue"
                  ? "bg-rose-500"
                  : "bg-indigo-600"
              }`}
              style={{ width: `${percent}%` }}
            />
          </div>
        </div>
      </div>

      {/* Questions list header */}
      <div className="flex items-center justify-between pt-2">
        <h2 className="text-xl font-black text-slate-900 dark:text-white tracking-tight">
          Danh sách câu hỏi trong bài
        </h2>
        <span className="text-sm font-bold text-slate-500 dark:text-slate-400">
          {questions.length} câu hỏi · Dự kiến {Math.max(30, questions.length * 4)} phút
        </span>
      </div>

      {/* Question Items List */}
      <div className="space-y-4">
        {questions.map((question, index) => {
          const formattedId = `Q-${String(index + 1).padStart(2, "0")}-${question.questionId.slice(0, 4)}`;
          const timeSec = question.estimatedTimeSeconds || 120;
          const hasMath = /[\\[{^_\\]]/.test(question.questionText);

          return (
            <div
              key={question.questionId}
              className="rounded-3xl bg-white dark:bg-[#0f172a] border border-slate-200/90 dark:border-slate-800 p-6 sm:p-7 shadow-xs hover:border-indigo-300 dark:hover:border-indigo-700 hover:shadow-sm transition-all"
            >
              {/* Question header */}
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 mb-4 border-b border-slate-100 dark:border-slate-800">
                <div className="flex flex-wrap items-center gap-2.5">
                  <span className="text-base font-black text-slate-900 dark:text-white">
                    Câu {index + 1}
                  </span>
                  <span className="text-xs font-mono font-bold text-slate-600 dark:text-slate-300 bg-slate-100 dark:bg-slate-800 px-2.5 py-1 rounded-lg">
                    {formattedId}
                  </span>
                  <span className="rounded-xl bg-indigo-50 dark:bg-indigo-950/70 border border-indigo-200 dark:border-indigo-800 px-3 py-1 text-xs font-bold text-indigo-700 dark:text-indigo-300">
                    {getQuestionTypeLabel(question.questionType)}
                  </span>
                  <span className="rounded-xl bg-amber-50 dark:bg-amber-950/70 border border-amber-200 dark:border-amber-800 px-3 py-1 text-xs font-bold text-amber-700 dark:text-amber-300">
                    {getDifficultyLabel(question.difficulty)}
                  </span>
                </div>

                <div>
                  {getAttemptStatusBadge(question)}
                </div>
              </div>

              {/* Question content with LaTeX formula support */}
              <div className="text-base sm:text-lg text-slate-900 dark:text-slate-100 leading-relaxed whitespace-pre-wrap font-bold">
                {question.questionText}
              </div>

              {/* KaTeX preview if formula detected */}
              {hasMath && (
                <div className="mt-3">
                  <MathFormulaPreview
                    formula={question.questionText}
                    label="Công thức toán học"
                  />
                </div>
              )}

              {/* Multiple choice options preview if present */}
              {question.options && question.options.length > 0 && (
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mt-4">
                  {question.options.map((opt) => (
                    <div
                      key={opt.optionId}
                      className="flex items-center gap-3 p-3.5 rounded-2xl border border-slate-200 dark:border-slate-800 bg-slate-50/80 dark:bg-slate-800/80 text-sm"
                    >
                      <span className="w-6 h-6 rounded-full bg-white dark:bg-slate-700 border border-slate-300 dark:border-slate-600 font-extrabold text-slate-800 dark:text-slate-100 flex items-center justify-center text-xs shrink-0">
                        {opt.label}
                      </span>
                      <span className="text-slate-900 dark:text-slate-200 font-semibold truncate">{opt.text}</span>
                    </div>
                  ))}
                </div>
              )}

              {/* Bottom footer: Time estimate & Direct action button */}
              <div className="mt-5 pt-4 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between">
                <span className="text-xs sm:text-sm font-bold text-slate-500 dark:text-slate-400">
                  ⏱ Thời gian dự kiến: {timeSec} giây
                </span>

                {(() => {
                  const isDone =
                    Boolean(question.latestAttempt) ||
                    question.attemptStatus === "Completed" ||
                    question.attemptStatus === "NeedsTeacherReview";

                  return (
                    <Link
                      to={`/hoc-tap/luyen-tap/${question.questionId}?assignmentId=${assignment.assignmentId}${selectedSubjectId ? `&subjectId=${selectedSubjectId}` : ""}`}
                      className={`inline-flex items-center gap-2 px-4 py-2 rounded-xl text-xs sm:text-sm font-black transition-all cursor-pointer ${
                        isDone
                          ? "bg-emerald-50 hover:bg-emerald-100 text-emerald-800 dark:bg-emerald-950/70 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800"
                          : "bg-indigo-600 hover:bg-indigo-500 text-white shadow-xs shadow-indigo-600/20"
                      }`}
                    >
                      <span>{isDone ? "👁️ Xem lại câu này" : "Làm câu này"}</span>
                      <span>→</span>
                    </Link>
                  );
                })()}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};
