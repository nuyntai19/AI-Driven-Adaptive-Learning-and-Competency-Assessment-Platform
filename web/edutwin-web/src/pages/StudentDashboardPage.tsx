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
import { StudentProgressTrack } from "../components/student/StudentProgressTrack";
import { StudentCompetencyList } from "../components/student/StudentCompetencyList";
import { StudentJourneyRoute } from "../components/student/StudentJourneyRoute";
import { studentScopeUrl } from "../utils/studentAcademicNavigation";
import { goalGapLabel } from "../utils/academicDisplay";
import { dashboardGreeting, dashboardNudge } from "../utils/studentDashboardNudge";
import { isAssignmentPastDue } from "../utils/assignmentReviewTiming";

const StudentRadarChart = React.lazy(() => import("../components/student/StudentRadarChart"));
const StudentMasteryTrendChart = React.lazy(() => import("../components/student/StudentMasteryTrendChart"));

const PANEL = "rounded-2xl border border-stone-200/90 bg-white p-4 sm:p-5 dark:border-stone-800/90 dark:bg-[#151d2f]";
const PRESS = "transition-[background-color,color,transform] duration-150 ease-out active:scale-[0.97] motion-reduce:transform-none";
const SECONDARY_ACTION = `rounded-lg border border-stone-300 px-3.5 py-2 text-sm font-semibold text-stone-800 hover:bg-stone-100 dark:border-stone-700 dark:text-stone-200 dark:hover:bg-stone-800 ${PRESS}`;

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
      <div className="min-w-0">
        <p className="text-xs font-semibold text-stone-500 dark:text-stone-400">{label}</p>
        <h3 className="mt-0.5 text-sm font-semibold text-stone-900 dark:text-stone-100">{title}</h3>
        <p className="mt-0.5 text-sm text-stone-500 dark:text-stone-400">{detail}</p>
      </div>
      <Link to={to} className={`shrink-0 self-start whitespace-nowrap sm:self-auto ${SECONDARY_ACTION}`}>
        {action}
      </Link>
    </li>
  );
}

function riskTone(risk: number): string {
  if (risk >= 70) return "text-rose-700 dark:text-rose-400";
  return risk >= 40 ? "text-amber-700 dark:text-amber-400" : "text-emerald-700 dark:text-emerald-400";
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
      <div className="w-full max-w-[1800px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6 min-w-0">
        <div className="h-44 animate-pulse rounded-2xl bg-white dark:bg-[#151d2f] border border-stone-200/70 dark:border-stone-800/70" />
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          <div className="lg:col-span-8 h-80 animate-pulse rounded-2xl bg-white dark:bg-[#151d2f] border border-stone-200/70 dark:border-stone-800/70" />
          <div className="lg:col-span-4 h-80 animate-pulse rounded-2xl bg-white dark:bg-[#151d2f] border border-stone-200/70 dark:border-stone-800/70" />
        </div>
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="w-full max-w-[1800px] mx-auto px-4 sm:px-6 lg:px-8 py-12 min-w-0">
        <div className="max-w-xl mx-auto rounded-2xl bg-white dark:bg-[#151d2f] border border-stone-200 dark:border-stone-800 p-8 text-center shadow-xs">
          <h2 className="text-base font-bold text-red-700 dark:text-red-400">
            Không thể tải dữ liệu học tập
          </h2>
          <p className="mt-2 text-xs text-stone-500 dark:text-stone-400">
            {(error as Error)?.message || "Vui lòng kiểm tra kết nối và thử lại."}
          </p>
          <button
            type="button"
            onClick={() => refetch()}
            className="mt-4 inline-flex items-center rounded-lg bg-stone-900 px-4 py-2 text-xs font-semibold text-white hover:bg-stone-800 dark:bg-stone-100 dark:text-stone-900 cursor-pointer"
          >
            Tải lại trang
          </button>
        </div>
      </div>
    );
  }

  const { student, subject, goal, masteryRadar = [] } = data;
  if (data.academicContext?.message && data.academicContext.curriculums.length === 0) return (
    <div className="student-shell w-full max-w-[1800px] mx-auto px-4 sm:px-6 lg:px-8 py-8">
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

  return (
    <div className="w-full max-w-[1800px] mx-auto px-4 sm:px-6 lg:px-8 py-5 space-y-4 min-w-0 student-shell">
      <section
        aria-label="Hành trình học tập"
        className="rounded-2xl bg-[#5b3fd1] p-5 text-white sm:p-6 dark:bg-[#3a2a8f]"
        style={{ backgroundImage: "radial-gradient(circle at 88% 0%, rgb(255 255 255 / 0.16), transparent 46%)" }}
      >
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div className="min-w-0">
            <h1 className="text-2xl font-bold tracking-tight sm:text-3xl">
              {dashboardGreeting(new Date().getHours())}, {student.fullName}
            </h1>
            <p className="mt-1 text-sm sm:text-base text-white/90">
              {isHistory ? `${currentSemester} · Đang xem lịch sử học tập` : dashboardNudge({ streak, studiedToday, gainToday: gainToday ? { topicName: gainToday.topicName, delta: gainToday.delta } : null })}
            </p>
          </div>
          <dl className="flex shrink-0 gap-6">
            <div>
              <dt className="text-xs text-white/85">Chuỗi ngày học</dt>
              <dd className="mt-0.5 flex items-center gap-1.5 text-2xl font-bold tabular-nums">
                <svg aria-hidden="true" className={`h-5 w-5 ${studiedToday ? "text-amber-300" : "text-white/60"}`} viewBox="0 0 24 24" fill="currentColor"><path d="M12 2c1 3.5 5.5 5.6 5.5 11a5.5 5.5 0 0 1-11 0c0-2 .9-3.5 2-4.6.2 1.5 1 2.6 2 3.1C10.300 8.500 10.800 5 12 2Z" /></svg>
                {streak}
              </dd>
            </div>
            {journeyTopics.length > 0 && (
              <div>
                <dt className="text-xs text-white/85">Đã vững</dt>
                <dd className="mt-0.5 text-2xl font-bold tabular-nums">{masteredCount}<span className="text-base font-medium text-white/85">/{journeyTopics.length}</span></dd>
              </div>
            )}
          </dl>
        </div>

        <div className="mt-4">
          <StudentJourneyRoute topics={journeyTopics} subjectKey={`${selectedSubjectId}:${selectedClassId}`} />
        </div>

        {isHistory ? (
          <div className="mt-4 rounded-xl bg-white/10 p-3.5 sm:p-4">
            <h2 className="text-sm font-semibold">Phạm vi học tập đã lưu</h2>
            <p className="mt-0.5 text-sm leading-relaxed text-white/90">
              Chỉ gồm chủ đề của các giáo trình từng áp dụng trong lớp đã chọn. Điểm năng lực phản ánh bằng chứng tích lũy hiện có, không phải bản chụp điểm tại một thời điểm trong quá khứ.
            </p>
            <Link to={studentScopeUrl("/hoc-tap/bai-tap", searchParams)} className="mt-2 inline-block text-sm font-semibold underline underline-offset-2">
              Xem bài làm trong phạm vi này
            </Link>
          </div>
        ) : topTask && (
          <div className="mt-4 flex flex-col gap-3 rounded-xl bg-white/10 p-3.5 sm:flex-row sm:items-center sm:justify-between sm:p-4">
            <div className="min-w-0">
              <p className="text-xs font-semibold text-white/85">{topTask.label}</p>
              <h2 className="mt-0.5 text-base font-bold">{topTask.title}</h2>
              <p className="mt-0.5 text-sm text-white/90">{topTask.detail}</p>
            </div>
            <Link to={topTask.to} className={`group inline-flex shrink-0 items-center gap-1.5 self-start whitespace-nowrap rounded-lg bg-white px-4 py-2.5 text-sm font-semibold text-[#4a31b8] hover:bg-white/90 sm:self-auto ${PRESS}`}>
              {topTask.action}
              <svg aria-hidden="true" className="h-4 w-4 transition-transform duration-150 ease-out group-hover:translate-x-0.5 motion-reduce:transform-none" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M5 12h14M13 6l6 6-6 6" /></svg>
            </Link>
          </div>
        )}
      </section>

      <div className="grid grid-cols-1 xl:grid-cols-[minmax(0,3fr)_minmax(0,2fr)] gap-4 items-start">
        <section aria-labelledby="competency-heading" className={`${PANEL} min-w-0`}>
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <h2 id="competency-heading" className="text-base font-bold text-stone-900 dark:text-stone-100">
              {isAllSubjects ? "Radar Năng Lực Theo Môn Học" : "Năng lực theo chuyên đề"}
            </h2>
            <div className="inline-flex gap-0.5 self-start rounded-lg bg-stone-100 p-0.5 text-xs dark:bg-stone-800/70 sm:self-auto">
              {([["atlas", "Danh sách"], ["radar", "Biểu đồ radar"]] as const).map(([mode, label]) => (
                <button
                  key={mode}
                  type="button"
                  aria-pressed={competencyViewMode === mode}
                  onClick={() => setCompetencyViewMode(mode)}
                  className={`rounded-md px-3 py-1.5 font-semibold cursor-pointer ${PRESS} ${
                    competencyViewMode === mode
                      ? "bg-white text-stone-900 shadow-xs dark:bg-stone-950 dark:text-stone-100"
                      : "text-stone-600 hover:text-stone-900 dark:text-stone-300 dark:hover:text-white"
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
            <React.Suspense fallback={<div role="status" className="h-72 w-full flex items-center justify-center text-sm text-stone-500">Đang tải biểu đồ radar...</div>}>
              <StudentRadarChart data={radarChartData} themeColor={currentSubjectTheme.color} />
            </React.Suspense>
          )}
        </section>

        <div className="min-w-0 space-y-4">
          <section aria-labelledby="goal-heading" className={PANEL}>
            <div className="flex items-baseline justify-between gap-3">
              <h2 id="goal-heading" className="text-base font-bold text-stone-900 dark:text-stone-100">Mục tiêu của bạn</h2>
              {goal.hasGoal && <span className="text-xs tabular-nums text-stone-500 dark:text-stone-400">Còn {goal.remainingDays} ngày</span>}
            </div>

            {goal.hasGoal ? (
              <div className="mt-3">
                <StudentProgressTrack
                  currentValue={goal.currentPredictedScore}
                  targetValue={goal.targetScore}
                  max={10}
                  currentLabel="Dự đoán"
                  targetLabel="Mục tiêu"
                  unit="/10"
                  color={currentSubjectTheme.color}
                />
                <dl className="mt-3 divide-y divide-stone-100 text-sm dark:divide-stone-800/80">
                  <div className="flex items-center justify-between py-2">
                    <dt className="text-stone-600 dark:text-stone-400">Khoảng cách tới mục tiêu</dt>
                    <dd className="font-semibold tabular-nums text-stone-900 dark:text-stone-100">
                      {goal.hasPrediction === false ? "Chưa đủ dữ liệu" : goalDiff}
                    </dd>
                  </div>
                  <div className="flex items-center justify-between py-2">
                    <dt className="text-stone-600 dark:text-stone-400">Nguy cơ chưa đạt mục tiêu</dt>
                    <dd className={`font-semibold tabular-nums ${riskTone(goal.riskScore)}`}>{goal.riskScore.toFixed(0)}%</dd>
                  </div>
                </dl>
              </div>
            ) : (
              <p className="mt-3 text-sm leading-relaxed text-stone-600 dark:text-stone-300">
                Bạn chưa đặt mục tiêu điểm cho môn này. Hãy tự đặt mục tiêu trong Hồ sơ năng lực.
              </p>
            )}

            <Link to={studentScopeUrl("/hoc-tap/ho-so-nang-luc", searchParams)} className="mt-1 inline-block text-sm font-semibold text-[var(--student-brand)] hover:underline">
              {goal.hasGoal ? "Xem hồ sơ chi tiết" : "Đặt mục tiêu"}
            </Link>
          </section>

          {otherTasks.length > 0 && (
            <section aria-labelledby="other-tasks-heading" className={PANEL}>
              <h2 id="other-tasks-heading" className="text-base font-bold text-stone-900 dark:text-stone-100">Việc tiếp theo</h2>
              <ul className="mt-3 divide-y divide-stone-100 dark:divide-stone-800/80">
                {otherTasks.map((task) => <TodayTask key={task.action + task.title} {...task} />)}
              </ul>
            </section>
          )}

          {!isHistory && (
            <section aria-labelledby="recent-heading" className={`${PANEL} min-w-0`}>
              <h2 id="recent-heading" className="text-base font-bold text-stone-900 dark:text-stone-100">Thay đổi gần đây</h2>
              {recentChanges.length > 0 ? (
                <ul className="mt-2 divide-y divide-stone-100 dark:divide-stone-800/80">
                  {recentChanges.map((change) => (
                    <li key={change.historyId} className="flex items-baseline justify-between gap-3 py-2.5 text-sm">
                      <div className="min-w-0">
                        <p className="font-semibold text-stone-900 dark:text-stone-100">{change.topicName}</p>
                        <p className="truncate text-xs text-stone-500 dark:text-stone-400">
                          {change.learningContext === "Assignment" ? `Bài tập: ${change.contextLabel}` : change.contextLabel} · {new Date(change.recordedAt).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })}
                        </p>
                      </div>
                      <span className={`shrink-0 text-sm font-semibold tabular-nums ${change.delta > 0 ? "text-emerald-700 dark:text-emerald-400" : change.delta < 0 ? "text-rose-700 dark:text-rose-400" : "text-stone-500 dark:text-stone-400"}`}>
                        {change.delta > 0 ? "+" : ""}{change.delta.toFixed(1)}%
                      </span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="mt-2 text-sm text-stone-500 dark:text-stone-400">Chưa có thay đổi nào. Năng lực sẽ được ghi lại sau mỗi bài tập được chấm hoặc phiên luyện tập.</p>
              )}
            </section>
          )}
        </div>
      </div>

      {!!selectedSubjectId && !isHistory && (
        <section aria-labelledby="trend-heading" className={PANEL}>
          <h2 id="trend-heading" className="text-base font-bold text-stone-900 dark:text-stone-100">Diễn biến năng lực</h2>
          <React.Suspense fallback={<div role="status" className="h-64 w-full flex items-center justify-center text-sm text-stone-500">Đang tải biểu đồ diễn biến...</div>}>
            <StudentMasteryTrendChart history={twinHistory ?? []} />
          </React.Suspense>
        </section>
      )}
    </div>
  );
};
