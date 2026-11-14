import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { organizationApi } from "../api/organizationApi";
import { getCapabilityMap, getLearningAgenda } from "../api/learningPathApi";
import { useStudentLearningAccess } from "../hooks/useStudentLearningAccess";
import { useAuthStore } from "../stores/authStore";
import { StudentLearningReadOnlyNotice } from "../components/student/StudentLearningReadOnlyNotice";
import { GoalAndProposalModal } from "../components/learningPathV4/GoalAndProposalModal";
import { MissionPlayerModal } from "../components/learningPathV4/MissionPlayerModal";
import { TimeBudgetSlider } from "../components/learningPathV4/TimeBudgetSlider";
import { formatStudyMinutes, splitReason } from "../utils/studyTime";
import { formatDayMonth } from "../utils/dateInput";
import type { AgendaItemDto, LearningAgendaDto, MissionRunSummaryDto } from "../types/learningPath";
import { learningPathTimeline } from "../utils/learningPathTimeline";
import { missionAction } from "../utils/learningMission";
import { PlacementReport } from "../components/learningPathV4/PlacementReport";
import { ExtendCheckModal } from "../components/learningPathV4/ExtendCheckModal";
import type { SubjectDto } from "../types/organization";

const activityLabels: Record<string, string> = { Study: "Tìm hiểu và ví dụ", Practice: "Luyện có hướng dẫn",
  IndependentCheck: "Tự kiểm tra", WorkedExample: "Ví dụ từng bước", FindError: "Tìm lỗi", Explore: "Khám phá",
  Explain: "Giải thích", CompleteSolution: "Hoàn thiện lời giải", TransferChallenge: "Vận dụng", Review: "Ôn nhớ lại" };
const weaknessLabels: Record<string, [string, string]> = {
  Weak: ["Phần yếu · học nhiều hơn", "bg-rose-50 text-rose-800 dark:bg-rose-950/40 dark:text-rose-200"],
  Developing: ["Cần củng cố", "bg-amber-50 text-amber-800 dark:bg-amber-950/40 dark:text-amber-200"],
  Strong: ["Khá vững · luyện ngắn", "bg-emerald-50 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-200"] };
const runStatusLabels: Record<string, string> = { InProgress: "Đang làm", Grading: "Đang chấm", NeedsReview: "Chờ duyệt", Completed: "Đã xong", Abandoned: "Đã hủy" };
const outcomeLabels: Record<string, string> = { Passed: "đạt", NotPassed: "chưa đạt", InsufficientEvidence: "chưa đủ bằng chứng" };
function runSummary(r: MissionRunSummaryDto): string {
  const parts = [`Lượt ${r.runNumber}`, new Date(r.completedAt ?? r.startedAt).toLocaleDateString("vi-VN"), runStatusLabels[r.status] ?? r.status];
  if (outcomeLabels[r.outcome]) parts.push(outcomeLabels[r.outcome]);
  if (r.totalQuestions > 0) parts.push(`${r.correctAnswers}/${r.answeredQuestions} câu đúng${r.answeredQuestions < r.totalQuestions ? ` (đã làm ${r.answeredQuestions}/${r.totalQuestions})` : ""}`);
  return parts.join(" · ");
}
function sizeLabel(m: AgendaItemDto): string {
  if (m.purpose !== "Practice" && m.purpose !== "Review") return "";
  const sets = m.plannedSets ?? 1;
  return sets > 1 ? `${sets} bộ × ${m.questionCount} câu · ` : `${m.questionCount} câu · `;
}
function Reason({ text, className = "" }: { text: string; className?: string }) {
  const { lead, rest } = splitReason(text);
  return <div className={className}><p>{lead}</p>{rest && <details className="mt-1"><summary className="cursor-pointer text-indigo-600">Vì sao?</summary><p className="mt-1">{rest}</p></details>}</div>;
}
type PathView = "route" | "today" | "map" | "history";
const isRepairMission = (m: AgendaItemDto) => m.title.startsWith("Rèn lại");
const CALENDAR_ICON = "M7 3v3m10-3v3M4 9h16M5 5h14a1 1 0 0 1 1 1v13a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1Z";
const purposeIcon: Record<string, string> = {
  Probe: "M13 2 4 14h7l-1 8 9-12h-7Z",
  Study: "M4 4.5A1.5 1.5 0 0 1 5.5 3H20v15H5.5A1.5 1.5 0 0 0 4 19.5ZM4 19.5A1.5 1.5 0 0 0 5.5 21H20",
  Practice: "M6 7v10M18 7v10M3 10v4M21 10v4M6 12h12",
  IndependentCheck: "M12 3 4 6v6c0 4.5 3.4 8.3 8 9 4.6-.7 8-4.5 8-9V6ZM9 12l2 2 4-4",
  Review: "M20 11a8 8 0 0 0-14.9-3.9L3 9M3 4v5h5M4 13a8 8 0 0 0 14.9 3.9L21 15M21 20v-5h-5",
};
function PathIcon({ d }: { d: string }) {
  return <svg aria-hidden="true" className="h-[18px] w-[18px]" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round"><path d={d} /></svg>;
}
const stateLabels: Record<string, string> = { Unknown: "Chưa đủ bằng chứng", Developing: "Cần củng cố",
  Demonstrated: "Đã có bằng chứng ban đầu", Confirmed: "Đã xác nhận", ReviewDue: "Cần ôn lại" };
export function StudentLearningPathPage() {
  const [params, setParams] = useSearchParams();
  const actor = useAuthStore(s => s.user);
  const subjects = useQuery({ queryKey: ["subjects", "student-learning-path", actor?.centerId, actor?.userId],
    queryFn: () => organizationApi.listSubjects(true) });
  const storedSubjectId = (() => { try { return localStorage.getItem("edutwin_last_subject_id"); } catch { return null; } })();
  const subject = subjects.data?.data.find(s => s.subjectId === params.get("subjectId"))
    ?? subjects.data?.data.find(s => s.subjectId === storedSubjectId)
    ?? subjects.data?.data[0];
  // Commit the resolved subject to the shared scope (URL + localStorage) so
  // the subject picked on the dashboard stays the same on this page.
  useEffect(() => {
    if (!subject) return;
    try { localStorage.setItem("edutwin_last_subject_id", subject.subjectId); } catch { /* ignore */ }
    if (params.get("subjectId") === subject.subjectId) return;
    const next = new URLSearchParams(params);
    next.set("subjectId", subject.subjectId);
    for (const key of ["classId", "pathId", "sessionId"]) next.delete(key);
    setParams(next, { replace: true });
  }, [subject, params, setParams]);
  if (subjects.isError) return <div role="alert" className="p-6">Không thể tải môn học. <button className="underline" onClick={() => void subjects.refetch()}>Thử lại</button></div>;
  if (subjects.isPending || (subject && params.get("subjectId") !== subject.subjectId)) return <p role="status" className="p-6">Đang tải môn học…</p>;
  if (!subject) return <p className="p-6">Chưa có môn học khả dụng. Hãy liên hệ giáo viên để được bổ sung môn học.</p>;
  return <LearningPathWorkspace key={`${subject.subjectId}:${params.get("classId")}:${params.get("history")}`} subject={subject} />;
}

function LearningPathWorkspace({ subject }: { subject: SubjectDto }) {
  const actor = useAuthStore(s => s.user);
  const subjectId = subject.subjectId;
  const access = useStudentLearningAccess(subjectId);
  const [sliderMinutes, setSliderMinutes] = useState(15);
  // 0 asks the server for the goal's own minutes/day, so the first plan already matches the slider.
  const [minutes, setMinutes] = useState(0);
  const sliderTouched = useRef(false);
  const changeSlider = (value: number) => { sliderTouched.current = true; setSliderMinutes(value); };
  // The slider updates its readout immediately; the plan is recalculated once dragging pauses.
  useEffect(() => {
    if (!sliderTouched.current) return;
    const timer = setTimeout(() => setMinutes(sliderMinutes), 350);
    return () => clearTimeout(timer);
  }, [sliderMinutes]);
  const [showGoal, setShowGoal] = useState(false);
  const [entryMode, setEntryMode] = useState<"Start" | "Placement" | "Foundation" | "Update" | "Next" | "FromPlacement" | undefined>();
  const [initialScope, setInitialScope] = useState<string[]>([]);
  const [selfAssessed, setSelfAssessed] = useState<string[]>([]);
  const openSetup = (mode?: typeof entryMode, scope: string[] = [], assessed: string[] = []) => {
    setEntryMode(mode); setInitialScope(scope); setSelfAssessed(assessed); setShowGoal(true);
  };
  // Placement runs as one test: after each section closes, the next untested section opens automatically.
  const placementFlow = useRef(false);
  // Whether a section was actually opened in this run of the test; never jump to a route without one.
  const placementOpened = useRef(false);
  const [placementRunning, setPlacementRunning] = useState(false);
  const setFlow = (on: boolean) => { placementFlow.current = on; setPlacementRunning(on); };
  // "Kiểm tra thêm phần khác": checks added to the current route, taken one after another.
  const [showExtend, setShowExtend] = useState(false);
  const extraQueue = useRef<string[]>([]);
  const [mission, setMission] = useState<AgendaItemDto | null>(null);
  // Set while a run of an older route is open read-only from "Lịch sử tuyến học".
  const [historyContext, setHistoryContext] = useState<string | null>(null);
  // Between two parts of a multi-part check the route is reloaded (2-5 s) before the next part opens; a full-screen
  // waiting screen covers it instead of flashing the route page.
  const [transitionNote, setTransitionNote] = useState<string | null>(null);
  const [acceptedNotice, setAcceptedNotice] = useState("");
  // The page is split into views so a student never scrolls past the whole route to reach one part.
  const [view, setView] = useState<PathView | null>(null);
  const [routeKey, setRouteKey] = useState<string>();
  const map = useQuery({ queryKey: ["learning-paths", "capability-map", actor?.centerId, actor?.userId, subjectId, access.classId],
    queryFn: () => getCapabilityMap(subjectId, access.classId), enabled: !!subjectId && access.writable });
  const agenda = useQuery({ queryKey: ["learning-agenda", actor?.centerId, actor?.userId, subjectId, access.classId, minutes],
    queryFn: () => getLearningAgenda(subjectId, minutes, access.classId), enabled: !!subjectId && access.writable, placeholderData: keepPreviousData });
  // Seed the time slider from the goal's minutes/day once per path — the
  // default 15 must not mask a 5-minute/day goal.
  const goalSeededFor = useRef<string | null>(null);
  useEffect(() => {
    const lpId = agenda.data?.learningPathId;
    const perDay = agenda.data?.availableMinutesPerDay;
    if (!lpId || !perDay || perDay <= 0 || sliderTouched.current) return;
    const seed = `${lpId}:${perDay}`;
    if (goalSeededFor.current === seed) return;
    goalSeededFor.current = seed;
    setSliderMinutes(perDay);
  }, [agenda.data?.learningPathId, agenda.data?.availableMinutesPerDay]);
  const missions = agenda.data?.missions ?? [];
  // Proposal from a finished placement: tested parts become the scope; untested parts are self-assessed.
  const openFromPlacement = (data: LearningAgendaDto) => {
    const tested = [...new Set(data.missions.map(m => m.skillId))];
    const assessed = data.selfAssessedSkillIds ?? [];
    openSetup("FromPlacement", tested, assessed);
  };
  const nextPlacementSection = (data?: LearningAgendaDto) => data?.isPlacement
    ? data.missions.find(m => m.canStart && m.status !== "Completed" && !m.activeRunId && !m.latestRunId) : undefined;
  const advancePlacement = (data?: LearningAgendaDto) => {
    const next = nextPlacementSection(data);
    if (next) { placementOpened.current = true; setMission({ ...next, reviewOnly: false }); return; }
    setFlow(false);
    if (!placementOpened.current) {
      if (data?.placementReport?.isComplete) { openFromPlacement(data); return; }
      setAcceptedNotice("Các phần còn lại chưa đủ câu mới để kiểm tra. Em vẫn có thể xem tuyến tạm từ dữ liệu hiện có.");
      return;
    }
    if (data?.placementReport?.isComplete && data.ongoingRuns.length === 0) openFromPlacement(data);
  };
  const placementSections = agenda.data?.isPlacement ? missions : [];
  // Parts outside the route's learning scope (untested, self-assessed or self-reported) that can still be tested.
  const routeSkills = new Set(missions.filter(m => !m.isSelfAssessedCheck && !m.title.startsWith("Kiểm tra thêm")).map(m => m.skillId));
  const extendCandidates = agenda.data?.learningPathId && !agenda.data.isPlacement ? (map.data?.skills ?? [])
    .filter(s => s.state !== "Confirmed" && !routeSkills.has(s.skillId) && !missions.some(m => m.skillId === s.skillId && m.title.startsWith("Kiểm tra thêm") && m.status !== "Skipped"))
    .map(s => ({ ...s, checkableQuestions: agenda.data?.checkableQuestionsBySkill?.[s.skillId], note: agenda.data?.selfAssessedSkillIds?.includes(s.skillId) ? "Tự đánh giá đã vững · thay cho bài ôn xác nhận"
      : agenda.data?.selfReportedPrerequisiteSkillIds?.includes(s.skillId) ? "Nền tảng em đã tự xác nhận bỏ qua" : "Chưa có trong lộ trình" })) : [];
  const openNextExtra = (data?: LearningAgendaDto) => {
    while (extraQueue.current.length) {
      const next = data?.missions.find(m => m.missionId === extraQueue.current[0]);
      if (next && next.canStart && !next.latestRunId) { setMission({ ...next, reviewOnly: false }); return; }
      extraQueue.current.shift();
    }
  };
  const sectionLabel = (m: AgendaItemDto) => {
    const index = placementSections.findIndex(x => x.missionId === m.missionId);
    return index >= 0 ? `Kiểm tra đầu vào · phần ${index + 1}/${placementSections.length}` : undefined;
  };
  const primary = agenda.data?.primaryMission;
  const todayPlan = agenda.data?.todayPlan ?? (primary ? [primary, ...(agenda.data?.alternativeOptions ?? [])] : []);
  const planSummary = agenda.data?.learningPathId
    ? todayPlan.length === 0 ? "Chưa có nhiệm vụ vừa với thời gian này."
      // Count the rows the student sees below; practice sets are named separately (one row may hold several sets).
      : `Kế hoạch: ${todayPlan.length} nhiệm vụ${todayPlan.some(m => (m.plannedSets ?? 1) > 1) ? ` (${todayPlan.reduce((n, m) => n + (m.purpose === "Practice" || m.purpose === "Review" ? m.plannedSets ?? 1 : 0), 0)} bộ luyện)` : ""} · khoảng ${formatStudyMinutes(agenda.data.plannedMinutes ?? 0)}${agenda.data.plannedQuestionCount ? ` · ≈ ${agenda.data.plannedQuestionCount} câu` : ""}${agenda.isPlaceholderData ? " · đang tính lại…" : ""}`
    : undefined;
  const panel = "st-card st-spot p-5 sm:p-6";
  const action = "st-btn-primary inline-flex items-center justify-center rounded-full px-5 py-2.5 text-sm font-bold whitespace-nowrap disabled:cursor-not-allowed [font-family:var(--student-font-display)]";
  const secondary = "inline-flex items-center justify-center rounded-full border border-[var(--student-border)] bg-[var(--student-surface)] px-4 py-2.5 text-sm font-semibold whitespace-nowrap transition-colors hover:border-[var(--student-brand)] hover:text-[var(--student-brand)] disabled:opacity-50";
  const hasPath = !!agenda.data?.learningPathId;
  const isPlacement = !!agenda.data?.isPlacement;
  const timeline = isPlacement ? [] : learningPathTimeline(missions, agenda.data?.exams ?? [], agenda.data?.schedule?.timeZoneId ?? "Asia/Ho_Chi_Minh");
  const stageDone = (group: AgendaItemDto[]) => group.every(m => m.status === "Completed" || m.status === "Skipped");
  const stageLive = (group: AgendaItemDto[]) => group.some(m => !!m.activeRunId || (m.canStart && m.status !== "Completed" && m.status !== "Skipped"));
  const mapSkills = map.data?.skills ?? [];
  const untestedSkills = mapSkills.filter(s => s.state === "Unknown");
  const placementDone = placementSections.filter(m => m.status === "Completed").length;
  const defaultRouteKey = isPlacement ? "place"
    : (timeline.find(e => e.kind === "stage" && stageLive(e.missions)) ?? timeline.find(e => e.kind === "stage" && !stageDone(e.missions)))?.key ?? "place";
  const activeRouteKey = routeKey === "place" || timeline.some(e => e.key === routeKey) ? routeKey! : defaultRouteKey;
  const activeView: PathView = view ?? (hasPath ? "route" : "today");
  const views: [PathView, string, number | undefined][] = [
    ["route", isPlacement ? "Kiểm tra đầu vào" : "Lộ trình", isPlacement ? placementSections.length || undefined : timeline.filter(e => e.kind === "stage").length || undefined],
    ["today", "Hôm nay", (agenda.data?.todayPlan?.length ?? 0) || undefined],
    ["map", "Bản đồ năng lực", mapSkills.length || undefined],
    ...((agenda.data?.pathHistory?.length ?? 0) > 0 ? [["history", "Lịch sử tuyến", agenda.data?.pathHistory?.length] as [PathView, string, number | undefined]] : []),
  ];
  const stageNumber = (key: string) => timeline.filter(e => e.kind === "stage").findIndex(e => e.key === key) + 1;
  const missionKind = (m: AgendaItemDto) => m.isSelfAssessedCheck ? "Ôn xác nhận" : m.title.startsWith("Thử nhanh") ? "Thử nhanh"
    : isRepairMission(m) ? "Rèn lại" : activityLabels[m.purpose] ?? m.purpose;
  const missionState = (m: AgendaItemDto): "ok" | "fail" | "skip" | "now" | "open" | "wait" | "lock" => {
    const last = m.runs?.length ? m.runs[m.runs.length - 1] : undefined;
    if (m.activeRunId) return "now";
    if (m.status === "Skipped") return "skip";
    if (m.status === "Completed") return m.purpose === "IndependentCheck" && last?.outcome === "NotPassed" ? "fail" : "ok";
    // A failed self-check waiting for its repair set reads as "not passed", not as a fresh task.
    if (m.purpose === "IndependentCheck" && last?.outcome === "NotPassed" && !m.canRetry) return "fail";
    if (m.canStart && !m.deferred) return "open";
    // A spaced review booked for a later day is scheduled, not blocked.
    if (!m.deferred && m.availableFrom && new Date(m.availableFrom).getTime() > Date.now()) return "wait";
    return "lock";
  };
  const shortDate = formatDayMonth;
  const nextOpening = (group: AgendaItemDto[]) => group.filter(m => missionState(m) === "wait").map(m => m.availableFrom!).sort()[0];
  const stepDot: Record<string, string> = {
    ok: "bg-emerald-600 text-white", fail: "border-2 border-rose-500 bg-rose-50 text-rose-700 dark:bg-rose-950/50 dark:text-rose-300",
    skip: "bg-[var(--student-surface-subtle)] text-[var(--student-muted)]", now: "st-node-active text-white bg-[linear-gradient(135deg,var(--student-brand),var(--student-brand-2))] shadow-[var(--student-glow)]",
    open: "border-2 border-[var(--student-brand)] bg-[var(--student-surface)] text-[var(--student-brand)]",
    wait: "border border-dashed border-[var(--student-border)] bg-[var(--student-surface)] text-[var(--student-ink-secondary)]", lock: "bg-[var(--student-surface-subtle)] text-[var(--student-muted)]",
  };
  const renderStep = (m: AgendaItemDto, last: boolean, repairHead: boolean) => {
    const st = missionState(m);
    const repair = isRepairMission(m);
    const icon = st === "ok" ? "m5 12 5 5 9-10" : st === "fail" ? "M6 6l12 12M18 6 6 18" : st === "lock" ? "M7 11V7a5 5 0 0 1 10 0v4M5 11h14v10H5Z"
      : st === "wait" ? CALENDAR_ICON : purposeIcon[m.title.startsWith("Thử nhanh") ? "Probe" : m.purpose] ?? purposeIcon.Practice;
    const enabled = !(m.deferred || (!m.canStart && !m.activeRunId && !m.latestRunId));
    return <li key={m.missionId} className={repair ? "ml-5 border-l-2 border-dashed border-amber-400 pl-4" : ""}>
      {repairHead && <span className="mb-1 inline-flex items-center gap-1.5 rounded-full bg-amber-100 px-3 py-0.5 text-xs font-bold text-amber-800 dark:bg-amber-900/50 dark:text-amber-200">Nhánh rèn lại · ưu tiên câu từng sai</span>}
      <div className="relative grid grid-cols-[2.25rem_minmax(0,1fr)] gap-x-3 py-3 sm:grid-cols-[2.25rem_minmax(0,1fr)_auto]">
        {!last && !repair && <span aria-hidden="true" className={`absolute left-[1.06rem] top-[3.4rem] bottom-[-0.75rem] w-0.5 ${st === "ok" ? "bg-emerald-500" : "bg-[var(--student-border)]"}`} />}
        <span className={`relative z-[1] flex h-9 w-9 items-center justify-center rounded-full ${stepDot[st]}`}><PathIcon d={icon} /></span>
        <div className="min-w-0">
          <p className="text-[11px] font-bold uppercase tracking-wider text-[var(--student-muted)]">{missionKind(m)}{m.plannedDate ? ` · Dự kiến ${m.plannedDate.split("-").reverse().join("/")}` : ""}</p>
          <h4 className="text-[15px] font-bold leading-snug">{m.title}</h4>
          <p className="mt-0.5 text-xs text-[var(--student-muted)]">
            {m.questionCount > 0 ? `${m.setNumber ? `Bộ ${m.setNumber}${m.estimatedSets ? ` (dự kiến ${m.estimatedSets} bộ)` : ""} · ` : ""}${m.questionCount} câu · khoảng ${m.questionCount * 2 > 0 && (m.purpose === "Practice" || m.purpose === "Review") ? m.questionCount * 2 : m.estimatedDurationMinutes} phút` : `khoảng ${m.estimatedDurationMinutes} phút`}
            {m.availableFrom ? ` · hẹn ôn ${new Date(m.availableFrom).toLocaleDateString("vi-VN")}` : ""}
          </p>
          {st === "fail" && <span className="mt-1.5 inline-block rounded-full bg-rose-50 px-2.5 py-0.5 text-xs font-bold text-rose-700 dark:bg-rose-950/50 dark:text-rose-300">Chưa đạt</span>}
          {st === "skip" && <span className="mt-1.5 inline-block rounded-full bg-[var(--student-surface-subtle)] px-2.5 py-0.5 text-xs font-bold text-[var(--student-muted)]">Đã bỏ qua</span>}
          {m.skipReason === "MissedStudyDate" && <p className="mt-1.5 text-xs text-amber-700 dark:text-amber-300">Đã bỏ lượt dự kiến; phần chưa học được xếp lại trong lộ trình.</p>}
          {!!m.remainingUnscheduledMinutes && <p className="mt-1.5 text-xs text-amber-700 dark:text-amber-300">Có phần có thể học ngay; còn khoảng {m.remainingUnscheduledMinutes} phút chưa xếp được trước ngày đích.</p>}
          {m.advice && <p className="mt-1.5 text-xs text-amber-700 dark:text-amber-300">{m.advice}</p>}
          {m.recommendationReason && <details className="mt-1.5 text-xs text-[var(--student-ink-secondary)]"><summary className="cursor-pointer font-semibold text-[var(--student-brand-ink)]">Vì sao?</summary><Reason className="mt-1" text={m.recommendationReason} /></details>}
          {!m.activeRunId && !m.canRetry && m.retryBlockedReason && <p className="mt-1.5 text-xs text-[var(--student-muted)]">{m.retryBlockedReason}</p>}
          {(m.runs?.length ?? 0) > 0 && <details className="mt-1.5 text-xs"><summary className="cursor-pointer font-semibold text-[var(--student-brand-ink)]">Lịch sử {m.runs!.length} lượt</summary>
            <ul className="mt-2 space-y-1">{m.runs!.map(r => <li key={r.sessionRunId} className="flex items-center justify-between gap-2">
              <span className="text-[var(--student-ink-secondary)]">{runSummary(r)}</span>
              {r.status !== "InProgress" && <button className="shrink-0 font-semibold underline" onClick={() => setMission({ ...m, latestRunId: r.sessionRunId, reviewOnly: true, retry: false })}>Xem</button>}
            </li>)}</ul></details>}
        </div>
        <div className="col-start-2 mt-2 flex flex-wrap gap-2 sm:col-start-3 sm:row-start-1 sm:mt-0 sm:flex-col sm:items-stretch">
          <button className={st === "ok" || st === "fail" || st === "skip" ? secondary : action} disabled={!enabled} onClick={() => setMission({ ...m, reviewOnly: missionAction(m).reviewOnly })}>
            {m.deferred ? "Đã hoãn · cần điều chỉnh mục tiêu" : st === "wait" ? `Mở ngày ${shortDate(m.availableFrom!)}` : missionAction(m).label}</button>
          {!m.activeRunId && m.canRetry && <button className={secondary} onClick={() => setMission({ ...m, retry: true, reviewOnly: false })}>{m.retryLabel ?? "Làm lại"}</button>}
        </div>
      </div>
    </li>;
  };
  if (access.pending) return <p role="status" className="p-6">Đang kiểm tra môn học và giáo trình…</p>;
  if (access.readOnly) return <StudentLearningReadOnlyNotice reason={access.reason} />;
  return <div className="mx-auto w-full max-w-[1440px] space-y-5 px-4 py-6 sm:px-6 lg:px-8 text-[var(--student-ink)]">
    {acceptedNotice && <div role="status" className="flex items-start justify-between gap-3 rounded-2xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-900 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-100"><p>{acceptedNotice}</p><button aria-label="Đóng thông báo đã chọn tuyến" onClick={() => setAcceptedNotice("")}>×</button></div>}
    <header className="flex flex-wrap items-end justify-between gap-4 st-rise">
      <div className="min-w-0"><p className="st-eyebrow">Lộ trình cá nhân · {subject.subjectName}</p>
        <h1 className="mt-1.5 text-2xl sm:text-3xl font-extrabold">{agenda.data?.goalTitle ? agenda.data.goalTitle : <>Lộ trình học tập <span className="st-grad-text">cá nhân</span></>}</h1>
        <p className="mt-1.5 text-sm text-[var(--student-ink-secondary)]">Học theo mục tiêu, luyện phần còn yếu và theo dõi tiến bộ qua từng chặng.</p>
        {hasPath && !isPlacement && <div className="mt-3 flex flex-wrap gap-2 text-xs font-bold">
          {agenda.data?.scheduleSummary && <span className="rounded-full border border-[var(--student-border)] px-3 py-1 text-[var(--student-ink-secondary)]">{agenda.data.scheduleSummary.phase === "MockExam" ? "Giai đoạn thi thử và vá lỗ hổng" : agenda.data.scheduleSummary.phase === "ShortChecks" ? "Giai đoạn tăng kiểm tra ngắn" : "Học, luyện và ôn cân bằng"}</span>}
          {agenda.data?.targetScore != null && <span className="rounded-full bg-[var(--student-brand-soft)] px-3 py-1 text-[var(--student-brand-ink)]">Mục tiêu {agenda.data.targetScore}/10</span>}
          {agenda.data?.deadlineDate && <span className="rounded-full bg-[var(--student-brand-soft)] px-3 py-1 text-[var(--student-brand-ink)]">Ngày đích {agenda.data.deadlineDate.split("-").reverse().join("/")}</span>}
          {(agenda.data?.scheduleSummary?.missingMinutes ?? 0) > 0 && <span className="rounded-full bg-amber-50 px-3 py-1 text-amber-800 dark:bg-amber-950/50 dark:text-amber-200">Thiếu thời gian so với mục tiêu</span>}
        </div>}</div>
      <div className="flex flex-wrap items-center gap-3">
        {!isPlacement && agenda.data?.progress && agenda.data.progress.totalSkills > 0 && <div className="flex items-center gap-3">
          <span className="relative h-14 w-14 shrink-0">
            <svg viewBox="0 0 56 56" className="h-full w-full -rotate-90" aria-hidden="true"><circle cx="28" cy="28" r="23" fill="none" strokeWidth="6" className="stroke-[var(--student-surface-subtle)]" /><circle cx="28" cy="28" r="23" fill="none" strokeWidth="6" strokeLinecap="round" className="stroke-[var(--student-brand)] st-ring" strokeDasharray="144.5" strokeDashoffset={144.5 * (1 - agenda.data.progress.confirmedSkills / agenda.data.progress.totalSkills)} /></svg>
            <b className="absolute inset-0 flex items-center justify-center text-sm font-extrabold [font-family:var(--student-font-display)]">{agenda.data.progress.confirmedSkills}/{agenda.data.progress.totalSkills}</b>
          </span>
          <span className="text-sm"><b className="block">kỹ năng đã vững</b><span className="text-xs text-[var(--student-muted)]">{agenda.data.progress.estimatedFinishDate ? `Dự kiến xong ${new Date(agenda.data.progress.estimatedFinishDate).toLocaleDateString("vi-VN")}` : agenda.data.progress.remainingMinutes > 0 ? `Còn khoảng ${formatStudyMinutes(agenda.data.progress.remainingMinutes)}` : "Đã xong các phần trong tuyến"}</span></span>
        </div>}
        {hasPath && !isPlacement && <button className={secondary} onClick={() => openSetup("Update", agenda.data?.targetSkillIds)} disabled={!map.data?.skills.length}>Điều chỉnh mục tiêu</button>}
        {extendCandidates.length > 0 && <button className={secondary} onClick={() => setShowExtend(true)}>Kiểm tra thêm phần khác</button>}
      </div>
    </header>
    {(map.error || agenda.error) && <div role="alert" className="rounded-2xl border border-rose-200 bg-rose-50 p-4 text-rose-800 dark:border-rose-900 dark:bg-rose-950/40 dark:text-rose-200">
      Không thể tải lộ trình. {((map.error ?? agenda.error) as any)?.response?.data?.detail ?? (map.error ?? agenda.error)?.message}
      <button className="ml-3 underline" onClick={() => { void map.refetch(); void agenda.refetch(); }}>Thử lại</button>
    </div>}
    {agenda.data && !agenda.data.learningPathId && <section className={`${panel} st-hero`} aria-labelledby="starting-point-title">
      <p className="st-eyebrow">Bắt đầu học cùng EduTwin</p>
      <h2 id="starting-point-title" className="mt-2 text-2xl font-extrabold">Chưa biết bắt đầu từ đâu?</h2>
      <p className="mt-3 text-sm text-slate-500">Chọn phần muốn học, sau đó kiểm tra đầu vào để tìm điểm xuất phát hoặc bỏ qua để học ngay.</p>
      <div className="mt-5"><button className={action} disabled={!map.data?.skills.length} onClick={() => openSetup("Start")}>Bắt đầu lộ trình</button></div>
      {!map.data?.skills.length && <p className="mt-3 text-sm text-amber-700">Môn học chưa có kỹ năng trong giáo trình. Cần giáo viên bổ sung để bắt đầu.</p>}
    </section>}
    {hasPath && <div className="sticky top-16 z-30 -mx-4 px-4 sm:-mx-6 sm:px-6 lg:-mx-8 lg:px-8 py-2 bg-[color-mix(in_srgb,var(--student-canvas)_94%,transparent)] backdrop-blur-xl">
      <nav className="flex w-fit max-w-full gap-1 overflow-x-auto rounded-full border border-[var(--student-border)] bg-[var(--student-surface)] p-1 shadow-[var(--student-shadow)]" aria-label="Các phần của lộ trình" role="tablist">
        {views.map(([key, label, count]) => <button key={key} type="button" role="tab" aria-selected={activeView === key} onClick={() => setView(key)}
          className={`flex shrink-0 items-center gap-2 rounded-full px-4 py-2 text-sm font-semibold whitespace-nowrap transition-colors ${activeView === key ? "st-nav-active" : "text-[var(--student-ink-secondary)] hover:bg-[var(--student-surface-subtle)] hover:text-[var(--student-ink)]"}`}>
          {label}{count != null && <span className={`rounded-full px-1.5 text-[11px] font-bold leading-5 ${activeView === key ? "bg-white/25" : "bg-[var(--student-surface-subtle)]"}`}>{count}</span>}
        </button>)}
      </nav>
    </div>}
    {hasPath && <div hidden={activeView !== "route"} className="grid gap-5 lg:grid-cols-[minmax(0,380px)_minmax(0,1fr)] lg:items-start">
      <ol className="space-y-2.5" aria-label="Các chặng của lộ trình">
        <li>
          <button type="button" aria-pressed={activeRouteKey === "place"} onClick={() => setRouteKey("place")}
            className={`grid w-full grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-3 rounded-[18px] border bg-[var(--student-surface)] p-3.5 text-left shadow-[var(--student-shadow)] transition-[border-color,transform] duration-200 hover:translate-x-0.5 ${activeRouteKey === "place" ? "border-[var(--student-brand)] ring-[3px] ring-[var(--student-brand-soft)]" : "border-[var(--student-border)]"}`}>
            <span className="flex h-10 w-10 items-center justify-center rounded-full border-2 border-[var(--atlas-cobalt)] bg-[var(--atlas-cobalt-soft)] text-[var(--atlas-cobalt)]"><PathIcon d={purposeIcon.Probe} /></span>
            <span className="min-w-0"><b className="block text-[15px]">Kiểm tra đầu vào</b>
              <small className="block text-xs text-[var(--student-muted)]">{isPlacement ? `${placementDone}/${placementSections.length} phần đã kiểm tra` : `${mapSkills.length - untestedSkills.length}/${mapSkills.length} phần đã có kết quả`}</small></span>
            {!isPlacement && untestedSkills.length > 0 ? <span className="rounded-full bg-[var(--atlas-cobalt-soft)] px-2.5 py-0.5 text-xs font-bold text-[var(--atlas-cobalt)] whitespace-nowrap">{untestedSkills.length} chưa kiểm tra</span>
              : isPlacement ? <span className="rounded-full bg-[var(--student-brand-soft)] px-2.5 py-0.5 text-xs font-bold text-[var(--student-brand-ink)] whitespace-nowrap">Đang làm</span> : <span />}
          </button>
        </li>
        {timeline.map(entry => {
          const on = entry.key === activeRouteKey;
          const base = `grid w-full grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-3 rounded-[18px] border bg-[var(--student-surface)] p-3.5 text-left shadow-[var(--student-shadow)] transition-[border-color,transform] duration-200 hover:translate-x-0.5 ${on ? "border-[var(--student-brand)] ring-[3px] ring-[var(--student-brand-soft)]" : "border-[var(--student-border)]"}`;
          if (entry.kind === "exam") return <li key={entry.key}><button type="button" aria-pressed={on} onClick={() => setRouteKey(entry.key)} className={base}>
            <span className="flex h-10 w-10 items-center justify-center rounded-full border-2 border-amber-400 bg-amber-50 text-amber-700 dark:bg-amber-950/50 dark:text-amber-300"><PathIcon d="M5 21V4M5 4h12l-2 4 2 4H5" /></span>
            <span className="min-w-0"><b className="block line-clamp-2 break-words text-[15px] leading-snug">{entry.exam.title}</b><small className="block text-xs text-[var(--student-muted)]">{({Midterm: "Thi giữa kỳ", Final: "Thi cuối kỳ", MockExam: "Thi thử"} as Record<string, string>)[entry.exam.kind] ?? "Bài thi của lớp"} · {new Date(entry.exam.examAt).toLocaleDateString("vi-VN")}</small></span><span />
          </button></li>;
          const group = entry.missions;
          const done = stageDone(group), live = stageLive(group), fixing = group.some(m => isRepairMission(m) && m.status !== "Completed");
          const opensOn = !done && !live ? nextOpening(group) : undefined;
          const waiting = !!opensOn && group.every(m => m.status === "Completed" || m.status === "Skipped" || missionState(m) === "wait");
          const solid = mapSkills.find(s => s.skillId === group[0].skillId)?.state === "Confirmed";
          const completed = group.filter(m => m.status === "Completed").length;
          return <li key={entry.key}><button type="button" aria-pressed={on} onClick={() => setRouteKey(entry.key)} className={base}>
            <span className={`flex h-10 w-10 items-center justify-center rounded-full text-sm font-extrabold [font-family:var(--student-font-display)] ${done ? "bg-emerald-600 text-white" : live ? "text-white bg-[linear-gradient(135deg,var(--student-brand),var(--student-brand-2))] shadow-[var(--student-glow)]" : waiting && solid ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300" : waiting ? "border border-dashed border-[var(--student-border)] bg-[var(--student-surface)] text-[var(--student-ink-secondary)]" : "bg-[var(--student-surface-subtle)] text-[var(--student-muted)]"}`}>
              {done || (waiting && solid) ? <PathIcon d="m5 12 5 5 9-10" /> : live ? stageNumber(entry.key) : waiting ? <PathIcon d={CALENDAR_ICON} /> : <PathIcon d="M7 11V7a5 5 0 0 1 10 0v4M5 11h14v10H5Z" />}</span>
            <span className="min-w-0"><b className="block line-clamp-2 break-words text-[15px] leading-snug">{group[0].milestoneTitle || group[0].skillName}</b>
              <small className="block text-xs text-[var(--student-muted)]">{completed}/{group.length} nhiệm vụ · {stateLabels[mapSkills.find(s => s.skillId === group[0].skillId)?.state ?? "Unknown"]}</small>
              <span className="mt-1.5 block h-1.5 overflow-hidden rounded-full bg-[var(--student-surface-subtle)]"><span className={`block h-full rounded-full st-bar-fill ${done ? "bg-emerald-500" : "bg-[linear-gradient(90deg,var(--student-brand),var(--student-brand-2))]"}`} style={{ width: `${(completed / group.length) * 100}%` }} /></span></span>
            {fixing ? <span className="rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-bold text-amber-800 whitespace-nowrap dark:bg-amber-900/50 dark:text-amber-200">Đang rèn lại</span>
              : live && !done ? <span className="rounded-full bg-[var(--student-brand-soft)] px-2.5 py-0.5 text-xs font-bold text-[var(--student-brand-ink)] whitespace-nowrap">Đang học</span>
              : waiting ? <span className="rounded-full bg-[var(--student-surface-subtle)] px-2.5 py-0.5 text-xs font-bold text-[var(--student-ink-secondary)] whitespace-nowrap">Hẹn ôn {shortDate(opensOn!)}</span> : <span />}
          </button></li>;
        })}
      </ol>

      <div className={`${panel} lg:sticky lg:top-36`} aria-live="polite">
        {activeRouteKey === "place" ? <div className="space-y-4">
          <div className="flex items-start gap-3"><span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-[var(--atlas-cobalt-soft)] text-[var(--atlas-cobalt)]"><PathIcon d={purposeIcon.Probe} /></span>
            <div className="min-w-0"><p className="st-eyebrow">Điểm xuất phát của em</p><h2 className="text-xl font-extrabold">Kiểm tra đầu vào</h2>
              <p className="mt-1 text-sm text-[var(--student-ink-secondary)]">{isPlacement ? "Kiểm tra theo từng kỹ năng. Hoàn thành phần đang làm rồi chuyển sang phần tiếp theo; có thể lưu và quay lại sau." : "Phần chưa kiểm tra giữ trạng thái chưa đánh giá, hệ thống không tự coi là đã vững. Kiểm tra thêm thì lộ trình và tiến độ hiện tại vẫn được giữ."}</p></div></div>
          {agenda.data?.placementReport && <PlacementReport report={agenda.data.placementReport} pending={agenda.data.ongoingRuns.length > 0}
      onBuild={() => agenda.data && openFromPlacement(agenda.data)}
      onContinue={!placementRunning && (nextPlacementSection(agenda.data) || agenda.data.ongoingRuns.some(m => m.runs?.some(r => r.sessionRunId === m.activeRunId && r.status === "InProgress")))
        ? () => { const ongoing = agenda.data?.ongoingRuns.find(m => m.runs?.some(r => r.sessionRunId === m.activeRunId && r.status === "InProgress"));
          setFlow(true); placementOpened.current = true; if (ongoing) setMission({ ...ongoing, reviewOnly: false }); else advancePlacement(agenda.data); } : undefined} />}
          {isPlacement ? <ol className="space-y-1">{placementSections.map((m, i) => renderStep(m, i === placementSections.length - 1, false))}</ol>
            : <div className="grid gap-2.5 sm:grid-cols-2">{mapSkills.map(s => {
              const tested = s.state !== "Unknown";
              const canCheck = extendCandidates.some(c => c.skillId === s.skillId);
              return <div key={s.skillId} className={`flex flex-col gap-2 rounded-2xl border p-3.5 ${tested ? "border-[var(--student-border)]" : "border-dashed border-[var(--student-border)] bg-[var(--student-surface-subtle)]"}`}>
                <b className="text-sm">{s.skillName}</b>
                <span className={`w-fit rounded-full px-2.5 py-0.5 text-xs font-bold ${s.state === "Confirmed" ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300" : s.state === "Developing" ? "bg-amber-50 text-amber-800 dark:bg-amber-950/50 dark:text-amber-200" : tested ? "bg-[var(--atlas-cobalt-soft)] text-[var(--atlas-cobalt)]" : "bg-[var(--student-surface)] text-[var(--student-muted)]"}`}>{tested ? stateLabels[s.state] ?? s.state : "Chưa kiểm tra"}</span>
                <small className="text-xs text-[var(--student-muted)]">{tested ? `${s.evidenceCount} câu có bằng chứng${s.masteryScore != null ? ` · ${s.masteryScore}%` : ""}` : routeSkills.has(s.skillId) ? "Có trong lộ trình · sẽ được đánh giá khi học" : "Chưa có trong lộ trình"}</small>
                {!tested && canCheck && <button className={`${action} !px-4 !py-2 text-xs`} onClick={() => setShowExtend(true)}>Kiểm tra phần này</button>}
              </div>;
            })}</div>}
        </div>
        : (() => {
          const entry = timeline.find(e => e.key === activeRouteKey);
          if (!entry) return null;
          if (entry.kind === "exam") {
            const exam = entry.exam;
            return <div aria-label="Mốc thi của lớp">
              <p className="st-eyebrow">Mốc thi của lớp · {({Midterm: "Thi giữa kỳ", Final: "Thi cuối kỳ", MockExam: "Thi thử"} as Record<string, string>)[exam.kind] ?? "Bài thi của lớp"}</p>
              <h2 className="mt-1 text-xl font-extrabold">{exam.title}</h2>
              <p className="mt-2 text-sm">{new Date(exam.examAt).toLocaleString("vi-VN", { timeZone: agenda.data?.schedule?.timeZoneId })} · {exam.timeLimitMinutes} phút</p>
              <p className="mt-1 text-sm text-[var(--student-muted)]">{({Scheduled: "Theo lịch giáo viên", Cancelled: "Giáo viên đã hủy lịch thi", Missed: "Đã qua giờ thi · xem chính sách của lớp", PendingGrading: "Đã nộp · đang hoàn tất chấm", Completed: "Đã có kết quả"} as Record<string,string>)[exam.status] ?? exam.status}</p>
              <p className="mt-2 text-xs text-[var(--student-muted)]">Phạm vi và cách chấm theo bài giáo viên công bố; kết quả dùng để điều chỉnh phần cần rèn trong tuyến.</p>
              {exam.status !== "Cancelled" && <Link className={`${secondary} mt-4`} to={`/hoc-tap/bai-tap/${exam.assignmentId}?subjectId=${subjectId}&classId=${exam.classId}`}>Xem bài thi</Link>}
            </div>;
          }
          const group = entry.missions; const skill = mapSkills.find(s => s.skillId === group[0].skillId);
          const firstRepair = group.findIndex(isRepairMission);
          return <div>
            <div className="flex items-start gap-3">
              <span className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-full text-base font-extrabold text-white [font-family:var(--student-font-display)] ${stageDone(group) ? "bg-emerald-600" : "bg-[linear-gradient(135deg,var(--student-brand),var(--student-brand-2))]"}`}>{stageNumber(entry.key)}</span>
              <div className="min-w-0"><p className="st-eyebrow">Chặng {stageNumber(entry.key)} · {group.filter(m => m.status === "Completed").length}/{group.length} nhiệm vụ</p>
                <h2 className="text-xl font-extrabold">{group[0].milestoneTitle || group[0].skillName}</h2>
                <div className="mt-1.5 flex flex-wrap gap-1.5">
                  {group.every(m => m.isSelfAssessedCheck) && <span className="rounded-full bg-sky-50 px-2.5 py-0.5 text-xs font-medium text-sky-800 dark:bg-sky-950/50 dark:text-sky-200">Tự đánh giá đã vững · ôn xác nhận</span>}
                  {group[0].weaknessLevel && weaknessLabels[group[0].weaknessLevel] && <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${weaknessLabels[group[0].weaknessLevel][1]}`}>{weaknessLabels[group[0].weaknessLevel][0]}</span>}
                  <span className="rounded-full border border-[var(--student-border)] px-2.5 py-0.5 text-xs font-medium text-[var(--student-ink-secondary)]">{stateLabels[skill?.state ?? "Unknown"]} · {skill?.evidenceCount ?? 0} câu có bằng chứng hợp lệ</span>
                </div></div></div>
            <details className="mt-3 text-sm text-[var(--student-ink-secondary)]"><summary className="cursor-pointer font-semibold text-[var(--student-brand-ink)]">Ý chính của kỹ năng</summary><p className="mt-1">{skill?.description || "Học và tự thực hiện các nhiệm vụ thuộc kỹ năng này."}</p></details>
            <ol className="mt-3">{group.map((m, i) => renderStep(m, i === group.length - 1 || isRepairMission(group[i + 1] ?? m), i === firstRepair))}</ol>
          </div>;
        })()}
      </div>
    </div>}
    <div className="space-y-5" hidden={hasPath && activeView !== "today"}>
    {agenda.data?.scheduleSummary && !agenda.data.isPlacement && <section className={`${panel} ${agenda.data.scheduleSummary.missingMinutes > 0 ? "border-amber-300" : "border-indigo-200"}`} aria-label="Lịch học và ngân sách">
      <div className="flex flex-wrap justify-between gap-3"><h2 className="text-xl font-bold">Lịch học theo mục tiêu</h2><span className="text-sm text-indigo-700 dark:text-indigo-300">{agenda.data.scheduleSummary.phase === "MockExam" ? "Giai đoạn thi thử và vá lỗ hổng" : agenda.data.scheduleSummary.phase === "ShortChecks" ? "Giai đoạn tăng kiểm tra ngắn" : "Học, luyện và ôn cân bằng"}</span></div>
      <p className="mt-3 text-sm">{agenda.data.scheduleSummary.message}</p>
      {!!agenda.data.scheduleSummary.deferredSkillIds.length && <p className="mt-2 text-sm text-amber-800 dark:text-amber-300">Phần có nội dung bị hoãn: {agenda.data.scheduleSummary.deferredSkillIds.map(id => map.data?.skills.find(s => s.skillId === id)?.skillName ?? id).join(", ")}.</p>}
      {agenda.data.targetScore != null && <p className="mt-3 text-sm font-medium">Mục tiêu {agenda.data.targetScore}/10{agenda.data.predictedScore != null ? ` · Ước lượng sơ bộ ${agenda.data.predictedScore}/10` : ""}</p>}
      {agenda.data.forecastMessage && <p className="mt-1 text-xs text-slate-500">{agenda.data.forecastMessage}</p>}
    </section>}

    {!!agenda.data?.selfAssessedSkillIds?.length && <section className="rounded-2xl border border-sky-200 bg-sky-50 p-4 text-sm text-sky-950 dark:border-sky-900 dark:bg-sky-950/40 dark:text-sky-100">
      <p>Phần em tự đánh giá đã vững (không chọn kiểm tra): <strong>{agenda.data.selfAssessedSkillIds.map(id => map.data?.skills.find(s => s.skillId === id)?.skillName ?? id).join(", ")}</strong>.</p>
      <p className="mt-1">Mỗi phần có một bài ôn ngắn ở cuối lộ trình để xác nhận. Đạt thì giữ lịch ôn; chưa đạt sẽ thêm bộ rèn lại và bài tự kiểm tra.</p>
    </section>}
    {!!agenda.data?.selfReportedPrerequisiteSkillIds?.length && <section className="rounded-2xl border border-sky-200 bg-sky-50 p-4 text-sm text-sky-950 dark:border-sky-900 dark:bg-sky-950/40 dark:text-sky-100">
      <p>Nền tảng em tự xác nhận và chọn bỏ qua: <strong>{agenda.data.selfReportedPrerequisiteSkillIds.map(id => map.data?.skills.find(s => s.skillId === id)?.skillName ?? id).join(", ")}</strong>.</p>
      <p className="mt-1">Tuyến tập trung vào các phần em đã chọn. Thông tin tự xác nhận chưa thay thế kết quả đánh giá trên web; em có thể cập nhật mục tiêu để ôn thêm bất cứ lúc nào.</p>
    </section>}
    {agenda.data?.canAdvance && <section className={`${panel} border-emerald-200`}>
      <h2 className="text-xl font-bold">Em đã nắm vững phạm vi của chặng này</h2>
      <p className="mt-2 text-sm text-slate-500">Các kỹ năng đã được xác nhận vững qua bài kiểm tra. Em có thể chọn chặng tiếp theo; lịch sử bài làm tiếp tục được dùng để lập tuyến mới.</p>
      <button className={`${action} mt-4`} onClick={() => openSetup("Next", agenda.data?.nextSkillIds)}>{agenda.data.nextSkillIds?.length ? "Khám phá và học phần tiếp theo" : "Chọn mục tiêu tiếp theo"}</button>
    </section>}
    {agenda.data?.insights && agenda.data.insights.answerCount > 0 && <section className={`${panel} border-indigo-200`} aria-labelledby="path-insights-title">
      <div className="flex flex-wrap items-start justify-between gap-5"><div className="max-w-2xl">
        <p className="text-xs font-semibold uppercase tracking-wider text-indigo-600">Từ bài làm của bạn · {agenda.data.insights.windowDays} ngày gần đây</p>
        <h2 id="path-insights-title" className="mt-2 text-2xl font-semibold">Lộ trình đề xuất cho bạn</h2>
        <p className="mt-3 text-sm leading-relaxed">{agenda.data.insights.summary}</p></div>
        <button className={action} disabled={!map.data?.skills.length} onClick={() => agenda.data?.learningPathId
          ? openSetup("Update", agenda.data.targetSkillIds) : openSetup("Update", agenda.data?.insights?.suggestedSkillIds)}>Cập nhật lộ trình từ bài làm</button></div>
      <div className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-4">{[
        [agenda.data.insights.answerCount, "lượt trả lời"], [agenda.data.insights.assignmentCount, "bài tập đã tham gia"],
        [agenda.data.insights.gradedQuestionCount, "câu khác nhau đã chấm"],
        [agenda.data.insights.scorePercentage == null ? "—" : `${agenda.data.insights.scorePercentage}%`, "điểm trên các câu gần nhất"],
      ].map(([value, label]) => <div key={label} className="rounded-xl bg-slate-50 p-4 dark:bg-slate-800"><p className="text-2xl font-bold">{value}</p><p className="mt-1 text-xs text-slate-500">{label}</p></div>)}</div>
      {agenda.data.insights.pendingCount > 0 && <p className="mt-3 text-xs text-amber-700">{agenda.data.insights.pendingCount} lượt còn chờ phân tích. Điểm chấm ban đầu và nhận xét xác nhận được phân biệt trong từng buổi.</p>}
      <div className="mt-5 grid gap-3 sm:grid-cols-2">{agenda.data.insights.skills.filter(s => s.questionCount > 0).map(s => <article key={s.skillId} className="rounded-xl border border-slate-200 p-4 dark:border-slate-700">
        <div className="flex items-center justify-between gap-3"><h3 className="font-semibold">{s.skillName}</h3><span className={`rounded-full px-3 py-1 text-xs ${s.wrongCount > 0 ? "bg-amber-50 text-amber-800" : "bg-emerald-50 text-emerald-800"}`}>{s.wrongCount > 0 ? "Nên xem lại" : s.questionCount < 5 ? "Cần thêm bài làm" : "Kết quả gần đây tốt"}</span></div>
        <p className="mt-2 text-sm text-slate-500">{s.observation}</p>{s.confirmedMisconceptions.map(m => <p key={m} className="mt-2 text-sm">Giáo viên đã xác nhận: {m}</p>)}
        <details className="mt-3 text-sm"><summary className="cursor-pointer font-medium text-indigo-600">Xem bài làm liên quan</summary>
          <ul className="mt-3 space-y-3">{s.recentAnswers.map(a => <li className="border-t border-slate-100 pt-3" key={a.attemptId}><p className="whitespace-pre-wrap">{a.questionText}</p><p className="mt-1 text-xs text-slate-500">{a.isCorrect == null ? "Chờ chấm" : a.isCorrect ? "Đúng" : "Cần sửa"} · {new Date(a.submittedAt).toLocaleDateString("vi-VN")}{a.guided ? " · Đã có hướng dẫn" : ""}</p></li>)}</ul>
        </details></article>)}</div>
    </section>}
    {agenda.data?.planOutdated && <div role="status" className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
      <p>Tuyến này được tạo trước khi có vòng luyện theo kết quả. Cập nhật để mỗi kỹ năng được luyện nhiều bộ ngắn tùy mức độ yếu; bài làm cũ vẫn được giữ và dùng để tính.</p>
      <button className={action} disabled={!map.data?.skills.length} onClick={() => openSetup("Update", agenda.data?.targetSkillIds)}>Xem tuyến cập nhật</button>
    </div>}
    {!agenda.data?.isPlacement && agenda.data?.progress && agenda.data.progress.totalSkills > 0 && <section className={panel} aria-label="Tiến độ mục tiêu">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div><p className="text-sm text-slate-500">Tiến độ mục tiêu</p>
          <p className="mt-1 text-2xl font-semibold">{agenda.data.progress.confirmedSkills}/{agenda.data.progress.totalSkills} kỹ năng đã vững</p>
          {agenda.data.progress.readyForCheckSkills > 0 && <p className="mt-1 text-sm text-indigo-700 dark:text-indigo-300">{agenda.data.progress.readyForCheckSkills} kỹ năng đã sẵn sàng tự kiểm tra</p>}</div>
        <div className="text-right text-sm">
          <p>{agenda.data.progress.streakDays > 0 ? `Chuỗi ${agenda.data.progress.streakDays} ngày học${agenda.data.progress.studiedToday ? "" : " · hôm nay chưa học"}` : "Chưa có chuỗi ngày học"}</p>
          <p className="mt-1 text-slate-500">{agenda.data.progress.remainingMinutes > 0 ? `Còn khoảng ${formatStudyMinutes(agenda.data.progress.remainingMinutes)}${agenda.data.progress.estimatedFinishDate ? ` · dự kiến xong ${new Date(agenda.data.progress.estimatedFinishDate).toLocaleDateString("vi-VN")}` : ""}` : "Đã xong các phần trong tuyến"}</p>
        </div>
      </div>
      <div className="mt-4 h-2 overflow-hidden rounded-full bg-[var(--student-surface-subtle)]"><div className="h-full rounded-full bg-[linear-gradient(90deg,var(--student-brand),var(--student-brand-2))] st-bar-fill" style={{ width: `${100 * agenda.data.progress.confirmedSkills / agenda.data.progress.totalSkills}%` }} /></div>
    </section>}
    {agenda.data?.learningPathId && !agenda.data.isPlacement && <section className={panel} aria-labelledby="today-title">
      <div className="grid gap-5 md:grid-cols-[1fr_minmax(0,22rem)] md:items-start">
        <div><h2 id="today-title" className="text-xl font-bold">Hôm nay học gì?</h2>
          {agenda.data.goalTitle && <p className="mt-1 text-sm text-slate-500">Mục tiêu: {agenda.data.goalTitle}</p>}
          <p className="mt-3 text-sm text-slate-500">{agenda.data.agendaMessage}</p></div>
        <TimeBudgetSlider label="Thời gian hôm nay" value={sliderMinutes} onChange={changeSlider} summary={planSummary} capacity={agenda.data.learningPathId && !agenda.data.isPlacement ? agenda.data.availableTodayMinutes : undefined} />
        {sliderMinutes < 10 && <p className="mt-2 text-xs leading-relaxed text-amber-700 dark:text-amber-300">Học dưới 10 phút/ngày thường tiến bộ rất chậm. Nên đặt tối thiểu 10–15 phút/ngày để thấy cải thiện rõ rệt.</p>}
      </div>
      {!!agenda.data.extraOptions?.length && <section aria-labelledby="extra-time-title" className="mt-4 border-t border-slate-200 pt-4 dark:border-slate-700">
        <h3 id="extra-time-title" className="text-sm font-semibold">Thời gian còn dư, bạn có thể làm thêm</h3>
        <ul className="mt-2 space-y-2">{agenda.data.extraOptions.map(option => {
          const to = option.kind === "Assignment"
            ? `/hoc-tap/bai-tap/${option.assignmentId}?subjectId=${subjectId}${access.classId ? `&classId=${access.classId}` : ""}`
            : `/hoc-tap/luyen-tap?subjectId=${subjectId}${access.classId ? `&classId=${access.classId}` : ""}&topicId=${option.skillId}`;
          return <li key={`${option.kind}:${option.assignmentId ?? option.skillId}`} className="flex flex-wrap items-center justify-between gap-2 text-sm">
            <span className="min-w-0">{option.kind === "Assignment" ? "Bài tập" : "Ôn thêm"}: <strong className="font-semibold">{option.title}</strong>
              <span className="tabular-nums text-slate-500 dark:text-slate-400"> · {option.questionCount} câu · khoảng {option.estimatedMinutes} phút</span></span>
            <Link to={to} className={`${secondary} shrink-0 !py-1.5`}>{option.kind === "Assignment" ? "Mở bài" : "Luyện"}</Link>
          </li>;
        })}</ul>
      </section>}
      {primary && <div className="mt-5 rounded-2xl st-hero border border-[var(--student-brand-border)] p-5 sm:p-6">
        <p className="st-eyebrow inline-flex items-center gap-2"><span className="st-live" aria-hidden="true" />{primary.activeRunId ? "Tiếp tục nhiệm vụ" : "Bắt đầu từ đây"}</p>
        <h3 className="mt-2 text-xl sm:text-2xl font-extrabold">{primary.title}</h3>{agenda.data.primaryReason && <p className="mt-1 text-xs text-indigo-700 dark:text-indigo-300">Vì sao đứng đầu: {agenda.data.primaryReason}</p>}<Reason className="mt-2 text-sm" text={primary.recommendationReason} />
        <button className={`${action} mt-4`} onClick={() => setMission(primary)}>{primary.activeRunId ? "Tiếp tục đúng tiến độ" : "Mở nhiệm vụ"} · {sizeLabel(primary)}khoảng {primary.estimatedDurationMinutes} phút</button>
      </div>}
      {todayPlan.length > 1 && <ol className="mt-4 space-y-2" aria-label="Các bước hôm nay">{todayPlan.slice(1).map((m, i) => <li key={m.missionId} className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-[var(--student-border)] p-3 transition-colors hover:border-[var(--student-brand-border)]">
        <div className="flex items-start gap-3"><span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-slate-100 text-xs font-semibold dark:bg-slate-800">{i + 2}</span>
          <div><p className="text-sm font-medium">{m.title}</p><p className="text-xs text-slate-500">{activityLabels[m.purpose] ?? m.purpose} · {sizeLabel(m)}khoảng {m.estimatedDurationMinutes} phút{m.weaknessLevel === "Weak" ? " · phần yếu" : ""}</p></div></div>
        <button className={secondary} onClick={() => setMission(m)}>{m.activeRunId ? "Tiếp tục" : "Mở"}</button>
      </li>)}</ol>}
      {agenda.data.pacingNote && <p className="mt-4 rounded-xl bg-slate-50 p-3 text-xs leading-relaxed text-slate-600 dark:bg-slate-800 dark:text-slate-300">{agenda.data.pacingNote}</p>}
      {agenda.data.progress?.studiedToday && todayPlan.length === 0 && (agenda.data.missions?.length ?? 0) > 0 && <div className="mt-5 rounded-xl border border-emerald-200 bg-emerald-50 p-4 dark:border-emerald-800 dark:bg-emerald-950/30">
        <p className="text-sm font-semibold text-emerald-900 dark:text-emerald-100">Em đã hoàn thành kế hoạch hôm nay</p>
        <p className="mt-1 text-sm text-emerald-800/80 dark:text-emerald-200/80">Còn năng lượng thì ôn thêm với luyện tập thích ứng — hệ thống chọn câu vừa sức để củng cố phần còn yếu.</p>
        <Link to={`/hoc-tap/luyen-tap?subjectId=${subjectId}`} className={`${action} mt-3 inline-block`}>Luyện tập thêm</Link>
      </div>}
    </section>}
    {agenda.data && (agenda.data.reviewDueTasks.length > 0 || agenda.data.recentChanges?.length > 0) && <section className={panel}>
      <h2 className="text-xl font-bold">Tiến bộ và lịch ôn</h2>
      {!!agenda.data.recentChanges?.length && <p className="mt-3 text-xs font-semibold text-slate-500">Lịch sử cập nhật tuyến · số liệu tại thời điểm cập nhật; tiến độ hiện tại xem ở trên.</p>}
      {agenda.data.recentChanges?.map((r, i) => <p className="mt-3 text-sm text-slate-500" key={i}>{r}</p>)}
      {agenda.data.reviewDueTasks.map(r => <div key={r.missionId} className="mt-3 flex flex-wrap items-center justify-between gap-3 rounded-xl bg-indigo-50 p-4 dark:bg-indigo-950/40"><p className="text-sm">{r.title} · {r.availableFrom ? new Date(r.availableFrom).toLocaleDateString("vi-VN") : "Đến lượt ôn"}</p><button disabled={!r.canStart} className={action} onClick={() => setMission(r)}>{r.canStart ? "Ôn ngay" : "Đã hẹn lịch"}</button></div>)}
    </section>}
    </div>
    <section className={panel} hidden={hasPath && activeView !== "map"}><h2 className="text-xl font-bold">Bản đồ năng lực · {subject?.subjectName}</h2>
      {!map.data?.skills.length && <p className="mt-3 text-sm text-slate-500">Chưa có kỹ năng khả dụng trong giáo trình. Cần giáo viên bổ sung nội dung; hệ thống không dùng môn khác để thay thế.</p>}
      <div className="mt-4 grid grid-cols-[repeat(auto-fit,minmax(min(100%,20rem),1fr))] gap-3">{map.data?.skills.map(s => <div className="min-w-0 rounded-2xl border border-[var(--student-border)] p-4" key={s.skillId}>
        <h3 className="font-semibold">{s.skillName}</h3><p className="mt-1 text-sm text-indigo-600">{stateLabels[s.state] ?? s.state}</p>
        <p className="mt-2 text-xs text-slate-500">{s.rationale}</p>{s.masteryScore != null && <p className="mt-2 text-xs">Hồ sơ năng lực: {s.masteryScore}%</p>}
        {s.availability === "PrerequisiteBlocked" && !s.prerequisiteSkillIds.every(id => agenda.data?.selfReportedPrerequisiteSkillIds?.includes(id) || map.data?.skills.some(p => p.skillId === id && p.state === "Confirmed")) && <p className="mt-2 text-xs text-amber-700">{agenda.data?.isPlacement ? "Nền tảng chưa được xác nhận; kiểm tra đầu vào vẫn giúp nhận biết phần đã biết." : "Cần xác nhận kỹ năng nền tảng hoặc cập nhật mục tiêu để tự xác nhận phần đã học ở trường."}</p>}
      </div>)}</div>
    </section>
    {(agenda.data?.pathHistory?.length ?? 0) > 0 && <section className={panel} hidden={activeView !== "history"} aria-labelledby="path-history-title">
      <h2 id="path-history-title" className="text-xl font-semibold">Lịch sử tuyến học</h2>
      <p className="mt-1 text-sm text-slate-500">Đổi tuyến không xóa bài làm. Mọi câu trả lời của các tuyến trước vẫn được dùng để xác định phần yếu và bỏ qua phần đã vững.</p>
      <ul className="mt-4 space-y-2">{agenda.data!.pathHistory!.map(h => <li key={h.learningPathId} className="rounded-xl border border-slate-200 text-sm dark:border-slate-700">
        <details className="group">
          <summary className="flex cursor-pointer list-none items-start justify-between gap-3 p-3">
            <span><span className="block font-medium">Phiên bản {h.version} · {h.goalTitle}</span>
              <span className="mt-1 block text-xs text-slate-500">{new Date(h.createdAt).toLocaleDateString("vi-VN")}{h.endedAt ? ` – ${new Date(h.endedAt).toLocaleDateString("vi-VN")}` : ""} · {h.completedMissions}/{h.totalMissions} nhiệm vụ hoàn thành · {h.answeredQuestions} câu đã trả lời</span></span>
            <span className="shrink-0 text-xs font-semibold text-indigo-600 dark:text-indigo-300"><span className="group-open:hidden">Xem bài đã làm</span><span className="hidden group-open:inline">Thu gọn</span></span>
          </summary>
          <div className="border-t border-slate-100 p-3 dark:border-slate-800">
            {!h.missions?.length ? <p className="text-xs text-slate-500">Tuyến này chưa có lượt làm bài nào để xem lại.</p>
              : <ul className="space-y-3">{[...new Set(h.missions.map(m => m.skillName))].map(skillName => <li key={skillName}>
                <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">{skillName}</p>
                <ul className="mt-1 space-y-1">{h.missions!.filter(m => m.skillName === skillName).flatMap(m => m.runs.map(r => <li key={r.sessionRunId} className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-slate-50 px-3 py-2 dark:bg-slate-800">
                  <span className="min-w-0"><span className="font-medium">{m.title}</span><span className="block text-xs text-slate-500">{runSummary(r)}</span></span>
                  <button className="shrink-0 rounded-lg border border-slate-300 px-3 py-1 text-xs font-semibold dark:border-slate-600"
                    onClick={() => { setHistoryContext(`Lịch sử tuyến · Phiên bản ${h.version} · chỉ xem, không làm lại hoặc sửa`);
                      setMission({ missionId: m.missionId, title: m.title, objective: "", purpose: m.purpose, skillId: m.skillId, skillName: m.skillName,
                        estimatedDurationMinutes: 0, activityTypes: [], recommendationReason: "", questionCount: 0, canStart: false, status: "Completed",
                        latestRunId: r.sessionRunId, reviewOnly: true, retry: false }); }}>Xem lại</button>
                </li>))}</ul>
              </li>)}</ul>}
            <p className="mt-3 text-xs text-slate-500">Chỉ xem lại câu hỏi, câu trả lời và lời giải đã lưu; không thể làm lại hoặc chỉnh sửa bài của tuyến cũ.</p>
          </div>
        </details>
      </li>)}</ul>
    </section>}
    {showGoal && <GoalAndProposalModal key={`${subjectId}:${access.classId}:${entryMode}`} isOpen onClose={() => setShowGoal(false)}
      entryMode={entryMode} initialMinutes={agenda.data?.availableMinutesPerDay} initialDays={agenda.data?.availableDaysPerWeek}
      initialTargetScore={agenda.data?.targetScore} initialSchedule={agenda.data?.schedule} initialPreferredPace={agenda.data?.preferredPace}
      initialGoalType={agenda.data?.goalType} initialTitle={agenda.data?.goalTitle ?? undefined} initialDeadline={agenda.data?.deadlineDate}
      insights={agenda.data?.insights} suggestedSkillIds={initialScope} selfAssessedSkillIds={selfAssessed}
      placementReport={entryMode === "FromPlacement" ? (agenda.data?.placementReport ?? undefined) : undefined}
      initialSelfReportedPrerequisiteSkillIds={entryMode === "Update" || entryMode === "FromPlacement" ? agenda.data?.selfReportedPrerequisiteSkillIds : undefined}
      onPlacementStarted={async () => { setFlow(true); placementOpened.current = false; setTransitionNote("Đang mở phần kiểm tra đầu tiên…");
        try { const fresh = await agenda.refetch(); void map.refetch(); advancePlacement(fresh.data); } finally { setTransitionNote(null); } }}
      subjectId={subjectId} classId={access.classId} skills={map.data?.skills ?? []} subjectName={subject?.subjectName ?? ""}
      onOpenBlockingMission={blocked => {
        setShowGoal(false);
        const existing = agenda.data?.missions.find(m => m.missionId === blocked.missionId);
        setMission({ ...(existing ?? { objective: "", purpose: "", skillId: "", skillName: "Buổi cần xử lý", estimatedDurationMinutes: 0, activityTypes: [], recommendationReason: "", questionCount: 0, canStart: false }),
          missionId: blocked.missionId, title: blocked.title, activeRunId: blocked.runId, latestRunId: blocked.runId, status: "InProgress", reviewOnly: false });
      }}
      onAccepted={res => { setAcceptedNotice(`${res.message} Các nhiệm vụ của tuyến đã được cập nhật.`); void agenda.refetch(); void map.refetch(); }} />}
    {showExtend && <ExtendCheckModal subjectId={subjectId} classId={access.classId} subjectName={subject?.subjectName ?? ""} candidates={extendCandidates}
      onClose={() => setShowExtend(false)}
      onStarted={async (ids, unavailableNames) => {
        setShowExtend(false); extraQueue.current = [...ids]; setTransitionNote("Đang mở phần kiểm tra đầu tiên…");
        setAcceptedNotice(`Đã thêm ${ids.length} phần kiểm tra vào lộ trình hiện tại; các phần sẽ mở nối tiếp nhau. Lộ trình và tiến độ đang có được giữ nguyên.`
          + (unavailableNames.length ? ` Chưa thêm được: ${unavailableNames.join(", ")} (chưa đủ 3 câu tự chấm được; hãy báo giáo viên bổ sung câu hỏi).` : ""));
        try { const [fresh] = await Promise.all([agenda.refetch(), map.refetch()]); openNextExtra(fresh.data); } finally { setTransitionNote(null); }
      }} />}
    {mission && <MissionPlayerModal key={`${mission.missionId}:${mission.retry ? "retry" : mission.reviewOnly ? `review-${mission.latestRunId}` : "open"}`} isOpen mission={mission} subjectId={subjectId}
      contextLabel={historyContext ?? (placementRunning ? sectionLabel(mission) : mission.title.startsWith("Kiểm tra thêm") ? "Kiểm tra thêm phần khác · lộ trình hiện tại được giữ" : undefined)}
      nextLabel={placementRunning && sectionLabel(mission) ? placementSections.filter(m => m.missionId !== mission.missionId).some(m => m.canStart && m.status !== "Completed" && !m.latestRunId) ? "Sang phần tiếp theo" : "Xem kết quả và lộ trình đề xuất" : undefined}
      onResumeMission={setMission}
      onClose={async () => {
        const closedId = mission.missionId; setMission(null);
        if (historyContext) { setHistoryContext(null); return; }
        if (extraQueue.current.length > 1 || placementFlow.current) setTransitionNote("Đã lưu phần vừa làm. Đang chuẩn bị phần kiểm tra tiếp theo…");
        try { await afterMissionClosed(closedId); } finally { setTransitionNote(null); }
      }} />}
    {transitionNote && !mission && <div role="status" aria-live="polite" className="fixed inset-0 z-50 flex flex-col items-center justify-center gap-4 bg-[var(--student-canvas)] px-6 text-center">
      <span className="h-10 w-10 animate-spin rounded-full border-4 border-[var(--student-brand)] border-t-transparent motion-reduce:animate-none" aria-hidden="true" />
      <p className="text-base font-semibold text-slate-800 dark:text-slate-100">{transitionNote}</p>
      <p className="text-sm text-slate-500 dark:text-slate-400">Bài làm được lưu theo tài khoản; nếu mạng chậm, phần tiếp theo sẽ tự mở khi sẵn sàng.</p>
    </div>}
  </div>;

  // After a part closes: reload the route, then open the next queued part (if any). Declared below the JSX (hoisted).
  async function afterMissionClosed(closedId: string) {
    const [fresh] = await Promise.all([agenda.refetch(), map.refetch()]);
    if (extraQueue.current.length) {
      const closedExtra = fresh.data?.missions.find(m => m.missionId === closedId);
      if (closedExtra?.runs?.some(r => r.sessionRunId === closedExtra.activeRunId && r.status === "InProgress")) { extraQueue.current = []; return; }
      if (extraQueue.current[0] === closedId) extraQueue.current.shift();
      openNextExtra(fresh.data); return;
    }
    if (!placementFlow.current) return;
    // Saved and left mid-section: stop here; "Tiếp tục kiểm tra" resumes later.
    const closed = fresh.data?.missions.find(m => m.missionId === closedId);
    if (closed?.runs?.some(r => r.sessionRunId === closed.activeRunId && r.status === "InProgress")) { setFlow(false); return; }
    advancePlacement(fresh.data);
  }
}
