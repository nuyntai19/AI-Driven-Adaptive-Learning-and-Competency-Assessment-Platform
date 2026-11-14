import React from "react";
import { useStudentTheme } from "../hooks/useStudentTheme";
import { useParams, Link, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { adaptivePracticeApi } from "../api/adaptivePracticeApi";
import { answeredQuestionCount, formatPracticeDuration, practiceEndLabel } from "../utils/adaptivePracticeSummary";

const PANEL = "rounded-2xl border border-slate-200/80 bg-white p-4 sm:p-5 dark:border-slate-800 dark:bg-[#0f172a]";
const PRESS = "transition-[background-color,transform] duration-150 ease-out active:scale-[0.97] motion-reduce:transform-none";

export const AdaptivePracticeSummaryPage: React.FC = () => {
  useStudentTheme();
  const { sessionId } = useParams<{ sessionId: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const sessionQuery = useQuery({
    queryKey: ["adaptive-practice", "session", sessionId],
    queryFn: () => adaptivePracticeApi.getSessionById(sessionId!),
    enabled: !!sessionId,
  });
  const session = sessionQuery.data;

  const summaryQuery = useQuery({
    queryKey: ["adaptive-practice", "summary", sessionId],
    queryFn: () => adaptivePracticeApi.getSummary(sessionId!),
    enabled: !!sessionId,
  });
  const summary = summaryQuery.data;

  // Mutation to start follow-up practice session (BR-38: creates a new linked session)
  const followUpMutation = useMutation({
    mutationFn: () =>
      adaptivePracticeApi.createSession({
        subjectId: session!.subjectId,
        topicNodeId: summary?.recommendedNextTopicId || session!.topicNodeId,
        practiceMode: "Weakness",
        timeBudgetMinutes: 10,
      }),
    onSuccess: (newSession) => {
      queryClient.invalidateQueries({ queryKey: ["adaptive-practice"] });
      navigate(`/hoc-tap/luyen-tap/phien/${newSession.sessionId}`);
    },
  });

  if (summaryQuery.isLoading || sessionQuery.isLoading) {
    return (
      <div className="min-h-screen bg-slate-50 dark:bg-slate-950 flex flex-col items-center justify-center p-6 space-y-4">
        <div className="w-10 h-10 border-4 border-indigo-500 border-t-transparent rounded-full animate-spin" />
        <p className="text-sm font-semibold text-slate-600 dark:text-slate-300">
          Đang tổng hợp kết quả phiên luyện tập...
        </p>
      </div>
    );
  }

  if (!summary || !session) {
    return (
      <div className="min-h-screen bg-slate-50 dark:bg-slate-950 flex flex-col items-center justify-center p-6 space-y-4">
        <h2 className="text-base font-bold text-slate-800 dark:text-white">
          Chưa tìm thấy dữ liệu tổng kết
        </h2>
        <Link
          to="/hoc-tap/luyen-tap"
          className="px-5 py-2 rounded-xl bg-indigo-600 text-white font-semibold text-xs"
        >
          Quay lại trang luyện tập
        </Link>
      </div>
    );
  }

  const answered = answeredQuestionCount(summary);
  const accuracy = answered > 0 ? Math.round((summary.correctCount / answered) * 100) : 0;
  const endingMastery = summary.endingMastery ?? summary.startingMastery;
  const masteryChange = endingMastery - summary.startingMastery;
  const outOfQuestions = session.closeReason === "OutOfQuestions";
  const status = answered === 0
    ? { label: "Chưa có câu trả lời", tone: "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300" }
    : summary.goalAchievementStatus === "Achieved"
    ? { label: "Đạt mục tiêu", tone: "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300" }
    : summary.goalAchievementStatus === "RemediationRecommended"
    ? { label: "Cần củng cố kiến thức nền", tone: "bg-amber-100 text-amber-800 dark:bg-amber-950/60 dark:text-amber-300" }
    : { label: practiceEndLabel(session.closeReason), tone: "bg-indigo-100 text-indigo-800 dark:bg-indigo-950/60 dark:text-indigo-300" };
  const needsWork = [...summary.weakSkills, ...summary.remediationSkills.map((skill) => `Củng cố nền: ${skill}`)];
  const practiceUrl = `/hoc-tap/luyen-tap?subjectId=${session.subjectId}`;

  return (
    <div className="min-h-screen bg-stone-50/50 dark:bg-[var(--student-canvas)] text-stone-800 dark:text-stone-100 antialiased student-shell">
      <div className="w-full max-w-[1500px] mx-auto px-4 sm:px-6 py-5 space-y-4">
        <header className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div className="min-w-0">
            <Link to={practiceUrl} className="text-xs font-semibold text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-slate-200">
              ← Luyện tập thích ứng
            </Link>
            <h1 className="mt-1 text-xl sm:text-2xl font-bold tracking-tight text-slate-900 dark:text-white">{session.topicName}</h1>
            <p className="mt-0.5 text-sm text-slate-500 dark:text-slate-400">
              {session.subjectName} · {session.completedAt ? new Date(session.completedAt).toLocaleString("vi-VN") : "Hôm nay"}
            </p>
          </div>
          <span className={`self-start sm:self-auto rounded-lg px-3 py-1.5 text-sm font-semibold ${status.tone}`}>{status.label}</span>
        </header>

        <div className="grid grid-cols-1 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)] gap-4 items-start">
          <div className="min-w-0 space-y-4">
            <section aria-labelledby="summary-result-heading" className={PANEL}>
              <h2 id="summary-result-heading" className="text-base font-bold text-slate-900 dark:text-white">Kết quả phiên</h2>
              {answered > 0 ? (
                <>
                  <p className="mt-3 text-3xl font-bold tabular-nums text-slate-900 dark:text-white">
                    {summary.correctCount}<span className="text-base font-medium text-slate-400"> / {answered} câu đúng</span>
                  </p>
                  <dl className="mt-3 divide-y divide-slate-100 text-sm dark:divide-slate-800">
                    <div className="flex justify-between py-2"><dt className="text-slate-600 dark:text-slate-400">Tỷ lệ chính xác</dt><dd className="font-semibold tabular-nums text-slate-900 dark:text-white">{accuracy}%</dd></div>
                    <div className="flex justify-between py-2"><dt className="text-slate-600 dark:text-slate-400">Trả lời sai</dt><dd className="font-semibold tabular-nums text-slate-900 dark:text-white">{summary.incorrectCount} câu</dd></div>
                    <div className="flex justify-between py-2"><dt className="text-slate-600 dark:text-slate-400">Thời gian làm</dt><dd className="font-semibold tabular-nums text-slate-900 dark:text-white">{formatPracticeDuration(summary.timeSpentSeconds)}</dd></div>
                  </dl>
                </>
              ) : (
                <p className="mt-2 text-sm leading-relaxed text-slate-600 dark:text-slate-400">
                  {outOfQuestions
                    ? "Chủ đề này tạm hết câu hỏi khả dụng cho bạn, nên phiên kết thúc trước khi bắt đầu."
                    : "Phiên đã dừng trước khi bạn nộp câu trả lời nào."}
                </p>
              )}
            </section>

            <section aria-labelledby="summary-mastery-heading" className={PANEL}>
              <div className="flex items-baseline justify-between gap-3">
                <h2 id="summary-mastery-heading" className="text-base font-bold text-slate-900 dark:text-white">Năng lực chủ đề</h2>
                <span className={`text-xs font-semibold tabular-nums ${masteryChange > 0 ? "text-emerald-700 dark:text-emerald-400" : masteryChange < 0 ? "text-rose-700 dark:text-rose-400" : "text-slate-500 dark:text-slate-400"}`}>
                  {masteryChange === 0 ? "Không đổi" : `${masteryChange > 0 ? "+" : ""}${masteryChange.toFixed(0)}%`}
                </span>
              </div>
              <p className="mt-3 text-2xl font-bold tabular-nums text-slate-900 dark:text-white">
                <span className="text-slate-400">{summary.startingMastery.toFixed(0)}%</span>
                <span className="mx-2 text-base font-medium text-slate-400">→</span>
                {endingMastery.toFixed(0)}%
              </p>
              <div className="mt-2 flex h-2 overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800">
                <div className="bg-slate-400 dark:bg-slate-500" style={{ width: `${Math.min(100, Math.min(summary.startingMastery, endingMastery))}%` }} />
                <div className={masteryChange >= 0 ? "bg-emerald-500" : "bg-rose-500"} style={{ width: `${Math.min(100, Math.abs(masteryChange))}%` }} />
              </div>
            </section>
          </div>

          <div className="min-w-0 space-y-4">
            <section aria-labelledby="summary-analysis-heading" className={PANEL}>
              <h2 id="summary-analysis-heading" className="text-base font-bold text-slate-900 dark:text-white">Nhận xét</h2>
              <p className="mt-2 text-sm leading-relaxed text-slate-700 dark:text-slate-300 whitespace-pre-wrap">{summary.summaryConclusion}</p>
              {(summary.strongSkills.length > 0 || needsWork.length > 0) && (
                <div className="mt-3 grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-3 border-t border-slate-100 pt-3 dark:border-slate-800">
                  {summary.strongSkills.length > 0 && (
                    <div>
                      <h3 className="text-sm font-semibold text-emerald-700 dark:text-emerald-400">Làm tốt</h3>
                      <ul className="mt-1 space-y-1 text-sm text-slate-700 dark:text-slate-300">
                        {summary.strongSkills.map((skill) => <li key={skill}>{skill}</li>)}
                      </ul>
                    </div>
                  )}
                  {needsWork.length > 0 && (
                    <div>
                      <h3 className="text-sm font-semibold text-amber-700 dark:text-amber-400">Cần củng cố</h3>
                      <ul className="mt-1 space-y-1 text-sm text-slate-700 dark:text-slate-300">
                        {needsWork.map((skill) => <li key={skill}>{skill}</li>)}
                      </ul>
                    </div>
                  )}
                </div>
              )}
            </section>

            <section aria-labelledby="summary-next-heading" className={PANEL}>
              <h2 id="summary-next-heading" className="text-base font-bold text-slate-900 dark:text-white">Làm gì tiếp theo</h2>
              <p className="mt-1 text-sm leading-relaxed text-slate-600 dark:text-slate-400">
                {outOfQuestions && answered === 0 ? "Hãy chọn chủ đề khác để luyện tập." : summary.recommendedNextAction}
              </p>
              <div className="mt-3 flex flex-wrap gap-2">
                {!outOfQuestions && (
                  <button
                    type="button"
                    onClick={() => followUpMutation.mutate()}
                    disabled={followUpMutation.isPending}
                    className={`rounded-lg bg-indigo-600 hover:bg-indigo-700 px-4 py-2 text-sm font-semibold text-white cursor-pointer disabled:opacity-50 ${PRESS}`}
                  >
                    {followUpMutation.isPending ? "Đang tạo phiên mới..." : "Luyện thêm 10 phút"}
                  </button>
                )}
                <Link to={`/hoc-tap/lo-trinh-hoc-tap?subjectId=${session.subjectId}`} className={`rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-800 hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800 ${PRESS}`}>
                  Tiếp tục lộ trình
                </Link>
                <Link to={practiceUrl} className={`rounded-lg px-4 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800 ${PRESS}`}>
                  Chọn chủ đề khác
                </Link>
              </div>
              {followUpMutation.isError && (
                <p role="alert" className="mt-2 text-xs text-rose-700 dark:text-rose-300">Không tạo được phiên mới. Hãy chọn chủ đề khác để luyện tập.</p>
              )}
            </section>
          </div>
        </div>
      </div>
    </div>
  );
};
