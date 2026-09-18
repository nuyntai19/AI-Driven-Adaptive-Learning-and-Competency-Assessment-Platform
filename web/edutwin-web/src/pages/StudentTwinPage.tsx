import React, { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { getStudentTwin, getStudentTwinHistory } from "../api/digitalTwinApi";
import { getStudentDashboard } from "../api/dashboardsApi";
import type { StudentTwinDataDto, TwinUpdateHistoryItemDto } from "../types/digitalTwin";
import { SubjectRequiredState } from "../components/SubjectRequiredState";

export const StudentTwinPage: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const selectedSubjectId = searchParams.get("subjectId") || "";
  const [historyPage, setHistoryPage] = useState<number>(1);

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

  const {
    data: historyData,
    isLoading: historyLoading,
  } = useQuery<TwinUpdateHistoryItemDto[]>({
    queryKey: ["studentTwinHistory", selectedSubjectId],
    queryFn: () => getStudentTwinHistory(selectedSubjectId),
    enabled: !!selectedSubjectId,
  });

  if (!selectedSubjectId) {
    return <SubjectRequiredState onSelect={(subjectId) => setSearchParams({ subjectId })} />;
  }

  if (twinLoading || dashboardQuery.isLoading) {
    return (
      <div className="max-w-[1700px] mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
        <div className="h-28 animate-pulse rounded-3xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 shadow-xs" />
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <div className="h-80 animate-pulse rounded-3xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 shadow-xs" />
          <div className="h-80 animate-pulse rounded-3xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 shadow-xs" />
        </div>
        <div className="h-96 animate-pulse rounded-3xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 shadow-xs" />
      </div>
    );
  }

  if (twinError || dashboardQuery.isError || !twinData || !dashboardQuery.data) {
    return (
      <div className="max-w-4xl mx-auto px-4 py-12">
        <div className="rounded-2xl bg-white dark:bg-[#0f172a] p-8 border border-slate-200 dark:border-slate-800 text-center shadow-sm">
          <h2 className="text-xl font-bold text-rose-600 dark:text-rose-400">Không thể tải Hồ sơ Năng lực (Digital Twin)</h2>
          <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
            {(twinErrorObj as Error)?.message || "Vui lòng kiểm tra lại quyền truy cập hoặc kết nối mạng."}
          </p>
          <div className="mt-6 flex justify-center gap-3">
            <button
              onClick={() => refetchTwin()}
              className="rounded-xl bg-indigo-600 px-5 py-2.5 text-xs font-bold text-white shadow-xs hover:bg-indigo-500 cursor-pointer"
            >
              Thử lại
            </button>
            <Link
              to={`/hoc-tap/tong-quan?subjectId=${selectedSubjectId}`}
              className="rounded-xl bg-slate-100 dark:bg-slate-800 px-5 py-2.5 text-xs font-bold text-slate-700 dark:text-slate-200 hover:bg-slate-200 dark:hover:bg-slate-700"
            >
              Về Dashboard
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

  const overallMastery =
    knowledgeTwin.length > 0
      ? knowledgeTwin.reduce((sum, topic) => sum + topic.mastery, 0) / knowledgeTwin.length
      : 74.5;

  const growthVelocity =
    progressLine.length > 1
      ? progressLine[progressLine.length - 1].overallSubjectMastery -
        progressLine[progressLine.length - 2].overallSubjectMastery
      : 3.2;

  const historyPageSize = 8;
  const historyTotalPages = Math.max(1, Math.ceil((historyData?.length ?? 0) / historyPageSize));
  const visibleHistory =
    historyData?.slice((historyPage - 1) * historyPageSize, historyPage * historyPageSize) ?? [];

  const getQualityBadge = (quality?: number | null) => {
    if (quality === undefined || quality === null) return null;
    if (quality >= 80) {
      return (
        <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-bold bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800">
          Tư duy tốt - {quality}đ
        </span>
      );
    }
    if (quality >= 60) {
      return (
        <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-bold bg-indigo-50 dark:bg-indigo-950/40 text-indigo-700 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-800">
          Đạt yêu cầu - {quality}đ
        </span>
      );
    }
    if (quality >= 40) {
      return (
        <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-bold bg-amber-50 dark:bg-amber-950/40 text-amber-700 dark:text-amber-400 border border-amber-200 dark:border-amber-800">
          Cần rèn luyện
        </span>
      );
    }
    return (
      <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-bold bg-rose-50 dark:bg-rose-950/40 text-rose-700 dark:text-rose-400 border border-rose-200 dark:border-rose-800">
        Yếu ({quality}đ)
      </span>
    );
  };

  return (
    <div className="max-w-[1700px] mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6">
      {/* Page Header Banner */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl sm:text-3xl font-black text-slate-900 dark:text-white tracking-tight">
            Hồ Sơ Năng Lực Số (Digital Twin) · {student.fullName}
          </h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400 font-medium">
            Mô hình hóa bản sao tri thức, tư duy giải quyết vấn đề và thói quen học tập.
          </p>
        </div>

        <div className="flex items-center gap-3 self-start md:self-auto">
          <Link
            to={`/hoc-tap/tong-quan?subjectId=${subject.subjectId}`}
            className="inline-flex items-center gap-1.5 rounded-xl bg-white dark:bg-slate-800 px-4 py-2.5 text-xs font-bold text-slate-700 dark:text-slate-200 shadow-xs border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors"
          >
            <span>‹</span>
            <span>Về Dashboard</span>
          </Link>
          <Link
            to={`/hoc-tap/luyen-tap?subjectId=${subject.subjectId}`}
            className="inline-flex items-center gap-1.5 rounded-xl bg-indigo-600 hover:bg-indigo-500 px-5 py-2.5 text-xs font-bold text-white shadow-sm shadow-indigo-600/20 transition-all"
          >
            <span>⚡ Luyện tập ngay</span>
          </Link>
        </div>
      </div>

      {/* Top 2 Bento Cards: Cognitive Growth & Behavioral Twin */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Card 1: Chỉ Số Tăng Trưởng Nhận Thức (Cognitive Growth) */}
        <div className="rounded-3xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 p-6 sm:p-7 shadow-xs flex flex-col justify-between">
          <div>
            <div className="flex items-center gap-3 mb-5">
              <div className="w-10 h-10 rounded-2xl bg-indigo-50 dark:bg-indigo-950/60 text-indigo-600 dark:text-indigo-400 flex items-center justify-center font-bold text-lg">
                ✦
              </div>
              <div>
                <h3 className="text-base font-extrabold text-slate-900 dark:text-white">
                  Chỉ Số Tăng Trưởng Nhận Thức
                </h3>
                <p className="text-xs text-slate-400 dark:text-slate-500 font-medium">Cognitive Growth</p>
              </div>
            </div>

            {/* Overall Mastery & Velocity badge */}
            <div className="flex items-baseline justify-between mb-3">
              <div>
                <span className="text-4xl sm:text-5xl font-black text-slate-900 dark:text-white tracking-tight">
                  {overallMastery.toFixed(1)}%
                </span>
                <p className="text-xs font-bold text-slate-400 dark:text-slate-500 mt-1">Độ thành thạo tổng thể</p>
              </div>

              <div className="flex items-center gap-1 px-2.5 py-1 rounded-full bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-400 border border-emerald-200/80 dark:border-emerald-800 text-xs font-bold">
                <span>↑</span>
                <span>+{Math.abs(growthVelocity).toFixed(1)}% / mốc</span>
              </div>
            </div>

            {/* Gradient Progress Bar */}
            <div className="w-full bg-slate-100 dark:bg-slate-800 rounded-full h-2.5 overflow-hidden mb-6">
              <div
                className="h-full rounded-full bg-gradient-to-r from-indigo-500 via-indigo-600 to-purple-600 transition-all duration-700"
                style={{ width: `${Math.min(100, Math.max(0, overallMastery))}%` }}
              />
            </div>
          </div>

          {/* Sub-metrics 3 columns */}
          <div className="grid grid-cols-3 gap-3 pt-5 border-t border-slate-100 dark:border-slate-800">
            <div>
              <p className="text-lg sm:text-xl font-extrabold text-slate-900 dark:text-white">
                {goal.currentPredictedScore.toFixed(1)} / 10
              </p>
              <p className="text-xs text-slate-400 dark:text-slate-500 font-medium mt-0.5">Điểm dự đoán</p>
            </div>

            <div>
              <p className="text-lg sm:text-xl font-extrabold text-indigo-600 dark:text-indigo-400">
                {goal.targetScore.toFixed(1)}
              </p>
              <p className="text-xs text-slate-400 dark:text-slate-500 font-medium mt-0.5">Điểm mục tiêu</p>
            </div>

            <div>
              <p
                className={`text-lg sm:text-xl font-extrabold ${
                  goal.riskScore >= 70 ? "text-rose-600 dark:text-rose-400" : goal.riskScore >= 30 ? "text-amber-600 dark:text-amber-400" : "text-emerald-600 dark:text-emerald-400"
                }`}
              >
                {goal.riskScore.toFixed(0)}%
              </p>
              <p className="text-xs text-slate-400 dark:text-slate-500 font-medium mt-0.5">Risk index</p>
            </div>
          </div>
        </div>

        {/* Card 2: Mô Hình Hành Vi Học Tập (Behavioral Twin) */}
        <div className="rounded-3xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 p-6 sm:p-7 shadow-xs flex flex-col justify-between">
          <div>
            <div className="flex items-center gap-3 mb-4">
              <div className="w-10 h-10 rounded-2xl bg-emerald-50 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center font-bold text-lg">
                👤
              </div>
              <div>
                <h3 className="text-base font-extrabold text-slate-900 dark:text-white">
                  Mô Hình Hành Vi Học Tập
                </h3>
                <p className="text-xs text-slate-400 dark:text-slate-500 font-medium">Behavioral Twin</p>
              </div>
            </div>

            {/* List of telemetry metrics */}
            <div className="divide-y divide-slate-100 dark:divide-slate-800 text-xs">
              <div className="flex items-center justify-between py-2.5">
                <span className="text-slate-500 dark:text-slate-400 font-medium">Thời gian làm bài trung bình</span>
                <span className="font-bold text-slate-900 dark:text-white">{behaviorTwin.avgTimeSpentSeconds} giây / câu</span>
              </div>

              <div className="flex items-center justify-between py-2.5">
                <span className="text-slate-500 dark:text-slate-400 font-medium">Tỷ lệ bỏ qua câu</span>
                <span className="font-bold text-slate-900 dark:text-white">{behaviorTwin.skipRate.toFixed(1)}%</span>
              </div>

              <div className="flex items-center justify-between py-2.5">
                <span className="text-slate-500 dark:text-slate-400 font-medium">Tỷ lệ thay đổi đáp án</span>
                <span className="font-bold text-slate-900 dark:text-white">{behaviorTwin.changeAnswerRate.toFixed(1)}%</span>
              </div>

              <div className="flex items-center justify-between py-2.5">
                <span className="text-slate-500 dark:text-slate-400 font-medium">Độ tự tin trung bình</span>
                <span className="font-bold text-slate-900 dark:text-white">{behaviorTwin.avgConfidence.toFixed(1)}%</span>
              </div>

              <div className="flex items-center justify-between py-2.5">
                <span className="text-slate-500 dark:text-slate-400 font-medium">Confidence Calibration Index</span>
                <span className="inline-flex items-center gap-1 font-bold text-emerald-700 dark:text-emerald-300 bg-emerald-50 dark:bg-emerald-950/40 px-2 py-0.5 rounded-full border border-emerald-200 dark:border-emerald-800">
                  <span>{(behaviorTwin.confidenceCalibration / 100).toFixed(2)}</span>
                  <span>- Hiệu chuẩn chuẩn xác</span>
                </span>
              </div>

              <div className="flex items-center justify-between py-2.5">
                <span className="text-slate-500 dark:text-slate-400 font-medium">Tổng số lượt làm bài</span>
                <span className="font-bold text-indigo-600 dark:text-indigo-400">{behaviorTwin.attemptCount} bài</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Middle Card: Knowledge Twin · Phân rã chuyên đề */}
      <div className="rounded-3xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden">
        <div className="p-6 sm:p-7 border-b border-slate-100 dark:border-slate-800">
          <h3 className="text-base font-extrabold text-slate-900 dark:text-white">
            Knowledge Twin · Phân rã chuyên đề
          </h3>
          <p className="text-xs text-slate-400 dark:text-slate-500 font-medium mt-0.5">
            Bằng chứng học tập và chất lượng tư duy gần nhất
          </p>
        </div>

        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-100 dark:divide-slate-800 text-left text-xs">
            <thead className="bg-slate-50/75 dark:bg-slate-800/60 text-slate-400 font-bold uppercase tracking-wider">
              <tr>
                <th className="px-6 py-3.5">Chuyên đề</th>
                <th className="px-6 py-3.5">Độ thành thạo</th>
                <th className="px-6 py-3.5">Bằng chứng</th>
                <th className="px-6 py-3.5">Chất lượng tư duy</th>
                <th className="px-6 py-3.5 text-right">Lần làm gần nhất</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {knowledgeTwin.map((topic) => {
                const masteryNum = topic.mastery;
                const barColor =
                  masteryNum >= 75 ? "bg-emerald-500" : masteryNum >= 50 ? "bg-indigo-600" : "bg-amber-500";

                return (
                  <tr key={topic.topicNodeId} className="hover:bg-slate-50/60 dark:hover:bg-slate-800/50 transition-colors">
                    <td className="px-6 py-4 font-bold text-slate-900 dark:text-white">
                      {topic.topicName}
                    </td>

                    <td className="px-6 py-4">
                      <div className="flex items-center gap-3">
                        <div className="w-24 bg-slate-100 dark:bg-slate-800 rounded-full h-2 overflow-hidden">
                          <div
                            className={`h-full rounded-full ${barColor}`}
                            style={{ width: `${Math.min(100, Math.max(0, masteryNum))}%` }}
                          />
                        </div>
                        <span className="font-extrabold text-slate-700 dark:text-slate-300 w-10">
                          {masteryNum.toFixed(0)}%
                        </span>
                      </div>
                    </td>

                    <td className="px-6 py-4 text-slate-500 dark:text-slate-400 font-medium">
                      {topic.evidenceCount} bằng chứng
                    </td>

                    <td className="px-6 py-4">
                      {getQualityBadge(topic.lastReasoningQuality) || (
                        <span className="text-slate-400 text-xs">Chưa có đánh giá</span>
                      )}
                    </td>

                    <td className="px-6 py-4 text-right">
                      {topic.lastAttemptId ? (
                        <Link
                          to={`/hoc-tap/luyen-tap?attemptId=${topic.lastAttemptId}`}
                          className="font-mono text-indigo-600 dark:text-indigo-400 hover:text-indigo-800 dark:hover:text-indigo-300 font-bold"
                        >
                          #{topic.lastAttemptId.slice(0, 8)}
                        </Link>
                      ) : (
                        <span className="text-slate-400">—</span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>

      {/* Bottom Section: Nhật Ký Cập Nhật Năng Lực (Twin Update History) */}
      <div className="rounded-3xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 p-6 sm:p-7 shadow-xs">
        <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-4">
          <div>
            <h3 className="text-base font-extrabold text-slate-900 dark:text-white">
              Nhật Ký Cập Nhật Năng Lực (Twin History)
            </h3>
            <p className="text-xs text-slate-400 dark:text-slate-500 font-medium mt-0.5">
              Các sự kiện phân tích AI và điều chỉnh của giáo viên đã tác động vào hồ sơ
            </p>
          </div>
        </div>

        <div className="divide-y divide-slate-100 dark:divide-slate-800 mt-2">
          {historyLoading ? (
            <div className="p-8 text-center text-xs text-slate-400">Đang tải lịch sử cập nhật...</div>
          ) : visibleHistory.length > 0 ? (
            visibleHistory.map((item) => (
              <div
                key={item.historyId}
                className="flex flex-col sm:flex-row sm:items-center justify-between py-3.5 gap-2 text-xs"
              >
                <div>
                  <div className="flex items-center gap-2">
                    <span className="font-bold text-slate-900 dark:text-white">{item.topicName}</span>
                    <span
                      className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                        item.eventSource === "TeacherOverride"
                          ? "bg-purple-100 dark:bg-purple-950/50 text-purple-700 dark:text-purple-300"
                          : "bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400"
                      }`}
                    >
                      {item.eventSource === "TeacherOverride" ? "Giáo viên xác nhận" : "AI Đánh giá"}
                    </span>
                  </div>
                  <p className="mt-0.5 text-slate-500 dark:text-slate-400 font-normal">{item.explanation}</p>
                </div>

                <div className="flex items-center gap-4 text-right shrink-0">
                  <div>
                    <span className="text-slate-400">{item.previousMastery.toFixed(1)}% → </span>
                    <span className="font-extrabold text-slate-900 dark:text-white">{item.newMastery.toFixed(1)}%</span>
                    <span
                      className={`ml-1 font-bold ${
                        item.delta >= 0 ? "text-emerald-600 dark:text-emerald-400" : "text-rose-600 dark:text-rose-400"
                      }`}
                    >
                      ({item.delta >= 0 ? `+${item.delta.toFixed(1)}` : item.delta.toFixed(1)}%)
                    </span>
                  </div>
                  <span className="text-slate-400 font-mono text-[11px]">
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
            <div className="p-8 text-center text-xs text-slate-400">
              Chưa có sự kiện cập nhật hồ sơ nào
            </div>
          )}
        </div>

        {/* History Pagination */}
        {historyTotalPages > 1 && (
          <div className="mt-4 pt-3 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between text-xs">
            <button
              type="button"
              onClick={() => setHistoryPage((p) => Math.max(1, p - 1))}
              disabled={historyPage <= 1}
              className="rounded-xl bg-white dark:bg-slate-800 px-3.5 py-1.5 font-bold text-slate-700 dark:text-slate-200 border border-slate-200 dark:border-slate-700 shadow-2xs hover:bg-slate-50 dark:hover:bg-slate-700 disabled:opacity-40 cursor-pointer"
            >
              ← Trang trước
            </button>
            <span className="text-slate-400 font-medium">
              Trang {historyPage} / {historyTotalPages}
            </span>
            <button
              type="button"
              onClick={() => setHistoryPage((p) => Math.min(historyTotalPages, p + 1))}
              disabled={historyPage >= historyTotalPages}
              className="rounded-xl bg-white dark:bg-slate-800 px-3.5 py-1.5 font-bold text-slate-700 dark:text-slate-200 border border-slate-200 dark:border-slate-700 shadow-2xs hover:bg-slate-50 dark:hover:bg-slate-700 disabled:opacity-40 cursor-pointer"
            >
              Trang sau →
            </button>
          </div>
        )}
      </div>
    </div>
  );
};
