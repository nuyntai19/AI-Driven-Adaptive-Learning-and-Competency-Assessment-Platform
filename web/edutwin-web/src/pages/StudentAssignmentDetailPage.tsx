import React from "react";
import { useParams, Link, useSearchParams } from "react-router-dom";
import { isAxiosError } from "axios";
import { useStudentAssignment } from "../features/assignments/useStudentAssignment";
import { MathFormulaPreview } from "../components/math/MathFormulaPreview";
import { RichMathText } from "../components/math/RichMathText";
import { getSubjectTheme } from "../components/student/subjectTheme";
import { StudentBadge } from "../components/student/StudentBadge";
import { StudentSubjectPattern } from "../components/student/StudentSubjectPattern";
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
      return "Điền số";
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
      <div className="w-full max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-5 student-shell">
        <div className="h-6 w-32 bg-stone-200 dark:bg-stone-800 animate-pulse rounded" />
        <div className="h-44 bg-white dark:bg-[#151d2f] animate-pulse rounded-xl border border-stone-200 dark:border-stone-800" />
        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <div key={i} className="h-32 bg-white dark:bg-[#151d2f] animate-pulse rounded-xl border border-stone-200 dark:border-stone-800" />
          ))}
        </div>
      </div>
    );
  }

  if (isError || !assignment) {
    return (
      <div className="max-w-2xl mx-auto px-4 py-12 student-shell">
        <div className="rounded-xl bg-white dark:bg-[#151d2f] p-8 border border-stone-200 dark:border-stone-800 text-center shadow-xs">
          <p className="text-sm font-medium text-red-700 dark:text-red-400 mb-4">
            Đã có lỗi xảy ra: {getStudentDetailError(error)}
          </p>
          <Link
            to={`/hoc-tap/bai-tap${selectedSubjectId ? `?subjectId=${selectedSubjectId}` : ""}`}
            className="inline-flex items-center gap-1.5 text-xs font-semibold text-indigo-600 dark:text-indigo-400 hover:underline"
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

  // First uncompleted question
  const firstUnfinishedQuestion = questions.find(
    (q) => !q.latestAttempt && q.attemptStatus !== "Completed" && q.attemptStatus !== "NeedsTeacherReview"
  );

  const subjectName = assignment.subjectName || "Học phần";
  const subjectTheme = getSubjectTheme(subjectName);

  const renderStatusBadge = (status: ProgressStatus) => {
    switch (status) {
      case "Completed":
        return <StudentBadge variant="success" size="sm">Đã hoàn thành</StudentBadge>;
      case "InProgress":
        return <StudentBadge variant="accent" size="sm">Đang làm</StudentBadge>;
      case "Overdue":
        return <StudentBadge variant="danger" size="sm">Quá hạn</StudentBadge>;
      case "NotStarted":
      default:
        return <StudentBadge variant="neutral" size="sm">Chưa bắt đầu</StudentBadge>;
    }
  };

  const renderAttemptStatusBadge = (question: StudentAssignmentQuestionDto) => {
    const status = question.attemptStatus;
    const attempt = question.latestAttempt;

    if (attempt?.skipped) {
      return (
        <StudentBadge variant="warning" size="xs">
          Đã bỏ qua
        </StudentBadge>
      );
    }

    if (!status && !attempt) {
      return (
        <span className="text-xs font-medium text-stone-400 dark:text-stone-500">
          Chưa làm
        </span>
      );
    }

    if (status === "Completed") {
      return (
        <StudentBadge variant="success" size="xs">
          Đã hoàn thành
        </StudentBadge>
      );
    }

    if (status === "NeedsTeacherReview") {
      return (
        <StudentBadge variant="warning" size="xs">
          Chờ giáo viên duyệt
        </StudentBadge>
      );
    }

    return (
      <StudentBadge variant="accent" size="xs">
        Đang phân tích
      </StudentBadge>
    );
  };

  return (
    <div className="w-full max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-5 min-w-0 student-shell">
      {/* Navigation Breadcrumb */}
      <div>
        <Link
          to={`/hoc-tap/bai-tap${selectedSubjectId ? `?subjectId=${selectedSubjectId}` : ""}`}
          className="inline-flex items-center gap-1.5 text-xs font-semibold text-stone-600 dark:text-stone-400 hover:text-stone-900 dark:hover:text-stone-100 transition-colors"
        >
          <span>← Quay lại danh sách bài tập</span>
        </Link>
      </div>

      {/* Assignment Overview Workspace Card */}
      <div className="relative rounded-xl bg-white dark:bg-[#151d2f] border border-stone-200/90 dark:border-stone-800/90 p-5 sm:p-6 shadow-xs overflow-hidden">
        {/* Subtle Subject Graphic Pattern */}
        <StudentSubjectPattern subjectName={subjectName} opacity={0.05} />

        {/* Left accent rail */}
        <div
          className="absolute left-0 top-0 bottom-0 w-1"
          style={{ backgroundColor: subjectTheme.color }}
        />

        <div className="pl-2 space-y-4 relative z-10">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div className="flex flex-wrap items-center gap-2">
              <span
                className="text-[11px] font-bold uppercase tracking-wider px-2 py-0.5 rounded"
                style={{
                  backgroundColor: subjectTheme.bg,
                  color: subjectTheme.color,
                  border: `1px solid ${subjectTheme.border}`,
                }}
              >
                {subjectName}
              </span>
              {renderStatusBadge(progress.status)}
            </div>

            {/* Primary Action Button */}
            {isAssignmentFinished ? (
              <Link
                to={`/hoc-tap/luyen-tap/${questions[0]?.questionId || ""}?assignmentId=${assignment.assignmentId}${selectedSubjectId ? `&subjectId=${selectedSubjectId}` : ""}`}
                className="inline-flex items-center justify-center gap-1.5 rounded-lg bg-stone-900 hover:bg-stone-800 dark:bg-stone-100 dark:hover:bg-stone-200 px-4 py-2 text-xs sm:text-sm font-semibold text-white dark:text-stone-900 transition-colors cursor-pointer self-start sm:self-auto"
              >
                <span>Xem lại bài làm</span>
                <span>→</span>
              </Link>
            ) : firstUnfinishedQuestion ? (
              <Link
                to={`/hoc-tap/luyen-tap/${firstUnfinishedQuestion.questionId}?assignmentId=${assignment.assignmentId}${selectedSubjectId ? `&subjectId=${selectedSubjectId}` : ""}`}
                className="inline-flex items-center justify-center gap-1.5 rounded-lg bg-stone-900 hover:bg-stone-800 dark:bg-stone-100 dark:hover:bg-stone-200 px-5 py-2 text-xs sm:text-sm font-semibold text-white dark:text-stone-900 transition-colors cursor-pointer self-start sm:self-auto"
              >
                <span>{progress.completedQuestionCount > 0 ? "Tiếp tục làm bài" : "Bắt đầu làm bài"}</span>
                <span>→</span>
              </Link>
            ) : null}
          </div>

          <div>
            <h1 className="text-xl sm:text-2xl font-bold tracking-tight text-stone-900 dark:text-stone-100">
              {assignment.title}
            </h1>
            <p className="mt-1.5 text-xs sm:text-sm text-stone-600 dark:text-stone-400 leading-relaxed">
              {assignment.instructions ||
                "Hoàn thành các câu hỏi bên dưới và giải trình các bước tư duy để hệ thống đánh giá năng lực thích ứng."}
            </p>
          </div>

          {/* Progress & Due date */}
          <div className="pt-3 border-t border-stone-100 dark:border-stone-800/80 space-y-1.5">
            <div className="flex items-center justify-between text-xs">
              <span className="font-semibold text-stone-700 dark:text-stone-300">
                Tiến độ: {progress.completedQuestionCount}/{progress.totalQuestionCount} câu hoàn thành ({percent}%)
              </span>
              {assignment.dueAt && (
                <span className="text-stone-500 dark:text-stone-400">
                  Hạn nộp:{" "}
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
            <div className="w-full bg-stone-100 dark:bg-stone-800 rounded-full h-1.5 overflow-hidden">
              <div
                className={`h-full rounded-full transition-all duration-300 ${
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
      </div>

      {/* Questions list header */}
      <div className="flex items-center justify-between pt-2">
        <h2 className="text-sm sm:text-base font-bold text-stone-900 dark:text-stone-100">
          Danh sách câu hỏi ({questions.length})
        </h2>
        <span className="text-xs text-stone-500 dark:text-stone-400">
          Dự kiến ~{Math.max(20, questions.length * 4)} phút
        </span>
      </div>

      {/* Question Items List */}
      <div className="space-y-3">
        {questions.map((question, index) => {
          const formattedId = `Q-${String(index + 1).padStart(2, "0")}`;
          const timeSec = question.estimatedTimeSeconds || 120;
          const hasMath = /[\\[{^_\\]]/.test(question.questionText);
          const isDone =
            Boolean(question.latestAttempt) ||
            question.attemptStatus === "Completed" ||
            question.attemptStatus === "NeedsTeacherReview";

          return (
            <div
              key={question.questionId}
              className="rounded-xl bg-white dark:bg-[#151d2f] border border-stone-200/90 dark:border-stone-800/90 p-5 shadow-xs hover:border-stone-300 dark:hover:border-stone-700 transition-all space-y-3"
            >
              {/* Question header */}
              <div className="flex items-center justify-between gap-3 pb-3 border-b border-stone-100 dark:border-stone-800/80">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-xs font-mono font-bold text-stone-700 dark:text-stone-300">
                    Câu {index + 1}
                  </span>
                  <span className="text-[11px] font-mono text-stone-400">
                    {formattedId}
                  </span>
                  <span className="text-[11px] px-2 py-0.5 rounded bg-stone-100 dark:bg-stone-800 text-stone-600 dark:text-stone-300 font-medium">
                    {getQuestionTypeLabel(question.questionType)}
                  </span>
                  <span className="text-[11px] px-2 py-0.5 rounded bg-stone-100 dark:bg-stone-800 text-stone-600 dark:text-stone-300 font-medium">
                    {getDifficultyLabel(question.difficulty)}
                  </span>
                </div>

                <div>
                  {renderAttemptStatusBadge(question)}
                </div>
              </div>

              {/* Question content */}
              <div className="text-sm sm:text-base text-stone-800 dark:text-stone-200 leading-relaxed font-medium">
                <RichMathText text={question.questionText} />
              </div>

              {/* KaTeX preview if formula detected */}
              {hasMath && (
                <div className="pt-1">
                  <MathFormulaPreview
                    formula={question.questionText}
                    label="Công thức toán"
                  />
                </div>
              )}

              {/* Options preview if present */}
              {question.options && question.options.length > 0 && (
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 pt-1">
                  {question.options.map((opt) => (
                    <div
                      key={opt.optionId}
                      className="flex items-center gap-2.5 p-2.5 rounded-lg border border-stone-200/80 dark:border-stone-800/80 bg-stone-50/60 dark:bg-stone-900/40 text-xs"
                    >
                      <span className="w-5 h-5 rounded-full bg-white dark:bg-stone-800 border border-stone-300 dark:border-stone-700 font-bold text-stone-700 dark:text-stone-300 flex items-center justify-center text-[10px] shrink-0">
                        {opt.label}
                      </span>
                      <span className="text-stone-800 dark:text-stone-200 truncate">
                        <RichMathText text={opt.text} />
                      </span>
                    </div>
                  ))}
                </div>
              )}

              {/* Footer row */}
              <div className="pt-2 border-t border-stone-100 dark:border-stone-800/80 flex items-center justify-between">
                <span className="text-xs text-stone-400 dark:text-stone-500">
                  Thời lượng ước tính: ~{Math.round(timeSec / 60)} phút
                </span>

                <Link
                  to={`/hoc-tap/luyen-tap/${question.questionId}?assignmentId=${assignment.assignmentId}${selectedSubjectId ? `&subjectId=${selectedSubjectId}` : ""}`}
                  className={`inline-flex items-center gap-1 px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors cursor-pointer ${
                    isDone
                      ? "text-emerald-700 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-950/30 border border-emerald-200/80 dark:border-emerald-800/80 hover:bg-emerald-100"
                      : "text-stone-100 bg-stone-900 hover:bg-stone-800 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200"
                  }`}
                >
                  <span>{isDone ? "Xem lại" : "Làm câu này"}</span>
                  <span className="text-[11px]">→</span>
                </Link>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};
