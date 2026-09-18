import React, { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Radar,
  RadarChart,
  PolarGrid,
  PolarAngleAxis,
  PolarRadiusAxis,
  ResponsiveContainer,
  AreaChart,
  Area,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
} from "recharts";
import { getStudentDashboard } from "../api/dashboardsApi";
import { acceptRecommendation, dismissRecommendation } from "../api/learningFeedbackApi";
import type { StudentDashboardDataDto } from "../types/dashboards";
import { SubjectRequiredState } from "../components/SubjectRequiredState";
import { useThemeMode } from "../utils/themeMode";

export const StudentDashboardPage: React.FC = () => {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const selectedSubjectId = searchParams.get("subjectId") || "";
  const queryClient = useQueryClient();
  const [actionSuccessMessage, setActionSuccessMessage] = useState<string | null>(null);
  const [isTableViewOpen, setIsTableViewOpen] = useState<boolean>(false);
  const { isDark } = useThemeMode();

  const {
    data: dashboard,
    isLoading,
    isError,
    error,
    refetch,
  } = useQuery<StudentDashboardDataDto>({
    queryKey: ["studentDashboard", selectedSubjectId],
    queryFn: () => getStudentDashboard(selectedSubjectId),
    enabled: !!selectedSubjectId,
  });

  const acceptMutation = useMutation({
    mutationFn: (recommendationId: string) => acceptRecommendation(recommendationId),
    onSuccess: () => {
      setActionSuccessMessage("Đã chấp nhận đề xuất học tập vào lộ trình cá nhân!");
      queryClient.invalidateQueries({ queryKey: ["studentDashboard"] });
      setTimeout(() => setActionSuccessMessage(null), 4000);
    },
  });

  const dismissMutation = useMutation({
    mutationFn: (recommendationId: string) => dismissRecommendation(recommendationId),
    onSuccess: () => {
      setActionSuccessMessage("Đã bỏ qua đề xuất học tập này.");
      queryClient.invalidateQueries({ queryKey: ["studentDashboard"] });
      setTimeout(() => setActionSuccessMessage(null), 4000);
    },
  });

  if (!selectedSubjectId) {
    return <SubjectRequiredState onSelect={(subjectId) => setSearchParams({ subjectId })} />;
  }

  if (isLoading) {
    return (
      <div className="max-w-[1700px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6">
        <div className="h-28 animate-pulse rounded-2xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 shadow-xs" />
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="h-36 animate-pulse rounded-2xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 shadow-xs" />
          ))}
        </div>
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <div className="h-88 animate-pulse rounded-2xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 shadow-xs" />
          <div className="h-88 animate-pulse rounded-2xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 shadow-xs" />
        </div>
      </div>
    );
  }

  if (isError || !dashboard) {
    return (
      <div className="max-w-4xl mx-auto px-4 py-12">
        <div className="rounded-2xl bg-white dark:bg-[#0f172a] p-8 border border-slate-200 dark:border-slate-800 shadow-sm text-center">
          <div className="w-12 h-12 rounded-full bg-rose-100 dark:bg-rose-950/50 text-rose-600 dark:text-rose-400 flex items-center justify-center mx-auto mb-4 font-bold text-xl">
            !
          </div>
          <h2 className="text-xl font-bold text-slate-900 dark:text-white">Không thể tải Tổng quan học tập</h2>
          <p className="mt-2 text-sm text-slate-500 dark:text-slate-400 max-w-md mx-auto">
            {(error as Error)?.message || "Vui lòng kiểm tra lại kết nối mạng hoặc thử chuyển đổi môn học."}
          </p>
          <div className="mt-6 flex justify-center gap-3">
            <button
              onClick={() => refetch()}
              className="rounded-xl bg-indigo-600 px-5 py-2.5 text-sm font-bold text-white shadow-xs hover:bg-indigo-500 cursor-pointer"
            >
              Thử lại
            </button>
            <Link
              to="/"
              className="rounded-xl bg-slate-100 dark:bg-slate-800 px-5 py-2.5 text-sm font-semibold text-slate-700 dark:text-slate-200 hover:bg-slate-200 dark:hover:bg-slate-700"
            >
              Về trang chủ
            </Link>
          </div>
        </div>
      </div>
    );
  }

  const { student, subject, goal, masteryRadar, progressLine, action } = dashboard;

  const radarChartData = masteryRadar.map((r) => ({
    topic: r.topicName.length > 18 ? `${r.topicName.slice(0, 16)}...` : r.topicName,
    fullTopic: r.topicName,
    mastery: r.mastery,
    target: 80,
  }));

  const progressChartData = progressLine.map((p, index) => ({
    milestone: index === 0 ? "Bắt đầu" : index === progressLine.length - 1 ? "Hiện tại" : `Mốc ${index}`,
    time: new Date(p.recordedAt).toLocaleDateString("vi-VN", {
      month: "numeric",
      day: "numeric",
    }),
    mastery: p.overallSubjectMastery,
  }));

  // Risk Assessment Helper
  const getRiskDetails = (riskScore: number) => {
    if (riskScore >= 70) {
      return {
        label: "Nguy cơ cao",
        badgeClass: "bg-rose-50 dark:bg-rose-950/40 text-rose-700 dark:text-rose-400 border-rose-200 dark:border-rose-800",
        iconColor: "text-rose-600 dark:text-rose-400 bg-rose-100 dark:bg-rose-950/50",
        text: "Cần tăng tốc ôn tập và lấp lỗ hổng ngay",
      };
    }
    if (riskScore >= 30) {
      return {
        label: "Cần lưu ý",
        badgeClass: "bg-amber-50 dark:bg-amber-950/40 text-amber-700 dark:text-amber-400 border-amber-200 dark:border-amber-800",
        iconColor: "text-amber-600 dark:text-amber-400 bg-amber-100 dark:bg-amber-950/50",
        text: "Duy trì nhịp độ làm bài để đạt mục tiêu",
      };
    }
    return {
      label: "An toàn",
      badgeClass: "bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-400 border-emerald-200 dark:border-emerald-800",
      iconColor: "text-emerald-600 dark:text-emerald-400 bg-emerald-100 dark:bg-emerald-950/50",
      text: "Dựa trên tốc độ tiến bộ hiện tại",
    };
  };

  const riskInfo = getRiskDetails(goal.riskScore);
  const scoreDiff = goal.targetScore - goal.currentPredictedScore;

  return (
    <div className="max-w-[1700px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6">
      {/* Subject pill & Title Hero Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2 mb-2">
            <span className="inline-flex items-center gap-1.5 rounded-lg bg-indigo-50 dark:bg-indigo-950/50 px-2.5 py-1 text-xs font-bold text-indigo-700 dark:text-indigo-300 border border-indigo-200/60 dark:border-indigo-800">
              <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" />
              </svg>
              <span>Môn: {subject.subjectName}</span>
            </span>
          </div>

          <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-900 dark:text-white tracking-tight">
            Tổng quan học tập · {student.fullName}
          </h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400 font-medium">
            Định vị năng lực thích ứng và lộ trình bứt phá điểm thi mục tiêu.
          </p>
        </div>

        {/* Top Action Buttons */}
        <div className="flex items-center gap-3 self-start md:self-auto">
          <Link
            to={`/hoc-tap/ho-so-nang-luc?subjectId=${subject.subjectId}`}
            className="inline-flex items-center gap-2 rounded-xl bg-white dark:bg-slate-800 px-4 py-2.5 text-xs sm:text-sm font-bold text-slate-700 dark:text-slate-200 shadow-2xs border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700 hover:text-indigo-600 dark:hover:text-indigo-400 transition-all cursor-pointer"
          >
            <svg className="w-4 h-4 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
            </svg>
            <span>Hồ sơ năng lực</span>
          </Link>

          <Link
            to={`/hoc-tap/luyen-tap?subjectId=${subject.subjectId}`}
            className="inline-flex items-center gap-2 rounded-xl bg-indigo-600 px-5 py-2.5 text-xs sm:text-sm font-bold text-white shadow-sm shadow-indigo-600/20 hover:bg-indigo-500 transition-all cursor-pointer"
          >
            <span>⚡ Luyện tập ngay</span>
          </Link>
        </div>
      </div>

      {/* Success Banner */}
      {actionSuccessMessage && (
        <div className="rounded-2xl bg-emerald-50 dark:bg-emerald-950/40 p-4 border border-emerald-200 dark:border-emerald-800 text-sm font-semibold text-emerald-800 dark:text-emerald-300 flex items-center justify-between animate-in fade-in duration-200">
          <div className="flex items-center gap-2">
            <span className="text-base">✓</span>
            <span>{actionSuccessMessage}</span>
          </div>
          <button
            onClick={() => setActionSuccessMessage(null)}
            className="text-emerald-600 dark:text-emerald-400 hover:text-emerald-900 text-xs cursor-pointer"
          >
            Đóng
          </button>
        </div>
      )}

      {/* Bento 4 KPI Cards Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-5">
        {/* Card 1: Điểm mục tiêu */}
        <div className="rounded-2xl bg-white dark:bg-[#0f172a] p-5 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col justify-between hover:shadow-md transition-shadow">
          <div className="flex items-center justify-between mb-3">
            <span className="text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider">
              Điểm mục tiêu
            </span>
            <div className="w-8 h-8 rounded-full bg-slate-50 dark:bg-slate-800 border border-slate-200/60 dark:border-slate-700 flex items-center justify-center text-slate-600 dark:text-slate-300">
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <circle cx="12" cy="12" r="9" strokeWidth="2" />
                <circle cx="12" cy="12" r="5" strokeWidth="2" />
                <circle cx="12" cy="12" r="1" strokeWidth="2" />
              </svg>
            </div>
          </div>
          <div>
            <div className="flex items-baseline gap-1">
              <span className="text-3xl sm:text-4xl font-black tracking-tight text-slate-900 dark:text-white">
                {goal.targetScore.toFixed(1)}
              </span>
              <span className="text-sm font-semibold text-slate-400 dark:text-slate-500">/ 10</span>
            </div>
            <p className="mt-2 text-xs font-medium text-slate-400 dark:text-slate-500">
              Kỳ thi ĐGNL - ĐHQG
            </p>
          </div>
        </div>

        {/* Card 2: Điểm dự đoán hiện tại */}
        <div className="rounded-2xl bg-white dark:bg-[#0f172a] p-5 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col justify-between hover:shadow-md transition-shadow">
          <div className="flex items-center justify-between mb-3">
            <span className="text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider">
              Điểm dự đoán hiện tại
            </span>
            <div className="w-8 h-8 rounded-full bg-indigo-50 dark:bg-indigo-950/60 border border-indigo-200/60 dark:border-indigo-800 flex items-center justify-center text-indigo-600 dark:text-indigo-400">
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
              </svg>
            </div>
          </div>
          <div>
            <div className="flex items-baseline gap-1">
              <span className="text-3xl sm:text-4xl font-black tracking-tight text-slate-900 dark:text-white">
                {goal.currentPredictedScore.toFixed(1)}
              </span>
              <span className="text-sm font-semibold text-slate-400 dark:text-slate-500">/ 10</span>
            </div>
            <p className="mt-2 text-xs font-medium text-slate-500 dark:text-slate-400">
              Độ lệch:{" "}
              <span className={scoreDiff > 0 ? "font-bold text-amber-600 dark:text-amber-400" : "font-bold text-emerald-600 dark:text-emerald-400"}>
                {scoreDiff > 0 ? `+${scoreDiff.toFixed(1)} điểm cần bứt phá` : "Đã đạt mục tiêu!"}
              </span>
            </p>
          </div>
        </div>

        {/* Card 3: Thời gian còn lại */}
        <div className="rounded-2xl bg-white dark:bg-[#0f172a] p-5 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col justify-between hover:shadow-md transition-shadow">
          <div className="flex items-center justify-between mb-3">
            <span className="text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider">
              Thời gian còn lại
            </span>
            <div className="w-8 h-8 rounded-full bg-amber-50 dark:bg-amber-950/60 border border-amber-200/60 dark:border-amber-800 flex items-center justify-center text-amber-600 dark:text-amber-400">
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </div>
          </div>
          <div>
            <div className="flex items-baseline gap-1">
              <span className="text-3xl sm:text-4xl font-black tracking-tight text-slate-900 dark:text-white">
                {goal.remainingDays}
              </span>
              <span className="text-sm font-semibold text-slate-500 dark:text-slate-400">ngày</span>
            </div>
            <p className="mt-2 text-xs font-medium text-slate-400 dark:text-slate-500">
              Thời gian vàng ôn luyện trọng điểm
            </p>
          </div>
        </div>

        {/* Card 4: Đánh giá rủi ro */}
        <div className="rounded-2xl bg-white dark:bg-[#0f172a] p-5 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col justify-between hover:shadow-md transition-shadow">
          <div className="flex items-center justify-between mb-3">
            <span className="text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider">
              Đánh giá rủi ro mục tiêu
            </span>
            <div className={`w-8 h-8 rounded-full flex items-center justify-center ${riskInfo.iconColor}`}>
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </div>
          </div>
          <div>
            <div className="flex items-center gap-2">
              <span className="text-2xl sm:text-3xl font-black tracking-tight text-slate-900 dark:text-white">
                {riskInfo.label}
              </span>
            </div>
            <p className="mt-2 text-xs font-medium text-slate-400 dark:text-slate-500">
              {riskInfo.text}
            </p>
          </div>
        </div>
      </div>

      {/* Middle Section: Radar Chart + Progress Chart */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Left: Radar Năng Lực Theo Chuyên Đề */}
        <div className="rounded-2xl bg-white dark:bg-[#0f172a] p-6 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-base font-bold text-slate-900 dark:text-white">
                  Radar Năng Lực Theo Chuyên Đề
                </h3>
                <p className="text-xs text-slate-400 dark:text-slate-500 mt-0.5">
                  Mức độ thành thạo hiện tại · {subject.subjectName}
                </p>
              </div>
              <button
                type="button"
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 p-1 cursor-pointer"
                title="Tùy chọn hiển thị"
              >
                •••
              </button>
            </div>

            <div className="h-72 w-full mt-4">
              {radarChartData.length > 0 ? (
                <ResponsiveContainer width="100%" height="100%">
                  <RadarChart cx="50%" cy="50%" outerRadius="75%" data={radarChartData}>
                    <PolarGrid stroke={isDark ? "#334155" : "#e2e8f0"} strokeDasharray="3 3" />
                    <PolarAngleAxis
                      dataKey="topic"
                      tick={{ fill: isDark ? "#94a3b8" : "#64748b", fontSize: 11, fontWeight: 500 }}
                    />
                    <PolarRadiusAxis
                      angle={30}
                      domain={[0, 100]}
                      stroke={isDark ? "#475569" : "#cbd5e1"}
                      tick={{ fill: isDark ? "#64748b" : "#94a3b8", fontSize: 10 }}
                    />
                    <Radar
                      name="Mức thành thạo"
                      dataKey="mastery"
                      stroke="#6366f1"
                      strokeWidth={2}
                      fill="#6366f1"
                      fillOpacity={isDark ? 0.5 : 0.35}
                    />
                    <Tooltip
                      formatter={(val: number) => [`${val}%`, "Độ thành thạo"]}
                      labelFormatter={(label, payload) => payload?.[0]?.payload?.fullTopic || label}
                      contentStyle={{
                        backgroundColor: isDark ? "#1e293b" : "#ffffff",
                        borderColor: isDark ? "#334155" : "#e2e8f0",
                        color: isDark ? "#f8fafc" : "#0f172a",
                        borderRadius: "12px",
                        fontSize: "12px",
                      }}
                    />
                  </RadarChart>
                </ResponsiveContainer>
              ) : (
                <div className="flex h-full items-center justify-center text-sm text-slate-400 dark:text-slate-500">
                  Chưa có dữ liệu chuyên đề để tạo biểu đồ
                </div>
              )}
            </div>
          </div>

          <div className="flex items-center justify-center gap-6 pt-4 border-t border-slate-100 dark:border-slate-800 text-xs text-slate-500 dark:text-slate-400 font-medium">
            <span className="flex items-center gap-2">
              <span className="w-2.5 h-2.5 rounded-full bg-indigo-600" />
              <span>Mức thành thạo</span>
            </span>
            <span className="flex items-center gap-2">
              <span className="w-2.5 h-2.5 rounded-full bg-slate-300 dark:bg-slate-700" />
              <span>Mục tiêu 80%</span>
            </span>
          </div>
        </div>

        {/* Right: Tiến Trình Phát Triển Năng Lực */}
        <div className="rounded-2xl bg-white dark:bg-[#0f172a] p-6 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-base font-bold text-slate-900 dark:text-white">
                  Tiến Trình Phát Triển Năng Lực
                </h3>
                <p className="text-xs text-slate-400 dark:text-slate-500 mt-0.5">
                  Tổng quan qua các mốc làm bài
                </p>
              </div>
              <button
                type="button"
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 p-1 cursor-pointer"
                title="Tùy chọn hiển thị"
              >
                •••
              </button>
            </div>

            <div className="h-72 w-full mt-4">
              {progressChartData.length > 0 ? (
                <ResponsiveContainer width="100%" height="100%">
                  <AreaChart data={progressChartData} margin={{ top: 15, right: 20, left: -15, bottom: 5 }}>
                    <defs>
                      <linearGradient id="progressGradient" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor="#6366f1" stopOpacity={0.35} />
                        <stop offset="95%" stopColor="#6366f1" stopOpacity={0.0} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" stroke={isDark ? "#1e293b" : "#f1f5f9"} vertical={false} />
                    <XAxis
                      dataKey="milestone"
                      tick={{ fill: isDark ? "#94a3b8" : "#64748b", fontSize: 11 }}
                      axisLine={{ stroke: isDark ? "#334155" : "#e2e8f0" }}
                    />
                    <YAxis
                      domain={[0, 100]}
                      tick={{ fill: isDark ? "#94a3b8" : "#64748b", fontSize: 11 }}
                      axisLine={false}
                      tickLine={false}
                      ticks={[0, 25, 50, 75, 100]}
                      unit="%"
                    />
                    <Tooltip
                      formatter={(val: number) => [`${val.toFixed(1)}%`, "Năng lực tổng"]}
                      labelFormatter={(label, payload) => `${label} (${payload?.[0]?.payload?.time || ""})`}
                      contentStyle={{
                        backgroundColor: isDark ? "#1e293b" : "#ffffff",
                        borderColor: isDark ? "#334155" : "#e2e8f0",
                        color: isDark ? "#f8fafc" : "#0f172a",
                        borderRadius: "12px",
                        fontSize: "12px",
                      }}
                    />
                    <Area
                      type="monotone"
                      dataKey="mastery"
                      stroke="#6366f1"
                      strokeWidth={3}
                      fillOpacity={1}
                      fill="url(#progressGradient)"
                      dot={{ fill: "#6366f1", stroke: isDark ? "#0f172a" : "#ffffff", strokeWidth: 2, r: 4 }}
                      activeDot={{ r: 6, fill: "#6366f1" }}
                    />
                  </AreaChart>
                </ResponsiveContainer>
              ) : (
                <div className="flex h-full items-center justify-center text-sm text-slate-400 dark:text-slate-500">
                  Chưa có lịch sử làm bài để vẽ tiến trình
                </div>
              )}
            </div>
          </div>

          <div className="pt-4 border-t border-slate-100 dark:border-slate-800 flex items-center justify-center">
            <button
              type="button"
              onClick={() => setIsTableViewOpen((prev) => !prev)}
              className="text-xs font-semibold text-indigo-600 dark:text-indigo-400 hover:text-indigo-800 dark:hover:text-indigo-300 flex items-center gap-1.5 transition-colors cursor-pointer"
            >
              <span>Xem dữ liệu biểu đồ dưới dạng bảng</span>
              <span className={`transform transition-transform duration-150 ${isTableViewOpen ? "rotate-180" : ""}`}>
                ⌵
              </span>
            </button>
          </div>
        </div>
      </div>

      {/* Collapsible Chart Data Table */}
      {isTableViewOpen && (
        <div className="rounded-2xl bg-white dark:bg-[#0f172a] p-6 border border-slate-200 dark:border-slate-800 shadow-sm animate-in fade-in slide-in-from-top-2 duration-200">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
            <div>
              <h4 className="text-sm font-bold text-slate-800 dark:text-slate-200 mb-3">Năng lực theo chuyên đề</h4>
              <div className="overflow-x-auto">
                <table className="min-w-full text-left text-xs">
                  <thead className="bg-slate-50 dark:bg-slate-800/75 text-slate-500 dark:text-slate-400 font-semibold">
                    <tr>
                      <th className="py-2.5 px-3 rounded-l-lg">Chuyên đề</th>
                      <th className="py-2.5 px-3 rounded-r-lg text-right">Độ thành thạo</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                    {masteryRadar.map((item) => (
                      <tr key={item.topicNodeId} className="hover:bg-slate-50/50 dark:hover:bg-slate-800/40">
                        <td className="py-2 px-3 text-slate-700 dark:text-slate-300 font-medium">{item.topicName}</td>
                        <td className="py-2 px-3 text-right font-bold text-indigo-600 dark:text-indigo-400">
                          {item.mastery.toFixed(1)}%
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>

            <div>
              <h4 className="text-sm font-bold text-slate-800 dark:text-slate-200 mb-3">Lịch sử các mốc tiến triển</h4>
              <div className="overflow-x-auto">
                <table className="min-w-full text-left text-xs">
                  <thead className="bg-slate-50 dark:bg-slate-800/75 text-slate-500 dark:text-slate-400 font-semibold">
                    <tr>
                      <th className="py-2.5 px-3 rounded-l-lg">Thời điểm</th>
                      <th className="py-2.5 px-3 rounded-r-lg text-right">Thành thạo tổng thể</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                    {progressLine.map((item) => (
                      <tr key={item.recordedAt} className="hover:bg-slate-50/50 dark:hover:bg-slate-800/40">
                        <td className="py-2 px-3 text-slate-700 dark:text-slate-300 font-medium">
                          {new Date(item.recordedAt).toLocaleString("vi-VN")}
                        </td>
                        <td className="py-2 px-3 text-right font-bold text-emerald-600 dark:text-emerald-400">
                          {item.overallSubjectMastery.toFixed(1)}%
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* AI Strategy & Recommendation Banner (Bottom) */}
      {action ? (
        <div className="relative overflow-hidden rounded-3xl bg-gradient-to-r from-slate-950 via-[#18163b] to-slate-950 p-6 sm:p-8 text-white shadow-xl border border-indigo-500/20">
          <div className="absolute top-0 right-0 -mt-8 -mr-8 w-64 h-64 bg-indigo-600/15 rounded-full blur-3xl pointer-events-none" />

          <div className="relative z-10 flex flex-col lg:flex-row lg:items-center justify-between gap-6">
            <div className="space-y-3 max-w-3xl">
              <div className="flex flex-wrap items-center gap-2.5">
                <span className="rounded-lg bg-indigo-500/25 px-3 py-1 text-xs font-bold uppercase tracking-wider text-indigo-200 ring-1 ring-inset ring-indigo-400/30">
                  Chiến lược: {action.strategy}
                </span>

                {action.opportunityScore !== null && action.opportunityScore !== undefined && (
                  <span className="rounded-lg bg-emerald-500/25 px-3 py-1 text-xs font-bold text-emerald-300 ring-1 ring-inset ring-emerald-400/30">
                    Cơ hội tăng điểm: +{action.opportunityScore.toFixed(1)} điểm (+{(action.opportunityScore * 10).toFixed(1)}%)
                  </span>
                )}
              </div>

              <div className="flex items-center gap-2 text-indigo-300 text-xs font-semibold">
                <span>✦</span>
                <span>EduTwin AI đề xuất</span>
              </div>

              <h3 className="text-xl sm:text-2xl font-black text-white tracking-tight">
                Đề xuất ưu tiên: {action.topicName}
              </h3>

              <p className="text-sm text-indigo-100/80 leading-relaxed font-normal">
                {action.explanation}
              </p>
            </div>

            <div className="flex flex-wrap items-center gap-3 shrink-0">
              <button
                type="button"
                onClick={() => navigate(`/hoc-tap/luyen-tap?subjectId=${subject.subjectId}`)}
                className="flex items-center gap-2 rounded-xl bg-emerald-500 hover:bg-emerald-400 px-5 py-3 text-sm font-bold text-white shadow-lg shadow-emerald-500/20 transition-all cursor-pointer focus-visible:outline-emerald-500"
              >
                <span>▶ Bắt đầu học ngay</span>
              </button>

              {action.recommendationId && (
                <>
                  <button
                    type="button"
                    onClick={() => acceptMutation.mutate(action.recommendationId!)}
                    disabled={acceptMutation.isPending}
                    className="rounded-xl bg-white/10 hover:bg-white/20 border border-white/20 px-4 py-3 text-sm font-bold text-white transition-all disabled:opacity-50 cursor-pointer"
                  >
                    {acceptMutation.isPending ? "Đang lưu..." : "Chấp nhận vào lộ trình"}
                  </button>

                  <button
                    type="button"
                    onClick={() => dismissMutation.mutate(action.recommendationId!)}
                    disabled={dismissMutation.isPending}
                    className="rounded-xl px-4 py-3 text-sm font-semibold text-indigo-300 hover:text-white hover:bg-white/5 transition-all disabled:opacity-50 cursor-pointer"
                    title="Bỏ qua đề xuất này"
                  >
                    {dismissMutation.isPending ? "Đang xử lý..." : "Bỏ qua"}
                  </button>
                </>
              )}
            </div>
          </div>
        </div>
      ) : (
        <div className="rounded-2xl bg-white dark:bg-[#0f172a] p-8 border border-slate-200 dark:border-slate-800 shadow-xs text-center">
          <div className="w-12 h-12 rounded-full bg-indigo-50 dark:bg-indigo-950/60 text-indigo-600 dark:text-indigo-400 flex items-center justify-center mx-auto mb-3 text-xl font-bold">
            🌟
          </div>
          <h3 className="text-base font-bold text-slate-800 dark:text-white">
            Bạn đang duy trì lộ trình học tập rất xuất sắc!
          </h3>
          <p className="mt-1 text-xs text-slate-500 dark:text-slate-400 max-w-md mx-auto">
            Hệ thống chưa ghi nhận thêm lỗ hổng kiến thức nghiêm trọng nào. Hãy tiếp tục luyện tập các chuyên đề nâng cao.
          </p>
          <Link
            to={`/hoc-tap/luyen-tap?subjectId=${subject.subjectId}`}
            className="mt-4 inline-flex items-center gap-2 rounded-xl bg-indigo-600 px-5 py-2.5 text-xs font-bold text-white shadow-xs hover:bg-indigo-500"
          >
            <span>Luyện tập nâng cao</span>
          </Link>
        </div>
      )}
    </div>
  );
};
