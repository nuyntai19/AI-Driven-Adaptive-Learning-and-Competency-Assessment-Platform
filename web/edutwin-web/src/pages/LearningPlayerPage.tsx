import { useState, useEffect, useRef } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  getNextQuestion,
  submitAttempt,
  getAnalysisJobStatus,
  getAttemptFeedback,
} from "../api/learningFeedbackApi";
import type {
  NextQuestionDataDto,
  AttemptFeedbackDataDto,
} from "../types/learning";

export const LearningPlayerPage = () => {
  const [searchParams] = useSearchParams();
  const subjectId = searchParams.get("subjectId") || "";

  // Attempt form state
  const [finalAnswer, setFinalAnswer] = useState<string>("");
  const [reasoningText, setReasoningText] = useState<string>("");
  const [confidence, setConfidence] = useState<number>(80);
  const [answerChanges, setAnswerChanges] = useState<number>(0);
  const [timeSpentSeconds, setTimeSpentSeconds] = useState<number>(0);

  // Client submission token (unique per attempt session)
  const clientSubmissionIdRef = useRef<string>(crypto.randomUUID());

  // Workflow state
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [pollingJobId, setPollingJobId] = useState<string | null>(null);
  const [pollingStatus, setPollingStatus] = useState<string>("Đang xử lý...");
  const [feedbackData, setFeedbackData] = useState<AttemptFeedbackDataDto | null>(null);
  const [submissionError, setSubmissionError] = useState<string | null>(null);

  // Timer
  useEffect(() => {
    if (pollingJobId || feedbackData) return;
    const timer = setInterval(() => {
      setTimeSpentSeconds((prev) => prev + 1);
    }, 1000);
    return () => clearInterval(timer);
  }, [pollingJobId, feedbackData]);

  // Load question
  const {
    data: question,
    isLoading: questionLoading,
    isError: questionError,
    refetch: refetchQuestion,
  } = useQuery<NextQuestionDataDto>({
    queryKey: ["nextQuestion", subjectId],
    queryFn: () => getNextQuestion(subjectId),
    enabled: !feedbackData && !pollingJobId,
  });

  // Track answer changes deterministically
  const handleAnswerChange = (newAnswer: string) => {
    if (newAnswer !== finalAnswer && finalAnswer !== "") {
      setAnswerChanges((prev) => prev + 1);
    }
    setFinalAnswer(newAnswer);
  };

  // Submit attempt
  const handleSubmit = async (skipped: boolean = false) => {
    if (!question) return;
    if (!skipped && !finalAnswer.trim()) {
      setSubmissionError("Vui lòng nhập đáp án trước khi nộp.");
      return;
    }

    setIsSubmitting(true);
    setSubmissionError(null);

    try {
      const response = await submitAttempt({
        questionId: question.questionId,
        finalAnswer: skipped ? "SKIPPED" : finalAnswer.trim(),
        reasoningText: reasoningText.trim() ? reasoningText.trim() : null,
        timeSpentSeconds,
        confidence: confidence / 100,
        answerChanges,
        skipped,
        reasoningLanguage: "vi",
        clientSubmissionId: clientSubmissionIdRef.current,
      });

      setPollingJobId(response.jobId);
      setPollingStatus("AI đang phân tích câu trả lời...");
    } catch (err: unknown) {
      setIsSubmitting(false);
      const message = (err as Error)?.message || "Không thể gửi bài làm. Vui lòng thử lại.";
      setSubmissionError(message);
    }
  };

  // Poll analysis job every 3 seconds until terminal == true
  useEffect(() => {
    if (!pollingJobId) return;

    let isMounted = true;
    const interval = setInterval(async () => {
      try {
        const jobStatus = await getAnalysisJobStatus(pollingJobId);
        if (!isMounted) return;

        setPollingStatus(`Đang phân tích tư duy... (${jobStatus.status})`);

        if (jobStatus.terminal) {
          clearInterval(interval);
          setPollingJobId(null);
          setIsSubmitting(false);

          // Fetch full feedback according to API contract 54
          const feedback = await getAttemptFeedback(jobStatus.attemptId);
          if (isMounted) {
            setFeedbackData(feedback);
          }
        }
      } catch {
        // Continue polling until timeout or success
      }
    }, 3000);

    return () => {
      isMounted = false;
      clearInterval(interval);
    };
  }, [pollingJobId]);

  // Next question handler
  const handleNextQuestion = () => {
    setFeedbackData(null);
    setPollingJobId(null);
    setFinalAnswer("");
    setReasoningText("");
    setAnswerChanges(0);
    setTimeSpentSeconds(0);
    setIsSubmitting(false);
    setSubmissionError(null);
    clientSubmissionIdRef.current = crypto.randomUUID();
    refetchQuestion();
  };

  if (questionLoading) {
    return (
      <div className="min-h-screen bg-slate-50 p-6">
        <div className="mx-auto max-w-3xl rounded-xl bg-white p-8 shadow-sm ring-1 ring-slate-200">
          <div className="h-6 w-1/3 animate-pulse rounded bg-slate-200" />
          <div className="mt-4 h-32 animate-pulse rounded-lg bg-slate-100" />
          <div className="mt-6 h-24 animate-pulse rounded-lg bg-slate-100" />
        </div>
      </div>
    );
  }

  if (questionError && !feedbackData) {
    return (
      <div className="min-h-screen bg-slate-50 p-6">
        <div className="mx-auto max-w-2xl rounded-xl bg-white p-8 text-center shadow-sm ring-1 ring-slate-200">
          <h2 className="text-xl font-bold text-slate-900">Không tìm thấy câu hỏi thích hợp</h2>
          <p className="mt-2 text-sm text-slate-500">
            Hiện tại bạn đã hoàn thành hết các câu hỏi đề xuất trong môn học này hoặc hệ thống đang đồng bộ.
          </p>
          <div className="mt-6 flex justify-center gap-3">
            <Link
              to="/hoc-tap/tong-quan"
              className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500"
            >
              Về Dashboard
            </Link>
          </div>
        </div>
      </div>
    );
  }

  // 1. Polling Screen (HTTP 202 Processing)
  if (pollingJobId) {
    return (
      <div className="min-h-screen bg-slate-50 p-6">
        <div className="mx-auto max-w-xl rounded-xl bg-white p-12 text-center shadow-sm ring-1 ring-slate-200">
          <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-indigo-50">
            <div className="h-8 w-8 animate-spin rounded-full border-4 border-indigo-600 border-t-transparent" />
          </div>
          <h2 className="mt-6 text-xl font-bold text-slate-900">AI Đang Đánh Giá & Phân Tích</h2>
          <p className="mt-2 text-sm text-slate-500">{pollingStatus}</p>
          <div className="mt-4 flex justify-center gap-2">
            <span className="inline-block h-2 w-2 animate-bounce rounded-full bg-indigo-400" />
            <span className="inline-block h-2 w-2 animate-bounce rounded-full bg-indigo-500 [animation-delay:0.2s]" />
            <span className="inline-block h-2 w-2 animate-bounce rounded-full bg-indigo-600 [animation-delay:0.4s]" />
          </div>
          <p className="mt-6 text-xs text-slate-400">
            Hệ thống đang kiểm chứng phương pháp giải, phát hiện quan niệm sai và cập nhật Hồ sơ Năng lực (Twin).
          </p>
        </div>
      </div>
    );
  }

  // 2. Feedback Screen (API Contract 54)
  if (feedbackData) {
    const { grading, analysis, twinChange, recommendation } = feedbackData;

    return (
      <div className="min-h-screen bg-slate-50 p-6">
        <div className="mx-auto max-w-3xl space-y-6">
          {/* Header Result Card */}
          <div
            className={`rounded-xl p-6 text-white shadow-md ${
              grading.isCorrect
                ? "bg-gradient-to-r from-emerald-600 to-teal-700"
                : "bg-gradient-to-r from-rose-600 to-amber-700"
            }`}
          >
            <div className="flex items-center justify-between">
              <div>
                <span className="rounded-full bg-white/20 px-3 py-1 text-xs font-bold uppercase tracking-wider text-white">
                  {grading.isCorrect ? "Đáp án chính xác" : "Cần hoàn thiện"}
                </span>
                <h2 className="mt-2 text-2xl font-black">
                  Điểm số: {grading.awardedScore ?? 0} / {grading.maxScore}
                </h2>
              </div>
              <div className="text-right">
                <span className="text-xs text-white/80">Lượt làm bài: #{feedbackData.attemptId}</span>
                <p className="text-xs text-white/80">Trạng thái: {feedbackData.status}</p>
              </div>
            </div>
          </div>

          {/* Reasoning Analysis Card */}
          {analysis && (
            <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200 space-y-4">
              <div className="flex items-center justify-between border-b border-slate-100 pb-3">
                <h3 className="text-base font-bold text-slate-900">
                  Phân Tích Tư Duy & Lập Luận (AI Reasoning Analysis)
                </h3>
                {analysis.qualityBand && (
                  <span
                    className={`rounded-full px-3 py-0.5 text-xs font-bold ${
                      analysis.qualityBand === "Good"
                        ? "bg-emerald-100 text-emerald-800"
                        : analysis.qualityBand === "Acceptable"
                        ? "bg-blue-100 text-blue-800"
                        : analysis.qualityBand === "NeedsImprovement"
                        ? "bg-amber-100 text-amber-800"
                        : "bg-red-100 text-red-800"
                    }`}
                  >
                    Bậc tư duy: {analysis.qualityBand}
                  </span>
                )}
              </div>

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                {analysis.methodDetected && (
                  <div className="rounded-lg bg-slate-50 p-3">
                    <p className="text-xs font-medium text-slate-500">Phương pháp nhận diện</p>
                    <p className="mt-1 text-sm font-semibold text-slate-900">{analysis.methodDetected}</p>
                  </div>
                )}
                {analysis.reasoningQuality !== null && analysis.reasoningQuality !== undefined && (
                  <div className="rounded-lg bg-slate-50 p-3">
                    <p className="text-xs font-medium text-slate-500">Chất lượng lập luận</p>
                    <p className="mt-1 text-sm font-semibold text-indigo-600">
                      {analysis.reasoningQuality} / 100 điểm
                    </p>
                  </div>
                )}
              </div>

              {/* Feedback text */}
              <div className="rounded-lg bg-indigo-50/60 p-4 ring-1 ring-inset ring-indigo-200/50">
                <p className="text-xs font-bold text-indigo-900 uppercase">Nhận xét từ AI</p>
                <p className="mt-1 text-sm text-indigo-950 leading-relaxed">{analysis.feedback}</p>
              </div>

              {/* Missing steps if any */}
              {analysis.missingSteps && analysis.missingSteps.length > 0 && (
                <div>
                  <p className="text-xs font-bold text-slate-700">Các bước còn thiếu hoặc cần bổ sung:</p>
                  <ul className="mt-1 list-inside list-disc space-y-1 text-sm text-slate-600">
                    {analysis.missingSteps.map((step, idx) => (
                      <li key={idx}>{step}</li>
                    ))}
                  </ul>
                </div>
              )}

              {/* Misconception if any */}
              {analysis.misconception && (
                <div className="rounded-lg bg-rose-50 p-3 text-xs text-rose-800 ring-1 ring-rose-200">
                  <span className="font-bold">Quan niệm sai lầm: </span>
                  {analysis.misconception}
                </div>
              )}

              {/* Root cause nodes */}
              {analysis.rootCauseNodes && analysis.rootCauseNodes.length > 0 && (
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="text-xs text-slate-500">Gốc rễ kiến thức liên quan:</span>
                  {analysis.rootCauseNodes.map((node) => (
                    <span
                      key={node.nodeId}
                      className="rounded bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-700"
                    >
                      {node.nodeName}
                    </span>
                  ))}
                </div>
              )}

              {/* Review status flags */}
              <div className="flex flex-wrap gap-2 pt-2 text-xs">
                {analysis.isFallback && (
                  <span className="rounded bg-amber-100 px-2.5 py-0.5 font-medium text-amber-800">
                    Phân tích quy tắc dự phòng (Fallback)
                  </span>
                )}
                {analysis.needsTeacherReview && (
                  <span className="rounded bg-amber-100 px-2.5 py-0.5 font-medium text-amber-800">
                    Đã xếp hàng chờ Giáo viên xem xét
                  </span>
                )}
                {analysis.hasTeacherOverride && (
                  <span className="rounded bg-indigo-100 px-2.5 py-0.5 font-medium text-indigo-800">
                    Đã được Giáo viên điều chỉnh
                  </span>
                )}
              </div>
            </div>
          )}

          {/* Digital Twin Change Card */}
          {twinChange && (
            <div className="rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
              <h3 className="text-base font-bold text-slate-900">
                Tác Động Hồ Sơ Năng Lực (Digital Twin Delta)
              </h3>
              <div className="mt-3 flex items-center justify-between rounded-lg bg-slate-50 p-4">
                <div>
                  <p className="font-semibold text-slate-900">{twinChange.topicName}</p>
                  <p className="text-xs text-slate-500">{twinChange.explanation}</p>
                </div>
                <div className="text-right">
                  <span className="text-xs text-slate-400">
                    {twinChange.previousMastery.toFixed(1)}% →{" "}
                  </span>
                  <span className="text-base font-bold text-slate-900">
                    {twinChange.newMastery.toFixed(1)}%
                  </span>
                  <span
                    className={`ml-1 text-xs font-bold ${
                      twinChange.delta >= 0 ? "text-emerald-600" : "text-red-600"
                    }`}
                  >
                    ({twinChange.delta >= 0 ? `+${twinChange.delta.toFixed(1)}` : twinChange.delta.toFixed(1)}%)
                  </span>
                </div>
              </div>
            </div>
          )}

          {/* Next Recommendation Card */}
          {recommendation && (
            <div className="rounded-xl bg-gradient-to-br from-indigo-50 to-purple-50 p-6 shadow-sm ring-1 ring-indigo-200">
              <span className="inline-flex items-center rounded-full bg-indigo-100 px-2.5 py-0.5 text-xs font-semibold text-indigo-800">
                Gợi ý bước tiếp theo: {recommendation.type}
              </span>
              <h4 className="mt-2 text-base font-bold text-slate-900">
                {recommendation.topicName}
              </h4>
              <p className="mt-1 text-sm text-slate-600">{recommendation.explanation}</p>
            </div>
          )}

          {/* Action Buttons */}
          <div className="flex justify-between items-center pt-4">
            <Link
              to="/hoc-tap/tong-quan"
              className="rounded-lg bg-white px-5 py-2.5 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
            >
              Về Dashboard
            </Link>
            <button
              onClick={handleNextQuestion}
              className="rounded-lg bg-indigo-600 px-6 py-2.5 text-sm font-bold text-white shadow-sm hover:bg-indigo-500"
            >
              Luyện câu tiếp theo →
            </button>
          </div>
        </div>
      </div>
    );
  }

  // 3. Question Answering Screen
  return (
    <div className="min-h-screen bg-slate-50 p-6">
      <div className="mx-auto max-w-3xl space-y-6">
        {/* Header bar */}
        <div className="flex items-center justify-between rounded-xl bg-white p-4 shadow-sm ring-1 ring-slate-200">
          <div className="flex items-center gap-3">
            <span className="rounded-md bg-indigo-50 px-2.5 py-1 text-xs font-semibold text-indigo-700">
              {question?.topicName || "Chuyên đề luyện tập"}
            </span>
            <span className="text-xs text-slate-500">Độ khó: {question?.difficulty ?? 3}/5</span>
          </div>
          <div className="flex items-center gap-4 text-xs font-medium text-slate-600">
            <span>
              Thời gian: {Math.floor(timeSpentSeconds / 60)}:
              {(timeSpentSeconds % 60).toString().padStart(2, "0")}
            </span>
            {answerChanges > 0 && (
              <span className="text-amber-600">Đã đổi đáp án: {answerChanges} lần</span>
            )}
          </div>
        </div>

        {submissionError && (
          <div className="rounded-lg bg-red-50 p-4 text-sm font-medium text-red-800 ring-1 ring-red-200">
            {submissionError}
          </div>
        )}

        {/* Question content card */}
        <div className="rounded-xl bg-white p-8 shadow-sm ring-1 ring-slate-200 space-y-6">
          <div>
            <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
              Câu hỏi (#{question?.questionId})
            </span>
            <div className="mt-3 text-base text-slate-900 leading-relaxed whitespace-pre-wrap">
              {question?.questionText}
            </div>
          </div>

          {/* Final Answer Input */}
          <div className="border-t border-slate-100 pt-6">
            <label className="block text-sm font-bold text-slate-900">
              Đáp án cuối cùng của bạn:
            </label>
            <input
              type="text"
              value={finalAnswer}
              onChange={(e) => handleAnswerChange(e.target.value)}
              disabled={isSubmitting}
              placeholder="Nhập đáp án (ví dụ: A, 2x, 4.5...)"
              className="mt-2 w-full rounded-lg border border-slate-300 px-4 py-2.5 text-slate-900 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
            />
          </div>

          {/* Reasoning Text Input */}
          <div className="border-t border-slate-100 pt-6">
            <div className="flex items-center justify-between">
              <label className="block text-sm font-bold text-slate-900">
                Các bước suy luận & Giải thích cách làm:
              </label>
              {question?.reasoningRequired && (
                <span className="text-xs font-medium text-indigo-600">Bắt buộc giải thích</span>
              )}
            </div>
            <p className="mt-1 text-xs text-slate-500">
              Hệ thống AI sẽ phân tích các bước lập luận để phát hiện thiếu sót và cập nhật Hồ sơ Năng lực.
            </p>
            <textarea
              rows={4}
              value={reasoningText}
              onChange={(e) => setReasoningText(e.target.value)}
              disabled={isSubmitting}
              placeholder="Mô tả từng bước bạn đã thực hiện để tìm ra kết quả..."
              className="mt-2 w-full rounded-lg border border-slate-300 p-3 text-slate-900 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 text-sm"
            />
          </div>

          {/* Confidence Slider */}
          <div className="border-t border-slate-100 pt-6">
            <div className="flex items-center justify-between">
              <label className="text-sm font-bold text-slate-900">
                Độ tự tin vào lời giải: {confidence}%
              </label>
              <span className="text-xs text-slate-500">
                {confidence >= 80 ? "Rất chắc chắn" : confidence >= 50 ? "Khá tự tin" : "Chưa chắc chắn"}
              </span>
            </div>
            <input
              type="range"
              min={10}
              max={100}
              step={5}
              value={confidence}
              onChange={(e) => setConfidence(Number(e.target.value))}
              disabled={isSubmitting}
              className="mt-3 w-full accent-indigo-600"
            />
          </div>

          {/* Action buttons */}
          <div className="flex items-center justify-between border-t border-slate-100 pt-6">
            <button
              onClick={() => handleSubmit(true)}
              disabled={isSubmitting}
              className="rounded-lg bg-slate-100 px-4 py-2.5 text-sm font-semibold text-slate-600 hover:bg-slate-200 disabled:opacity-50"
            >
              Bỏ qua câu này
            </button>

            <button
              onClick={() => handleSubmit(false)}
              disabled={isSubmitting || !finalAnswer.trim()}
              className="rounded-lg bg-indigo-600 px-6 py-2.5 text-sm font-bold text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
            >
              {isSubmitting ? "Đang nộp bài..." : "Nộp bài & Phân tích"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
