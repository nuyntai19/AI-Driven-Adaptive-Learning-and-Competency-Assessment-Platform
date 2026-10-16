import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { organizationApi } from "../api/organizationApi";
import { getCapabilityMap, getLearningAgenda } from "../api/learningPathApi";
import { useStudentLearningAccess } from "../hooks/useStudentLearningAccess";
import { useAuthStore } from "../stores/authStore";
import { StudentLearningReadOnlyNotice } from "../components/student/StudentLearningReadOnlyNotice";
import { GoalAndProposalModal } from "../components/learningPathV4/GoalAndProposalModal";
import { MissionPlayerModal } from "../components/learningPathV4/MissionPlayerModal";
import type { AgendaItemDto } from "../types/learningPath";

const activityLabels: Record<string, string> = { Study: "Tìm hiểu và ví dụ", Practice: "Luyện có hướng dẫn",
  IndependentCheck: "Tự kiểm tra", WorkedExample: "Ví dụ từng bước", FindError: "Tìm lỗi", Explore: "Khám phá",
  Explain: "Giải thích", CompleteSolution: "Hoàn thiện lời giải", TransferChallenge: "Vận dụng" };
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
  const [minutes, setMinutes] = useState(15);
  const [showGoal, setShowGoal] = useState(false);
  const [mission, setMission] = useState<AgendaItemDto | null>(null);
  const map = useQuery({ queryKey: ["learning-paths", "capability-map", actor?.centerId, actor?.userId, subjectId, access.classId],
    queryFn: () => getCapabilityMap(subjectId, access.classId), enabled: !!subjectId && access.writable });
  const agenda = useQuery({ queryKey: ["learning-agenda", actor?.centerId, actor?.userId, subjectId, access.classId, minutes],
    queryFn: () => getLearningAgenda(subjectId, minutes, access.classId), enabled: !!subjectId && access.writable });
  const missions = agenda.data?.missions ?? [];
  const primary = agenda.data?.primaryMission;
  const panel = "rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 p-5 sm:p-6";
  const action = "rounded-xl bg-indigo-600 hover:bg-indigo-700 px-4 py-2.5 text-sm font-semibold text-white disabled:opacity-40 disabled:cursor-not-allowed";
  if (access.pending || subjects.isPending) return <p role="status" className="p-6">Đang kiểm tra môn học và giáo trình…</p>;
  if (access.readOnly) return <StudentLearningReadOnlyNotice reason={access.reason} />;
  return <div className="mx-auto max-w-6xl space-y-7 px-4 py-7 text-slate-900 dark:text-slate-100">
    <header className="flex flex-wrap items-start justify-between gap-4">
      <div><p className="text-sm text-indigo-600 dark:text-indigo-300">Mục tiêu của bạn · Tiến bộ từ bằng chứng thật</p>
        <h1 className="mt-1 text-3xl font-bold">Lộ trình học tập cá nhân</h1>
        <p className="mt-2 max-w-xl text-sm text-slate-500">Tìm hiểu, luyện có hướng dẫn và tự kiểm tra là những hoạt động riêng. Chọn kỹ năng phù hợp với mục tiêu của bạn.</p></div>
      <div className="flex flex-wrap gap-3"><label className="sr-only" htmlFor="path-subject">Môn học</label>
        <select id="path-subject" value={subjectId} className="rounded-xl border border-slate-300 bg-white p-2.5 dark:bg-slate-900"
          onChange={e => { const next = new URLSearchParams(params); next.set("subjectId", e.target.value); next.delete("pathId"); next.delete("sessionId"); setParams(next); setMission(null); setShowGoal(false); }}>
          {subjects.data?.data.map(s => <option key={s.subjectId} value={s.subjectId}>{s.subjectName}</option>)}
        </select><button className={action} onClick={() => setShowGoal(true)} disabled={!map.data?.skills.length}>Thiết lập mục tiêu</button></div>
    </header>
    {(map.error || agenda.error) && <div role="alert" className="rounded-xl bg-rose-50 p-4 text-rose-800">
      Không thể tải lộ trình. {(map.error ?? agenda.error)?.message}
      <button className="ml-3 underline" onClick={() => { void map.refetch(); void agenda.refetch(); }}>Thử lại</button>
    </div>}
    {agenda.data && <section className={panel}>
      <div className="flex flex-wrap items-center justify-between gap-3"><div><h2 className="text-xl font-semibold">Hôm nay học gì?</h2>
        {agenda.data.goalTitle && <p className="mt-1 text-sm text-slate-500">Mục tiêu: {agenda.data.goalTitle}</p>}</div>
        <div className="flex gap-2" aria-label="Thời gian hôm nay">{[5, 15, 30].map(m => <button key={m} aria-pressed={m === minutes}
          className={`rounded-lg border px-3 py-2 text-sm ${m === minutes ? "border-indigo-500 bg-indigo-50 text-indigo-800" : "border-slate-300"}`} onClick={() => setMinutes(m)}>{m} phút</button>)}</div></div>
      <p className="mt-3 text-sm text-slate-500">{agenda.data.agendaMessage}</p>
      {primary && <div className="mt-5 rounded-xl bg-indigo-50 p-5 dark:bg-indigo-950/40">
        <p className="text-xs font-semibold uppercase tracking-wider text-indigo-600">{primary.activeRunId ? "Tiếp tục nhiệm vụ" : "Đề xuất tiếp theo"}</p>
        <h3 className="mt-2 text-xl font-semibold">{primary.title}</h3><p className="mt-2 text-sm">{primary.recommendationReason}</p>
        <button className={`${action} mt-4`} onClick={() => setMission(primary)}>{primary.activeRunId ? "Tiếp tục đúng tiến độ" : "Mở nhiệm vụ"} · khoảng {primary.estimatedDurationMinutes} phút</button>
      </div>}
      {!agenda.data.learningPathId && <button className={`${action} mt-4`} disabled={!map.data?.skills.length} onClick={() => setShowGoal(true)}>Chọn mục tiêu và xem trước tuyến học</button>}
      {agenda.data.alternativeOptions.length > 0 && <div className="mt-4 flex flex-wrap gap-2">{agenda.data.alternativeOptions.map(m => <button className="rounded-xl border border-slate-300 px-4 py-2 text-sm" key={m.missionId} onClick={() => setMission(m)}>{m.title}</button>)}</div>}
    </section>}
    <section className="space-y-4"><div><h2 className="text-xl font-semibold">Các chặng trong tuyến của bạn</h2><p className="mt-1 text-sm text-slate-500">Mỗi chặng gắn với một kỹ năng. Phần đã có bằng chứng sẽ được giảm luyện lặp; nội dung còn thiếu được nêu rõ.</p></div>
      {missions.length === 0 && <p className={panel}>Chưa có tuyến được xác nhận cho phạm vi này. Hãy chọn mục tiêu ở trên; các bài làm và lộ trình trước vẫn được giữ trong lịch sử.</p>}
      {[...new Set(missions.map(m => m.skillId))].map((id, index) => {
        const group = missions.filter(m => m.skillId === id); const skill = map.data?.skills.find(s => s.skillId === id);
        return <article className={panel} key={id}><div className="flex items-start gap-3"><span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-indigo-100 font-semibold text-indigo-800">{index + 1}</span>
          <div><h3 className="text-lg font-semibold">{group[0].skillName}</h3><p className="mt-1 text-sm text-slate-500">{skill?.description || "Học và tự thực hiện các nhiệm vụ thuộc kỹ năng này."}</p>
            <p className="mt-2 text-xs font-semibold text-indigo-600">{stateLabels[skill?.state ?? "Unknown"]} · {skill?.evidenceCount ?? 0} câu có bằng chứng hợp lệ</p></div></div>
          <div className="mt-5 grid gap-3 sm:grid-cols-3">{group.map(m => <div key={m.missionId} className="flex flex-col rounded-xl border border-slate-200 p-4 dark:border-slate-700">
            <p className="text-xs font-semibold text-indigo-600">{activityLabels[m.purpose] ?? m.purpose}</p><h4 className="mt-2 font-medium">{m.title}</h4>
            <p className="mt-2 flex-1 text-xs leading-relaxed text-slate-500">{m.recommendationReason}</p>
            <button className={`${action} mt-4`} disabled={!m.canStart && !m.activeRunId && !(m.status === "Completed" && !["Practice", "IndependentCheck"].includes(m.purpose))} onClick={() => setMission(m)}>
              {m.status === "Completed" ? "Xem lại" : m.activeRunId ? "Tiếp tục / xem kết quả" : m.canStart ? "Bắt đầu" : "Chưa đủ điều kiện"}</button>
          </div>)}</div></article>;
      })}
    </section>
    <section className={panel}><h2 className="text-xl font-semibold">Bản đồ năng lực · {subject?.subjectName}</h2>
      {!map.data?.skills.length && <p className="mt-3 text-sm text-slate-500">Chưa có kỹ năng khả dụng trong giáo trình. Cần giáo viên bổ sung nội dung; hệ thống không dùng môn khác để thay thế.</p>}
      <div className="mt-4 grid gap-3 sm:grid-cols-2">{map.data?.skills.map(s => <div className="rounded-xl border border-slate-200 p-4 dark:border-slate-700" key={s.skillId}>
        <h3 className="font-semibold">{s.skillName}</h3><p className="mt-1 text-sm text-indigo-600">{stateLabels[s.state] ?? s.state}</p>
        <p className="mt-2 text-xs text-slate-500">{s.rationale}</p>{s.masteryScore != null && <p className="mt-2 text-xs">Hồ sơ năng lực: {s.masteryScore}%</p>}
        {s.availability === "PrerequisiteBlocked" && <p className="mt-2 text-xs text-amber-700">Cần xác nhận kỹ năng nền tảng trước khi kiểm tra độc lập.</p>}
      </div>)}</div>
    </section>
    {showGoal && <GoalAndProposalModal key={`${subjectId}:${access.classId}`} isOpen onClose={() => setShowGoal(false)}
      subjectId={subjectId} classId={access.classId} skills={map.data?.skills ?? []} subjectName={subject?.subjectName ?? ""}
      onAccepted={() => { void agenda.refetch(); void map.refetch(); }} />}
    {mission && <MissionPlayerModal key={mission.missionId} isOpen mission={mission} subjectId={subjectId}
      onClose={() => { setMission(null); void agenda.refetch(); void map.refetch(); }} />}
  </div>;
}
