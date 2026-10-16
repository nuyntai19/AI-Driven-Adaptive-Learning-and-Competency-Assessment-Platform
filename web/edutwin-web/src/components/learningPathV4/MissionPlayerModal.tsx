import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { startLearningMission, saveActivityDraft, requestActivityAssistance, submitActivity, evaluateMissionRunV4 } from "../../api/learningPathApi";
import { useAuthStore } from "../../stores/authStore";
import { QuestionImage } from "../QuestionImage";
import { RichMathText } from "../math/RichMathText";
import type { ActivityRunDto, AgendaItemDto, MissionRunDto } from "../../types/learningPath";

const outcomeNames: Record<string, string> = { Passed: "Đạt yêu cầu kiểm tra độc lập", NeedsSupport: "Cần củng cố thêm",
  ActivityOnlyCompleted: "Đã hoàn thành hoạt động học", InsufficientEvidence: "Chưa đủ bằng chứng",
  PendingGrading: "Đang chờ chấm bài", PendingTeacherReview: "Chờ giáo viên xác nhận" };
const activityNames: Record<string, string> = { Study: "Tìm hiểu và ví dụ", WorkedExample: "Ví dụ từng bước", FindError: "Tìm và sửa lỗi",
  Explore: "Khám phá", Practice: "Luyện có hướng dẫn", IndependentCheck: "Tự kiểm tra", Explain: "Giải thích", CompleteSolution: "Hoàn thiện lời giải" };
const done = (a: ActivityRunDto) => a.status === "Submitted" || a.status === "Completed";

export function MissionPlayerModal({ isOpen, onClose, mission, subjectId }: { isOpen: boolean; onClose: () => void; mission: AgendaItemDto | null; subjectId: string }) {
  const cache = useQueryClient();
  const actor = useAuthStore(s => s.user);
  const key = ["mission-run", actor?.centerId, actor?.userId, subjectId, mission?.missionId];
  const run = useQuery({ queryKey: key, queryFn: () => startLearningMission(mission!.missionId), enabled: isOpen && !!mission,
    staleTime: Infinity, retry: false, refetchOnWindowFocus: false });
  const current = run.data?.activities.find(a => !done(a));
  const evaluation = useQuery({ queryKey: ["mission-evaluation", actor?.centerId, actor?.userId, run.data?.sessionRunId],
    queryFn: () => evaluateMissionRunV4(run.data!.sessionRunId), enabled: !!run.data && !current && run.data.activities.length > 0,
    retry: false, refetchInterval: query => ["PendingGrading", "PendingTeacherReview"].includes(query.state.data?.outcome ?? "") ? 4000 : false });
  const close = () => { void cache.invalidateQueries({ queryKey: ["learning-agenda"] }); void cache.invalidateQueries({ queryKey: ["learning-paths"] }); onClose(); };
  if (!isOpen || !mission) return null;
  return <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-slate-950/75 p-3 sm:p-6">
    <section role="dialog" aria-modal="true" aria-labelledby="mission-title" className="max-h-[94vh] w-full max-w-3xl overflow-y-auto rounded-2xl bg-white text-slate-900 shadow-xl dark:bg-slate-900 dark:text-slate-100">
      <header className="sticky top-0 z-10 flex items-start justify-between gap-4 border-b border-slate-200 bg-white p-5 dark:border-slate-700 dark:bg-slate-900">
        <div><p className="text-xs font-semibold uppercase tracking-wider text-indigo-600">{mission.skillName}</p><h2 id="mission-title" className="mt-1 text-xl font-semibold">{mission.title}</h2>
          {run.data && <p className="mt-2 text-xs text-slate-500">{run.data.activities.filter(done).length}/{run.data.activities.length} hoạt động đã nộp · Tiến độ lưu theo tài khoản</p>}</div>
        <button className="rounded-lg border px-3 py-1" aria-label="Đóng và giữ tiến độ" onClick={close}>×</button>
      </header>
      <div className="space-y-5 p-5 sm:p-7">
        {run.isPending && <p role="status">Đang mở nhiệm vụ và khôi phục tiến độ…</p>}
        {run.error && <div role="alert" className="rounded-xl bg-rose-50 p-4 text-rose-800">{run.error.message}<button className="ml-3 underline" onClick={() => void run.refetch()}>Thử lại</button></div>}
        {current && <Activity key={current.activityRunId} activity={current} onSubmitted={status => {
          cache.setQueryData<MissionRunDto>(key, old => old ? { ...old, activities: old.activities.map(a => a.activityRunId === current.activityRunId ? { ...a, status } : a) } : old);
        }} />}
        {evaluation.isPending && run.data && !current && <p role="status">Đang kiểm tra kết quả đã lưu…</p>}
        {evaluation.error && <div role="alert" className="rounded-xl bg-rose-50 p-4 text-rose-800">Chưa lấy được kết quả. Bài đã nộp vẫn được giữ. {evaluation.error.message}
          <button className="ml-2 underline" onClick={() => void evaluation.refetch()}>Đánh giá lại</button></div>}
        {evaluation.data && <div className="rounded-xl border border-indigo-200 bg-indigo-50 p-5 text-indigo-950"><h3 className="text-xl font-semibold">{outcomeNames[evaluation.data.outcome] ?? evaluation.data.outcome}</h3>
          {evaluation.data.decisionReasons.map((r, i) => <p className="mt-3 text-sm leading-relaxed" key={i}>{r}</p>)}
          <p className="mt-4 text-sm">Quay lại tuyến học để xem nhiệm vụ tiếp theo dựa trên kết quả mới. Mức năng lực chỉ thay đổi khi pipeline bằng chứng xác nhận.</p>
          <button className="mt-4 rounded-lg bg-indigo-600 px-4 py-2 text-white" onClick={close}>Về tuyến học</button></div>}
        {!current && run.data?.activities.filter(a => a.content).map(a => {
          const p = (a.content?.payload ?? {}) as Record<string, unknown>;
          return <details key={a.activityRunId} className="rounded-xl border border-slate-200 p-4"><summary className="cursor-pointer font-semibold">Xem lại: {a.content?.title}</summary>
            <p className="mt-3 whitespace-pre-wrap text-sm">{a.content?.description}</p>
            {text(p.example) && <RichMathText className="mt-3" text={text(p.example)} />}
            {text(p.solution) && <RichMathText className="mt-3" text={text(p.solution)} />}
            {Array.isArray(p.steps) && (p.steps as Record<string, unknown>[]).map((s, i) => <p key={i} className="mt-3 whitespace-pre-wrap text-sm">{text(s.title)} {text(s.content)}</p>)}
          </details>;
        })}
      </div>
    </section>
  </div>;
}

function text(value: unknown): string { return typeof value === "string" || typeof value === "number" ? String(value) : ""; }
function Activity({ activity: a, onSubmitted }: { activity: ActivityRunDto; onSubmitted: (status: string) => void }) {
  const restored = (() => { try { return JSON.parse(a.draftPayload ?? "{}") as Record<string, unknown>; } catch { return {}; } })();
  const [answer, setAnswer] = useState(text(restored.finalAnswer));
  const [reasoning, setReasoning] = useState(text(restored.reasoningText));
  const [errorStep, setErrorStep] = useState(text(restored.selectedErrorStep));
  const [revision, setRevision] = useState(a.draftRevision);
  const [lastSaved, setLastSaved] = useState(a.draftPayload ?? "");
  const [notice, setNotice] = useState("");
  const [conflict, setConflict] = useState(false);
  const [submissionId] = useState(() => crypto.randomUUID());
  const [started] = useState(() => Date.now());
  const q = a.question?.question;
  const payload = (a.content?.payload ?? {}) as Record<string, unknown>;
  const steps = Array.isArray(payload.steps) ? payload.steps as Record<string, unknown>[] : [];
  const currentDraft = JSON.stringify({ finalAnswer: answer, reasoningText: reasoning, selectedErrorStep: errorStep });
  const save = useMutation({ mutationFn: (draft: string) => saveActivityDraft(a.activityRunId, { expectedRevision: revision, draftPayload: draft }),
    onSuccess: (res, draft) => { if (res.hasConflict) { setConflict(true); setNotice("Bản nháp đã thay đổi ở thiết bị khác. Giữ nội dung hiện tại và mở lại nhiệm vụ để xem bản đã lưu; không tự ghi đè."); }
      else { setRevision(res.draftRevision); setLastSaved(draft); setNotice("Đã lưu bản nháp trên server."); } },
    onError: e => setNotice(`Chưa đồng bộ bản nháp: ${e.message}. Nội dung vẫn ở màn hình; hãy lưu lại trước khi đóng.`) });
  const submit = useMutation({ mutationFn: () => submitActivity(a.activityRunId, { clientSubmissionId: submissionId,
    questionId: q ? q.questionId : undefined, finalAnswer: answer, reasoningText: reasoning || undefined,
    selectedErrorStep: errorStep || undefined, timeSpentSeconds: Math.floor((Date.now() - started) / 1000) }),
    onSuccess: res => onSubmitted(res.activityStatus) });
  const assist = useMutation({ mutationFn: () => requestActivityAssistance(a.activityRunId, { stepIndex: 0 }), onSuccess: r => setNotice(r.explanation) });
  useEffect(() => {
    if (conflict || save.isPending || submit.isPending || currentDraft === lastSaved || (!answer && !reasoning && !errorStep)) return;
    const timer = setTimeout(() => save.mutate(currentDraft), 1200);
    return () => clearTimeout(timer);
  }, [currentDraft, lastSaved, answer, reasoning, errorStep, conflict, save.isPending, submit.isPending, save.mutate]);
  const independent = a.activityType === "IndependentCheck" || a.activityType === "TransferChallenge";
  const field = "mt-2 w-full rounded-lg border border-slate-300 bg-white p-3 dark:bg-slate-900";
  return <form className="space-y-5" onSubmit={e => { e.preventDefault(); submit.mutate(); }}>
    <div><p className="text-sm font-semibold text-indigo-600">{activityNames[a.activityType] ?? a.activityType} · Hoạt động {a.activityOrder}</p>
      {independent && <p className="mt-2 rounded-lg bg-amber-50 p-3 text-sm text-amber-900">Tự làm, không có gợi ý hoặc lời giải trước khi nộp. Kết quả còn qua chấm và kiểm duyệt bằng chứng.</p>}</div>
    {a.content && <article className="space-y-3 rounded-xl bg-slate-50 p-4 dark:bg-slate-800"><h3 className="text-lg font-semibold">{a.content.title}</h3>
      {a.content.description && <p className="whitespace-pre-wrap text-sm leading-relaxed">{a.content.description}</p>}
      {steps.map((step, i) => <div className="rounded-lg border border-slate-200 p-3" key={i}><p className="font-medium">{text(step.title) || `Bước ${i + 1}`}</p><p className="mt-1 whitespace-pre-wrap text-sm">{text(step.content) || text(step.explanation)}</p></div>)}
      {text(payload.example) && <p className="whitespace-pre-wrap text-sm"><strong>Ví dụ: </strong>{text(payload.example)}</p>}
      {text(payload.solution) && <p className="whitespace-pre-wrap text-sm"><strong>Cách giải đã xuất bản: </strong>{text(payload.solution)}</p>}
      {text(payload.notice) && <p className="text-sm text-amber-700">{text(payload.notice)}</p>}
      {text(payload.prompt) && <p className="text-sm font-medium">{text(payload.prompt)}</p>}
    </article>}
    {q && <article><RichMathText text={q.questionText} className="whitespace-pre-wrap text-base leading-relaxed" /><QuestionImage questionId={q.questionId} hasImage={q.hasImage ?? false} />
      {q.options?.length > 0 && <fieldset className="mt-4 space-y-2"><legend className="sr-only">Chọn đáp án</legend>{q.options.map(o => <label className="flex gap-3 rounded-xl border border-slate-200 p-3" key={o.optionId}>
        <input required type="radio" name={`answer-${a.activityRunId}`} value={o.optionId} checked={answer === o.optionId} onChange={e => setAnswer(e.target.value)} /><span>{o.label}. {o.text}</span></label>)}</fieldset>}
    </article>}
    {a.activityType === "FindError" && <label className="block text-sm font-medium">Bước nào sai?<input required className={field} value={errorStep} onChange={e => setErrorStep(e.target.value)} placeholder="Số bước" /></label>}
    {!q?.options?.length && <label className="block text-sm font-medium">{q ? "Câu trả lời" : a.activityType === "FindError" ? "Sửa lại bước sai" : "Em hiểu hoặc rút ra điều gì?"}<textarea required rows={3} className={field} value={answer} onChange={e => setAnswer(e.target.value)} /></label>}
    {q && <label className="block text-sm font-medium">Giải thích cách làm{q.reasoningRequired ? " (bắt buộc)" : " (nếu có)"}<textarea required={q.reasoningRequired} rows={3} className={field} value={reasoning} onChange={e => setReasoning(e.target.value)} /></label>}
    {notice && <p role="status" className="text-sm text-slate-500">{notice}</p>}
    {(submit.error || assist.error) && <p role="alert" className="rounded-lg bg-rose-50 p-3 text-sm text-rose-800">{(submit.error ?? assist.error)?.message}</p>}
    <div className="flex flex-wrap gap-3"><button disabled={submit.isPending || save.isPending || conflict} type="submit" className="rounded-xl bg-indigo-600 px-5 py-3 font-semibold text-white disabled:opacity-40">{submit.isPending ? "Đang lưu bài…" : q ? "Nộp câu trả lời" : "Hoàn thành hoạt động"}</button>
      <button type="button" disabled={save.isPending || conflict} className="rounded-xl border px-4 py-3" onClick={() => save.mutate(currentDraft)}>Lưu bản nháp</button>
      {!independent && <button type="button" disabled={assist.isPending} className="rounded-xl border px-4 py-3" onClick={() => assist.mutate()}>Cần hướng dẫn</button>}
    </div>
  </form>;
}
