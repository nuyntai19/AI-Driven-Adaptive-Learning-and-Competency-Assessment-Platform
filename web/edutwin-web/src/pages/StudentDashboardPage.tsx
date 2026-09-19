import React, { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  ResponsiveContainer,
  RadarChart,
  PolarGrid,
  PolarAngleAxis,
  PolarRadiusAxis,
  Radar,
  Tooltip,
} from "recharts";
import { getStudentDashboard } from "../api/dashboardsApi";
import type { StudentDashboardDataDto } from "../types/dashboards";
import { getSubjectTheme } from "../components/student/subjectTheme";
import { StudentProgressTrack } from "../components/student/StudentProgressTrack";
import { StudentSubjectPattern } from "../components/student/StudentSubjectPattern";
import { StudentKnowledgeMap, type TopicMapNode } from "../components/student/StudentKnowledgeMap";

export const StudentDashboardPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const selectedSubjectId = searchParams.get("subjectId") || "";
  const isAllSubjects = !selectedSubjectId;

  // View mode for competency visualization: Atlas (default) or Radar
  const [competencyViewMode, setCompetencyViewMode] = useState<"atlas" | "radar">("atlas");

  const { data, isLoading, isError, error, refetch } = useQuery<StudentDashboardDataDto>({
    queryKey: ["studentDashboard", selectedSubjectId || "all"],
    queryFn: () => getStudentDashboard(selectedSubjectId || undefined),
    staleTime: 60 * 1000,
  });

  if (isLoading) {
    return (
      <div className="w-full max-w-[1600px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6 min-w-0">
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
      <div className="w-full max-w-[1600px] mx-auto px-4 sm:px-6 lg:px-8 py-12 min-w-0">
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
  const currentSubjectTheme = getSubjectTheme(subject.subjectName);

  // Radar chart data mapping
  const radarChartData = masteryRadar.map((item) => ({
    name: item.topicName,
    score: item.mastery,
    fullMark: 100,
  }));

  // Knowledge Map nodes mapping
  const knowledgeMapTopics: TopicMapNode[] = masteryRadar.map((item) => ({
    topicNodeId: item.topicNodeId,
    topicName: item.topicName,
    masteryPercentage: item.mastery,
    evidenceCount: 1,
  }));

  const goalDiff = (goal.targetScore - goal.currentPredictedScore).toFixed(1);
  const nextTargetTopic = action?.topicName || masteryRadar[0]?.topicName || "Hàm số & Khảo sát hàm";
  const missionExplanation =
    action?.explanation ||
    `Dựa trên phân tích năng lực Digital Twin, đây là chặng kiến thức tiếp theo cần củng cố để đạt mốc mục tiêu ${goal.targetScore} điểm.`;

  const practiceLink = selectedSubjectId
    ? `/hoc-tap/luyen-tap?subjectId=${selectedSubjectId}`
    : `/hoc-tap/luyen-tap`;

  return (
    <div className="w-full max-w-[1600px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6 min-w-0 student-shell">
      {/* Scope Context & Student Greeting */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-stone-200/80 dark:border-stone-800/80">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-xl sm:text-2xl font-bold tracking-tight text-stone-900 dark:text-stone-100">
              Hành trình học tập của {student.fullName}
            </h1>
            <span
              className="text-[11px] font-semibold uppercase tracking-wider px-2 py-0.5 rounded"
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

        <div className="flex items-center gap-2 self-start sm:self-auto text-xs">
          <Link
            to={practiceLink}
            className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-lg bg-stone-900 hover:bg-stone-800 dark:bg-stone-100 dark:hover:bg-stone-200 text-white dark:text-stone-900 font-semibold transition-colors"
          >
            <span>Luyện tập ngay</span>
            <span className="text-[11px]">→</span>
          </Link>
        </div>
      </div>

      {/* Above-the-fold Asymmetric Layout: Left 65% Today Mission, Right 35% Twin Snapshot */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 items-stretch">
        {/* Left 65% (8 cols): TODAY MISSION */}
        <div className="lg:col-span-7 xl:col-span-8 relative rounded-2xl border border-stone-200/90 dark:border-stone-800/90 p-6 sm:p-7 shadow-xs overflow-hidden flex flex-col justify-between bg-white dark:bg-[#151d2f]">
          {/* Subtle Subject Graphic Pattern */}
          <StudentSubjectPattern subjectName={subject.subjectName} opacity={0.06} />

          <div className="relative z-10 space-y-3">
            <div className="flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
              <span className="text-xs font-mono font-bold uppercase tracking-wider text-stone-500 dark:text-stone-400">
                Nhiệm vụ trọng tâm hôm nay
              </span>
            </div>

            <h2 className="text-xl sm:text-2xl font-bold text-stone-900 dark:text-stone-100 leading-snug">
              {nextTargetTopic}
            </h2>

            <p className="text-xs sm:text-sm text-stone-600 dark:text-stone-400 max-w-xl leading-relaxed">
              {missionExplanation}
            </p>

            <div className="flex flex-wrap items-center gap-4 text-xs text-stone-500 dark:text-stone-400 pt-1">
              <span className="inline-flex items-center gap-1.5">
                <span className="text-stone-400">⏱</span>
                <span>Dự kiến 25 phút</span>
              </span>
              <span className="inline-flex items-center gap-1.5">
                <span className="text-stone-400">●</span>
                <span>3 câu hỏi bài tập rèn luyện</span>
              </span>
              <span className="inline-flex items-center gap-1.5">
                <span className="text-stone-400">⚡</span>
                <span>Phân tích tư duy thích ứng</span>
              </span>
            </div>
          </div>

          <div className="relative z-10 pt-5 mt-4 border-t border-stone-100 dark:border-stone-800/80 flex items-center justify-between">
            <span className="text-xs font-medium text-stone-500 dark:text-stone-400 hidden sm:inline">
              Môn: {subject.subjectName}
            </span>
            <Link
              to={practiceLink}
              className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-stone-900 hover:bg-stone-800 dark:bg-stone-100 dark:hover:bg-stone-200 text-white dark:text-stone-900 text-xs sm:text-sm font-bold shadow-xs transition-all cursor-pointer"
            >
              <span>Bắt đầu học ngay</span>
              <span>→</span>
            </Link>
          </div>
        </div>

        {/* Right 35% (4-5 cols): TWIN SNAPSHOT */}
        <div className="lg:col-span-5 xl:col-span-4 rounded-2xl border border-stone-200/90 dark:border-stone-800/90 p-6 sm:p-7 shadow-xs flex flex-col justify-between bg-white dark:bg-[#151d2f]">
          <div>
            <div className="flex items-center justify-between pb-3 mb-4 border-b border-stone-100 dark:border-stone-800/80">
              <span className="text-xs font-mono font-bold uppercase tracking-wider text-stone-500 dark:text-stone-400">
                Lộ trình năng lực (Twin)
              </span>
              <span className="text-[11px] font-mono text-stone-400">
                {goal.remainingDays} ngày còn lại
              </span>
            </div>

            {/* Twin Track Current -> Target */}
            <div className="space-y-4">
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
                  +{goalDiff} điểm
                </span>
              </div>
            </div>
          </div>

          <div className="pt-4 mt-4 border-t border-stone-100 dark:border-stone-800/80 flex items-center justify-between text-xs">
            <span className="text-stone-500 dark:text-stone-400">
              Mục tiêu: <strong className="text-stone-800 dark:text-stone-200">{goal.targetScore} điểm</strong>
            </span>
            <Link
              to={selectedSubjectId ? `/hoc-tap/ho-so-nang-luc?subjectId=${selectedSubjectId}` : `/hoc-tap/ho-so-nang-luc`}
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
            {goal.targetScore.toFixed(1)} / 10
          </span>
        </div>
        <div className="py-2.5 px-3 border-l-2 border-stone-300 dark:border-stone-700">
          <span className="text-[11px] font-medium text-stone-500 dark:text-stone-400 uppercase tracking-wider block">
            Khoảng cách cần vượt
          </span>
          <span className="text-xl font-bold font-mono text-emerald-600 dark:text-emerald-400">
            +{goalDiff}
          </span>
        </div>
        <div className="py-2.5 px-3 border-l-2 border-stone-300 dark:border-stone-700">
          <span className="text-[11px] font-medium text-stone-500 dark:text-stone-400 uppercase tracking-wider block">
            Chỉ số nguy cơ
          </span>
          <span className="text-xl font-bold font-mono text-stone-900 dark:text-stone-100">
            {goal.riskScore.toFixed(0)}%
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
            <div className="h-72 w-full min-w-0 mt-4 relative">
              <ResponsiveContainer width="100%" height="100%" minWidth={0}>
                <RadarChart data={radarChartData} outerRadius="68%">
                  <PolarGrid stroke="#e5e7eb" className="dark:opacity-20" />
                  <PolarAngleAxis
                    dataKey="name"
                    tick={{ fill: "#6b7280", fontSize: 11, fontWeight: 600 }}
                  />
                  <PolarRadiusAxis angle={30} domain={[0, 100]} tick={false} stroke="#9ca3af" />
                  <Radar
                    name="Năng lực"
                    dataKey="score"
                    stroke={currentSubjectTheme.color}
                    fill={currentSubjectTheme.color}
                    fillOpacity={0.25}
                  />
                  <Tooltip
                    contentStyle={{
                      borderRadius: "12px",
                      fontSize: "12px",
                      border: "1px solid #e5e7eb",
                      backgroundColor: "rgba(255, 255, 255, 0.95)",
                    }}
                  />
                </RadarChart>
              </ResponsiveContainer>
            </div>
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
              to={selectedSubjectId ? `/hoc-tap/bai-tap?subjectId=${selectedSubjectId}` : `/hoc-tap/bai-tap`}
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
