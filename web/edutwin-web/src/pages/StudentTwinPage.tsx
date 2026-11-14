import React, { useState, useMemo, useEffect } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { getStudentTwin, getStudentTwinHistory } from "../api/digitalTwinApi";
import { getStudentDashboard } from "../api/dashboardsApi";
import type { StudentTwinDataDto, TwinUpdateHistoryItemDto } from "../types/digitalTwin";
import { StudentSubjectRequiredState } from "../components/student/StudentSubjectRequiredState";
import { organizationApi } from "../api/organizationApi";
import { mapSafeOperationalError } from "../utils/problemDetails";
import { studentScopeUrl } from "../utils/studentAcademicNavigation";
import { TOPIC_STATUS_LABELS, topicMasteryStatus, type TopicMasteryStatus } from "../utils/topicMastery";

const PANEL = "rounded-2xl border border-stone-200/90 bg-white p-4 sm:p-5 dark:border-stone-800/90 dark:bg-[var(--student-surface)]";
const PRESS = "transition-[background-color,color,transform] duration-150 ease-out active:scale-[0.97] motion-reduce:transform-none";
const TOPIC_FILTERS: TopicMasteryStatus[] = ["needsWork", "developing", "mastered", "noData"];
const STATUS_TEXT: Record<TopicMasteryStatus, string> = {
  noData: "text-stone-400 dark:text-stone-500",
  needsWork: "text-amber-700 dark:text-amber-400",
  developing: "text-indigo-700 dark:text-indigo-300",
  mastered: "text-emerald-700 dark:text-emerald-400",
};
const STATUS_BAR: Record<TopicMasteryStatus, string> = {
  noData: "bg-stone-300 dark:bg-stone-600",
  needsWork: "bg-amber-500",
  developing: "bg-indigo-500",
  mastered: "bg-emerald-500",
};

export const StudentTwinPage: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const selectedSubjectId = searchParams.get("subjectId") || "";
  const selectedClassId = searchParams.get("classId") || "";
  const isHistoryView = searchParams.get("history") === "true";
  const [topicFilter, setTopicFilter] = useState<TopicMasteryStatus | "all">("all");
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
    queryKey: ["studentTwin", selectedSubjectId, selectedClassId, isHistoryView],
    queryFn: () => getStudentTwin(selectedSubjectId, selectedClassId || undefined, isHistoryView),
    enabled: !!selectedSubjectId,
  });

  const dashboardQuery = useQuery({
    queryKey: ["studentDashboard", selectedSubjectId, searchParams.get("classId") || "", searchParams.get("history") === "true"],
    queryFn: () => getStudentDashboard(selectedSubjectId, searchParams.get("classId") || undefined, searchParams.get("history") === "true"),
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
    queryKey: ["studentTwinHistory", selectedSubjectId, selectedClassId, isHistoryView],
    queryFn: () => getStudentTwinHistory(selectedSubjectId, undefined, selectedClassId || undefined, isHistoryView),
    enabled: !!selectedSubjectId,
  });

  // Calculate factual insight from behavioral telemetry
  const behavioralInsight = useMemo(() => {
    if (!twinData?.behavior) return "";
    const b = twinData.behavior;
    if (b.attemptCount === 0) return "Chưa có lượt làm bài để nhận định tốc độ và thói quen làm bài.";
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
    } else {
      parts.push("hãy tiếp tục rèn luyện thêm bài tập để củng cố kiến thức");
    }

    return parts.join(", ") + ".";
  }, [twinData?.behavior]);

  if (!selectedSubjectId) {
    return <StudentSubjectRequiredState onSelect={(subjectId) => setSearchParams({ subjectId })} />;
  }

  if (twinLoading || dashboardQuery.isLoading) {
    return (
      <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6 min-w-0 student-shell">
        <div className="h-32 animate-pulse rounded-2xl bg-white dark:bg-[var(--student-surface)] border border-stone-200/70 dark:border-stone-800/70" />
        <div className="h-56 animate-pulse rounded-2xl bg-white dark:bg-[var(--student-surface)] border border-stone-200/70 dark:border-stone-800/70" />
        <div className="h-72 animate-pulse rounded-2xl bg-white dark:bg-[var(--student-surface)] border border-stone-200/70 dark:border-stone-800/70" />
      </div>
    );
  }

  if (twinError || dashboardQuery.isError || !twinData || !dashboardQuery.data) {
    return (
      <div className="max-w-2xl mx-auto px-4 py-12 student-shell">
        <div className="rounded-2xl bg-white dark:bg-[var(--student-surface)] p-8 border border-stone-200 dark:border-stone-800 text-center shadow-xs">
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
              to={studentScopeUrl("/hoc-tap/tong-quan", searchParams)}
              className="rounded-lg bg-stone-100 dark:bg-stone-800 px-4 py-2 text-xs font-medium text-stone-700 dark:text-stone-300 hover:bg-stone-200"
            >
              Về Tổng quan
            </Link>
          </div>
        </div>
      </div>
    );
  }

  const { subject, goal, progressLine, academicContext } = dashboardQuery.data;
  const behaviorTwin = twinData.behavior;
  const topics = twinData.topics.map((topic) => ({
    ...topic,
    status: topicMasteryStatus(topic.masteryPercentage, topic.evidenceCount),
  }));
  const countByStatus = (status: TopicMasteryStatus) => topics.filter((topic) => topic.status === status).length;
  const visibleTopics = topicFilter === "all" ? topics : topics.filter((topic) => topic.status === topicFilter);
  const classInView = academicContext?.classes.find((item) => item.classId === academicContext.selectedClassId);

  const latestChange = progressLine.length > 1
    ? progressLine[progressLine.length - 1].overallSubjectMastery - progressLine[progressLine.length - 2].overallSubjectMastery
    : null;

  const historyPageSize = 8;
  const historyTotalPages = Math.max(1, Math.ceil((historyData?.length ?? 0) / historyPageSize));
  const currentHistoryPage = Math.min(historyPage, historyTotalPages);
  const visibleHistory =
    historyData?.slice((currentHistoryPage - 1) * historyPageSize, currentHistoryPage * historyPageSize) ?? [];

  const currentPercent = Math.min(100, Math.max(0, (goal.currentPredictedScore / 10) * 100));
  const targetPercent = Math.min(100, Math.max(0, (goal.targetScore / 10) * 100));
  const filterChoices: Array<{ value: TopicMasteryStatus | "all"; label: string; count: number }> = [
    { value: "all", label: "Tất cả", count: topics.length },
    ...TOPIC_FILTERS.map((value) => ({ value, label: TOPIC_STATUS_LABELS[value], count: countByStatus(value) })),
  ];

  return (
    <div className="w-full max-w-[1440px] mx-auto px-4 sm:px-6 lg:px-8 py-5 space-y-4 min-w-0 student-shell">
      <header className="flex flex-col md:flex-row md:items-end justify-between gap-3 st-rise">
        <div className="min-w-0">
          <p className="st-eyebrow">Bản sao số năng lực · Digital Twin</p>
          <h1 className="mt-1.5 text-2xl sm:text-3xl font-extrabold tracking-tight text-[var(--student-ink)]">
            Hồ sơ <span className="st-grad-text">năng lực</span>
          </h1>
          <p className="mt-1.5 text-sm text-[var(--student-ink-secondary)]">
            {subject.subjectName}{classInView ? ` · ${classInView.className}` : ""} · chỉ tính các chuyên đề trong giáo trình của lớp đang xem
          </p>
        </div>
        <div className="flex items-center gap-2 text-sm">
          <button
            type="button"
            onClick={() => setIsGoalEditorOpen((open) => !open)}
            aria-expanded={isGoalEditorOpen}
            className={`rounded-lg border border-stone-300 px-3.5 py-2 font-semibold text-stone-800 hover:bg-stone-100 cursor-pointer dark:border-stone-700 dark:text-stone-200 dark:hover:bg-stone-800 ${PRESS}`}
          >
            {currentGoal ? "Sửa mục tiêu" : "Đặt mục tiêu"}
          </button>
          <Link
            to={studentScopeUrl("/hoc-tap/luyen-tap", searchParams)}
            className={`rounded-lg bg-stone-900 px-3.5 py-2 font-semibold text-white hover:bg-stone-800 dark:bg-stone-100 dark:text-stone-900 dark:hover:bg-stone-200 ${PRESS}`}
          >
            Luyện tập ngay
          </Link>
        </div>
      </header>

      {isGoalEditorOpen && (
        <div className={PANEL}>
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

      <div className="grid grid-cols-1 xl:grid-cols-[minmax(0,2fr)_minmax(0,3fr)] gap-4 items-start">
        <div className="min-w-0 space-y-4">
          <section aria-labelledby="twin-goal-heading" className={PANEL}>
            <div className="flex items-baseline justify-between gap-3">
              <h2 id="twin-goal-heading" className="text-base font-bold text-stone-900 dark:text-stone-100">
                Tiến tới mục tiêu
              </h2>
              {latestChange !== null && Math.abs(latestChange) >= 0.05 && (
                <span
                  title="Thay đổi mức thành thạo của môn so với mốc đo trước"
                  className={`text-xs font-semibold tabular-nums ${latestChange >= 0 ? "text-emerald-700 dark:text-emerald-400" : "text-rose-700 dark:text-rose-400"}`}
                >
                  {latestChange >= 0 ? "+" : ""}{latestChange.toFixed(1)}% so với mốc trước
                </span>
              )}
            </div>

            <div className="mt-4 flex items-end justify-between gap-4">
              <div>
                <p className="text-xs text-stone-500 dark:text-stone-400">Hiện tại</p>
                <p className="text-3xl font-bold tabular-nums text-stone-900 dark:text-stone-100">
                  {goal.hasPrediction === false ? "—" : goal.currentPredictedScore.toFixed(1)}
                  {goal.hasPrediction !== false && <span className="text-sm font-medium text-stone-400"> / 10</span>}
                </p>
              </div>
              <div className="text-right">
                <p className="text-xs text-stone-500 dark:text-stone-400">Mục tiêu</p>
                <p className="text-3xl font-bold tabular-nums text-indigo-700 dark:text-indigo-300">
                  {goal.hasGoal ? goal.targetScore.toFixed(1) : "—"}
                  {goal.hasGoal && <span className="text-sm font-medium text-stone-400"> / 10</span>}
                </p>
              </div>
            </div>

            <div className="relative mt-3 h-2 rounded-full bg-stone-100 dark:bg-stone-800">
              {goal.hasPrediction !== false && (
                <div className="h-full rounded-full bg-indigo-500 transition-[width] duration-300 ease-out" style={{ width: `${currentPercent}%` }} />
              )}
              {goal.hasGoal && (
                <span
                  aria-hidden="true"
                  className="absolute top-1/2 h-4 w-0.5 -translate-x-1/2 -translate-y-1/2 rounded-full bg-stone-500 dark:bg-stone-300"
                  style={{ left: `${targetPercent}%` }}
                />
              )}
            </div>

            <div className="mt-2 flex flex-wrap items-center justify-between gap-x-4 gap-y-1 text-xs text-stone-500 dark:text-stone-400">
              <span>{goal.hasDeadline === false ? "Chưa đặt ngày đích" : goal.hasGoal ? `Còn ${goal.remainingDays} ngày` : "Bạn chưa đặt mục tiêu cho môn này"}</span>
              <span className="tabular-nums">{goal.hasPrediction === false ? "Chưa đủ dữ liệu dự báo" : goal.hasGoal ? `Cần thêm ${Math.max(0, goal.targetScore - goal.currentPredictedScore).toFixed(1)} điểm` : "Hãy chọn Đặt mục tiêu"}</span>
            </div>
            {goal.forecastMessage && <p className="mt-2 text-xs leading-relaxed text-stone-500 dark:text-stone-400">{goal.forecastMessage}</p>}
          </section>

          <section aria-labelledby="twin-behavior-heading" className={PANEL}>
            <div className="flex items-baseline justify-between gap-3">
              <h2 id="twin-behavior-heading" className="text-base font-bold text-stone-900 dark:text-stone-100">
                Thói quen làm bài
              </h2>
              <span className="text-xs tabular-nums text-stone-500 dark:text-stone-400">{behaviorTwin.attemptCount} lượt làm · cả môn</span>
            </div>
            {behaviorTwin.attemptCount > 0 && (
              <dl className="mt-3 divide-y divide-stone-100 text-sm dark:divide-stone-800/80">
                <div className="flex items-center justify-between py-2">
                  <dt className="text-stone-600 dark:text-stone-400">Thời gian trung bình</dt>
                  <dd className="font-semibold tabular-nums text-stone-900 dark:text-stone-100">{Math.round(behaviorTwin.avgTimeSpentSeconds)} giây / câu</dd>
                </div>
                <div className="flex items-center justify-between py-2">
                  <dt className="text-stone-600 dark:text-stone-400">Tỷ lệ bỏ qua câu</dt>
                  <dd className="font-semibold tabular-nums text-stone-900 dark:text-stone-100">{behaviorTwin.skipRate.toFixed(0)}%</dd>
                </div>
                <div className="flex items-center justify-between py-2">
                  <dt className="text-stone-600 dark:text-stone-400">Tỷ lệ đổi đáp án</dt>
                  <dd className="font-semibold tabular-nums text-stone-900 dark:text-stone-100">{behaviorTwin.changeAnswerRate.toFixed(0)}%</dd>
                </div>
              </dl>
            )}
            {behavioralInsight && <p className="mt-2 text-sm leading-relaxed text-stone-600 dark:text-stone-300">{behavioralInsight}</p>}
          </section>
        </div>

        <section aria-labelledby="twin-topics-heading" className={`${PANEL} min-w-0`}>
          <h2 id="twin-topics-heading" className="text-base font-bold text-stone-900 dark:text-stone-100">
            Năng lực theo chuyên đề
          </h2>
          {topics.length > 0 ? (
            <>
              <div role="group" aria-label="Lọc chuyên đề theo mức năng lực" className="mt-3 flex flex-wrap gap-1">
                {filterChoices.map((choice) => {
                  const active = topicFilter === choice.value;
                  return (
                    <button
                      key={choice.value}
                      type="button"
                      aria-pressed={active}
                      onClick={() => setTopicFilter(choice.value)}
                      className={`rounded-lg px-2.5 py-1.5 text-xs font-semibold cursor-pointer ${PRESS} ${
                        active
                          ? "bg-indigo-50 text-indigo-700 dark:bg-indigo-950/70 dark:text-indigo-300"
                          : "text-stone-600 hover:bg-stone-100 dark:text-stone-300 dark:hover:bg-stone-800"
                      }`}
                    >
                      {choice.label} <span className="tabular-nums opacity-70">{choice.count}</span>
                    </button>
                  );
                })}
              </div>

              {visibleTopics.length > 0 ? (
                <ul className="mt-2 divide-y divide-stone-100 dark:divide-stone-800/80">
                  {visibleTopics.map((topic) => (
                    <li key={topic.topicNodeId} className="py-2.5">
                      <div className="flex items-baseline justify-between gap-3 text-sm">
                        <span className="min-w-0 font-semibold text-stone-900 dark:text-stone-100">{topic.topicName}</span>
                        <span className="shrink-0 text-xs tabular-nums">
                          {topic.status !== "noData" && <span className="text-stone-500 dark:text-stone-400">{topic.evidenceCount} bài · </span>}
                          <span className={`font-bold ${STATUS_TEXT[topic.status]}`}>
                            {topic.status === "noData" ? "Chưa làm" : `${topic.masteryPercentage.toFixed(0)}%`}
                          </span>
                        </span>
                      </div>
                      <div className="mt-1.5 h-1.5 rounded-full bg-stone-100 dark:bg-stone-800">
                        <div
                          className={`h-full rounded-full transition-[width] duration-300 ease-out ${STATUS_BAR[topic.status]}`}
                          style={{ width: `${Math.min(100, Math.max(0, topic.masteryPercentage))}%` }}
                        />
                      </div>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="mt-4 text-sm text-stone-500 dark:text-stone-400">Không có chuyên đề nào ở mức này.</p>
              )}
            </>
          ) : (
            <p className="mt-3 text-sm leading-relaxed text-stone-500 dark:text-stone-400">
              {academicContext?.message || "Lớp đang xem chưa có chuyên đề nào. Giáo trình sẽ hiện ở đây khi giáo viên áp dụng cho lớp."}
            </p>
          )}
        </section>
      </div>

      <section aria-labelledby="twin-history-heading" className={PANEL}>
        <div className="flex items-baseline justify-between gap-3">
          <h2 id="twin-history-heading" className="text-base font-bold text-stone-900 dark:text-stone-100">
            Nhật ký cập nhật năng lực
          </h2>
          <span className="text-xs tabular-nums text-stone-500 dark:text-stone-400">{historyData?.length ?? 0} lần cập nhật</span>
        </div>

        <div className="mt-2 divide-y divide-stone-100 dark:divide-stone-800/80">
          {historyLoading ? (
            <p role="status" className="py-4 text-sm text-stone-500 dark:text-stone-400">Đang tải nhật ký...</p>
          ) : visibleHistory.length > 0 ? (
            visibleHistory.map((item) => (
              <div key={item.historyId} className="flex flex-col lg:flex-row lg:items-center justify-between gap-2 py-3 text-sm">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-semibold text-stone-900 dark:text-stone-100">{item.topicName}</span>
                    <span className="text-xs text-stone-500 dark:text-stone-400">
                      {item.eventSource === "TeacherOverride" ? "Giáo viên xác nhận" : item.learningContext === "Assignment" ? `Bài tập: ${item.contextLabel}` : item.contextLabel}
                    </span>
                  </div>
                  <p className="mt-0.5 text-xs leading-relaxed text-stone-500 dark:text-stone-400">{item.explanation}</p>
                </div>

                <div className="flex shrink-0 items-center gap-4 text-xs tabular-nums">
                  <span>
                    <span className="text-stone-500 dark:text-stone-400">{item.previousMastery.toFixed(1)}% → </span>
                    <span className="font-semibold text-stone-900 dark:text-stone-100">{item.newMastery.toFixed(1)}%</span>
                    <span className={`ml-1.5 font-semibold ${item.delta >= 0 ? "text-emerald-700 dark:text-emerald-400" : "text-rose-700 dark:text-rose-400"}`}>
                      {item.delta >= 0 ? `+${item.delta.toFixed(1)}` : item.delta.toFixed(1)}%
                    </span>
                  </span>
                  <span className="text-stone-500 dark:text-stone-400">
                    {new Date(item.recordedAt).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })}
                  </span>
                </div>
              </div>
            ))
          ) : (
            <p className="py-4 text-sm text-stone-500 dark:text-stone-400">
              Chưa có cập nhật nào. Năng lực sẽ được ghi lại sau mỗi bài tập được chấm.
            </p>
          )}
        </div>

        {historyTotalPages > 1 && (
          <div className="mt-2 flex items-center justify-between text-xs">
            <button
              type="button"
              onClick={() => setHistoryPage(Math.max(1, currentHistoryPage - 1))}
              disabled={currentHistoryPage <= 1}
              className={`rounded-lg border border-stone-300 px-3 py-1.5 font-semibold text-stone-800 hover:bg-stone-100 disabled:cursor-not-allowed disabled:opacity-50 cursor-pointer dark:border-stone-700 dark:text-stone-200 dark:hover:bg-stone-800 ${PRESS}`}
            >
              Trang trước
            </button>
            <span className="tabular-nums text-stone-600 dark:text-stone-300">Trang {currentHistoryPage} / {historyTotalPages}</span>
            <button
              type="button"
              onClick={() => setHistoryPage(Math.min(historyTotalPages, currentHistoryPage + 1))}
              disabled={currentHistoryPage >= historyTotalPages}
              className={`rounded-lg border border-stone-300 px-3 py-1.5 font-semibold text-stone-800 hover:bg-stone-100 disabled:cursor-not-allowed disabled:opacity-50 cursor-pointer dark:border-stone-700 dark:text-stone-200 dark:hover:bg-stone-800 ${PRESS}`}
            >
              Trang sau
            </button>
          </div>
        )}
      </section>
    </div>
  );
};
