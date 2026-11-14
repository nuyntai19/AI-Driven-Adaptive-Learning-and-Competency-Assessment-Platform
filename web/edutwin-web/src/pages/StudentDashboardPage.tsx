import React, { useMemo } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { getStudentDashboard, getStudentWorkspaceSummary } from "../api/dashboardsApi";
import { getStudentAssignments } from "../api/assignmentsApi";
import { getLearningAgenda } from "../api/learningPathApi";
import { getStudentTwinHistory } from "../api/digitalTwinApi";
import type { StudentDashboardDataDto } from "../types/dashboards";
import type { TwinUpdateHistoryItemDto } from "../types/digitalTwin";
import type { AgendaItemDto } from "../types/learningPath";
import { StudentJourneyRoute } from "../components/student/StudentJourneyRoute";
import { DueCountdown } from "../components/student/DueCountdown";
import { studentScopeUrl } from "../utils/studentAcademicNavigation";
import { goalGapLabel } from "../utils/academicDisplay";
import { dashboardGreeting, dashboardNudge } from "../utils/studentDashboardNudge";
import { isAssignmentPastDue } from "../utils/assignmentReviewTiming";
import { formatDayMonth, formatTimeDayMonth } from "../utils/dateInput";

const CARD = "st-card st-spot p-5 sm:p-6";
const PRIMARY_ACTION = "st-btn-primary group inline-flex items-center gap-2 rounded-full px-5 py-3 text-sm font-bold whitespace-nowrap [font-family:var(--student-font-display)]";
const SECONDARY_ACTION = "inline-flex items-center rounded-full border border-[var(--student-border)] bg-[var(--student-surface)] px-4 py-2.5 text-sm font-semibold text-[var(--student-ink)] transition-colors hover:border-[var(--student-brand)] hover:text-[var(--student-brand)]";
const LINK = "st-link group inline-flex items-center gap-1.5 text-sm font-bold text-[var(--student-brand-ink)]";
const ARROW = <svg aria-hidden="true" className="st-arrow h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><path d="M5 12h14M13 6l6 6-6 6" /></svg>;
const EXAM_KIND: Record<string, string> = { Midterm: "Thi giữa kỳ", Final: "Thi cuối kỳ", MockExam: "Thi thử" };

interface TodayTaskData {
  label: string;
  title: string;
  detail: string;
  to: string;
  action: string;
}

function Icon({ d, className = "h-[18px] w-[18px]" }: { d: string; className?: string }) {
  return <svg aria-hidden="true" className={className} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d={d} /></svg>;
}

/** What a mission is, in the words a student uses on the plan list. */
function planKind(m: AgendaItemDto): [string, string] {
  if (m.title.startsWith("Rèn lại")) return ["Rèn lại", "bg-amber-50 text-amber-800 dark:bg-amber-950/60 dark:text-amber-200"];
  if (m.title.startsWith("Thử nhanh")) return ["Thử nhanh", "bg-[var(--student-brand-soft)] text-[var(--student-brand-ink)]"];
  if (m.purpose === "Review") return ["Đến hạn ôn", "bg-[var(--atlas-cobalt-soft)] text-[var(--atlas-cobalt)]"];
  if (m.purpose === "IndependentCheck") return ["Tự kiểm tra", "bg-emerald-50 text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-200"];
  if (m.purpose === "Study") return ["Học mới", "bg-[var(--atlas-cobalt-soft)] text-[var(--atlas-cobalt)]"];
  return ["Luyện tập", "bg-[var(--student-brand-soft)] text-[var(--student-brand-ink)]"];
}

export const StudentDashboardPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const selectedSubjectId = searchParams.get("subjectId") || "";
  const selectedClassId = searchParams.get("classId") || "";
  const isHistory = searchParams.get("history") === "true";
  const isAllSubjects = !selectedSubjectId;

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

  const activeAssignments = useMemo(() => {
    const list = assignmentsData?.data || [];
    // An overdue assignment can no longer be started or submitted: never push it as "do it now".
    const active = list.filter((a) => a.progress?.status !== "Completed" && a.progress?.status !== "Overdue" && !isAssignmentPastDue(a.dueAt));
    active.sort((a, b) => {
      if (!a.dueAt) return 1;
      if (!b.dueAt) return -1;
      return new Date(a.dueAt).getTime() - new Date(b.dueAt).getTime();
    });
    return active;
  }, [assignmentsData?.data]);
  const urgentAssignment = activeAssignments[0] || null;

  // Use the same current agenda as the learning-path page, not the retired path engine.
  const activeSession = agenda?.ongoingRuns.find(m => m.canStart && m.status === "InProgress") ?? null;
  const nextEligibleSession = !activeSession && agenda?.primaryMission?.canStart ? agenda.primaryMission : null;

  if (isLoading) {
    return (
      <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-5 min-w-0">
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
          <p className="mt-2 text-xs text-[var(--student-muted)]">
            {(error as Error)?.message || "Vui lòng kiểm tra kết nối và thử lại."}
          </p>
          <button type="button" onClick={() => refetch()} className={`mt-4 ${PRIMARY_ACTION}`}>
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
      <div className="mt-6 st-card p-6">
        <h2 className="text-lg font-semibold">{isHistory ? "Phạm vi lịch sử" : "Giáo trình đang học"}</h2>
        <p className="mt-3">{data.academicContext.message}</p>
        <p className="mt-2 text-sm">Bài làm, điểm và năng lực cũ vẫn được giữ nguyên. Không có biểu đồ toàn môn thay thế khi lớp chưa được gán giáo trình.</p>
        <Link className="mt-4 inline-block text-[var(--student-brand)] font-semibold" to={studentScopeUrl("/hoc-tap/bai-tap", searchParams)}>Xem bài tập của tôi →</Link>
      </div>
    </div>
  );

  const goalDiff = goal.hasGoal && goal.hasPrediction !== false ? goalGapLabel(goal.targetScore, goal.currentPredictedScore) : "—";
  const currentMonth = new Date().getMonth() + 1;
  const currentSemester = currentMonth >= 8 || currentMonth === 1 ? "Học kỳ I" : "Học kỳ II";
  const pathLink = studentScopeUrl("/hoc-tap/lo-trinh-hoc-tap", searchParams);
  const recentChanges = (twinHistory ?? []).filter((change) => change.delta !== 0).slice(0, 4);
  const today = new Date().toDateString();
  const gainToday = recentChanges.find((change) => change.delta > 0 && new Date(change.recordedAt).toDateString() === today);
  const journeyTopics = masteryRadar.map((topic) => ({
    id: topic.topicNodeId,
    name: topic.topicName,
    mastery: topic.mastery,
    evidenceCount: topic.evidenceCount ?? 0,
  }));
  const masteredCount = journeyTopics.filter((topic) => topic.evidenceCount > 0 && topic.mastery >= 75).length;
  const weakest = masteryRadar.filter((t) => (t.evidenceCount ?? 0) > 0).sort((a, b) => a.mastery - b.mastery).slice(0, 3);
  const streak = workspace?.dailyStreak ?? 0;
  const studiedToday = workspace?.studiedToday ?? false;
  const pendingAssignments = typeof workspace?.assignmentCount === "number" ? workspace.assignmentCount : null;
  const predicted = goal.hasGoal && goal.hasPrediction !== false ? goal.currentPredictedScore : null;
  const todayLabel = new Date().toLocaleDateString("vi-VN", { weekday: "long", day: "2-digit", month: "2-digit" });

  const tasks: TodayTaskData[] = [];
  if (urgentAssignment) {
    tasks.push({
      label: urgentAssignment.dueAt
        ? `Bài tập của giáo viên · hạn ${formatTimeDayMonth(urgentAssignment.dueAt)}`
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
  const heroIsAssignment = !!urgentAssignment && topTask?.title === urgentAssignment.title;
  const isExamAssignment = urgentAssignment?.kind === "Midterm" || urgentAssignment?.kind === "Final";
  // Exams must not reveal their per-skill question distribution before submission.
  const parts = heroIsAssignment && !isExamAssignment ? (urgentAssignment?.summary?.knowledgeAreas ?? []).slice(0, 3) : [];

  // "Sắp thi": the class's next scheduled exam, plus the next deadlines after the hero task.
  const nextExam = (agenda?.exams ?? [])
    .filter((e) => e.status === "Scheduled" && new Date(e.examAt).getTime() > Date.now())
    .sort((a, b) => new Date(a.examAt).getTime() - new Date(b.examAt).getTime())[0];
  const upcoming = activeAssignments.filter((a) => a.dueAt && a.assignmentId !== (heroIsAssignment ? urgentAssignment?.assignmentId : "")).slice(0, 2);
  const daysUntil = (iso: string) => Math.max(0, Math.ceil((new Date(iso).getTime() - Date.now()) / 86400000));

  const plan = selectedSubjectId && !isHistory ? (agenda?.todayPlan ?? (agenda?.primaryMission ? [agenda.primaryMission, ...(agenda.alternativeOptions ?? [])] : [])) : [];
  const planDone = plan.filter((m) => m.status === "Completed").length;
  const planMinutes = plan.reduce((n, m) => n + (m.estimatedDurationMinutes || 0), 0);

  return (
    <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-5 space-y-4 min-w-0 student-shell">
      <section aria-label="Hành trình học tập" className="space-y-5">
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
          <dl className="grid grid-cols-2 sm:flex shrink-0 overflow-hidden rounded-[18px] border border-[var(--student-border)] bg-[var(--student-surface)] shadow-[var(--student-shadow)] [&>div]:min-w-0 [&>div]:px-4 [&>div]:py-3 sm:[&>div]:px-5 [&>div+div]:border-l [&>div]:border-[var(--student-border)]">
            <div>
              <dd className="text-xl font-extrabold tabular-nums leading-tight [font-family:var(--student-font-display)]">{streak}</dd>
              <dt className="text-xs font-semibold text-[var(--student-muted)]">ngày liên tiếp</dt>
            </div>
            {journeyTopics.length > 0 && (
              <div>
                <dd className="text-xl font-extrabold tabular-nums leading-tight [font-family:var(--student-font-display)]">{masteredCount}<span className="text-sm font-semibold text-[var(--student-muted)]">/{journeyTopics.length}</span></dd>
                <dt className="text-xs font-semibold text-[var(--student-muted)]">{isAllSubjects ? "môn đã vững" : "chuyên đề đã vững"}</dt>
              </div>
            )}
            {predicted != null && (
              <div>
                <dd className="text-xl font-extrabold tabular-nums leading-tight [font-family:var(--student-font-display)]">{predicted.toFixed(1)}<span className="text-sm font-semibold text-[var(--student-muted)]">/{goal.targetScore}</span></dd>
                <dt className="text-xs font-semibold text-[var(--student-muted)]">dự đoán / mục tiêu</dt>
              </div>
            )}
            {pendingAssignments != null && (
              <div>
                <dd className="text-xl font-extrabold tabular-nums leading-tight [font-family:var(--student-font-display)]">{pendingAssignments}</dd>
                <dt className="text-xs font-semibold text-[var(--student-muted)]">bài tập đang chờ</dt>
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
              <>
                <div className="flex flex-wrap items-center gap-2">
                  <span className="inline-flex items-center gap-2 rounded-full bg-[var(--student-brand-soft)] px-3 py-1 text-xs font-bold text-[var(--student-brand-ink)]">
                    <span className="st-live" aria-hidden="true" />
                    {topTask.label}
                  </span>
                  {heroIsAssignment && urgentAssignment?.dueAt && (
                    <span className="inline-flex items-center gap-1.5 rounded-full border border-[var(--student-border)] bg-[var(--student-surface)] px-3 py-1 text-xs font-bold text-[var(--student-ink-secondary)]">
                      <Icon d="M12 7v5l3 2M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" className="h-3.5 w-3.5" />
                      Hạn còn <DueCountdown dueAt={urgentAssignment.dueAt} />
                    </span>
                  )}
                </div>
                <div>
                  <h2 className="text-xl sm:text-[1.75rem] font-extrabold leading-tight max-w-[28ch]">{topTask.title}</h2>
                  <p className="mt-2 max-w-[62ch] text-sm sm:text-base text-[var(--student-ink-secondary)]">
                    {topTask.detail}{heroIsAssignment && urgentAssignment?.summary?.estimatedMinutes != null ? ` · khoảng ${urgentAssignment.summary.estimatedMinutes} phút` : ""}
                  </p>
                </div>
                {parts.length > 0 && (
                  <ol className="flex flex-wrap items-stretch gap-2" aria-label="Nội dung bài">
                    {parts.map((area, i) => (
                      <li key={area.skillId} className="flex items-center gap-2">
                        {i > 0 && <span aria-hidden="true" className="hidden sm:inline text-[var(--student-muted)]">›</span>}
                        <span className="flex min-w-[7.5rem] flex-col items-center gap-0.5 rounded-2xl border border-[var(--student-border)] bg-[color-mix(in_srgb,var(--student-surface)_75%,transparent)] px-3.5 py-2.5 text-center transition-[transform,border-color] duration-200 hover:-translate-y-0.5 hover:border-[var(--student-brand)]">
                          <b className="text-sm [font-family:var(--student-font-display)]">{area.skillName}</b>
                          <span className="text-xs font-semibold text-[var(--student-muted)]">{area.questionCount} câu</span>
                        </span>
                      </li>
                    ))}
                  </ol>
                )}
                <div className="flex flex-wrap items-center gap-3">
                  <Link to={topTask.to} className={PRIMARY_ACTION}>
                    {topTask.action}
                    {ARROW}
                  </Link>
                  {topTask.to !== pathLink && <Link to={pathLink} className={SECONDARY_ACTION}>Xem lộ trình</Link>}
                </div>
                {otherTasks.length > 0 && (
                  <ul className="mt-auto space-y-2 border-t border-[var(--student-border)] pt-4" aria-label="Việc tiếp theo">
                    {otherTasks.map((task) => (
                      <li key={task.action + task.title} className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                        <div className="min-w-0">
                          <p className="text-xs font-semibold text-[var(--student-muted)]">{task.label}</p>
                          <p className="text-sm font-semibold break-words">{task.title}</p>
                        </div>
                        <Link to={task.to} className={`${SECONDARY_ACTION} shrink-0 self-start sm:self-auto`}>{task.action}</Link>
                      </li>
                    ))}
                  </ul>
                )}
              </>
            )}
          </div>

          {/* "Sắp thi": the one vermilion block on the page */}
          <section aria-labelledby="upcoming-heading" className="lg:col-span-4 relative overflow-hidden rounded-[20px] p-5 sm:p-6 text-white bg-[linear-gradient(150deg,var(--student-brand),var(--student-brand-2))] shadow-[var(--student-shadow)] flex flex-col gap-4 st-rise" style={{ ["--d" as string]: 2 }}>
            <span aria-hidden="true" className="pointer-events-none absolute -bottom-16 -right-10 h-56 w-56 rounded-full border-[36px] border-white/10" />
            <div className="flex items-center gap-2.5">
              <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-white/15"><Icon d="M5 21V4M5 4h12l-2 4 2 4H5" /></span>
              <span className="text-xs font-bold uppercase tracking-[0.12em] text-white/85">{nextExam ? "Sắp thi" : "Sắp đến hạn"}</span>
            </div>
            {nextExam ? (
              <div>
                <h2 id="upcoming-heading" className="text-lg font-bold">{EXAM_KIND[nextExam.kind] ?? "Bài thi của lớp"} · {nextExam.title}</h2>
                <p className="mt-2 flex items-baseline gap-2"><span className="text-5xl font-extrabold tabular-nums tracking-tight [font-family:var(--student-font-display)]">{daysUntil(nextExam.examAt)}</span><span className="font-bold">ngày nữa</span></p>
                <p className="mt-1 text-sm text-white/90">{new Date(nextExam.examAt).toLocaleString("vi-VN", { weekday: "long", day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })} · {nextExam.timeLimitMinutes} phút</p>
              </div>
            ) : upcoming.length === 0 ? (
              <div>
                <h2 id="upcoming-heading" className="text-lg font-bold">Chưa có lịch thi hay hạn nộp sắp tới</h2>
                <p className="mt-2 text-sm text-white/90">Lịch thi do giáo viên đặt và các hạn nộp bài tập sẽ hiện ở đây.</p>
              </div>
            ) : (
              <h2 id="upcoming-heading" className="text-lg font-bold">Các hạn nộp gần nhất</h2>
            )}
            {upcoming.length > 0 && (
              <ul className="space-y-2">
                {upcoming.map((a) => (
                  <li key={a.assignmentId}>
                    <Link to={studentScopeUrl(`/hoc-tap/bai-tap/${a.assignmentId}`, searchParams)} className="flex items-center justify-between gap-3 rounded-xl bg-white/12 px-3.5 py-2.5 text-sm font-semibold transition-colors hover:bg-white/20">
                      <span className="min-w-0 break-words">{a.title}</span>
                      <span className="shrink-0 rounded-full bg-white px-2.5 py-0.5 text-xs font-bold text-[#A81700]">{formatDayMonth(a.dueAt as string)}</span>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
            <Link to={nextExam ? pathLink : studentScopeUrl("/hoc-tap/bai-tap", searchParams)} className="st-link group mt-auto inline-flex items-center gap-1.5 text-sm font-bold text-white">
              {nextExam ? "Xem lịch ôn thi" : "Xem tất cả bài tập"} {ARROW}
            </Link>
          </section>

          {/* Today's plan from the learning path: read-only, each row opens the path */}
          <section aria-labelledby="plan-heading" className={`lg:col-span-6 xl:col-span-5 ${CARD} st-rise`} style={{ ["--d" as string]: 3 }}>
            <div className="mb-3 flex items-start justify-between gap-3">
              <div><p className="st-eyebrow">Theo lộ trình</p><h2 id="plan-heading" className="mt-0.5 text-base font-bold">Kế hoạch hôm nay</h2></div>
              {plan.length > 0 && <span className="shrink-0 rounded-full border border-[var(--student-border)] px-2.5 py-1 text-xs font-bold text-[var(--student-ink-secondary)]">{planDone}/{plan.length} việc · {planMinutes} phút</span>}
            </div>
            {plan.length > 0 ? (
              <ul className="divide-y divide-[var(--student-border)]">
                {plan.slice(0, 5).map((m) => {
                  const done = m.status === "Completed";
                  const now = !done && (!!m.activeRunId || m.missionId === plan.find((x) => x.status !== "Completed")?.missionId);
                  const [kind, kindClass] = planKind(m);
                  return (
                    <li key={m.missionId}>
                      <Link to={pathLink} className="grid grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-3 py-2.5">
                        <span className={`flex h-[26px] w-[26px] items-center justify-center rounded-full border-2 ${done ? "border-emerald-600 bg-emerald-600 text-white" : now ? "border-[var(--student-brand)] shadow-[0_0_0_4px_var(--student-brand-soft)]" : "border-[var(--student-border)]"}`}>
                          {done && <Icon d="m5 12 5 5 9-10" className="h-3.5 w-3.5" />}
                        </span>
                        <span className="min-w-0">
                          <span className={`block break-words text-sm font-semibold ${done ? "text-[var(--student-muted)] line-through" : ""}`}>{m.title}</span>
                          <span className="mt-0.5 flex flex-wrap items-center gap-2 text-xs text-[var(--student-muted)]">
                            <span className={`rounded-full px-2 py-0.5 font-bold ${kindClass}`}>{kind}</span>
                            {m.questionCount > 0 ? `${m.questionCount} câu · ` : ""}{m.estimatedDurationMinutes} phút
                          </span>
                        </span>
                        <span className={`text-xs font-bold ${done ? "text-emerald-700 dark:text-emerald-300" : "text-[var(--student-brand-ink)]"}`}>{done ? "Xong" : m.activeRunId ? "Tiếp tục" : "Mở"}</span>
                      </Link>
                    </li>
                  );
                })}
              </ul>
            ) : (
              <p className="text-sm text-[var(--student-muted)]">
                {!selectedSubjectId || isHistory ? "Chọn một môn đang học để xem kế hoạch hôm nay."
                  : agendaLoading ? "Đang kiểm tra kế hoạch hôm nay…"
                  : agendaError ? "Chưa tải được kế hoạch hôm nay."
                  : agenda?.agendaMessage || "Chưa có nhiệm vụ cho hôm nay."}
              </p>
            )}
          </section>

          {/* Weakest topics with a direct way to practise them */}
          <section aria-labelledby="focus-heading" className={`lg:col-span-6 xl:col-span-4 ${CARD} st-rise`} style={{ ["--d" as string]: 4 }}>
            <p className="st-eyebrow">Ưu tiên luyện</p>
            <h2 id="focus-heading" className="mt-0.5 mb-3 text-base font-bold">Cần chú ý</h2>
            {weakest.length > 0 ? (
              <ul className="space-y-3.5">
                {weakest.map((t) => {
                  const tone = t.mastery >= 75 ? "var(--atlas-mint)" : t.mastery >= 50 ? "var(--atlas-sun)" : "var(--student-brand)";
                  return (
                    <li key={t.topicNodeId} className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-x-3 gap-y-1.5">
                      <span className="min-w-0 break-words text-sm font-semibold">{t.topicName}</span>
                      <span className="text-sm font-bold tabular-nums">{t.mastery.toFixed(0)}%</span>
                      <span className="col-span-2 h-1.5 overflow-hidden rounded-full bg-[var(--student-surface-subtle)]"><span className="block h-full rounded-full st-bar-fill" style={{ width: `${t.mastery}%`, background: tone }} /></span>
                      {!isAllSubjects && !isHistory && <Link to={`${studentScopeUrl("/hoc-tap/luyen-tap", searchParams)}&topicId=${t.topicNodeId}`} className={`col-span-2 ${LINK} text-xs`}>Luyện 10 phút {ARROW}</Link>}
                    </li>
                  );
                })}
              </ul>
            ) : (
              <p className="text-sm text-[var(--student-muted)]">Chưa có chuyên đề nào có bằng chứng. Làm bài kiểm tra đầu vào trong lộ trình để biết phần nào cần ưu tiên.</p>
            )}
          </section>

          {/* Goal */}
          <section aria-labelledby="goal-heading" className={`lg:col-span-12 xl:col-span-3 ${CARD} st-rise`} style={{ ["--d" as string]: 5 }}>
            <div className="mb-2 flex items-center justify-between gap-2">
              <h2 id="goal-heading" className="text-base font-bold">Mục tiêu của bạn</h2>
              {goal.hasGoal && <span className="rounded-full bg-[var(--student-brand-soft)] px-2.5 py-0.5 text-xs font-bold text-[var(--student-brand-ink)]">{goal.targetScore}</span>}
            </div>
            {goal.hasGoal ? (
              <div>
                {goal.hasPrediction === false
                  ? <p className="text-xl font-extrabold [font-family:var(--student-font-display)]">Chưa có dự đoán</p>
                  : <p className="flex items-baseline gap-1.5"><span className="text-4xl font-extrabold tabular-nums tracking-tight [font-family:var(--student-font-display)]">{goal.currentPredictedScore.toFixed(1)}</span><span className="text-sm text-[var(--student-muted)]">dự đoán</span></p>}
                <div className="relative mt-3 h-2 rounded-full bg-[var(--student-surface-subtle)]" role="img" aria-label={`Dự đoán ${goal.currentPredictedScore} trên 10, mục tiêu ${goal.targetScore}`}>
                  <span className="absolute inset-y-0 left-0 rounded-full bg-[linear-gradient(90deg,var(--atlas-sun),var(--student-brand-2))] st-bar-fill" style={{ width: `${goal.hasPrediction === false ? 0 : Math.min(100, goal.currentPredictedScore * 10)}%` }} />
                  <span className="absolute -top-1.5 h-5 w-[3px] rounded bg-[var(--student-ink)]" style={{ left: `${Math.min(100, goal.targetScore * 10)}%` }} />
                </div>
                <p className="mt-3 text-xs text-[var(--student-ink-secondary)]">
                  {goal.hasPrediction === false ? "Làm thêm bài để có ước lượng." : <>Khoảng cách: <b>{goalDiff}</b></>} · nguy cơ chưa đạt <b>{goal.riskScore.toFixed(0)}%</b>{goal.remainingDays != null ? ` · còn ${goal.remainingDays} ngày` : ""}
                </p>
              </div>
            ) : (
              <p className="text-sm leading-relaxed text-[var(--student-ink-secondary)]">
                Bạn chưa đặt mục tiêu điểm cho môn này. Hãy tự đặt mục tiêu trong Hồ sơ năng lực.
              </p>
            )}
            <Link to={studentScopeUrl("/hoc-tap/ho-so-nang-luc", searchParams)} className={`mt-3 ${LINK}`}>
              {goal.hasGoal ? "Xem hồ sơ chi tiết" : "Đặt mục tiêu"} {ARROW}
            </Link>
          </section>
        </div>
      </section>

      <div className="grid grid-cols-1 xl:grid-cols-12 gap-4 items-start">
        <section aria-labelledby="journey-heading" className={`min-w-0 xl:col-span-8 ${CARD} st-rise`} style={{ ["--d" as string]: 6 }}>
          <div className="mb-2 flex items-start justify-between gap-3">
            <div><p className="st-eyebrow">{subject.subjectName}</p><h2 id="journey-heading" className="mt-0.5 text-base font-bold">{isAllSubjects ? "Năng lực theo môn học" : "Hành trình chuyên đề"}</h2></div>
            {journeyTopics.length > 0 && <span className="shrink-0 rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-bold text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300">{masteredCount}/{journeyTopics.length} đã vững</span>}
          </div>
          {journeyTopics.length > 0
            ? <StudentJourneyRoute topics={journeyTopics} subjectKey={`${selectedSubjectId}:${selectedClassId}`} />
            : <p className="text-sm text-[var(--student-muted)]">Chưa có chuyên đề trong giáo trình của lớp.</p>}
          <Link to={studentScopeUrl("/hoc-tap/ho-so-nang-luc", searchParams)} className={`mt-3 ${LINK}`}>Chi tiết năng lực {ARROW}</Link>
        </section>

        {!isHistory && (
          <section aria-labelledby="recent-heading" className={`xl:col-span-4 ${CARD} min-w-0 st-rise`} style={{ ["--d" as string]: 7 }}>
            <h2 id="recent-heading" className="mb-3 text-base font-bold">Vừa tiến bộ</h2>
            {recentChanges.length > 0 ? (
              <ul className="space-y-2.5">
                {recentChanges.map((change) => (
                  <li key={change.historyId} className="flex items-center gap-3 text-sm">
                    <span className={`w-16 shrink-0 rounded-xl py-1.5 text-center text-sm font-extrabold tabular-nums [font-family:var(--student-font-display)] ${change.delta > 0 ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300" : "bg-rose-50 text-rose-700 dark:bg-rose-950/60 dark:text-rose-300"}`}>
                      {change.delta > 0 ? "+" : ""}{change.delta.toFixed(1)}%
                    </span>
                    <div className="min-w-0">
                      <p className="break-words font-semibold">{change.topicName}</p>
                      <p className="break-words text-xs text-[var(--student-muted)]">
                        {change.learningContext === "Assignment" ? `Bài tập: ${change.contextLabel}` : change.contextLabel} · {formatTimeDayMonth(change.recordedAt)}
                      </p>
                    </div>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-sm text-[var(--student-muted)]">Chưa có thay đổi nào. Năng lực sẽ được ghi lại sau mỗi bài tập được chấm hoặc phiên luyện tập.</p>
            )}
          </section>
        )}
      </div>
    </div>
  );
};
