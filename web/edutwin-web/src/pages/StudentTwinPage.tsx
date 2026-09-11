import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { getStudentTwin, getStudentTwinHistory } from "../api/digitalTwinApi";
import type { StudentTwinDataDto, TwinUpdateHistoryItemDto, PagedList } from "../types/digitalTwin";

export const StudentTwinPage = () => {
  const [searchParams] = useSearchParams();
  const selectedSubjectId = searchParams.get("subjectId") || undefined;
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
  });

  const {
    data: historyData,
    isLoading: historyLoading,
  } = useQuery<PagedList<TwinUpdateHistoryItemDto>>({
    queryKey: ["studentTwinHistory", selectedSubjectId, historyPage],
    queryFn: () => getStudentTwinHistory(selectedSubjectId, undefined, historyPage, 10),
  });

  if (twinLoading) {
    return (
      <div className="min-h-screen bg-slate-50 p-6">
        <div className="mx-auto max-w-6xl space-y-6">
          <div className="h-28 animate-pulse rounded-xl bg-white shadow-sm ring-1 ring-slate-200" />
          <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
            <div className="h-64 animate-pulse rounded-xl bg-white shadow-sm ring-1 ring-slate-200" />
            <div className="h-64 animate-pulse rounded-xl bg-white shadow-sm ring-1 ring-slate-200" />
          </div>
          <div className="h-96 animate-pulse rounded-xl bg-white shadow-sm ring-1 ring-slate-200" />
        </div>
      </div>
    );
  }

  if (twinError || !twinData) {
    return (
      <div className="min-h-screen bg-slate-50 p-6">
        <div className="mx-auto max-w-4xl rounded-xl bg-white p-8 shadow-sm ring-1 ring-slate-200">
          <h2 className="text-xl font-bold text-red-600">Không thể tải Hồ sơ Năng lực (Digital Twin)</h2>
          <p className="mt-2 text-slate-600">
            {(twinErrorObj as Error)?.message || "Vui lòng kiểm tra lại quyền truy cập hoặc kết nối."}
          </p>
          <div className="mt-4 flex gap-3">
            <button
              onClick={() => refetchTwin()}
              className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500"
            >
              Thử lại
            </button>
            <Link
              to="/hoc-tap/tong-quan"
              className="rounded-md bg-white px-4 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
            >
              Về Dashboard
            </Link>
          </div>
        </div>
      </div>
    );
  }

  const { student, subject, cognitiveGrowth, knowledgeTwin, behaviorTwin } = twinData;

  const getQualityBadge = (quality?: number | null) => {
    if (quality === undefined || quality === null) return null;
    if (quality >= 80) {
      return (
        <span className="rounded-full bg-emerald-100 px-2 py-0.5 text-xs font-semibold text-emerald-800">
          Tư duy tốt ({quality}đ)
        </span>
      );
    }
    if (quality >= 60) {
      return (
        <span className="rounded-full bg-blue-100 px-2 py-0.5 text-xs font-semibold text-blue-800">
          Đạt yêu cầu ({quality}đ)
        </span>
      );
    }
    if (quality >= 40) {
      return (
        <span className="rounded-full bg-amber-100 px-2 py-0.5 text-xs font-semibold text-amber-800">
          Cần rèn luyện ({quality}đ)
        </span>
      );
    }
    return (
      <span className="rounded-full bg-red-100 px-2 py-0.5 text-xs font-semibold text-red-800">
        Yếu ({quality}đ)
      </span>
    );
  };

  return (
    <div className="min-h-screen bg-slate-50 p-6">
      <div className="mx-auto max-w-6xl space-y-6">
        {/* Header */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200 gap-4">
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-2xl font-bold text-slate-900">
                Hồ Sơ Năng Lực Số (Digital Twin) · {student.fullName}
              </h1>
              <span className="rounded-md bg-indigo-50 px-2.5 py-1 text-xs font-medium text-indigo-700 ring-1 ring-inset ring-indigo-700/10">
                Môn: {subject.subjectName}
              </span>
            </div>
            <p className="mt-1 text-sm text-slate-500">
              Mô hình hóa toàn diện tri thức, tư duy giải quyết vấn đề và hành vi học tập.
            </p>
          </div>
          <div className="flex items-center gap-3">
            <Link
              to="/hoc-tap/tong-quan"
              className="inline-flex items-center rounded-md bg-white px-3.5 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
            >
              Về Dashboard
            </Link>
            <Link
              to={`/hoc-tap/luyen-tap?subjectId=${subject.subjectId}`}
              className="inline-flex items-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500"
            >
              Luyện tập ngay
            </Link>
          </div>
        </div>

        {/* Cognitive Growth & Behavior Twin Cards */}
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
          {/* Cognitive Growth Card */}
          <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200 lg:col-span-2">
            <h3 className="text-base font-bold text-slate-900">Chỉ Số Tăng Trưởng Nhận Thức</h3>
            <p className="text-xs text-slate-500">
              Tổng hợp điểm thành thạo môn học và tốc độ cải thiện năng lực.
            </p>

            <div className="mt-6 grid grid-cols-2 gap-4 sm:grid-cols-4">
              <div className="rounded-lg bg-indigo-50/50 p-4">
                <p className="text-xs font-medium text-slate-500">Độ thành thạo tổng</p>
                <p className="mt-1 text-2xl font-extrabold text-indigo-600">
                  {cognitiveGrowth.overallMastery.toFixed(1)}%
                </p>
                <div className="mt-2 h-2 w-full rounded-full bg-slate-200">
                  <div
                    className="h-2 rounded-full bg-indigo-600"
                    style={{ width: `${Math.min(100, Math.max(0, cognitiveGrowth.overallMastery))}%` }}
                  />
                </div>
              </div>

              <div className="rounded-lg bg-emerald-50/50 p-4">
                <p className="text-xs font-medium text-slate-500">Tốc độ tăng trưởng</p>
                <p className="mt-1 text-2xl font-extrabold text-emerald-600">
                  +{cognitiveGrowth.growthVelocity.toFixed(1)}%
                </p>
                <p className="mt-1 text-xs text-slate-400">trên mỗi đợt làm bài</p>
              </div>

              <div className="rounded-lg bg-slate-50 p-4">
                <p className="text-xs font-medium text-slate-500">Dự đoán hiện tại</p>
                <p className="mt-1 text-2xl font-extrabold text-slate-900">
                  {cognitiveGrowth.currentPredictedScore.toFixed(1)}
                  <span className="text-xs font-normal text-slate-400"> / 10</span>
                </p>
                <p className="mt-1 text-xs text-slate-400">Mục tiêu: {cognitiveGrowth.targetScore.toFixed(1)}</p>
              </div>

              <div className="rounded-lg bg-slate-50 p-4">
                <p className="text-xs font-medium text-slate-500">Chỉ số rủi ro</p>
                <p
                  className={`mt-1 text-2xl font-extrabold ${
                    cognitiveGrowth.riskScore >= 70
                      ? "text-red-600"
                      : cognitiveGrowth.riskScore >= 30
                      ? "text-amber-600"
                      : "text-emerald-600"
                  }`}
                >
                  {cognitiveGrowth.riskScore.toFixed(1)}%
                </p>
                <p className="mt-1 text-xs text-slate-400">
                  {cognitiveGrowth.riskScore >= 70 ? "Cần can thiệp" : "Kiểm soát tốt"}
                </p>
              </div>
            </div>
          </div>

          {/* Behavior Twin Card */}
          <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <h3 className="text-base font-bold text-slate-900">Mô Hình Hành Vi Học Tập</h3>
            <p className="text-xs text-slate-500">
              Đo lường nhịp độ làm bài và độ chuẩn xác tự đánh giá.
            </p>

            <div className="mt-4 space-y-3">
              <div className="flex items-center justify-between border-b border-slate-100 py-2">
                <span className="text-xs text-slate-600">Thời gian làm bài TB</span>
                <span className="text-sm font-semibold text-slate-900">
                  {behaviorTwin.avgTimeSpentSeconds} giây / câu
                </span>
              </div>

              <div className="flex items-center justify-between border-b border-slate-100 py-2">
                <span className="text-xs text-slate-600">Tỷ lệ bỏ qua câu</span>
                <span className="text-sm font-semibold text-slate-900">
                  {(behaviorTwin.skipRate * 100).toFixed(1)}%
                </span>
              </div>

              <div className="flex items-center justify-between border-b border-slate-100 py-2">
                <span className="text-xs text-slate-600">Tỷ lệ thay đổi đáp án</span>
                <span className="text-sm font-semibold text-slate-900">
                  {(behaviorTwin.changeAnswerRate * 100).toFixed(1)}%
                </span>
              </div>

              <div className="flex items-center justify-between border-b border-slate-100 py-2">
                <span className="text-xs text-slate-600">Độ tự tin trung bình</span>
                <span className="text-sm font-semibold text-slate-900">
                  {(behaviorTwin.avgConfidence * 100).toFixed(1)}%
                </span>
              </div>

              <div className="flex items-center justify-between border-b border-slate-100 py-2">
                <span className="text-xs text-slate-600">Hiệu chuẩn tự tin</span>
                <span
                  className={`text-sm font-semibold ${
                    behaviorTwin.confidenceCalibration >= 0.8
                      ? "text-emerald-600"
                      : "text-amber-600"
                  }`}
                >
                  {behaviorTwin.confidenceCalibration.toFixed(2)}
                </span>
              </div>

              <div className="flex items-center justify-between py-2">
                <span className="text-xs text-slate-600">Tổng số lượt làm bài</span>
                <span className="text-sm font-semibold text-indigo-600">
                  {behaviorTwin.attemptCount} bài
                </span>
              </div>
            </div>
          </div>
        </div>

        {/* Knowledge Topics Breakdown Table */}
        <div className="overflow-hidden rounded-xl bg-white shadow-sm ring-1 ring-slate-200">
          <div className="border-b border-slate-100 p-6">
            <h3 className="text-base font-bold text-slate-900">
              Chi Tiết Tri Thức Số (Knowledge Twin Topics)
            </h3>
            <p className="text-xs text-slate-500">
              Định vị mức độ thành thạo và chất lượng lập luận ở từng chủ đề kiến thức.
            </p>
          </div>

          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
              <thead className="bg-slate-50 text-xs font-semibold text-slate-500">
                <tr>
                  <th className="px-6 py-3">Chuyên đề</th>
                  <th className="px-6 py-3">Độ thành thạo</th>
                  <th className="px-6 py-3">Số lượng bằng chứng</th>
                  <th className="px-6 py-3">Chất lượng tư duy gần nhất</th>
                  <th className="px-6 py-3">Lần làm bài gần nhất</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {knowledgeTwin.map((topic) => (
                  <tr key={topic.topicNodeId} className="hover:bg-slate-50/50">
                    <td className="px-6 py-4 font-medium text-slate-900">
                      {topic.topicName}
                    </td>
                    <td className="px-6 py-4">
                      <div className="flex items-center gap-3">
                        <span className="w-12 text-sm font-bold text-slate-700">
                          {topic.mastery.toFixed(1)}%
                        </span>
                        <div className="h-2 w-32 rounded-full bg-slate-200">
                          <div
                            className={`h-2 rounded-full ${
                              topic.mastery >= 75
                                ? "bg-emerald-500"
                                : topic.mastery >= 50
                                ? "bg-indigo-500"
                                : "bg-amber-500"
                            }`}
                            style={{ width: `${Math.min(100, Math.max(0, topic.mastery))}%` }}
                          />
                        </div>
                      </div>
                    </td>
                    <td className="px-6 py-4 text-slate-600">
                      {topic.evidenceCount} bằng chứng
                    </td>
                    <td className="px-6 py-4">
                      {getQualityBadge(topic.lastReasoningQuality) || (
                        <span className="text-xs text-slate-400">Chưa có đánh giá</span>
                      )}
                    </td>
                    <td className="px-6 py-4 text-xs text-slate-500">
                      {topic.lastAttemptAt
                        ? new Date(topic.lastAttemptAt).toLocaleDateString("vi-VN", {
                            day: "2-digit",
                            month: "2-digit",
                            year: "numeric",
                            hour: "2-digit",
                            minute: "2-digit",
                          })
                        : "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Update History Timeline */}
        <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
          <div className="flex items-center justify-between border-b border-slate-100 pb-4">
            <div>
              <h3 className="text-base font-bold text-slate-900">
                Nhật Ký Cập Nhật Năng Lực (Twin History)
              </h3>
              <p className="text-xs text-slate-500">
                Các sự kiện phân tích AI và điều chỉnh của giáo viên đã tác động vào hồ sơ.
              </p>
            </div>
          </div>

          <div className="mt-4 divide-y divide-slate-100">
            {historyLoading ? (
              <div className="p-4 text-center text-sm text-slate-400">Đang tải lịch sử...</div>
            ) : historyData && historyData.items.length > 0 ? (
              historyData.items.map((item) => (
                <div key={item.historyId} className="flex flex-col sm:flex-row sm:items-center justify-between py-3 gap-2">
                  <div>
                    <div className="flex items-center gap-2">
                      <span className="font-semibold text-slate-900">{item.topicName}</span>
                      <span className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-600">
                        {item.eventSource}
                      </span>
                    </div>
                    <p className="mt-0.5 text-xs text-slate-500">{item.explanation}</p>
                  </div>
                  <div className="flex items-center gap-4 text-right">
                    <div>
                      <span className="text-xs text-slate-400">
                        {item.previousMastery.toFixed(1)}% →{" "}
                      </span>
                      <span className="text-sm font-bold text-slate-900">
                        {item.newMastery.toFixed(1)}%
                      </span>{" "}
                      <span
                        className={`text-xs font-semibold ${
                          item.delta >= 0 ? "text-emerald-600" : "text-red-600"
                        }`}
                      >
                        ({item.delta >= 0 ? `+${item.delta.toFixed(1)}` : item.delta.toFixed(1)}%)
                      </span>
                    </div>
                    <span className="text-xs text-slate-400">
                      {new Date(item.createdAt).toLocaleDateString("vi-VN", {
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
              <div className="p-4 text-center text-sm text-slate-400">
                Chưa có sự kiện cập nhật nào
              </div>
            )}
          </div>

          {historyData && historyData.totalPages > 1 && (
            <div className="mt-4 flex items-center justify-between border-t border-slate-100 pt-3">
              <button
                onClick={() => setHistoryPage((p) => Math.max(1, p - 1))}
                disabled={historyPage <= 1}
                className="rounded-md bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 shadow-sm ring-1 ring-slate-300 disabled:opacity-50"
              >
                Trang trước
              </button>
              <span className="text-xs text-slate-500">
                Trang {historyPage} / {historyData.totalPages}
              </span>
              <button
                onClick={() => setHistoryPage((p) => Math.min(historyData.totalPages, p + 1))}
                disabled={historyPage >= historyData.totalPages}
                className="rounded-md bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 shadow-sm ring-1 ring-slate-300 disabled:opacity-50"
              >
                Trang sau
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
