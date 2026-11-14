import React, { useState, useMemo } from "react";
import { isAxiosError } from "axios";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useStudentAssignments } from "../features/assignments/useStudentAssignments";
import { organizationApi } from "../api/organizationApi";
import { getSubjectTheme } from "../components/student/subjectTheme";
import type { ProgressStatus, StudentAssignmentListItemDto } from "../types/assignments";
import { studentScopeUrl } from "../utils/studentAcademicNavigation";
import { isAssignmentPastDue } from "../utils/assignmentReviewTiming";
import { AssignmentKnowledgeScope } from "../components/student/AssignmentKnowledgeScope";
import { assignmentKnowledgeSearch, assignmentResultLabel } from "../utils/assignmentKnowledge";
import { formatDayMonth } from "../utils/dateInput";

const getStudentListError = (error: unknown) => {
  if (isAxiosError(error)) return error.response?.data?.detail || error.message;
  return error instanceof Error ? error.message : "Không thể tải danh sách bài tập.";
};

type TimelineGroupKey = "overdue" | "today" | "tomorrow" | "thisWeek" | "later" | "done";

interface TimelineGroup {
  key: TimelineGroupKey;
  label: string;
  items: StudentAssignmentListItemDto[];
}

const STATUS_TABS: { id: ProgressStatus | ""; label: string }[] = [
  { id: "", label: "Tất cả" },
  { id: "NotStarted", label: "Chưa bắt đầu" },
  { id: "InProgress", label: "Đang làm" },
  { id: "Completed", label: "Đã xong" },
  { id: "Overdue", label: "Quá hạn" },
];

const SUMMARY_TILES: { id: ProgressStatus; label: string; tone: string }[] = [
  { id: "NotStarted", label: "chưa bắt đầu", tone: "" },
  { id: "InProgress", label: "đang làm", tone: "" },
  { id: "Overdue", label: "quá hạn", tone: "text-[var(--student-brand)]" },
  { id: "Completed", label: "đã nộp", tone: "text-emerald-700 dark:text-emerald-400" },
];

const kindLabel = (kind?: string) =>
  kind === "Midterm" ? "Thi giữa kỳ" : kind === "Final" ? "Thi cuối kỳ" : kind === "MockExam" ? "Thi thử" : "Bài tập";

const PILL = "inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-bold leading-4";

const Icon = ({ d, className = "h-4 w-4" }: { d: string; className?: string }) => (
  <svg aria-hidden="true" className={className} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d={d} /></svg>
);
const ICON_CHECK = "m5 12 5 5 9-10";
const ICON_CLOCK = "M12 7v5l3 2M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z";
const ICON_CLIP = "M9 4h6v3H9zM8 5H6a1 1 0 0 0-1 1v14a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V6a1 1 0 0 0-1-1h-2M9 12h6M9 16h4";
const ICON_SEARCH = "m20 20-4.3-4.3M17 10.5a6.5 6.5 0 1 1-13 0 6.5 6.5 0 0 1 13 0Z";
const ICON_ARROW = "M5 12h14m-5-5 5 5-5 5";

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

  // The whole list is loaded once so the status tiles can count every state; the status filter is applied locally.
  const { data: response, isLoading, isError, error, refetch } = useStudentAssignments({
    subjectId: (!ignoreSubjectFilter && selectedSubjectId) ? selectedSubjectId : undefined,
    classId: !ignoreSubjectFilter ? searchParams.get("classId") || undefined : undefined,
    history,
    pageSize: 100,
  });

  const rawAssignments = useMemo(() => response?.data || [], [response]);

  const statusCounts = useMemo(() => {
    const counts: Record<string, number> = {};
    rawAssignments.forEach((a) => { counts[a.progress.status] = (counts[a.progress.status] || 0) + 1; });
    return counts;
  }, [rawAssignments]);

  // Filter by status and search term
  const filteredAssignments = useMemo(() => {
    let result = statusFilter ? rawAssignments.filter((a) => a.progress.status === statusFilter) : [...rawAssignments];

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
  }, [rawAssignments, statusFilter, searchTerm]);

  // Group into a timeline: overdue first, then by due date, submitted work last.
  const timelineGroups = useMemo<TimelineGroup[]>(() => {
    const now = new Date();
    const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
    const startOfTomorrow = startOfToday + 24 * 60 * 60 * 1000;
    const startOfDayAfterTomorrow = startOfTomorrow + 24 * 60 * 60 * 1000;
    const endOfWeek = startOfToday + 7 * 24 * 60 * 60 * 1000;

    const buckets: Record<TimelineGroupKey, StudentAssignmentListItemDto[]> = { overdue: [], today: [], tomorrow: [], thisWeek: [], later: [], done: [] };

    filteredAssignments.forEach((item) => {
      if (item.progress.status === "Completed") { buckets.done.push(item); return; }
      if (item.progress.status === "Overdue" || isAssignmentPastDue(item.dueAt)) { buckets.overdue.push(item); return; }
      if (!item.dueAt) { buckets.later.push(item); return; }
      const dueTime = new Date(item.dueAt).getTime();
      if (dueTime < startOfTomorrow) buckets.today.push(item);
      else if (dueTime < startOfDayAfterTomorrow) buckets.tomorrow.push(item);
      else if (dueTime < endOfWeek) buckets.thisWeek.push(item);
      else buckets.later.push(item);
    });

    const byDue = (a: StudentAssignmentListItemDto, b: StudentAssignmentListItemDto) =>
      (a.dueAt ? new Date(a.dueAt).getTime() : Infinity) - (b.dueAt ? new Date(b.dueAt).getTime() : Infinity);
    const labels: [TimelineGroupKey, string][] = [
      ["overdue", "Đã quá hạn"], ["today", "Hôm nay"], ["tomorrow", "Ngày mai"], ["thisWeek", "Tuần này"], ["later", "Sau đó"], ["done", "Đã nộp"],
    ];
    return labels
      .filter(([key]) => buckets[key].length > 0)
      .map(([key, label]) => ({ key, label, items: key === "done" ? buckets[key] : [...buckets[key]].sort(byDue) }));
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

  const dueText = (item: StudentAssignmentListItemDto, isDone: boolean, isPastDue: boolean) => {
    if (!item.dueAt) return "Không giới hạn hạn nộp";
    const date = new Date(item.dueAt);
    const day = formatDayMonth(date);
    const time = date.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
    if (isDone) return `Hạn ${day}`;
    return isPastDue ? `Quá hạn ${day}` : `Hạn ${time} · ${day}`;
  };

  return (
    <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-5 min-w-0 student-shell">
      {/* Page header */}
      <header className="st-rise flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div className="min-w-0">
          <p className="st-eyebrow">Bài giáo viên giao · theo mốc thời gian</p>
          <h1 className="mt-1.5 text-2xl font-extrabold tracking-tight sm:text-3xl [font-family:var(--student-font-display)]">
            Bài tập <span className="st-grad-text">của tôi</span>
          </h1>
          <p className="mt-1.5 max-w-2xl text-sm text-[var(--student-ink-secondary)]">
            {history
              ? "Bài tập thuộc các lớp đã kết thúc hoặc lớp bạn đã rời khỏi."
              : "Bài gần hạn đứng trước. Bài đã nộp vẫn xem lại được cùng nhận xét của giáo viên."}
          </p>
        </div>

        {/* Active subject filter indicator */}
        {currentSubjectName && !ignoreSubjectFilter ? (() => {
          const theme = getSubjectTheme(currentSubjectName);
          return (
            <span
              className="inline-flex w-fit shrink-0 items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold"
              style={{ backgroundColor: theme.bg, color: theme.color, border: `1px solid ${theme.border}` }}
            >
              <span className="w-1.5 h-1.5 rounded-full" style={{ backgroundColor: theme.color }} />
              <span>Môn: {currentSubjectName}</span>
              <button
                type="button"
                onClick={() => setIgnoreSubjectFilter(true)}
                className="ml-1 hover:opacity-75 cursor-pointer font-bold"
                title="Xem tất cả môn học"
                aria-label="Xem tất cả môn học"
              >
                ×
              </button>
            </span>
          );
        })() : ignoreSubjectFilter && currentSubjectName ? (
          <button
            type="button"
            onClick={() => setIgnoreSubjectFilter(false)}
            className="w-fit shrink-0 text-xs font-semibold text-[var(--student-brand-ink)] hover:underline cursor-pointer"
          >
            ← Quay lại lọc môn {currentSubjectName}
          </button>
        ) : null}
      </header>

      {/* Status summary tiles (click to filter, click again to clear) */}
      {!isError && (
        <div className="grid grid-cols-2 gap-3 md:grid-cols-4" aria-label="Tóm tắt trạng thái bài tập">
          {SUMMARY_TILES.map((tile, i) => {
            const isActive = statusFilter === tile.id;
            return (
              <button
                key={tile.id}
                type="button"
                aria-pressed={isActive}
                onClick={() => setStatusFilter(isActive ? "" : tile.id)}
                style={{ "--d": i } as React.CSSProperties}
                className={`st-rise st-card cursor-pointer px-4 py-3.5 text-left transition-all hover:-translate-y-0.5 ${isActive ? "!border-[var(--student-brand)] ring-4 ring-[var(--student-brand-soft)]" : ""}`}
              >
                <b className={`block text-2xl font-extrabold leading-tight [font-family:var(--student-font-display)] ${tile.tone}`}>
                  {isLoading ? "–" : statusCounts[tile.id] || 0}
                </b>
                <small className="text-xs font-semibold text-[var(--student-muted)]">{tile.label}</small>
              </button>
            );
          })}
        </div>
      )}

      {/* Filter pills & search */}
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div
          role="group"
          aria-label="Lọc theo trạng thái"
          className="flex w-fit max-w-full flex-wrap gap-0.5 rounded-[22px] border border-[var(--student-border)] bg-[var(--student-surface)] p-1 shadow-[var(--student-shadow)] sm:flex-nowrap sm:overflow-x-auto sm:rounded-full lg:shrink-0"
        >
          {STATUS_TABS.map((tab) => {
            const isActive = statusFilter === tab.id;
            return (
              <button
                key={tab.id || "all"}
                type="button"
                aria-pressed={isActive}
                onClick={() => setStatusFilter(tab.id)}
                className={`shrink-0 rounded-full px-3.5 py-1.5 text-sm font-semibold whitespace-nowrap transition-colors cursor-pointer ${
                  isActive ? "st-nav-active" : "text-[var(--student-ink-secondary)] hover:bg-[var(--student-surface-subtle)] hover:text-[var(--student-ink)]"
                }`}
              >
                {tab.label}
              </button>
            );
          })}
        </div>

        <label className="relative block w-full sm:w-80 lg:w-auto lg:min-w-56 lg:max-w-80 lg:flex-1">
          <span className="pointer-events-none absolute inset-y-0 left-3.5 flex items-center text-[var(--student-muted)]"><Icon d={ICON_SEARCH} /></span>
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder="Tìm bài tập hoặc kỹ năng..."
            aria-label="Tìm bài tập hoặc kỹ năng"
            className="w-full rounded-full border border-[var(--student-border)] bg-[var(--student-surface)] py-2.5 pl-10 pr-9 text-sm text-[var(--student-ink)] placeholder-[var(--student-muted)] transition-shadow focus:border-[var(--student-brand)] focus:outline-none focus:ring-4 focus:ring-[var(--student-brand-soft)]"
          />
          {searchTerm && (
            <button
              type="button"
              onClick={() => setSearchTerm("")}
              aria-label="Xóa tìm kiếm"
              className="absolute inset-y-0 right-0 flex items-center pr-3.5 text-sm text-[var(--student-muted)] hover:text-[var(--student-ink)]"
            >
              ×
            </button>
          )}
        </label>
      </div>

      {/* Loading Skeleton */}
      {isLoading && (
        <div className="space-y-3 py-1">
          {[1, 2, 3].map((i) => (
            <div key={i} className="h-24 animate-pulse rounded-[20px] border border-[var(--student-border)] bg-[var(--student-surface)]" />
          ))}
        </div>
      )}

      {/* Error state */}
      {isError && (
        <div role="alert" className="rounded-2xl border border-rose-200 bg-rose-50 p-6 text-center dark:border-rose-900/50 dark:bg-rose-950/30">
          <p className="text-sm font-medium text-rose-800 dark:text-rose-300">
            Không thể tải danh sách bài tập: {getStudentListError(error)}
          </p>
          <button
            type="button"
            onClick={() => refetch()}
            className="mt-3 inline-flex items-center rounded-full bg-rose-700 px-4 py-1.5 text-xs font-semibold text-white hover:bg-rose-600 cursor-pointer"
          >
            Thử lại
          </button>
        </div>
      )}

      {/* Empty State */}
      {!isLoading && !isError && filteredAssignments.length === 0 && (
        <div className="st-card px-6 py-12 text-center">
          <span className="mx-auto mb-3 flex h-11 w-11 items-center justify-center rounded-2xl bg-[var(--student-brand-soft)] text-[var(--student-brand)]">
            <Icon d={ICON_CHECK} className="h-5 w-5" />
          </span>
          <h3 className="text-sm font-bold text-[var(--student-ink)]">
            {searchTerm || statusFilter ? "Không có bài phù hợp" : history ? "Chưa có bài tập trong lịch sử lớp học" : "Chưa có bài tập trong phạm vi đang xem"}
          </h3>
          <p className="mx-auto mt-1 max-w-sm text-xs text-[var(--student-muted)]">
            {searchTerm || statusFilter
              ? "Thử tìm kiếm với từ khóa khác hoặc bỏ bộ lọc."
              : history ? "Chỉ bài tập thuộc lớp đã kết thúc hoặc lớp bạn đã rời khỏi xuất hiện ở đây. Bài làm cũ trong lớp đang học vẫn được xem tại mục Đang học."
              : "Thử chọn lớp khác hoặc xem lại bộ lọc trạng thái bài tập."}
          </p>
          {(searchTerm || statusFilter) && (
            <button
              type="button"
              onClick={() => { setSearchTerm(""); setStatusFilter(""); }}
              className="mt-3 text-xs font-bold text-[var(--student-brand-ink)] hover:underline"
            >
              Xóa bộ lọc
            </button>
          )}
        </div>
      )}

      {/* Timeline groups */}
      {!isLoading && !isError && timelineGroups.length > 0 && (
        <div className="space-y-7">
          {timelineGroups.map((group) => (
            <section key={group.key} className="space-y-3" aria-label={group.label}>
              <div className="flex items-center gap-3">
                <span className="st-eyebrow">{group.label}</span>
                <span className="h-px flex-1 bg-[var(--student-border)]" />
                <span className={`${PILL} bg-[var(--student-surface-subtle)] text-[var(--student-muted)]`}>{group.items.length} bài</span>
              </div>

              <div className="space-y-3">
                {group.items.map((item, index) => {
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
                  const isUrgent = !isDone && !isPastDue && group.key === "today";
                  const detailUrl = studentScopeUrl(`/hoc-tap/bai-tap/${item.assignmentId}`, searchParams);
                  const actionLabel = history || isDone ? "Xem lại" : isPastDue ? "Xem (quá hạn)" : isStarted ? "Tiếp tục" : "Bắt đầu";

                  return (
                    <article
                      key={item.assignmentId}
                      style={{ "--d": Math.min(index, 6) } as React.CSSProperties}
                      className="st-rise st-card st-spot group grid grid-cols-[auto_minmax(0,1fr)] gap-x-4 gap-y-3 p-4 sm:p-5 lg:grid-cols-[auto_minmax(0,1fr)_auto] lg:items-center"
                    >
                      {/* Status tile */}
                      <span
                        className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl ${
                          isDone
                            ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400"
                            : isPastDue
                              ? "bg-[var(--student-surface-subtle)] text-[var(--student-muted)]"
                              : "bg-[linear-gradient(135deg,var(--student-brand),var(--student-brand-2))] text-white shadow-[0_8px_18px_-10px_var(--student-brand)]"
                        }`}
                      >
                        <Icon d={isDone ? ICON_CHECK : isPastDue ? ICON_CLOCK : ICON_CLIP} className="h-5 w-5" />
                      </span>

                      {/* Body */}
                      <div className="min-w-0 space-y-1.5">
                        <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                          <span className="text-[11px] font-bold uppercase tracking-wider" style={{ color: theme.color }}>{subjectName}</span>
                          <span className="text-xs text-[var(--student-muted)]">{kindLabel(item.kind)} · Giáo viên giao</span>
                          {isDone ? (
                            item.summary ? (
                              item.summary.teacherFinalReviewStatus === "Approved"
                                ? <span className={`${PILL} bg-emerald-50 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300`}>GV đã duyệt</span>
                                : <span className={`${PILL} bg-amber-50 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300`}>GV chưa duyệt</span>
                            ) : <span className={`${PILL} bg-emerald-50 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300`}>Đã xong</span>
                          ) : isPastDue ? (
                            <span className={`${PILL} bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300`}>Quá hạn</span>
                          ) : isUrgent ? (
                            <span className={`${PILL} bg-[var(--student-brand-soft)] text-[var(--student-brand-ink)]`}><span className="st-live" />Gần hạn</span>
                          ) : null}
                        </div>

                        <h3 className="text-base font-bold leading-snug">
                          <Link to={detailUrl} className="break-words text-[var(--student-ink)] transition-colors group-hover:text-[var(--student-brand-ink)]">
                            {item.title}
                          </Link>
                        </h3>

                        <AssignmentKnowledgeScope summary={item.summary} completed={isDone} hideDistribution={item.kind === "Midterm" || item.kind === "Final"} />
                        {isDone && item.summary && <p className="text-xs font-semibold text-[var(--student-ink-secondary)]">{assignmentResultLabel(item.summary)}</p>}

                        <p className="flex items-center gap-1.5 text-xs text-[var(--student-muted)]">
                          <Icon d={ICON_CLOCK} className="h-3.5 w-3.5 shrink-0" />
                          <span>{dueText(item, isDone, isPastDue)}</span>
                        </p>
                        {item.instructions && (
                          <p className="line-clamp-1 text-xs text-[var(--student-muted)]">{item.instructions}</p>
                        )}
                      </div>

                      {/* Progress & action */}
                      <div className="col-span-2 flex flex-wrap items-center justify-between gap-3 lg:col-span-1 lg:flex-nowrap lg:justify-end lg:gap-5">
                        <div className="w-full max-w-[220px] space-y-1.5 lg:w-40" aria-label={`Tiến độ làm bài: ${progress.completedQuestionCount}/${progress.totalQuestionCount} câu`}>
                          <span className="block text-xs text-[var(--student-muted)]">
                            Đã làm {progress.completedQuestionCount}/{progress.totalQuestionCount} câu
                          </span>
                          <div className="h-1.5 w-full overflow-hidden rounded-full bg-[var(--student-surface-subtle)]">
                            <div
                              className={`st-bar-fill h-full rounded-full ${isDone ? "bg-emerald-500" : "bg-[linear-gradient(90deg,var(--student-brand),var(--student-brand-2))]"}`}
                              style={{ width: `${percent}%` }}
                            />
                          </div>
                        </div>

                        <Link
                          to={detailUrl}
                          className={
                            history || isDone || isPastDue
                              ? "st-link inline-flex shrink-0 items-center gap-1.5 rounded-full border border-[var(--student-border)] bg-[var(--student-surface)] px-4 py-2 text-sm font-bold text-[var(--student-ink)] transition-colors hover:border-[var(--student-brand)] hover:text-[var(--student-brand-ink)]"
                              : "st-btn-primary inline-flex shrink-0 items-center gap-1.5 rounded-full px-4 py-2 text-sm font-bold whitespace-nowrap [font-family:var(--student-font-display)]"
                          }
                        >
                          <span>{actionLabel}</span>
                          <span className="st-arrow"><Icon d={ICON_ARROW} className="h-3.5 w-3.5" /></span>
                        </Link>
                      </div>
                    </article>
                  );
                })}
              </div>
            </section>
          ))}
        </div>
      )}
    </div>
  );
};
