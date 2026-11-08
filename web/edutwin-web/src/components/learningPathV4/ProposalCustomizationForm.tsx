import { useState } from "react";
import type { CustomizePathProposalRequest, PathProposalDto, ProposedMilestoneDto } from "../../types/learningPath";
import { TimeBudgetSlider } from "./TimeBudgetSlider";
import { MIN_GOAL_STUDY_MINUTES } from "../../utils/studyTime";

function todayIso() { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`; }
/** Minutes for one set, at the pace the teacher-set question times give this practice. */
function minutesPerSet(questions: number, practice?: { questionCount: number; estimatedMinutes: number }) {
  const pace = practice && practice.questionCount > 0 && practice.estimatedMinutes > 0 ? practice.estimatedMinutes / practice.questionCount : 2;
  return Math.max(1, Math.ceil(questions * pace));
}
function datePart(iso?: string | null) { return iso ? iso.split("T")[0] : ""; }
function formatIsoDate(iso?: string | null) { const bits = datePart(iso).split("-"); return bits.length === 3 ? `${bits[2]}/${bits[1]}/${bits[0]}` : ""; }
function formatDate(d: Date) { return `${String(d.getDate()).padStart(2, "0")}/${String(d.getMonth() + 1).padStart(2, "0")}/${d.getFullYear()}`; }

export function ProposalCustomizationForm({ proposal, availableMilestones: allMilestones = proposal.milestones, onSubmit, onCancel, pending }: {
  proposal: PathProposalDto;
  availableMilestones?: ProposedMilestoneDto[];
  onSubmit: (request: CustomizePathProposalRequest) => void;
  onCancel: () => void;
  pending: boolean;
}) {
  // Confirmation reviews of self-assessed parts stay at the end of the route and are not edited here.
  const availableMilestones = allMilestones.filter(m => !m.isSelfAssessedCheck);
  const [minutes, setMinutes] = useState(Math.max(MIN_GOAL_STUDY_MINUTES, proposal.availableMinutesPerDay ?? 15));
  const [days, setDays] = useState(proposal.availableDaysPerWeek ?? 5);
  const [questionsBySkill, setQuestionsBySkill] = useState<Record<string, string>>(() => Object.fromEntries(
    availableMilestones.flatMap(milestone => milestone.skillIds.map(id => {
      const current = proposal.milestones.flatMap(item => item.missions).find(mission =>
        mission.skillId === id && mission.purpose === "Practice" && !mission.isProbe);
      const original = milestone.missions.find(mission => mission.skillId === id && mission.purpose === "Practice" && !mission.isProbe);
      return [id, String(Math.max(3, proposal.practiceQuestionsBySkill?.[id] ?? current?.questionCount ??
        original?.questionCount ?? proposal.practiceQuestionsPerSet ?? 6))];
    }))));
  const [deadline, setDeadline] = useState(() => {
    if (!proposal.deadlineDate) return "";
    const date = new Date(proposal.deadlineDate);
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
  });
  const [targets, setTargets] = useState(proposal.milestones.filter(m => !m.isSelfAssessedCheck).flatMap(m => m.skillIds));
  // Live re-check with the schedule being edited; the server re-checks exactly on submit.
  const requiredMinutes = proposal.requiredStudyMinutes ?? 0;
  const daysNeeded = requiredMinutes > 0 && minutes > 0 ? Math.ceil(requiredMinutes / minutes) : 0;
  const approxCalendarDays = daysNeeded > 0 ? Math.ceil(daysNeeded * 7 / Math.max(1, days)) : 0;
  const earliestApprox = approxCalendarDays > 0 ? (() => { const t = new Date(); t.setHours(0, 0, 0, 0); t.setDate(t.getDate() + approxCalendarDays - 1); return t; })() : null;
  const chosen = deadline ? new Date(`${deadline}T00:00:00`) : null;
  const infeasible = !!chosen && !!earliestApprox && chosen < earliestApprox;
  const suggestedPart = datePart(proposal.suggestedDeadline);
  const suggestedDate = suggestedPart ? new Date(`${suggestedPart}T00:00:00`) : null;
  const tight = !infeasible && !!chosen && !!suggestedDate && chosen < suggestedDate;
  const input = "mt-1 w-full rounded-lg border border-slate-300 bg-white p-2.5 dark:bg-slate-900";
  return <form className="mt-6 space-y-5" onSubmit={event => {
    event.preventDefault();
    const counts = Object.fromEntries(targets.map(id => [id, Number(questionsBySkill[id])]));
    if (pending || infeasible || !targets.length || Object.values(counts).some(count => !Number.isInteger(count) || count < 3 || count > 20)) return;
    onSubmit({ availableMinutesPerDay: minutes, availableDaysPerWeek: days, practiceQuestionsPerSet: proposal.practiceQuestionsPerSet ?? 6,
      practiceQuestionsBySkill: counts, targetSkillIds: targets,
      deadlineDate: deadline ? new Date(`${deadline}T23:59:59`).toISOString() : null });
  }}>
    <div className="rounded-xl border border-indigo-200 bg-indigo-50 p-4 text-indigo-950">
      <p className="font-semibold">Giữ định hướng: {proposal.goalTitle}</p>
      <p className="mt-1 text-sm">Phân bổ câu hỏi theo từng phần cần cải thiện. Bạn sẽ xem lại bản cập nhật trước khi chọn tuyến.</p>
    </div>
    {(proposal.currentEstimatedScore != null || proposal.targetScore != null || requiredMinutes > 0 || proposal.suggestedDeadline) && <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-800 dark:bg-slate-800 dark:text-slate-100">
      <dl className="grid gap-2 sm:grid-cols-2">
        <div><dt className="font-semibold">Điểm hiện tại ước tính</dt><dd>{proposal.currentEstimatedScore != null ? `${proposal.currentEstimatedScore}/10` : "Chưa đủ dữ liệu"}</dd></div>
        {proposal.targetScore != null && <div><dt className="font-semibold">Điểm mục tiêu</dt><dd>{proposal.targetScore}/10</dd></div>}
        {requiredMinutes > 0 && <div><dt className="font-semibold">Thời lượng cần</dt><dd>~{requiredMinutes} phút ({proposal.minimumStudyDays ?? 0} ngày học)</dd></div>}
        {proposal.earliestFeasibleDate && <div><dt className="font-semibold">Ngày sớm nhất</dt><dd>{formatIsoDate(proposal.earliestFeasibleDate)}</dd></div>}
        {proposal.suggestedDeadline && <div><dt className="font-semibold">Ngày đề xuất</dt><dd>{formatIsoDate(proposal.suggestedDeadline)}</dd></div>}
      </dl>
    </div>}
    <fieldset disabled={pending} className="space-y-5 disabled:opacity-60">
      <TimeBudgetSlider label="Tổng thời gian học mỗi ngày" min={MIN_GOAL_STUDY_MINUTES} value={minutes} onChange={setMinutes} summary="Tối thiểu 10 phút/ngày để đủ làm một bộ luyện. Đây là tổng quỹ thời gian cho cả tuyến; hệ thống xếp các phần học vào quỹ này theo tiến độ và mức độ cần củng cố." />
      <div className="grid gap-4 sm:grid-cols-2">
        <label className="text-sm">Ngày mỗi tuần<select className={input} value={days} onChange={event => setDays(Number(event.target.value))}>{[1, 2, 3, 4, 5, 6, 7].map(day => <option key={day}>{day}</option>)}</select></label>
        <label className="text-sm">Mốc thời gian (nếu có)<input type="date" min={todayIso()} className={input} value={deadline} onChange={event => setDeadline(event.target.value)} />
          {proposal.suggestedDeadline && <button type="button" className="mt-1 text-xs font-semibold text-indigo-700 underline" onClick={() => setDeadline(suggestedPart)}>Dùng ngày đề xuất ({formatIsoDate(proposal.suggestedDeadline)})</button>}</label>
      </div>
      {infeasible && earliestApprox && <p role="alert" className="rounded-lg bg-rose-50 p-3 text-sm text-rose-800">Không có lộ trình phù hợp với mốc này: cần tối thiểu khoảng {daysNeeded} ngày học, sớm nhất khoảng {formatDate(earliestApprox)} theo lịch vừa chọn (ước tính lại; hệ thống kiểm tra chính xác khi cập nhật). Hãy dời mốc, tăng phút/ngày hoặc ngày/tuần.</p>}
      {tight && proposal.suggestedDeadline && <p className="rounded-lg bg-amber-50 p-3 text-sm text-amber-900">Rất gấp: mốc này sát mức tối thiểu; ngày đề xuất an toàn hơn là {formatIsoDate(proposal.suggestedDeadline)}.</p>}
      <fieldset><legend className="text-sm font-semibold">Phân bổ theo phần học</legend>
        <p className="my-2 text-xs text-slate-500">Giữ ít nhất một phần. Phần yếu có thể nhận nhiều câu hơn mỗi bộ. Số bộ dự kiến thay đổi theo kết quả luyện. Lựa chọn bỏ qua nền tảng đã tự xác nhận được giữ khi tùy chỉnh; em có thể đổi ở phần mục tiêu học. Mỗi bộ có thể ít câu hơn nếu ngân hàng chưa đủ. Tự kiểm tra giữ 5 câu độc lập.</p>
        <div className="grid gap-3 sm:grid-cols-2">{availableMilestones.map(milestone => {
          const checked = milestone.skillIds.every(id => targets.includes(id));
          const skillId = milestone.skillIds[0];
          const practice = milestone.missions.find(mission => mission.purpose === "Practice" && !mission.isProbe)
            ?? milestone.missions.find(mission => mission.purpose === "Practice" && mission.isProbe);
          return <div key={milestone.order} className="rounded-lg border border-slate-200 p-3 text-sm">
            <label className="flex items-start gap-2 font-medium"><input type="checkbox" checked={checked} onChange={event => setTargets(current => event.target.checked
              ? [...new Set([...current, ...milestone.skillIds])] : current.filter(id => !milestone.skillIds.includes(id)))} /><span>{milestone.title}</span></label>
            {skillId && <label className="mt-3 block text-xs">Số câu mỗi bộ luyện cho phần này{practice?.isProbe ? " (sau thử nhanh)" : ""}
              <input type="number" min={3} max={20} step={1} required={checked} disabled={!checked} className={input}
                value={questionsBySkill[skillId] ?? "6"} onChange={event => setQuestionsBySkill(current => ({ ...current,
                  ...Object.fromEntries(milestone.skillIds.map(id => [id, event.target.value])) }))} />
              <span className="mt-1 block text-slate-500">Dự kiến {practice?.estimatedSets || 2} bộ theo kết quả hiện có, khoảng {minutesPerSet(Number(questionsBySkill[skillId] || 0), practice)} phút/bộ.
                {minutesPerSet(Number(questionsBySkill[skillId] || 0), practice) > minutes && " Bộ này dài hơn quỹ thời gian mỗi ngày; bạn có thể tạm dừng và làm tiếp."}</span>
            </label>}
          </div>;
        })}</div>
        {!targets.length && <p role="alert" className="mt-2 text-sm text-rose-700">Chọn ít nhất một phần muốn học.</p>}
      </fieldset>
    </fieldset>
    <div className="flex flex-wrap gap-3 border-t border-slate-200 pt-4">
      <button type="button" disabled={pending} onClick={onCancel} className="rounded-xl border px-4 py-3 disabled:opacity-50">Hủy tùy chỉnh</button>
      <button type="submit" disabled={pending || infeasible || !targets.length} className="rounded-xl bg-indigo-600 px-5 py-3 font-semibold text-white disabled:opacity-50">{pending ? "Đang cập nhật tuyến…" : "Cập nhật bản xem trước"}</button>
    </div>
  </form>;
}
