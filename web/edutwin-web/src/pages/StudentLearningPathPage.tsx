import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
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
import type { AgendaItemDto, MissionRunSummaryDto } from "../types/learningPath";
import { missionAction } from "../utils/learningMission";
import { PlacementReport } from "../components/learningPathV4/PlacementReport";

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
const stateLabels: Record<string, string> = { Unknown: "Chưa đủ bằng chứng", Developing: "Cần củng cố",
  Demonstrated: "Đã có bằng chứng ban đầu", Confirmed: "Đã xác nhận", ReviewDue: "Cần ôn lại" };
export function StudentLearningPathPage() {
  const [params, setParams] = useSearchParams();
  const actor = useAuthStore(s => s.user);
  const subjects = useQuery({ queryKey: ["subjects", "student-learning-path", actor?.centerId, actor?.userId],
    queryFn: () => organizationApi.listSubjects(true) });
  const subject = subjects.data?.data.find(s => s.subjectId === params.get("subjectId")) ?? subjects.data?.data[0];
  const subjectId = subject?.subjectId ?? "";
  const access = useStudentLearningAccess(subjectId);
  const [sliderMinutes, setSliderMinutes] = useState(15);
  const [minutes, setMinutes] = useState(15);
  // The slider updates its readout immediately; the plan is recalculated once dragging pauses.
  useEffect(() => { const timer = setTimeout(() => setMinutes(sliderMinutes), 350); return () => clearTimeout(timer); }, [sliderMinutes]);
  const [showGoal, setShowGoal] = useState(false);
  const [entryMode, setEntryMode] = useState<"Placement" | "Foundation" | "Update" | "Next" | undefined>();
  const [initialScope, setInitialScope] = useState<string[]>([]);
  const openSetup = (mode?: typeof entryMode, scope: string[] = []) => {
    setEntryMode(mode); setInitialScope(scope); setShowGoal(true);
  };
  const [mission, setMission] = useState<AgendaItemDto | null>(null);
  const [acceptedNotice, setAcceptedNotice] = useState("");
  const map = useQuery({ queryKey: ["learning-paths", "capability-map", actor?.centerId, actor?.userId, subjectId, access.classId],
    queryFn: () => getCapabilityMap(subjectId, access.classId), enabled: !!subjectId && access.writable });
  const agenda = useQuery({ queryKey: ["learning-agenda", actor?.centerId, actor?.userId, subjectId, access.classId, minutes],
    queryFn: () => getLearningAgenda(subjectId, minutes, access.classId), enabled: !!subjectId && access.writable, placeholderData: keepPreviousData });
  const missions = agenda.data?.missions ?? [];
  const primary = agenda.data?.primaryMission;
  const todayPlan = agenda.data?.todayPlan ?? (primary ? [primary, ...(agenda.data?.alternativeOptions ?? [])] : []);
  const planSummary = agenda.data?.learningPathId
    ? todayPlan.length === 0 ? "Chưa có nhiệm vụ vừa với thời gian này."
      : `Kế hoạch: ${todayPlan.reduce((n, m) => n + (m.purpose === "Practice" || m.purpose === "Review" ? m.plannedSets ?? 1 : 1), 0)} bước · khoảng ${formatStudyMinutes(agenda.data.plannedMinutes ?? 0)}${agenda.data.plannedQuestionCount ? ` · ≈ ${agenda.data.plannedQuestionCount} câu` : ""}${agenda.isPlaceholderData ? " · đang tính lại…" : ""}`
    : undefined;
  const panel = "rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 p-5 sm:p-6";
  const action = "rounded-xl bg-indigo-600 hover:bg-indigo-700 px-4 py-2.5 text-sm font-semibold text-white disabled:opacity-40 disabled:cursor-not-allowed";
  if (access.pending || subjects.isPending) return <p role="status" className="p-6">Đang kiểm tra môn học và giáo trình…</p>;
  if (access.readOnly) return <StudentLearningReadOnlyNotice reason={access.reason} />;
  return <div className="mx-auto max-w-6xl space-y-7 px-4 py-7 text-slate-900 dark:text-slate-100">
    {acceptedNotice && <div role="status" className="flex items-start justify-between gap-3 rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-900"><p>{acceptedNotice}</p><button aria-label="Đóng thông báo đã chọn tuyến" onClick={() => setAcceptedNotice("")}>×</button></div>}
    <header className="flex flex-wrap items-start justify-between gap-4">
      <div><p className="text-sm text-indigo-600 dark:text-indigo-300">Mục tiêu của bạn · Tiến bộ từ bằng chứng thật</p>
        <h1 className="mt-1 text-3xl font-bold">Lộ trình học tập cá nhân</h1>
        <p className="mt-2 max-w-xl text-sm text-slate-500">Tìm hiểu, luyện có hướng dẫn và tự kiểm tra là những hoạt động riêng. Chọn kỹ năng phù hợp với mục tiêu của bạn.</p></div>
      <div className="flex flex-wrap gap-3"><label className="sr-only" htmlFor="path-subject">Môn học</label>
        <select id="path-subject" value={subjectId} className="rounded-xl border border-slate-300 bg-white p-2.5 dark:bg-slate-900"
          onChange={e => { const next = new URLSearchParams(params); next.set("subjectId", e.target.value); next.delete("pathId"); next.delete("sessionId"); setParams(next); setMission(null); setShowGoal(false); }}>
          {subjects.data?.data.map(s => <option key={s.subjectId} value={s.subjectId}>{s.subjectName}</option>)}
        </select><button className={action} onClick={() => openSetup()} disabled={!map.data?.skills.length}>Thiết lập mục tiêu</button>
        {agenda.data?.learningPathId && !agenda.data.isPlacement && <button className="rounded-xl border px-4 py-2.5 text-sm" onClick={() => openSetup("Placement", agenda.data?.targetSkillIds)} disabled={!map.data?.skills.length}>Kiểm tra đầu vào</button>}</div>
    </header>
    {(map.error || agenda.error) && <div role="alert" className="rounded-xl bg-rose-50 p-4 text-rose-800">
      Không thể tải lộ trình. {((map.error ?? agenda.error) as any)?.response?.data?.detail ?? (map.error ?? agenda.error)?.message}
      <button className="ml-3 underline" onClick={() => { void map.refetch(); void agenda.refetch(); }}>Thử lại</button>
    </div>}
    {agenda.data && !agenda.data.learningPathId && <section className={`${panel} border-indigo-200`} aria-labelledby="starting-point-title">
      <p className="text-sm font-semibold text-indigo-600">Bắt đầu học cùng EduTwin</p>
      <h2 id="starting-point-title" className="mt-2 text-2xl font-semibold">Chưa biết bắt đầu từ đâu?</h2>
      <p className="mt-3 max-w-2xl text-sm text-slate-500">Chọn phần muốn học, EduTwin sẽ kiểm tra kiến thức liên quan và đề xuất việc cần làm tiếp theo. Em có thể bắt đầu từ nền tảng nếu muốn học ngay.</p>
      <div className="mt-5 flex flex-wrap gap-3"><button className={action} disabled={!map.data?.skills.length} onClick={() => openSetup("Placement")}>Tìm điểm xuất phát · Kiểm tra đầu vào</button>
        <button className="rounded-xl border px-4 py-2.5 text-sm font-semibold" disabled={!map.data?.skills.length} onClick={() => openSetup("Foundation")}>Học từ nền tảng</button></div>
      {!map.data?.skills.length && <p className="mt-3 text-sm text-amber-700">Môn học chưa có kỹ năng trong giáo trình. Cần giáo viên bổ sung để bắt đầu.</p>}
    </section>}
    {agenda.data?.placementReport && <PlacementReport report={agenda.data.placementReport} pending={agenda.data.ongoingRuns.length > 0}
      onBuild={() => openSetup("Update", agenda.data?.targetSkillIds)} />}
    {!!agenda.data?.selfReportedPrerequisiteSkillIds?.length && <section className="rounded-xl border border-sky-200 bg-sky-50 p-4 text-sm text-sky-950">
      <p>Nền tảng em tự xác nhận và chọn bỏ qua: <strong>{agenda.data.selfReportedPrerequisiteSkillIds.map(id => map.data?.skills.find(s => s.skillId === id)?.skillName ?? id).join(", ")}</strong>.</p>
      <p className="mt-1">Tuyến tập trung vào các phần em đã chọn. Thông tin tự xác nhận chưa thay thế kết quả đánh giá trên web; em có thể cập nhật mục tiêu để ôn thêm bất cứ lúc nào.</p>
    </section>}
    {agenda.data?.canAdvance && <section className={`${panel} border-emerald-200`}>
      <h2 className="text-xl font-semibold">Em đã nắm vững phạm vi của chặng này</h2>
      <p className="mt-2 text-sm text-slate-500">Các kỹ năng đã có bằng chứng độc lập hợp lệ. Em có thể chọn chặng tiếp theo; lịch sử bài làm tiếp tục được dùng để lập tuyến mới.</p>
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
      <div className="mt-4 h-2 overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800"><div className="h-full rounded-full bg-indigo-600" style={{ width: `${100 * agenda.data.progress.confirmedSkills / agenda.data.progress.totalSkills}%` }} /></div>
    </section>}
    {agenda.data && <section className={panel} aria-labelledby="today-title">
      <div className="grid gap-5 md:grid-cols-[1fr_minmax(0,22rem)] md:items-start">
        <div><h2 id="today-title" className="text-xl font-semibold">Hôm nay học gì?</h2>
          {agenda.data.goalTitle && <p className="mt-1 text-sm text-slate-500">Mục tiêu: {agenda.data.goalTitle}</p>}
          <p className="mt-3 text-sm text-slate-500">{agenda.data.agendaMessage}</p></div>
        <TimeBudgetSlider label="Thời gian hôm nay" value={sliderMinutes} onChange={setSliderMinutes} summary={planSummary} />
      </div>
      {primary && <div className="mt-5 rounded-xl bg-indigo-50 p-5 dark:bg-indigo-950/40">
        <p className="text-xs font-semibold uppercase tracking-wider text-indigo-600">{primary.activeRunId ? "Tiếp tục nhiệm vụ" : "Bắt đầu từ đây"}</p>
        <h3 className="mt-2 text-xl font-semibold">{primary.title}</h3><Reason className="mt-2 text-sm" text={primary.recommendationReason} />
        <button className={`${action} mt-4`} onClick={() => setMission(primary)}>{primary.activeRunId ? "Tiếp tục đúng tiến độ" : "Mở nhiệm vụ"} · {sizeLabel(primary)}khoảng {primary.estimatedDurationMinutes} phút</button>
      </div>}
      {todayPlan.length > 1 && <ol className="mt-4 space-y-2" aria-label="Các bước hôm nay">{todayPlan.slice(1).map((m, i) => <li key={m.missionId} className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 p-3 dark:border-slate-700">
        <div className="flex items-start gap-3"><span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-slate-100 text-xs font-semibold dark:bg-slate-800">{i + 2}</span>
          <div><p className="text-sm font-medium">{m.title}</p><p className="text-xs text-slate-500">{activityLabels[m.purpose] ?? m.purpose} · {sizeLabel(m)}khoảng {m.estimatedDurationMinutes} phút{m.weaknessLevel === "Weak" ? " · phần yếu" : ""}</p></div></div>
        <button className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm" onClick={() => setMission(m)}>{m.activeRunId ? "Tiếp tục" : "Mở"}</button>
      </li>)}</ol>}
      {agenda.data.pacingNote && <p className="mt-4 rounded-xl bg-slate-50 p-3 text-xs leading-relaxed text-slate-600 dark:bg-slate-800 dark:text-slate-300">{agenda.data.pacingNote}</p>}
      {!agenda.data.learningPathId && <button className={`${action} mt-4`} disabled={!map.data?.skills.length} onClick={() => openSetup("Placement")}>Chọn phần học và tìm điểm xuất phát</button>}
    </section>}
    {agenda.data && (agenda.data.reviewDueTasks.length > 0 || agenda.data.recentChanges?.length > 0) && <section className={panel}>
      <h2 className="text-xl font-semibold">Tiến bộ và lịch ôn</h2>
      {agenda.data.recentChanges?.map((r, i) => <p className="mt-3 text-sm text-slate-500" key={i}>{r}</p>)}
      {agenda.data.reviewDueTasks.map(r => <div key={r.missionId} className="mt-3 flex flex-wrap items-center justify-between gap-3 rounded-xl bg-indigo-50 p-4 dark:bg-indigo-950/40"><p className="text-sm">{r.title} · {r.availableFrom ? new Date(r.availableFrom).toLocaleDateString("vi-VN") : "Đến lượt ôn"}</p><button disabled={!r.canStart} className={action} onClick={() => setMission(r)}>{r.canStart ? "Ôn ngay" : "Đã hẹn lịch"}</button></div>)}
    </section>}
    <section className="space-y-4"><div><h2 className="text-xl font-semibold">{agenda.data?.isPlacement ? "Các phần kiểm tra đầu vào" : "Các chặng trong tuyến của bạn"}</h2><p className="mt-1 text-sm text-slate-500">{agenda.data?.isPlacement ? "Kiểm tra theo từng kỹ năng. Hoàn thành phần đang làm rồi chuyển sang phần tiếp theo; có thể lưu và quay lại sau." : "Mỗi chặng gắn với một kỹ năng. Phần đã có bằng chứng sẽ được giảm luyện lặp; nội dung còn thiếu được nêu rõ."}</p></div>
      {missions.length === 0 && <p className={panel}>{agenda.data?.isPlacement ? "Các phần đã chọn đã có bằng chứng hợp lệ. Xem đánh giá đầu vào để chọn tuyến học tiếp theo." : "Chọn phần muốn học và tìm điểm xuất phát ở trên để bắt đầu. Các bài làm và tuyến trước được giữ trong lịch sử."}</p>}
      {[...new Set(missions.map(m => m.skillId))].map((id, index) => {
        const group = missions.filter(m => m.skillId === id); const skill = map.data?.skills.find(s => s.skillId === id);
        return <article className={panel} key={id}><div className="flex items-start gap-3"><span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-indigo-100 font-semibold text-indigo-800">{index + 1}</span>
          <div><div className="flex flex-wrap items-center gap-2"><h3 className="text-lg font-semibold">{group[0].skillName}</h3>
            {group[0].weaknessLevel && weaknessLabels[group[0].weaknessLevel] && <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${weaknessLabels[group[0].weaknessLevel][1]}`}>{weaknessLabels[group[0].weaknessLevel][0]}</span>}</div><p className="mt-1 text-sm text-slate-500">{skill?.description || "Học và tự thực hiện các nhiệm vụ thuộc kỹ năng này."}</p>
            <p className="mt-2 text-xs font-semibold text-indigo-600">{stateLabels[skill?.state ?? "Unknown"]} · {skill?.evidenceCount ?? 0} câu có bằng chứng hợp lệ</p></div></div>
          <div className="mt-5 grid gap-3 sm:grid-cols-3">{group.map(m => <div key={m.missionId} className="flex flex-col rounded-xl border border-slate-200 p-4 dark:border-slate-700">
            <p className="text-xs font-semibold text-indigo-600">{activityLabels[m.purpose] ?? m.purpose}</p><h4 className="mt-2 font-medium">{m.title}</h4>
            <Reason className="mt-2 flex-1 text-xs leading-relaxed text-slate-500" text={m.recommendationReason} />
            {m.questionCount > 0 && <p className="mt-2 text-xs font-medium">{m.setNumber ? `Bộ ${m.setNumber}${m.estimatedSets ? ` (dự kiến ${m.estimatedSets} bộ)` : ""} · ` : ""}{m.questionCount} câu · khoảng {m.questionCount * 2 > 0 && (m.purpose === "Practice" || m.purpose === "Review") ? m.questionCount * 2 : m.estimatedDurationMinutes} phút</p>}
            {m.advice && <p className="mt-2 text-xs text-amber-700 dark:text-amber-300">{m.advice}</p>}
            {m.availableFrom && <p className="mt-2 text-xs text-indigo-600">Hẹn ôn {new Date(m.availableFrom).toLocaleDateString("vi-VN")}</p>}
            <div className="mt-4 flex flex-wrap gap-2">
              <button className={action} disabled={!m.canStart && !m.activeRunId && !m.latestRunId} onClick={() => setMission({ ...m, reviewOnly: missionAction(m).reviewOnly })}>
                {missionAction(m).label}</button>
              {!m.activeRunId && m.canRetry && <button className="rounded-xl border border-indigo-300 px-4 py-2.5 text-sm font-semibold text-indigo-700 dark:text-indigo-300"
                onClick={() => setMission({ ...m, retry: true, reviewOnly: false })}>{m.retryLabel ?? "Làm lại"}</button>}
            </div>
            {!m.activeRunId && !m.canRetry && m.retryBlockedReason && <p className="mt-2 text-xs text-slate-500">{m.retryBlockedReason}</p>}
            {(m.runs?.length ?? 0) > 0 && <details className="mt-3 text-xs"><summary className="cursor-pointer font-medium text-indigo-600">Lịch sử {m.runs!.length} lượt</summary>
              <ul className="mt-2 space-y-1">{m.runs!.map(r => <li key={r.sessionRunId} className="flex items-center justify-between gap-2">
                <span className="text-slate-600 dark:text-slate-300">{runSummary(r)}</span>
                {r.status !== "InProgress" && <button className="shrink-0 underline" onClick={() => setMission({ ...m, latestRunId: r.sessionRunId, reviewOnly: true, retry: false })}>Xem</button>}
              </li>)}</ul></details>}
          </div>)}</div></article>;
      })}
    </section>
    <section className={panel}><h2 className="text-xl font-semibold">Bản đồ năng lực · {subject?.subjectName}</h2>
      {!map.data?.skills.length && <p className="mt-3 text-sm text-slate-500">Chưa có kỹ năng khả dụng trong giáo trình. Cần giáo viên bổ sung nội dung; hệ thống không dùng môn khác để thay thế.</p>}
      <div className="mt-4 grid gap-3 sm:grid-cols-2">{map.data?.skills.map(s => <div className="rounded-xl border border-slate-200 p-4 dark:border-slate-700" key={s.skillId}>
        <h3 className="font-semibold">{s.skillName}</h3><p className="mt-1 text-sm text-indigo-600">{stateLabels[s.state] ?? s.state}</p>
        <p className="mt-2 text-xs text-slate-500">{s.rationale}</p>{s.masteryScore != null && <p className="mt-2 text-xs">Hồ sơ năng lực: {s.masteryScore}%</p>}
        {s.availability === "PrerequisiteBlocked" && !s.prerequisiteSkillIds.every(id => agenda.data?.selfReportedPrerequisiteSkillIds?.includes(id) || map.data?.skills.some(p => p.skillId === id && p.state === "Confirmed")) && <p className="mt-2 text-xs text-amber-700">{agenda.data?.isPlacement ? "Nền tảng chưa được xác nhận; kiểm tra đầu vào vẫn giúp nhận biết phần đã biết." : "Cần xác nhận kỹ năng nền tảng hoặc cập nhật mục tiêu để tự xác nhận phần đã học ở trường."}</p>}
      </div>)}</div>
    </section>
    {(agenda.data?.pathHistory?.length ?? 0) > 0 && <section className={panel} aria-labelledby="path-history-title">
      <h2 id="path-history-title" className="text-xl font-semibold">Lịch sử tuyến học</h2>
      <p className="mt-1 text-sm text-slate-500">Đổi tuyến không xóa bài làm. Mọi câu trả lời của các tuyến trước vẫn được dùng để xác định phần yếu và bỏ qua phần đã vững.</p>
      <ul className="mt-4 space-y-2">{agenda.data!.pathHistory!.map(h => <li key={h.learningPathId} className="rounded-xl border border-slate-200 p-3 text-sm dark:border-slate-700">
        <p className="font-medium">Phiên bản {h.version} · {h.goalTitle}</p>
        <p className="mt-1 text-xs text-slate-500">{new Date(h.createdAt).toLocaleDateString("vi-VN")}{h.endedAt ? ` – ${new Date(h.endedAt).toLocaleDateString("vi-VN")}` : ""} · {h.completedMissions}/{h.totalMissions} nhiệm vụ hoàn thành · {h.answeredQuestions} câu đã trả lời</p>
      </li>)}</ul>
    </section>}
    {showGoal && <GoalAndProposalModal key={`${subjectId}:${access.classId}:${entryMode}`} isOpen onClose={() => setShowGoal(false)}
      entryMode={entryMode} initialMinutes={agenda.data?.availableMinutesPerDay} initialDays={agenda.data?.availableDaysPerWeek}
      initialGoalType={agenda.data?.goalType} initialTitle={agenda.data?.goalTitle ?? undefined} initialDeadline={agenda.data?.deadlineDate}
      initialSelfReportedPrerequisiteSkillIds={entryMode === "Update" ? agenda.data?.selfReportedPrerequisiteSkillIds : undefined}
      insights={agenda.data?.insights} suggestedSkillIds={initialScope}
      subjectId={subjectId} classId={access.classId} skills={map.data?.skills ?? []} subjectName={subject?.subjectName ?? ""}
      onOpenBlockingMission={blocked => {
        setShowGoal(false);
        const existing = agenda.data?.missions.find(m => m.missionId === blocked.missionId);
        setMission({ ...(existing ?? { objective: "", purpose: "", skillId: "", skillName: "Buổi cần xử lý", estimatedDurationMinutes: 0, activityTypes: [], recommendationReason: "", questionCount: 0, canStart: false }),
          missionId: blocked.missionId, title: blocked.title, activeRunId: blocked.runId, latestRunId: blocked.runId, status: "InProgress", reviewOnly: false });
      }}
      onAccepted={res => { setAcceptedNotice(`${res.message} Các nhiệm vụ của tuyến đã được cập nhật.`); void agenda.refetch(); void map.refetch(); }} />}
    {mission && <MissionPlayerModal key={`${mission.missionId}:${mission.retry ? "retry" : mission.reviewOnly ? `review-${mission.latestRunId}` : "open"}`} isOpen mission={mission} subjectId={subjectId}
      onResumeMission={setMission}
      onClose={() => { setMission(null); void agenda.refetch(); void map.refetch(); }} />}
  </div>;
}
