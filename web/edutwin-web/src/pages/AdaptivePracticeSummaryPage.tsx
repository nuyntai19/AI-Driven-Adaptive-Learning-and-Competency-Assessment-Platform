import React from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { adaptivePracticeApi } from "../api/adaptivePracticeApi";

export const AdaptivePracticeSummaryPage: React.FC = () => {
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
        customTargetMastery: Math.min(100, (session!.currentMastery || 50) + 10),
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

  const isGoalAchieved = summary.goalAchievementStatus === "Achieved";
  const isRemediation = summary.goalAchievementStatus === "RemediationRecommended";

  return (
    <div className="w-full max-w-4xl mx-auto px-4 sm:px-6 py-10 space-y-8">
      {/* Top Header Card */}
      <div className="text-center space-y-2">
        <span className="px-3 py-1 rounded-full text-xs font-bold uppercase tracking-wider bg-indigo-50 text-indigo-700 dark:bg-indigo-950 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-800">
          Tổng kết phiên luyện tập thích ứng
        </span>
        <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-slate-900 dark:text-white">
          {session.topicName}
        </h1>
        <p className="text-xs text-slate-500 dark:text-slate-400">
          Môn học: {session.subjectName} · Thời điểm hoàn thành: {session.completedAt ? new Date(session.completedAt).toLocaleString("vi-VN") : "Hôm nay"}
        </p>
      </div>

      {/* Block 1: Kết quả phiên */}
      <div className="p-6 sm:p-8 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs space-y-6">
        <div className="flex flex-wrap items-center justify-between gap-4 pb-4 border-b border-slate-100 dark:border-slate-800">
          <div className="space-y-1">
            <span className="text-xs font-semibold text-slate-400">Trạng thái mục tiêu:</span>
            <div className="flex items-center gap-2">
              <span
                className={`px-3 py-1 rounded-full text-xs font-bold ${
                  isGoalAchieved
                    ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 border border-emerald-300"
                    : isRemediation
                    ? "bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300 border border-amber-300"
                    : "bg-indigo-100 text-indigo-800 dark:bg-indigo-950 dark:text-indigo-300 border border-indigo-300"
                }`}
              >
                {isGoalAchieved
                  ? "✓ Đạt mục tiêu năng lực"
                  : isRemediation
                  ? "⚠ Cần củng cố kiến thức nền"
                  : "Đã hoàn thành phiên"}
              </span>
            </div>
          </div>

          <div className="text-right">
            <div className="text-xs text-slate-400 font-medium">Thời lượng thực hiện:</div>
            <div className="text-base font-bold text-slate-800 dark:text-slate-200">
              {Math.round(summary.timeSpentSeconds / 60)} phút ({summary.timeSpentSeconds}s)
            </div>
          </div>
        </div>

        {/* Stats Grid */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
          <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/60 dark:border-slate-700/60 text-center">
            <div className="text-2xl font-black text-slate-800 dark:text-slate-100">{summary.totalQuestions}</div>
            <div className="text-xs text-slate-500 dark:text-slate-400 font-medium mt-0.5">Số câu đã làm</div>
          </div>
          <div className="p-4 rounded-2xl bg-emerald-50/70 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800/60 text-center">
            <div className="text-2xl font-black text-emerald-600 dark:text-emerald-400">{summary.correctCount}</div>
            <div className="text-xs text-emerald-700 dark:text-emerald-300 font-medium mt-0.5">Trả lời đúng</div>
          </div>
          <div className="p-4 rounded-2xl bg-rose-50/70 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-800/60 text-center">
            <div className="text-2xl font-black text-rose-600 dark:text-rose-400">{summary.incorrectCount}</div>
            <div className="text-xs text-rose-700 dark:text-rose-300 font-medium mt-0.5">Trả lời sai</div>
          </div>
          <div className="p-4 rounded-2xl bg-indigo-50/70 dark:bg-indigo-950/40 border border-indigo-200 dark:border-indigo-800/60 text-center">
            <div className="text-2xl font-black text-indigo-600 dark:text-indigo-400">
              {summary.totalQuestions > 0 ? Math.round((summary.correctCount / summary.totalQuestions) * 100) : 0}%
            </div>
            <div className="text-xs text-indigo-700 dark:text-indigo-300 font-medium mt-0.5">Tỷ lệ chính xác</div>
          </div>
        </div>
      </div>

      {/* Block 2: Đánh giá kỹ năng & Năng lực */}
      <div className="p-6 sm:p-8 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs space-y-6">
        <h3 className="text-base font-bold text-slate-900 dark:text-white pb-3 border-b border-slate-100 dark:border-slate-800">
          Đánh giá năng lực & Kỹ năng
        </h3>

        {/* Starting vs Ending Mastery Bar */}
        <div className="space-y-2">
          <div className="flex justify-between text-xs font-semibold">
            <span className="text-slate-500">Trước phiên: {summary.startingMastery.toFixed(0)}%</span>
            <span className="text-indigo-600 dark:text-indigo-400">
              Năng lực xác nhận sau phiên: <strong>{summary.endingMastery ? summary.endingMastery.toFixed(0) : summary.startingMastery.toFixed(0)}%</strong>
            </span>
          </div>
          <div className="h-4 w-full bg-slate-100 dark:bg-slate-800 rounded-full overflow-hidden flex">
            <div
              className="bg-slate-400 dark:bg-slate-600 rounded-l-full transition-all"
              style={{ width: `${Math.min(100, summary.startingMastery)}%` }}
            />
            <div
              className="bg-emerald-500 dark:bg-emerald-400 rounded-r-full transition-all"
              style={{
                width: `${Math.max(0, (summary.endingMastery ?? summary.startingMastery) - summary.startingMastery)}%`,
              }}
            />
          </div>
        </div>

        {/* Skills breakdown */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2">
          {/* Strong Skills */}
          <div className="p-4 rounded-2xl bg-emerald-50/50 dark:bg-emerald-950/30 border border-emerald-200/80 dark:border-emerald-800/60 space-y-2">
            <div className="text-xs font-bold text-emerald-800 dark:text-emerald-300 flex items-center gap-1.5">
              <span>✓</span>
              <span>Kỹ năng thể hiện tốt & Vững vàng</span>
            </div>
            {summary.strongSkills.length > 0 ? (
              <ul className="text-xs text-slate-700 dark:text-slate-300 space-y-1 list-disc list-inside">
                {summary.strongSkills.map((sk, idx) => (
                  <li key={idx}>{sk}</li>
                ))}
              </ul>
            ) : (
              <p className="text-xs text-slate-400 italic">Chưa đủ dữ liệu để ghi nhận kỹ năng vững vàng.</p>
            )}
          </div>

          {/* Weak / Remediation Skills */}
          <div className="p-4 rounded-2xl bg-amber-50/50 dark:bg-amber-950/30 border border-amber-200/80 dark:border-amber-800/60 space-y-2">
            <div className="text-xs font-bold text-amber-800 dark:text-amber-300 flex items-center gap-1.5">
              <span>⚠</span>
              <span>Kỹ năng cần củng cố thêm</span>
            </div>
            {summary.weakSkills.length > 0 || summary.remediationSkills.length > 0 ? (
              <ul className="text-xs text-slate-700 dark:text-slate-300 space-y-1 list-disc list-inside">
                {summary.weakSkills.map((sk, idx) => (
                  <li key={idx}>{sk}</li>
                ))}
                {summary.remediationSkills.map((sk, idx) => (
                  <li key={`rem-${idx}`} className="font-semibold text-amber-700 dark:text-amber-400">
                    Củng cố nền: {sk}
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-xs text-slate-400 italic">Không phát hiện điểm yếu nghiêm trọng.</p>
            )}
          </div>
        </div>
      </div>

      {/* Block 3: Giải thích kết quả từ EduTwin */}
      <div className="p-6 sm:p-8 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs space-y-3">
        <h3 className="text-base font-bold text-slate-900 dark:text-white flex items-center gap-2">
          <span>🧠</span>
          <span>Phân tích kết quả từ EduTwin</span>
        </h3>
        <p className="text-xs sm:text-sm text-slate-700 dark:text-slate-300 leading-relaxed whitespace-pre-wrap">
          {summary.summaryConclusion}
        </p>
      </div>

      {/* Block 4: Hành động tiếp theo */}
      <div className="p-6 sm:p-8 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs space-y-4">
        <h3 className="text-base font-bold text-slate-900 dark:text-white">
          Hành động tiếp theo được đề xuất
        </h3>
        <p className="text-xs text-slate-600 dark:text-slate-400">
          {summary.recommendedNextAction}
        </p>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-2">
          {/* Action 1: Luyện thêm 1 phiên ngắn */}
          <button
            type="button"
            onClick={() => followUpMutation.mutate()}
            disabled={followUpMutation.isPending}
            className="p-4 rounded-2xl bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-xs shadow-md flex items-center justify-between cursor-pointer transition-all"
          >
            <span>{followUpMutation.isPending ? "Đang tạo phiên mới..." : "Luyện thêm 1 phiên ngắn (10 phút)"}</span>
            <span>→</span>
          </button>

          {/* Action 2: Quay lại Learning Path */}
          <Link
            to={`/hoc-tap/lo-trinh-hoc-tap?subjectId=${session.subjectId}`}
            className="p-4 rounded-2xl bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200 font-bold text-xs flex items-center justify-between transition-all"
          >
            <span>Tiếp tục Lộ trình học tập</span>
            <span>→</span>
          </Link>
        </div>

        <div className="pt-2 text-center">
          <Link
            to="/hoc-tap/luyen-tap"
            className="text-xs font-semibold text-slate-500 hover:text-slate-800 dark:hover:text-slate-200 underline"
          >
            Trở về trang tổng quan Luyện tập
          </Link>
        </div>
      </div>
    </div>
  );
};
