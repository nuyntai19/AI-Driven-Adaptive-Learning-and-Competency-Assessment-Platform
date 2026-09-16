import { useState, useEffect, useRef } from "react";
import { overrideReasoningAnalysis } from "../api/teacherReviewsApi";
import type { TeacherReviewQueueItemDto, ErrorType, TeacherOverrideRequest } from "../types/reviews";
import { extractProblemDetails, isOverrideConflict } from "../utils/problemDetails";
import { isValidOccVersion, formatOccVersionLabel } from "../utils/reviewQueueHelpers";

interface TeacherOverrideModalProps {
  review: TeacherReviewQueueItemDto | null;
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  onRefetch?: () => Promise<number>;
}

export const TeacherOverrideModal = ({
  review,
  isOpen,
  onClose,
  onSuccess,
  onRefetch,
}: TeacherOverrideModalProps) => {
  const [isCorrect, setIsCorrect] = useState<boolean>(true);
  const [reasoningQuality, setReasoningQuality] = useState<number>(80);
  const [errorType, setErrorType] = useState<ErrorType>("None");
  const [awardedScore, setAwardedScore] = useState<number | "">("");
  const [feedback, setFeedback] = useState<string>("");
  const [reason, setReason] = useState<string>("");
  const [overrideVersion, setOverrideVersion] = useState<number | null>(null);
  const [isConflict, setIsConflict] = useState<boolean>(false);
  const [conflictDetails, setConflictDetails] = useState<{ message: string; traceId?: string } | null>(null);
  const [isRefetching, setIsRefetching] = useState<boolean>(false);

  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successInfo, setSuccessInfo] = useState<string | null>(null);
  const dialogRef = useRef<HTMLDivElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const isSubmittingRef = useRef(false);

  useEffect(() => {
    isSubmittingRef.current = isSubmitting;
  }, [isSubmitting]);

  useEffect(() => {
    if (review) {
      setIsCorrect(true);
      setReasoningQuality(review.reasoningQuality !== null ? Number(review.reasoningQuality) : 75);
      setErrorType("None");
      setAwardedScore("");
      setFeedback(review.analysisFeedback || "");
      setReason("");

      const rawVersion = review.evidence?.analysisOverrideVersion;
      if (isValidOccVersion(rawVersion)) {
        setOverrideVersion(rawVersion);
      } else {
        setOverrideVersion(null);
      }

      setIsConflict(false);
      setConflictDetails(null);
      setErrorMessage(null);
      setSuccessInfo(null);
    }
  }, [review]);

  useEffect(() => {
    if (!isOpen) return;

    const previouslyFocused = document.activeElement as HTMLElement | null;
    closeButtonRef.current?.focus();

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !isSubmittingRef.current) {
        event.preventDefault();
        onClose();
        return;
      }

      if (event.key !== "Tab" || !dialogRef.current) return;
      const focusable = Array.from(
        dialogRef.current.querySelectorAll<HTMLElement>(
          'button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [href], [tabindex]:not([tabindex="-1"])'
        )
      );
      if (focusable.length === 0) return;
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      previouslyFocused?.focus();
    };
  }, [isOpen, onClose]);

  if (!isOpen || !review) return null;

  const handleRefetch = async () => {
    if (!onRefetch) return;
    setIsRefetching(true);
    setErrorMessage(null);
    try {
      const freshVersion = await onRefetch();
      if (!isValidOccVersion(freshVersion)) {
        throw new Error("Không thể xác định phiên bản OCC hợp lệ sau khi tải lại.");
      }
      setOverrideVersion(freshVersion);
      setIsConflict(false);
      setConflictDetails(null);
      setErrorMessage(null);
    } catch (err: unknown) {
      const errorInfo = extractProblemDetails(err);
      setErrorMessage(errorInfo.message || "Không thể làm mới dữ liệu từ máy chủ. Vui lòng thử lại.");
    } finally {
      setIsRefetching(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reason.trim()) {
      setErrorMessage("Vui lòng cung cấp lý do điều chỉnh (bắt buộc theo quy định kiểm toán).");
      return;
    }

    if (overrideVersion === null || !isValidOccVersion(overrideVersion)) {
      setErrorMessage("Không thể xác định phiên bản đồng thời hợp lệ. Vui lòng tải lại dữ liệu trước khi can thiệp.");
      return;
    }

    if (isConflict) {
      setErrorMessage("Dữ liệu đang gặp xung đột phiên bản (409 Conflict). Vui lòng đồng bộ lại trước khi gửi.");
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
      if (isOverrideConflict(err)) {
        const errorInfo = extractProblemDetails(err);
        setIsConflict(true);
        setConflictDetails({
          message: "Lượt phân tích đã được cập nhật bởi một phiên làm việc khác.",
          traceId: errorInfo.traceId ?? undefined,
        });
      } else {
        const errorInfo = extractProblemDetails(err);
        setErrorMessage(errorInfo.message);
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-slate-900/60 p-4">
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="teacher-override-title"
        className="relative w-full max-w-2xl rounded-2xl bg-white p-6 shadow-2xl ring-1 ring-slate-200 sm:p-8 max-h-[90vh] overflow-y-auto"
      >
        <div className="flex items-center justify-between border-b border-slate-100 pb-4">
          <div>
            <span className="text-xs font-bold uppercase tracking-wider text-indigo-600">
              Giám sát & Đánh giá chuyên môn
            </span>
            <h2 id="teacher-override-title" className="text-xl font-bold text-slate-900">
              Điều Chỉnh Đánh Giá Suy Luận (Teacher Override)
            </h2>
          </div>
          <button
            ref={closeButtonRef}
            type="button"
            aria-label="Đóng hộp thoại điều chỉnh đánh giá"
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
            <div className="flex items-center gap-2">
              <span className="text-xs text-slate-500">Mã lượt làm: #{review.attemptId}</span>
              <span className="text-xs font-mono text-slate-500">· Phiên bản OCC: {formatOccVersionLabel(overrideVersion)}</span>
            </div>
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
                {review.analysisConfidence !== null && review.analysisConfidence !== undefined
                  ? `${review.analysisConfidence.toFixed(0)}%`
                  : "Không có"}
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

        {/* Concurrency Conflict Banner (409) */}
        {isConflict && conflictDetails && (
          <div role="alert" aria-live="assertive" className="mt-4 rounded-xl border border-amber-300 bg-amber-50 p-4 text-sm text-amber-900 flex items-start gap-3">
            <span className="text-xl leading-none">⚠️</span>
            <div className="flex-1">
              <p className="font-bold">Xung đột phiên bản dữ liệu đồng thời (409 Conflict)</p>
              <p className="mt-1 text-xs text-amber-800 leading-relaxed">
                {conflictDetails.message}
                {conflictDetails.traceId && (
                  <span className="block mt-1 font-mono text-[11px] text-amber-700">
                    W3C Trace ID: {conflictDetails.traceId}
                  </span>
                )}
              </p>
              {onRefetch && (
                <div className="mt-3">
                  <button
                    type="button"
                    onClick={handleRefetch}
                    disabled={isRefetching}
                    className="inline-flex items-center gap-1.5 rounded-lg bg-amber-700 px-3.5 py-1.5 text-xs font-bold text-white shadow-sm hover:bg-amber-600 disabled:opacity-50 transition-colors"
                  >
                    {isRefetching ? "Đang đồng bộ dữ liệu..." : "Tải lại dữ liệu mới nhất (Refetch)"}
                  </button>
                </div>
              )}
            </div>
          </div>
        )}

        {/* Missing / Invalid OCC Token Warning (Fail-Closed) */}
        {overrideVersion === null && !isConflict && (
          <div role="alert" aria-live="assertive" className="mt-4 rounded-xl border border-rose-300 bg-rose-50 p-4 text-sm text-rose-900 flex items-start gap-3">
            <span className="text-xl leading-none">🚫</span>
            <div className="flex-1">
              <p className="font-bold">Thiếu mã phiên bản kiểm soát đồng thời (analysisOverrideVersion)</p>
              <p className="mt-1 text-xs text-rose-800 leading-relaxed">
                Bản ghi không chứa phiên bản phân tích hợp lệ. Biểu mẫu can thiệp tạm thời bị khóa an toàn (Fail-Closed) để tránh ghi đè sai lệch dữ liệu.
              </p>
              {onRefetch && (
                <div className="mt-3">
                  <button
                    type="button"
                    onClick={handleRefetch}
                    disabled={isRefetching}
                    className="inline-flex items-center gap-1.5 rounded-lg bg-rose-700 px-3.5 py-1.5 text-xs font-bold text-white shadow-sm hover:bg-rose-600 disabled:opacity-50 transition-colors"
                  >
                    {isRefetching ? "Đang tải lại..." : "Tải lại dữ liệu (Refetch)"}
                  </button>
                </div>
              )}
            </div>
          </div>
        )}

        {/* Feedback / error alerts */}
        {errorMessage && !isConflict && (
          <div role="alert" aria-live="assertive" className="mt-4 rounded-lg bg-red-50 p-3 text-sm font-medium text-red-800 ring-1 ring-red-200">
            {errorMessage}
          </div>
        )}

        {successInfo && (
          <div role="status" aria-live="polite" className="mt-4 rounded-lg bg-emerald-50 p-3 text-sm font-medium text-emerald-800 ring-1 ring-emerald-200">
            {successInfo}
          </div>
        )}

        {/* Adjustment Form */}
        <form onSubmit={handleSubmit} className="mt-6 space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {/* Correctness */}
            <fieldset>
              <legend className="block text-xs font-bold uppercase tracking-wider text-slate-700 mb-1">
                Kết quả bài giải
              </legend>
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
            </fieldset>

            {/* Error Type */}
            <div>
              <label htmlFor="override-error-type" className="block text-xs font-bold uppercase tracking-wider text-slate-700 mb-1">
                Phân loại lỗi (Error Type)
              </label>
              <select
                id="override-error-type"
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
              <label htmlFor="override-reasoning-quality" className="text-xs font-bold uppercase tracking-wider text-slate-700">
                Chất lượng suy luận (Reasoning Quality)
              </label>
              <span className="text-sm font-bold text-indigo-600">{reasoningQuality}/100</span>
            </div>
            <input
              id="override-reasoning-quality"
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
            <label htmlFor="override-feedback" className="block text-xs font-bold uppercase tracking-wider text-slate-700 mb-1">
              Nhận xét của Giáo viên (Gửi tới học sinh)
            </label>
            <textarea
              id="override-feedback"
              rows={2}
              value={feedback}
              onChange={(e) => setFeedback(e.target.value)}
              placeholder="Nhập nhận xét chi tiết giúp học sinh cải thiện phương pháp giải..."
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
            />
          </div>

          {/* Override Reason (Mandatory) */}
          <div>
            <label htmlFor="override-reason" className="block text-xs font-bold uppercase tracking-wider text-slate-700 mb-1">
              Lý do can thiệp / điều chỉnh <span className="text-red-500">* (Bắt buộc kiểm toán)</span>
            </label>
            <input
              id="override-reason"
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
              disabled={isSubmitting || overrideVersion === null || !isValidOccVersion(overrideVersion) || isConflict}
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
