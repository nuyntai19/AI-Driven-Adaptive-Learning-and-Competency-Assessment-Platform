import { useEffect, useRef, useState } from "react";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createOrUpdateLearningGoal, estimateLearningGoal, generatePathProposal, acceptPathProposal, customizePathProposal } from "../../api/learningPathApi";
import type { CreateLearningGoalRequest, CustomizePathProposalRequest, LearningGoalEstimateDto, PathProposalDto, PlacementReportDto, AcceptPathProposalResponse, SkillCapabilityStatusDto, LearningInsightsDto, LearningScheduleSettings } from "../../types/learningPath";
import { DateInput } from "../DateInput";
import { extractProblemDetails } from "../../utils/problemDetails";
import { recommendLearningGoal } from "../../utils/learningPathRecommendation";
import { pathChangeBlockersFromError, type BlockingLearningMission } from "../../utils/learningMission";
import { ProposalCustomizationForm } from "./ProposalCustomizationForm";
import { TimeBudgetSlider } from "./TimeBudgetSlider";
import { MIN_GOAL_STUDY_MINUTES } from "../../utils/studyTime";
import { formatStudyMinutes } from "../../utils/studyTime";
import { groupLearningSkills, missingLearningPrerequisites } from "../../utils/learningScope";
import { SkillScopeSelector } from "./SkillScopeSelector";

interface Props { isOpen: boolean; onClose: () => void; subjectId: string; classId?: string;
  initialTargetScore?: number | null; initialSchedule?: LearningScheduleSettings; initialPreferredPace?: string;
  onOpenBlockingMission?: (mission: BlockingLearningMission) => void;
  onAccepted: (res: AcceptPathProposalResponse) => void; skills?: SkillCapabilityStatusDto[]; subjectName?: string;
  suggestedSkillIds?: string[]; recommendationSummary?: string; automatic?: boolean; insights?: LearningInsightsDto;
  entryMode?: "Start" | "Placement" | "Foundation" | "Update" | "Next" | "FromPlacement";
  /** Parts left out of the placement check; the route ends with a short confirmation review for each. */
  selfAssessedSkillIds?: string[]; onPlacementStarted?: () => void; placementReport?: PlacementReportDto | null;
  initialMinutes?: number; initialDays?: number; initialGoalType?: string; initialTitle?: string; initialDeadline?: string | null; initialSelfReportedPrerequisiteSkillIds?: string[] }
function todayIso() { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`; }
function formatIsoDate(iso?: string | null) { if (!iso) return ""; const bits = iso.split("T")[0].split("-"); return bits.length === 3 ? `${bits[2]}/${bits[1]}/${bits[0]}` : iso; }
type FeasibilityFields = Pick<LearningGoalEstimateDto, "currentEstimatedScore" | "targetScore" | "requiredStudyMinutes" | "minimumStudyDays"
  | "earliestFeasibleDate" | "suggestedDeadline" | "achievableScore" | "feasibilityStatus" | "feasibilityMessage">;
/** Score, workload and dates behind a deadline verdict; shared by the goal form, its warning and the route preview. */
function FeasibilitySummary({ value }: { value: FeasibilityFields }) {
  return <div className={`rounded-xl border p-4 text-sm ${value.feasibilityStatus === "InsufficientTime" ? "border-rose-300 bg-rose-50 text-rose-950" : value.feasibilityStatus === "TightBudget" ? "border-amber-300 bg-amber-50 text-amber-950" : "border-emerald-200 bg-emerald-50 text-emerald-950"}`}>
    <dl className="grid gap-2 sm:grid-cols-2">
      <div><dt className="font-semibold">Điểm hiện tại ước tính</dt><dd>{value.currentEstimatedScore != null ? `${value.currentEstimatedScore}/10` : "Chưa đủ dữ liệu"}</dd></div>
      {value.targetScore != null && <div><dt className="font-semibold">Điểm mục tiêu</dt><dd>{value.targetScore}/10</dd></div>}
      {!!value.requiredStudyMinutes && <div><dt className="font-semibold">Thời lượng cần</dt><dd>~{value.requiredStudyMinutes} phút ({value.minimumStudyDays ?? 0} ngày học)</dd></div>}
      {value.earliestFeasibleDate && <div><dt className="font-semibold">Ngày sớm nhất</dt><dd>{formatIsoDate(value.earliestFeasibleDate)}</dd></div>}
      {value.suggestedDeadline && <div><dt className="font-semibold">Ngày đề xuất</dt><dd>{formatIsoDate(value.suggestedDeadline)}</dd></div>}
      {value.achievableScore != null && value.feasibilityStatus === "InsufficientTime" && <div><dt className="font-semibold">Điểm đạt được tối đa với mốc đã chọn</dt><dd>{value.achievableScore}/10</dd></div>}
    </dl>
    <p className="mt-3">{value.feasibilityMessage}</p>
  </div>;
}
export function GoalAndProposalModal({ isOpen, onClose, subjectId, classId, onAccepted, onOpenBlockingMission, skills = [], subjectName = "môn đang học", suggestedSkillIds = [], recommendationSummary, automatic = false, insights, entryMode, initialMinutes = 15, initialDays = 5, initialGoalType, initialTitle, initialDeadline, initialSelfReportedPrerequisiteSkillIds = [], selfAssessedSkillIds = [], onPlacementStarted, placementReport, initialTargetScore, initialSchedule, initialPreferredPace }: Props) {
  const fromPlacement = entryMode === "FromPlacement";
  const cache = useQueryClient();
  const [recommendation] = useState(() => recommendLearningGoal(subjectName, skills, insights));
  const [title, setTitle] = useState(entryMode === "Update" && initialGoalType !== "Placement" && initialTitle ? initialTitle : entryMode === "Placement" ? `Tìm điểm xuất phát · ${subjectName}` : entryMode ? `Kế hoạch học ${subjectName}` : automatic ? recommendation.title : `Kế hoạch học ${subjectName} của tôi`);
  const [goalType, setGoalType] = useState(initialGoalType && initialGoalType !== "Placement" ? initialGoalType : entryMode === "Foundation" ? "Foundation" : automatic ? recommendation.goalType : "KeepUp");
  const [placement, setPlacement] = useState(entryMode === "Placement" || entryMode === "Start");
  const [targetScore, setTargetScore] = useState(initialTargetScore == null ? "" : String(initialTargetScore));
  const [pace, setPace] = useState(initialPreferredPace ?? "Moderate");
  const [schedule, setSchedule] = useState<LearningScheduleSettings>(initialSchedule ?? {studyDays: [], excludedDates: [], timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone || "Asia/Ho_Chi_Minh", reviewReservePercent: 20, shortCheckDays: 45, mockExamDays: 14});
  const [minutes, setMinutes] = useState(Math.max(MIN_GOAL_STUDY_MINUTES, initialMinutes));
  const [days, setDays] = useState(initialDays);
  const [deadline, setDeadline] = useState(() => {
    if (!initialDeadline) return "";
    const date = new Date(initialDeadline);
    const iso = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
    return iso >= todayIso() ? iso : "";
  });
  // A date the student picked (or kept from the current goal) is checked as is; otherwise the suggested date is filled in.
  const [deadlineChosen, setDeadlineChosen] = useState(deadline !== "");
  const [targets, setTargets] = useState<string[]>(suggestedSkillIds.length ? suggestedSkillIds : entryMode
    ? groupLearningSkills(skills).flatMap(g => g.skills.map(s => s.skillId)) : automatic ? recommendation.targetSkillIds : []);
  const missing = missingLearningPrerequisites(skills, targets);
  const scopeKey = [...targets].sort().join(",");
  const restoreSelfReport = missing.length > 0 && missing.every(s => initialSelfReportedPrerequisiteSkillIds.includes(s.skillId));
  const [acknowledgedScope, setAcknowledgedScope] = useState(restoreSelfReport ? scopeKey : "");
  const [prerequisiteChoice, setPrerequisiteChoice] = useState<"Include" | "Skip" | "">(restoreSelfReport ? "Skip" : "");
  const needsAcknowledgement = missing.length > 0 && (acknowledgedScope !== scopeKey || !prerequisiteChoice);
  const [proposal, setProposal] = useState<PathProposalDto | null>(null);
  const [sourceProposal, setSourceProposal] = useState<PathProposalDto | null>(null);
  const contentRef = useRef<HTMLDivElement>(null);
  const [customizing, setCustomizing] = useState(false);
  const [editing, setEditing] = useState(!fromPlacement && (!!entryMode || !automatic));
  const started = useRef(false);
  // From a finished placement check the scope is already decided: keep the self-reported prerequisites and
  // treat every untested part as self-assessed (confirmed by a short review at the end of the route).
  const selfReported = fromPlacement && !editing ? missing.map(s => s.skillId).filter(id => initialSelfReportedPrerequisiteSkillIds.includes(id))
    : !needsAcknowledgement && prerequisiteChoice === "Skip" ? missing.map(s => s.skillId) : [];
  const selfAssessed = placement ? [] : selfAssessedSkillIds.filter(id => !targets.includes(id) && !selfReported.includes(id));
  const goalRequest = (end: string): CreateLearningGoalRequest => ({ subjectId, classId, title, goalType, targetSkillIds: targets,
    startWithPlacement: placement, targetScore: placement || targetScore === "" ? null : Number(targetScore), schedule: { ...schedule, studyDays: schedule.studyDays.length ? schedule.studyDays : [1,2,3,4,5,6,0].slice(0,days) },
    clearDeadline: placement ? true : !!initialDeadline && !end,
    selfReportedPrerequisiteSkillIds: selfReported, selfAssessedSkillIds: selfAssessed,
    availableMinutesPerDay: minutes, availableDaysPerWeek: days, preferredMode: "Balanced", preferredPace: pace,
    deadlineDate: !placement && end ? new Date(`${end}T23:59:59`).toISOString() : undefined });

  // Deadline check while the form is filled in, from the same planner as the preview (nothing is saved). Without a
  // date picked by the student the server proposes one, which is filled in; a picked date is checked right away.
  const today = todayIso();
  const pastDeadline = !!deadline && deadline < today;
  const targetValue = targetScore === "" ? null : Number(targetScore);
  const canEstimate = isOpen && editing && !customizing && !placement && targets.length > 0 && !needsAcknowledgement
    && (targetValue == null || (Number.isFinite(targetValue) && targetValue >= 0 && targetValue <= 10))
    && (!deadlineChosen || (!!deadline && !pastDeadline));
  // The title does not change the estimate, so it is left out and typing it does not recalculate anything.
  const estimateKey = canEstimate ? JSON.stringify({ ...goalRequest(deadlineChosen ? deadline : ""), title: "", clearDeadline: !deadlineChosen }) : "";
  const [debouncedEstimateKey, setDebouncedEstimateKey] = useState(estimateKey);
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedEstimateKey(estimateKey), 500);
    return () => clearTimeout(timer);
  }, [estimateKey]);
  const estimate = useQuery({ queryKey: ["learning-goal-estimate", debouncedEstimateKey],
    queryFn: ({ signal }) => estimateLearningGoal(JSON.parse(debouncedEstimateKey) as CreateLearningGoalRequest, signal),
    enabled: !!debouncedEstimateKey, placeholderData: keepPreviousData, staleTime: 60_000, retry: false });
  const estimateIsCurrent = !!estimateKey && debouncedEstimateKey === estimateKey;
  const estimateCurrent = estimateIsCurrent && !estimate.isPlaceholderData ? estimate.data ?? null : null;
  const estimateError = estimateIsCurrent && estimate.isError ? estimate.error : null;
  const estimating = !!estimateKey && !estimateCurrent && !estimateError;
  const suggestedFill = !deadlineChosen && estimateCurrent?.deadlineSuggested ? estimateCurrent.deadlineDate ?? "" : "";
  useEffect(() => { if (suggestedFill) setDeadline(suggestedFill); }, [suggestedFill]);
  const infeasibleDeadline = !!deadline && estimateCurrent?.feasibilityStatus === "InsufficientTime";
  // Same rule as the preview: with a target score, a route that cannot reach it in time cannot be chosen.
  const blockedByDeadline = infeasibleDeadline && estimateCurrent?.targetScore != null;
  // Warn once per picked date; the details stay under the field until the date or the plan changes.
  const [warnedDeadline, setWarnedDeadline] = useState("");
  const [warningOpen, setWarningOpen] = useState(false);
  useEffect(() => {
    if (infeasibleDeadline && deadlineChosen && warnedDeadline !== deadline) { setWarnedDeadline(deadline); setWarningOpen(true); }
  }, [infeasibleDeadline, deadlineChosen, deadline, warnedDeadline]);
  const applySuggestedDeadline = () => {
    if (estimateCurrent?.suggestedDeadline) setDeadline(estimateCurrent.suggestedDeadline);
    setDeadlineChosen(false); setWarningOpen(false);
  };
  const suggestionBasis = [estimateCurrent?.requiredStudyMinutes ? `cần khoảng ${estimateCurrent.requiredStudyMinutes} phút ≈ ${estimateCurrent.minimumStudyDays ?? 0} ngày học` : "",
    estimateCurrent?.earliestFeasibleDate ? `sớm nhất ${formatIsoDate(estimateCurrent.earliestFeasibleDate)}` : ""].filter(Boolean).join("; ");
  const pickAnotherDeadline = () => { setWarningOpen(false); setTimeout(() => document.getElementById("goal-deadline")?.focus(), 0); };

  // A route preview is laid out against a real end date, so one is needed first (placement has none).
  const needsDeadline = !placement && !deadline;
  const submitBlockReason = !targets.length ? "Chọn ít nhất một chủ đề hoặc kỹ năng để tiếp tục."
    : needsAcknowledgement ? "Chọn cách xử lý các phần nền tảng ở trên để tiếp tục."
    : needsDeadline ? (!deadlineChosen && estimating ? "Đang tính ngày đề xuất cho em…" : "Chọn \"Ngày thi / học tới ngày\" để xem trước lộ trình.")
    : placement ? null
    : pastDeadline ? `Ngày thi đã qua. Chọn từ ${formatIsoDate(today)} trở đi.`
    : blockedByDeadline ? `Ngày thi đã chọn không đủ thời gian để đạt điểm mục tiêu. Chọn từ ${formatIsoDate(estimateCurrent?.earliestFeasibleDate) || "ngày sớm nhất"} trở đi${estimateCurrent?.suggestedDeadline ? ` (đề xuất ${formatIsoDate(estimateCurrent.suggestedDeadline)})` : ""}, tăng thời gian học hoặc hạ điểm mục tiêu.`
    : null;

  const generate = useMutation({ mutationFn: async () => {
    const goal = await createOrUpdateLearningGoal(goalRequest(deadline));
    const value = await generatePathProposal({ subjectId, classId, goalId: goal.goalId });
    // Placement starts right away: the chosen parts open as one test, section after section.
    const accepted = value.isPlacement ? await acceptPathProposal(value.proposalId) : undefined;
    return { value, accepted };
  }, onSuccess: async ({ value, accepted }) => {
    if (accepted) {
      await Promise.all([cache.invalidateQueries({ queryKey: ["learning-agenda"] }), cache.invalidateQueries({ queryKey: ["learning-paths"] })]);
      onAccepted(accepted); onPlacementStarted?.(); onClose(); return;
    }
    setProposal(value); setSourceProposal(value); setEditing(false); contentRef.current?.scrollTo({ top: 0 }); } });
  const generateProposal = generate.mutate;
  useEffect(() => {
    if ((fromPlacement || (automatic && !entryMode)) && isOpen && !started.current) { started.current = true; generateProposal(); }
  }, [automatic, entryMode, fromPlacement, isOpen, generateProposal]);
  const accept = useMutation({ mutationFn: () => acceptPathProposal(proposal!.proposalId), onSuccess: async res => {
    await Promise.all([cache.invalidateQueries({ queryKey: ["learning-agenda"] }), cache.invalidateQueries({ queryKey: ["learning-paths"] })]);
    onAccepted(res); onClose();
  } });
  const customize = useMutation({ mutationFn: (request: CustomizePathProposalRequest) => customizePathProposal(sourceProposal!.proposalId, request),
    onSuccess: value => {
      setProposal(value); setMinutes(Math.max(MIN_GOAL_STUDY_MINUTES, value.availableMinutesPerDay ?? minutes)); setDays(value.availableDaysPerWeek ?? days);
      const date = value.deadlineDate ? new Date(value.deadlineDate) : null;
      setDeadline(date ? `${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,"0")}-${String(date.getDate()).padStart(2,"0")}` : ""); setDeadlineChosen(!!date);
      setTargets(value.milestones.flatMap(m => m.skillIds)); setCustomizing(false); contentRef.current?.scrollTo({ top: 0 });
    } });
  if (!isOpen) return null;
  const blockers = pathChangeBlockersFromError(accept.error);
  const assessedTargets = skills.filter(s => targets.includes(s.skillId) && s.evidenceCount > 0 && s.masteryScore != null);
  const currentMasteryAverage = assessedTargets.length ? assessedTargets.reduce((sum, s) => sum + (s.masteryScore ?? 0), 0) / assessedTargets.length : null;
  const infeasibleWithTarget = !!proposal && proposal.feasibilityStatus === "InsufficientTime" && proposal.targetScore != null;
  const input = "mt-1 w-full rounded-lg border border-slate-300 bg-white p-2.5 dark:bg-slate-900";
  return <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
    <section role="dialog" aria-modal="true" aria-labelledby="goal-title" className="flex max-h-[90vh] w-full max-w-3xl flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white p-6 text-slate-900 shadow-2xl dark:bg-slate-900 dark:text-slate-100">
      <header className="flex shrink-0 items-start justify-between gap-4 border-b border-slate-100 pb-4"><div><h2 id="goal-title" className="text-2xl font-semibold">{customizing ? "Tùy chỉnh tuyến học" : placement ? "Kiểm tra đầu vào" : editing ? "Mục tiêu học" : fromPlacement ? "Lộ trình đề xuất từ kết quả kiểm tra ·" : "Tuyến học đề xuất"} {subjectName}</h2>
        <p className="mt-2 text-sm text-slate-500">{placement ? "Chọn phần muốn kiểm tra. Bấm bắt đầu là làm ngay; các phần nối tiếp nhau trong một bài." : fromPlacement && !editing ? "Lộ trình lập từ kết quả kiểm tra đầu vào. Em có thể tùy chỉnh phần học, số câu và lịch học trước khi chọn." : entryMode === "Update" ? "Giữ phạm vi đã chọn và lập tuyến mới từ bài làm hiện có. Em có thể điều chỉnh trước khi xác nhận." : automatic ? "Mục tiêu, nội dung và lịch học đã được đề xuất sẵn. Xem lại hoặc chỉnh sửa, rồi chọn tuyến khi phù hợp." : "Chọn phạm vi của bạn. Xem trước chưa thay tuyến đang học; chỉ kích hoạt khi bạn xác nhận."}</p></div>
        <button onClick={onClose} aria-label="Đóng" className="rounded-lg border px-3 py-1">×</button></header>
      <div ref={contentRef} className="min-h-0 overflow-y-auto pr-1">
      {generate.error && <p role="alert" className="mt-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-800">{extractProblemDetails(generate.error).message}</p>}
      {automatic && !entryMode && !customizing && <div className="mt-5 grid gap-3 sm:grid-cols-3">{[["Bài làm cho thấy", recommendation.observation], ["Đề xuất cải thiện", recommendation.improvement], ["Phần nên học", recommendation.focus]].map(([label, value]) => <div className="rounded-xl border border-indigo-100 bg-indigo-50 p-4 text-indigo-950" key={label}><h3 className="text-sm font-semibold">{label}</h3><p className="mt-2 text-sm leading-relaxed">{value}</p></div>)}</div>}
      {!automatic && recommendationSummary && <p className="mt-4 rounded-xl bg-indigo-50 p-4 text-sm text-indigo-900">{recommendationSummary}</p>}
      {generate.isPending && !editing && !proposal && <p role="status" className="mt-6 rounded-xl bg-slate-50 p-5 text-sm text-slate-600">Đang chuẩn bị tuyến học, số câu và lịch đề xuất cho bạn…</p>}
      {automatic && generate.error && !editing && <button className="mt-4 rounded-xl border px-4 py-3" onClick={() => generate.mutate()}>Thử tạo lại tuyến đề xuất</button>}
      {customizing && customize.error && <p role="alert" className="mt-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-800">{extractProblemDetails(customize.error).message}</p>}
      {customizing && proposal ? <ProposalCustomizationForm key={proposal.proposalId} proposal={proposal} availableMilestones={sourceProposal?.milestones} pending={customize.isPending} onSubmit={request => customize.mutate(request)} onCancel={() => { customize.reset(); setCustomizing(false); }} /> : editing ? <form className="mt-6 space-y-5" onSubmit={e => { e.preventDefault(); if (!submitBlockReason) generate.mutate(); }}>
        {entryMode === "Start" && <fieldset disabled={generate.isPending} className="space-y-3 rounded-xl border border-indigo-200 p-4 dark:border-indigo-800">
          <legend className="px-1 text-sm font-semibold">Bạn muốn bắt đầu thế nào?</legend>
          <label className="flex items-start gap-3 text-sm"><input className="mt-1" type="radio" name="path-start-mode" checked={placement} onChange={() => setPlacement(true)} /><span><strong className="block">Kiểm tra đầu vào · Đề xuất</strong><span className="text-slate-500">Tìm phần đã vững và phần cần học trước khi lập lộ trình.</span></span></label>
          <label className="flex items-start gap-3 text-sm"><input className="mt-1" type="radio" name="path-start-mode" checked={!placement} onChange={() => setPlacement(false)} /><span><strong className="block">Bỏ qua kiểm tra, học ngay</strong><span className="text-slate-500">Lập lộ trình từ dữ liệu hiện có và bổ sung đánh giá trong lúc học.</span></span></label>
        </fieldset>}
        <SkillScopeSelector skills={skills} selected={targets} onChange={setTargets} placement={placement} />
        {missing.length > 0 && <div className="space-y-3 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-950" role="note">
          <p>Các phần đã chọn có nền tảng liên quan chưa có kết quả xác nhận trên web: <strong>{missing.map(s => s.skillName).join(", ")}</strong>. Em có thể đã học những phần này ở trường. Hãy chọn cách phù hợp:</p>
          <label className="flex items-start gap-2"><input type="radio" name="prerequisite-choice" checked={acknowledgedScope === scopeKey && prerequisiteChoice === "Skip"} onChange={() => { setAcknowledgedScope(scopeKey); setPrerequisiteChoice("Skip"); }} />Em đã nắm nền tảng này, bỏ qua và chỉ học các phần đã chọn.</label>
          <label className="flex items-start gap-2"><input type="radio" name="prerequisite-choice" checked={acknowledgedScope === scopeKey && prerequisiteChoice === "Include"} onChange={() => { setAcknowledgedScope(scopeKey); setPrerequisiteChoice("Include"); }} />{placement ? "Thêm các nền tảng này vào kiểm tra đầu vào." : "Thêm các nền tảng này vào lộ trình để ôn lại."}</label>
          <p className="text-xs">Bỏ qua được ghi nhận theo tự xác nhận của em, không tự tính là đã nắm vững qua kiểm tra. Em có thể đổi lựa chọn khi cập nhật lộ trình.</p>
        </div>}
        {placement && <p className="rounded-xl bg-indigo-50 p-3 text-sm text-indigo-950">Bài kiểm tra gồm {targets.length} phần, khoảng {targets.length * 5} câu, mất khoảng {targets.length * 10} phút; câu xếp từ dễ đến khó trong từng phần. Có thể bỏ bớt phần đã chắc chắn ở danh sách trên. Mỗi phần dùng 3–5 câu mới có thể tự chấm. Phần thiếu câu được ghi chưa đủ dữ liệu; không chặn phần khác hoặc chặn tạo lộ trình. Mẫu ít có độ tin cậy thấp.</p>}
        {!placement && <details open><summary className="cursor-pointer text-base font-semibold text-slate-900 dark:text-slate-100">Mục tiêu và lịch học · {formatStudyMinutes(minutes)}/ngày, {days} ngày/tuần</summary><p className="mt-2 text-sm text-slate-500">Ngày thi và điểm mục tiêu giúp lộ trình xếp đúng theo ngày thật và chừa thời gian ôn, thi thử. Ngày thi / học tới ngày được tự điền theo đề xuất; em có thể đổi, ngày quá gần sẽ được cảnh báo ngay.</p><div className="mt-4 space-y-4">
        <label className="block text-sm font-medium">Tên mục tiêu<input required maxLength={200} className={input} value={title} onChange={e => setTitle(e.target.value)} /></label>
        <label className="block text-sm font-medium">Bạn muốn đạt điều gì?<select className={input} value={goalType} onChange={e => setGoalType(e.target.value)}>
          <option value="Foundation">Học lại nền tảng</option><option value="KeepUp">Theo kịp bài trên lớp</option><option value="Diagnostic">Vừa thử vừa học</option><option value="ExamPrep">Chuẩn bị bài kiểm tra</option><option value="Advanced">Học nâng cao</option>
        </select></label>
        <div className="grid gap-4 sm:grid-cols-2"><label className="text-sm font-medium">Điểm muốn đạt (thang 10)<input type="number" placeholder="Ví dụ: 8" min="0" max="10" step="0.1" className={input} value={targetScore} onChange={e => setTargetScore(e.target.value)} /></label>
          <label className="text-sm">Nhịp học<select className={input} value={pace} onChange={e => setPace(e.target.value)}><option value="Gentle">Nhẹ nhàng · bộ ngắn</option><option value="Moderate">Vừa phải</option><option value="Accelerated">Tập trung · bộ dài hơn</option></select></label></div>
        {targets.length > 0 && (currentMasteryAverage != null
          ? <p className="rounded-lg bg-slate-50 p-3 text-sm text-slate-700 dark:bg-slate-800 dark:text-slate-200">Điểm ước tính hiện tại (từ bản đồ năng lực, {assessedTargets.length}/{targets.length} phần đã chọn có bằng chứng): <strong>{(currentMasteryAverage / 10).toFixed(1)}/10</strong>. Thời gian học tối thiểu và ngày đích đề xuất được tính ngay ở phần ngày thi bên dưới; mốc quá ngắn sẽ không chọn được tuyến.</p>
          : <p className="rounded-lg bg-slate-50 p-3 text-sm text-slate-700 dark:bg-slate-800 dark:text-slate-200">Chưa đủ bằng chứng để ước tính điểm hiện tại cho các phần đã chọn — kiểm tra đầu vào sẽ cho con số này, và thời gian tối thiểu sẽ tính theo khối lượng nội dung.</p>)}
        <TimeBudgetSlider label="Thời gian học mỗi ngày" minValue={MIN_GOAL_STUDY_MINUTES} value={minutes} onChange={setMinutes} summary="Tối thiểu 10 phút/ngày để đủ làm một bộ luyện. Đây là trần thời gian mỗi ngày, dùng để xếp lịch thực tế. Khi không đủ thời gian, hệ thống sẽ chỉ rõ phần cần hoãn hoặc điều chỉnh." />
        <div className="grid gap-4 sm:grid-cols-2">
          <label className="text-sm">Ngày mỗi tuần<select className={input} value={days} onChange={e => { setDays(Number(e.target.value)); setSchedule(s => ({...s, studyDays: []})); }}>{[1, 2, 3, 4, 5, 6, 7].map(n => <option key={n}>{n}</option>)}</select></label>
          <label className="text-sm font-medium">Ngày thi / học tới ngày <span className="text-rose-600" aria-hidden="true">*</span><DateInput id="goal-deadline" required min={today} aria-invalid={needsDeadline || pastDeadline || blockedByDeadline || undefined} aria-describedby="goal-deadline-feedback" className={`${input}${blockedByDeadline || pastDeadline ? " border-rose-400" : needsDeadline ? " border-amber-400" : ""}`} value={deadline} onChange={value => { setDeadline(value); setDeadlineChosen(true); }} /></label></div>
        <div id="goal-deadline-feedback" aria-live="polite" className="space-y-2">
          {estimating && <p className="text-sm text-slate-500">{deadlineChosen ? "Đang kiểm tra ngày đã chọn…" : "Đang tính ngày đề xuất theo phạm vi và lịch học…"}</p>}
          {estimateError && <p className="text-sm text-amber-700">Chưa tính được ngày đề xuất: {extractProblemDetails(estimateError).message} Em vẫn có thể tự chọn ngày.</p>}
          {estimateCurrent && (!deadlineChosen && estimateCurrent.deadlineSuggested
            ? <p className="rounded-lg bg-indigo-50 p-3 text-sm text-indigo-950">Đã tự điền ngày đề xuất <strong>{formatIsoDate(estimateCurrent.deadlineDate)}</strong> theo phạm vi và lịch học{suggestionBasis ? ` (${suggestionBasis})` : ""}. Em có thể đổi sang ngày khác; ngày quá gần sẽ được cảnh báo.</p>
            : <FeasibilitySummary value={estimateCurrent} />)}
          {estimateCurrent && deadlineChosen && estimateCurrent.suggestedDeadline && estimateCurrent.suggestedDeadline !== deadline && <button type="button" className="text-sm font-semibold text-indigo-700 underline" onClick={applySuggestedDeadline}>Dùng ngày đề xuất {formatIsoDate(estimateCurrent.suggestedDeadline)}</button>}
        </div>
        <fieldset><legend className="text-sm font-medium">Ngày có thể học</legend><div className="mt-2 flex flex-wrap gap-2">{[1,2,3,4,5,6,0].map((day, i) => { const selectedDays = schedule.studyDays.length ? schedule.studyDays : [1,2,3,4,5,6,0].slice(0,days); return <label key={day} className="rounded-lg border px-3 py-2 text-sm"><input type="checkbox" checked={selectedDays.includes(day)} onChange={() => { const next = selectedDays.includes(day) ? selectedDays.filter(d => d !== day) : [...selectedDays, day]; if (!next.length) return; setDays(next.length); setSchedule(s => ({...s, studyDays: next})); }} /> {i === 6 ? "CN" : `T${i+2}`}</label>; })}</div></fieldset>
        <label className="block text-sm">Dự phòng ôn và thi thử<select className={input} value={schedule.reviewReservePercent} onChange={e => setSchedule(s => ({...s, reviewReservePercent: Number(e.target.value)}))}>{[15,20,25,30].map(n => <option key={n} value={n}>{n}% thời gian</option>)}</select></label>
        <label className="block text-sm">Chờ trước khi luyện lại câu đã làm<select className={input} value={schedule.questionReuseDelayDays ?? 2} onChange={e => setSchedule(s => ({...s, questionReuseDelayDays: Number(e.target.value)}))}>{[0,1,2,3,7].map(n => <option key={n} value={n}>{n === 0 ? "Có thể luyện lại ngay" : `${n} ngày`}</option>)}</select><span className="mt-1 block text-xs text-slate-500">Câu đã làm chỉ dùng để luyện/ôn; bài tự kiểm tra vẫn cần câu mới để xác nhận kỹ năng.</span></label>
        <p className="text-xs text-slate-500">Nhịp học và lịch ôn luôn nằm trong trần phút/ngày. Khi thiếu thời gian, hệ thống sẽ nêu phần bị hoãn và thời lượng còn thiếu.</p>
        </div></details>}
        {submitBlockReason && <p id="goal-submit-reason" role="status" className="text-sm text-amber-700">{submitBlockReason}</p>}
        <button type="submit" disabled={generate.isPending || !!submitBlockReason} aria-describedby={submitBlockReason ? "goal-submit-reason" : undefined} title={submitBlockReason ?? undefined} className="rounded-xl bg-indigo-600 px-5 py-3 font-semibold text-white disabled:opacity-50">{generate.isPending ? placement ? "Đang tạo bài kiểm tra…" : "Đang chuẩn bị…" : placement ? "Bắt đầu kiểm tra" : "Xem trước lộ trình"}</button>
      </form> : proposal && <div className="mt-6 space-y-5">
        {fromPlacement && placementReport && <section className="rounded-xl border border-indigo-200 p-4" aria-label="Kết quả kiểm tra đầu vào">
          <div className="flex items-start justify-between gap-3"><h3 className="font-semibold">Kết quả kiểm tra đầu vào</h3>
            {placementReport.scoreOutOfTen != null && <p className="text-lg font-bold text-indigo-700">{placementReport.scoreOutOfTen}/10</p>}</div>
          <ul className="mt-3 space-y-3">{placementReport.skills.map(s => <li key={s.skillId} className="text-sm">
            <p className="font-medium">{s.skillName}{s.scoreOutOfTen != null && <span className="ml-2 font-normal text-slate-500">{s.scoreOutOfTen}/10 · đúng {s.correctAnswers}/{s.answeredQuestions} câu</span>}</p>
            <p className="mt-1 text-slate-600 dark:text-slate-300">{s.recommendation}</p></li>)}</ul>
        </section>}<div className="rounded-xl border border-slate-200 p-4"><h3 className="font-semibold">{title}</h3><p className="mt-2 text-sm">{placement ? "Kiểm tra đầu vào" : goalType === "Advanced" ? "Học nâng cao" : goalType === "Diagnostic" ? "Xác định điểm xuất phát" : goalType === "Foundation" ? "Củng cố nền tảng" : goalType === "ExamPrep" ? "Chuẩn bị bài kiểm tra" : "Theo kịp bài trên lớp"} · Tổng quỹ {formatStudyMinutes(minutes)}/ngày · {days} ngày/tuần · {placement ? "5 câu mới / kỹ năng cần kiểm tra" : Object.keys(proposal.practiceQuestionsBySkill ?? {}).length ? "Số câu theo từng phần học" : `${proposal.practiceQuestionsPerSet ?? 6} câu/bộ luyện`}</p><p className="mt-2 text-sm text-slate-500">Mốc dự kiến: {deadline ? deadline.split("-").reverse().join("/") : "Theo tiến độ học"}. Đây là ước lượng có thể điều chỉnh.</p>{(!deadline || targetScore === "") && !placement && <p className="mt-2 rounded-lg bg-amber-50 p-2 text-sm text-amber-900">Chưa đặt {[targetScore === "" ? "điểm muốn đạt" : "", !deadline ? "ngày thi" : ""].filter(Boolean).join(" và ")}. <button type="button" className="font-semibold underline" onClick={() => { accept.reset(); setEditing(true); contentRef.current?.scrollTo({ top: 0 }); }}>Đặt ngay</button> để lộ trình xếp theo ngày thật và có mốc kiểm tra, thi thử.</p>}</div><p className="rounded-xl bg-indigo-50 p-4 text-sm text-indigo-900">{proposal.startingBaselineSummary}</p>
        <p className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">Đổi tuyến không xóa gì: mọi câu trả lời trước đây vẫn được giữ và dùng để quyết định kỹ năng nào cần học nhiều hơn, kỹ năng nào được rút gọn. Tuyến cũ chuyển vào “Lịch sử tuyến học”. Nhiệm vụ đang làm dở cần làm xong hoặc hủy lượt trước khi đổi; bài đang chờ chấm sẽ được giữ nguyên.</p>
        <FeasibilitySummary value={proposal} />
        {!!proposal.selfReportedPrerequisiteSkillIds?.length && <p className="rounded-xl border border-sky-200 bg-sky-50 p-3 text-sm text-sky-950">Bỏ qua theo tự xác nhận: {proposal.selfReportedPrerequisiteSkillIds.map(id => skills.find(s => s.skillId === id)?.skillName ?? id).join(", ")}. Các phần này không thêm vào bài kiểm tra hoặc tuyến học và chưa được đánh dấu nắm vững trên hệ thống.</p>}
        {!!proposal.selfAssessedSkillIds?.length && <p className="rounded-xl border border-sky-200 bg-sky-50 p-3 text-sm text-sky-950">Phần em tự đánh giá đã vững (không chọn kiểm tra): <strong>{proposal.selfAssessedSkillIds.map(id => skills.find(s => s.skillId === id)?.skillName ?? id).join(", ")}</strong>. Cuối lộ trình có một bài ôn ngắn cho mỗi phần để xác nhận; chưa đạt sẽ thêm bộ rèn lại.</p>}
        {proposal.milestones.map(m => <article key={m.order} className={`rounded-xl border p-4 ${m.isSelfAssessedCheck ? "border-sky-200" : "border-slate-200"}`}><h3 className="font-semibold">{m.order}. {m.title}{m.isSelfAssessedCheck && <span className="ml-2 rounded-full bg-sky-50 px-2 py-0.5 text-xs font-medium text-sky-800">Tự đánh giá đã vững · ôn xác nhận</span>}</h3>
          {proposal.confirmedSkills.some(id => m.skillIds.includes(id)) && <p className="mt-2 text-sm text-emerald-700">Đã có bằng chứng hợp lệ; phần cơ bản được rút gọn.</p>}
          <p className="mt-1 text-sm text-slate-500">{m.outcomeDescription}</p>
          {m.missions.some(a => a.purpose === "Practice" && a.estimatedSets) && <p className="mt-2 text-sm text-indigo-700">Dự kiến {Math.max(...m.missions.map(a => a.estimatedSets ?? 0))} bộ luyện cho kỹ năng này; sẽ điều chỉnh theo kết quả từng bộ.</p>}
          {proposal.practiceQuestionsBySkill?.[m.skillIds[0]] && <p className="mt-2 rounded-lg bg-indigo-50 px-3 py-2 text-sm text-indigo-900">Bộ luyện cho phần này: tối đa {proposal.practiceQuestionsBySkill[m.skillIds[0]]} câu/bộ, khoảng {proposal.practiceQuestionsBySkill[m.skillIds[0]] * 2} phút/bộ{m.missions.some(a => a.purpose === "Practice" && a.isProbe) ? " sau lượt thử nhanh" : ""}. Số câu thực tế phụ thuộc ngân hàng câu hỏi.</p>}
          <ul className="mt-3 space-y-2 text-sm">{m.missions.map(a => <li key={a.missionId}>{a.title} · {a.questionCount > 0 ? `${a.questionCount} câu · ` : ""}khoảng {a.estimatedMinutes} phút<p className="mt-1 text-xs text-slate-500">{a.selectionReason}</p>{a.canStart === false && <span className="ml-2 text-amber-700">Cần bổ sung nội dung</span>}</li>)}</ul></article>)}
      </div>}
      </div>
      {!editing && !customizing && proposal && <footer className="mt-4 max-h-[45vh] shrink-0 space-y-3 overflow-y-auto border-t border-slate-200 pt-4">
        {accept.error && <div role="alert" className="rounded-xl bg-rose-50 p-3 text-sm text-rose-800">
          <p className="font-semibold">Chưa chọn được tuyến mới</p>
          <p className="mt-1">{blockers.length ? "Cần xử lý các buổi dưới đây trước. Tuyến hiện tại và bài làm của bạn vẫn được giữ." : extractProblemDetails(accept.error).message}</p>
          {blockers.length > 0 && <ul className="mt-2 max-h-28 space-y-2 overflow-y-auto">{blockers.map(m => <li key={m.runId}>
            <p><span className="font-semibold">{m.title}</span> · {m.status === "Grading" ? "Đang chờ chấm" : m.status === "NeedsReview" ? "Chờ giáo viên duyệt" : "Đang làm dở"}</p>
            {m.canResume && onOpenBlockingMission && <button className="mt-1 font-semibold underline" onClick={() => onOpenBlockingMission(m)}>{m.status === "InProgress" ? "Mở để làm tiếp hoặc hủy lượt" : "Xem bài và trạng thái"}</button>}
          </li>)}</ul>}
        </div>}
        {accept.isPending && <p role="status" className="text-sm text-indigo-700">Đang lưu tuyến và cập nhật nhiệm vụ của bạn…</p>}
        <button disabled={accept.isPending} className="text-sm text-slate-600 underline disabled:opacity-50" onClick={() => { accept.reset(); setEditing(true); contentRef.current?.scrollTo({ top: 0 }); }}>Thay đổi mục tiêu học</button>
        {infeasibleWithTarget && <p role="alert" className="rounded-lg bg-rose-50 p-3 text-sm text-rose-800">Chưa thể chọn tuyến này: mốc thời gian không đủ để đạt điểm mục tiêu. Hãy bấm "Tùy chỉnh tuyến học" để dời mốc/tăng thời gian, hoặc "Thay đổi mục tiêu học".</p>}
        <div className="flex flex-wrap gap-3">{!proposal.isPlacement && <button disabled={accept.isPending} className="rounded-xl border px-4 py-3 disabled:opacity-50" onClick={() => { accept.reset(); customize.reset(); setCustomizing(true); contentRef.current?.scrollTo({ top: 0 }); }}>Tùy chỉnh tuyến học</button>}
          <button disabled={accept.isPending || infeasibleWithTarget} className="rounded-xl bg-indigo-600 px-5 py-3 font-semibold text-white disabled:opacity-50" onClick={() => accept.mutate()}>{accept.isPending ? "Đang kích hoạt…" : accept.error ? "Thử lại" : proposal.isPlacement ? "Bắt đầu kiểm tra đầu vào" : "Chọn tuyến học này"}</button></div>
      </footer>}
      {warningOpen && editing && estimateCurrent && infeasibleDeadline && <div className="fixed inset-0 z-[60] flex items-center justify-center bg-slate-900/40 p-4">
        <section role="alertdialog" aria-modal="true" aria-labelledby="deadline-warning-title" onKeyDown={e => { if (e.key === "Escape") pickAnotherDeadline(); }}
          className="flex max-h-[85vh] w-full max-w-xl flex-col overflow-hidden rounded-2xl border border-rose-200 bg-white p-6 text-slate-900 shadow-2xl dark:bg-slate-900 dark:text-slate-100">
          <h3 id="deadline-warning-title" className="text-lg font-semibold text-rose-700 dark:text-rose-300">Ngày {formatIsoDate(deadline)} chưa đủ thời gian</h3>
          <p className="mt-1 text-sm text-slate-600 dark:text-slate-300">{estimateCurrent.targetScore != null ? "Với lịch học hiện tại, mốc này không đủ để đạt điểm mục tiêu nên chưa xem trước được lộ trình." : "Với lịch học hiện tại, mốc này không đủ để học hết phần đã chọn."} Hãy dời ngày, tăng thời gian học mỗi ngày/số ngày mỗi tuần hoặc hạ điểm mục tiêu.</p>
          <div className="mt-4 min-h-0 overflow-y-auto"><FeasibilitySummary value={estimateCurrent} /></div>
          <div className="mt-5 flex flex-wrap justify-end gap-3">
            <button type="button" autoFocus className="rounded-xl border px-4 py-2.5" onClick={pickAnotherDeadline}>Chọn ngày khác</button>
            {estimateCurrent.suggestedDeadline && <button type="button" className="rounded-xl bg-indigo-600 px-4 py-2.5 font-semibold text-white" onClick={applySuggestedDeadline}>Dùng ngày đề xuất {formatIsoDate(estimateCurrent.suggestedDeadline)}</button>}
          </div>
        </section>
      </div>}
    </section>
  </div>;
}
