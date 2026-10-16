import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createOrUpdateLearningGoal, generatePathProposal, acceptPathProposal } from "../../api/learningPathApi";
import type { PathProposalDto, AcceptPathProposalResponse, SkillCapabilityStatusDto } from "../../types/learningPath";
import { extractProblemDetails } from "../../utils/problemDetails";

interface Props { isOpen: boolean; onClose: () => void; subjectId: string; classId?: string;
  onAccepted: (res: AcceptPathProposalResponse) => void; skills?: SkillCapabilityStatusDto[]; subjectName?: string }
export function GoalAndProposalModal({ isOpen, onClose, subjectId, classId, onAccepted, skills = [], subjectName = "môn đang học" }: Props) {
  const cache = useQueryClient();
  const [title, setTitle] = useState(`Kế hoạch học ${subjectName} của tôi`);
  const [goalType, setGoalType] = useState("Foundation");
  const [minutes, setMinutes] = useState(15);
  const [days, setDays] = useState(5);
  const [deadline, setDeadline] = useState("");
  const [targets, setTargets] = useState<string[]>([]);
  const [proposal, setProposal] = useState<PathProposalDto | null>(null);
  const generate = useMutation({ mutationFn: async () => {
    const goal = await createOrUpdateLearningGoal({ subjectId, classId, title, goalType, targetSkillIds: targets,
      availableMinutesPerDay: minutes, availableDaysPerWeek: days, preferredMode: "Balanced", preferredPace: "Moderate",
      deadlineDate: deadline ? new Date(`${deadline}T23:59:59`).toISOString() : undefined });
    return generatePathProposal({ subjectId, classId, goalId: goal.goalId });
  }, onSuccess: setProposal });
  const accept = useMutation({ mutationFn: () => acceptPathProposal(proposal!.proposalId), onSuccess: res => {
    void cache.invalidateQueries({ queryKey: ["learning-agenda"] });
    void cache.invalidateQueries({ queryKey: ["learning-paths"] }); onAccepted(res); onClose();
  } });
  if (!isOpen) return null;
  const input = "mt-1 w-full rounded-lg border border-slate-300 bg-white p-2.5 dark:bg-slate-900";
  return <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-slate-950/70 p-4">
    <section role="dialog" aria-modal="true" aria-labelledby="goal-title" className="my-6 max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-2xl bg-white p-6 text-slate-900 shadow-xl dark:bg-slate-900 dark:text-slate-100">
      <header className="flex items-start justify-between gap-4"><div><h2 id="goal-title" className="text-2xl font-semibold">Mục tiêu học {subjectName}</h2>
        <p className="mt-2 text-sm text-slate-500">Chọn phạm vi của bạn. Xem trước chưa thay tuyến đang học; chỉ kích hoạt khi bạn xác nhận.</p></div>
        <button onClick={onClose} aria-label="Đóng" className="rounded-lg border px-3 py-1">×</button></header>
      {(generate.error || accept.error) && <p role="alert" className="mt-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-800">{extractProblemDetails(generate.error ?? accept.error).message}</p>}
      {!proposal ? <form className="mt-6 space-y-5" onSubmit={e => { e.preventDefault(); generate.mutate(); }}>
        <label className="block text-sm font-medium">Tên mục tiêu<input required maxLength={200} className={input} value={title} onChange={e => setTitle(e.target.value)} /></label>
        <label className="block text-sm font-medium">Bạn muốn đạt điều gì?<select className={input} value={goalType} onChange={e => setGoalType(e.target.value)}>
          <option value="Foundation">Học lại nền tảng</option><option value="KeepUp">Theo kịp bài trên lớp</option><option value="ExamPrep">Chuẩn bị bài kiểm tra</option><option value="Advanced">Học nâng cao</option>
        </select></label>
        <div className="grid gap-4 sm:grid-cols-3"><label className="text-sm">Phút mỗi ngày<select className={input} value={minutes} onChange={e => setMinutes(Number(e.target.value))}>{[5, 15, 30, 45, 60].map(n => <option key={n}>{n}</option>)}</select></label>
          <label className="text-sm">Ngày mỗi tuần<select className={input} value={days} onChange={e => setDays(Number(e.target.value))}>{[1, 2, 3, 4, 5, 6, 7].map(n => <option key={n}>{n}</option>)}</select></label>
          <label className="text-sm">Mốc thời gian (nếu có)<input type="date" className={input} value={deadline} onChange={e => setDeadline(e.target.value)} /></label></div>
        <fieldset><legend className="text-sm font-semibold">Kỹ năng trọng tâm</legend><p className="my-2 text-xs text-slate-500">Không chọn nghĩa là xem xét tất cả kỹ năng trong giáo trình. Nền tảng cần thiết sẽ được bổ sung vào tuyến.</p>
          <div className="grid max-h-56 gap-2 overflow-y-auto sm:grid-cols-2">{skills.map(s => <label className="flex items-start gap-2 rounded-lg border border-slate-200 p-3 text-sm" key={s.skillId}>
            <input type="checkbox" checked={targets.includes(s.skillId)} onChange={e => setTargets(v => e.target.checked ? [...v, s.skillId] : v.filter(id => id !== s.skillId))} /><span>{s.skillName}</span>
          </label>)}</div></fieldset>
        <button type="submit" disabled={generate.isPending || skills.length === 0} className="rounded-xl bg-indigo-600 px-5 py-3 font-semibold text-white disabled:opacity-50">{generate.isPending ? "Đang lập tuyến từ dữ liệu học…" : "Xem trước lộ trình"}</button>
      </form> : <div className="mt-6 space-y-5"><p className="rounded-xl bg-indigo-50 p-4 text-sm text-indigo-900">{proposal.startingBaselineSummary}</p>
        <p className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">Khi chọn tuyến này, các buổi học ở giao diện cũ còn dở sẽ được kết thúc để chuyển sang nhiệm vụ mới. Toàn bộ bài đã nộp và bằng chứng vẫn được giữ; phần đang chờ chấm hoặc giáo viên duyệt phải được xử lý trước.</p>
        <p className="text-sm">{proposal.feasibilityMessage}</p>
        {proposal.milestones.map(m => <article key={m.order} className="rounded-xl border border-slate-200 p-4"><h3 className="font-semibold">{m.order}. {m.title}</h3>
          <p className="mt-1 text-sm text-slate-500">{m.outcomeDescription}</p><ul className="mt-3 space-y-2 text-sm">{m.missions.map(a => <li key={a.missionId}>{a.title} · khoảng {a.estimatedMinutes} phút{a.canStart === false && <span className="ml-2 text-amber-700">Cần bổ sung nội dung</span>}</li>)}</ul></article>)}
        <div className="flex flex-wrap gap-3"><button className="rounded-xl border px-4 py-3" onClick={() => setProposal(null)}>Điều chỉnh mục tiêu</button>
          <button disabled={accept.isPending} className="rounded-xl bg-indigo-600 px-5 py-3 font-semibold text-white disabled:opacity-50" onClick={() => accept.mutate()}>{accept.isPending ? "Đang kích hoạt…" : "Chọn tuyến học này"}</button></div>
      </div>}
    </section>
  </div>;
}
