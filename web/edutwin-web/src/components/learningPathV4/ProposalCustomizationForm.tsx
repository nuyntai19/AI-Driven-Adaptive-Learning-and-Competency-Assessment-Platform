import { useState } from "react";
import type { CustomizePathProposalRequest, PathProposalDto, ProposedMilestoneDto } from "../../types/learningPath";
import { TimeBudgetSlider } from "./TimeBudgetSlider";

export function ProposalCustomizationForm({ proposal, availableMilestones = proposal.milestones, onSubmit, onCancel, pending }: {
  proposal: PathProposalDto;
  availableMilestones?: ProposedMilestoneDto[];
  onSubmit: (request: CustomizePathProposalRequest) => void;
  onCancel: () => void;
  pending: boolean;
}) {
  const [minutes, setMinutes] = useState(proposal.availableMinutesPerDay ?? 15);
  const [days, setDays] = useState(proposal.availableDaysPerWeek ?? 5);
  const [questions, setQuestions] = useState(String(proposal.practiceQuestionsPerSet ?? 6));
  const [deadline, setDeadline] = useState(() => {
    if (!proposal.deadlineDate) return "";
    const date = new Date(proposal.deadlineDate);
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
  });
  const [targets, setTargets] = useState(proposal.milestones.flatMap(m => m.skillIds));
  const input = "mt-1 w-full rounded-lg border border-slate-300 bg-white p-2.5 dark:bg-slate-900";
  return <form className="mt-6 space-y-5" onSubmit={event => {
    event.preventDefault();
    if (pending || !targets.length || !Number.isInteger(Number(questions)) || Number(questions) < 3 || Number(questions) > 20) return;
    onSubmit({ availableMinutesPerDay: minutes, availableDaysPerWeek: days, practiceQuestionsPerSet: Number(questions),
      targetSkillIds: targets, deadlineDate: deadline ? new Date(`${deadline}T23:59:59`).toISOString() : null });
  }}>
    <div className="rounded-xl border border-indigo-200 bg-indigo-50 p-4 text-indigo-950">
      <p className="font-semibold">Giữ định hướng: {proposal.goalTitle}</p>
      <p className="mt-1 text-sm">Điều chỉnh lượng bài và lịch học trên tuyến này. Bạn sẽ xem lại bản cập nhật trước khi chọn tuyến.</p>
    </div>
    <fieldset disabled={pending} className="space-y-5 disabled:opacity-60">
      <label className="block text-sm font-medium">Số câu mỗi bộ luyện
        <input type="number" min={3} max={20} step={1} required className={input} value={questions} onChange={event => setQuestions(event.target.value)} />
        <span className="mt-2 block text-xs font-normal text-slate-500">Từ 3 đến 20 câu, tùy số câu phù hợp trong ngân hàng. Lượt thử nhanh và bài tự kiểm tra giữ số câu riêng; tự kiểm tra gồm 5 câu độc lập.</span>
      </label>
      <TimeBudgetSlider label="Thời gian học mỗi ngày" value={minutes} onChange={setMinutes} summary="Lịch học và số câu được dùng để cập nhật thời gian dự kiến của tuyến." />
      <div className="grid gap-4 sm:grid-cols-2">
        <label className="text-sm">Ngày mỗi tuần<select className={input} value={days} onChange={event => setDays(Number(event.target.value))}>{[1, 2, 3, 4, 5, 6, 7].map(day => <option key={day}>{day}</option>)}</select></label>
        <label className="text-sm">Mốc thời gian (nếu có)<input type="date" className={input} value={deadline} onChange={event => setDeadline(event.target.value)} /></label>
      </div>
      <fieldset><legend className="text-sm font-semibold">Phần muốn học trong tuyến này</legend>
        <p className="my-2 text-xs text-slate-500">Giữ ít nhất một phần. Nền tảng cần thiết cho phần đã chọn sẽ được giữ lại và hiển thị trong bản xem trước.</p>
        <div className="grid gap-2 sm:grid-cols-2">{availableMilestones.map(milestone => <label key={milestone.order} className="flex items-start gap-2 rounded-lg border border-slate-200 p-3 text-sm">
          <input type="checkbox" checked={milestone.skillIds.every(id => targets.includes(id))} onChange={event => setTargets(current => event.target.checked
            ? [...new Set([...current, ...milestone.skillIds])] : current.filter(id => !milestone.skillIds.includes(id)))} />
          <span>{milestone.title}</span>
        </label>)}</div>
        {!targets.length && <p role="alert" className="mt-2 text-sm text-rose-700">Chọn ít nhất một phần muốn học.</p>}
      </fieldset>
    </fieldset>
    <div className="flex flex-wrap gap-3 border-t border-slate-200 pt-4">
      <button type="button" disabled={pending} onClick={onCancel} className="rounded-xl border px-4 py-3 disabled:opacity-50">Hủy tùy chỉnh</button>
      <button type="submit" disabled={pending || !targets.length} className="rounded-xl bg-indigo-600 px-5 py-3 font-semibold text-white disabled:opacity-50">{pending ? "Đang cập nhật tuyến…" : "Cập nhật bản xem trước"}</button>
    </div>
  </form>;
}
