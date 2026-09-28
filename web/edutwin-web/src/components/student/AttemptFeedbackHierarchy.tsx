import { useState, useEffect } from "react";
import type { AttemptFeedbackDataDto } from "../../types/learning";
import { RichMathText } from "../math/RichMathText";
import { retryAttemptAIAnalysis, createStudentReviewRequest } from "../../api/learningFeedbackApi";
import { extractProblemDetails } from "../../utils/problemDetails";

function safeClientErrorMessage(error: unknown, fallback: string): string {
  const details = extractProblemDetails(error);
  if (details.status === 401) return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
  if (details.status === 403) return "Bạn không có quyền yêu cầu xem xét bài làm này.";
  if (details.status === 404) return "Không tìm thấy bài làm cần xem xét.";
  if (details.status === 409) return "Yêu cầu xem xét này đã được gửi trước đó.";
  if (details.status === 429) return "Bạn thao tác quá nhanh. Vui lòng chờ một lúc rồi thử lại.";
  if (details.status && details.status >= 500) {
    return details.traceId ? `${fallback} (Mã theo dõi: ${details.traceId})` : fallback;
  }
  return details.detail?.trim() || fallback;
}

interface AttemptFeedbackHierarchyProps {
  feedbackData: AttemptFeedbackDataDto;
  onRefreshFeedback: () => Promise<void>;
  onPollJob?: (jobId: string) => void;
  showStudentSubmission?: boolean;
  scoreAndFeedbackOnly?: boolean;
  answerOptions?: Array<{ optionId: string; label: string; text: string }>;
  assignmentQuestionCount?: number;
}

export function AttemptFeedbackHierarchy({
  feedbackData,
  onRefreshFeedback,
  onPollJob,
  showStudentSubmission = true,
  scoreAndFeedbackOnly = false,
  answerOptions = [],
  assignmentQuestionCount,
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

  // Dispute Question State
  const [isDisputeModalOpen, setIsDisputeModalOpen] = useState(false);
  const [disputeCategory, setDisputeCategory] = useState("DEFECTIVE_QUESTION");
  const [disputeComment, setDisputeComment] = useState("");
  const [isSubmittingDispute, setIsSubmittingDispute] = useState(false);
  const [disputeModalError, setDisputeModalError] = useState<string | null>(null);
  const [disputeSuccessMessage, setDisputeSuccessMessage] = useState<string | null>(null);

  // Teacher solution accordion state (collapsed by default)
  const [isTeacherSolutionOpen, setIsTeacherSolutionOpen] = useState(false);

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
    } catch (error: unknown) {
      setRetryError(
        safeClientErrorMessage(error, "Không thể kích hoạt chấm lại AI. Vui lòng thử lại sau."),
      );
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
      await createStudentReviewRequest(attemptId, { studentComment: reviewReason.trim() });
      setReviewSuccessMessage("Đã gửi yêu cầu xem xét tới giáo viên phụ trách!");
      setTimeout(() => {
        setIsReviewModalOpen(false);
        setReviewSuccessMessage(null);
        setReviewReason("");
      }, 1500);
      await onRefreshFeedback();
    } catch (error: unknown) {
      setReviewModalError(
        safeClientErrorMessage(error, "Không thể gửi yêu cầu xem xét. Vui lòng thử lại sau."),
      );
    } finally {
      setIsSubmittingReview(false);
    }
  };

  const handleSubmitDispute = async () => {
    if (!disputeComment.trim() || disputeComment.trim().length < 10) {
      setDisputeModalError("Vui lòng nhập mô tả sự cố cụ thể (tối thiểu 10 ký tự).");
      return;
    }

    try {
      setIsSubmittingDispute(true);
      setDisputeModalError(null);
      await createStudentReviewRequest(attemptId, {
        studentComment: `[${disputeCategory}] ${disputeComment.trim()}`,
        disputeCategory,
      });
      setDisputeSuccessMessage("Đã gửi báo cáo sự cố đề bài tới giáo viên phụ trách!");
      setTimeout(() => {
        setIsDisputeModalOpen(false);
        setDisputeSuccessMessage(null);
        setDisputeComment("");
      }, 1500);
      await onRefreshFeedback();
    } catch (error: unknown) {
      setDisputeModalError(
        safeClientErrorMessage(error, "Không thể gửi báo cáo sự cố. Vui lòng thử lại sau."),
      );
    } finally {
      setIsSubmittingDispute(false);
    }
  };

  const isPending = feedbackData.status === "PendingAnalysis" || feedbackData.status === "Processing";
  const isUnavailable = !analysis && !isPending;
  const isDegradedOrFallback = !analysis || analysis.isFallback || isPending;
  const displayedMaxScore = assignmentQuestionCount && assignmentQuestionCount > 0
    ? Math.round((10 / assignmentQuestionCount) * 100) / 100
    : grading.maxScore;
  const toDisplayedScore = (score?: number | null) => {
    if (score === null || score === undefined) return null;
    if (!assignmentQuestionCount || assignmentQuestionCount <= 0 || grading.maxScore <= 0) return score;
    return Math.round((score / grading.maxScore) * displayedMaxScore * 100) / 100;
  };
  const displayedAwardedScore = toDisplayedScore(grading.awardedScore);
  const formatAnswer = (answer: string) => {
    const option = answerOptions.find(
      (candidate) => candidate.optionId === answer || candidate.label === answer
    );
    return option ? `${option.label}. ${option.text}` : answer;
  };

  const formatQualityBand = (band?: string | null): string => {
    switch (band) {
      case "Good":
        return "Tốt";
      case "Acceptable":
        return "Đạt";
      case "NeedsImprovement":
        return "Cần cải thiện";
      case "Poor":
        return "Chưa đạt";
      default:
        return band || "Chưa xếp loại";
    }
  };

  const formatSolutionTypeBadge = (type?: string | null): string => {
    switch (type) {
      case "REFINED":
        return "Lời giải tối ưu / Hoàn thiện";
      case "CORRECTED":
        return "Lời giải sửa sai từng bước";
      case "GENERATED":
        return "Lời giải thích nghi từ AI";
      case "MODEL_ANSWER":
        return "Lời giải mẫu";
      default:
        return "Lời giải đề xuất";
    }
  };

  return (
    <div className="space-y-6">
      {/* TIER 1: BÀI LÀM CỦA HỌC SINH (Student Work) */}
      {showStudentSubmission && <div className="rounded-3xl bg-white dark:bg-[#0f172a] p-6 sm:p-7 shadow-xs border border-slate-200/80 dark:border-slate-800 space-y-4">
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
                {studentSubmission?.finalAnswer ? formatAnswer(studentSubmission.finalAnswer) : "Chưa có đáp án"}
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
              <span>🎯 Mức tự tin: <strong>{Math.round(studentSubmission.confidence)}%</strong></span>
            </div>
          )}
        </div>
      </div>}

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
              Bậc tư duy: {formatQualityBand(analysis.qualityBand)}
            </span>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <span className={`rounded-full px-3 py-1 text-xs font-bold ${
            grading.isCorrect === true
              ? "bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300"
              : grading.isCorrect === false
              ? "bg-rose-100 dark:bg-rose-950/60 text-rose-800 dark:text-rose-300"
              : "bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300"
          }`}>
            {grading.isCorrect === true
              ? "Kết quả: Đúng"
              : grading.isCorrect === false
              ? "Kết quả: Chưa đúng"
              : "Kết quả: Chờ đánh giá"}
          </span>
          <span className="rounded-full bg-slate-100 dark:bg-slate-800 px-3 py-1 text-xs font-bold text-slate-700 dark:text-slate-300">
            Điểm: {displayedAwardedScore ?? "Chưa chấm"} / {displayedMaxScore}
          </span>
        </div>

        {/* Graceful Degradation / Fallback Notice */}
        {isDegradedOrFallback && (
          <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-xs text-amber-900 dark:text-amber-200 space-y-2">
            <div className="flex items-center gap-2 font-bold">
              <span>ℹ</span>
              <span>{isPending ? "AI đang phân tích câu trả lời" : "AI hiện đang tạm thời không khả dụng"}</span>
            </div>
            <p className="leading-relaxed">
              {isPending
                ? scoreAndFeedbackOnly
                  ? "Bài làm của bạn đã được lưu. Điểm số và nhận xét của câu này đang được xử lý."
                  : "Bài làm của bạn đã được lưu. Kết quả phân tích của riêng câu này đang được xử lý; đáp án và lời giải giáo viên vẫn hiển thị bên dưới."
                : isUnavailable
                ? scoreAndFeedbackOnly
                  ? "AI hiện đang tạm thời không khả dụng. Bài làm của bạn đã được lưu để giáo viên xem xét."
                  : "AI hiện đang tạm thời không khả dụng. Bài làm của bạn đã được lưu. Đáp án và lời giải giáo viên vẫn hiển thị bên dưới."
                : scoreAndFeedbackOnly
                  ? "AI đã trả về kết quả dự phòng hoặc cần giáo viên xem xét. Bài làm của bạn đã được lưu."
                  : "AI đã trả về kết quả dự phòng hoặc cần giáo viên xem xét. Bài làm của bạn đã được lưu; đáp án và lời giải giáo viên vẫn hiển thị bên dưới."}
            </p>
          </div>
        )}

        {analysis && (
          <div className="space-y-4">
            {!scoreAndFeedbackOnly && <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
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
            </div>}

            <div className="rounded-2xl bg-indigo-50/70 dark:bg-indigo-950/50 p-4 border border-indigo-200/60 dark:border-indigo-800">
              <p className="text-xs font-extrabold text-indigo-900 dark:text-indigo-300 uppercase tracking-wider">
                Nhận xét từ AI
              </p>
              <div className="mt-1 text-sm text-indigo-950 dark:text-indigo-200 leading-relaxed font-medium">
                <RichMathText content={analysis.feedback} />
              </div>
            </div>

            {!scoreAndFeedbackOnly && analysis.missingSteps && analysis.missingSteps.length > 0 && (
              <div>
                <p className="text-xs font-bold text-slate-700 dark:text-slate-300 mb-1.5">
                  Các bước còn thiếu hoặc cần bổ sung:
                </p>
                <ul className="list-inside list-disc space-y-1 text-sm text-slate-600 dark:text-slate-400">
                  {analysis.missingSteps.map((step, idx) => (
                    <li key={idx}>
                      <RichMathText content={step} />
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {!scoreAndFeedbackOnly && analysis.misconception && (
              <div className="rounded-2xl bg-rose-50 dark:bg-rose-950/40 p-4 text-xs text-rose-800 dark:text-rose-300 border border-rose-200 dark:border-rose-800">
                <span className="font-bold">Quan niệm sai lầm nhận diện: </span>
                <RichMathText content={analysis.misconception} />
              </div>
            )}

            {!scoreAndFeedbackOnly && analysis.aiSolution && (
              <div className="rounded-2xl bg-indigo-50/70 dark:bg-indigo-950/40 p-5 border border-indigo-200 dark:border-indigo-800 space-y-2.5">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <span className="text-sm">💡</span>
                    <span className="text-xs font-black uppercase tracking-wider text-indigo-950 dark:text-indigo-300">
                      Lời giải đề xuất từ AI (AI Solution)
                    </span>
                  </div>
                  {analysis.solutionType && (
                    <span className="text-[11px] font-bold px-2.5 py-0.5 rounded-lg bg-indigo-100 dark:bg-indigo-900/60 text-indigo-800 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-700">
                      {formatSolutionTypeBadge(analysis.solutionType)}
                    </span>
                  )}
                </div>
                <div className="text-sm text-slate-800 dark:text-slate-200 leading-relaxed whitespace-pre-wrap pt-1 font-medium">
                  <RichMathText content={analysis.aiSolution} />
                </div>
              </div>
            )}
          </div>
        )}

        {/* Existing Student Review Request Status Card */}
        {reviewRequest && (
          <div className={`rounded-2xl border p-4 space-y-2 ${
            reviewRequest.studentComment?.startsWith("[DEFECTIVE") || reviewRequest.studentComment?.startsWith("[QUESTION")
              ? "border-amber-500/30 bg-amber-500/10"
              : "border-purple-500/30 bg-purple-500/10"
          }`}>
            <div className="flex items-center justify-between">
              <span className={`text-xs font-bold uppercase tracking-wider flex items-center gap-1.5 ${
                reviewRequest.studentComment?.startsWith("[DEFECTIVE") || reviewRequest.studentComment?.startsWith("[QUESTION")
                  ? "text-amber-400"
                  : "text-purple-300"
              }`}>
                <span>{reviewRequest.studentComment?.startsWith("[DEFECTIVE") || reviewRequest.studentComment?.startsWith("[QUESTION") ? "🚩" : "🙋"}</span>
                {reviewRequest.studentComment?.startsWith("[DEFECTIVE") || reviewRequest.studentComment?.startsWith("[QUESTION")
                  ? "Báo cáo sự cố đề bài của bạn"
                  : "Yêu cầu xem xét của bạn"}
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
            <p className={`text-xs italic ${
              reviewRequest.studentComment?.startsWith("[DEFECTIVE") || reviewRequest.studentComment?.startsWith("[QUESTION")
                ? "text-amber-200"
                : "text-purple-200"
            }`}>
              "{reviewRequest.studentComment}"
            </p>
            {reviewRequest.teacherNote && (
              <div className={`pt-2 border-t text-xs ${
                reviewRequest.studentComment?.startsWith("[DEFECTIVE") || reviewRequest.studentComment?.startsWith("[QUESTION")
                  ? "border-amber-500/20 text-amber-100"
                  : "border-purple-500/20 text-purple-100"
              }`}>
                <span className={`font-semibold ${
                  reviewRequest.studentComment?.startsWith("[DEFECTIVE") || reviewRequest.studentComment?.startsWith("[QUESTION")
                    ? "text-amber-300"
                    : "text-purple-300"
                }`}>Phản hồi từ giáo viên: </span>
                {reviewRequest.teacherNote}
              </div>
            )}
          </div>
        )}

        {/* Action Buttons: Retry AI & Review Request / Dispute */}
        <div className="flex flex-wrap items-center justify-between gap-3 pt-2 border-t border-slate-100 dark:border-slate-800">
          <div className="flex items-center gap-2">
            {feedbackData.status === "AnalysisFailed" && retryQuota?.canRetry && (
              <button
                type="button"
                disabled={cooldownSeconds > 0 || isRetryingAI}
                onClick={handleRetryAI}
                className="inline-flex items-center gap-2 px-3.5 py-2 rounded-xl text-xs font-bold border transition-colors disabled:opacity-50 disabled:cursor-not-allowed bg-rose-50 dark:bg-rose-950/60 hover:bg-rose-100 dark:hover:bg-rose-900/60 text-rose-800 dark:text-rose-200 border-rose-300 dark:border-rose-700"
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

            {feedbackData.status === "AnalysisFailed" && !retryQuota?.canRetry && (
              <span className="text-xs font-semibold text-rose-500 dark:text-rose-400">
                ⚠️ Đã hết lượt kích hoạt chấm lại AI. Vui lòng liên hệ giáo viên để được hỗ trợ.
              </span>
            )}

            {retryError && (
              <span className="text-xs text-rose-500 dark:text-rose-400">{retryError}</span>
            )}
          </div>

          {!reviewRequest && (
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => setIsReviewModalOpen(true)}
                className="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-xl text-xs font-bold border border-purple-500/40 text-purple-600 dark:text-purple-300 hover:bg-purple-500/10 transition-colors cursor-pointer"
              >
                <span>🙋</span> Yêu cầu xem xét kết quả AI
              </button>
              <button
                type="button"
                onClick={() => setIsDisputeModalOpen(true)}
                className="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-xl text-xs font-bold border border-rose-500/40 text-rose-600 dark:text-rose-300 hover:bg-rose-500/10 transition-colors cursor-pointer"
              >
                <span>🚩</span> Báo cáo đề bài bị sai
              </button>
            </div>
          )}
        </div>
      </div>

      {/* TIER 3: ĐÁP ÁN & LỜI GIẢI CỦA GIÁO VIÊN (Teacher Solution - Collapsed by default) */}
      {!scoreAndFeedbackOnly && teacherSolution && (
        <div className="rounded-3xl bg-white dark:bg-[#0f172a] p-6 sm:p-7 shadow-xs border border-emerald-500/30 dark:border-emerald-500/20 space-y-4">
          <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
            <div className="flex items-center gap-2.5">
              <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 font-black text-xs">
                3
              </span>
              <div>
                <h3 className="text-base font-extrabold text-slate-900 dark:text-white">
                  Đáp Án & Lời Giải Chuẩn Của Giáo Viên (Teacher Solution)
                </h3>
                <span className="text-[11px] font-semibold text-emerald-600 dark:text-emerald-400 uppercase tracking-wider block sm:inline mt-0.5">
                  Tài liệu tham khảo chính thức
                </span>
              </div>
            </div>
            <button
              type="button"
              onClick={() => setIsTeacherSolutionOpen((prev) => !prev)}
              className="inline-flex items-center gap-1.5 text-xs font-bold text-emerald-800 dark:text-emerald-300 bg-emerald-100/70 hover:bg-emerald-200/80 dark:bg-emerald-950/60 dark:hover:bg-emerald-900/80 px-3.5 py-2 rounded-xl border border-emerald-300 dark:border-emerald-700 transition-colors cursor-pointer shrink-0"
            >
              <span>{isTeacherSolutionOpen ? "Thu gọn lời giải ▲" : "Xem lời giải của giáo viên ▼"}</span>
            </button>
          </div>

          {isTeacherSolutionOpen ? (
            <div className="space-y-4 pt-1">
              <div>
                <span className="text-xs font-semibold text-slate-400 block mb-1">Đáp án chính xác:</span>
                <div className="rounded-xl bg-emerald-500/10 border border-emerald-500/30 p-3.5 text-emerald-900 dark:text-emerald-200 font-mono font-bold text-sm">
                  <RichMathText content={formatAnswer(teacherSolution.correctAnswer)} />
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
          ) : (
            <div className="py-2 text-center text-xs text-slate-500 dark:text-slate-400">
              <p>Lời giải chuẩn của giáo viên đã sẵn sàng để đối chiếu sau khi nộp bài. Bấm <strong>[Xem lời giải của giáo viên ▼]</strong> ở trên để mở chi tiết.</p>
            </div>
          )}
        </div>
      )}

      {/* TIER 4: ĐÁNH GIÁ CHÍNH THỨC CỦA GIÁO VIÊN (Teacher Final Evaluation) */}
      {(teacherFinalEvaluation?.hasTeacherOverride || teacherFinalEvaluation?.isApprovedAsIs) && (
        <div className={`rounded-3xl p-6 sm:p-7 shadow-xs border space-y-4 ${
          teacherFinalEvaluation.isApprovedAsIs
            ? "bg-emerald-500/5 dark:bg-emerald-950/20 border-emerald-500/40"
            : "bg-purple-500/5 dark:bg-purple-950/20 border-purple-500/40"
        }`}>
          <div className="flex items-center justify-between border-b border-slate-200/60 dark:border-slate-800 pb-3">
            <div className="flex items-center gap-2.5">
              <span className={`flex h-7 w-7 items-center justify-center rounded-lg font-black text-xs ${
                teacherFinalEvaluation.isApprovedAsIs
                  ? "bg-emerald-500/20 text-emerald-600 dark:text-emerald-300"
                  : "bg-purple-500/20 text-purple-600 dark:text-purple-300"
              }`}>
                4
              </span>
              <h3 className="text-base font-extrabold text-slate-900 dark:text-white">
                {teacherFinalEvaluation.isApprovedAsIs
                  ? "Giáo Viên Đã Phê Duyệt Kết Quả AI (Teacher Approved)"
                  : "Đánh Giá Chính Thức Của Giáo Viên (Teacher Final Evaluation)"}
              </h3>
            </div>
            <span className={`text-[11px] font-bold uppercase tracking-wider px-2.5 py-1 rounded-full border ${
              teacherFinalEvaluation.isApprovedAsIs
                ? "text-emerald-700 dark:text-emerald-300 bg-emerald-500/10 border-emerald-500/30"
                : "text-purple-600 dark:text-purple-300 bg-purple-500/10 border-purple-500/30"
            }`}>
              {teacherFinalEvaluation.isApprovedAsIs
                ? "✓ Đã duyệt kết quả AI"
                : "Kết quả chấm đè chính thức"}
            </span>
          </div>

          <div className="space-y-3">
            <div className="flex items-center justify-between p-3.5 rounded-xl bg-white/60 dark:bg-slate-800/60 border border-slate-200/60 dark:border-slate-700/60">
              <div>
                <span className="text-xs text-slate-500 dark:text-slate-400 block">
                  {teacherFinalEvaluation.isApprovedAsIs ? "Điểm số chính thức (được xác nhận):" : "Điểm số sau khi giáo viên chấm đè:"}
                </span>
                <span className="text-xl font-black text-slate-900 dark:text-white">
                  {toDisplayedScore(teacherFinalEvaluation.teacherScore ?? grading.awardedScore) ?? "Chưa chấm"} / {displayedMaxScore}
                </span>
              </div>
              {teacherFinalEvaluation.reviewedByTeacherName && (
                <div className="text-right">
                  <span className="text-xs text-slate-500 dark:text-slate-400 block">Giáo viên xác nhận:</span>
                  <span className="text-sm font-bold text-slate-900 dark:text-white">{teacherFinalEvaluation.reviewedByTeacherName}</span>
                  {teacherFinalEvaluation.reviewedAt && (
                    <span className="text-[11px] text-slate-400 block">
                      {new Date(teacherFinalEvaluation.reviewedAt).toLocaleDateString("vi-VN")}
                    </span>
                  )}
                </div>
              )}
            </div>

            {teacherFinalEvaluation.isApprovedAsIs && (
              <p className="text-xs text-emerald-800 dark:text-emerald-300 leading-relaxed font-medium">
                Thầy/cô đã kiểm tra bài làm và xác nhận các luận điểm, phân tích và số điểm do AI đề xuất là hoàn toàn chính xác.
              </p>
            )}

            {(teacherFinalEvaluation.teacherFeedback || teacherFinalEvaluation.teacherReviewNote) && (
              <div className="rounded-xl bg-white dark:bg-slate-800/60 p-4 border border-slate-200 dark:border-slate-700">
                <span className="text-xs font-bold text-slate-400 block mb-1 uppercase tracking-wider">
                  Ghi chú từ giáo viên:
                </span>
                <div className="text-sm text-slate-900 dark:text-slate-100 leading-relaxed font-medium">
                  <RichMathText content={teacherFinalEvaluation.teacherFeedback || teacherFinalEvaluation.teacherReviewNote} />
                </div>
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

      {/* STUDENT DISPUTE MODAL */}
      {isDisputeModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
          <div className="w-full max-w-lg rounded-3xl bg-white dark:bg-[#0f172a] border border-slate-200 dark:border-slate-800 p-6 sm:p-7 shadow-2xl space-y-5">
            <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
              <h3 className="text-base font-extrabold text-slate-900 dark:text-white flex items-center gap-2">
                <span>🚩</span> Báo Cáo Đề Bài Bị Sai Sót
              </h3>
              <button
                type="button"
                onClick={() => setIsDisputeModalOpen(false)}
                className="text-slate-400 hover:text-slate-200 text-lg cursor-pointer"
              >
                ✕
              </button>
            </div>

            <div className="space-y-4">
              <div className="rounded-xl bg-amber-500/10 border border-amber-500/20 p-3 text-xs text-amber-800 dark:text-amber-200 leading-relaxed">
                <span className="font-bold">Lưu ý:</span> Khi bạn báo cáo câu hỏi sai sót (sai số liệu, thiếu đề, không có đáp án đúng, lỗi công thức LaTeX...), giáo viên phụ trách sẽ kiểm tra trực tiếp. Nếu đề bài thực sự có lỗi, giáo viên có thể hủy câu này và cộng điểm tối đa cho cả lớp.
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-2">
                  Loại sự cố gặp phải <span className="text-rose-400">*</span>:
                </label>
                <div className="space-y-2">
                  {[
                    { value: "DEFECTIVE_QUESTION_WRONG_CONTENT", label: "Đề bài sai dữ liệu / Thiếu giả thiết không giải được" },
                    { value: "DEFECTIVE_QUESTION_WRONG_OPTIONS", label: "Đáp án trắc nghiệm bị sai / Không có đáp án đúng" },
                    { value: "DEFECTIVE_QUESTION_TYPO_LATEX", label: "Lỗi hiển thị công thức LaTeX / Lỗi chính tả nghiêm trọng" },
                    { value: "DEFECTIVE_QUESTION_OTHER", label: "Sự cố khác liên quan đến đề bài" }
                  ].map((option) => (
                    <label
                      key={option.value}
                      className={`flex items-center gap-2.5 p-2.5 rounded-xl border text-xs cursor-pointer transition-colors ${
                        disputeCategory === option.value
                          ? "border-amber-500 bg-amber-500/10 text-amber-800 dark:text-amber-200 font-semibold"
                          : "border-slate-200 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800/40 text-slate-700 dark:text-slate-300"
                      }`}
                    >
                      <input
                        type="radio"
                        name="disputeCategory"
                        value={option.value}
                        checked={disputeCategory === option.value}
                        onChange={(e) => setDisputeCategory(e.target.value)}
                        className="text-amber-500 focus:ring-amber-500"
                      />
                      <span>{option.label}</span>
                    </label>
                  ))}
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                  Mô tả chi tiết lỗi <span className="text-rose-400">*</span>:
                </label>
                <textarea
                  rows={4}
                  value={disputeComment}
                  onChange={(e) => setDisputeComment(e.target.value)}
                  placeholder="Ví dụ: Đề bài yêu cầu tìm x nhưng không cho dữ kiện cạnh BC; hoặc đáp án A và C đều giống hệt nhau..."
                  className="w-full rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/60 p-3 text-xs text-slate-900 dark:text-slate-100 focus:outline-hidden focus:border-amber-500"
                />
                <span className="text-[11px] text-slate-400 mt-1 block">
                  Tối thiểu 10 ký tự ({disputeComment.trim().length}/500)
                </span>
              </div>

              {disputeModalError && (
                <div className="p-3 rounded-xl bg-rose-500/10 border border-rose-500/30 text-rose-300 text-xs">
                  {disputeModalError}
                </div>
              )}

              {disputeSuccessMessage && (
                <div className="p-3 rounded-xl bg-emerald-500/10 border border-emerald-500/30 text-emerald-300 text-xs font-semibold">
                  ✓ {disputeSuccessMessage}
                </div>
              )}
            </div>

            <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
              <button
                type="button"
                onClick={() => setIsDisputeModalOpen(false)}
                className="px-4 py-2 text-xs font-bold rounded-xl border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                Hủy
              </button>
              <button
                type="button"
                disabled={isSubmittingDispute || disputeComment.trim().length < 10}
                onClick={handleSubmitDispute}
                className="px-5 py-2 text-xs font-bold rounded-xl bg-amber-600 hover:bg-amber-500 text-white disabled:opacity-50 flex items-center gap-2 cursor-pointer"
              >
                {isSubmittingDispute ? "Đang gửi..." : "Gửi báo cáo sự cố"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
