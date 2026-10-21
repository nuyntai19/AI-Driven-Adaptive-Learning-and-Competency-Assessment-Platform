import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { startLearningMission, abandonMissionRun, getMissionRun, getActivityAiFeedback, saveActivityDraft, requestActivityAssistance, submitActivity, evaluateMissionRunV4 } from "../../api/learningPathApi";
import { useAuthStore } from "../../stores/authStore";
import { QuestionImage } from "../QuestionImage";
import { RichMathText } from "../math/RichMathText";
import type { ActivityRunDto, AgendaItemDto, MissionRunDto, ActivitySubmissionResponseDto, QuestionLearningFeedbackDto, AssistanceResponseDto } from "../../types/learningPath";
import { extractProblemDetails } from "../../utils/problemDetails";
import { ongoingMissionFromError } from "../../utils/learningMission";

const outcomeNames: Record<string, string> = { Passed: "Đạt yêu cầu kiểm tra độc lập", NotPassed: "Cần củng cố thêm", NeedsSupport: "Cần củng cố thêm",
  ActivityOnlyCompleted: "Đã hoàn thành hoạt động học", InsufficientEvidence: "Chưa đủ bằng chứng",
  PendingGrading: "Đang chờ chấm bài" };
const activityNames: Record<string, string> = { Study: "Tìm hiểu và ví dụ", WorkedExample: "Ví dụ từng bước", FindError: "Tìm và sửa lỗi",
  Explore: "Khám phá", Practice: "Luyện có hướng dẫn", IndependentCheck: "Tự kiểm tra", Explain: "Giải thích", CompleteSolution: "Hoàn thiện lời giải" };
const done = (a: ActivityRunDto) => a.status === "Submitted" || a.status === "Completed";

export function MissionPlayerModal({ isOpen, onClose, onResumeMission, mission, subjectId, contextLabel, nextLabel }: { isOpen: boolean; onClose: () => void; onResumeMission: (mission: AgendaItemDto) => void; mission: AgendaItemDto | null; subjectId: string; contextLabel?: string; nextLabel?: string }) {
  const cache = useQueryClient();
  const actor = useAuthStore(s => s.user);
  const [feedbackActivity, setFeedbackActivity] = useState<ActivityRunDto | null>(null);
  const reviewing = !mission?.retry && !!mission?.latestRunId && (mission.reviewOnly || mission.status === "Completed");
  const key = ["mission-run", actor?.centerId, actor?.userId, subjectId, mission?.missionId, reviewing, reviewing ? mission?.latestRunId : null, !!mission?.retry, mission?.questionCount ?? null];
  const run = useQuery({ queryKey: key, queryFn: () => reviewing ? getMissionRun(mission!.missionId, mission!.latestRunId!)
    : startLearningMission(mission!.missionId, { retry: mission!.retry, questionCount: mission!.questionCount || undefined }), enabled: isOpen && !!mission,
    staleTime: Infinity, retry: false, refetchOnWindowFocus: false });
  const current = reviewing ? undefined : run.data?.activities.find(a => !done(a));
  const evaluation = useQuery({ queryKey: ["mission-evaluation", actor?.centerId, actor?.userId, run.data?.sessionRunId],
    queryFn: () => evaluateMissionRunV4(run.data!.sessionRunId), enabled: !reviewing && !!run.data && !current && !feedbackActivity && run.data.activities.length > 0 && run.data.activities.every(done),
    retry: false, refetchInterval: query => query.state.data?.outcome === "PendingGrading" ? 3000 : false, refetchIntervalInBackground: true });
  // Drop the cached run on close: reopening must read the server state (saved draft, hints, a cancelled or finished run).
  const close = () => { cache.removeQueries({ queryKey: ["mission-run"] }); void cache.invalidateQueries({ queryKey: ["learning-agenda"] }); void cache.invalidateQueries({ queryKey: ["learning-paths"] }); onClose(); };
  const [paused, setPaused] = useState(false);
  const [confirmCancel, setConfirmCancel] = useState(false);
  const [activeSeconds, setActiveSeconds] = useState(0);
  const runActive = !reviewing && run.data?.status === "InProgress";
  // The clock only counts active study time; it stops while paused or between sets.
  useEffect(() => {
    if (paused || !runActive || !current) return;
    const timer = setInterval(() => setActiveSeconds(v => v + 1), 1000);
    return () => clearInterval(timer);
  }, [paused, runActive, current]);
  const abandon = useMutation({ mutationFn: () => abandonMissionRun(mission!.missionId, run.data!.sessionRunId), onSuccess: () => close() });
  const submittedCount = run.data?.activities.filter(done).length ?? 0;
  const independentRun = !!run.data?.activities.some(a => a.activityType === "IndependentCheck" || a.activityType === "TransferChallenge");
  const cancelBlocked = independentRun && submittedCount > 0;
  if (!isOpen || !mission) return null;
  const ongoing = ongoingMissionFromError(run.error, mission);
  return <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-3 sm:p-6">
    <section role="dialog" aria-modal="true" aria-labelledby="mission-title" className="flex max-h-[94vh] w-full max-w-3xl flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white text-slate-900 shadow-2xl dark:bg-slate-900 dark:text-slate-100">
      <header className="z-10 flex shrink-0 items-start justify-between gap-4 border-b border-slate-200 bg-white p-5 dark:border-slate-700 dark:bg-slate-900">
        <div>{contextLabel && <p className="mb-1 text-xs font-medium text-slate-500">{contextLabel}</p>}<p className="text-xs font-semibold uppercase tracking-wider text-indigo-600">{mission.skillName}</p><h2 id="mission-title" className="mt-1 text-xl font-semibold">{mission.title}</h2>
          {run.data && <p className="mt-2 text-xs text-slate-500">{run.data.activities.filter(done).length}/{run.data.activities.length} hoạt động đã nộp · Tiến độ lưu theo tài khoản</p>}</div>
        <div className="flex shrink-0 items-center gap-2">
          {runActive && <span className="font-mono text-sm tabular-nums text-slate-500" title="Thời gian học thực tế (không tính lúc tạm dừng)">{clock(activeSeconds)}</span>}
          {runActive && !paused && <button className="rounded-lg border px-3 py-1 text-sm" onClick={() => { setPaused(true); setConfirmCancel(false); }}>Tạm dừng</button>}
          <button className="rounded-lg border px-3 py-1" aria-label="Đóng và giữ tiến độ" title="Đóng · tiến độ và bản nháp được giữ" onClick={close}>×</button>
        </div>
      </header>
      <div className="min-h-0 space-y-5 overflow-y-auto p-5 sm:p-7">
        {reviewing && <p className="rounded-xl bg-slate-50 p-3 text-sm text-slate-600">Đang xem lại lượt học đã lưu. Không tạo lượt làm mới.</p>}
        {run.isPending && <p role="status">Đang mở nhiệm vụ và khôi phục tiến độ…</p>}
        {run.error && <div role="alert" className="rounded-xl bg-rose-50 p-4 text-rose-800">
          <p>{ongoing ? `Bạn đang học dở “${ongoing.title}” trong môn này. Tiếp tục buổi đó trước khi mở buổi mới; tiến độ của bạn vẫn được giữ.` : extractProblemDetails(run.error).message}</p>
          {ongoing ? <button className="mt-3 rounded-lg bg-indigo-600 px-4 py-2 font-semibold text-white" onClick={() => onResumeMission(ongoing)}>Tiếp tục: {ongoing.title}</button>
            : <button className="mt-3 underline" onClick={() => void run.refetch()}>Thử lại</button>}</div>}
        {run.data && <div><div className="h-2 overflow-hidden rounded-full bg-slate-100"><div className="h-full rounded-full bg-indigo-600 transition-all" style={{ width: `${100 * run.data.activities.filter(done).length / Math.max(1, run.data.activities.length)}%` }} /></div>
          {!reviewing && !!mission.questionCount && (mission.purpose === "Practice" || mission.purpose === "Review") && run.data.activities.length !== mission.questionCount &&
            <p className="mt-2 text-xs text-slate-500">Bộ này đã mở trước đó với {run.data.activities.length} câu nên giữ nguyên để kết quả so sánh được. Thời gian mới áp dụng từ bộ tiếp theo.</p>}
          <div aria-label="Các câu trong buổi" className="mt-3 flex flex-wrap gap-2">{run.data.activities.map(a => <button key={a.activityRunId} disabled={!done(a) || (!a.learningFeedback && !a.content)} onClick={() => setFeedbackActivity(a)} className={`h-9 w-9 rounded-lg border text-sm ${done(a) ? "border-indigo-200 bg-indigo-50 text-indigo-700" : "border-slate-200"}`} aria-label={`Xem lại câu ${a.activityOrder}`}>{a.activityOrder}</button>)}</div></div>}
        {paused && run.data && <div className="space-y-4 rounded-xl border border-slate-200 bg-slate-50 p-5 dark:border-slate-700 dark:bg-slate-800">
          <h3 className="text-lg font-semibold">Đang tạm dừng</h3>
          <p className="text-sm text-slate-600 dark:text-slate-300">Đã học {formatDuration(activeSeconds)} trong lượt này · {submittedCount}/{run.data.activities.length} hoạt động đã nộp. Bản nháp được lưu trên server; đồng hồ không chạy khi tạm dừng. Có thể thoát và quay lại sau, bài sẽ mở đúng chỗ đang làm.</p>
          <div className="flex flex-wrap gap-3">
            <button className="rounded-xl bg-indigo-600 px-5 py-3 font-semibold text-white" onClick={() => { setPaused(false); setConfirmCancel(false); }}>Tiếp tục học</button>
            <button className="rounded-xl border px-4 py-3" onClick={close}>Lưu và thoát</button>
            <button className="rounded-xl border border-rose-300 px-4 py-3 text-rose-700" onClick={() => setConfirmCancel(true)}>Hủy lượt này</button>
          </div>
          {confirmCancel && <div role="alertdialog" aria-label="Xác nhận hủy lượt" className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-900">
            {cancelBlocked ? <p>Bài tự kiểm tra đã có câu được nộp nên không hủy giữa chừng, để kết quả phản ánh đúng năng lực. Em có thể thoát và làm tiếp sau; tiến độ vẫn được giữ.</p>
              : <><p>Hủy lượt này? {submittedCount > 0 ? `${submittedCount} câu đã nộp vẫn được giữ trong lịch sử và hồ sơ năng lực; ` : ""}phần chưa nộp sẽ bị bỏ. Nhiệm vụ có thể bắt đầu lại bất cứ lúc nào{independentRun ? " với bộ câu mới" : ""}.</p>
                <div className="mt-3 flex flex-wrap gap-3"><button disabled={abandon.isPending} className="rounded-lg bg-rose-600 px-4 py-2 font-semibold text-white disabled:opacity-50" onClick={() => abandon.mutate()}>{abandon.isPending ? "Đang hủy…" : "Xác nhận hủy"}</button>
                  <button className="rounded-lg border border-rose-300 px-4 py-2" onClick={() => setConfirmCancel(false)}>Giữ lại</button></div></>}
            {abandon.error && <p className="mt-2">{extractProblemDetails(abandon.error).message}</p>}
          </div>}
        </div>}
        {feedbackActivity && !paused && <div className="space-y-4">{feedbackActivity.question && <RichMathText className="whitespace-pre-wrap text-base" text={feedbackActivity.question.question.questionText} />}
          {feedbackActivity.learningFeedback && <Feedback feedback={feedbackActivity.learningFeedback} />}
          {feedbackActivity.content && <div className="rounded-xl border p-4"><h3 className="font-semibold">Cách hiểu em đã nộp</h3><p className="mt-2 whitespace-pre-wrap text-sm">{feedbackActivity.submittedResponse ?? "Chưa có nội dung để hiển thị."}</p></div>}
          {(feedbackActivity.learningFeedback || feedbackActivity.content) && <TutorFeedback activity={feedbackActivity} automatic />}
          <button className="rounded-xl bg-indigo-600 px-5 py-3 font-semibold text-white" onClick={() => setFeedbackActivity(null)}>{current ? "Câu tiếp theo" : "Xem tổng kết buổi học"}</button></div>}
        {current && !feedbackActivity && <div hidden={paused}><Activity key={current.activityRunId} activity={current} paused={paused} onSubmitted={(res, submittedResponse) => {
          const updated = { ...current, status: res.activityStatus, learningFeedback: res.learningFeedback, submittedResponse };
          cache.setQueryData<MissionRunDto>(key, old => old ? { ...old, activities: old.activities.map(a => a.activityRunId === current.activityRunId ? updated : a) } : old);
          if ((current.activityType === "Practice" && res.learningFeedback) || current.content) setFeedbackActivity(updated);
        }} /></div>}
        {reviewing && run.data && !feedbackActivity && <div className="rounded-xl bg-indigo-50 p-4 text-indigo-950"><h3 className="font-semibold">{outcomeNames[run.data.outcome] ?? "Bài làm đã lưu"}</h3><p className="mt-2 text-sm">Chọn số câu hoặc mở phần bên dưới để xem lại câu trả lời và lời giải đã lưu.</p></div>}
        {!reviewing && evaluation.isFetching && !evaluation.data && run.data && !current && !feedbackActivity && <p role="status">Đang tổng kết buổi học…</p>}
        {!reviewing && evaluation.error && <div role="alert" className="rounded-xl bg-rose-50 p-4 text-rose-800">Chưa lấy được kết quả. Bài đã nộp vẫn được giữ. {extractProblemDetails(evaluation.error).message}
          <button className="ml-2 underline" onClick={() => void evaluation.refetch()}>Đánh giá lại</button></div>}
        {!reviewing && evaluation.data && !feedbackActivity && (
          evaluation.data.outcome === "PendingGrading" ? (
            <div className="rounded-xl border border-amber-200 bg-amber-50 p-5 text-amber-950">
              <div className="flex items-center gap-3">
                <span className="inline-block h-5 w-5 animate-spin rounded-full border-2 border-amber-600 border-t-transparent" />
                <h3 className="text-lg font-semibold">Đang chấm điểm và phân tích lập luận bằng AI…</h3>
              </div>
              <p className="mt-2 text-sm leading-relaxed text-amber-900">
                Bài làm của bạn đã được ghi nhận an toàn. AI đang phân tích từng bước lập luận (thường mất 5-15 giây). Trang sẽ tự động hiển thị kết quả ngay khi chấm xong.
              </p>
              <div className="mt-4 flex flex-wrap items-center gap-3">
                <button
                  className="rounded-lg bg-amber-600 px-4 py-2 text-sm font-medium text-white hover:bg-amber-700"
                  onClick={() => void evaluation.refetch()}
                >
                  Kiểm tra kết quả ngay
                </button>
                <button className="rounded-lg border border-amber-300 px-4 py-2 text-sm text-amber-900 hover:bg-amber-100" onClick={close}>
                  Về tuyến học
                </button>
              </div>
            </div>
          ) : (
            <div className="rounded-xl border border-indigo-200 bg-indigo-50 p-5 text-indigo-950">
              <h3 className="text-xl font-semibold">{nextLabel ? "Đã xong phần kiểm tra này" : outcomeNames[evaluation.data.outcome] ?? evaluation.data.outcome}</h3>
              {evaluation.data.isProvisional && <p className="mt-2 inline-block rounded-full bg-amber-100 px-3 py-1 text-xs font-semibold text-amber-900">Tạm đạt · chờ giáo viên xác nhận</p>}
              {evaluation.data.practiceAccuracy != null && <p className="mt-3 text-3xl font-bold">{Math.round(evaluation.data.practiceAccuracy * 100)}%</p>}
              {evaluation.data.decisionReasons.map((r, i) => <p className="mt-3 text-sm leading-relaxed" key={i}>{r}</p>)}
              {evaluation.data.nextStep && <p className="mt-3 rounded-lg bg-white p-3 text-sm font-medium dark:bg-slate-900">Bước tiếp theo: {evaluation.data.nextStep}</p>}
              <p className="mt-4 text-sm">{nextLabel ? "Kết quả phần này được dùng để lập lộ trình. Nhận xét chi tiết có trong trang kết quả sau khi làm xong các phần." : "Quay lại tuyến học để xem bước tiếp theo và lịch ôn. Hoàn thành phần luyện chưa thay thế kết quả tự kiểm tra."}</p>
              <button className="mt-4 rounded-lg bg-indigo-600 px-4 py-2 text-white" onClick={close}>{nextLabel ?? "Về tuyến học"}</button>
            </div>
          )
        )}
        {!current && run.data?.activities.filter(a => a.content && done(a)).map(a => {
          const p = (a.content?.payload ?? {}) as Record<string, unknown>;
          return <details key={a.activityRunId} className="rounded-xl border border-slate-200 p-4"><summary className="cursor-pointer font-semibold">Xem lại: {a.content?.title}</summary>
            <p className="mt-3 whitespace-pre-wrap text-sm">{a.content?.description}</p>
            <p className="mt-3 text-sm font-semibold">Cách hiểu em đã nộp</p><p className="mt-1 whitespace-pre-wrap text-sm">{a.submittedResponse}</p>
            {text(p.example) && <RichMathText className="mt-3" text={text(p.example)} />}
            {text(p.solution) && <RichMathText className="mt-3" text={text(p.solution)} />}
            {Array.isArray(p.steps) && (p.steps as Record<string, unknown>[]).map((s, i) => <p key={i} className="mt-3 whitespace-pre-wrap text-sm">{text(s.title)} {text(s.content)}</p>)}
            <TutorFeedback activity={a} />
          </details>;
        })}
        {!current && !feedbackActivity && run.data?.activities.filter(a => a.question && done(a)).map(a => <details key={a.activityRunId} className="rounded-xl border border-slate-200 p-4"><summary className="cursor-pointer font-semibold">Xem lại câu {a.activityOrder}</summary><RichMathText className="mt-3" text={a.question!.question.questionText} />
          {a.learningFeedback ? <><Feedback feedback={a.learningFeedback} /><TutorFeedback activity={a} /></> : <button className="mt-3 underline" onClick={() => void run.refetch()}>Tải lời giải sau khi nộp cả bộ</button>}</details>)}
      </div>
    </section>
  </div>;
}

function clock(seconds: number): string {
  const h = Math.floor(seconds / 3600), m = Math.floor((seconds % 3600) / 60), sec = seconds % 60;
  return `${h > 0 ? `${h}:` : ""}${String(m).padStart(h > 0 ? 2 : 1, "0")}:${String(sec).padStart(2, "0")}`;
}
function formatDuration(seconds: number): string {
  const m = Math.floor(seconds / 60);
  return m < 1 ? "chưa tới 1 phút" : m < 60 ? `${m} phút` : `${Math.floor(m / 60)} giờ ${m % 60} phút`;
}
/** Worked example: the student predicts each step before revealing it, then compares with the answer. */
function WorkedExample({ index, example }: { index: number; example: { question: string; steps: string[]; answer: string } }) {
  const [shown, setShown] = useState(0);
  const total = example.steps.length;
  return <section className="rounded-lg border border-slate-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-900">
    <p className="text-xs font-semibold uppercase tracking-wider text-indigo-600">Ví dụ mẫu {index}</p>
    <RichMathText className="mt-2 whitespace-pre-wrap text-sm" text={example.question} />
    {total > 0 && <ol className="mt-3 space-y-2">{example.steps.slice(0, shown).map((s, i) => <li key={i} className="rounded-md bg-slate-50 p-2 text-sm dark:bg-slate-800">
      <span className="mr-2 font-semibold">Bước {i + 1}.</span><RichMathText className="inline whitespace-pre-wrap" text={s} /></li>)}</ol>}
    {shown < total ? <button type="button" className="mt-3 rounded-lg border px-3 py-1.5 text-sm" onClick={() => setShown(v => v + 1)}>
      {shown === 0 ? "Thử tự nghĩ bước 1, rồi xem" : `Xem bước ${shown + 1}/${total}`}</button>
      : example.answer && <p className="mt-3 text-sm"><strong>Đáp án: </strong>{example.answer}</p>}
  </section>;
}
function text(value: unknown): string { return typeof value === "string" || typeof value === "number" ? String(value) : ""; }
function Feedback({ feedback: f }: { feedback: QuestionLearningFeedbackDto }) {
  return <section aria-label="Phản hồi câu trả lời" className="space-y-3 rounded-xl border border-indigo-200 bg-indigo-50/60 p-5 text-slate-900">
    <h3 className="text-lg font-semibold">{f.isCorrect == null ? "Đã lưu bài, đang chờ chấm" : f.isCorrect ? "Đáp án đúng" : "Cùng sửa câu này"}</h3>
    <p className="text-xs text-slate-500">{f.status === "Reviewed" ? "Kết quả đã xử lý" : f.status === "PendingTeacherReview" ? "Còn chờ giáo viên xác nhận" : "Phản hồi ban đầu · nhận xét sâu có thể bổ sung sau"}</p>
    <div className="grid gap-3 sm:grid-cols-2"><div className="rounded-lg bg-white p-3"><p className="text-xs text-slate-500">Em đã trả lời</p><RichMathText className="mt-1" text={f.submittedAnswer} /></div><div className="rounded-lg bg-white p-3"><p className="text-xs text-slate-500">Đáp án tham khảo</p><RichMathText className="mt-1" text={f.correctAnswer} /></div></div>
    <div><p className="font-semibold">Lời giải ngân hàng · nguyên văn</p><RichMathText className="mt-1 whitespace-pre-wrap" text={f.explanation || "Câu này chưa có lời giải được biên soạn."} /></div>
    <details><summary className="cursor-pointer font-semibold">Cách tiếp cận dạng bài</summary><RichMathText className="mt-2 whitespace-pre-wrap" text={f.methodGuide} /></details>
    <p className="rounded-lg border border-indigo-100 bg-white p-3 text-sm">{f.nextStep}</p>
  </section>;
}
function TutorFeedback({ activity, automatic = false }: { activity: ActivityRunDto; automatic?: boolean }) {
  const actor = useAuthStore(s => s.user);
  const [requested, setRequested] = useState(automatic || !!activity.aiFeedback);
  const query = useQuery({ queryKey: ["learning-tutor", actor?.centerId, actor?.userId, activity.activityRunId], queryFn: () => getActivityAiFeedback(activity.activityRunId),
    initialData: activity.aiFeedback ?? undefined, enabled: requested, staleTime: Infinity, retry: false, refetchOnWindowFocus: false });
  const value = query.data;
  const understanding: Record<string, string> = { Understood: "Cách hiểu phù hợp", PartlyUnderstood: "Hiểu đúng một phần, cần bổ sung", Misunderstood: "Có điểm cần sửa", NotEnoughContext: "Cần diễn giải thêm để đánh giá" };
  return <section aria-label="Nhận xét trợ giảng AI" className="mt-4 space-y-3 rounded-xl border border-violet-200 bg-violet-50 p-5 text-slate-900">
    <h3 className="font-semibold">Trợ giảng AI · tiếng Việt</h3>
    {!requested && <button className="rounded-lg border border-violet-200 bg-white px-3 py-2 text-sm font-semibold" onClick={() => setRequested(true)}>Nhờ AI nhận xét và giải thích</button>}
    {requested && query.isPending && <p role="status" className="text-sm">AI đang đọc bài và chuẩn bị nhận xét. Em có thể tiếp tục học trong lúc chờ.</p>}
    {(query.error || value?.status === "Unavailable") && <div className="text-sm"><p>{value?.comment || "Chưa lấy được nhận xét AI. Bài làm vẫn được giữ."}</p><button className="mt-2 font-semibold underline" onClick={() => void query.refetch()}>Thử nhận xét AI lại</button></div>}
    {value?.status === "Generated" && <><p className="text-sm font-semibold">{understanding[value.understanding] ?? "Nhận xét tham khảo"}</p><RichMathText className="whitespace-pre-wrap text-sm" text={value.comment} />
      <h4 className="font-semibold">{activity.content ? "Cách hiểu đúng và ví dụ của AI" : "Lời giải riêng của AI"}</h4><RichMathText className="whitespace-pre-wrap text-sm" text={value.aiSolution} />
      <h4 className="font-semibold">Điểm cần sửa / bước tiếp theo</h4><RichMathText className="whitespace-pre-wrap text-sm" text={value.correction} />
      <p className="text-xs text-slate-500">Nhận xét AI để hỗ trợ học; không thay điểm chấm hoặc xác nhận thành thạo. {value.generatedAt && new Date(value.generatedAt).toLocaleString("vi-VN")}</p></>}
  </section>;
}
function Activity({ activity: a, onSubmitted, paused }: { activity: ActivityRunDto; paused: boolean; onSubmitted: (res: ActivitySubmissionResponseDto, response: string) => void }) {
  const restored = (() => { try { return JSON.parse(a.draftPayload ?? "{}") as Record<string, unknown>; } catch { return {}; } })();
  const [answer, setAnswer] = useState(text(restored.finalAnswer));
  const [reasoning, setReasoning] = useState(text(restored.reasoningText));
  const [errorStep, setErrorStep] = useState(text(restored.selectedErrorStep));
  const [revision, setRevision] = useState(a.draftRevision);
  const [lastSaved, setLastSaved] = useState(a.draftPayload ?? "");
  const [notice, setNotice] = useState("");
  const [conflict, setConflict] = useState(false);
  const [submissionId] = useState(() => crypto.randomUUID());
  // Time spent excludes paused periods.
  const activeMs = useRef(0);
  const since = useRef<number | null>(Date.now());
  useEffect(() => {
    if (paused && since.current != null) { activeMs.current += Date.now() - since.current; since.current = null; }
    else if (!paused && since.current == null) since.current = Date.now();
  }, [paused]);
  const elapsedSeconds = () => Math.floor((activeMs.current + (since.current != null ? Date.now() - since.current : 0)) / 1000);
  const q = a.question?.question;
  const payload = (a.content?.payload ?? {}) as Record<string, unknown>;
  const steps = Array.isArray(payload.steps) ? payload.steps as Record<string, unknown>[] : [];
  const workedExamples = Array.isArray(payload.examples) ? (payload.examples as Record<string, unknown>[])
    .map(e => ({ question: text(e.question), steps: Array.isArray(e.steps) ? (e.steps as unknown[]).map(text).filter(Boolean) : [], answer: text(e.answer) }))
    .filter(e => e.question) : [];
  const currentDraft = JSON.stringify({ finalAnswer: answer, reasoningText: reasoning, selectedErrorStep: errorStep });
  const save = useMutation({ mutationFn: (draft: string) => saveActivityDraft(a.activityRunId, { expectedRevision: revision, draftPayload: draft }),
    onSuccess: (res, draft) => { if (res.hasConflict) { setConflict(true); setNotice("Bản nháp đã thay đổi ở thiết bị khác. Giữ nội dung hiện tại và mở lại nhiệm vụ để xem bản đã lưu; không tự ghi đè."); }
      else { setRevision(res.draftRevision); setLastSaved(draft); setNotice("Đã lưu bản nháp trên server."); } },
    onError: e => setNotice(`Chưa đồng bộ bản nháp: ${e.message}. Nội dung vẫn ở màn hình; hãy lưu lại trước khi đóng.`) });
  const submit = useMutation({ mutationFn: () => submitActivity(a.activityRunId, { clientSubmissionId: submissionId,
    questionId: q ? q.questionId : undefined, finalAnswer: answer, reasoningText: reasoning || undefined,
    selectedErrorStep: errorStep || undefined, timeSpentSeconds: elapsedSeconds() }),
    onSuccess: res => onSubmitted(res, answer) });
  const [hints, setHints] = useState<AssistanceResponseDto[]>([]);
  const addHints = (list: AssistanceResponseDto[]) => setHints(h => [...h, ...list].filter((x, i, all) => all.findIndex(y => y.hintLevel === x.hintLevel) === i)
    .sort((x, y) => (x.hintLevel ?? 0) - (y.hintLevel ?? 0)));
  const assist = useMutation({ mutationFn: (step: number) => requestActivityAssistance(a.activityRunId, { stepIndex: step }), onSuccess: r => addHints([r]) });
  useEffect(() => {
    if (conflict || save.isPending || submit.isPending || currentDraft === lastSaved || (!answer && !reasoning && !errorStep)) return;
    const timer = setTimeout(() => save.mutate(currentDraft), 1200);
    return () => clearTimeout(timer);
  }, [currentDraft, lastSaved, answer, reasoning, errorStep, conflict, save.isPending, submit.isPending, save.mutate]);
  const independent = a.activityType === "IndependentCheck" || a.activityType === "TransferChallenge";
  // Re-open hints already revealed for this activity (e.g. after pausing or reloading); the server does not count them again.
  const restoredHints = useRef(false);
  useEffect(() => {
    if (restoredHints.current || independent || !a.assistanceRequestedCount) return;
    restoredHints.current = true;
    void Promise.all(Array.from({ length: a.assistanceRequestedCount }, (_, i) => requestActivityAssistance(a.activityRunId, { stepIndex: i })))
      .then(addHints).catch(() => undefined);
  }, [a.activityRunId, a.assistanceRequestedCount, independent]);
  // Pausing flushes the draft immediately instead of waiting for the autosave delay.
  useEffect(() => {
    if (paused && !conflict && currentDraft !== lastSaved && (answer || reasoning || errorStep)) save.mutate(currentDraft);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [paused]);
  const lastHint = hints[hints.length - 1];
  const moreHints = !lastHint || !!lastHint.hasMoreHints;
  const field = "mt-2 w-full rounded-lg border border-slate-300 bg-white p-3 dark:bg-slate-900";
  return <form className="space-y-5" onSubmit={e => { e.preventDefault(); submit.mutate(); }}>
    <div><p className="text-sm font-semibold text-indigo-600">{a.stage || activityNames[a.activityType] || a.activityType} · Câu / hoạt động {a.activityOrder}</p>
      {independent && <p className="mt-2 rounded-lg bg-amber-50 p-3 text-sm text-amber-900">Tự làm, không có gợi ý hoặc lời giải trước khi nộp. Kết quả còn qua chấm và kiểm duyệt bằng chứng.</p>}</div>
    {a.content && <article className="space-y-3 rounded-xl bg-slate-50 p-4 dark:bg-slate-800"><h3 className="text-lg font-semibold">{a.content.title}</h3>
      {a.content.description && <p className="whitespace-pre-wrap text-sm leading-relaxed">{a.content.description}</p>}
      {steps.map((step, i) => <div className="rounded-lg border border-slate-200 p-3" key={i}><p className="font-medium">{text(step.title) || `Bước ${i + 1}`}</p><p className="mt-1 whitespace-pre-wrap text-sm">{text(step.content) || text(step.explanation)}</p></div>)}
      {workedExamples.length > 0 ? workedExamples.map((ex, i) => <WorkedExample key={i} index={i + 1} example={ex} />) : <>
        {text(payload.example) && <p className="whitespace-pre-wrap text-sm"><strong>Ví dụ: </strong>{text(payload.example)}</p>}
        {text(payload.solution) && <p className="whitespace-pre-wrap text-sm"><strong>Cách giải đã xuất bản: </strong>{text(payload.solution)}</p>}</>}
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
    {hints.length > 0 && <section aria-label="Gợi ý" className="space-y-3 rounded-xl border border-amber-200 bg-amber-50 p-4 text-slate-900">
      {hints.map(h => <div key={h.hintLevel}><p className="text-xs font-semibold uppercase tracking-wider text-amber-700">Gợi ý {h.hintLevel}/{h.totalHints} · {h.hintTitle}</p>
        <RichMathText className="mt-1 whitespace-pre-wrap text-sm leading-relaxed" text={h.explanation} /></div>)}
      {lastHint?.policyNotice && <p className="text-xs text-amber-800">{lastHint.policyNotice}</p>}
    </section>}
    {notice && <p role="status" className="text-sm text-slate-500">{notice}</p>}
    {(submit.error || assist.error) && <p role="alert" className="rounded-lg bg-rose-50 p-3 text-sm text-rose-800">{(submit.error ?? assist.error)?.message}</p>}
    <div className="flex flex-wrap gap-3"><button disabled={submit.isPending || save.isPending || conflict} type="submit" className="rounded-xl bg-indigo-600 px-5 py-3 font-semibold text-white disabled:opacity-40">{submit.isPending ? "Đang lưu bài…" : q ? "Nộp câu trả lời" : "Hoàn thành hoạt động"}</button>
      <button type="button" disabled={save.isPending || conflict} className="rounded-xl border px-4 py-3" onClick={() => save.mutate(currentDraft)}>Lưu bản nháp</button>
      {!independent && <button type="button" disabled={assist.isPending || !moreHints} className="rounded-xl border border-amber-300 px-4 py-3 text-amber-900 disabled:opacity-50"
        onClick={() => assist.mutate(hints.length)}>{assist.isPending ? "Đang mở gợi ý…" : hints.length === 0 ? "Gợi ý" : moreHints ? `Gợi ý tiếp (${hints.length + 1}/${lastHint?.totalHints ?? "?"})` : "Đã mở hết gợi ý"}</button>}
    </div>
  </form>;
}
