import React, { useState, useMemo } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { getStudentDashboard } from "../api/dashboardsApi";
import { getStudentAssignments } from "../api/assignmentsApi";
import { getDetailedLearningPath } from "../api/learningPathApi";
import type { StudentDashboardDataDto } from "../types/dashboards";
import { getSubjectTheme } from "../components/student/subjectTheme";
import { StudentProgressTrack } from "../components/student/StudentProgressTrack";
import { StudentSubjectPattern } from "../components/student/StudentSubjectPattern";
import { StudentKnowledgeMap, type TopicMapNode } from "../components/student/StudentKnowledgeMap";
import { studentScopeUrl } from "../utils/studentAcademicNavigation";
import { goalGapLabel } from "../utils/academicDisplay";

const StudentRadarChart = React.lazy(() => import("../components/student/StudentRadarChart"));

export const StudentDashboardPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const selectedSubjectId = searchParams.get("subjectId") || "";
  const selectedClassId = searchParams.get("classId") || "";
  const isHistory = searchParams.get("history") === "true";
  const isAllSubjects = !selectedSubjectId;

  // View mode for competency visualization: Atlas (default) or Radar
  const [competencyViewMode, setCompetencyViewMode] = useState<"atlas" | "radar">("atlas");

  const { data, isLoading, isError, error, refetch } = useQuery<StudentDashboardDataDto>({
    queryKey: ["studentDashboard", selectedSubjectId || "all", selectedClassId, isHistory],
    queryFn: () => getStudentDashboard(selectedSubjectId || undefined, selectedClassId || undefined, isHistory),
    staleTime: 60 * 1000,
  });

  const { data: assignmentsData } = useQuery({
    queryKey: ["studentAssignmentsToday", selectedSubjectId, selectedClassId, isHistory],
    queryFn: () => getStudentAssignments({
      subjectId: selectedSubjectId || undefined,
      classId: selectedClassId || undefined,
      history: isHistory,
      pageSize: 10,
    }),
    staleTime: 60 * 1000,
  });

  const { data: detailedPathData } = useQuery({
    queryKey: ["studentLearningPathToday", selectedSubjectId, selectedClassId],
    queryFn: () => getDetailedLearningPath(selectedSubjectId, selectedClassId || undefined),
    enabled: !!selectedSubjectId,
    staleTime: 60 * 1000,
  });

  const urgentAssignment = useMemo(() => {
    const list = assignmentsData?.data || [];
    const active = list.filter((a) => a.progress?.status !== "Completed");
    active.sort((a, b) => {
      if (!a.dueAt) return 1;
      if (!b.dueAt) return -1;
      return new Date(a.dueAt).getTime() - new Date(b.dueAt).getTime();
    });
    return active[0] || null;
  }, [assignmentsData?.data]);

  const detailedPath = detailedPathData?.data;

  const activeSession = useMemo(() => {
    if (!detailedPath) return null;
    for (const phase of detailedPath.phases) {
      for (const week of phase.weeks) {
        for (const session of week.sessions) {
          if (session.status === "InProgress" || session.status === "NeedsReview" || session.status === "Grading") {
            return session;
          }
        }
      }
    }
    return null;
  }, [detailedPath]);

  const nextEligibleSession = useMemo(() => {
    if (activeSession) return null;
    if (detailedPath?.nextSession && detailedPath.nextSession.status !== "Completed") {
      return detailedPath.nextSession;
    }
    if (detailedPath) {
      for (const phase of detailedPath.phases) {
        for (const week of phase.weeks) {
          for (const session of week.sessions) {
            if (session.status === "NotStarted") {
              return session;
            }
          }
        }
      }
    }
    return null;
  }, [detailedPath, activeSession]);

  if (isLoading) {
    return (
      <div className="w-full max-w-[1800px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6 min-w-0">
        <div className="h-44 animate-pulse rounded-2xl bg-white dark:bg-[#151d2f] border border-stone-200/70 dark:border-stone-800/70" />
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          <div className="lg:col-span-8 h-80 animate-pulse rounded-2xl bg-white dark:bg-[#151d2f] border border-stone-200/70 dark:border-stone-800/70" />
          <div className="lg:col-span-4 h-80 animate-pulse rounded-2xl bg-white dark:bg-[#151d2f] border border-stone-200/70 dark:border-stone-800/70" />
        </div>
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="w-full max-w-[1800px] mx-auto px-4 sm:px-6 lg:px-8 py-12 min-w-0">
        <div className="max-w-xl mx-auto rounded-2xl bg-white dark:bg-[#151d2f] border border-stone-200 dark:border-stone-800 p-8 text-center shadow-xs">
          <h2 className="text-base font-bold text-red-700 dark:text-red-400">
            Không thể tải dữ liệu học tập
          </h2>
          <p className="mt-2 text-xs text-stone-500 dark:text-stone-400">
            {(error as Error)?.message || "Vui lòng kiểm tra kết nối và thử lại."}
          </p>
          <button
            type="button"
            onClick={() => refetch()}
            className="mt-4 inline-flex items-center rounded-lg bg-stone-900 px-4 py-2 text-xs font-semibold text-white hover:bg-stone-800 dark:bg-stone-100 dark:text-stone-900 cursor-pointer"
          >
            Tải lại trang
          </button>
        </div>
      </div>
    );
  }

  const { student, subject, goal, masteryRadar = [], progressLine = [], action } = data;
  if (data.academicContext?.message && data.academicContext.curriculums.length === 0) return (
    <div className="student-shell w-full max-w-[1800px] mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <h1 className="text-2xl font-bold">Hành trình học tập của {student.fullName}</h1>
      <div className="mt-6 rounded-2xl border border-[var(--student-border)] bg-[var(--student-surface)] p-6">
        <h2 className="text-lg font-semibold">{isHistory ? "Phạm vi lịch sử" : "Giáo trình đang học"}</h2>
        <p className="mt-3">{data.academicContext.message}</p>
        <p className="mt-2 text-sm">Bài làm, điểm và năng lực cũ vẫn được giữ nguyên. Không có biểu đồ toàn môn thay thế khi lớp chưa được gán giáo trình.</p>
        <Link className="mt-4 inline-block text-[var(--student-brand)] font-semibold" to={studentScopeUrl("/hoc-tap/bai-tap", searchParams)}>Xem bài tập của tôi →</Link>
      </div>
    </div>
  );
  const currentSubjectTheme = getSubjectTheme(subject.subjectName);

  // Radar chart data mapping
  const radarChartData = masteryRadar.map((item) => ({
    name: item.topicName,
    score: item.mastery,
    fullMark: 100,
    groupId: item.groupNodeId || item.topicNodeId,
    groupName: item.groupName || item.topicName,
    weight: item.examImportance ?? 1,
    evidenceCount: item.evidenceCount ?? 0,
  }));

  // Knowledge Map nodes mapping
  const knowledgeMapTopics: TopicMapNode[] = masteryRadar.map((item) => ({
    topicNodeId: item.topicNodeId,
    topicName: item.topicName,
    masteryPercentage: item.mastery,
    evidenceCount: item.evidenceCount ?? 0,
    groupId: item.groupNodeId,
    groupName: item.groupName,
  }));

  const goalDiff = goal.hasGoal ? goalGapLabel(goal.targetScore, goal.currentPredictedScore) : "—";
  const nextTargetTopic = action?.topicName || masteryRadar[0]?.topicName || "Hàm số & Khảo sát hàm";
  const missionExplanation =
    action?.explanation ||
    (goal.hasGoal
      ? `Dựa trên phân tích năng lực Digital Twin, đây là chặng kiến thức tiếp theo cần củng cố để đạt mốc mục tiêu ${goal.targetScore} điểm.`
      : "Dựa trên phân tích năng lực Digital Twin, đây là chặng kiến thức tiếp theo bạn nên củng cố.");

  const practiceLink = selectedSubjectId
    ? `/hoc-tap/luyen-tap?subjectId=${selectedSubjectId}`
    : `/hoc-tap/luyen-tap`;



  return (
    <div className="w-full max-w-[1800px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6 min-w-0 student-shell">
      {/* Scope Context & Student Greeting */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-stone-200/80 dark:border-stone-800/80">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-xl sm:text-2xl font-bold tracking-tight text-stone-900 dark:text-stone-100">
              Hành trình học tập của {student.fullName}
            </h1>
            <span
              className="text-[11px] font-semibold uppercase tracking-wider px-2 py-0.5 rounded whitespace-nowrap"
              style={{
                backgroundColor: currentSubjectTheme.bg,
                color: currentSubjectTheme.color,
                border: `1px solid ${currentSubjectTheme.border}`,
              }}
            >
              {isAllSubjects ? "Phạm vi: Toàn bộ môn học" : `Môn: ${subject.subjectName}`}
            </span>
          </div>
          <p className="mt-0.5 text-xs text-stone-500 dark:text-stone-400">
            Học kỳ II · Lộ trình cá nhân hóa định hướng năng lực mục tiêu
          </p>
        </div>

        {!isHistory && <div className="flex items-center gap-2 self-start sm:self-auto text-xs">
          <Link
            to={practiceLink}
            className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-lg bg-stone-900 hover:bg-stone-800 dark:bg-stone-100 dark:hover:bg-stone-200 text-white dark:text-stone-900 font-semibold transition-colors"
          >
            <span>Luyện tập ngay</span>
            <span className="text-[11px]">→</span>
          </Link>
        </div>}
      </div>

      {/* Above-the-fold Asymmetric Layout: Left 65% Today Mission, Right 35% Twin Snapshot */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 items-stretch">
        {/* Left 65% (8 cols): PRIORITIZED TODAY PLAN */}
        {isHistory ? <div className="lg:col-span-7 xl:col-span-8 rounded-2xl border border-stone-200/90 dark:border-stone-800/90 p-6 sm:p-7 bg-white dark:bg-[#151d2f]">
          <h2 className="text-xl font-bold">Phạm vi học tập đã lưu</h2>
          <p className="mt-3 text-sm text-stone-600 dark:text-stone-300">Bản đồ bên dưới chỉ gồm chủ đề của các giáo trình từng áp dụng trong lớp đã chọn. Điểm năng lực phản ánh bằng chứng tích lũy hiện có, không phải bản chụp điểm tại một thời điểm trong quá khứ.</p>
          <Link to={studentScopeUrl("/hoc-tap/bai-tap", searchParams)} className="mt-4 inline-block font-semibold text-[var(--student-brand)]">Xem bài làm trong phạm vi này →</Link>
        </div> : <div className="lg:col-span-7 xl:col-span-8 relative rounded-2xl border border-stone-200/90 dark:border-stone-800/90 p-6 sm:p-7 shadow-xs overflow-hidden flex flex-col justify-between bg-white dark:bg-[#151d2f]">
          {/* Subtle Subject Graphic Pattern */}
          <StudentSubjectPattern subjectName={subject.subjectName} opacity={0.06} />

          <div className="relative z-10 space-y-4">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <span className="w-2.5 h-2.5 rounded-full bg-emerald-500 animate-pulse" />
                <span className="text-xs font-mono font-bold uppercase tracking-wider text-stone-500 dark:text-stone-400">
                  Kế hoạch hôm nay · Phân tầng ưu tiên
                </span>
              </div>
              <span className="text-xs font-semibold text-stone-500 dark:text-stone-400">
                Môn: {subject.subjectName}
              </span>
            </div>

            {/* 4-Tier Prioritized Task Stack */}
            <div className="space-y-3">
              {/* Tier 1: Bài giáo viên bắt buộc, sắp hết hạn */}
              {urgentAssignment && (
                <div className="p-4 rounded-xl border border-rose-200 dark:border-rose-900/60 bg-rose-50/70 dark:bg-rose-950/25 flex flex-col sm:flex-row sm:items-center justify-between gap-3 shadow-xs">
                  <div className="space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider bg-rose-600 text-white">
                        1. Bài tập bắt buộc từ giáo viên
                      </span>
                      {urgentAssignment.dueAt && (
                        <span className="text-[11px] font-bold text-rose-700 dark:text-rose-400">
                          ⏱ Hạn nộp: {new Date(urgentAssignment.dueAt).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })}
                        </span>
                      )}
                    </div>
                    <h4 className="text-sm font-bold text-slate-900 dark:text-white">
                      {urgentAssignment.title}
                    </h4>
                    <p className="text-xs text-slate-500 dark:text-slate-400">
                      Tiến độ bài làm: {urgentAssignment.progress?.completedQuestionCount || 0} / {urgentAssignment.progress?.totalQuestionCount || 0} câu
                    </p>
                  </div>
                  <Link
                    to={studentScopeUrl(`/hoc-tap/bai-tap/${urgentAssignment.assignmentId}`, searchParams)}
                    className="px-4 py-2 rounded-xl bg-rose-600 hover:bg-rose-700 text-white font-bold text-xs shadow-xs transition-colors text-center whitespace-nowrap self-start sm:self-auto cursor-pointer"
                  >
                    Làm bài tập ngay →
                  </Link>
                </div>
              )}

              {/* Tier 2: Phiên AI đang thực hiện */}
              {activeSession && (
                <div className="p-4 rounded-xl border border-purple-200 dark:border-purple-900/60 bg-purple-50/70 dark:bg-purple-950/25 flex flex-col sm:flex-row sm:items-center justify-between gap-3 shadow-xs">
                  <div className="space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider bg-purple-600 text-white animate-pulse">
                        2. Phiên AI đang thực hiện
                      </span>
                      <span className="text-[11px] font-bold text-purple-700 dark:text-purple-300">
                        ⏱ {activeSession.estimatedMinutes} phút
                      </span>
                    </div>
                    <h4 className="text-sm font-bold text-slate-900 dark:text-white">
                      {activeSession.title}
                    </h4>
                    <p className="text-xs text-slate-500 dark:text-slate-400">
                      {activeSession.objective}
                    </p>
                  </div>
                  <Link
                    to={studentScopeUrl(`/hoc-tap/lo-trinh-hoc-tap?subjectId=${selectedSubjectId}`, searchParams)}
                    className="px-4 py-2 rounded-xl bg-purple-600 hover:bg-purple-700 text-white font-bold text-xs shadow-xs transition-colors text-center whitespace-nowrap self-start sm:self-auto cursor-pointer"
                  >
                    Tiếp tục học ngay →
                  </Link>
                </div>
              )}

              {/* Tier 3: Phiên AI tiếp theo được phép học */}
              {nextEligibleSession && (
                <div className="p-4 rounded-xl border border-indigo-200 dark:border-indigo-900/60 bg-indigo-50/70 dark:bg-indigo-950/25 flex flex-col sm:flex-row sm:items-center justify-between gap-3 shadow-xs">
                  <div className="space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider bg-indigo-600 text-white">
                        3. Phiên AI tiếp theo
                      </span>
                      <span className="text-[11px] font-bold text-indigo-700 dark:text-indigo-300">
                        Mục tiêu: {nextEligibleSession.targetMastery}% mastery
                      </span>
                    </div>
                    <h4 className="text-sm font-bold text-slate-900 dark:text-white">
                      {nextEligibleSession.title}
                    </h4>
                    <p className="text-xs text-slate-500 dark:text-slate-400">
                      {nextEligibleSession.objective}
                    </p>
                  </div>
                  <Link
                    to={selectedSubjectId ? `/hoc-tap/lo-trinh-hoc-tap?subjectId=${selectedSubjectId}` : `/hoc-tap/lo-trinh-hoc-tap`}
                    className="px-4 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-xs shadow-xs transition-colors text-center whitespace-nowrap self-start sm:self-auto cursor-pointer"
                  >
                    Bắt đầu phiên →
                  </Link>
                </div>
              )}

              {/* Tier 4: Hoạt động tùy chọn / Luyện tập tự do */}
              <div className="p-4 rounded-xl border border-stone-200 dark:border-stone-800 bg-stone-50/60 dark:bg-stone-900/30 flex flex-col sm:flex-row sm:items-center justify-between gap-3 shadow-2xs">
                <div className="space-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider bg-stone-600 text-white">
                      4. Hoạt động tùy chọn
                    </span>
                    <span className="text-[11px] text-stone-500">
                      Luyện tập tự do theo năng lực
                    </span>
                  </div>
                  <h4 className="text-sm font-bold text-slate-900 dark:text-white">
                    {nextTargetTopic}
                  </h4>
                  <p className="text-xs text-stone-500 dark:text-stone-400">
                    {missionExplanation}
                  </p>
                </div>
                <Link
                  to={practiceLink}
                  className="px-4 py-2 rounded-xl bg-stone-900 hover:bg-stone-800 dark:bg-stone-100 dark:hover:bg-stone-200 text-white dark:text-stone-900 font-bold text-xs shadow-xs transition-colors text-center whitespace-nowrap self-start sm:self-auto cursor-pointer"
                >
                  Luyện tự do →
                </Link>
              </div>
            </div>
          </div>
        </div>}

        {/* Right 35% (4-5 cols): TWIN SNAPSHOT */}
        <div className="lg:col-span-5 xl:col-span-4 rounded-2xl border border-stone-200/90 dark:border-stone-800/90 p-6 sm:p-7 shadow-xs flex flex-col justify-between bg-white dark:bg-[#151d2f]">
          <div>
            <div className="flex items-center justify-between pb-3 mb-4 border-b border-stone-100 dark:border-stone-800/80">
              <span className="text-xs font-mono font-bold uppercase tracking-wider text-stone-500 dark:text-stone-400">
                Lộ trình năng lực (Twin)
              </span>
              <span className="text-[11px] font-mono text-stone-400">
                {goal.hasGoal ? `${goal.remainingDays} ngày còn lại` : "Chưa đặt mục tiêu"}
              </span>
            </div>

            {/* Twin Track Current -> Target */}
            {goal.hasGoal ? <div className="space-y-4">
              <StudentProgressTrack
                currentValue={goal.currentPredictedScore}
                targetValue={goal.targetScore}
                max={10}
                currentLabel="Hiện tại"
                targetLabel="Mục tiêu"
                unit="/10"
                color={currentSubjectTheme.color}
              />

              <div className="p-3 rounded-xl bg-stone-50/70 dark:bg-stone-900/50 border border-stone-200/60 dark:border-stone-800/60 flex items-center justify-between text-xs">
                <span className="text-stone-600 dark:text-stone-400">Khoảng cách tới mục tiêu</span>
                <span className="font-mono font-bold text-stone-900 dark:text-stone-100">
                  {goalDiff}
                </span>
              </div>
            </div> : (
              <div className="rounded-xl border border-indigo-200 bg-indigo-50 p-4 text-xs text-indigo-900 dark:border-indigo-800 dark:bg-indigo-950/40 dark:text-indigo-200">
                Mục tiêu điểm không còn được gán sẵn. Hãy tự đặt mục tiêu cho từng môn trong Hồ sơ năng lực.
              </div>
            )}
          </div>

          <div className="pt-4 mt-4 border-t border-stone-100 dark:border-stone-800/80 flex items-center justify-between text-xs">
            <span className="text-stone-500 dark:text-stone-400">
              {goal.hasGoal ? <>Mục tiêu: <strong className="text-stone-800 dark:text-stone-200">{goal.targetScore} điểm</strong></> : "Mục tiêu do học sinh tự đặt"}
            </span>
            <Link
              to={studentScopeUrl("/hoc-tap/ho-so-nang-luc", searchParams)}
              className="text-xs font-semibold text-stone-700 dark:text-stone-300 hover:text-stone-900 dark:hover:text-white"
            >
              Xem hồ sơ chi tiết →
            </Link>
          </div>
        </div>
      </div>

      {/* Coordinate Checkpoints Bar (Clean flat metrics row satisfying test regex) */}
      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-5">
        <div className="py-2.5 px-3 border-l-2 border-stone-300 dark:border-stone-700">
          <span className="text-[11px] font-medium text-stone-500 dark:text-stone-400 uppercase tracking-wider block">
            Điểm dự đoán
          </span>
          <span className="text-xl font-bold font-mono text-stone-900 dark:text-stone-100">
            {goal.currentPredictedScore.toFixed(1)} / 10
          </span>
        </div>
        <div className="py-2.5 px-3 border-l-2 border-stone-300 dark:border-stone-700">
          <span className="text-[11px] font-medium text-stone-500 dark:text-stone-400 uppercase tracking-wider block">
            Mục tiêu kỳ thi
          </span>
          <span className="text-xl font-bold font-mono text-stone-900 dark:text-stone-100">
            {goal.hasGoal ? `${goal.targetScore.toFixed(1)} / 10` : "Chưa đặt"}
          </span>
        </div>
        <div className="py-2.5 px-3 border-l-2 border-stone-300 dark:border-stone-700">
          <span className="text-[11px] font-medium text-stone-500 dark:text-stone-400 uppercase tracking-wider block">
            Khoảng cách cần vượt
          </span>
          <span className="text-xl font-bold font-mono text-emerald-600 dark:text-emerald-400">
            {goalDiff}
          </span>
        </div>
        <div className="py-2.5 px-3 border-l-2 border-stone-300 dark:border-stone-700">
          <span className="text-[11px] font-medium text-stone-500 dark:text-stone-400 uppercase tracking-wider block">
            Chỉ số nguy cơ
          </span>
          <span className="text-xl font-bold font-mono text-stone-900 dark:text-stone-100">
            {goal.hasGoal ? `${goal.riskScore.toFixed(0)}%` : "—"}
          </span>
        </div>
      </div>

      {/* Main Section: Knowledge Map Visual Anchor + Agenda Route */}
      <div className="grid grid-cols-1 xl:grid-cols-2 gap-6 items-start">
        {/* Left Column: BẢN ĐỒ NĂNG LỰC (KNOWLEDGE MAP) with Radar toggle option */}
        <div className="rounded-2xl border border-stone-200/90 dark:border-stone-800/90 p-5 sm:p-6 bg-white dark:bg-[#151d2f] shadow-xs">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 mb-2 border-b border-stone-100 dark:border-stone-800/80">
            <div>
              <h3 className="text-base font-bold text-stone-900 dark:text-stone-100">
                {isAllSubjects ? "Radar Năng Lực Theo Môn Học" : "Radar Năng Lực Theo Chuyên Đề"}
              </h3>
              <p className="text-xs text-stone-500 dark:text-stone-400 mt-0.5">
                Bản đồ định vị năng lực & các mốc kiến thức đã chinh phục
              </p>
            </div>

            {/* View Mode Switcher */}
            <div className="inline-flex rounded-lg border border-stone-200 dark:border-stone-800 p-0.5 bg-stone-50 dark:bg-stone-900 self-start sm:self-auto text-xs">
              <button
                type="button"
                onClick={() => setCompetencyViewMode("atlas")}
                className={`px-3 py-1 rounded-md font-semibold transition-colors cursor-pointer ${
                  competencyViewMode === "atlas"
                    ? "bg-white dark:bg-stone-800 text-stone-900 dark:text-stone-100 shadow-2xs"
                    : "text-stone-500 hover:text-stone-800 dark:text-stone-400"
                }`}
              >
                Bản đồ Atlas
              </button>
              <button
                type="button"
                onClick={() => setCompetencyViewMode("radar")}
                className={`px-3 py-1 rounded-md font-semibold transition-colors cursor-pointer ${
                  competencyViewMode === "radar"
                    ? "bg-white dark:bg-stone-800 text-stone-900 dark:text-stone-100 shadow-2xs"
                    : "text-stone-500 hover:text-stone-800 dark:text-stone-400"
                }`}
              >
                Biểu đồ Radar
              </button>
            </div>
          </div>

          {competencyViewMode === "atlas" ? (
            <StudentKnowledgeMap
              topics={knowledgeMapTopics}
              subjectName={subject.subjectName}
            />
          ) : (
            <React.Suspense
              fallback={
                <div className="h-72 w-full flex items-center justify-center text-sm text-stone-400">
                  Đang tải biểu đồ radar...
                </div>
              }
            >
              <StudentRadarChart
                data={radarChartData}
                themeColor={currentSubjectTheme.color}
              />
            </React.Suspense>
          )}
        </div>

        {/* Right Column: TIẾN ĐỘ CHẶNG HỌC TẬP (STUDY PROGRESSION ROUTE) */}
        <div className="rounded-2xl border border-stone-200/90 dark:border-stone-800/90 p-5 sm:p-6 bg-white dark:bg-[#151d2f] shadow-xs">
          <div className="flex items-center justify-between pb-3 mb-4 border-b border-stone-100 dark:border-stone-800/80">
            <div>
              <h3 className="text-base font-bold text-stone-900 dark:text-stone-100">
                Tiến độ các chặng chuyên đề
              </h3>
              <p className="text-xs text-stone-500 dark:text-stone-400 mt-0.5">
                Độ thành thạo theo từng mắt xích trong chương trình
              </p>
            </div>
            <Link
              to={studentScopeUrl("/hoc-tap/bai-tap", searchParams)}
              className="text-xs font-semibold text-stone-600 dark:text-stone-400 hover:text-stone-900 dark:hover:text-stone-100"
            >
              Xem tất cả bài tập →
            </Link>
          </div>

          <div className="space-y-4">
            {masteryRadar.slice(0, 5).map((topic, idx) => {
              const mastery = topic.mastery;
              const isDone = mastery >= 75;

              return (
                <div key={topic.topicNodeId} className="space-y-1.5">
                  <div className="flex items-center justify-between text-xs">
                    <div className="flex items-center gap-2">
                      <span className="text-[11px] font-mono text-stone-400">
                        {String(idx + 1).padStart(2, "0")}
                      </span>
                      <span className="font-semibold text-stone-800 dark:text-stone-200">
                        {topic.topicName}
                      </span>
                    </div>
                    <div className="flex items-center gap-2 font-mono text-xs">
                      <span className={`font-bold ${isDone ? "text-emerald-600 dark:text-emerald-400" : "text-stone-800 dark:text-stone-200"}`}>
                        {mastery.toFixed(0)}%
                      </span>
                    </div>
                  </div>

                  {/* Slim Route Line */}
                  <div className="w-full bg-stone-100 dark:bg-stone-800 rounded-full h-1.5 overflow-hidden">
                    <div
                      className={`h-full rounded-full transition-all duration-300 ${
                        isDone ? "bg-emerald-500" : "bg-indigo-600"
                      }`}
                      style={{ width: `${Math.min(100, Math.max(0, mastery))}%` }}
                    />
                  </div>
                </div>
              );
            })}
          </div>

          {/* Recent Progress Record Strip */}
          {progressLine.length > 0 && (
            <div className="mt-6 pt-4 border-t border-stone-100 dark:border-stone-800/80">
              <span className="text-[11px] font-mono uppercase tracking-wider text-stone-400 block mb-2">
                Điểm ghi nhận gần nhất
              </span>
              <div className="space-y-2 text-xs">
                {progressLine.slice(-2).map((point, idx) => (
                  <div
                    key={idx}
                    className="flex items-center justify-between text-stone-600 dark:text-stone-400 py-1"
                  >
                    <span>
                      Độ thành thạo chung: <strong className="text-stone-800 dark:text-stone-200">{point.overallSubjectMastery.toFixed(0)}%</strong>
                    </span>
                    <span className="font-mono text-[11px] text-stone-400 shrink-0">
                      {new Date(point.recordedAt).toLocaleDateString("vi-VN", {
                        day: "2-digit",
                        month: "2-digit",
                      })}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
