import React, { useState, useMemo } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { getStudentDashboard, getStudentWorkspaceSummary } from "../api/dashboardsApi";
import { getStudentAssignments } from "../api/assignmentsApi";
import { getLearningAgenda } from "../api/learningPathApi";
import { getStudentTwinHistory } from "../api/digitalTwinApi";
import type { StudentDashboardDataDto } from "../types/dashboards";
import type { TwinUpdateHistoryItemDto } from "../types/digitalTwin";
import { getSubjectTheme } from "../components/student/subjectTheme";
import { StudentCompetencyList } from "../components/student/StudentCompetencyList";
import { StudentJourneyRoute } from "../components/student/StudentJourneyRoute";
import { studentScopeUrl } from "../utils/studentAcademicNavigation";
import { goalGapLabel } from "../utils/academicDisplay";
import { dashboardGreeting, dashboardNudge } from "../utils/studentDashboardNudge";
import { isAssignmentPastDue } from "../utils/assignmentReviewTiming";

const StudentRadarChart = React.lazy(() => import("../components/student/StudentRadarChart"));
const StudentMasteryTrendChart = React.lazy(() => import("../components/student/StudentMasteryTrendChart"));

const PANEL = "st-card st-spot p-4 sm:p-6";
const PRESS = "transition-[background-color,color,transform,border-color] duration-150 ease-out active:scale-[0.97] motion-reduce:transform-none";
const SECONDARY_ACTION = `rounded-full border border-[var(--student-border)] bg-[var(--student-surface)] px-4 py-2 text-sm font-semibold text-[var(--student-ink)] hover:border-[var(--student-brand)] hover:text-[var(--student-brand)] ${PRESS}`;
const PRIMARY_ACTION = `st-btn-primary group inline-flex items-center gap-2 rounded-full px-5 py-3 text-sm font-bold whitespace-nowrap [font-family:var(--student-font-display)]`;
const ARROW = <svg aria-hidden="true" className="st-arrow h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M5 12h14M13 6l6 6-6 6" /></svg>;

interface TodayTaskData {
  label: string;
  title: string;
  detail: string;
  to: string;
  action: string;
}

function TodayTask({ label, title, detail, to, action }: TodayTaskData) {
  return (
    <li className="flex flex-col gap-3 py-3 first:pt-0 last:pb-0 sm:flex-row sm:items-center sm:justify-between">
      <div className="min-w-0 flex gap-3">
        <span aria-hidden="true" className="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-[var(--student-brand-soft)] text-[var(--student-brand-ink)]">
          <svg className="h-[18px] w-[18px]" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M8.5 19H17a3.5 3.5 0 0 0 0-7H7a3.5 3.5 0 0 1 0-7h8.5" /></svg>
        </span>
        <div className="min-w-0">
          <p className="text-xs font-semibold text-[var(--student-muted)]">{label}</p>
          <h3 className="mt-0.5 text-sm font-semibold text-[var(--student-ink)]">{title}</h3>
          <p className="mt-0.5 text-sm text-[var(--student-muted)]">{detail}</p>
        </div>
      </div>
      <Link to={to} className={`shrink-0 self-start whitespace-nowrap sm:self-auto ${SECONDARY_ACTION}`}>
        {action}
      </Link>
    </li>
  );
}

export const StudentDashboardPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const selectedSubjectId = searchParams.get("subjectId") || "";
  const selectedClassId = searchParams.get("classId") || "";
  const isHistory = searchParams.get("history") === "true";
  const isAllSubjects = !selectedSubjectId;

  // View mode for competency visualization: Atlas (default) or Radar
  const [competencyViewMode, setCompetencyViewMode] = useState<"atlas" | "radar">("atlas");

  const { data, isLoading, isError, error, refetch } = useQuery<StudentDashboardDataDto>({
    queryKey: ["studentDashboard", selectedSubjectId || "all", selectedClassId, isHistory],
    queryFn: () => getStudentDashboard(selectedSubjectId || undefined, selectedClassId || undefined, isHistory),
    staleTime: 60 * 1000,
  });

  const { data: assignmentsData } = useQuery({
    queryKey: ["studentAssignmentsToday", selectedSubjectId, selectedClassId, isHistory],
    queryFn: () => getStudentAssignments({
      subjectId: selectedSubjectId || undefined,
      classId: selectedClassId || undefined,
      history: isHistory,
      pageSize: 10,
    }),
    staleTime: 60 * 1000,
  });

  const { data: agenda, isPending: agendaLoading, isError: agendaError } = useQuery({
    queryKey: ["learning-agenda", "dashboard", selectedSubjectId, selectedClassId],
    queryFn: () => getLearningAgenda(selectedSubjectId, undefined, selectedClassId || undefined),
    enabled: !!selectedSubjectId && !isHistory,
    staleTime: 60 * 1000,
  });

  const { data: twinHistory } = useQuery<TwinUpdateHistoryItemDto[]>({
    queryKey: ["studentTwinHistory", selectedSubjectId, selectedClassId],
    queryFn: () => getStudentTwinHistory(selectedSubjectId, undefined, selectedClassId || undefined),
    enabled: !!selectedSubjectId && !isHistory,
    staleTime: 60 * 1000,
  });

  const { data: workspace } = useQuery({
    queryKey: ["student-workspace-summary", "dashboard", selectedSubjectId, selectedClassId, isHistory],
    queryFn: () => getStudentWorkspaceSummary(selectedSubjectId || undefined, selectedClassId || undefined, isHistory),
    staleTime: 60 * 1000,
  });

  const urgentAssignment = useMemo(() => {
    const list = assignmentsData?.data || [];
    // An overdue assignment can no longer be started or submitted: never push it as "do it now".
    const active = list.filter((a) => a.progress?.status !== "Completed" && a.progress?.status !== "Overdue" && !isAssignmentPastDue(a.dueAt));
    active.sort((a, b) => {
      if (!a.dueAt) return 1;
      if (!b.dueAt) return -1;
      return new Date(a.dueAt).getTime() - new Date(b.dueAt).getTime();
    });
    return active[0] || null;
  }, [assignmentsData?.data]);

  // Use the same current agenda as the learning-path page, not the retired path engine.
  const activeSession = agenda?.ongoingRuns.find(m => m.canStart && m.status === "InProgress") ?? null;
  const nextEligibleSession = !activeSession && agenda?.primaryMission?.canStart ? agenda.primaryMission : null;

  if (isLoading) {
    return (
      <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6 min-w-0">
        <div className="h-24 w-2/3 animate-pulse rounded-2xl bg-[var(--student-surface-subtle)]" />
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-5">
          <div className="lg:col-span-8 h-80 animate-pulse rounded-[20px] bg-[var(--student-surface)] border border-[var(--student-border)]" />
          <div className="lg:col-span-4 h-80 animate-pulse rounded-[20px] bg-[var(--student-surface)] border border-[var(--student-border)]" />
        </div>
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-12 min-w-0">
        <div className="max-w-xl mx-auto st-card p-8 text-center">
          <h2 className="text-base font-bold text-red-700 dark:text-red-400">
            Không thể tải dữ liệu học tập
          </h2>
          <p className="mt-2 text-xs text-stone-500 dark:text-stone-400">
            {(error as Error)?.message || "Vui lòng kiểm tra kết nối và thử lại."}
          </p>
          <button
            type="button"
            onClick={() => refetch()}
            className={`mt-4 ${PRIMARY_ACTION}`}
          >
            Tải lại trang
          </button>
        </div>
      </div>
    );
  }

  const { student, subject, goal, masteryRadar = [] } = data;
  if (data.academicContext?.message && data.academicContext.curriculums.length === 0) return (
    <div className="student-shell w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <h1 className="text-2xl font-bold">Hành trình học tập của {student.fullName}</h1>
      <div className="mt-6 rounded-2xl border border-[var(--student-border)] bg-[var(--student-surface)] p-6">
        <h2 className="text-lg font-semibold">{isHistory ? "Phạm vi lịch sử" : "Giáo trình đang học"}</h2>
        <p className="mt-3">{data.academicContext.message}</p>
        <p className="mt-2 text-sm">Bài làm, điểm và năng lực cũ vẫn được giữ nguyên. Không có biểu đồ toàn môn thay thế khi lớp chưa được gán giáo trình.</p>
        <Link className="mt-4 inline-block text-[var(--student-brand)] font-semibold" to={studentScopeUrl("/hoc-tap/bai-tap", searchParams)}>Xem bài tập của tôi →</Link>
      </div>
    </div>
  );
  const currentSubjectTheme = getSubjectTheme(subject.subjectName);

  // Radar chart data mapping
  const radarChartData = masteryRadar.map((item) => ({
    name: item.topicName,
    score: item.mastery,
    fullMark: 100,
    groupId: item.groupNodeId || item.topicNodeId,
    groupName: item.groupName || item.topicName,
    weight: item.examImportance ?? 1,
    evidenceCount: item.evidenceCount ?? 0,
  }));

  const goalDiff = goal.hasGoal && goal.hasPrediction !== false ? goalGapLabel(goal.targetScore, goal.currentPredictedScore) : "—";
  const currentMonth = new Date().getMonth() + 1;
  const currentSemester = currentMonth >= 8 || currentMonth === 1 ? "Học kỳ I" : "Học kỳ II";
  const pathLink = studentScopeUrl("/hoc-tap/lo-trinh-hoc-tap", searchParams);
  const recentChanges = (twinHistory ?? []).filter((change) => change.delta !== 0).slice(0, 5);
  const today = new Date().toDateString();
  const gainToday = recentChanges.find((change) => change.delta > 0 && new Date(change.recordedAt).toDateString() === today);
  const journeyTopics = masteryRadar.map((topic) => ({
    id: topic.topicNodeId,
    name: topic.topicName,
    mastery: topic.mastery,
    evidenceCount: topic.evidenceCount ?? 0,
  }));
  const masteredCount = journeyTopics.filter((topic) => topic.evidenceCount > 0 && topic.mastery >= 75).length;
  const streak = workspace?.dailyStreak ?? 0;
  const studiedToday = workspace?.studiedToday ?? false;

  const tasks: TodayTaskData[] = [];
  if (urgentAssignment) {
    tasks.push({
      label: urgentAssignment.dueAt
        ? `Bài tập của giáo viên · hạn ${new Date(urgentAssignment.dueAt).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })}`
        : "Bài tập của giáo viên",
      title: urgentAssignment.title,
      detail: `Đã làm ${urgentAssignment.progress?.completedQuestionCount || 0}/${urgentAssignment.progress?.totalQuestionCount || 0} câu`,
      to: studentScopeUrl(`/hoc-tap/bai-tap/${urgentAssignment.assignmentId}`, searchParams),
      action: "Làm bài tập ngay",
    });
  }
  if (activeSession) {
    tasks.push({ label: `Đang thực hiện · ${activeSession.estimatedDurationMinutes} phút`, title: activeSession.title, detail: activeSession.objective, to: pathLink, action: "Tiếp tục học ngay" });
  }
  if (nextEligibleSession) {
    tasks.push({ label: `Nhiệm vụ tiếp theo · ${nextEligibleSession.estimatedDurationMinutes} phút`, title: nextEligibleSession.title, detail: nextEligibleSession.objective, to: pathLink, action: "Mở nhiệm vụ trong lộ trình" });
  }
  if (!activeSession && !nextEligibleSession) {
    tasks.push({
      label: "Kế hoạch học tập cá nhân",
      title: selectedSubjectId ? "Chọn nhiệm vụ phù hợp" : "Chọn môn và nhiệm vụ học tập",
      detail: !selectedSubjectId ? "Mở lộ trình và chọn môn để xem các nhiệm vụ có thể thực hiện."
        : agendaLoading ? "Đang kiểm tra kế hoạch hôm nay…"
        : agendaError ? "Chưa tải được kế hoạch hôm nay. Mở lộ trình để thử lại."
        : agenda?.agendaMessage || "Thiết lập mục tiêu trong lộ trình để nhận nhiệm vụ phù hợp với bạn.",
      to: pathLink,
      action: "Xem lộ trình học tập",
    });
  }
  const [topTask, ...otherTasks] = tasks;

  const predicted = goal.hasGoal && goal.hasPrediction !== false ? goal.currentPredictedScore : null;
  const todayLabel = new Date().toLocaleDateString("vi-VN", { weekday: "long", day: "2-digit", month: "2-digit" });

  return (
    <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-5 space-y-4 min-w-0 student-shell">
      <section aria-label="Hành trình học tập" className="space-y-5">
        {/* Greeting + headline numbers */}
        <div className="flex flex-col gap-4 xl:flex-row xl:items-end xl:justify-between st-rise">
          <div className="min-w-0">
            <p className="st-eyebrow">{todayLabel} · {subject.subjectName}</p>
            <h1 className="mt-1.5 text-2xl font-extrabold tracking-tight sm:text-3xl">
              {dashboardGreeting(new Date().getHours())}, <span className="st-grad-text">{student.fullName}</span>
            </h1>
            <p className="mt-1.5 text-sm sm:text-base text-[var(--student-ink-secondary)]">
              {isHistory ? `${currentSemester} · Đang xem lịch sử học tập` : dashboardNudge({ streak, studiedToday, gainToday: gainToday ? { topicName: gainToday.topicName, delta: gainToday.delta } : null })}
            </p>
          </div>
          <dl className="grid grid-cols-3 sm:flex shrink-0 overflow-hidden rounded-[18px] border border-[var(--student-border)] bg-[var(--student-surface)] shadow-[var(--student-shadow)]">
            <div className="flex items-center gap-3 px-3 py-3 sm:px-5">
              <svg aria-hidden="true" className={`hidden sm:block h-6 w-6 shrink-0 ${studiedToday ? "text-[var(--student-brand-2)]" : "text-[var(--student-muted)]"}`} viewBox="0 0 24 24" fill="currentColor"><path d="M12 22c4 0 7-2.8 7-6.8 0-4.2-3.2-6.7-4.6-10.7-.3-.8-1.3-.9-1.7-.2-.9 1.6-1 3.4-2.4 4.6C9.6 7.3 9 6 8.6 5.2c-.3-.6-1.2-.6-1.5.1C5.9 7.9 5 10.4 5 15.2 5 19.2 8 22 12 22Z" /></svg>
              <div>
                <dd className="text-xl font-extrabold tabular-nums leading-tight [font-family:var(--student-font-display)]">{streak}</dd>
                <dt className="text-xs font-semibold text-[var(--student-muted)] sm:whitespace-nowrap">Chuỗi ngày học</dt>
              </div>
            </div>
            {journeyTopics.length > 0 && (
              <div className="px-3 py-3 sm:px-5 border-l border-[var(--student-border)]">
                <dd className="text-xl font-extrabold tabular-nums leading-tight [font-family:var(--student-font-display)]">{masteredCount}<span className="text-sm font-semibold text-[var(--student-muted)]">/{journeyTopics.length}</span></dd>
                <dt className="text-xs font-semibold text-[var(--student-muted)] sm:whitespace-nowrap">Đã vững</dt>
              </div>
            )}
            {predicted != null && (
              <div className="px-3 py-3 sm:px-5 border-l border-[var(--student-border)]">
                <dd className="text-xl font-extrabold tabular-nums leading-tight [font-family:var(--student-font-display)]">{predicted.toFixed(1)}<span className="text-sm font-semibold text-[var(--student-muted)]">/{goal.targetScore}</span></dd>
                <dt className="text-xs font-semibold text-[var(--student-muted)] sm:whitespace-nowrap">Dự đoán / mục tiêu</dt>
              </div>
            )}
          </dl>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-12 gap-4">
          {/* The one thing to do now */}
          <div className="lg:col-span-8 st-card st-spot st-hero p-5 sm:p-7 flex flex-col gap-5 st-rise" style={{ ["--d" as string]: 1 }}>
            {isHistory ? (
              <div>
                <p className="st-eyebrow">Lịch sử học tập</p>
                <h2 className="mt-2 text-xl sm:text-2xl font-extrabold">Phạm vi học tập đã lưu</h2>
                <p className="mt-2 max-w-[62ch] text-sm leading-relaxed text-[var(--student-ink-secondary)]">
                  Chỉ gồm chủ đề của các giáo trình từng áp dụng trong lớp đã chọn. Điểm năng lực phản ánh bằng chứng tích lũy hiện có, không phải bản chụp điểm tại một thời điểm trong quá khứ.
                </p>
                <Link to={studentScopeUrl("/hoc-tap/bai-tap", searchParams)} className={`mt-4 ${PRIMARY_ACTION}`}>
                  Xem bài làm trong phạm vi này {ARROW}
                </Link>
              </div>
            ) : topTask && (
              <div className="flex flex-col gap-4">
                <span className="inline-flex w-fit items-center gap-2 rounded-full bg-[var(--student-brand-soft)] px-3 py-1 text-xs font-bold text-[var(--student-brand-ink)]">
                  <span className="st-live" aria-hidden="true" />
                  {topTask.label}
                </span>
                <div>
                  <h2 className="text-xl sm:text-[1.75rem] font-extrabold leading-tight max-w-[26ch]">{topTask.title}</h2>
                  <p className="mt-2 max-w-[62ch] text-sm sm:text-base text-[var(--student-ink-secondary)]">{topTask.detail}</p>
                </div>
                <div className="flex flex-wrap items-center gap-3">
                  <Link to={topTask.to} className={PRIMARY_ACTION}>
                    {topTask.action}
                    {ARROW}
                  </Link>
                  {topTask.to !== pathLink && <Link to={pathLink} className={SECONDARY_ACTION}>Xem lộ trình</Link>}
                </div>
              </div>
            )}
            {journeyTopics.length > 0 && (
              <div className="mt-auto rounded-2xl border border-[var(--student-border)] bg-[color-mix(in_srgb,var(--student-surface)_80%,transparent)] p-3 sm:p-4">
                <p className="mb-1 text-xs font-bold text-[var(--student-muted)]">Hành trình chuyên đề</p>
                <StudentJourneyRoute topics={journeyTopics} subjectKey={`${selectedSubjectId}:${selectedClassId}`} />
              </div>
            )}
          </div>

          {/* Goal card: the one vermilion block on the page */}
          <section aria-labelledby="goal-heading" className="lg:col-span-4 relative overflow-hidden rounded-[20px] p-5 sm:p-6 text-white bg-[linear-gradient(150deg,var(--student-brand),var(--student-brand-2))] shadow-[var(--student-shadow)] flex flex-col gap-4 st-rise" style={{ ["--d" as string]: 2 }}>
            <span aria-hidden="true" className="pointer-events-none absolute -bottom-16 -right-10 h-56 w-56 rounded-full border-[36px] border-white/10" />
            <div className="flex items-baseline justify-between gap-3">
              <h2 id="goal-heading" className="text-base font-bold">Mục tiêu của bạn</h2>
              {goal.hasGoal && <span className="rounded-full bg-white/15 px-2.5 py-0.5 text-xs font-semibold tabular-nums">Còn {goal.remainingDays} ngày</span>}
            </div>

            {goal.hasGoal ? (
              <div>
                {goal.hasPrediction === false ? (
                  <div>
                    <p className="text-2xl font-extrabold [font-family:var(--student-font-display)]">Chưa có dự đoán</p>
                    <p className="mt-1 text-sm text-white/85">Mục tiêu {goal.targetScore}/10 · làm thêm bài để có ước lượng</p>
                  </div>
                ) : (
                  <div className="flex items-baseline gap-2">
                    <span className="text-5xl font-extrabold tabular-nums tracking-tight [font-family:var(--student-font-display)]">{goal.currentPredictedScore.toFixed(1)}</span>
                    <span className="text-sm font-semibold text-white/85">dự đoán · mục tiêu {goal.targetScore}</span>
                  </div>
                )}
                <div className="mt-3 h-2 rounded-full bg-white/20" role="img" aria-label={`Dự đoán ${goal.currentPredictedScore} trên 10, mục tiêu ${goal.targetScore}`}>
                  <div className="relative h-full rounded-full bg-white st-bar-fill" style={{ width: `${Math.min(100, Math.max(0, (goal.hasPrediction === false ? 0 : goal.currentPredictedScore) * 10))}%` }} />
                </div>
                <dl className="mt-4 space-y-2 text-sm">
                  <div className="flex items-center justify-between rounded-xl bg-white/12 px-3.5 py-2.5">
                    <dt className="text-white/90">Khoảng cách tới mục tiêu</dt>
                    <dd className="font-bold tabular-nums">{goal.hasPrediction === false ? "Chưa đủ dữ liệu" : goalDiff}</dd>
                  </div>
                  <div className="flex items-center justify-between rounded-xl bg-white/12 px-3.5 py-2.5">
                    <dt className="text-white/90">Nguy cơ chưa đạt mục tiêu</dt>
                    <dd className="font-bold tabular-nums">{goal.riskScore.toFixed(0)}%</dd>
                  </div>
                </dl>
              </div>
            ) : (
              <p className="text-sm leading-relaxed text-white/90">
                Bạn chưa đặt mục tiêu điểm cho môn này. Hãy tự đặt mục tiêu trong Hồ sơ năng lực.
              </p>
            )}

            <Link to={studentScopeUrl("/hoc-tap/ho-so-nang-luc", searchParams)} className="st-link group mt-auto inline-flex items-center gap-1.5 text-sm font-bold text-white">
              {goal.hasGoal ? "Xem hồ sơ chi tiết" : "Đặt mục tiêu"} {ARROW}
            </Link>
          </section>
        </div>
      </section>

      <div className="grid grid-cols-1 xl:grid-cols-[minmax(0,3fr)_minmax(0,2fr)] gap-4 items-start">
        <section aria-labelledby="competency-heading" className={`${PANEL} min-w-0 st-rise`} style={{ ["--d" as string]: 3 }}>
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div>
              <p className="st-eyebrow">{subject.subjectName}</p>
              <h2 id="competency-heading" className="mt-0.5 text-base font-bold text-[var(--student-ink)]">
                {isAllSubjects ? "Radar Năng Lực Theo Môn Học" : "Năng lực theo chuyên đề"}
              </h2>
            </div>
            <div className="inline-flex gap-0.5 self-start rounded-full bg-[var(--student-surface-subtle)] p-1 text-xs sm:self-auto">
              {([["atlas", "Danh sách"], ["radar", "Biểu đồ radar"]] as const).map(([mode, label]) => (
                <button
                  key={mode}
                  type="button"
                  aria-pressed={competencyViewMode === mode}
                  onClick={() => setCompetencyViewMode(mode)}
                  className={`rounded-full px-3.5 py-1.5 font-semibold cursor-pointer ${PRESS} ${
                    competencyViewMode === mode
                      ? "bg-[var(--student-surface)] text-[var(--student-ink)] shadow-[var(--student-shadow)]"
                      : "text-[var(--student-muted)] hover:text-[var(--student-ink)]"
                  }`}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>

          {competencyViewMode === "atlas" ? (
            <StudentCompetencyList data={radarChartData} themeColor={currentSubjectTheme.color} />
          ) : (
            <React.Suspense fallback={<div role="status" className="h-72 w-full flex items-center justify-center text-sm text-[var(--student-muted)]">Đang tải biểu đồ radar...</div>}>
              <StudentRadarChart data={radarChartData} themeColor={currentSubjectTheme.color} />
            </React.Suspense>
          )}
        </section>

        <div className="min-w-0 space-y-4">
          {otherTasks.length > 0 && (
            <section aria-labelledby="other-tasks-heading" className={`${PANEL} st-rise`} style={{ ["--d" as string]: 4 }}>
              <p className="st-eyebrow">Theo lộ trình</p>
              <h2 id="other-tasks-heading" className="mt-0.5 text-base font-bold text-[var(--student-ink)]">Việc tiếp theo</h2>
              <ul className="mt-3 divide-y divide-[var(--student-border)]">
                {otherTasks.map((task) => <TodayTask key={task.action + task.title} {...task} />)}
              </ul>
            </section>
          )}

          {!isHistory && (
            <section aria-labelledby="recent-heading" className={`${PANEL} min-w-0 st-rise`} style={{ ["--d" as string]: 5 }}>
              <h2 id="recent-heading" className="text-base font-bold text-[var(--student-ink)]">Thay đổi gần đây</h2>
              {recentChanges.length > 0 ? (
                <ul className="mt-3 space-y-2.5">
                  {recentChanges.map((change) => (
                    <li key={change.historyId} className="flex items-center gap-3 text-sm">
                      <span className={`w-16 shrink-0 rounded-xl py-1.5 text-center text-sm font-extrabold tabular-nums [font-family:var(--student-font-display)] ${change.delta > 0 ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300" : change.delta < 0 ? "bg-rose-50 text-rose-700 dark:bg-rose-950/60 dark:text-rose-300" : "bg-[var(--student-surface-subtle)] text-[var(--student-muted)]"}`}>
                        {change.delta > 0 ? "+" : ""}{change.delta.toFixed(1)}%
                      </span>
                      <div className="min-w-0">
                        <p className="font-semibold text-[var(--student-ink)] truncate">{change.topicName}</p>
                        <p className="truncate text-xs text-[var(--student-muted)]">
                          {change.learningContext === "Assignment" ? `Bài tập: ${change.contextLabel}` : change.contextLabel} · {new Date(change.recordedAt).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })}
                        </p>
                      </div>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="mt-2 text-sm text-[var(--student-muted)]">Chưa có thay đổi nào. Năng lực sẽ được ghi lại sau mỗi bài tập được chấm hoặc phiên luyện tập.</p>
              )}
            </section>
          )}
        </div>
      </div>

      {!!selectedSubjectId && !isHistory && (
        <section aria-labelledby="trend-heading" className={PANEL}>
          <h2 id="trend-heading" className="text-base font-bold text-[var(--student-ink)]">Diễn biến năng lực</h2>
          <React.Suspense fallback={<div role="status" className="h-64 w-full flex items-center justify-center text-sm text-[var(--student-muted)]">Đang tải biểu đồ diễn biến...</div>}>
            <StudentMasteryTrendChart history={twinHistory ?? []} />
          </React.Suspense>
        </section>
      )}
    </div>
  );
};
