import React, { useState, useMemo } from "react";
import { isAxiosError } from "axios";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useStudentAssignments } from "../features/assignments/useStudentAssignments";
import { organizationApi } from "../api/organizationApi";
import { getSubjectTheme } from "../components/student/subjectTheme";
import { StudentBadge } from "../components/student/StudentBadge";
import type { ProgressStatus, StudentAssignmentListItemDto } from "../types/assignments";
import { studentScopeUrl } from "../utils/studentAcademicNavigation";
import { isAssignmentPastDue } from "../utils/assignmentReviewTiming";
import { AssignmentKnowledgeScope } from "../components/student/AssignmentKnowledgeScope";
import { assignmentKnowledgeSearch, assignmentResultLabel } from "../utils/assignmentKnowledge";

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
  const history = searchParams.get("history") === "true";

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
    classId: !ignoreSubjectFilter ? searchParams.get("classId") || undefined : undefined,
    history,
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
          (a.instructions && a.instructions.toLowerCase().includes(term)) ||
          assignmentKnowledgeSearch(a.summary).toLowerCase().includes(term)
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
    return "Môn học";
  };

  return (
    <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6 min-w-0 student-shell">
      {/* Workspace Header */}
      <div className="flex flex-col sm:flex-row sm:items-end justify-between gap-4 st-rise">
        <div>
          <p className="st-eyebrow">Bài giáo viên giao · theo mốc thời gian</p>
          <div className="mt-1.5 flex flex-wrap items-center gap-3">
            <h1 className="text-2xl sm:text-3xl font-extrabold tracking-tight text-[var(--student-ink)]">
              Lịch học tập & <span className="st-grad-text">Bài tập</span>
            </h1>
            <span className="rounded-full bg-[var(--student-brand-soft)] px-3 py-1 text-xs font-bold text-[var(--student-brand-ink)]">
              {filteredAssignments.length} nhiệm vụ
            </span>
          </div>
          <p className="mt-1.5 text-sm text-[var(--student-ink-secondary)]">
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
                  className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold"
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
        <div className="flex w-fit max-w-full items-center gap-1 overflow-x-auto rounded-full border border-[var(--student-border)] bg-[var(--student-surface)] p-1 scrollbar-none" role="tablist" aria-label="Lọc theo trạng thái">
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
                role="tab"
                aria-selected={isActive}
                className={`rounded-full px-3.5 py-1.5 text-xs sm:text-sm font-semibold whitespace-nowrap transition-colors cursor-pointer ${
                  isActive
                    ? "st-nav-active"
                    : "text-[var(--student-ink-secondary)] hover:bg-[var(--student-surface-subtle)] hover:text-[var(--student-ink)]"
                }`}
              >
                <span>{tab.label}</span>
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
            placeholder="Tìm bài tập hoặc kỹ năng..."
            aria-label="Tìm bài tập hoặc kỹ năng"
            className="w-full pl-4 pr-8 py-2 rounded-full border border-[var(--student-border)] bg-[var(--student-surface)] text-xs sm:text-sm text-[var(--student-ink)] placeholder-[var(--student-muted)] focus:outline-none focus:border-[var(--student-brand)] focus:ring-4 focus:ring-[var(--student-brand-soft)] transition-colors"
          />
          {searchTerm && (
            <button
              type="button"
              onClick={() => setSearchTerm("")}
              aria-label="Xóa tìm kiếm"
              className="absolute inset-y-0 right-0 pr-3.5 flex items-center text-xs text-[var(--student-muted)] hover:text-[var(--student-ink)]"
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
              className="h-24 animate-pulse rounded-[20px] bg-[var(--student-surface)] border border-[var(--student-border)]"
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
        <div className="st-card py-14 px-6 text-center">
          <div className="w-12 h-12 rounded-2xl bg-[var(--student-brand-soft)] text-[var(--student-brand)] flex items-center justify-center mx-auto mb-3" aria-hidden="true">
            <svg className="h-6 w-6" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M7 4h10a2 2 0 0 1 2 2v13a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2ZM9 4V3h6v1M9 11h6M9 15h4" /></svg>
          </div>
          <h3 className="text-sm font-semibold text-stone-800 dark:text-stone-200">
            {searchTerm ? "Không có kết quả phù hợp" : history ? "Chưa có bài tập trong lịch sử lớp học" : "Chưa có bài tập trong phạm vi đang xem"}
          </h3>
          <p className="mt-1 text-xs text-stone-500 dark:text-stone-400 max-w-sm mx-auto">
            {searchTerm
              ? "Thử tìm kiếm với từ khóa khác hoặc xóa bộ lọc."
              : history ? "Chỉ bài tập thuộc lớp đã kết thúc hoặc lớp bạn đã rời khỏi xuất hiện ở đây. Bài làm cũ trong lớp đang học vẫn được xem tại mục Đang học."
              : "Thử chọn lớp khác hoặc xem lại bộ lọc trạng thái bài tập."}
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
                <span className="st-eyebrow">
                  {group.label}
                </span>
                <div className="flex-1 h-px bg-[var(--student-border)]" />
                <span className="rounded-full bg-[var(--student-surface-subtle)] px-2.5 py-0.5 text-xs font-semibold text-[var(--student-muted)]">
                  {group.items.length} bài
                </span>
              </div>

              {/* Assignment Rows List */}
              <div className="grid gap-3">
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
                  const isPastDue = !isDone && (progress.status === "Overdue" || isAssignmentPastDue(item.dueAt));

                  return (
                    <div
                      key={item.assignmentId}
                      className="group st-card st-spot flex flex-col sm:flex-row sm:items-center justify-between p-4 sm:p-5 gap-3 transition-[transform,border-color,box-shadow] duration-200 hover:-translate-y-0.5 hover:border-[var(--student-brand-border)] hover:shadow-[var(--student-shadow-lg)]"
                    >
                      {/* Left: Subject Indicator & Title */}
                      <div className="flex items-start gap-3 min-w-0 flex-1">
                        <span
                          className={`mt-0.5 flex h-10 w-10 shrink-0 items-center justify-center rounded-xl text-white ${isDone ? "bg-emerald-600" : isPastDue ? "bg-[var(--student-muted)]" : "bg-[linear-gradient(135deg,var(--student-brand),var(--student-brand-2))]"}`}
                          title={subjectName}
                          aria-hidden="true"
                        >
                          {isDone
                            ? <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="m5 12 5 5 9-10" /></svg>
                            : <svg className="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M7 4h10a2 2 0 0 1 2 2v13a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2ZM9 11h6M9 15h4" /></svg>}
                        </span>

                        <div className="min-w-0 space-y-1">
                          <div className="flex flex-wrap items-center gap-2">
                            <span className="text-[11px] font-bold uppercase tracking-wider" style={{ color: theme.color }}>
                              {subjectName}
                            </span>
                            <span className="text-[10px] text-stone-500">Giáo viên giao</span>
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
                                  </>
                                )}
                              </>
                            ) : item.progress.status === "Overdue" ? (
                              <StudentBadge variant="danger" size="xs">Quá hạn</StudentBadge>
                            ) : null}
                          </div>

                          <Link
                            to={studentScopeUrl(`/hoc-tap/bai-tap/${item.assignmentId}`, searchParams)}
                            className="text-sm sm:text-base font-bold text-[var(--student-ink)] group-hover:text-[var(--student-brand)] transition-colors line-clamp-1 [font-family:var(--student-font-display)]"
                          >
                            {item.title}
                          </Link>

                          <AssignmentKnowledgeScope summary={item.summary} completed={isDone} hideDistribution={item.kind === "Midterm" || item.kind === "Final"} />
                          {isDone && item.summary && <p className="text-xs font-medium text-stone-600 dark:text-stone-300">{assignmentResultLabel(item.summary)}</p>}

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
                        <div className="w-36 sm:w-40 space-y-1 text-right" aria-label={`Tiến độ làm bài: ${progress.completedQuestionCount}/${progress.totalQuestionCount} câu`}>
                          <div className="flex items-center justify-between text-[11px]">
                            <span className="text-stone-500 dark:text-stone-400">
                              Đã làm {progress.completedQuestionCount}/{progress.totalQuestionCount} câu
                            </span>
                          </div>
                          <div className="w-full bg-[var(--student-surface-subtle)] rounded-full h-2 overflow-hidden">
                            <div
                              className={`h-full rounded-full transition-all duration-500 ${
                                isDone ? "bg-emerald-500" : "bg-[linear-gradient(90deg,var(--student-brand),var(--student-brand-2))]"
                              }`}
                              style={{ width: `${percent}%` }}
                            />
                          </div>
                        </div>

                        {/* Due info */}
                        {item.dueAt && (
                          <span className="hidden md:inline rounded-full border border-[var(--student-border)] px-2.5 py-1 text-xs font-semibold tabular-nums text-[var(--student-ink-secondary)]">
                            {new Date(item.dueAt).toLocaleDateString("vi-VN", {
                              day: "2-digit",
                              month: "2-digit",
                            })}
                          </span>
                        )}

                        {/* CTA Link */}
                        <Link
                          to={studentScopeUrl(`/hoc-tap/bai-tap/${item.assignmentId}`, searchParams)}
                          className={`inline-flex items-center gap-1.5 rounded-full px-4 py-2 text-xs sm:text-sm font-bold whitespace-nowrap ${history || isDone || isPastDue ? "border border-[var(--student-border)] bg-[var(--student-surface)] text-[var(--student-ink)] hover:border-[var(--student-brand)] hover:text-[var(--student-brand)] transition-colors" : "st-btn-primary"}`}
                        >
                          <span>{history || isDone ? "Xem lại" : isPastDue ? "Xem (quá hạn)" : isStarted ? "Tiếp tục" : "Bắt đầu"}</span>
                          <svg aria-hidden="true" className="st-arrow h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M5 12h14M13 6l6 6-6 6" /></svg>
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
