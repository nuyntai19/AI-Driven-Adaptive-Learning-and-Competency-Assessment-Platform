import React, { useState, useMemo, useEffect } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { getStudentTwin, getStudentTwinHistory } from "../api/digitalTwinApi";
import { getStudentDashboard } from "../api/dashboardsApi";
import type { StudentTwinDataDto, TwinUpdateHistoryItemDto } from "../types/digitalTwin";
import { StudentSubjectRequiredState } from "../components/student/StudentSubjectRequiredState";
import { organizationApi } from "../api/organizationApi";
import { mapSafeOperationalError } from "../utils/problemDetails";
import { studentScopeUrl } from "../utils/studentAcademicNavigation";
import { TOPIC_STATUS_LABELS, topicMasteryStatus, type TopicMasteryStatus } from "../utils/topicMastery";
import { formatDayMonth, formatTimeDayMonth } from "../utils/dateInput";

const PANEL = "st-card p-5 sm:p-6";
const PRIMARY = "st-btn-primary group inline-flex items-center justify-center gap-2 rounded-full px-5 py-2.5 text-sm font-bold whitespace-nowrap [font-family:var(--student-font-display)]";
const OUTLINE = "inline-flex items-center justify-center rounded-full border border-[var(--student-border)] bg-[var(--student-surface)] px-4 py-2.5 text-sm font-semibold text-[var(--student-ink)] transition-colors hover:border-[var(--student-brand)] hover:text-[var(--student-brand-ink)] cursor-pointer disabled:opacity-50";
const INPUT = "mt-1 w-full rounded-xl border border-[var(--student-border)] bg-[var(--student-surface)] px-3 py-2.5 text-sm font-normal text-[var(--student-ink)] focus:border-[var(--student-brand)] focus:outline-none focus:ring-4 focus:ring-[var(--student-brand-soft)]";
const PILL = "inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-[11px] font-bold leading-4";
const ArrowIcon = () => (
  <svg aria-hidden="true" className="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M5 12h14m-5-5 5 5-5 5" /></svg>
);
const TOPIC_FILTERS: TopicMasteryStatus[] = ["needsWork", "developing", "mastered", "noData"];
const STATUS_TEXT: Record<TopicMasteryStatus, string> = {
  noData: "text-stone-400 dark:text-stone-500",
  needsWork: "text-amber-700 dark:text-amber-400",
  developing: "text-[var(--student-brand-ink)]",
  mastered: "text-emerald-700 dark:text-emerald-400",
};
const STATUS_BAR: Record<TopicMasteryStatus, string> = {
  noData: "bg-stone-300 dark:bg-stone-600",
  needsWork: "bg-amber-500",
  developing: "bg-[linear-gradient(90deg,var(--student-brand),var(--student-brand-2))]",
  mastered: "bg-emerald-500",
};

export const StudentTwinPage: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const selectedSubjectId = searchParams.get("subjectId") || "";
  const selectedClassId = searchParams.get("classId") || "";
  const isHistoryView = searchParams.get("history") === "true";
  const [topicFilter, setTopicFilter] = useState<TopicMasteryStatus | "all">("all");
  const [historyPage, setHistoryPage] = useState<number>(1);
  const [isGoalEditorOpen, setIsGoalEditorOpen] = useState(false);
  const [targetScoreInput, setTargetScoreInput] = useState("8.0");
  const [remainingDaysInput, setRemainingDaysInput] = useState("90");
  const [goalError, setGoalError] = useState<string | null>(null);
  const queryClient = useQueryClient();

  const {
    data: twinData,
    isLoading: twinLoading,
    isError: twinError,
    error: twinErrorObj,
    refetch: refetchTwin,
  } = useQuery<StudentTwinDataDto>({
    queryKey: ["studentTwin", selectedSubjectId, selectedClassId, isHistoryView],
    queryFn: () => getStudentTwin(selectedSubjectId, selectedClassId || undefined, isHistoryView),
    enabled: !!selectedSubjectId,
  });

  const dashboardQuery = useQuery({
    queryKey: ["studentDashboard", selectedSubjectId, searchParams.get("classId") || "", searchParams.get("history") === "true"],
    queryFn: () => getStudentDashboard(selectedSubjectId, searchParams.get("classId") || undefined, searchParams.get("history") === "true"),
    enabled: !!selectedSubjectId,
  });

  const studentId = dashboardQuery.data?.student.studentId || "";
  const goalsQuery = useQuery({
    queryKey: ["student-subject-goals", studentId],
    queryFn: () => organizationApi.listStudentSubjectGoals(studentId),
    enabled: Boolean(studentId),
  });
  const currentGoal = goalsQuery.data?.find((item) => item.subjectId === selectedSubjectId);

  useEffect(() => {
    if (!currentGoal) return;
    setTargetScoreInput(currentGoal.targetScore.toString());
    setRemainingDaysInput(currentGoal.remainingDays.toString());
  }, [currentGoal]);

  const saveGoalMutation = useMutation({
    mutationFn: () => organizationApi.upsertStudentSubjectGoal(studentId, selectedSubjectId, {
      targetScore: Number(targetScoreInput),
      remainingDays: Number(remainingDaysInput),
      rowVersion: currentGoal?.rowVersion,
    }),
    onSuccess: async () => {
      setGoalError(null);
      setIsGoalEditorOpen(false);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["student-subject-goals", studentId] }),
        queryClient.invalidateQueries({ queryKey: ["studentDashboard", selectedSubjectId] }),
      ]);
    },
    onError: (error: unknown) => setGoalError(mapSafeOperationalError(error, "Không thể lưu mục tiêu học tập.")),
  });

  const handleSaveGoal = () => {
    const target = Number(targetScoreInput);
    const days = Number(remainingDaysInput);
    if (!Number.isFinite(target) || target < 0 || target > 10 || !Number.isInteger(days) || days < 1 || days > 3650) {
      setGoalError("Điểm mục tiêu phải từ 0 đến 10 và thời hạn phải từ 1 đến 3650 ngày.");
      return;
    }
    setGoalError(null);
    saveGoalMutation.mutate();
  };

  const {
    data: historyData,
    isLoading: historyLoading,
  } = useQuery<TwinUpdateHistoryItemDto[]>({
    queryKey: ["studentTwinHistory", selectedSubjectId, selectedClassId, isHistoryView],
    queryFn: () => getStudentTwinHistory(selectedSubjectId, undefined, selectedClassId || undefined, isHistoryView),
    enabled: !!selectedSubjectId,
  });

  // Calculate factual insight from behavioral telemetry
  const behavioralInsight = useMemo(() => {
    if (!twinData?.behavior) return "";
    const b = twinData.behavior;
    if (b.attemptCount === 0) return "Chưa có lượt làm bài để nhận định tốc độ và thói quen làm bài.";
    const parts: string[] = [];

    if (b.avgTimeSpentSeconds < 30) {
      parts.push("Bạn có tốc độ làm bài khá nhanh");
    } else if (b.avgTimeSpentSeconds > 120) {
      parts.push("Bạn dành nhiều thời gian suy ngẫm kỹ lưỡng từng bước giải");
    } else {
      parts.push("Tốc độ xử lý câu hỏi của bạn ở mức cân bằng");
    }

    if (b.skipRate >= 40) {
      parts.push(`tuy nhiên tỷ lệ bỏ qua câu còn cao (${b.skipRate.toFixed(0)}%) — hãy kiên nhẫn khai thác các bước trung gian`);
    } else if (b.changeAnswerRate >= 35) {
      parts.push(`tỷ lệ đổi đáp án ở mức ${b.changeAnswerRate.toFixed(0)}% — hãy rèn luyện thêm tính chắc chắn trong lập luận`);
    } else {
      parts.push("hãy tiếp tục rèn luyện thêm bài tập để củng cố kiến thức");
    }

    return parts.join(", ") + ".";
  }, [twinData?.behavior]);

  if (!selectedSubjectId) {
    return <StudentSubjectRequiredState onSelect={(subjectId) => setSearchParams({ subjectId })} />;
  }

  if (twinLoading || dashboardQuery.isLoading) {
    return (
      <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6 min-w-0 student-shell">
        <div className="h-32 animate-pulse rounded-[20px] bg-[var(--student-surface)] border border-[var(--student-border)]" />
        <div className="h-56 animate-pulse rounded-[20px] bg-[var(--student-surface)] border border-[var(--student-border)]" />
        <div className="h-72 animate-pulse rounded-[20px] bg-[var(--student-surface)] border border-[var(--student-border)]" />
      </div>
    );
  }

  if (twinError || dashboardQuery.isError || !twinData || !dashboardQuery.data) {
    return (
      <div className="max-w-2xl mx-auto px-4 py-12 student-shell">
        <div role="alert" className="st-card p-8 text-center">
          <h2 className="text-base font-bold text-red-700 dark:text-red-400">
            Không thể tải Hồ sơ Năng lực
          </h2>
          <p className="mt-2 text-xs text-stone-500 dark:text-stone-400">
            {(twinErrorObj as Error)?.message || "Vui lòng kiểm tra lại quyền truy cập hoặc kết nối mạng."}
          </p>
          <div className="mt-4 flex justify-center gap-3">
            <button
              type="button"
              onClick={() => refetchTwin()}
              className={`${PRIMARY} !py-2 !text-xs`}
            >
              Thử lại
            </button>
            <Link
              to={studentScopeUrl("/hoc-tap/tong-quan", searchParams)}
              className={`${OUTLINE} !py-2 !text-xs`}
            >
              Về Tổng quan
            </Link>
          </div>
        </div>
      </div>
    );
  }

  const { subject, goal, progressLine, academicContext } = dashboardQuery.data;
  const behaviorTwin = twinData.behavior;
  const topics = twinData.topics.map((topic) => ({
    ...topic,
    status: topicMasteryStatus(topic.masteryPercentage, topic.evidenceCount),
  }));
  const countByStatus = (status: TopicMasteryStatus) => topics.filter((topic) => topic.status === status).length;
  const visibleTopics = topicFilter === "all" ? topics : topics.filter((topic) => topic.status === topicFilter);
  const classInView = academicContext?.classes.find((item) => item.classId === academicContext.selectedClassId);

  const latestChange = progressLine.length > 1
    ? progressLine[progressLine.length - 1].overallSubjectMastery - progressLine[progressLine.length - 2].overallSubjectMastery
    : null;

  const historyPageSize = 8;
  const historyTotalPages = Math.max(1, Math.ceil((historyData?.length ?? 0) / historyPageSize));
  const currentHistoryPage = Math.min(historyPage, historyTotalPages);
  const visibleHistory =
    historyData?.slice((currentHistoryPage - 1) * historyPageSize, currentHistoryPage * historyPageSize) ?? [];

  const currentPercent = Math.min(100, Math.max(0, (goal.currentPredictedScore / 10) * 100));
  const targetPercent = Math.min(100, Math.max(0, (goal.targetScore / 10) * 100));
  const filterChoices: Array<{ value: TopicMasteryStatus | "all"; label: string; count: number }> = [
    { value: "all", label: "Tất cả", count: topics.length },
    ...TOPIC_FILTERS.map((value) => ({ value, label: TOPIC_STATUS_LABELS[value], count: countByStatus(value) })),
  ];

  const chartPoints = progressLine.slice(-12);
  const chart = (() => {
    if (chartPoints.length < 2) return null;
    const left = 34, right = 352, top = 16, bottom = 120;
    const x = (i: number) => left + (i * (right - left)) / (chartPoints.length - 1);
    const y = (v: number) => bottom - (Math.min(100, Math.max(0, v)) / 100) * (bottom - top);
    const line = chartPoints.map((p, i) => `${i === 0 ? "M" : "L"}${x(i).toFixed(1)} ${y(p.overallSubjectMastery).toFixed(1)}`).join(" ");
    const area = `${line} L${right} ${bottom} L${left} ${bottom} Z`;
    const last = chartPoints[chartPoints.length - 1];
    const first = chartPoints[0];
    const fmt = formatDayMonth;
    return { line, area, endX: x(chartPoints.length - 1), endY: y(last.overallSubjectMastery), first, last, fmt, top, bottom, left, right };
  })();

  return (
    <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-5 min-w-0 student-shell">
      <header className="st-rise flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div className="min-w-0">
          <p className="st-eyebrow">Bản sao số năng lực · {subject.subjectName}{classInView ? ` · ${classInView.className}` : ""}</p>
          <h1 className="mt-1.5 text-2xl font-extrabold tracking-tight sm:text-3xl [font-family:var(--student-font-display)]">
            Hồ sơ <span className="st-grad-text">năng lực</span>
          </h1>
          <p className="mt-1.5 text-sm text-[var(--student-ink-secondary)]">Chỉ tính các chuyên đề trong giáo trình của lớp đang xem.</p>
        </div>
        <div className="flex shrink-0 flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => setIsGoalEditorOpen((open) => !open)}
            aria-expanded={isGoalEditorOpen}
            className={OUTLINE}
          >
            {currentGoal ? "Sửa mục tiêu" : "Đặt mục tiêu"}
          </button>
          <Link to={studentScopeUrl("/hoc-tap/luyen-tap", searchParams)} className={PRIMARY}>
            <span>Luyện tập ngay</span>
            <span className="st-arrow"><ArrowIcon /></span>
          </Link>
        </div>
      </header>

      {isGoalEditorOpen && (
        <div className={PANEL}>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
            <label className="flex-1 text-xs font-bold text-[var(--student-ink)]">
              Điểm mục tiêu (0–10)
              <input
                type="number"
                min="0"
                max="10"
                step="0.1"
                value={targetScoreInput}
                onChange={(event) => setTargetScoreInput(event.target.value)}
                className={INPUT}
              />
            </label>
            <label className="flex-1 text-xs font-bold text-[var(--student-ink)]">
              Thời hạn (ngày)
              <input
                type="number"
                min="1"
                max="3650"
                step="1"
                value={remainingDaysInput}
                onChange={(event) => setRemainingDaysInput(event.target.value)}
                className={INPUT}
              />
            </label>
            <button type="button" onClick={handleSaveGoal} disabled={saveGoalMutation.isPending} className={`${PRIMARY} !py-2.5`}>
              {saveGoalMutation.isPending ? "Đang lưu..." : "Lưu mục tiêu của tôi"}
            </button>
          </div>
          {goalError && <p role="alert" className="mt-2 text-xs font-medium text-rose-700 dark:text-rose-300">{goalError}</p>}
        </div>
      )}

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-12 lg:items-start">
        <section aria-labelledby="twin-goal-heading" className={`${PANEL} st-spot st-rise lg:col-span-5`} style={{ "--d": 1 } as React.CSSProperties}>
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 id="twin-goal-heading" className="text-base font-bold">Tiến tới mục tiêu</h2>
            {latestChange !== null && Math.abs(latestChange) >= 0.05 && (
              <span
                title="Thay đổi mức thành thạo của môn so với mốc đo trước"
                className={`${PILL} tabular-nums ${latestChange >= 0 ? "bg-emerald-50 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300" : "bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300"}`}
              >
                {latestChange >= 0 ? "+" : ""}{latestChange.toFixed(1)}% so với mốc trước
              </span>
            )}
          </div>

          <div className="mt-4 flex items-end justify-between gap-4">
            <div>
              <p className="text-xs text-[var(--student-muted)]">Hiện tại</p>
              <p className="text-4xl font-extrabold tabular-nums [font-family:var(--student-font-display)]">
                {goal.hasPrediction === false ? <span className="text-2xl text-[var(--student-muted)]">Chưa có</span> : goal.currentPredictedScore.toFixed(1)}
                {goal.hasPrediction !== false && <span className="text-sm font-medium text-[var(--student-muted)]"> / 10</span>}
              </p>
            </div>
            <div className="text-right">
              <p className="text-xs text-[var(--student-muted)]">Mục tiêu</p>
              <p className="text-4xl font-extrabold tabular-nums [font-family:var(--student-font-display)]">
                {goal.hasGoal ? <span className="st-grad-text">{goal.targetScore.toFixed(1)}</span> : <span className="text-2xl text-[var(--student-muted)]">Chưa đặt</span>}
              </p>
            </div>
          </div>

          <div className="relative mt-3 h-2.5 rounded-full bg-[var(--student-surface-subtle)]">
            {goal.hasPrediction !== false && (
              <div className="st-bar-fill h-full rounded-full bg-[linear-gradient(90deg,var(--student-brand),var(--student-brand-2))]" style={{ width: `${currentPercent}%` }} />
            )}
            {goal.hasGoal && (
              <span
                aria-hidden="true"
                className="absolute top-1/2 h-5 w-0.5 -translate-x-1/2 -translate-y-1/2 rounded-full bg-[var(--student-ink)]"
                style={{ left: `${targetPercent}%` }}
              />
            )}
          </div>

          <div className="mt-2 flex flex-wrap items-center justify-between gap-x-4 gap-y-1 text-xs text-[var(--student-muted)]">
            <span>{goal.hasDeadline === false ? "Chưa đặt ngày đích" : goal.hasGoal ? `Còn ${goal.remainingDays} ngày` : "Bạn chưa đặt mục tiêu cho môn này"}</span>
            <span className="tabular-nums">{goal.hasPrediction === false ? "Chưa đủ dữ liệu dự báo" : goal.hasGoal ? `Cần thêm ${Math.max(0, goal.targetScore - goal.currentPredictedScore).toFixed(1)} điểm` : "Hãy chọn Đặt mục tiêu"}</span>
          </div>
          {goal.forecastMessage && <p className="mt-3 text-xs leading-relaxed text-[var(--student-ink-secondary)]">{goal.forecastMessage}</p>}
        </section>

        <section aria-labelledby="twin-behavior-heading" className={`${PANEL} st-spot st-rise lg:col-span-7`} style={{ "--d": 2 } as React.CSSProperties}>
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 id="twin-behavior-heading" className="text-base font-bold">Thói quen làm bài</h2>
            <span className="text-xs tabular-nums text-[var(--student-muted)]">{behaviorTwin.attemptCount} lượt làm · cả môn</span>
          </div>
          {behaviorTwin.attemptCount > 0 && (
            <dl className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
              {[
                { label: "thời gian trung bình / câu", value: `${Math.round(behaviorTwin.avgTimeSpentSeconds)} giây` },
                { label: "tỷ lệ bỏ qua câu", value: `${behaviorTwin.skipRate.toFixed(0)}%` },
                { label: "tỷ lệ đổi đáp án", value: `${behaviorTwin.changeAnswerRate.toFixed(0)}%` },
              ].map((item) => (
                <div key={item.label} className="flex flex-col-reverse rounded-2xl bg-[var(--student-surface-subtle)] px-4 py-3">
                  <dt className="text-xs text-[var(--student-muted)]">{item.label}</dt>
                  <dd className="text-xl font-extrabold tabular-nums [font-family:var(--student-font-display)]">{item.value}</dd>
                </div>
              ))}
            </dl>
          )}
          {behavioralInsight && <p className="mt-3 text-sm leading-relaxed text-[var(--student-ink-secondary)]">{behavioralInsight}</p>}
        </section>

        <section aria-labelledby="twin-topics-heading" className={`${PANEL} st-rise min-w-0 lg:col-span-7`} style={{ "--d": 3 } as React.CSSProperties}>
          <h2 id="twin-topics-heading" className="text-base font-bold">Năng lực theo chuyên đề</h2>
          {topics.length > 0 ? (
            <>
              <div
                role="group"
                aria-label="Lọc chuyên đề theo mức năng lực"
                className="mt-3 flex flex-wrap gap-1.5"
              >
                {filterChoices.map((choice) => {
                  const active = topicFilter === choice.value;
                  return (
                    <button
                      key={choice.value}
                      type="button"
                      aria-pressed={active}
                      onClick={() => setTopicFilter(choice.value)}
                      className={`flex shrink-0 items-center gap-1.5 rounded-full border px-3 py-1.5 text-xs font-semibold whitespace-nowrap cursor-pointer transition-colors ${
                        active ? "st-nav-active border-transparent" : "border-[var(--student-border)] bg-[var(--student-surface)] text-[var(--student-ink-secondary)] hover:border-[var(--student-brand)] hover:text-[var(--student-ink)]"
                      }`}
                    >
                      {choice.label}
                      <span className={`rounded-full px-1.5 text-[10px] font-bold leading-4 tabular-nums ${active ? "bg-white/25" : "bg-[var(--student-surface-subtle)]"}`}>{choice.count}</span>
                    </button>
                  );
                })}
              </div>

              {visibleTopics.length > 0 ? (
                <ul className="mt-3 divide-y divide-[var(--student-border)]">
                  {visibleTopics.map((topic) => (
                    <li key={topic.topicNodeId} className="py-3">
                      <div className="flex items-baseline justify-between gap-3 text-sm">
                        <span className="min-w-0 break-words font-semibold">{topic.topicName}</span>
                        <span className="shrink-0 text-xs tabular-nums">
                          {topic.status !== "noData" && <span className="text-[var(--student-muted)]">{topic.evidenceCount} bài · </span>}
                          <span className={`font-bold ${STATUS_TEXT[topic.status]}`}>
                            {topic.status === "noData" ? "Chưa làm" : `${topic.masteryPercentage.toFixed(0)}%`}
                          </span>
                        </span>
                      </div>
                      <div className="mt-1.5 h-1.5 rounded-full bg-[var(--student-surface-subtle)]">
                        <div
                          className={`st-bar-fill h-full rounded-full ${STATUS_BAR[topic.status]}`}
                          style={{ width: `${Math.min(100, Math.max(0, topic.masteryPercentage))}%` }}
                        />
                      </div>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="mt-4 text-sm text-[var(--student-muted)]">Không có chuyên đề nào ở mức này.</p>
              )}
            </>
          ) : (
            <p className="mt-3 text-sm leading-relaxed text-[var(--student-muted)]">
              {academicContext?.message || "Lớp đang xem chưa có chuyên đề nào. Giáo trình sẽ hiện ở đây khi giáo viên áp dụng cho lớp."}
            </p>
          )}
        </section>

        <div className="min-w-0 space-y-4 lg:col-span-5">
          <section aria-labelledby="twin-chart-heading" className={`${PANEL} st-rise`} style={{ "--d": 4 } as React.CSSProperties}>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h2 id="twin-chart-heading" className="text-base font-bold">Diễn biến năng lực</h2>
              {chart && <span className={`${PILL} border border-[var(--student-border)] text-[var(--student-muted)]`}>{chartPoints.length} mốc đo</span>}
            </div>
            {chart ? (
              <svg
                className="mt-3 w-full"
                viewBox="0 0 360 150"
                role="img"
                aria-label={`Mức thành thạo môn từ ${chart.first.overallSubjectMastery.toFixed(0)}% lên ${chart.last.overallSubjectMastery.toFixed(0)}% qua ${chartPoints.length} mốc đo`}
              >
                <defs>
                  <linearGradient id="twin-area" x1="0" x2="0" y1="0" y2="1">
                    <stop offset="0%" stopColor="var(--student-brand)" stopOpacity="0.22" />
                    <stop offset="100%" stopColor="var(--student-brand)" stopOpacity="0" />
                  </linearGradient>
                </defs>
                {[0, 50, 100].map((v) => {
                  const yy = chart.bottom - (v / 100) * (chart.bottom - chart.top);
                  return (
                    <g key={v}>
                      <line x1={chart.left} x2={chart.right} y1={yy} y2={yy} stroke="var(--student-border)" strokeDasharray="3 4" />
                      <text x={chart.left - 6} y={yy + 4} textAnchor="end" fontSize="10" fill="var(--student-muted)">{v}</text>
                    </g>
                  );
                })}
                <path d={chart.area} fill="url(#twin-area)" />
                <path d={chart.line} fill="none" stroke="var(--student-brand)" strokeWidth="2.5" strokeLinejoin="round" strokeLinecap="round" />
                <circle cx={chart.endX} cy={chart.endY} r="5" fill="var(--student-brand)" stroke="var(--student-surface)" strokeWidth="2" />
                <text x={chart.left} y="140" fontSize="10" fill="var(--student-muted)">{chart.fmt(chart.first.recordedAt)}</text>
                <text x={chart.right} y="140" textAnchor="end" fontSize="10" fill="var(--student-muted)">{chart.fmt(chart.last.recordedAt)} · {chart.last.overallSubjectMastery.toFixed(0)}%</text>
              </svg>
            ) : (
              <p className="mt-3 text-sm text-[var(--student-muted)]">Biểu đồ sẽ hiện khi có từ hai mốc đo năng lực trở lên.</p>
            )}
          </section>

          <section aria-labelledby="twin-history-heading" className={`${PANEL} st-rise`} style={{ "--d": 5 } as React.CSSProperties}>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h2 id="twin-history-heading" className="text-base font-bold">Nhật ký cập nhật năng lực</h2>
              <span className="text-xs tabular-nums text-[var(--student-muted)]">{historyData?.length ?? 0} lần cập nhật</span>
            </div>

            <div className="mt-2 divide-y divide-[var(--student-border)]">
              {historyLoading ? (
                <p role="status" className="py-4 text-sm text-[var(--student-muted)]">Đang tải nhật ký...</p>
              ) : visibleHistory.length > 0 ? (
                visibleHistory.map((item) => {
                  const isTeacher = item.eventSource === "TeacherOverride";
                  return (
                    <div key={item.historyId} className="flex items-start gap-3 py-3 text-sm">
                      <span
                        title={`${item.previousMastery.toFixed(1)}% → ${item.newMastery.toFixed(1)}%`}
                        className={`mt-0.5 inline-flex min-w-[52px] shrink-0 justify-center rounded-lg px-1.5 py-1 text-xs font-bold tabular-nums ${
                          isTeacher
                            ? "bg-sky-50 text-sky-800 dark:bg-sky-950/40 dark:text-sky-300"
                            : item.delta >= 0
                              ? "bg-emerald-50 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300"
                              : "bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300"
                        }`}
                      >
                        {isTeacher ? "GV" : item.delta >= 0 ? `+${item.delta.toFixed(1)}%` : `${item.delta.toFixed(1)}%`}
                      </span>
                      <div className="min-w-0 flex-1">
                        <b className="block break-words">{item.topicName}</b>
                        <small className="block text-xs text-[var(--student-muted)]">
                          {isTeacher ? "Giáo viên xác nhận" : item.learningContext === "Assignment" ? `Bài tập: ${item.contextLabel}` : item.contextLabel}
                          {" · "}
                          {formatTimeDayMonth(item.recordedAt)}
                          {" · "}
                          <span className="tabular-nums">{item.previousMastery.toFixed(1)}% → {item.newMastery.toFixed(1)}%</span>
                        </small>
                        {item.explanation && <p className="mt-0.5 text-xs leading-relaxed text-[var(--student-ink-secondary)]">{item.explanation}</p>}
                      </div>
                    </div>
                  );
                })
              ) : (
                <p className="py-4 text-sm text-[var(--student-muted)]">
                  Chưa có cập nhật nào. Năng lực sẽ được ghi lại sau mỗi bài tập được chấm.
                </p>
              )}
            </div>

            {historyTotalPages > 1 && (
              <div className="mt-2 flex items-center justify-between gap-2 text-xs">
                <button
                  type="button"
                  onClick={() => setHistoryPage(Math.max(1, currentHistoryPage - 1))}
                  disabled={currentHistoryPage <= 1}
                  className={`${OUTLINE} !px-3 !py-1.5 !text-xs disabled:cursor-not-allowed`}
                >
                  Trang trước
                </button>
                <span className="tabular-nums text-[var(--student-ink-secondary)]">Trang {currentHistoryPage} / {historyTotalPages}</span>
                <button
                  type="button"
                  onClick={() => setHistoryPage(Math.min(historyTotalPages, currentHistoryPage + 1))}
                  disabled={currentHistoryPage >= historyTotalPages}
                  className={`${OUTLINE} !px-3 !py-1.5 !text-xs disabled:cursor-not-allowed`}
                >
                  Trang sau
                </button>
              </div>
            )}
          </section>
        </div>
      </div>
    </div>
  );
};
