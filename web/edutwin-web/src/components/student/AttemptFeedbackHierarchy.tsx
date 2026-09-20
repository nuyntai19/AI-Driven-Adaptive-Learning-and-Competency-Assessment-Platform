import { useState, useEffect } from "react";
import type { AttemptFeedbackDataDto } from "../../types/learning";
import { RichMathText } from "../math/RichMathText";
import { retryAttemptAIAnalysis, createStudentReviewRequest } from "../../api/learningFeedbackApi";

interface AttemptFeedbackHierarchyProps {
  feedbackData: AttemptFeedbackDataDto;
  onRefreshFeedback: () => Promise<void>;
  onPollJob?: (jobId: string) => void;
}

export function AttemptFeedbackHierarchy({
  feedbackData,
  onRefreshFeedback,
  onPollJob,
}: AttemptFeedbackHierarchyProps) {
  const {
    attemptId,
    grading,
    studentSubmission,
    analysis,
    teacherSolution,
    teacherFinalEvaluation,
    reviewRequest,
    retryQuota,
  } = feedbackData;

  // Manual Retry state & countdown
  const [isRetryingAI, setIsRetryingAI] = useState(false);
  const [retryError, setRetryError] = useState<string | null>(null);
  const [cooldownSeconds, setCooldownSeconds] = useState<number>(
    retryQuota?.cooldownRemainingSeconds ?? 0
  );

  // Review request modal state
  const [isReviewModalOpen, setIsReviewModalOpen] = useState(false);
  const [reviewReason, setReviewReason] = useState("");
  const [isSubmittingReview, setIsSubmittingReview] = useState(false);
  const [reviewModalError, setReviewModalError] = useState<string | null>(null);
  const [reviewSuccessMessage, setReviewSuccessMessage] = useState<string | null>(null);

  // Sync cooldown timer
  useEffect(() => {
    if (retryQuota?.cooldownRemainingSeconds) {
      setCooldownSeconds(retryQuota.cooldownRemainingSeconds);
    }
  }, [retryQuota?.cooldownRemainingSeconds]);

  // Tick down cooldown timer
  useEffect(() => {
    if (cooldownSeconds <= 0) return;
    const timer = setInterval(() => {
      setCooldownSeconds((prev) => (prev > 0 ? prev - 1 : 0));
    }, 1000);
    return () => clearInterval(timer);
  }, [cooldownSeconds]);

  const handleRetryAI = async () => {
    if (cooldownSeconds > 0 || !retryQuota?.canRetry) return;
    try {
      setIsRetryingAI(true);
      setRetryError(null);
      const res = await retryAttemptAIAnalysis(attemptId);
      if (res.analysisJobId && onPollJob) {
        onPollJob(res.analysisJobId);
      } else {
        await onRefreshFeedback();
      }
    } catch (err: any) {
      const msg =
        err?.response?.data?.detail ||
        err?.response?.data?.message ||
        err?.message ||
        "Không thể kích hoạt chấm lại AI. Vui lòng thử lại sau.";
      setRetryError(msg);
    } finally {
      setIsRetryingAI(false);
    }
  };

  const handleSubmitReviewRequest = async () => {
    if (!reviewReason.trim() || reviewReason.trim().length < 10) {
      setReviewModalError("Vui lòng nhập lý do cụ thể (tối thiểu 10 ký tự).");
      return;
    }

    try {
      setIsSubmittingReview(true);
      setReviewModalError(null);
      await createStudentReviewRequest(attemptId, { reason: reviewReason.trim() });
      setReviewSuccessMessage("Đã gửi yêu cầu xem xét tới giáo viên phụ trách!");
      setTimeout(() => {
        setIsReviewModalOpen(false);
        setReviewSuccessMessage(null);
        setReviewReason("");
      }, 1500);
      await onRefreshFeedback();
    } catch (err: any) {
      const msg =
        err?.response?.data?.detail ||
        err?.response?.data?.message ||
        err?.message ||
        "Không thể gửi yêu cầu xem xét. Vui lòng thử lại sau.";
      setReviewModalError(msg);
    } finally {
      setIsSubmittingReview(false);
    }
  };

  const isDegradedOrFallback = !analysis || analysis.isFallback || feedbackData.status === "PendingAnalysis";

  return (
    <div className="space-y-6">
      {/* TIER 1: BÀI LÀM CỦA HỌC SINH (Student Work) */}
      <div className="rounded-3xl bg-white dark:bg-[#0f172a] p-6 sm:p-7 shadow-xs border border-slate-200/80 dark:border-slate-800 space-y-4">
        <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
          <div className="flex items-center gap-2.5">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-blue-500/10 text-blue-600 dark:text-blue-400 font-black text-xs">
              1
            </span>
            <h3 className="text-base font-extrabold text-slate-900 dark:text-white">
              Bài Làm Của Bạn (Student Submission)
            </h3>
          </div>
          <span className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">
            Lượt nộp gốc
          </span>
        </div>

        <div className="space-y-3">
          <div>
            <span className="text-xs font-semibold text-slate-400 block mb-1">Đáp án đã chọn / đã nộp:</span>
            <div className="rounded-xl bg-slate-50 dark:bg-slate-800/60 p-3.5 border border-slate-100 dark:border-slate-700/60">
              <p className="font-bold text-slate-900 dark:text-white text-base">
                {studentSubmission?.finalAnswer || "Chưa có đáp án"}
              </p>
            </div>
          </div>

          {studentSubmission?.reasoningText && (
            <div>
              <span className="text-xs font-semibold text-slate-400 block mb-1">Các bước lập luận / giải trình:</span>
              <div className="rounded-xl bg-slate-50 dark:bg-slate-800/60 p-3.5 border border-slate-100 dark:border-slate-700/60 font-mono text-xs leading-relaxed text-slate-800 dark:text-slate-200">
                <RichMathText content={studentSubmission.reasoningText} />
              </div>
            </div>
          )}

          {studentSubmission?.attachmentUrl && (
            <div>
              <span className="text-xs font-semibold text-slate-400 block mb-1">Bản vẽ nháp đính kèm:</span>
              <div className="rounded-xl border border-slate-200 dark:border-slate-700 overflow-hidden bg-slate-900 max-w-sm">
                <img
                  src={studentSubmission.attachmentUrl}
                  alt="Bản nháp của học sinh"
                  className="w-full h-auto object-contain max-h-48"
                />
              </div>
            </div>
          )}

          {studentSubmission && (
            <div className="flex flex-wrap items-center gap-4 text-xs text-slate-500 dark:text-slate-400 pt-1">
              <span>⏱ Thời gian làm: <strong>{studentSubmission.timeSpentSeconds}s</strong></span>
              <span>🔄 Số lần đổi đáp án: <strong>{studentSubmission.answerChanges}</strong></span>
              <span>🎯 Mức tự tin: <strong>{Math.round(studentSubmission.confidence * 100)}%</strong></span>
            </div>
          )}
        </div>
      </div>

      {/* TIER 2: AI PHÂN TÍCH & ĐÁNH GIÁ (AI Analysis) */}
      <div className="rounded-3xl bg-white dark:bg-[#0f172a] p-6 sm:p-7 shadow-xs border border-slate-200/80 dark:border-slate-800 space-y-4">
        <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
          <div className="flex items-center gap-2.5">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 font-black text-xs">
              2
            </span>
            <h3 className="text-base font-extrabold text-slate-900 dark:text-white">
              AI Phân Tích & Chẩn Đoán Tư Duy (AI Reasoning)
            </h3>
          </div>
          {analysis?.qualityBand && (
            <span
              className={`rounded-full px-3 py-1 text-xs font-bold ${
                analysis.qualityBand === "Good"
                  ? "bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300"
                  : analysis.qualityBand === "Acceptable"
                  ? "bg-blue-100 dark:bg-blue-950/60 text-blue-800 dark:text-blue-300"
                  : analysis.qualityBand === "NeedsImprovement"
                  ? "bg-amber-100 dark:bg-amber-950/60 text-amber-800 dark:text-amber-300"
                  : "bg-red-100 dark:bg-red-950/60 text-red-800 dark:text-red-300"
              }`}
            >
              Bậc tư duy: {analysis.qualityBand}
            </span>
          )}
        </div>

        {/* Graceful Degradation / Fallback Notice */}
        {isDegradedOrFallback && (
          <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-xs text-amber-900 dark:text-amber-200 space-y-2">
            <div className="flex items-center gap-2 font-bold">
              <span>ℹ</span>
              <span>Đánh giá tự động dự phòng (Graceful Degradation)</span>
            </div>
            <p className="leading-relaxed">
              Hệ thống đã lưu bài làm và chấm điểm đáp án chuẩn xác. Phân tích tư duy chuyên sâu bằng AI đang chờ xử lý hoặc chưa đạt độ tin cậy tối ưu. Bạn có thể yêu cầu AI phân tích lại hoặc gửi yêu cầu tới giáo viên để được xem xét trực tiếp.
            </p>
          </div>
        )}

        {analysis && (
          <div className="space-y-4">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              {analysis.methodDetected && (
                <div className="rounded-2xl bg-slate-50 dark:bg-slate-800/60 p-4 border border-slate-100 dark:border-slate-700/60">
                  <p className="text-xs font-medium text-slate-400">Phương pháp nhận diện</p>
                  <p className="mt-1 text-sm font-bold text-slate-900 dark:text-white">{analysis.methodDetected}</p>
                </div>
              )}
              {analysis.reasoningQuality !== null && analysis.reasoningQuality !== undefined && (
                <div className="rounded-2xl bg-slate-50 dark:bg-slate-800/60 p-4 border border-slate-100 dark:border-slate-700/60">
                  <p className="text-xs font-medium text-slate-400">Chất lượng lập luận</p>
                  <p className="mt-1 text-sm font-black text-indigo-600 dark:text-indigo-400">
                    {analysis.reasoningQuality} / 100 điểm
                  </p>
                </div>
              )}
            </div>

            <div className="rounded-2xl bg-indigo-50/70 dark:bg-indigo-950/50 p-4 border border-indigo-200/60 dark:border-indigo-800">
              <p className="text-xs font-extrabold text-indigo-900 dark:text-indigo-300 uppercase tracking-wider">
                Nhận xét từ AI
              </p>
              <p className="mt-1 text-sm text-indigo-950 dark:text-indigo-200 leading-relaxed font-medium">
                {analysis.feedback}
              </p>
            </div>

            {analysis.missingSteps && analysis.missingSteps.length > 0 && (
              <div>
                <p className="text-xs font-bold text-slate-700 dark:text-slate-300 mb-1.5">
                  Các bước còn thiếu hoặc cần bổ sung:
                </p>
                <ul className="list-inside list-disc space-y-1 text-sm text-slate-600 dark:text-slate-400">
                  {analysis.missingSteps.map((step, idx) => (
                    <li key={idx}>{step}</li>
                  ))}
                </ul>
              </div>
            )}

            {analysis.misconception && (
              <div className="rounded-2xl bg-rose-50 dark:bg-rose-950/40 p-4 text-xs text-rose-800 dark:text-rose-300 border border-rose-200 dark:border-rose-800">
                <span className="font-bold">Quan niệm sai lầm nhận diện: </span>
                {analysis.misconception}
              </div>
            )}
          </div>
        )}

        {/* Existing Student Review Request Status Card */}
        {reviewRequest && (
          <div className="rounded-2xl border border-purple-500/30 bg-purple-500/10 p-4 space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-purple-300 uppercase tracking-wider flex items-center gap-1.5">
                <span>🙋</span> Yêu cầu xem xét của bạn
              </span>
              <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full uppercase border ${
                reviewRequest.status === "Pending"
                  ? "bg-amber-500/20 text-amber-300 border-amber-500/40"
                  : reviewRequest.status === "Resolved"
                  ? "bg-emerald-500/20 text-emerald-300 border-emerald-500/40"
                  : "bg-rose-500/20 text-rose-300 border-rose-500/40"
              }`}>
                {reviewRequest.status === "Pending" ? "Đang chờ giáo viên duyệt" : reviewRequest.status === "Resolved" ? "Đã giải quyết" : "Từ chối"}
              </span>
            </div>
            <p className="text-xs text-purple-200 italic">
              "{reviewRequest.studentComment}"
            </p>
            {reviewRequest.teacherNote && (
              <div className="pt-2 border-t border-purple-500/20 text-xs text-purple-100">
                <span className="font-semibold text-purple-300">Phản hồi từ giáo viên: </span>
                {reviewRequest.teacherNote}
              </div>
            )}
          </div>
        )}

        {/* Action Buttons: Retry AI & Review Request */}
        <div className="flex flex-wrap items-center justify-between gap-3 pt-2 border-t border-slate-100 dark:border-slate-800">
          <div className="flex items-center gap-2">
            {retryQuota && (
              <button
                type="button"
                disabled={!retryQuota.canRetry || cooldownSeconds > 0 || isRetryingAI}
                onClick={handleRetryAI}
                className="inline-flex items-center gap-2 px-3.5 py-2 rounded-xl text-xs font-bold border transition-colors disabled:opacity-50 disabled:cursor-not-allowed bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200 border-slate-200 dark:border-slate-700"
              >
                <span>{isRetryingAI ? "⏳" : "🔄"}</span>
                <span>
                  {isRetryingAI
                    ? "Đang gửi chấm lại..."
                    : cooldownSeconds > 0
                    ? `Chờ ${cooldownSeconds}s để thử lại`
                    : `Chấm lại AI (${retryQuota.manualRetriesRemaining}/${retryQuota.manualRetriesRemaining + retryQuota.manualRetriesUsed})`}
                </span>
              </button>
            )}

            {retryError && (
              <span className="text-xs text-rose-500 dark:text-rose-400">{retryError}</span>
            )}
          </div>

          {!reviewRequest && (
            <button
              type="button"
              onClick={() => setIsReviewModalOpen(true)}
              className="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-xl text-xs font-bold border border-purple-500/40 text-purple-600 dark:text-purple-300 hover:bg-purple-500/10 transition-colors cursor-pointer"
            >
              <span>🙋</span> Yêu cầu xem xét kết quả AI
            </button>
          )}
        </div>
      </div>

      {/* TIER 3: ĐÁP ÁN & LỜI GIẢI CỦA GIÁO VIÊN (Teacher Solution) */}
      {teacherSolution && (
        <div className="rounded-3xl bg-white dark:bg-[#0f172a] p-6 sm:p-7 shadow-xs border border-emerald-500/30 dark:border-emerald-500/20 space-y-4">
          <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
            <div className="flex items-center gap-2.5">
              <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 font-black text-xs">
                3
              </span>
              <h3 className="text-base font-extrabold text-slate-900 dark:text-white">
                Đáp Án & Lời Giải Chuẩn Của Giáo Viên (Teacher Solution)
              </h3>
            </div>
            <span className="text-[11px] font-semibold text-emerald-600 dark:text-emerald-400 uppercase tracking-wider">
              Tài liệu tham khảo chính thức
            </span>
          </div>

          <div className="space-y-4">
            <div>
              <span className="text-xs font-semibold text-slate-400 block mb-1">Đáp án chính xác:</span>
              <div className="rounded-xl bg-emerald-500/10 border border-emerald-500/30 p-3.5 text-emerald-900 dark:text-emerald-200 font-mono font-bold text-sm">
                <RichMathText content={teacherSolution.correctAnswer} />
              </div>
            </div>

            {teacherSolution.solution && (
              <div>
                <span className="text-xs font-semibold text-slate-400 block mb-1">Lời giải chi tiết từng bước:</span>
                <div className="rounded-xl bg-slate-50 dark:bg-slate-800/60 p-4 border border-slate-100 dark:border-slate-700/60 leading-relaxed text-slate-800 dark:text-slate-200 text-sm">
                  <RichMathText content={teacherSolution.solution} />
                </div>
              </div>
            )}

            {teacherSolution.expectedReasoning && (
              <div>
                <span className="text-xs font-semibold text-slate-400 block mb-1">Tư duy lập luận kỳ vọng:</span>
                <div className="rounded-xl bg-slate-50 dark:bg-slate-800/60 p-3.5 border border-slate-100 dark:border-slate-700/60 text-xs text-slate-700 dark:text-slate-300 leading-relaxed">
                  <RichMathText content={teacherSolution.expectedReasoning} />
                </div>
              </div>
            )}

            {teacherSolution.gradingCriteria && (
              <div className="rounded-2xl border border-slate-200 dark:border-slate-700 p-4 bg-slate-50/50 dark:bg-slate-800/40 space-y-3">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-700 dark:text-slate-300 block">
                  Tiêu chí chấm & Ý tưởng cốt lõi
                </span>

                {teacherSolution.gradingCriteria.scoringNotes && (
                  <p className="text-xs text-slate-600 dark:text-slate-400">
                    {teacherSolution.gradingCriteria.scoringNotes}
                  </p>
                )}

                {teacherSolution.gradingCriteria.requiredIdeas?.length > 0 && (
                  <div>
                    <span className="text-[11px] font-semibold text-slate-500 dark:text-slate-400 block mb-1">Ý tưởng bắt buộc:</span>
                    <ul className="list-disc list-inside text-xs text-slate-600 dark:text-slate-300 space-y-0.5">
                      {teacherSolution.gradingCriteria.requiredIdeas.map((idea, i) => (
                        <li key={i}>{idea}</li>
                      ))}
                    </ul>
                  </div>
                )}

                {teacherSolution.gradingCriteria.commonErrors?.length > 0 && (
                  <div>
                    <span className="text-[11px] font-semibold text-rose-500 dark:text-rose-400 block mb-1">Sai lầm phổ biến cần tránh:</span>
                    <ul className="list-disc list-inside text-xs text-rose-600 dark:text-rose-300 space-y-0.5">
                      {teacherSolution.gradingCriteria.commonErrors.map((err, i) => (
                        <li key={i}>{err}</li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      )}

      {/* TIER 4: ĐÁNH GIÁ CHÍNH THỨC CỦA GIÁO VIÊN (Teacher Final Evaluation) */}
      {teacherFinalEvaluation?.hasTeacherOverride && (
        <div className="rounded-3xl bg-white dark:bg-[#0f172a] p-6 sm:p-7 shadow-xs border border-purple-500/40 bg-purple-500/5 space-y-4">
          <div className="flex items-center justify-between border-b border-purple-500/20 pb-3">
            <div className="flex items-center gap-2.5">
              <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-purple-500/20 text-purple-600 dark:text-purple-300 font-black text-xs">
                4
              </span>
              <h3 className="text-base font-extrabold text-slate-900 dark:text-white">
                Đánh Giá Chính Thức Của Giáo Viên (Teacher Final Evaluation)
              </h3>
            </div>
            <span className="text-[11px] font-bold text-purple-600 dark:text-purple-300 uppercase tracking-wider px-2 py-0.5 rounded-full bg-purple-500/10 border border-purple-500/30">
              Kết quả chấm đè chính thức
            </span>
          </div>

          <div className="space-y-3">
            <div className="flex items-center justify-between p-3.5 rounded-xl bg-purple-500/10 border border-purple-500/20">
              <div>
                <span className="text-xs text-purple-300 block">Điểm số sau khi giáo viên duyệt:</span>
                <span className="text-xl font-black text-white">
                  {teacherFinalEvaluation.teacherScore ?? grading.awardedScore} / {grading.maxScore}
                </span>
              </div>
              {teacherFinalEvaluation.reviewedByTeacherName && (
                <div className="text-right">
                  <span className="text-xs text-purple-300 block">Giáo viên đánh giá:</span>
                  <span className="text-sm font-bold text-white">{teacherFinalEvaluation.reviewedByTeacherName}</span>
                </div>
              )}
            </div>

            {teacherFinalEvaluation.teacherFeedback && (
              <div className="rounded-xl bg-slate-50 dark:bg-slate-800/60 p-4 border border-slate-200 dark:border-slate-700">
                <span className="text-xs font-bold text-slate-400 block mb-1 uppercase tracking-wider">
                  Nhận xét trực tiếp từ giáo viên:
                </span>
                <p className="text-sm text-slate-900 dark:text-slate-100 leading-relaxed font-medium">
                  {teacherFinalEvaluation.teacherFeedback}
                </p>
              </div>
            )}
          </div>
        </div>
      )}

      {/* STUDENT REVIEW REQUEST MODAL */}
      {isReviewModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
          <div className="w-full max-w-lg rounded-3xl bg-white dark:bg-[#0f172a] border border-slate-200 dark:border-slate-800 p-6 sm:p-7 shadow-2xl space-y-5">
            <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
              <h3 className="text-base font-extrabold text-slate-900 dark:text-white flex items-center gap-2">
                <span>🙋</span> Yêu Cầu Xem Xét Kết Quả AI
              </h3>
              <button
                type="button"
                onClick={() => setIsReviewModalOpen(false)}
                className="text-slate-400 hover:text-slate-200 text-lg"
              >
                ✕
              </button>
            </div>

            <div className="space-y-3">
              <p className="text-xs text-slate-600 dark:text-slate-400 leading-relaxed">
                Nếu bạn cảm thấy AI hiểu nhầm phương pháp giải của bạn, đánh giá sót bước đúng, hoặc bài làm của bạn dùng cách giải khác hợp lệ, vui lòng mô tả chi tiết để thầy/cô xem xét lại.
              </p>

              <div>
                <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                  Lý do / Giải thích cụ thể của bạn <span className="text-rose-400">*</span>:
                </label>
                <textarea
                  rows={4}
                  value={reviewReason}
                  onChange={(e) => setReviewReason(e.target.value)}
                  placeholder="Ví dụ: Em sử dụng phương pháp đồ thị thay vì biến đổi đại số, các bước biến đổi của em hoàn toàn chính xác nhưng AI báo thiếu bước..."
                  className="w-full rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/60 p-3 text-xs text-slate-900 dark:text-slate-100 focus:outline-hidden focus:border-indigo-500"
                />
                <span className="text-[11px] text-slate-400 mt-1 block">
                  Tối thiểu 10 ký tự ({reviewReason.trim().length}/500)
                </span>
              </div>

              {reviewModalError && (
                <div className="p-3 rounded-xl bg-rose-500/10 border border-rose-500/30 text-rose-300 text-xs">
                  {reviewModalError}
                </div>
              )}

              {reviewSuccessMessage && (
                <div className="p-3 rounded-xl bg-emerald-500/10 border border-emerald-500/30 text-emerald-300 text-xs font-semibold">
                  ✓ {reviewSuccessMessage}
                </div>
              )}
            </div>

            <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
              <button
                type="button"
                onClick={() => setIsReviewModalOpen(false)}
                className="px-4 py-2 text-xs font-bold rounded-xl border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800"
              >
                Hủy
              </button>
              <button
                type="button"
                disabled={isSubmittingReview || reviewReason.trim().length < 10}
                onClick={handleSubmitReviewRequest}
                className="px-5 py-2 text-xs font-bold rounded-xl bg-indigo-600 hover:bg-indigo-500 text-white disabled:opacity-50 flex items-center gap-2"
              >
                {isSubmittingReview ? "Đang gửi..." : "Gửi yêu cầu tới giáo viên"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
