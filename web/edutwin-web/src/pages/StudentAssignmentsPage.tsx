import React, { useState, useMemo } from "react";
import { isAxiosError } from "axios";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useStudentAssignments } from "../features/assignments/useStudentAssignments";
import { organizationApi } from "../api/organizationApi";
import type { ProgressStatus, StudentAssignmentListItemDto } from "../types/assignments";

const getStudentListError = (error: unknown) => {
  if (isAxiosError(error)) return error.response?.data?.detail || error.message;
  return error instanceof Error ? error.message : "Không thể tải danh sách bài tập.";
};

type SortOption = "dueSoon" | "newest" | "oldest";

export const StudentAssignmentsPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const selectedSubjectId = searchParams.get("subjectId") || "";

  const [statusFilter, setStatusFilter] = useState<ProgressStatus | "">("");
  const [searchTerm, setSearchTerm] = useState<string>("");
  const [sortBy, setSortBy] = useState<SortOption>("dueSoon");
  const [ignoreSubjectFilter, setIgnoreSubjectFilter] = useState<boolean>(false);

  // Fetch subjects to match subject names
  const { data: subjectsResponse } = useQuery({
    queryKey: ["subjects", "student-workspace", "active"],
    queryFn: () => organizationApi.listSubjects(true),
    staleTime: 5 * 60 * 1000,
  });

  const subjects = subjectsResponse?.data || [];
  const currentSubject = subjects.find((s) => s.subjectId === selectedSubjectId);
  const currentSubjectName = currentSubject?.subjectName || "";

  const { data: response, isLoading, isError, error, refetch } = useStudentAssignments({
    status: statusFilter || undefined,
  });

  const rawAssignments = response?.data || [];

  // Filter by subject, search term & sort
  const filteredAssignments = useMemo(() => {
    let result = [...rawAssignments];

    // Subject Filtering: If a subject is selected and not ignoring, filter by subject match
    if (selectedSubjectId && !ignoreSubjectFilter && currentSubjectName) {
      const subNameLower = currentSubjectName.toLowerCase();
      result = result.filter((a) => {
        const titleLower = a.title.toLowerCase();
        const instrLower = (a.instructions || "").toLowerCase();

        if (subNameLower.includes("toán")) {
          // If Math selected: match math keywords or exclude other subjects
          return (
            titleLower.includes("toán") ||
            titleLower.includes("đại số") ||
            titleLower.includes("hình học") ||
            titleLower.includes("giải tích") ||
            (!titleLower.includes("tiếng anh") && !titleLower.includes("vật lý") && !titleLower.includes("hóa học"))
          );
        }

        if (subNameLower.includes("anh") || subNameLower.includes("english")) {
          // If English selected: must match english keywords
          return (
            titleLower.includes("tiếng anh") ||
            titleLower.includes("english") ||
            instrLower.includes("tiếng anh")
          );
        }

        if (subNameLower.includes("vật lý") || subNameLower.includes("lý")) {
          return titleLower.includes("vật lý") || titleLower.includes("vật lí");
        }

        if (subNameLower.includes("hóa")) {
          return titleLower.includes("hóa học") || titleLower.includes("hóa");
        }

        return true;
      });
    }

    if (searchTerm.trim()) {
      const term = searchTerm.toLowerCase();
      result = result.filter(
        (a) =>
          a.title.toLowerCase().includes(term) ||
          (a.instructions && a.instructions.toLowerCase().includes(term))
      );
    }

    result.sort((a, b) => {
      if (sortBy === "dueSoon") {
        if (!a.dueAt) return 1;
        if (!b.dueAt) return -1;
        return new Date(a.dueAt).getTime() - new Date(b.dueAt).getTime();
      }
      if (sortBy === "newest") {
        if (!a.dueAt) return 1;
        if (!b.dueAt) return -1;
        return new Date(b.dueAt).getTime() - new Date(a.dueAt).getTime();
      }
      return 0;
    });

    return result;
  }, [rawAssignments, selectedSubjectId, ignoreSubjectFilter, currentSubjectName, searchTerm, sortBy]);

  // Status counters for filter pills
  const statusCounts = useMemo(() => {
    const counts = {
      all: filteredAssignments.length,
      NotStarted: 0,
      InProgress: 0,
      Completed: 0,
      Overdue: 0,
    };
    filteredAssignments.forEach((a) => {
      if (counts[a.progress.status] !== undefined) {
        counts[a.progress.status]++;
      }
    });
    return counts;
  }, [filteredAssignments]);

  const getStatusBadge = (status: ProgressStatus) => {
    switch (status) {
      case "Completed":
        return (
          <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-400 border border-emerald-200/80 dark:border-emerald-800">
            Đã hoàn thành
          </span>
        );
      case "InProgress":
        return (
          <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold bg-indigo-50 dark:bg-indigo-950/40 text-indigo-700 dark:text-indigo-300 border border-indigo-200/80 dark:border-indigo-800">
            Đang làm
          </span>
        );
      case "Overdue":
        return (
          <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold bg-rose-50 dark:bg-rose-950/40 text-rose-700 dark:text-rose-400 border border-rose-200/80 dark:border-rose-800">
            Quá hạn
          </span>
        );
      case "NotStarted":
      default:
        return (
          <span className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 border border-slate-200/80 dark:border-slate-700">
            Chưa bắt đầu
          </span>
        );
    }
  };

  return (
    <div className="max-w-[1700px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6">
      {/* Title & Subtitle */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-900 dark:text-white tracking-tight">
            Bài tập của tôi
          </h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400 font-medium">
            Danh sách các nhiệm vụ học tập và bài kiểm tra được giáo viên giao.
          </p>
        </div>

        {/* Active Subject Filter Badge */}
        {currentSubjectName && !ignoreSubjectFilter && (
          <div className="flex items-center gap-2 self-start sm:self-auto">
            <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-indigo-50 dark:bg-indigo-950/50 text-indigo-700 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-800 text-xs font-bold shadow-2xs">
              <span>Đang lọc môn: {currentSubjectName}</span>
              <button
                type="button"
                onClick={() => setIgnoreSubjectFilter(true)}
                className="hover:text-indigo-900 dark:hover:text-white text-xs font-black cursor-pointer ml-1"
                title="Xem tất cả môn học"
              >
                ✕
              </button>
            </span>
          </div>
        )}

        {ignoreSubjectFilter && (
          <button
            type="button"
            onClick={() => setIgnoreSubjectFilter(false)}
            className="text-xs font-bold text-indigo-600 dark:text-indigo-400 hover:underline cursor-pointer"
          >
            ← Quay lại lọc theo môn {currentSubjectName}
          </button>
        )}
      </div>

      {/* Search Bar & Status Filter Pills */}
      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
        {/* Search input */}
        <div className="relative flex-1 max-w-lg">
          <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none text-slate-500">
            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
          </div>
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder="Tìm kiếm bài tập theo tên..."
            className="w-full pl-11 pr-4 py-3 rounded-2xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-[#0f172a] text-sm font-semibold text-slate-900 dark:text-white placeholder-slate-400 focus:outline-none focus:border-indigo-500 focus:ring-2 focus:ring-indigo-500/20 shadow-xs"
          />
          {searchTerm && (
            <button
              onClick={() => setSearchTerm("")}
              className="absolute inset-y-0 right-0 pr-4 flex items-center text-xs text-slate-400 hover:text-slate-600 dark:hover:text-white cursor-pointer"
            >
              ✕
            </button>
          )}
        </div>

        {/* Status Filter Pills */}
        <div className="flex items-center gap-2 overflow-x-auto pb-1 sm:pb-0 scrollbar-none">
          <button
            type="button"
            onClick={() => setStatusFilter("")}
            className={`px-4 py-2.5 rounded-2xl text-sm font-black transition-all whitespace-nowrap cursor-pointer flex items-center gap-2 ${
              statusFilter === ""
                ? "bg-slate-900 dark:bg-indigo-600 text-white shadow-xs"
                : "bg-white dark:bg-[#0f172a] text-slate-800 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 border border-slate-300/80 dark:border-slate-700"
            }`}
          >
            <span>Tất cả</span>
            <span className={`px-2 py-0.5 rounded-full text-xs font-black ${statusFilter === "" ? "bg-slate-800 dark:bg-indigo-700 text-white" : "bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300"}`}>
              {statusCounts.all}
            </span>
          </button>

          <button
            type="button"
            onClick={() => setStatusFilter("NotStarted")}
            className={`px-4 py-2.5 rounded-2xl text-sm font-black transition-all whitespace-nowrap cursor-pointer flex items-center gap-2 ${
              statusFilter === "NotStarted"
                ? "bg-slate-900 dark:bg-indigo-600 text-white shadow-xs"
                : "bg-white dark:bg-[#0f172a] text-slate-800 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 border border-slate-300/80 dark:border-slate-700"
            }`}
          >
            <span>Chưa bắt đầu</span>
            {statusCounts.NotStarted > 0 && (
              <span className={`px-2 py-0.5 rounded-full text-xs font-black ${statusFilter === "NotStarted" ? "bg-slate-800 dark:bg-indigo-700 text-white" : "bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300"}`}>
                {statusCounts.NotStarted}
              </span>
            )}
          </button>

          <button
            type="button"
            onClick={() => setStatusFilter("InProgress")}
            className={`px-4 py-2.5 rounded-2xl text-sm font-black transition-all whitespace-nowrap cursor-pointer flex items-center gap-2 ${
              statusFilter === "InProgress"
                ? "bg-slate-900 dark:bg-indigo-600 text-white shadow-xs"
                : "bg-white dark:bg-[#0f172a] text-slate-800 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 border border-slate-300/80 dark:border-slate-700"
            }`}
          >
            <span>Đang làm</span>
            {statusCounts.InProgress > 0 && (
              <span className={`px-2 py-0.5 rounded-full text-xs font-black ${statusFilter === "InProgress" ? "bg-indigo-600 dark:bg-indigo-700 text-white" : "bg-indigo-50 dark:bg-indigo-950/60 text-indigo-700 dark:text-indigo-300"}`}>
                {statusCounts.InProgress}
              </span>
            )}
          </button>

          <button
            type="button"
            onClick={() => setStatusFilter("Completed")}
            className={`px-4 py-2.5 rounded-2xl text-sm font-black transition-all whitespace-nowrap cursor-pointer flex items-center gap-2 ${
              statusFilter === "Completed"
                ? "bg-slate-900 dark:bg-indigo-600 text-white shadow-xs"
                : "bg-white dark:bg-[#0f172a] text-slate-800 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 border border-slate-300/80 dark:border-slate-700"
            }`}
          >
            <span>Đã hoàn thành</span>
            {statusCounts.Completed > 0 && (
              <span className={`px-2 py-0.5 rounded-full text-xs font-black ${statusFilter === "Completed" ? "bg-emerald-600 text-white" : "bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300"}`}>
                {statusCounts.Completed}
              </span>
            )}
          </button>

          <button
            type="button"
            onClick={() => setStatusFilter("Overdue")}
            className={`px-4 py-2.5 rounded-2xl text-sm font-black transition-all whitespace-nowrap cursor-pointer flex items-center gap-2 ${
              statusFilter === "Overdue"
                ? "bg-slate-900 dark:bg-indigo-600 text-white shadow-xs"
                : "bg-white dark:bg-[#0f172a] text-slate-800 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 border border-slate-300/80 dark:border-slate-700"
            }`}
          >
            <span>Quá hạn</span>
            {statusCounts.Overdue > 0 && (
              <span className={`px-2 py-0.5 rounded-full text-xs font-black ${statusFilter === "Overdue" ? "bg-rose-600 text-white" : "bg-rose-50 dark:bg-rose-950/60 text-rose-700 dark:text-rose-300"}`}>
                {statusCounts.Overdue}
              </span>
            )}
          </button>
        </div>
      </div>

      {/* Summary count and sorting bar */}
      <div className="flex items-center justify-between pt-3 border-t border-slate-200/90 dark:border-slate-800">
        <span className="text-sm font-extrabold text-slate-800 dark:text-slate-200">
          {filteredAssignments.length} nhiệm vụ học tập {currentSubjectName && !ignoreSubjectFilter ? `cho môn ${currentSubjectName}` : ""}
        </span>

        <div className="flex items-center gap-2.5">
          <span className="text-xs font-bold text-slate-500 hidden sm:inline">Sắp xếp:</span>
          <select
            value={sortBy}
            onChange={(e) => setSortBy(e.target.value as SortOption)}
            className="rounded-2xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-[#0f172a] px-3.5 py-2 text-sm font-extrabold text-slate-900 dark:text-slate-100 focus:outline-none focus:border-indigo-500 cursor-pointer shadow-xs"
          >
            <option value="dueSoon">Hạn chót gần nhất ⌵</option>
            <option value="newest">Mới nhất</option>
            <option value="oldest">Cũ nhất</option>
          </select>
        </div>
      </div>

      {/* Loading state */}
      {isLoading && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-5 py-4">
          {[1, 2].map((i) => (
            <div
              key={i}
              className="h-56 animate-pulse rounded-2xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 shadow-xs"
            />
          ))}
        </div>
      )}

      {/* Error state */}
      {isError && (
        <div className="rounded-2xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-800 p-6 text-center">
          <p className="text-sm font-bold text-rose-700 dark:text-rose-300">
            Không thể tải danh sách bài tập: {getStudentListError(error)}
          </p>
          <button
            onClick={() => refetch()}
            className="mt-3 inline-flex items-center rounded-xl bg-rose-600 px-4 py-2 text-xs font-bold text-white shadow-xs hover:bg-rose-500 cursor-pointer"
          >
            Thử lại
          </button>
        </div>
      )}

      {/* Empty State */}
      {!isLoading && !isError && filteredAssignments.length === 0 && (
        <div className="rounded-2xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 p-12 text-center shadow-xs">
          <div className="w-12 h-12 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-400 flex items-center justify-center mx-auto mb-3 text-xl font-bold">
            📋
          </div>
          <h3 className="text-base font-bold text-slate-800 dark:text-white">
            {searchTerm
              ? "Không tìm thấy bài tập phù hợp"
              : currentSubjectName && !ignoreSubjectFilter
              ? `Chưa có bài tập nào cho môn ${currentSubjectName}`
              : "Chưa có bài tập nào trong mục này"}
          </h3>
          <p className="mt-1 text-xs text-slate-500 dark:text-slate-400 max-w-sm mx-auto">
            {searchTerm
              ? "Hãy thử tìm kiếm với từ khóa khác hoặc xóa bộ lọc."
              : currentSubjectName && !ignoreSubjectFilter
              ? `Giáo viên chưa giao bài tập cho môn ${currentSubjectName}. Bạn có thể chọn môn khác hoặc bấm xem tất cả bài tập.`
              : "Các bài tập mới từ giáo viên sẽ hiển thị ở đây khi được giao."}
          </p>
          <div className="mt-4 flex items-center justify-center gap-2">
            {currentSubjectName && !ignoreSubjectFilter && (
              <button
                onClick={() => setIgnoreSubjectFilter(true)}
                className="inline-flex items-center rounded-xl bg-indigo-600 px-4 py-2 text-xs font-bold text-white shadow-xs hover:bg-indigo-500 cursor-pointer"
              >
                Xem tất cả bài tập của các môn
              </button>
            )}
            {searchTerm && (
              <button
                onClick={() => {
                  setSearchTerm("");
                  setStatusFilter("");
                }}
                className="inline-flex items-center rounded-xl bg-slate-100 dark:bg-slate-800 px-4 py-2 text-xs font-bold text-slate-700 dark:text-slate-200 hover:bg-slate-200 cursor-pointer"
              >
                Xóa tìm kiếm
              </button>
            )}
          </div>
        </div>
      )}

      {/* 2-Column Grid of Assignment Cards */}
      {!isLoading && !isError && filteredAssignments.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
          {filteredAssignments.map((assignment: StudentAssignmentListItemDto, index: number) => {
            const progress = assignment.progress;
            const percent =
              progress.totalQuestionCount > 0
                ? Math.round((progress.completedQuestionCount / progress.totalQuestionCount) * 100)
                : 0;

            const formattedId = `#${String(index + 1).padStart(2, "0")}`;

            const questionCount = progress.totalQuestionCount || 3;
            const estimatedMinutes = Math.max(20, questionCount * 4);
            const difficultyLabel =
              questionCount > 15 ? "Vận dụng" : questionCount > 10 ? "Trung bình" : "Cơ bản";

            return (
              <div
                key={assignment.assignmentId}
                className="group relative rounded-3xl bg-white dark:bg-[#0f172a] border border-slate-200/90 dark:border-slate-800 p-6 sm:p-7 shadow-xs hover:shadow-md hover:border-indigo-300 dark:hover:border-indigo-700 transition-all flex flex-col justify-between"
              >
                <div>
                  {/* Card Header: ID and Status */}
                  <div className="flex items-center justify-between mb-3.5">
                    <span className="text-xs font-black text-indigo-700 dark:text-indigo-300 bg-indigo-50 dark:bg-indigo-950/70 border border-indigo-200 dark:border-indigo-800 rounded-lg px-2.5 py-1">
                      {formattedId}
                    </span>
                    {getStatusBadge(progress.status)}
                  </div>

                  {/* Title */}
                  <h3 className="text-lg sm:text-xl font-extrabold text-slate-900 dark:text-white group-hover:text-indigo-600 dark:group-hover:text-indigo-400 transition-colors line-clamp-1">
                    {assignment.title}
                  </h3>

                  {/* Instructions / Teacher Note */}
                  <p className="mt-2 text-sm text-slate-700 dark:text-slate-300 line-clamp-2 font-medium">
                    {assignment.instructions || "Luyện tập có hướng dẫn và phân tích AI đa phương thức"}
                  </p>

                  {/* Due date with clock icon */}
                  {assignment.dueAt && (
                    <div className="mt-3.5 flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300 font-semibold">
                      <svg className="w-4 h-4 text-slate-500 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                      </svg>
                      <span>
                        {progress.status === "Completed" ? "Đã nộp · " : "Hạn chót · "}
                        {new Date(assignment.dueAt).toLocaleDateString("vi-VN", {
                          hour: "2-digit",
                          minute: "2-digit",
                          day: "2-digit",
                          month: "2-digit",
                          year: "numeric",
                        })}
                      </span>
                    </div>
                  )}

                  {/* Question progress and bar */}
                  <div className="mt-4 pt-3 border-t border-slate-100 dark:border-slate-800">
                    <div className="flex items-center justify-between text-sm mb-2">
                      <span className="font-extrabold text-slate-900 dark:text-slate-100">
                        {progress.completedQuestionCount} / {progress.totalQuestionCount} câu hỏi
                      </span>
                      <span className="font-black text-indigo-600 dark:text-indigo-400">{percent}%</span>
                    </div>
                    <div className="w-full bg-slate-100 dark:bg-slate-800 rounded-full h-2.5 overflow-hidden">
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

                {/* Footer: Tags and Action Link */}
                <div className="mt-5 pt-4 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <span className="rounded-xl bg-slate-100 dark:bg-slate-800 px-3 py-1 text-xs font-bold text-slate-800 dark:text-slate-200">
                      {difficultyLabel}
                    </span>
                    <span className="rounded-xl bg-slate-100 dark:bg-slate-800 px-3 py-1 text-xs font-bold text-slate-800 dark:text-slate-200">
                      {estimatedMinutes} phút
                    </span>
                  </div>

                  <Link
                    to={`/hoc-tap/bai-tap/${assignment.assignmentId}${selectedSubjectId ? `?subjectId=${selectedSubjectId}` : ""}`}
                    className="inline-flex items-center gap-1.5 px-4 py-2 rounded-xl text-sm font-black text-indigo-700 dark:text-indigo-300 bg-indigo-50 dark:bg-indigo-950/70 hover:bg-indigo-100 dark:hover:bg-indigo-900/60 border border-indigo-200/80 dark:border-indigo-800 transition-all cursor-pointer shadow-2xs"
                  >
                    <span>Xem chi tiết</span>
                    <span>→</span>
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
