import { useEffect, useRef, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createOrUpdateLearningGoal, generatePathProposal, acceptPathProposal, customizePathProposal } from "../../api/learningPathApi";
import type { CustomizePathProposalRequest, PathProposalDto, AcceptPathProposalResponse, SkillCapabilityStatusDto, LearningInsightsDto } from "../../types/learningPath";
import { extractProblemDetails } from "../../utils/problemDetails";
import { recommendLearningGoal } from "../../utils/learningPathRecommendation";
import { pathChangeBlockersFromError, type BlockingLearningMission } from "../../utils/learningMission";
import { ProposalCustomizationForm } from "./ProposalCustomizationForm";
import { TimeBudgetSlider } from "./TimeBudgetSlider";
import { formatStudyMinutes } from "../../utils/studyTime";

interface Props { isOpen: boolean; onClose: () => void; subjectId: string; classId?: string;
  onOpenBlockingMission?: (mission: BlockingLearningMission) => void;
  onAccepted: (res: AcceptPathProposalResponse) => void; skills?: SkillCapabilityStatusDto[]; subjectName?: string;
  suggestedSkillIds?: string[]; recommendationSummary?: string; automatic?: boolean; insights?: LearningInsightsDto }
export function GoalAndProposalModal({ isOpen, onClose, subjectId, classId, onAccepted, onOpenBlockingMission, skills = [], subjectName = "môn đang học", suggestedSkillIds = [], recommendationSummary, automatic = false, insights }: Props) {
  const cache = useQueryClient();
  const [recommendation] = useState(() => recommendLearningGoal(subjectName, skills, insights));
  const [title, setTitle] = useState(automatic ? recommendation.title : `Kế hoạch học ${subjectName} của tôi`);
  const [goalType, setGoalType] = useState(automatic ? recommendation.goalType : "Foundation");
  const [minutes, setMinutes] = useState(15);
  const [days, setDays] = useState(5);
  const [deadline, setDeadline] = useState(() => { if (!automatic) return ""; const date = new Date(); date.setDate(date.getDate() + 14); return `${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,"0")}-${String(date.getDate()).padStart(2,"0")}`; });
  const [targets, setTargets] = useState<string[]>(automatic ? recommendation.targetSkillIds : suggestedSkillIds);
  const [proposal, setProposal] = useState<PathProposalDto | null>(null);
  const [sourceProposal, setSourceProposal] = useState<PathProposalDto | null>(null);
  const contentRef = useRef<HTMLDivElement>(null);
  const [customizing, setCustomizing] = useState(false);
  const [editing, setEditing] = useState(!automatic);
  const started = useRef(false);
  const generate = useMutation({ mutationFn: async () => {
    const goal = await createOrUpdateLearningGoal({ subjectId, classId, title, goalType, targetSkillIds: targets,
      availableMinutesPerDay: minutes, availableDaysPerWeek: days, preferredMode: "Balanced", preferredPace: "Moderate",
      deadlineDate: deadline ? new Date(`${deadline}T23:59:59`).toISOString() : undefined });
    return generatePathProposal({ subjectId, classId, goalId: goal.goalId });
  }, onSuccess: value => { setProposal(value); setSourceProposal(value); setEditing(false); contentRef.current?.scrollTo({ top: 0 }); } });
  useEffect(() => {
    if (automatic && isOpen && !started.current) { started.current = true; generate.mutate(); }
  }, [automatic, isOpen, generate.mutate]);
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
  return <div className="fixed inset-0 z-50 flex items-center justify-center bg-transparent p-4">
    <section role="dialog" aria-modal="true" aria-labelledby="goal-title" className="flex max-h-[90vh] w-full max-w-3xl flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white p-6 text-slate-900 shadow-2xl dark:bg-slate-900 dark:text-slate-100">
      <header className="flex shrink-0 items-start justify-between gap-4 border-b border-slate-100 pb-4"><div><h2 id="goal-title" className="text-2xl font-semibold">{customizing ? "Tùy chỉnh tuyến học" : editing ? "Mục tiêu học" : "Tuyến học đề xuất"} {subjectName}</h2>
        <p className="mt-2 text-sm text-slate-500">{automatic ? "Mục tiêu, nội dung và lịch học đã được đề xuất sẵn. Xem lại hoặc chỉnh sửa, rồi chọn tuyến khi phù hợp." : "Chọn phạm vi của bạn. Xem trước chưa thay tuyến đang học; chỉ kích hoạt khi bạn xác nhận."}</p></div>
        <button onClick={onClose} aria-label="Đóng" className="rounded-lg border px-3 py-1">×</button></header>
      <div ref={contentRef} className="min-h-0 overflow-y-auto pr-1">
      {generate.error && <p role="alert" className="mt-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-800">{extractProblemDetails(generate.error).message}</p>}
      {automatic && !customizing && <div className="mt-5 grid gap-3 sm:grid-cols-3">{[["Bài làm cho thấy", recommendation.observation], ["Đề xuất cải thiện", recommendation.improvement], ["Phần nên học", recommendation.focus]].map(([label, value]) => <div className="rounded-xl border border-indigo-100 bg-indigo-50 p-4 text-indigo-950" key={label}><h3 className="text-sm font-semibold">{label}</h3><p className="mt-2 text-sm leading-relaxed">{value}</p></div>)}</div>}
      {!automatic && recommendationSummary && <p className="mt-4 rounded-xl bg-indigo-50 p-4 text-sm text-indigo-900">{recommendationSummary}</p>}
      {generate.isPending && !editing && <p role="status" className="mt-6 rounded-xl bg-slate-50 p-5 text-sm text-slate-600">Đang chuẩn bị tuyến học, số câu và lịch đề xuất cho bạn…</p>}
      {automatic && generate.error && !editing && <button className="mt-4 rounded-xl border px-4 py-3" onClick={() => generate.mutate()}>Thử tạo lại tuyến đề xuất</button>}
      {customizing && customize.error && <p role="alert" className="mt-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-800">{extractProblemDetails(customize.error).message}</p>}
      {customizing && proposal ? <ProposalCustomizationForm key={proposal.proposalId} proposal={proposal} availableMilestones={sourceProposal?.milestones} pending={customize.isPending} onSubmit={request => customize.mutate(request)} onCancel={() => { customize.reset(); setCustomizing(false); }} /> : editing ? <form className="mt-6 space-y-5" onSubmit={e => { e.preventDefault(); generate.mutate(); }}>
        <label className="block text-sm font-medium">Tên mục tiêu<input required maxLength={200} className={input} value={title} onChange={e => setTitle(e.target.value)} /></label>
        <label className="block text-sm font-medium">Bạn muốn đạt điều gì?<select className={input} value={goalType} onChange={e => setGoalType(e.target.value)}>
          <option value="Foundation">Học lại nền tảng</option><option value="KeepUp">Theo kịp bài trên lớp</option><option value="Diagnostic">Xác định điểm xuất phát</option><option value="ExamPrep">Chuẩn bị bài kiểm tra</option><option value="Advanced">Học nâng cao</option>
        </select></label>
        <TimeBudgetSlider label="Thời gian học mỗi ngày" value={minutes} onChange={setMinutes} summary="Dùng để ước lượng số tuần. Số câu của từng kỹ năng do mức độ yếu quyết định; mỗi ngày có thể kéo lại thời gian ở trang lộ trình." />
        <div className="grid gap-4 sm:grid-cols-2">
          <label className="text-sm">Ngày mỗi tuần<select className={input} value={days} onChange={e => setDays(Number(e.target.value))}>{[1, 2, 3, 4, 5, 6, 7].map(n => <option key={n}>{n}</option>)}</select></label>
          <label className="text-sm">Mốc thời gian (nếu có)<input type="date" className={input} value={deadline} onChange={e => setDeadline(e.target.value)} /></label></div>
        <fieldset><legend className="text-sm font-semibold">Kỹ năng trọng tâm</legend><p className="my-2 text-xs text-slate-500">Không chọn nghĩa là xem xét tất cả kỹ năng trong giáo trình. Nền tảng cần thiết sẽ được bổ sung vào tuyến.</p>
          <div className="grid max-h-56 gap-2 overflow-y-auto sm:grid-cols-2">{skills.map(s => <label className="flex items-start gap-2 rounded-lg border border-slate-200 p-3 text-sm" key={s.skillId}>
            <input type="checkbox" checked={targets.includes(s.skillId)} onChange={e => setTargets(v => e.target.checked ? [...v, s.skillId] : v.filter(id => id !== s.skillId))} /><span>{s.skillName}</span>
          </label>)}</div></fieldset>
        <button type="submit" disabled={generate.isPending || skills.length === 0} className="rounded-xl bg-indigo-600 px-5 py-3 font-semibold text-white disabled:opacity-50">{generate.isPending ? "Đang lập tuyến từ dữ liệu học…" : "Xem trước lộ trình"}</button>
      </form> : proposal && <div className="mt-6 space-y-5"><div className="rounded-xl border border-slate-200 p-4"><h3 className="font-semibold">{title}</h3><p className="mt-2 text-sm">{goalType === "Advanced" ? "Học nâng cao" : goalType === "Diagnostic" ? "Xác định điểm xuất phát" : goalType === "Foundation" ? "Củng cố nền tảng" : goalType === "ExamPrep" ? "Chuẩn bị bài kiểm tra" : "Theo kịp bài trên lớp"} · {formatStudyMinutes(minutes)}/ngày · {days} ngày/tuần · {proposal.practiceQuestionsPerSet ?? 6} câu/bộ luyện</p><p className="mt-2 text-sm text-slate-500">Mốc dự kiến: {deadline ? deadline.split("-").reverse().join("/") : "Theo tiến độ học"}. Đây là ước lượng có thể điều chỉnh.</p></div><p className="rounded-xl bg-indigo-50 p-4 text-sm text-indigo-900">{proposal.startingBaselineSummary}</p>
        <p className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">Đổi tuyến không xóa gì: mọi câu trả lời trước đây vẫn được giữ và dùng để quyết định kỹ năng nào cần học nhiều hơn, kỹ năng nào được rút gọn. Tuyến cũ chuyển vào “Lịch sử tuyến học”. Nhiệm vụ đang làm dở cần làm xong hoặc hủy lượt trước khi đổi; bài đang chờ chấm sẽ được giữ nguyên.</p>
        <p className="text-sm">{proposal.feasibilityMessage}</p>
        {proposal.milestones.map(m => <article key={m.order} className="rounded-xl border border-slate-200 p-4"><h3 className="font-semibold">{m.order}. {m.title}</h3>
          <p className="mt-1 text-sm text-slate-500">{m.outcomeDescription}</p><ul className="mt-3 space-y-2 text-sm">{m.missions.map(a => <li key={a.missionId}>{a.title} · {a.questionCount > 0 ? `${a.questionCount} câu · ` : ""}khoảng {a.estimatedMinutes} phút<p className="mt-1 text-xs text-slate-500">{a.selectionReason}</p>{a.canStart === false && <span className="ml-2 text-amber-700">Cần bổ sung nội dung</span>}</li>)}</ul></article>)}
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
        <div className="flex flex-wrap gap-3"><button disabled={accept.isPending} className="rounded-xl border px-4 py-3 disabled:opacity-50" onClick={() => { accept.reset(); customize.reset(); setCustomizing(true); contentRef.current?.scrollTo({ top: 0 }); }}>Tùy chỉnh tuyến học</button>
          <button disabled={accept.isPending} className="rounded-xl bg-indigo-600 px-5 py-3 font-semibold text-white disabled:opacity-50" onClick={() => accept.mutate()}>{accept.isPending ? "Đang kích hoạt…" : accept.error ? "Thử chọn lại tuyến" : "Chọn tuyến học này"}</button></div>
      </footer>}
    </section>
  </div>;
}
