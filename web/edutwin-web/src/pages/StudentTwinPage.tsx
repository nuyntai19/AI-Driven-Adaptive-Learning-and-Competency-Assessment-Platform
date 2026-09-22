import React, { useState, useMemo, useEffect } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { getStudentTwin, getStudentTwinHistory } from "../api/digitalTwinApi";
import { getStudentDashboard } from "../api/dashboardsApi";
import type { StudentTwinDataDto, TwinUpdateHistoryItemDto } from "../types/digitalTwin";
import { StudentSubjectRequiredState } from "../components/student/StudentSubjectRequiredState";
import { getSubjectTheme } from "../components/student/subjectTheme";
import { StudentSubjectPattern } from "../components/student/StudentSubjectPattern";
import { organizationApi } from "../api/organizationApi";
import { mapSafeOperationalError } from "../utils/problemDetails";

export const StudentTwinPage: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const selectedSubjectId = searchParams.get("subjectId") || "";
  const [historyPage, setHistoryPage] = useState<number>(1);
  const [isGoalEditorOpen, setIsGoalEditorOpen] = useState(false);
  const [targetScoreInput, setTargetScoreInput] = useState("8.0");
  const [remainingDaysInput, setRemainingDaysInput] = useState("90");
  const [goalError, setGoalError] = useState<string | null>(null);
  const queryClient = useQueryClient();

  const {
    data: twinData,
    isLoading: twinLoading,
    isError: twinError,
    error: twinErrorObj,
    refetch: refetchTwin,
  } = useQuery<StudentTwinDataDto>({
    queryKey: ["studentTwin", selectedSubjectId],
    queryFn: () => getStudentTwin(selectedSubjectId),
    enabled: !!selectedSubjectId,
  });

  const dashboardQuery = useQuery({
    queryKey: ["studentDashboard", selectedSubjectId],
    queryFn: () => getStudentDashboard(selectedSubjectId),
    enabled: !!selectedSubjectId,
  });

  const studentId = dashboardQuery.data?.student.studentId || "";
  const goalsQuery = useQuery({
    queryKey: ["student-subject-goals", studentId],
    queryFn: () => organizationApi.listStudentSubjectGoals(studentId),
    enabled: Boolean(studentId),
  });
  const currentGoal = goalsQuery.data?.find((item) => item.subjectId === selectedSubjectId);

  useEffect(() => {
    if (!currentGoal) return;
    setTargetScoreInput(currentGoal.targetScore.toString());
    setRemainingDaysInput(currentGoal.remainingDays.toString());
  }, [currentGoal]);

  const saveGoalMutation = useMutation({
    mutationFn: () => organizationApi.upsertStudentSubjectGoal(studentId, selectedSubjectId, {
      targetScore: Number(targetScoreInput),
      remainingDays: Number(remainingDaysInput),
      rowVersion: currentGoal?.rowVersion,
    }),
    onSuccess: async () => {
      setGoalError(null);
      setIsGoalEditorOpen(false);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["student-subject-goals", studentId] }),
        queryClient.invalidateQueries({ queryKey: ["studentDashboard", selectedSubjectId] }),
      ]);
    },
    onError: (error: unknown) => setGoalError(mapSafeOperationalError(error, "Không thể lưu mục tiêu học tập.")),
  });

  const handleSaveGoal = () => {
    const target = Number(targetScoreInput);
    const days = Number(remainingDaysInput);
    if (!Number.isFinite(target) || target < 0 || target > 10 || !Number.isInteger(days) || days < 1 || days > 3650) {
      setGoalError("Điểm mục tiêu phải từ 0 đến 10 và thời hạn phải từ 1 đến 3650 ngày.");
      return;
    }
    setGoalError(null);
    saveGoalMutation.mutate();
  };

  const {
    data: historyData,
    isLoading: historyLoading,
  } = useQuery<TwinUpdateHistoryItemDto[]>({
    queryKey: ["studentTwinHistory", selectedSubjectId],
    queryFn: () => getStudentTwinHistory(selectedSubjectId),
    enabled: !!selectedSubjectId,
  });

  // Calculate factual insight from behavioral telemetry
  const behavioralInsight = useMemo(() => {
    if (!twinData?.behavior) return "";
    const b = twinData.behavior;
    const parts: string[] = [];

    if (b.avgTimeSpentSeconds < 30) {
      parts.push("Bạn có tốc độ làm bài khá nhanh");
    } else if (b.avgTimeSpentSeconds > 120) {
      parts.push("Bạn dành nhiều thời gian suy ngẫm kỹ lưỡng từng bước giải");
    } else {
      parts.push("Tốc độ xử lý câu hỏi của bạn ở mức cân bằng");
    }

    if (b.skipRate >= 40) {
      parts.push(`tuy nhiên tỷ lệ bỏ qua câu còn cao (${b.skipRate.toFixed(0)}%) — hãy kiên nhẫn khai thác các bước trung gian`);
    } else if (b.changeAnswerRate >= 35) {
      parts.push(`tỷ lệ đổi đáp án ở mức ${b.changeAnswerRate.toFixed(0)}% — hãy rèn luyện thêm tính chắc chắn trong lập luận`);
    } else if (b.avgConfidence >= 75) {
      parts.push(`mức độ tự tin trung bình đạt ${b.avgConfidence.toFixed(0)}% với hiệu chuẩn nhận thức chuẩn xác`);
    } else {
      parts.push("hãy tiếp tục rèn luyện thêm bài tập để nâng cao độ tự tin");
    }

    return parts.join(", ") + ".";
  }, [twinData?.behavior]);

  if (!selectedSubjectId) {
    return <StudentSubjectRequiredState onSelect={(subjectId) => setSearchParams({ subjectId })} />;
  }

  if (twinLoading || dashboardQuery.isLoading) {
    return (
      <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6 min-w-0 student-shell">
        <div className="h-32 animate-pulse rounded-2xl bg-white dark:bg-[#151d2f] border border-stone-200/70 dark:border-stone-800/70" />
        <div className="h-56 animate-pulse rounded-2xl bg-white dark:bg-[#151d2f] border border-stone-200/70 dark:border-stone-800/70" />
        <div className="h-72 animate-pulse rounded-2xl bg-white dark:bg-[#151d2f] border border-stone-200/70 dark:border-stone-800/70" />
      </div>
    );
  }

  if (twinError || dashboardQuery.isError || !twinData || !dashboardQuery.data) {
    return (
      <div className="max-w-2xl mx-auto px-4 py-12 student-shell">
        <div className="rounded-2xl bg-white dark:bg-[#151d2f] p-8 border border-stone-200 dark:border-stone-800 text-center shadow-xs">
          <h2 className="text-base font-bold text-red-700 dark:text-red-400">
            Không thể tải Hồ sơ Năng lực
          </h2>
          <p className="mt-2 text-xs text-stone-500 dark:text-stone-400">
            {(twinErrorObj as Error)?.message || "Vui lòng kiểm tra lại quyền truy cập hoặc kết nối mạng."}
          </p>
          <div className="mt-4 flex justify-center gap-3">
            <button
              type="button"
              onClick={() => refetchTwin()}
              className="rounded-lg bg-stone-900 dark:bg-stone-100 px-4 py-2 text-xs font-semibold text-white dark:text-stone-900 cursor-pointer"
            >
              Thử lại
            </button>
            <Link
              to={`/hoc-tap/tong-quan?subjectId=${selectedSubjectId}`}
              className="rounded-lg bg-stone-100 dark:bg-stone-800 px-4 py-2 text-xs font-medium text-stone-700 dark:text-stone-300 hover:bg-stone-200"
            >
              Về Tổng quan
            </Link>
          </div>
        </div>
      </div>
    );
  }

  const { student, subject, goal, progressLine } = dashboardQuery.data;
  const knowledgeTwin = twinData.topics.map((topic) => ({
    ...topic,
    mastery: topic.masteryPercentage,
  }));
  const behaviorTwin = twinData.behavior;
  const subjectTheme = getSubjectTheme(subject.subjectName);

  const growthVelocity =
    progressLine.length > 1
      ? progressLine[progressLine.length - 1].overallSubjectMastery -
        progressLine[progressLine.length - 2].overallSubjectMastery
      : 0;

  const historyPageSize = 8;
  const historyTotalPages = Math.max(1, Math.ceil((historyData?.length ?? 0) / historyPageSize));
  const visibleHistory =
    historyData?.slice((historyPage - 1) * historyPageSize, historyPage * historyPageSize) ?? [];

  return (
    <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-8 min-w-0 student-shell">
      {/* 1. Header: HỒ SƠ NĂNG LỰC */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-4 border-b border-stone-200/80 dark:border-stone-800/80">
        <div className="space-y-1">
          <div className="flex items-center gap-2.5">
            <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-stone-900 dark:text-stone-100">
              Hồ sơ năng lực
            </h1>
            <span
              className="text-xs font-bold uppercase tracking-wider px-2.5 py-0.5 rounded"
              style={{
                backgroundColor: subjectTheme.bg,
                color: subjectTheme.color,
                border: `1px solid ${subjectTheme.border}`,
              }}
            >
              {subject.subjectName}
            </span>
          </div>
          <p className="text-xs sm:text-sm text-stone-500 dark:text-stone-400">
            Học sinh: <strong className="text-stone-800 dark:text-stone-200">{student.fullName}</strong> · Cập nhật gần nhất theo dữ liệu luyện tập thực tế
          </p>
        </div>

        <div className="flex items-center gap-2.5 self-start md:self-auto text-xs">
          <Link
            to={`/hoc-tap/tong-quan?subjectId=${subject.subjectId}`}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-stone-100 dark:bg-stone-800 text-stone-700 dark:text-stone-300 hover:bg-stone-200 transition-colors"
          >
            <span>← Tổng quan</span>
          </Link>
          <Link
            to={`/hoc-tap/luyen-tap?subjectId=${subject.subjectId}`}
            className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-lg bg-stone-900 hover:bg-stone-800 dark:bg-stone-100 dark:hover:bg-stone-200 text-white dark:text-stone-900 font-semibold transition-colors"
          >
            <span>Luyện tập ngay →</span>
          </Link>
          <button
            type="button"
            onClick={() => setIsGoalEditorOpen((open) => !open)}
            className="inline-flex items-center gap-1.5 rounded-lg border border-indigo-300 bg-indigo-50 px-3 py-1.5 font-semibold text-indigo-800 hover:bg-indigo-100 dark:border-indigo-700 dark:bg-indigo-950/50 dark:text-indigo-200"
          >
            {currentGoal ? "Sửa mục tiêu" : "Đặt mục tiêu"}
          </button>
        </div>
      </div>

      {isGoalEditorOpen && (
        <div className="rounded-2xl border border-indigo-200 bg-indigo-50/70 p-4 dark:border-indigo-800 dark:bg-indigo-950/30">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
            <label className="flex-1 text-xs font-semibold text-stone-700 dark:text-stone-200">
              Điểm mục tiêu (0–10)
              <input
                type="number"
                min="0"
                max="10"
                step="0.1"
                value={targetScoreInput}
                onChange={(event) => setTargetScoreInput(event.target.value)}
                className="mt-1 w-full rounded-lg border border-stone-300 bg-white px-3 py-2 text-stone-900 outline-none focus:border-indigo-500 dark:border-stone-600 dark:bg-slate-900 dark:text-white"
              />
            </label>
            <label className="flex-1 text-xs font-semibold text-stone-700 dark:text-stone-200">
              Thời hạn (ngày)
              <input
                type="number"
                min="1"
                max="3650"
                step="1"
                value={remainingDaysInput}
                onChange={(event) => setRemainingDaysInput(event.target.value)}
                className="mt-1 w-full rounded-lg border border-stone-300 bg-white px-3 py-2 text-stone-900 outline-none focus:border-indigo-500 dark:border-stone-600 dark:bg-slate-900 dark:text-white"
              />
            </label>
            <button
              type="button"
              onClick={handleSaveGoal}
              disabled={saveGoalMutation.isPending}
              className="rounded-lg bg-indigo-600 px-4 py-2 text-xs font-bold text-white hover:bg-indigo-700 disabled:opacity-50"
            >
              {saveGoalMutation.isPending ? "Đang lưu..." : "Lưu mục tiêu của tôi"}
            </button>
          </div>
          {goalError && <p className="mt-2 text-xs font-medium text-rose-700 dark:text-rose-300">{goalError}</p>}
        </div>
      )}

      {/* 2. Visual Signature: CURRENT vs TARGET (Bản đồ tiến trình lớn) */}
      <div className="relative rounded-2xl border border-stone-200/90 dark:border-stone-800/90 p-6 sm:p-8 bg-white dark:bg-[#151d2f] shadow-xs overflow-hidden">
        <StudentSubjectPattern subjectName={subject.subjectName} opacity={0.06} />

        <div className="relative z-10 space-y-6">
          <div className="flex items-center justify-between">
            <div>
              <span className="text-xs font-mono font-bold uppercase tracking-wider text-stone-400">
                Khoảng cách năng lực
              </span>
              <h2 className="text-base sm:text-lg font-bold text-stone-900 dark:text-stone-100">
                Hành trình từ vị trí hiện tại tới đích đến
              </h2>
            </div>
            <div className="text-right">
              <span className="text-xs font-mono text-emerald-600 dark:text-emerald-400 font-bold">
                +{Math.abs(growthVelocity).toFixed(1)}% / mốc
              </span>
              <p className="text-[11px] text-stone-400">Gia tốc tăng trưởng</p>
            </div>
          </div>

          {/* Large Visual: 0.8 ●━━━━━━━━━━━━━━━━━━━━○ 8.0 */}
          <div className="py-4 space-y-3">
            <div className="flex items-baseline justify-between">
              <div>
                <span className="text-[11px] font-mono uppercase tracking-wider text-stone-400 block">
                  Vị trí hiện tại (Current)
                </span>
                <span className="text-3xl sm:text-4xl font-extrabold font-mono text-stone-900 dark:text-stone-100">
                  {goal.currentPredictedScore.toFixed(1)}
                  <span className="text-sm text-stone-400 font-normal"> / 10</span>
                </span>
              </div>

              <div className="text-right">
                <span className="text-[11px] font-mono uppercase tracking-wider text-stone-400 block">
                  Mục tiêu kỳ vọng (Target)
                </span>
                <span
                  className="text-3xl sm:text-4xl font-extrabold font-mono"
                  style={{ color: subjectTheme.color }}
                >
                  {goal.hasGoal ? goal.targetScore.toFixed(1) : "—"}
                  {goal.hasGoal && <span className="text-sm text-stone-400 font-normal"> / 10</span>}
                </span>
              </div>
            </div>

            {/* Custom Large Dual-Node Route Track */}
            <div className="relative h-3 w-full bg-stone-100 dark:bg-stone-800 rounded-full overflow-visible">
              <div
                className="absolute top-0 bottom-0 rounded-full transition-all duration-700"
                style={{
                  width: `${Math.min(100, Math.max(0, (goal.currentPredictedScore / 10) * 100))}%`,
                  backgroundColor: subjectTheme.color,
                }}
              />
              {/* Current Node */}
              <div
                className="absolute top-1/2 -translate-y-1/2 w-5 h-5 rounded-full border-2 border-white dark:border-slate-900 shadow-sm transition-all duration-700"
                style={{
                  left: `calc(${Math.min(100, (goal.currentPredictedScore / 10) * 100)}% - 10px)`,
                  backgroundColor: subjectTheme.color,
                }}
              />
              {/* Target Node */}
              {goal.hasGoal && (
                <div
                  className="absolute top-1/2 -translate-y-1/2 w-4 h-4 rounded-full border-2 border-stone-400 dark:border-stone-500 bg-white dark:bg-stone-900 shadow-2xs"
                  style={{ left: `calc(${Math.min(100, (goal.targetScore / 10) * 100)}% - 8px)` }}
                />
              )}
            </div>

            <div className="flex items-center justify-between text-xs text-stone-500 dark:text-stone-400 pt-1">
              <span>{goal.hasGoal ? `Còn lại: ${goal.remainingDays} ngày` : "Bạn chưa đặt mục tiêu cho môn này"}</span>
              <span>{goal.hasGoal ? `Cần thêm: +${(goal.targetScore - goal.currentPredictedScore).toFixed(1)} điểm` : "Hãy chọn Đặt mục tiêu"}</span>
            </div>
          </div>
        </div>
      </div>

      {/* 3. Section: NĂNG LỰC CỦA BẠN (Mỗi topic là một path: ●━━━━━○ 42%) */}
      <div className="space-y-4">
        <div className="flex items-baseline justify-between border-b border-stone-200/80 dark:border-stone-800/80 pb-2">
          <div>
            <h2 className="text-base sm:text-lg font-bold text-stone-900 dark:text-stone-100">
              Năng lực của bạn theo chuyên đề
            </h2>
            <span className="text-[11px] font-mono text-stone-400">
              Tiến bộ nhận thức (Cognitive Growth)
            </span>
          </div>
          <span className="text-xs font-mono text-stone-500 dark:text-stone-400">
            {knowledgeTwin.length} chuyên đề
          </span>
        </div>

        {/* List of Topic Routes (Each topic has its own route path) */}
        <div className="space-y-3">
          {knowledgeTwin.map((topic) => {
            const mastery = topic.mastery;
            const isMastered = mastery >= 75;
            const isDeveloping = mastery >= 50 && mastery < 75;

            return (
              <div
                key={topic.topicNodeId}
                className="group p-3 sm:p-4 rounded-xl hover:bg-stone-50/70 dark:hover:bg-stone-900/40 transition-colors border border-stone-100 dark:border-stone-800/60"
              >
                <div className="flex items-center justify-between gap-3 mb-2 text-xs sm:text-sm">
                  <span className="font-semibold text-stone-900 dark:text-stone-100">
                    {topic.topicName}
                  </span>

                  <div className="flex items-center gap-3 font-mono text-xs">
                    <span className="text-stone-400 hidden sm:inline">
                      {topic.evidenceCount} bài tập
                    </span>
                    <span
                      className={`font-bold ${
                        isMastered
                          ? "text-emerald-600 dark:text-emerald-400"
                          : isDeveloping
                          ? "text-indigo-600 dark:text-indigo-400"
                          : "text-amber-600 dark:text-amber-400"
                      }`}
                    >
                      {mastery.toFixed(0)}%
                    </span>
                  </div>
                </div>

                {/* Path representation: ●━━━━━━━━━━━━○ */}
                <div className="relative h-2 w-full bg-stone-100 dark:bg-stone-800 rounded-full overflow-visible">
                  <div
                    className={`h-full rounded-full transition-all duration-500 ${
                      isMastered
                        ? "bg-emerald-500"
                        : isDeveloping
                        ? "bg-indigo-600"
                        : "bg-amber-500"
                    }`}
                    style={{ width: `${Math.min(100, Math.max(0, mastery))}%` }}
                  />
                  {/* Current Position Node */}
                  <span
                    className={`absolute top-1/2 -translate-y-1/2 w-2.5 h-2.5 rounded-full border border-white dark:border-slate-900 ${
                      isMastered ? "bg-emerald-600" : isDeveloping ? "bg-indigo-700" : "bg-amber-600"
                    }`}
                    style={{ left: `calc(${Math.min(100, Math.max(0, mastery))}% - 5px)` }}
                  />
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* 4. Section: CÁCH BẠN HỌC (Behavioral Twin with structured rows & micro bars) */}
      <div className="space-y-4">
        <div className="flex items-baseline justify-between border-b border-stone-200/80 dark:border-stone-800/80 pb-2">
          <div>
            <h2 className="text-base sm:text-lg font-bold text-stone-900 dark:text-stone-100">
              Cách bạn học & Thói quen giải quyết vấn đề
            </h2>
            <span className="text-[11px] font-mono text-stone-400">
              Hành vi học tập (Behavioral Twin)
            </span>
          </div>
          <span className="text-xs font-mono text-stone-500 dark:text-stone-400">
            {behaviorTwin.attemptCount} lượt làm bài
          </span>
        </div>

        {/* Structured Rows / Micro Bars */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="p-4 rounded-xl bg-white dark:bg-[#151d2f] border border-stone-200/80 dark:border-stone-800/80 space-y-2">
            <div className="flex items-center justify-between text-xs">
              <span className="text-stone-600 dark:text-stone-400">Thời gian làm bài trung bình</span>
              <span className="font-mono font-bold text-stone-900 dark:text-stone-100">
                {behaviorTwin.avgTimeSpentSeconds} giây / câu
              </span>
            </div>
            <div className="w-full bg-stone-100 dark:bg-stone-800 rounded-full h-1.5">
              <div
                className="bg-indigo-600 h-full rounded-full"
                style={{ width: `${Math.min(100, (behaviorTwin.avgTimeSpentSeconds / 180) * 100)}%` }}
              />
            </div>
          </div>

          <div className="p-4 rounded-xl bg-white dark:bg-[#151d2f] border border-stone-200/80 dark:border-stone-800/80 space-y-2">
            <div className="flex items-center justify-between text-xs">
              <span className="text-stone-600 dark:text-stone-400">Tỷ lệ bỏ qua câu hỏi</span>
              <span className="font-mono font-bold text-stone-900 dark:text-stone-100">
                {behaviorTwin.skipRate.toFixed(1)}%
              </span>
            </div>
            <div className="w-full bg-stone-100 dark:bg-stone-800 rounded-full h-1.5">
              <div
                className="bg-amber-500 h-full rounded-full"
                style={{ width: `${Math.min(100, behaviorTwin.skipRate)}%` }}
              />
            </div>
          </div>

          <div className="p-4 rounded-xl bg-white dark:bg-[#151d2f] border border-stone-200/80 dark:border-stone-800/80 space-y-2">
            <div className="flex items-center justify-between text-xs">
              <span className="text-stone-600 dark:text-stone-400">Tỷ lệ thay đổi đáp án</span>
              <span className="font-mono font-bold text-stone-900 dark:text-stone-100">
                {behaviorTwin.changeAnswerRate.toFixed(1)}%
              </span>
            </div>
            <div className="w-full bg-stone-100 dark:bg-stone-800 rounded-full h-1.5">
              <div
                className="bg-sky-500 h-full rounded-full"
                style={{ width: `${Math.min(100, behaviorTwin.changeAnswerRate)}%` }}
              />
            </div>
          </div>

          <div className="p-4 rounded-xl bg-white dark:bg-[#151d2f] border border-stone-200/80 dark:border-stone-800/80 space-y-2">
            <div className="flex items-center justify-between text-xs">
              <span className="text-stone-600 dark:text-stone-400">Mức độ tự tin trung bình</span>
              <span className="font-mono font-bold text-stone-900 dark:text-stone-100">
                {behaviorTwin.avgConfidence.toFixed(1)}%
              </span>
            </div>
            <div className="w-full bg-stone-100 dark:bg-stone-800 rounded-full h-1.5">
              <div
                className="bg-emerald-500 h-full rounded-full"
                style={{ width: `${Math.min(100, behaviorTwin.avgConfidence)}%` }}
              />
            </div>
          </div>
        </div>

        {/* Human-readable Data-backed Insight Banner */}
        {behavioralInsight && (
          <div className="p-4 rounded-xl bg-stone-50 dark:bg-[#141d30] border border-stone-200/80 dark:border-stone-800/80 text-xs sm:text-sm text-stone-700 dark:text-stone-300 leading-relaxed flex items-start gap-2.5">
            <span className="font-bold text-indigo-600 dark:text-indigo-400 shrink-0">●</span>
            <div>
              <span className="font-bold text-stone-900 dark:text-stone-100">Nhận định học tập: </span>
              <span>{behavioralInsight}</span>
            </div>
          </div>
        )}
      </div>

      {/* 5. Section: Nhật ký cập nhật năng lực (Twin History) */}
      <div className="space-y-3 pt-2">
        <div className="flex items-center justify-between border-b border-stone-200/80 dark:border-stone-800/80 pb-2">
          <h2 className="text-base font-bold text-stone-900 dark:text-stone-100">
            Nhật ký cập nhật năng lực
          </h2>
          <span className="text-xs font-mono text-stone-400">
            {historyData?.length ?? 0} sự kiện
          </span>
        </div>

        <div className="divide-y divide-stone-100 dark:divide-stone-800/80">
          {historyLoading ? (
            <div className="p-4 text-center text-xs text-stone-400">Đang tải lịch sử...</div>
          ) : visibleHistory.length > 0 ? (
            visibleHistory.map((item) => (
              <div
                key={item.historyId}
                className="flex flex-col sm:flex-row sm:items-center justify-between py-3 gap-2 text-xs"
              >
                <div>
                  <div className="flex items-center gap-2">
                    <span className="font-semibold text-stone-900 dark:text-stone-100">
                      {item.topicName}
                    </span>
                    <span
                      className={`px-1.5 py-0.2 rounded text-[10px] font-medium ${
                        item.eventSource === "TeacherOverride"
                          ? "bg-purple-100 dark:bg-purple-950/50 text-purple-700 dark:text-purple-300"
                          : "bg-stone-100 dark:bg-stone-800 text-stone-600 dark:text-stone-400"
                      }`}
                    >
                      {item.eventSource === "TeacherOverride" ? "Giáo viên xác nhận" : "Đánh giá bài tập"}
                    </span>
                  </div>
                  <p className="mt-0.5 text-stone-500 dark:text-stone-400">{item.explanation}</p>
                  <div className="mt-1 flex flex-wrap items-center gap-1.5 text-[11px]">
                    <span className="rounded-md border border-indigo-200 bg-indigo-50 px-1.5 py-0.5 font-semibold text-indigo-800 dark:border-indigo-800 dark:bg-indigo-950/50 dark:text-indigo-200">
                      {item.learningContext === "Assignment" ? `Bài tập: ${item.contextLabel}` : item.contextLabel}
                    </span>
                    {item.questionText && (
                      <span className="max-w-2xl truncate text-stone-600 dark:text-stone-300" title={item.questionText}>
                        Câu hỏi: {item.questionText}
                      </span>
                    )}
                  </div>
                </div>

                <div className="flex items-center gap-3 text-right shrink-0">
                  <div className="font-mono">
                    <span className="text-stone-400">{item.previousMastery.toFixed(1)}% → </span>
                    <span className="font-semibold text-stone-900 dark:text-stone-100">
                      {item.newMastery.toFixed(1)}%
                    </span>
                    <span
                      className={`ml-1 font-semibold ${
                        item.delta >= 0
                          ? "text-emerald-700 dark:text-emerald-400"
                          : "text-red-600 dark:text-red-400"
                      }`}
                    >
                      ({item.delta >= 0 ? `+${item.delta.toFixed(1)}` : item.delta.toFixed(1)}%)
                    </span>
                  </div>
                  <span className="text-stone-400 font-mono text-[11px]">
                    {new Date(item.recordedAt).toLocaleDateString("vi-VN", {
                      day: "2-digit",
                      month: "2-digit",
                      hour: "2-digit",
                      minute: "2-digit",
                    })}
                  </span>
                </div>
              </div>
            ))
          ) : (
            <div className="p-4 text-center text-xs text-stone-400">
              Chưa có sự kiện cập nhật hồ sơ nào
            </div>
          )}
        </div>

        {/* Pagination */}
        {historyTotalPages > 1 && (
          <div className="pt-2 flex items-center justify-between text-xs">
            <button
              type="button"
              onClick={() => setHistoryPage((p) => Math.max(1, p - 1))}
              disabled={historyPage <= 1}
              className="rounded-lg border border-stone-300 bg-white px-3 py-1.5 font-semibold text-stone-800 shadow-xs hover:bg-stone-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-stone-600 dark:bg-stone-800 dark:text-stone-100 dark:hover:bg-stone-700 cursor-pointer"
            >
              ← Trang trước
            </button>
            <span className="rounded-md bg-stone-100 px-2.5 py-1 font-semibold text-stone-700 dark:bg-stone-800 dark:text-stone-200">
              Trang {historyPage} / {historyTotalPages}
            </span>
            <button
              type="button"
              onClick={() => setHistoryPage((p) => Math.min(historyTotalPages, p + 1))}
              disabled={historyPage >= historyTotalPages}
              className="rounded-lg border border-stone-300 bg-white px-3 py-1.5 font-semibold text-stone-800 shadow-xs hover:bg-stone-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-stone-600 dark:bg-stone-800 dark:text-stone-100 dark:hover:bg-stone-700 cursor-pointer"
            >
              Trang sau →
            </button>
          </div>
        )}
      </div>
    </div>
  );
};
