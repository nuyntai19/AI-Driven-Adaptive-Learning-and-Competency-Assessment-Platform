import React, { useState, useMemo } from "react";
import { isAxiosError } from "axios";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useStudentAssignments } from "../features/assignments/useStudentAssignments";
import { organizationApi } from "../api/organizationApi";
import { getSubjectTheme } from "../components/student/subjectTheme";
import { StudentBadge } from "../components/student/StudentBadge";
import type { ProgressStatus, StudentAssignmentListItemDto } from "../types/assignments";

const getStudentListError = (error: unknown) => {
  if (isAxiosError(error)) return error.response?.data?.detail || error.message;
  return error instanceof Error ? error.message : "Không thể tải danh sách bài tập.";
};

type TimelineGroupKey = "today" | "tomorrow" | "thisWeek" | "later";

interface TimelineGroup {
  key: TimelineGroupKey;
  label: string;
  items: StudentAssignmentListItemDto[];
}

export const StudentAssignmentsPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const selectedSubjectId = searchParams.get("subjectId") || "";

  const [statusFilter, setStatusFilter] = useState<ProgressStatus | "">("");
  const [searchTerm, setSearchTerm] = useState<string>("");
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
    subjectId: (!ignoreSubjectFilter && selectedSubjectId) ? selectedSubjectId : undefined,
  });

  const rawAssignments = response?.data || [];

  // Filter by search term
  const filteredAssignments = useMemo(() => {
    let result = [...rawAssignments];

    if (searchTerm.trim()) {
      const term = searchTerm.toLowerCase();
      result = result.filter(
        (a) =>
          a.title.toLowerCase().includes(term) ||
          (a.instructions && a.instructions.toLowerCase().includes(term))
      );
    }

    return result;
  }, [rawAssignments, searchTerm]);

  // Group into Timeline / Agenda: HÔM NAY, NGÀY MAI, TUẦN NÀY, SAU ĐÓ
  const timelineGroups = useMemo<TimelineGroup[]>(() => {
    const now = new Date();
    const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
    const startOfTomorrow = startOfToday + 24 * 60 * 60 * 1000;
    const startOfDayAfterTomorrow = startOfTomorrow + 24 * 60 * 60 * 1000;
    const endOfWeek = startOfToday + 7 * 24 * 60 * 60 * 1000;

    const todayList: StudentAssignmentListItemDto[] = [];
    const tomorrowList: StudentAssignmentListItemDto[] = [];
    const thisWeekList: StudentAssignmentListItemDto[] = [];
    const laterList: StudentAssignmentListItemDto[] = [];

    filteredAssignments.forEach((item) => {
      if (item.progress.status === "Completed") {
        laterList.push(item);
        return;
      }

      if (!item.dueAt) {
        laterList.push(item);
        return;
      }

      const dueTime = new Date(item.dueAt).getTime();
      if (dueTime < startOfTomorrow) {
        todayList.push(item);
      } else if (dueTime < startOfDayAfterTomorrow) {
        tomorrowList.push(item);
      } else if (dueTime < endOfWeek) {
        thisWeekList.push(item);
      } else {
        laterList.push(item);
      }
    });

    const groups: TimelineGroup[] = [];
    if (todayList.length > 0) {
      groups.push({ key: "today", label: "HÔM NAY", items: todayList });
    }
    if (tomorrowList.length > 0) {
      groups.push({ key: "tomorrow", label: "NGÀY MAI", items: tomorrowList });
    }
    if (thisWeekList.length > 0) {
      groups.push({ key: "thisWeek", label: "TUẦN NÀY", items: thisWeekList });
    }
    if (laterList.length > 0) {
      groups.push({ key: "later", label: "CHỜ THỰC HIỆN & ĐÃ XONG", items: laterList });
    }

    // Fallback if no dates set
    if (groups.length === 0 && filteredAssignments.length > 0) {
      groups.push({ key: "later", label: "DANH SÁCH BÀI TẬP", items: filteredAssignments });
    }

    return groups;
  }, [filteredAssignments]);

  const getSubjectNameForAssignment = (item: StudentAssignmentListItemDto): string => {
    if (item.subjectName) return item.subjectName;
    if (item.subjectId) {
      const matched = subjects.find((s) => s.subjectId === item.subjectId);
      if (matched) return matched.subjectName;
    }
    if (currentSubjectName) return currentSubjectName;
    return "Toán học";
  };

  return (
    <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6 min-w-0 student-shell">
      {/* Workspace Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-3 border-b border-stone-200/80 dark:border-stone-800/80">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-xl sm:text-2xl font-bold tracking-tight text-stone-900 dark:text-stone-100">
              Lịch học tập & Bài tập
            </h1>
            <span className="text-xs font-mono px-2 py-0.5 rounded bg-stone-100 dark:bg-stone-800 text-stone-600 dark:text-stone-300 font-medium">
              {filteredAssignments.length} nhiệm vụ
            </span>
          </div>
          <p className="mt-0.5 text-xs text-stone-500 dark:text-stone-400">
            Kế hoạch rèn luyện theo mốc thời gian · Điểm chạm trên hành trình học tập
          </p>
        </div>

        {/* Active Subject Filter indicator */}
        {currentSubjectName && !ignoreSubjectFilter ? (
          <div className="flex items-center gap-2">
            {(() => {
              const theme = getSubjectTheme(currentSubjectName);
              return (
                <span
                  className="inline-flex items-center gap-1.5 px-3 py-1 rounded-lg text-xs font-semibold"
                  style={{
                    backgroundColor: theme.bg,
                    color: theme.color,
                    border: `1px solid ${theme.border}`,
                  }}
                >
                  <span className="w-1.5 h-1.5 rounded-full" style={{ backgroundColor: theme.color }} />
                  <span>Môn: {currentSubjectName}</span>
                  <button
                    type="button"
                    onClick={() => setIgnoreSubjectFilter(true)}
                    className="ml-1 hover:opacity-75 cursor-pointer font-bold"
                    title="Xem tất cả môn học"
                  >
                    ×
                  </button>
                </span>
              );
            })()}
          </div>
        ) : ignoreSubjectFilter && currentSubjectName ? (
          <button
            type="button"
            onClick={() => setIgnoreSubjectFilter(false)}
            className="text-xs font-semibold text-indigo-600 dark:text-indigo-400 hover:underline cursor-pointer"
          >
            ← Quay lại lọc môn {currentSubjectName}
          </button>
        ) : null}
      </div>

      {/* Filter Tabs & Search Bar */}
      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-3">
        {/* Status segmented tabs */}
        <div className="flex items-center gap-1 overflow-x-auto pb-1 sm:pb-0 scrollbar-none border-b border-stone-200/60 dark:border-stone-800/60 lg:border-none">
          {[
            { id: "", label: "Tất cả" },
            { id: "NotStarted", label: "Chưa bắt đầu" },
            { id: "InProgress", label: "Đang làm" },
            { id: "Completed", label: "Đã xong" },
            { id: "Overdue", label: "Quá hạn" },
          ].map((tab) => {
            const isActive = statusFilter === tab.id;
            return (
              <button
                key={tab.id}
                type="button"
                onClick={() => setStatusFilter(tab.id as ProgressStatus | "")}
                className={`relative px-3 py-1.5 text-xs sm:text-sm font-semibold whitespace-nowrap transition-colors cursor-pointer ${
                  isActive
                    ? "text-stone-900 dark:text-stone-100 font-bold"
                    : "text-stone-500 hover:text-stone-800 dark:text-stone-400"
                }`}
              >
                <span>{tab.label}</span>
                {isActive && (
                  <span className="absolute bottom-0 left-2 right-2 h-0.5 bg-stone-900 dark:bg-stone-100 rounded-full" />
                )}
              </button>
            );
          })}
        </div>

        {/* Search Input */}
        <div className="relative w-full sm:w-64">
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder="Tìm kiếm bài tập..."
            className="w-full pl-3 pr-7 py-1.5 rounded-lg border border-stone-200 dark:border-stone-800 bg-white dark:bg-[#151d2f] text-xs sm:text-sm text-stone-900 dark:text-stone-100 placeholder-stone-400 focus:outline-none focus:border-stone-400 dark:focus:border-stone-600 transition-colors"
          />
          {searchTerm && (
            <button
              type="button"
              onClick={() => setSearchTerm("")}
              className="absolute inset-y-0 right-0 pr-2.5 flex items-center text-xs text-stone-400 hover:text-stone-700 dark:hover:text-stone-200"
            >
              ×
            </button>
          )}
        </div>
      </div>

      {/* Loading Skeleton */}
      {isLoading && (
        <div className="space-y-4 py-2">
          {[1, 2, 3].map((i) => (
            <div
              key={i}
              className="h-20 animate-pulse rounded-xl bg-white dark:bg-[#151d2f] border border-stone-200/70 dark:border-stone-800/70"
            />
          ))}
        </div>
      )}

      {/* Error state */}
      {isError && (
        <div className="rounded-xl bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-900/50 p-6 text-center">
          <p className="text-sm font-medium text-red-800 dark:text-red-300">
            Không thể tải danh sách bài tập: {getStudentListError(error)}
          </p>
          <button
            type="button"
            onClick={() => refetch()}
            className="mt-3 inline-flex items-center rounded-lg bg-red-700 px-3.5 py-1.5 text-xs font-semibold text-white hover:bg-red-600 cursor-pointer"
          >
            Thử lại
          </button>
        </div>
      )}

      {/* Empty State */}
      {!isLoading && !isError && filteredAssignments.length === 0 && (
        <div className="py-16 text-center">
          <div className="w-8 h-8 rounded-full border border-stone-300 dark:border-stone-700 flex items-center justify-center mx-auto mb-3 text-stone-400 text-xs">
            ✓
          </div>
          <h3 className="text-sm font-semibold text-stone-800 dark:text-stone-200">
            {searchTerm ? "Không có kết quả phù hợp" : "Tất cả bài tập đã hoàn thành"}
          </h3>
          <p className="mt-1 text-xs text-stone-500 dark:text-stone-400 max-w-sm mx-auto">
            {searchTerm
              ? "Thử tìm kiếm với từ khóa khác hoặc xóa bộ lọc."
              : "Bạn đang theo sát lộ trình học tập. Hãy tiếp tục luyện tập tự do để nâng cao điểm số."}
          </p>
          {searchTerm && (
            <button
              type="button"
              onClick={() => setSearchTerm("")}
              className="mt-3 text-xs font-semibold text-indigo-600 dark:text-indigo-400 hover:underline"
            >
              Xóa tìm kiếm
            </button>
          )}
        </div>
      )}

      {/* TIMELINE / AGENDA SECTIONS (No card grid, clean list separated by whitespace and thin dividers) */}
      {!isLoading && !isError && timelineGroups.length > 0 && (
        <div className="space-y-8">
          {timelineGroups.map((group) => (
            <div key={group.key} className="space-y-3">
              {/* Group Header Line */}
              <div className="flex items-center gap-3">
                <span className="text-xs font-mono font-bold tracking-wider text-stone-500 dark:text-stone-400 uppercase">
                  {group.label}
                </span>
                <div className="flex-1 h-px bg-stone-200/80 dark:border-stone-800/80" />
                <span className="text-xs font-mono text-stone-400">
                  {group.items.length} bài
                </span>
              </div>

              {/* Assignment Rows List */}
              <div className="divide-y divide-stone-100 dark:divide-stone-800/80">
                {group.items.map((item) => {
                  const progress = item.progress;
                  const percent =
                    progress.totalQuestionCount > 0
                      ? Math.round((progress.completedQuestionCount / progress.totalQuestionCount) * 100)
                      : 0;

                  const subjectName = getSubjectNameForAssignment(item);
                  const theme = getSubjectTheme(subjectName);
                  const isDone = progress.status === "Completed";
                  const isStarted = progress.completedQuestionCount > 0;

                  return (
                    <div
                      key={item.assignmentId}
                      className="group relative flex flex-col sm:flex-row sm:items-center justify-between py-4 gap-3 transition-colors hover:bg-stone-50/50 dark:hover:bg-stone-900/30 px-2 rounded-lg"
                    >
                      {/* Left: Subject Indicator & Title */}
                      <div className="flex items-start gap-3 min-w-0 flex-1">
                        <span
                          className="w-2.5 h-2.5 rounded-full mt-1.5 shrink-0"
                          style={{ backgroundColor: theme.color }}
                          title={subjectName}
                        />

                        <div className="min-w-0 space-y-1">
                          <div className="flex items-center gap-2">
                            <span className="text-[11px] font-mono font-semibold uppercase tracking-wider text-stone-500 dark:text-stone-400">
                              {subjectName}
                            </span>
                            {isDone ? (
                              <>
                                <StudentBadge variant="success" size="xs">Đã xong</StudentBadge>
                                {item.summary && (
                                  <>
                                    {item.summary.teacherFinalReviewStatus === "Approved" ? (
                                      <StudentBadge variant="success" size="xs">GV đã duyệt</StudentBadge>
                                    ) : (
                                      <StudentBadge variant="warning" size="xs">GV chưa duyệt</StudentBadge>
                                    )}
                                    <span className="text-[10px] font-black text-emerald-700 dark:text-emerald-300 bg-emerald-50 dark:bg-emerald-950/40 px-1.5 py-0.5 rounded border border-emerald-200 dark:border-emerald-800">
                                      {item.summary.correctQuestionCount}/{item.summary.totalQuestionCount} câu đúng
                                    </span>
                                  </>
                                )}
                              </>
                            ) : item.progress.status === "Overdue" ? (
                              <StudentBadge variant="danger" size="xs">Quá hạn</StudentBadge>
                            ) : null}
                          </div>

                          <Link
                            to={`/hoc-tap/bai-tap/${item.assignmentId}${selectedSubjectId ? `?subjectId=${selectedSubjectId}` : ""}`}
                            className="text-sm sm:text-base font-bold text-stone-900 dark:text-stone-100 group-hover:text-indigo-600 dark:group-hover:text-indigo-400 transition-colors line-clamp-1"
                          >
                            {item.title}
                          </Link>

                          {item.instructions && (
                            <p className="text-xs text-stone-500 dark:text-stone-400 line-clamp-1">
                              {item.instructions}
                            </p>
                          )}
                        </div>
                      </div>

                      {/* Right: Progress Track & Action Link */}
                      <div className="flex items-center gap-5 sm:gap-6 shrink-0 self-end sm:self-center pl-6 sm:pl-0">
                        {/* Progress Tracker */}
                        <div className="w-28 sm:w-36 space-y-1 text-right">
                          <div className="flex items-center justify-between text-[11px]">
                            <span className="text-stone-500 dark:text-stone-400">
                              {progress.completedQuestionCount}/{progress.totalQuestionCount} câu
                            </span>
                            <span className="font-mono font-semibold text-stone-700 dark:text-stone-300">
                              {percent}%
                            </span>
                          </div>
                          <div className="w-full bg-stone-100 dark:bg-stone-800 rounded-full h-1.5 overflow-hidden">
                            <div
                              className={`h-full rounded-full transition-all duration-300 ${
                                isDone ? "bg-emerald-500" : "bg-indigo-600"
                              }`}
                              style={{ width: `${percent}%` }}
                            />
                          </div>
                        </div>

                        {/* Due info */}
                        {item.dueAt && (
                          <span className="text-xs text-stone-400 dark:text-stone-500 font-mono hidden md:inline">
                            {new Date(item.dueAt).toLocaleDateString("vi-VN", {
                              day: "2-digit",
                              month: "2-digit",
                            })}
                          </span>
                        )}

                        {/* CTA Link */}
                        <Link
                          to={`/hoc-tap/bai-tap/${item.assignmentId}${selectedSubjectId ? `?subjectId=${selectedSubjectId}` : ""}`}
                          className="inline-flex items-center gap-1 text-xs sm:text-sm font-semibold text-stone-800 dark:text-stone-200 hover:text-indigo-600 dark:hover:text-indigo-400 transition-colors"
                        >
                          <span>{isDone ? "Xem lại" : isStarted ? "Tiếp tục" : "Bắt đầu"}</span>
                          <span className="text-[11px]">→</span>
                        </Link>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
