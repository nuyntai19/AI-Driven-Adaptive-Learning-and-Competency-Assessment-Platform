import { useEffect, useRef, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createOrUpdateLearningGoal, generatePathProposal, acceptPathProposal, customizePathProposal } from "../../api/learningPathApi";
import type { CustomizePathProposalRequest, PathProposalDto, PlacementReportDto, AcceptPathProposalResponse, SkillCapabilityStatusDto, LearningInsightsDto } from "../../types/learningPath";
import { extractProblemDetails } from "../../utils/problemDetails";
import { recommendLearningGoal } from "../../utils/learningPathRecommendation";
import { pathChangeBlockersFromError, type BlockingLearningMission } from "../../utils/learningMission";
import { ProposalCustomizationForm } from "./ProposalCustomizationForm";
import { TimeBudgetSlider } from "./TimeBudgetSlider";
import { formatStudyMinutes } from "../../utils/studyTime";
import { groupLearningSkills, missingLearningPrerequisites } from "../../utils/learningScope";
import { SkillScopeSelector } from "./SkillScopeSelector";

interface Props { isOpen: boolean; onClose: () => void; subjectId: string; classId?: string;
  onOpenBlockingMission?: (mission: BlockingLearningMission) => void;
  onAccepted: (res: AcceptPathProposalResponse) => void; skills?: SkillCapabilityStatusDto[]; subjectName?: string;
  suggestedSkillIds?: string[]; recommendationSummary?: string; automatic?: boolean; insights?: LearningInsightsDto;
  entryMode?: "Placement" | "Foundation" | "Update" | "Next" | "FromPlacement";
  /** Parts left out of the placement check; the route ends with a short confirmation review for each. */
  selfAssessedSkillIds?: string[]; onPlacementStarted?: () => void; placementReport?: PlacementReportDto | null;
  initialMinutes?: number; initialDays?: number; initialGoalType?: string; initialTitle?: string; initialDeadline?: string | null; initialSelfReportedPrerequisiteSkillIds?: string[] }
export function GoalAndProposalModal({ isOpen, onClose, subjectId, classId, onAccepted, onOpenBlockingMission, skills = [], subjectName = "môn đang học", suggestedSkillIds = [], recommendationSummary, automatic = false, insights, entryMode, initialMinutes = 15, initialDays = 5, initialGoalType, initialTitle, initialDeadline, initialSelfReportedPrerequisiteSkillIds = [], selfAssessedSkillIds = [], onPlacementStarted, placementReport }: Props) {
  const fromPlacement = entryMode === "FromPlacement";
  const cache = useQueryClient();
  const [recommendation] = useState(() => recommendLearningGoal(subjectName, skills, insights));
  const [title, setTitle] = useState(entryMode === "Update" && initialGoalType !== "Placement" && initialTitle ? initialTitle : entryMode === "Placement" ? `Tìm điểm xuất phát · ${subjectName}` : entryMode ? `Kế hoạch học ${subjectName}` : automatic ? recommendation.title : `Kế hoạch học ${subjectName} của tôi`);
  const [goalType, setGoalType] = useState(entryMode === "Placement" ? "Placement" : entryMode === "Foundation" ? "Foundation" : entryMode === "Update" && initialGoalType && initialGoalType !== "Placement" ? initialGoalType : entryMode ? "KeepUp" : automatic ? recommendation.goalType : "Foundation");
  const [minutes, setMinutes] = useState(initialMinutes);
  const [days, setDays] = useState(initialDays);
  const [deadline, setDeadline] = useState(() => {
    if (entryMode !== "Update" || !initialDeadline) return "";
    const date = new Date(initialDeadline);
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
  });
  const [targets, setTargets] = useState<string[]>(suggestedSkillIds.length ? suggestedSkillIds : entryMode
    ? groupLearningSkills(skills)[0]?.skills.map(s => s.skillId) ?? [] : automatic ? recommendation.targetSkillIds : []);
  const missing = missingLearningPrerequisites(skills, targets);
  const scopeKey = [...targets].sort().join(",");
  const restoreSelfReport = missing.length > 0 && missing.every(s => initialSelfReportedPrerequisiteSkillIds.includes(s.skillId));
  const [acknowledgedScope, setAcknowledgedScope] = useState(restoreSelfReport ? scopeKey : "");
  const [prerequisiteChoice, setPrerequisiteChoice] = useState<"Include" | "Skip" | "">(restoreSelfReport ? "Skip" : "");
  const needsAcknowledgement = missing.length > 0 && (acknowledgedScope !== scopeKey || !prerequisiteChoice);
  const placement = goalType === "Placement";
  const [proposal, setProposal] = useState<PathProposalDto | null>(null);
  const [sourceProposal, setSourceProposal] = useState<PathProposalDto | null>(null);
  const contentRef = useRef<HTMLDivElement>(null);
  const [customizing, setCustomizing] = useState(false);
  const [editing, setEditing] = useState(!fromPlacement && (!!entryMode || !automatic));
  const started = useRef(false);
  const [placementBlocked, setPlacementBlocked] = useState("");
  // From a finished placement check the scope is already decided: keep the self-reported prerequisites and
  // treat every untested part as self-assessed (confirmed by a short review at the end of the route).
  const selfReported = fromPlacement && !editing ? missing.map(s => s.skillId).filter(id => initialSelfReportedPrerequisiteSkillIds.includes(id))
    : !needsAcknowledgement && prerequisiteChoice === "Skip" ? missing.map(s => s.skillId) : [];
  const selfAssessed = placement ? [] : selfAssessedSkillIds.filter(id => !targets.includes(id) && !selfReported.includes(id));
  const generate = useMutation({ mutationFn: async () => {
    const goal = await createOrUpdateLearningGoal({ subjectId, classId, title, goalType, targetSkillIds: targets,
      selfReportedPrerequisiteSkillIds: selfReported, selfAssessedSkillIds: selfAssessed,
      availableMinutesPerDay: minutes, availableDaysPerWeek: days, preferredMode: "Balanced", preferredPace: "Moderate",
      deadlineDate: deadline ? new Date(`${deadline}T23:59:59`).toISOString() : undefined });
    const value = await generatePathProposal({ subjectId, classId, goalId: goal.goalId });
    // Placement starts right away: the chosen parts open as one test, section after section.
    // Nothing to test (every chosen part lacks fresh questions): keep the current route and say why.
    const blocked = value.isPlacement && !value.milestones.some(m => m.missions.some(x => x.canStart !== false));
    const accepted = value.isPlacement && !blocked ? await acceptPathProposal(value.proposalId) : undefined;
    return { value, accepted, blocked };
  }, onSuccess: async ({ value, accepted, blocked }) => {
    if (blocked) {
      const names = value.milestones.filter(m => m.missions.length > 0).map(m => m.title);
      setPlacementBlocked(`Chưa đủ câu hỏi mới để kiểm tra ${names.length ? names.join(", ") : "các phần đã chọn"} (cần 5 câu chưa làm cho mỗi phần). Lộ trình hiện tại được giữ nguyên. Hãy chọn phần khác hoặc báo giáo viên bổ sung câu hỏi.`);
      return;
    }
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
      setProposal(value); setMinutes(value.availableMinutesPerDay ?? minutes); setDays(value.availableDaysPerWeek ?? days);
      const date = value.deadlineDate ? new Date(value.deadlineDate) : null;
      setDeadline(date ? `${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,"0")}-${String(date.getDate()).padStart(2,"0")}` : "");
      setTargets(value.milestones.flatMap(m => m.skillIds)); setCustomizing(false); contentRef.current?.scrollTo({ top: 0 });
    } });
  if (!isOpen) return null;
  const blockers = pathChangeBlockersFromError(accept.error);
  const input = "mt-1 w-full rounded-lg border border-slate-300 bg-white p-2.5 dark:bg-slate-900";
  return <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
    <section role="dialog" aria-modal="true" aria-labelledby="goal-title" className="flex max-h-[90vh] w-full max-w-3xl flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white p-6 text-slate-900 shadow-2xl dark:bg-slate-900 dark:text-slate-100">
      <header className="flex shrink-0 items-start justify-between gap-4 border-b border-slate-100 pb-4"><div><h2 id="goal-title" className="text-2xl font-semibold">{customizing ? "Tùy chỉnh tuyến học" : placement ? "Kiểm tra đầu vào" : editing ? "Mục tiêu học" : fromPlacement ? "Lộ trình đề xuất từ kết quả kiểm tra ·" : "Tuyến học đề xuất"} {subjectName}</h2>
        <p className="mt-2 text-sm text-slate-500">{placement ? "Chọn phần muốn kiểm tra. Bấm bắt đầu là làm ngay; các phần nối tiếp nhau trong một bài." : fromPlacement && !editing ? "Lộ trình lập từ kết quả kiểm tra đầu vào. Em có thể tùy chỉnh phần học, số câu và lịch học trước khi chọn." : entryMode === "Update" ? "Giữ phạm vi đã chọn và lập tuyến mới từ bài làm hiện có. Em có thể điều chỉnh trước khi xác nhận." : automatic ? "Mục tiêu, nội dung và lịch học đã được đề xuất sẵn. Xem lại hoặc chỉnh sửa, rồi chọn tuyến khi phù hợp." : "Chọn phạm vi của bạn. Xem trước chưa thay tuyến đang học; chỉ kích hoạt khi bạn xác nhận."}</p></div>
        <button onClick={onClose} aria-label="Đóng" className="rounded-lg border px-3 py-1">×</button></header>
      <div ref={contentRef} className="min-h-0 overflow-y-auto pr-1">
      {placementBlocked && placement && <p role="alert" className="mt-4 rounded-lg bg-amber-50 p-3 text-sm text-amber-900">{placementBlocked}</p>}
      {generate.error && <p role="alert" className="mt-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-800">{extractProblemDetails(generate.error).message}</p>}
      {automatic && !entryMode && !customizing && <div className="mt-5 grid gap-3 sm:grid-cols-3">{[["Bài làm cho thấy", recommendation.observation], ["Đề xuất cải thiện", recommendation.improvement], ["Phần nên học", recommendation.focus]].map(([label, value]) => <div className="rounded-xl border border-indigo-100 bg-indigo-50 p-4 text-indigo-950" key={label}><h3 className="text-sm font-semibold">{label}</h3><p className="mt-2 text-sm leading-relaxed">{value}</p></div>)}</div>}
      {!automatic && recommendationSummary && <p className="mt-4 rounded-xl bg-indigo-50 p-4 text-sm text-indigo-900">{recommendationSummary}</p>}
      {generate.isPending && !editing && !proposal && <p role="status" className="mt-6 rounded-xl bg-slate-50 p-5 text-sm text-slate-600">Đang chuẩn bị tuyến học, số câu và lịch đề xuất cho bạn…</p>}
      {automatic && generate.error && !editing && <button className="mt-4 rounded-xl border px-4 py-3" onClick={() => generate.mutate()}>Thử tạo lại tuyến đề xuất</button>}
      {customizing && customize.error && <p role="alert" className="mt-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-800">{extractProblemDetails(customize.error).message}</p>}
      {customizing && proposal ? <ProposalCustomizationForm key={proposal.proposalId} proposal={proposal} availableMilestones={sourceProposal?.milestones} pending={customize.isPending} onSubmit={request => customize.mutate(request)} onCancel={() => { customize.reset(); setCustomizing(false); }} /> : editing ? <form className="mt-6 space-y-5" onSubmit={e => { e.preventDefault(); if (targets.length && !needsAcknowledgement) generate.mutate(); }}>
        <SkillScopeSelector skills={skills} selected={targets} onChange={ids => { setPlacementBlocked(""); setTargets(ids); }} placement={placement} />
        {missing.length > 0 && <div className="space-y-3 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-950" role="note">
          <p>Các phần đã chọn có nền tảng liên quan chưa có kết quả xác nhận trên web: <strong>{missing.map(s => s.skillName).join(", ")}</strong>. Em có thể đã học những phần này ở trường. Hãy chọn cách phù hợp:</p>
          <label className="flex items-start gap-2"><input type="radio" name="prerequisite-choice" checked={acknowledgedScope === scopeKey && prerequisiteChoice === "Skip"} onChange={() => { setAcknowledgedScope(scopeKey); setPrerequisiteChoice("Skip"); }} />Em đã nắm nền tảng này, bỏ qua và chỉ học các phần đã chọn.</label>
          <label className="flex items-start gap-2"><input type="radio" name="prerequisite-choice" checked={acknowledgedScope === scopeKey && prerequisiteChoice === "Include"} onChange={() => { setAcknowledgedScope(scopeKey); setPrerequisiteChoice("Include"); }} />{placement ? "Thêm các nền tảng này vào kiểm tra đầu vào." : "Thêm các nền tảng này vào lộ trình để ôn lại."}</label>
          <p className="text-xs">Bỏ qua được ghi nhận theo tự xác nhận của em, không tự tính là đã nắm vững qua kiểm tra. Em có thể đổi lựa chọn khi cập nhật lộ trình.</p>
        </div>}
        {placement && <p className="rounded-xl bg-indigo-50 p-3 text-sm text-indigo-950">Mỗi phần đã chọn có 5 câu mới, khoảng 10 phút · tổng khoảng {10 * skills.filter(s => targets.includes(s.skillId)).length} phút. Các phần nối tiếp nhau; em có thể lưu và làm tiếp sau. Làm xong sẽ có kết quả, nhận xét và lộ trình đề xuất.</p>}
        {!placement && <details open={!entryMode}><summary className="cursor-pointer text-sm font-medium text-indigo-600">Điều chỉnh mục tiêu và lịch học · {formatStudyMinutes(minutes)}/ngày, {days} ngày/tuần</summary><div className="mt-4 space-y-4">
        <label className="block text-sm font-medium">Tên mục tiêu<input required maxLength={200} className={input} value={title} onChange={e => setTitle(e.target.value)} /></label>
        <label className="block text-sm font-medium">Bạn muốn đạt điều gì?<select className={input} value={goalType} onChange={e => setGoalType(e.target.value)}>
          <option value="Foundation">Học lại nền tảng</option><option value="KeepUp">Theo kịp bài trên lớp</option><option value="Placement">Kiểm tra đầu vào và tìm điểm xuất phát</option><option value="Diagnostic">Vừa thử vừa học</option><option value="ExamPrep">Chuẩn bị bài kiểm tra</option><option value="Advanced">Học nâng cao</option>
        </select></label>
        <TimeBudgetSlider label="Thời gian học mỗi ngày" value={minutes} onChange={setMinutes} summary="Dùng để ước lượng số tuần. Số câu của từng kỹ năng do mức độ yếu quyết định; mỗi ngày có thể kéo lại thời gian ở trang lộ trình." />
        <div className="grid gap-4 sm:grid-cols-2">
          <label className="text-sm">Ngày mỗi tuần<select className={input} value={days} onChange={e => setDays(Number(e.target.value))}>{[1, 2, 3, 4, 5, 6, 7].map(n => <option key={n}>{n}</option>)}</select></label>
          <label className="text-sm">Mốc thời gian (nếu có)<input type="date" className={input} value={deadline} onChange={e => setDeadline(e.target.value)} /></label></div>
        </div></details>}
        {!targets.length && <p className="text-sm text-amber-700">Chọn ít nhất một chủ đề hoặc kỹ năng để tiếp tục.</p>}
        <button type="submit" disabled={generate.isPending || targets.length === 0 || needsAcknowledgement} className="rounded-xl bg-indigo-600 px-5 py-3 font-semibold text-white disabled:opacity-50">{generate.isPending ? placement ? "Đang tạo bài kiểm tra…" : "Đang chuẩn bị…" : placement ? "Bắt đầu kiểm tra" : "Xem trước lộ trình"}</button>
      </form> : proposal && <div className="mt-6 space-y-5">
        {fromPlacement && placementReport && <section className="rounded-xl border border-indigo-200 p-4" aria-label="Kết quả kiểm tra đầu vào">
          <div className="flex items-start justify-between gap-3"><h3 className="font-semibold">Kết quả kiểm tra đầu vào</h3>
            {placementReport.scoreOutOfTen != null && <p className="text-lg font-bold text-indigo-700">{placementReport.scoreOutOfTen}/10</p>}</div>
          <ul className="mt-3 space-y-3">{placementReport.skills.map(s => <li key={s.skillId} className="text-sm">
            <p className="font-medium">{s.skillName}{s.scoreOutOfTen != null && <span className="ml-2 font-normal text-slate-500">{s.scoreOutOfTen}/10 · đúng {s.correctAnswers}/{s.answeredQuestions} câu</span>}</p>
            <p className="mt-1 text-slate-600 dark:text-slate-300">{s.recommendation}</p></li>)}</ul>
        </section>}<div className="rounded-xl border border-slate-200 p-4"><h3 className="font-semibold">{title}</h3><p className="mt-2 text-sm">{placement ? "Kiểm tra đầu vào" : goalType === "Advanced" ? "Học nâng cao" : goalType === "Diagnostic" ? "Xác định điểm xuất phát" : goalType === "Foundation" ? "Củng cố nền tảng" : goalType === "ExamPrep" ? "Chuẩn bị bài kiểm tra" : "Theo kịp bài trên lớp"} · Tổng quỹ {formatStudyMinutes(minutes)}/ngày · {days} ngày/tuần · {placement ? "5 câu mới / kỹ năng cần kiểm tra" : Object.keys(proposal.practiceQuestionsBySkill ?? {}).length ? "Số câu theo từng phần học" : `${proposal.practiceQuestionsPerSet ?? 6} câu/bộ luyện`}</p><p className="mt-2 text-sm text-slate-500">Mốc dự kiến: {deadline ? deadline.split("-").reverse().join("/") : "Theo tiến độ học"}. Đây là ước lượng có thể điều chỉnh.</p></div><p className="rounded-xl bg-indigo-50 p-4 text-sm text-indigo-900">{proposal.startingBaselineSummary}</p>
        <p className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">Đổi tuyến không xóa gì: mọi câu trả lời trước đây vẫn được giữ và dùng để quyết định kỹ năng nào cần học nhiều hơn, kỹ năng nào được rút gọn. Tuyến cũ chuyển vào “Lịch sử tuyến học”. Nhiệm vụ đang làm dở cần làm xong hoặc hủy lượt trước khi đổi; bài đang chờ chấm sẽ được giữ nguyên.</p>
        <p className="text-sm">{proposal.feasibilityMessage}</p>
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
        <div className="flex flex-wrap gap-3">{!proposal.isPlacement && <button disabled={accept.isPending} className="rounded-xl border px-4 py-3 disabled:opacity-50" onClick={() => { accept.reset(); customize.reset(); setCustomizing(true); contentRef.current?.scrollTo({ top: 0 }); }}>Tùy chỉnh tuyến học</button>}
          <button disabled={accept.isPending} className="rounded-xl bg-indigo-600 px-5 py-3 font-semibold text-white disabled:opacity-50" onClick={() => accept.mutate()}>{accept.isPending ? "Đang kích hoạt…" : accept.error ? "Thử lại" : proposal.isPlacement ? "Bắt đầu kiểm tra đầu vào" : "Chọn tuyến học này"}</button></div>
      </footer>}
    </section>
  </div>;
}
