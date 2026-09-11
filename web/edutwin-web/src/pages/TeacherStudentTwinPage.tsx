import { useParams, Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { getTeacherStudentTwin } from "../api/digitalTwinApi";
import type { StudentTwinDataDto } from "../types/digitalTwin";

export const TeacherStudentTwinPage = () => {
  const { studentId } = useParams<{ studentId: string }>();
  const [searchParams] = useSearchParams();
  const subjectId = searchParams.get("subjectId") || undefined;

  const {
    data: twinData,
    isLoading,
    isError,
    error,
    refetch,
  } = useQuery<StudentTwinDataDto>({
    queryKey: ["teacherStudentTwin", studentId, subjectId],
    queryFn: () => getTeacherStudentTwin(studentId!, subjectId),
    enabled: !!studentId,
  });

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-50">
        <div className="text-indigo-600 font-medium animate-pulse">
          Đang tải Hồ sơ Năng lực (Digital Twin) của học sinh...
        </div>
      </div>
    );
  }

  if (isError || !twinData) {
    return (
      <div className="min-h-screen bg-slate-50 p-6">
        <div className="mx-auto max-w-2xl rounded-xl bg-white p-8 text-center shadow-sm ring-1 ring-slate-200">
          <h2 className="text-xl font-bold text-red-600">Không thể tải Hồ sơ Năng lực</h2>
          <p className="mt-2 text-slate-600">
            {(error as Error)?.message ||
              "Học sinh này không thuộc lớp phụ trách của bạn hoặc không có dữ liệu Digital Twin."}
          </p>
          <div className="mt-4 flex justify-center gap-3">
            <button
              onClick={() => refetch()}
              className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500"
            >
              Thử lại
            </button>
            <Link
              to="/quan-ly/tong-quan-lop-hoc"
              className="rounded-md bg-white px-4 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
            >
              Về Dashboard lớp
            </Link>
          </div>
        </div>
      </div>
    );
  }

  const { student, subject, cognitiveGrowth, knowledgeTwin, behaviorTwin } = twinData;

  return (
    <div className="min-h-screen bg-slate-50 p-6">
      <div className="mx-auto max-w-6xl space-y-6">
        {/* Breadcrumb & Header */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-200 pb-4">
          <div>
            <div className="flex items-center gap-2 text-xs font-semibold text-slate-500 mb-1">
              <Link to="/quan-ly/tong-quan-lop-hoc" className="hover:text-indigo-600">
                Dashboard Lớp
              </Link>
              <span>/</span>
              <span className="text-slate-900">Hồ sơ năng lực học sinh</span>
            </div>
            <h1 className="text-2xl font-bold text-slate-900">
              Hồ Sơ Năng Lực (Digital Twin): {student.fullName}
            </h1>
            <p className="mt-1 text-sm text-slate-500">
              Môn học: <span className="font-semibold text-slate-800">{subject.subjectName}</span>
            </p>
          </div>

          <div className="flex gap-3">
            <Link
              to="/quan-ly/duyet-bai"
              className="rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
            >
              Hàng đợi duyệt bài
            </Link>
            <Link
              to="/quan-ly/tong-quan-lop-hoc"
              className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-bold text-white shadow-sm hover:bg-indigo-500"
            >
              Về Dashboard lớp
            </Link>
          </div>
        </div>

        {/* Cognitive Growth Highlights */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <div className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
              Độ thuần thục tổng thể
            </span>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-3xl font-black text-indigo-600">
                {cognitiveGrowth.overallMastery.toFixed(1)}%
              </span>
            </div>
            <p className="mt-1 text-xs text-slate-500">Mô hình tính toán năng lực xác định</p>
          </div>

          <div className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
              Tốc độ tăng trưởng (Velocity)
            </span>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-3xl font-black text-slate-900">
                {cognitiveGrowth.growthVelocity >= 0 ? `+${cognitiveGrowth.growthVelocity.toFixed(1)}%` : `${cognitiveGrowth.growthVelocity.toFixed(1)}%`}
              </span>
            </div>
            <p className="mt-1 text-xs text-slate-500">Tốc độ tích lũy năng lực</p>
          </div>

          <div className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
              Mức độ rủi ro (Risk Score)
            </span>
            <div className="mt-2 flex items-baseline gap-2">
              <span
                className={`text-3xl font-black ${
                  cognitiveGrowth.riskScore >= 70
                    ? "text-red-600"
                    : cognitiveGrowth.riskScore >= 30
                    ? "text-amber-600"
                    : "text-emerald-600"
                }`}
              >
                {cognitiveGrowth.riskScore.toFixed(1)}%
              </span>
            </div>
            <p className="mt-1 text-xs text-slate-500">Xác suất tụt lùi so với mục tiêu</p>
          </div>

          <div className="rounded-xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
              Dự báo điểm kỳ thi
            </span>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-3xl font-black text-purple-600">
                {cognitiveGrowth.currentPredictedScore.toFixed(1)}
              </span>
              <span className="text-xs text-slate-400">/ {cognitiveGrowth.targetScore.toFixed(1)}</span>
            </div>
            <p className="mt-1 text-xs text-slate-500">Mục tiêu đặt ra của học sinh</p>
          </div>
        </div>

        {/* Knowledge Twin (Topic Mastery Breakdown) */}
        <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
          <div className="flex items-center justify-between mb-4 border-b border-slate-100 pb-3">
            <div>
              <h2 className="text-lg font-bold text-slate-900">Hồ Sơ Kiến Thức (Knowledge Twin)</h2>
              <p className="text-xs text-slate-500">
                Phân tích độ thuần thục theo từng nút chuyên đề trong đồ thị kiến thức.
              </p>
            </div>
            <span className="text-xs font-semibold text-slate-500">
              {knowledgeTwin.length} chuyên đề
            </span>
          </div>

          <div className="divide-y divide-slate-100">
            {knowledgeTwin.map((kt) => (
              <div key={kt.topicNodeId} className="py-3 flex flex-col sm:flex-row sm:items-center justify-between gap-2">
                <div className="flex-1">
                  <div className="flex items-center gap-2">
                    <span className="font-semibold text-slate-900 text-sm">{kt.topicName}</span>
                    <span
                      className={`rounded px-2 py-0.5 text-[10px] font-bold uppercase ${
                        kt.mastery >= 80
                          ? "bg-emerald-100 text-emerald-800"
                          : kt.mastery >= 50
                          ? "bg-indigo-100 text-indigo-800"
                          : "bg-red-100 text-red-800"
                      }`}
                    >
                      {kt.mastery >= 80
                        ? "Thành thạo"
                        : kt.mastery >= 50
                        ? "Đang học"
                        : "Lỗ hổng (Gap)"}
                    </span>
                  </div>
                  <div className="mt-1 flex items-center gap-3 text-xs text-slate-500">
                    <span>Số bằng chứng tích lũy: {kt.evidenceCount} lượt</span>
                    {kt.lastReasoningQuality !== null && kt.lastReasoningQuality !== undefined && (
                      <>
                        <span>·</span>
                        <span>Chất lượng suy luận gần nhất: {kt.lastReasoningQuality.toFixed(0)}%</span>
                      </>
                    )}
                    {kt.lastAttemptAt && (
                      <>
                        <span>·</span>
                        <span>Gần nhất: {new Date(kt.lastAttemptAt).toLocaleDateString("vi-VN")}</span>
                      </>
                    )}
                  </div>
                </div>

                <div className="w-full sm:w-48 flex items-center gap-3">
                  <div className="flex-1 h-2.5 rounded-full bg-slate-100 overflow-hidden">
                    <div
                      className={`h-full rounded-full ${
                        kt.mastery >= 80
                          ? "bg-emerald-500"
                          : kt.mastery >= 50
                          ? "bg-amber-500"
                          : "bg-red-500"
                      }`}
                      style={{ width: `${Math.min(100, Math.max(0, kt.mastery))}%` }}
                    />
                  </div>
                  <span className="w-12 text-right text-xs font-bold text-slate-800">
                    {kt.mastery.toFixed(0)}%
                  </span>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Behavior Twin (6 strictly governed metrics) */}
        {behaviorTwin && (
          <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <h2 className="text-lg font-bold text-slate-900 mb-1">
              Hồ Sơ Hành Vi Học Tập (Behavior Twin)
            </h2>
            <p className="text-xs text-slate-500 mb-4">
              Chỉ số hành vi nhận thức được đo lường tự động từ tương tác luyện tập.
            </p>

            <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
              <div className="rounded-lg bg-slate-50 p-3 text-center ring-1 ring-slate-200">
                <span className="text-[11px] font-bold text-slate-500 uppercase block">
                  Thời gian TB / câu
                </span>
                <span className="mt-1 text-lg font-bold text-slate-900 block">
                  {Math.round(behaviorTwin.avgTimeSpentSeconds)}s
                </span>
                <span className="text-[10px] text-slate-400">Tốc độ làm bài</span>
              </div>

              <div className="rounded-lg bg-slate-50 p-3 text-center ring-1 ring-slate-200">
                <span className="text-[11px] font-bold text-slate-500 uppercase block">
                  Tỷ lệ bỏ qua
                </span>
                <span
                  className={`mt-1 text-lg font-bold block ${
                    behaviorTwin.skipRate > 0.3 ? "text-red-600" : "text-emerald-600"
                  }`}
                >
                  {(behaviorTwin.skipRate * 100).toFixed(0)}%
                </span>
                <span className="text-[10px] text-slate-400">Bỏ câu hỏi</span>
              </div>

              <div className="rounded-lg bg-slate-50 p-3 text-center ring-1 ring-slate-200">
                <span className="text-[11px] font-bold text-slate-500 uppercase block">
                  Độ tự tin TB
                </span>
                <span className="mt-1 text-lg font-bold text-indigo-600 block">
                  {behaviorTwin.avgConfidence.toFixed(0)}%
                </span>
                <span className="text-[10px] text-slate-400">Tự đánh giá</span>
              </div>

              <div className="rounded-lg bg-slate-50 p-3 text-center ring-1 ring-slate-200">
                <span className="text-[11px] font-bold text-slate-500 uppercase block">
                  Chuẩn hóa tự tin
                </span>
                <span className="mt-1 text-lg font-bold text-slate-900 block">
                  {behaviorTwin.confidenceCalibration.toFixed(2)}
                </span>
                <span className="text-[10px] text-slate-400">Độ khớp tự tin/thực tế</span>
              </div>

              <div className="rounded-lg bg-slate-50 p-3 text-center ring-1 ring-slate-200">
                <span className="text-[11px] font-bold text-slate-500 uppercase block">
                  Thay đổi đáp án
                </span>
                <span className="mt-1 text-lg font-bold text-purple-600 block">
                  {behaviorTwin.changeAnswerRate.toFixed(1)} lần
                </span>
                <span className="text-[10px] text-slate-400">Trung bình mỗi câu</span>
              </div>

              <div className="rounded-lg bg-slate-50 p-3 text-center ring-1 ring-slate-200">
                <span className="text-[11px] font-bold text-slate-500 uppercase block">
                  Tổng lượt làm bài
                </span>
                <span className="mt-1 text-lg font-bold text-slate-900 block">
                  {behaviorTwin.attemptCount} lượt
                </span>
                <span className="text-[10px] text-slate-400">Tần suất tương tác</span>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
