import { useState, useEffect } from "react";
import { overrideReasoningAnalysis } from "../api/teacherReviewsApi";
import type { TeacherReviewQueueItemDto, ErrorType, TeacherOverrideRequest } from "../types/reviews";
import axios from "axios";

interface TeacherOverrideModalProps {
  review: TeacherReviewQueueItemDto | null;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export const TeacherOverrideModal = ({
  review,
  isOpen,
  onClose,
  onSuccess,
}: TeacherOverrideModalProps) => {
  const [isCorrect, setIsCorrect] = useState<boolean>(true);
  const [reasoningQuality, setReasoningQuality] = useState<number>(80);
  const [errorType, setErrorType] = useState<ErrorType>("None");
  const [awardedScore, setAwardedScore] = useState<number | "">("");
  const [feedback, setFeedback] = useState<string>("");
  const [reason, setReason] = useState<string>("");
  const [overrideVersion, setOverrideVersion] = useState<number>(0);

  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successInfo, setSuccessInfo] = useState<string | null>(null);

  useEffect(() => {
    if (review) {
      setIsCorrect(true);
      setReasoningQuality(review.reasoningQuality ? Number(review.reasoningQuality) : 75);
      setErrorType("None");
      setAwardedScore("");
      setFeedback(review.analysisFeedback || "");
      setReason("");
      setOverrideVersion(0);
      setErrorMessage(null);
      setSuccessInfo(null);
    }
  }, [review]);

  if (!isOpen || !review) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reason.trim()) {
      setErrorMessage("Vui lòng cung cấp lý do điều chỉnh (bắt buộc theo quy định kiểm toán).");
      return;
    }

    setIsSubmitting(true);
    setErrorMessage(null);

    const payload: TeacherOverrideRequest = {
      isCorrect,
      reasoningQuality: Number(reasoningQuality),
      errorType,
      feedback: feedback.trim(),
      reason: reason.trim(),
      overrideVersion,
      awardedScore: awardedScore !== "" ? Number(awardedScore) : null,
    };

    try {
      const response = await overrideReasoningAnalysis(review.analysisId, payload);
      setSuccessInfo(
        `Điều chỉnh thành công! Năng lực mới của học sinh: ${response.data.replay.newMastery.toFixed(1)}%`
      );
      setTimeout(() => {
        onSuccess();
        onClose();
      }, 1200);
    } catch (err: unknown) {
      if (axios.isAxiosError(err) && err.response?.status === 409) {
        setErrorMessage(
          "Xung đột phiên bản (409 Conflict): Lượt phân tích này đã được điều chỉnh bởi một phiên khác. Vui lòng làm mới danh sách."
        );
      } else if (axios.isAxiosError(err) && err.response?.data?.detail) {
        setErrorMessage(err.response.data.detail);
      } else {
        setErrorMessage("Đã xảy ra lỗi khi gửi yêu cầu điều chỉnh. Vui lòng thử lại.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-slate-900/60 p-4">
      <div className="relative w-full max-w-2xl rounded-2xl bg-white p-6 shadow-2xl ring-1 ring-slate-200 sm:p-8 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between border-b border-slate-100 pb-4">
          <div>
            <span className="text-xs font-bold uppercase tracking-wider text-indigo-600">
              Giám sát & Đánh giá chuyên môn
            </span>
            <h2 className="text-xl font-bold text-slate-900">
              Điều Chỉnh Đánh Giá Suy Luận (Teacher Override)
            </h2>
          </div>
          <button
            onClick={onClose}
            disabled={isSubmitting}
            className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
          >
            ✕
          </button>
        </div>

        {/* Attempt info preview */}
        <div className="mt-4 rounded-xl bg-slate-50 p-4 text-sm space-y-3">
          <div className="flex items-center justify-between">
            <span className="font-semibold text-slate-900">Học sinh: {review.studentName}</span>
            <span className="text-xs text-slate-500">Mã lượt làm: #{review.attemptId}</span>
          </div>

          <div>
            <span className="text-xs font-bold text-slate-500">Nội dung câu hỏi:</span>
            <p className="mt-1 text-slate-800 line-clamp-2">{review.questionText}</p>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-2 border-t border-slate-200">
            <div>
              <span className="text-xs font-bold text-slate-500">Đáp án học sinh:</span>
              <p className="font-mono font-medium text-slate-900">{review.finalAnswer || "(Trống)"}</p>
            </div>
            <div>
              <span className="text-xs font-bold text-slate-500">AI đánh giá ban đầu:</span>
              <p className="text-slate-700">
                Chất lượng: {review.reasoningQuality ?? "N/A"}% · Tin cậy:{" "}
                {review.analysisConfidence ? `${(review.analysisConfidence * 100).toFixed(0)}%` : "N/A"}
              </p>
            </div>
          </div>

          {review.reasoningText && (
            <div>
              <span className="text-xs font-bold text-slate-500">Các bước suy luận của học sinh:</span>
              <p className="mt-1 rounded bg-white p-2.5 font-mono text-xs text-slate-700 whitespace-pre-wrap ring-1 ring-slate-200">
                {review.reasoningText}
              </p>
            </div>
          )}

          {review.isFallback && (
            <div className="rounded-lg bg-amber-50 p-2.5 text-xs font-medium text-amber-800 ring-1 ring-amber-200">
              ⚠️ Lượt phân tích này được sinh bởi thuật toán dự phòng (Rule-based Fallback) do chất lượng suy luận thấp hoặc AI không chắc chắn.
            </div>
          )}
        </div>

        {/* Feedback / error alerts */}
        {errorMessage && (
          <div className="mt-4 rounded-lg bg-red-50 p-3 text-sm font-medium text-red-800 ring-1 ring-red-200">
            {errorMessage}
          </div>
        )}

        {successInfo && (
          <div className="mt-4 rounded-lg bg-emerald-50 p-3 text-sm font-medium text-emerald-800 ring-1 ring-emerald-200">
            {successInfo}
          </div>
        )}

        {/* Adjustment Form */}
        <form onSubmit={handleSubmit} className="mt-6 space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {/* Correctness */}
            <div>
              <label className="block text-xs font-bold uppercase tracking-wider text-slate-700 mb-1">
                Kết quả bài giải
              </label>
              <div className="flex gap-4 mt-2">
                <label className="inline-flex items-center gap-2 text-sm font-medium text-slate-700 cursor-pointer">
                  <input
                    type="radio"
                    name="isCorrect"
                    checked={isCorrect === true}
                    onChange={() => setIsCorrect(true)}
                    className="text-indigo-600 focus:ring-indigo-500"
                  />
                  Chính xác (Đúng)
                </label>
                <label className="inline-flex items-center gap-2 text-sm font-medium text-slate-700 cursor-pointer">
                  <input
                    type="radio"
                    name="isCorrect"
                    checked={isCorrect === false}
                    onChange={() => setIsCorrect(false)}
                    className="text-indigo-600 focus:ring-indigo-500"
                  />
                  Chưa chính xác (Sai)
                </label>
              </div>
            </div>

            {/* Error Type */}
            <div>
              <label className="block text-xs font-bold uppercase tracking-wider text-slate-700 mb-1">
                Phân loại lỗi (Error Type)
              </label>
              <select
                value={errorType}
                onChange={(e) => setErrorType(e.target.value as ErrorType)}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
              >
                <option value="None">Không có lỗi (None)</option>
                <option value="Knowledge">Lỗ hổng kiến thức (Knowledge)</option>
                <option value="Skill">Kỹ năng tính toán (Skill)</option>
                <option value="Reasoning">Lỗi suy luận logic (Reasoning)</option>
                <option value="Behavior">Thói quen làm bài (Behavior)</option>
                <option value="Presentation">Trình bày / Diễn đạt (Presentation)</option>
                <option value="Unknown">Không xác định (Unknown)</option>
              </select>
            </div>
          </div>

          {/* Reasoning Quality Slider */}
          <div>
            <div className="flex items-center justify-between mb-1">
              <label className="text-xs font-bold uppercase tracking-wider text-slate-700">
                Chất lượng suy luận (Reasoning Quality)
              </label>
              <span className="text-sm font-bold text-indigo-600">{reasoningQuality}/100</span>
            </div>
            <input
              type="range"
              min={0}
              max={100}
              value={reasoningQuality}
              onChange={(e) => setReasoningQuality(Number(e.target.value))}
              className="w-full accent-indigo-600 cursor-pointer"
            />
          </div>

          {/* Teacher Feedback to student */}
          <div>
            <label className="block text-xs font-bold uppercase tracking-wider text-slate-700 mb-1">
              Nhận xét của Giáo viên (Gửi tới học sinh)
            </label>
            <textarea
              rows={2}
              value={feedback}
              onChange={(e) => setFeedback(e.target.value)}
              placeholder="Nhập nhận xét chi tiết giúp học sinh cải thiện phương pháp giải..."
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
            />
          </div>

          {/* Override Reason (Mandatory) */}
          <div>
            <label className="block text-xs font-bold uppercase tracking-wider text-slate-700 mb-1">
              Lý do can thiệp / điều chỉnh <span className="text-red-500">* (Bắt buộc kiểm toán)</span>
            </label>
            <input
              type="text"
              required
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Ví dụ: AI đánh giá nhầm bước quy đồng; Học sinh giải theo cách sáng tạo khác..."
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
            />
          </div>

          {/* Buttons */}
          <div className="flex justify-end gap-3 pt-4 border-t border-slate-100">
            <button
              type="button"
              onClick={onClose}
              disabled={isSubmitting}
              className="rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={isSubmitting}
              className="rounded-lg bg-indigo-600 px-5 py-2 text-sm font-bold text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
            >
              {isSubmitting ? "Đang áp dụng..." : "Xác nhận & Cập nhật Twin"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
