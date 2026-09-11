import { useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Radar,
  RadarChart,
  PolarGrid,
  PolarAngleAxis,
  PolarRadiusAxis,
  ResponsiveContainer,
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
} from "recharts";
import { getStudentDashboard } from "../api/dashboardsApi";
import { acceptRecommendation, dismissRecommendation } from "../api/learningFeedbackApi";
import type { StudentDashboardDataDto } from "../types/dashboards";

export const StudentDashboardPage = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const selectedSubjectId = searchParams.get("subjectId") || undefined;
  const queryClient = useQueryClient();
  const [actionSuccessMessage, setActionSuccessMessage] = useState<string | null>(null);

  const {
    data: dashboard,
    isLoading,
    isError,
    error,
    refetch,
  } = useQuery<StudentDashboardDataDto>({
    queryKey: ["studentDashboard", selectedSubjectId],
    queryFn: () => getStudentDashboard(selectedSubjectId),
  });

  const acceptMutation = useMutation({
    mutationFn: (recommendationId: string) => acceptRecommendation(recommendationId),
    onSuccess: () => {
      setActionSuccessMessage("Đã chấp nhận đề xuất học tập!");
      queryClient.invalidateQueries({ queryKey: ["studentDashboard"] });
      setTimeout(() => setActionSuccessMessage(null), 4000);
    },
  });

  const dismissMutation = useMutation({
    mutationFn: (recommendationId: string) => dismissRecommendation(recommendationId),
    onSuccess: () => {
      setActionSuccessMessage("Đã bỏ qua đề xuất học tập.");
      queryClient.invalidateQueries({ queryKey: ["studentDashboard"] });
      setTimeout(() => setActionSuccessMessage(null), 4000);
    },
  });

  if (isLoading) {
    return (
      <div className="min-h-screen bg-slate-50 p-6">
        <div className="mx-auto max-w-6xl space-y-6">
          <div className="h-24 animate-pulse rounded-xl bg-white shadow-sm ring-1 ring-slate-200" />
          <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
            <div className="h-48 animate-pulse rounded-xl bg-white shadow-sm ring-1 ring-slate-200" />
            <div className="h-48 animate-pulse rounded-xl bg-white shadow-sm ring-1 ring-slate-200" />
            <div className="h-48 animate-pulse rounded-xl bg-white shadow-sm ring-1 ring-slate-200" />
          </div>
          <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
            <div className="h-80 animate-pulse rounded-xl bg-white shadow-sm ring-1 ring-slate-200" />
            <div className="h-80 animate-pulse rounded-xl bg-white shadow-sm ring-1 ring-slate-200" />
          </div>
        </div>
      </div>
    );
  }

  if (isError || !dashboard) {
    return (
      <div className="min-h-screen bg-slate-50 p-6">
        <div className="mx-auto max-w-4xl rounded-xl bg-white p-8 shadow-sm ring-1 ring-slate-200">
          <h2 className="text-xl font-bold text-red-600">Không thể tải Dashboard học sinh</h2>
          <p className="mt-2 text-slate-600">
            {(error as Error)?.message || "Vui lòng kiểm tra lại kết nối hoặc phân quyền môn học."}
          </p>
          <div className="mt-4 flex gap-3">
            <button
              onClick={() => refetch()}
              className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500"
            >
              Thử lại
            </button>
            <Link
              to="/"
              className="rounded-md bg-white px-4 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
            >
              Về trang chủ
            </Link>
          </div>
        </div>
      </div>
    );
  }

  const { student, subject, goal, masteryRadar, progressLine, action } = dashboard;

  const getRiskBadge = (riskScore: number) => {
    if (riskScore >= 70) {
      return (
        <span className="inline-flex items-center rounded-full bg-red-100 px-3 py-0.5 text-xs font-semibold text-red-800 ring-1 ring-inset ring-red-600/20">
          Nguy cơ cao ({riskScore.toFixed(1)}%)
        </span>
      );
    }
    if (riskScore >= 30) {
      return (
        <span className="inline-flex items-center rounded-full bg-amber-100 px-3 py-0.5 text-xs font-semibold text-amber-800 ring-1 ring-inset ring-amber-600/20">
          Trung bình ({riskScore.toFixed(1)}%)
        </span>
      );
    }
    return (
      <span className="inline-flex items-center rounded-full bg-emerald-100 px-3 py-0.5 text-xs font-semibold text-emerald-800 ring-1 ring-inset ring-emerald-600/20">
        An toàn ({riskScore.toFixed(1)}%)
      </span>
    );
  };

  const radarChartData = masteryRadar.map((r) => ({
    topic: r.topicName.length > 16 ? `${r.topicName.slice(0, 14)}...` : r.topicName,
    fullTopic: r.topicName,
    mastery: r.mastery,
  }));

  const progressChartData = progressLine.map((p) => ({
    time: new Date(p.recordedAt).toLocaleDateString("vi-VN", {
      month: "numeric",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    }),
    mastery: p.overallSubjectMastery,
  }));

  return (
    <div className="min-h-screen bg-slate-50 p-6">
      <div className="mx-auto max-w-6xl space-y-6">
        {/* Header Banner */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200 gap-4">
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-2xl font-bold text-slate-900">
                Tổng quan học tập · {student.fullName}
              </h1>
              <span className="rounded-md bg-indigo-50 px-2.5 py-1 text-xs font-medium text-indigo-700 ring-1 ring-inset ring-indigo-700/10">
                Môn: {subject.subjectName}
              </span>
            </div>
            <p className="mt-1 text-sm text-slate-500">
              Định vị năng lực thích ứng và lộ trình bứt phá điểm thi mục tiêu.
            </p>
          </div>
          <div className="flex items-center gap-3">
            <Link
              to="/hoc-tap/ho-so-nang-luc"
              className="inline-flex items-center rounded-md bg-white px-3.5 py-2 text-sm font-semibold text-indigo-600 shadow-sm ring-1 ring-inset ring-indigo-200 hover:bg-indigo-50"
            >
              Hồ sơ năng lực (Twin)
            </Link>
            <Link
              to={`/hoc-tap/luyen-tap?subjectId=${subject.subjectId}`}
              className="inline-flex items-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500"
            >
              Luyện tập ngay
            </Link>
          </div>
        </div>

        {actionSuccessMessage && (
          <div className="rounded-lg bg-emerald-50 p-4 text-sm font-medium text-emerald-800 ring-1 ring-emerald-200">
            {actionSuccessMessage}
          </div>
        )}

        {/* Goal & Predicted Score Cards */}
        <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-4">
          <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <p className="text-sm font-medium text-slate-500">Điểm mục tiêu</p>
            <p className="mt-2 text-3xl font-extrabold text-indigo-600">
              {goal.targetScore.toFixed(1)}
              <span className="text-base font-normal text-slate-400"> / 10</span>
            </p>
            <p className="mt-2 text-xs text-slate-500">Kỳ thi đánh giá năng lực</p>
          </div>

          <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <p className="text-sm font-medium text-slate-500">Điểm dự đoán hiện tại</p>
            <p className="mt-2 text-3xl font-extrabold text-slate-900">
              {goal.currentPredictedScore.toFixed(1)}
              <span className="text-base font-normal text-slate-400"> / 10</span>
            </p>
            <p className="mt-2 text-xs text-slate-500">
              Độ lệch mục tiêu:{" "}
              <span
                className={
                  goal.targetScore - goal.currentPredictedScore > 0
                    ? "font-semibold text-amber-600"
                    : "font-semibold text-emerald-600"
                }
              >
                {(goal.targetScore - goal.currentPredictedScore).toFixed(1)} điểm
              </span>
            </p>
          </div>

          <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <p className="text-sm font-medium text-slate-500">Thời gian còn lại</p>
            <p className="mt-2 text-3xl font-extrabold text-slate-900">
              {goal.remainingDays}{" "}
              <span className="text-base font-normal text-slate-400">ngày</span>
            </p>
            <p className="mt-2 text-xs text-slate-500">Thời gian vàng để bứt phá kiến thức</p>
          </div>

          <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <p className="text-sm font-medium text-slate-500">Đánh giá rủi ro mục tiêu</p>
            <div className="mt-3">{getRiskBadge(goal.riskScore)}</div>
            <p className="mt-3 text-xs text-slate-500">
              Dựa trên tốc độ học tập và khoảng cách năng lực.
            </p>
          </div>
        </div>

        {/* Charts: Radar + Progress Line */}
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
          {/* Radar Chart */}
          <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <div className="flex items-center justify-between border-b border-slate-100 pb-4">
              <div>
                <h3 className="text-base font-bold text-slate-900">
                  Radar Năng Lực Theo Chuyên Đề
                </h3>
                <p className="text-xs text-slate-500">
                  Tỷ lệ thành thạo từng chủ đề trong môn học (%)
                </p>
              </div>
            </div>
            <div className="mt-4 h-72 w-full">
              {radarChartData.length > 0 ? (
                <ResponsiveContainer width="100%" height="100%">
                  <RadarChart cx="50%" cy="50%" outerRadius="80%" data={radarChartData}>
                    <PolarGrid stroke="#e2e8f0" />
                    <PolarAngleAxis dataKey="topic" tick={{ fill: "#475569", fontSize: 12 }} />
                    <PolarRadiusAxis angle={30} domain={[0, 100]} stroke="#cbd5e1" />
                    <Radar
                      name="Năng lực (%)"
                      dataKey="mastery"
                      stroke="#4f46e5"
                      fill="#6366f1"
                      fillOpacity={0.5}
                    />
                    <Tooltip
                      formatter={(val: number) => [`${val}%`, "Thành thạo"]}
                      labelFormatter={(label, payload) =>
                        payload?.[0]?.payload?.fullTopic || label
                      }
                    />
                  </RadarChart>
                </ResponsiveContainer>
              ) : (
                <div className="flex h-full items-center justify-center text-sm text-slate-400">
                  Chưa có dữ liệu chuyên đề
                </div>
              )}
            </div>
          </div>

          {/* Progress Line */}
          <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <div className="flex items-center justify-between border-b border-slate-100 pb-4">
              <div>
                <h3 className="text-base font-bold text-slate-900">
                  Tiến Trình Phát Triển Năng Lực
                </h3>
                <p className="text-xs text-slate-500">
                  Điểm thành thạo tổng thể qua các mốc học tập (%)
                </p>
              </div>
            </div>
            <div className="mt-4 h-72 w-full">
              {progressChartData.length > 0 ? (
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={progressChartData} margin={{ top: 10, right: 20, left: 0, bottom: 20 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                    <XAxis dataKey="time" tick={{ fill: "#64748b", fontSize: 11 }} />
                    <YAxis domain={[0, 100]} tick={{ fill: "#64748b", fontSize: 11 }} />
                    <Tooltip formatter={(val: number) => [`${val}%`, "Thành thạo"]} />
                    <Line
                      type="monotone"
                      dataKey="mastery"
                      stroke="#4f46e5"
                      strokeWidth={3}
                      dot={{ fill: "#4f46e5", r: 4 }}
                      activeDot={{ r: 6 }}
                    />
                  </LineChart>
                </ResponsiveContainer>
              ) : (
                <div className="flex h-full items-center justify-center text-sm text-slate-400">
                  Chưa có lịch sử làm bài để vẽ đường tiến trình
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Recommended Next Action Card */}
        {action ? (
          <div className="rounded-xl bg-gradient-to-r from-indigo-900 via-indigo-800 to-indigo-950 p-6 text-white shadow-md">
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-6">
              <div className="space-y-2">
                <div className="flex items-center gap-2">
                  <span className="rounded-full bg-indigo-500/30 px-3 py-1 text-xs font-semibold uppercase tracking-wider text-indigo-200 ring-1 ring-inset ring-indigo-400/30">
                    Chiến lược: {action.strategy}
                  </span>
                  <span className="rounded-full bg-emerald-500/30 px-3 py-1 text-xs font-semibold text-emerald-200 ring-1 ring-inset ring-emerald-400/30">
                    Cơ hội tăng điểm: +{action.opportunityScore.toFixed(1)}%
                  </span>
                </div>
                <h3 className="text-xl font-bold text-white">
                  Đề xuất ưu tiên: {action.topicName}
                </h3>
                <p className="max-w-2xl text-sm text-indigo-100/90">{action.explanation}</p>
              </div>

              <div className="flex flex-wrap items-center gap-3">
                <button
                  onClick={() =>
                    navigate(
                      `/hoc-tap/luyen-tap?subjectId=${subject.subjectId}&topicNodeId=${action.topicNodeId}${
                        action.questionId ? `&questionId=${action.questionId}` : ""
                      }`
                    )
                  }
                  className="rounded-lg bg-emerald-500 px-5 py-2.5 text-sm font-bold text-white shadow-sm hover:bg-emerald-400 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-500"
                >
                  Bắt đầu học ngay
                </button>

                {action.recommendationId && (
                  <>
                    <button
                      onClick={() => acceptMutation.mutate(action.recommendationId!)}
                      disabled={acceptMutation.isPending}
                      className="rounded-lg bg-white/10 px-4 py-2.5 text-sm font-semibold text-white hover:bg-white/20 disabled:opacity-50"
                    >
                      {acceptMutation.isPending ? "Đang lưu..." : "Chấp nhận"}
                    </button>
                    <button
                      onClick={() => dismissMutation.mutate(action.recommendationId!)}
                      disabled={dismissMutation.isPending}
                      className="rounded-lg bg-white/5 px-4 py-2.5 text-sm font-semibold text-indigo-200 hover:bg-white/10 disabled:opacity-50"
                    >
                      {dismissMutation.isPending ? "Đang lưu..." : "Bỏ qua"}
                    </button>
                  </>
                )}
              </div>
            </div>
          </div>
        ) : (
          <div className="rounded-xl bg-white p-6 text-center shadow-sm ring-1 ring-slate-200">
            <p className="text-base font-medium text-slate-700">
              Bạn đang duy trì lộ trình học tập rất tốt!
            </p>
            <p className="mt-1 text-sm text-slate-500">
              Hãy tiếp tục luyện tập các chuyên đề nâng cao để đạt điểm số tối đa.
            </p>
            <Link
              to={`/hoc-tap/luyen-tap?subjectId=${subject.subjectId}`}
              className="mt-4 inline-flex items-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500"
            >
              Luyện tập chuyên đề
            </Link>
          </div>
        )}
      </div>
    </div>
  );
};
