import { useState, useEffect, useRef, useMemo } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  getNextQuestion,
  submitAttempt,
  getAnalysisJobStatus,
  getAttemptFeedback,
  prepareAttemptAttachmentUpload,
} from "../api/learningFeedbackApi";
import type {
  NextQuestionDataDto,
  AttemptFeedbackDataDto,
} from "../types/learning";
import {
  isSuccessfulTerminalStatus,
  isTerminalStatus,
  shouldContinuePolling,
} from "../utils/polling";
import { SubjectRequiredState } from "../components/SubjectRequiredState";
import { MathFormulaPreview } from "../components/math/MathFormulaPreview";
import { MathInputToolbar } from "../components/math/MathInputToolbar";
import { ScientificCalculatorDrawer } from "../components/math/ScientificCalculatorDrawer";
import { ScratchpadCanvasModal } from "../components/math/ScratchpadCanvasModal";
import { deleteScratchpadDraft } from "../utils/scratchpadStorage";
import {
  clearAttemptSessionId,
  createClientSubmissionId,
  getOrCreateAttemptSessionId,
} from "../utils/attemptSessionStorage";
import { useAuthStore } from "../stores/authStore";

export const LearningPlayerPage = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const subjectId = searchParams.get("subjectId") || "";
  const persistedJobId = searchParams.get("analysisJobId");

  // Attempt form state
  const [finalAnswer, setFinalAnswer] = useState<string>("");
  const [answerDisplayLatex, setAnswerDisplayLatex] = useState<string>("");
  const [reasoningText, setReasoningText] = useState<string>("");
  const [confidence, setConfidence] = useState<number>(80);
  const [answerChanges, setAnswerChanges] = useState<number>(0);
  const [timeSpentSeconds, setTimeSpentSeconds] = useState<number>(0);
  const [isCalculatorOpen, setIsCalculatorOpen] = useState<boolean>(false);
  const [showMathToolbar, setShowMathToolbar] = useState<boolean>(false);
  const [isScratchpadOpen, setIsScratchpadOpen] = useState<boolean>(false);
  const [scratchpadPngBytes, setScratchpadPngBytes] = useState<number | null>(null);
  const [scratchpadPng, setScratchpadPng] = useState<Blob | null>(null);
  const [drawingUploadToken, setDrawingUploadToken] = useState<string | null>(null);
  const currentUser = useAuthStore((state) => state.user);

  // Input refs and cursor management
  const shortAnswerInputRef = useRef<HTMLInputElement>(null);
  const essayTextareaRef = useRef<HTMLTextAreaElement>(null);
  const reasoningTextareaRef = useRef<HTMLTextAreaElement>(null);
  const [activeInputTarget, setActiveInputTarget] = useState<"answer" | "reasoning">("answer");

  // Client submission token (unique per attempt session)
  const clientSubmissionIdRef = useRef<string>(createClientSubmissionId());

  // Workflow state
  const [isSubmitting, setIsSubmitting] = useState<boolean>(Boolean(persistedJobId));
  const [pollingJobId, setPollingJobId] = useState<string | null>(persistedJobId);
  const [pollingStatus, setPollingStatus] = useState<string>("Đang xử lý...");
  const [feedbackData, setFeedbackData] = useState<AttemptFeedbackDataDto | null>(null);
  const [submissionError, setSubmissionError] = useState<string | null>(null);
  const pollingAttemptRef = useRef(0);

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
    enabled: !!subjectId && !feedbackData && !pollingJobId && !persistedJobId,
  });

  const attemptSessionScope = useMemo(() => {
    if (!currentUser || !question) return null;
    return {
      centerId: currentUser.centerId,
      userId: currentUser.userId,
      subjectId,
      questionId: String(question.questionId),
    };
  }, [currentUser, question, subjectId]);

  // The same idempotency identity is restored after a reload/remount for this exact learner/question scope.
  useEffect(() => {
    if (!attemptSessionScope) return;
    clientSubmissionIdRef.current = getOrCreateAttemptSessionId(attemptSessionScope);
  }, [attemptSessionScope]);

  const getClientSubmissionId = () => {
    if (!attemptSessionScope) return clientSubmissionIdRef.current;
    const id = getOrCreateAttemptSessionId(attemptSessionScope);
    clientSubmissionIdRef.current = id;
    return id;
  };

  // Derive presentation LaTeX separating presentation from semantic evaluation
  const derivedDisplayLatex = useMemo(() => {
    if (answerDisplayLatex.trim()) return answerDisplayLatex.trim();
    if (!finalAnswer.trim()) return "";
    // If it's a simple rational fraction like 3/2 or -1/4, auto-format to KaTeX \frac{p}{q}
    const fracMatch = finalAnswer.trim().match(/^([+-]?\d+)\s*\/\s*(\d+)$/);
    if (fracMatch) {
      const sign = fracMatch[1].startsWith("-") ? "-" : "";
      const num = fracMatch[1].replace(/^[+-]/, "");
      const den = fracMatch[2];
      return `${sign}\\frac{${num}}{${den}}`;
    }
    return finalAnswer.trim();
  }, [answerDisplayLatex, finalAnswer]);

  // Track answer changes deterministically
  const handleAnswerChange = (newAnswer: string) => {
    if (newAnswer !== finalAnswer && finalAnswer !== "") {
      setAnswerChanges((prev) => prev + 1);
    }
    setFinalAnswer(newAnswer);
  };

  // Cursor-aware insertion supporting both answer inputs and reasoning text
  const insertTextAtCursor = (textToInsert: string) => {
    if (activeInputTarget === "reasoning") {
      const el = reasoningTextareaRef.current;
      if (!el) {
        setReasoningText((prev) => prev + textToInsert);
        return;
      }
      const start = el.selectionStart ?? reasoningText.length;
      const end = el.selectionEnd ?? reasoningText.length;
      const nextVal = reasoningText.substring(0, start) + textToInsert + reasoningText.substring(end);
      setReasoningText(nextVal);
      requestAnimationFrame(() => {
        el.focus();
        const pos = start + textToInsert.length;
        el.setSelectionRange(pos, pos);
      });
      return;
    }

    if (question?.questionType === "Essay") {
      const el = essayTextareaRef.current;
      if (!el) {
        handleAnswerChange(finalAnswer + textToInsert);
        return;
      }
      const start = el.selectionStart ?? finalAnswer.length;
      const end = el.selectionEnd ?? finalAnswer.length;
      const nextVal = finalAnswer.substring(0, start) + textToInsert + finalAnswer.substring(end);
      handleAnswerChange(nextVal);
      requestAnimationFrame(() => {
        el.focus();
        const pos = start + textToInsert.length;
        el.setSelectionRange(pos, pos);
      });
    } else {
      const el = shortAnswerInputRef.current;
      let insertStr = textToInsert;
      if (question?.answerEvaluationMode === "NumericRational" && textToInsert === "\\frac{a}{b}") {
        insertStr = "/";
      }
      if (!el) {
        handleAnswerChange(finalAnswer + insertStr);
        return;
      }
      const start = el.selectionStart ?? finalAnswer.length;
      const end = el.selectionEnd ?? finalAnswer.length;
      const nextVal = finalAnswer.substring(0, start) + insertStr + finalAnswer.substring(end);
      handleAnswerChange(nextVal);
      requestAnimationFrame(() => {
        el.focus();
        const pos = start + insertStr.length;
        el.setSelectionRange(pos, pos);
      });
    }
  };

  // Submit attempt
  const handleSubmit = async (skipped: boolean = false) => {
    if (!question) return;
    if (!skipped && !finalAnswer.trim()) {
      setSubmissionError("Vui lòng nhập đáp án trước khi nộp.");
      return;
    }
    if (!skipped && question.reasoningRequired && !reasoningText.trim()) {
      setSubmissionError("Câu hỏi này yêu cầu bạn trình bày các bước suy luận.");
      return;
    }

    setIsSubmitting(true);
    setSubmissionError(null);
    const clientSubmissionId = getClientSubmissionId();

    try {
      const uploadToken = scratchpadPng
        ? drawingUploadToken ?? (await prepareAttemptAttachmentUpload(scratchpadPng)).drawingUploadToken
        : null;
      if (scratchpadPng && !drawingUploadToken) {
        setDrawingUploadToken(uploadToken);
      }
      const submitted = await submitAttempt({
        questionId: String(question.questionId),
        finalAnswer: skipped ? "SKIPPED" : finalAnswer.trim(),
        reasoningText: reasoningText.trim() ? reasoningText.trim() : null,
        timeSpentSeconds,
        confidence,
        answerChanges,
        skipped,
        clientSubmissionId,
        answerDisplayLatex: derivedDisplayLatex || (finalAnswer.trim() ? finalAnswer.trim() : null),
        drawingUploadToken: uploadToken,
      });

      // A network/validation failure leaves the vector draft untouched. A 202 acceptance
      // (or future 200 idempotent replay) is the only point at which it may be discarded.
      if ((submitted.status === 202 || submitted.status === 200) && currentUser) {
        try {
          await deleteScratchpadDraft(
            currentUser.centerId,
            currentUser.userId,
            clientSubmissionId
          );
        } catch {
          // The server accepted the submission; a local cleanup failure must not invite a duplicate submission.
        }
        if (attemptSessionScope) clearAttemptSessionId(attemptSessionScope);
        setScratchpadPngBytes(null);
        setScratchpadPng(null);
        setDrawingUploadToken(null);
      }

      const response = submitted.data;
      const activeJobId = response.analysisJobId || response.jobId || "";
      if (!activeJobId) {
        throw new Error("Máy chủ không trả về mã tiến trình phân tích.");
      }
      pollingAttemptRef.current = 0;
      setPollingJobId(activeJobId);
      setPollingStatus("AI đang phân tích câu trả lời...");
      setSearchParams((current) => {
        const next = new URLSearchParams(current);
        next.set("analysisJobId", activeJobId);
        next.set("attemptId", response.attemptId);
        return next;
      });
    } catch (err: unknown) {
      setIsSubmitting(false);
      const message = (err as Error)?.message || "Không thể gửi bài làm. Vui lòng thử lại.";
      setSubmissionError(message);
    }
  };

  // Poll status only (never resubmit the Attempt). The URL-backed job id survives refresh/remount.
  const jobStatusQuery = useQuery({
    queryKey: ["analysisJobStatus", pollingJobId],
    queryFn: () => getAnalysisJobStatus(pollingJobId!),
    enabled: Boolean(pollingJobId),
    retry: false,
    refetchInterval: (query) => {
      const status = query.state.data;
      if (status && (status.terminal || isTerminalStatus(status.status))) return false;
      return pollingAttemptRef.current >= 60 ? false : 3000;
    },
  });

  useEffect(() => {
    if (!pollingJobId || !jobStatusQuery.data) return;

    const jobStatus = jobStatusQuery.data;
    pollingAttemptRef.current += 1;
    setPollingStatus(`Đang phân tích tư duy... (${jobStatus.status})`);

    if (jobStatus.terminal || isTerminalStatus(jobStatus.status)) {
      setPollingJobId(null);

      if (!isSuccessfulTerminalStatus(jobStatus.status)) {
        setIsSubmitting(false);
        setSearchParams((current) => {
          const next = new URLSearchParams(current);
          next.delete("analysisJobId");
          next.delete("attemptId");
          return next;
        });
        setSubmissionError("Phân tích không hoàn tất. Bạn có thể thử tải lại kết quả mà không cần nộp lại bài.");
        return;
      }

      void getAttemptFeedback(jobStatus.attemptId)
        .then((feedback) => {
          setFeedbackData(feedback);
          setSearchParams((current) => {
            const next = new URLSearchParams(current);
            next.delete("analysisJobId");
            next.delete("attemptId");
            return next;
          });
        })
        .catch((error: unknown) => {
          setSubmissionError((error as Error)?.message || "Không thể tải kết quả phân tích.");
        })
        .finally(() => setIsSubmitting(false));
    } else if (!shouldContinuePolling(jobStatus.status, pollingAttemptRef.current)) {
      setPollingJobId(null);
      setIsSubmitting(false);
      setSubmissionError("Phân tích mất nhiều thời gian hơn dự kiến. Hãy thử tải kết quả lại sau.");
    }
  }, [jobStatusQuery.data, jobStatusQuery.dataUpdatedAt, pollingJobId, setSearchParams]);

  useEffect(() => {
    if (!pollingJobId || !jobStatusQuery.error) return;
    pollingAttemptRef.current += 1;
    if (!shouldContinuePolling(undefined, pollingAttemptRef.current)) {
      setPollingJobId(null);
      setIsSubmitting(false);
      setSubmissionError((jobStatusQuery.error as Error)?.message || "Không thể kiểm tra tiến trình phân tích.");
    }
  }, [jobStatusQuery.error, jobStatusQuery.errorUpdatedAt, pollingJobId]);

  if (!subjectId) {
    return <SubjectRequiredState onSelect={(selected) => setSearchParams({ subjectId: selected })} />;
  }

  if (!pollingJobId && persistedJobId) {
    return (
      <div className="min-h-screen bg-slate-50 p-6">
        <div className="mx-auto max-w-xl rounded-xl bg-white p-8 text-center shadow-sm ring-1 ring-slate-200">
          <h2 className="text-xl font-bold text-slate-900">
            {isSubmitting ? "Đang tải kết quả phân tích" : "Tiến trình phân tích đang được giữ lại"}
          </h2>
          <p role="alert" className="mt-2 text-sm text-slate-600">
            {submissionError || "Bạn có thể tiếp tục kiểm tra tiến trình mà không cần nộp lại bài."}
          </p>
          <button
            type="button"
            disabled={isSubmitting}
            onClick={() => {
              pollingAttemptRef.current = 0;
              setSubmissionError(null);
              setIsSubmitting(true);
              setPollingJobId(persistedJobId);
            }}
            className="mt-5 rounded-lg bg-indigo-600 px-5 py-2.5 text-sm font-bold text-white hover:bg-indigo-500 disabled:opacity-50"
          >
            {isSubmitting ? "Đang tải..." : "Tiếp tục kiểm tra kết quả"}
          </button>
        </div>
      </div>
    );
  }

  // Next question handler
  const handleNextQuestion = () => {
    setFeedbackData(null);
    setPollingJobId(null);
    setFinalAnswer("");
    setAnswerDisplayLatex("");
    setReasoningText("");
    setAnswerChanges(0);
    setTimeSpentSeconds(0);
    setIsSubmitting(false);
    setShowMathToolbar(false);
    setIsScratchpadOpen(false);
    setScratchpadPngBytes(null);
    setSubmissionError(null);
    pollingAttemptRef.current = 0;
    if (attemptSessionScope) clearAttemptSessionId(attemptSessionScope);
    clientSubmissionIdRef.current = createClientSubmissionId();
    setSearchParams((current) => {
      const next = new URLSearchParams(current);
      next.delete("analysisJobId");
      next.delete("attemptId");
      return next;
    });
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
              grading.isCorrect === true
                ? "bg-gradient-to-r from-emerald-600 to-teal-700"
                : grading.isCorrect === false
                  ? "bg-gradient-to-r from-rose-600 to-amber-700"
                  : "bg-gradient-to-r from-slate-600 to-indigo-700"
            }`}
          >
            <div className="flex items-center justify-between">
              <div>
                <span className="rounded-full bg-white/20 px-3 py-1 text-xs font-bold uppercase tracking-wider text-white">
                  {grading.isCorrect === true
                    ? "Đáp án chính xác"
                    : grading.isCorrect === false
                      ? "Cần hoàn thiện"
                      : "Đang chờ đánh giá"}
                </span>
                <h2 className="mt-2 text-2xl font-black">
                  Điểm số: {grading.awardedScore === null || grading.awardedScore === undefined
                    ? `Chưa chấm / ${grading.maxScore}`
                    : `${grading.awardedScore} / ${grading.maxScore}`}
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
            <button
              type="button"
              onClick={() => setIsCalculatorOpen(true)}
              className="flex items-center gap-1.5 rounded-lg border border-indigo-200 bg-indigo-50 px-3 py-1.5 text-xs font-semibold text-indigo-700 hover:bg-indigo-100 transition-colors shadow-2xs cursor-pointer"
              title="Mở máy tính khoa học"
            >
              <span>🖩</span>
              <span>Máy tính khoa học</span>
            </button>
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
            {question?.questionText && /[\\[{^_\\]]/.test(question.questionText) && (
              <MathFormulaPreview
                formula={question.questionText}
                label="Hiển thị công thức Toán (KaTeX)"
                className="mt-3"
              />
            )}
          </div>

          {/* Question-type-specific answer control. Options never contain correctness metadata. */}
          <div className="border-t border-slate-100 pt-6">
            <div className="flex items-center justify-between mb-2">
              <p className="block text-sm font-bold text-slate-900">
                Đáp án cuối cùng của bạn:
              </p>
              {question?.questionType !== "MultipleChoice" && (
                <button
                  type="button"
                  onClick={() => setShowMathToolbar(!showMathToolbar)}
                  className="text-xs font-semibold px-2.5 py-1 rounded bg-indigo-50 hover:bg-indigo-100 text-indigo-700 border border-indigo-200 transition-colors flex items-center gap-1 cursor-pointer"
                >
                  <span>∑ Bảng gõ ký hiệu Toán</span>
                  <span>{showMathToolbar ? "▲" : "▼"}</span>
                </button>
              )}
            </div>

            {showMathToolbar && question?.questionType !== "MultipleChoice" && (
              <div className="mb-3">
                <MathInputToolbar
                  onInsert={(sym) => insertTextAtCursor(sym)}
                />
              </div>
            )}

            {question?.questionType === "MultipleChoice" ? (
              <fieldset className="mt-3 space-y-2" disabled={isSubmitting}>
                <legend className="sr-only">Chọn một đáp án</legend>
                {question.options.map((option) => (
                  <label
                    key={option.optionId}
                    className={`flex cursor-pointer items-start gap-3 rounded-lg border p-3 ${
                      finalAnswer === option.optionId
                        ? "border-indigo-500 bg-indigo-50"
                        : "border-slate-200 hover:bg-slate-50"
                    }`}
                  >
                    <input
                      type="radio"
                      name="answer"
                      value={option.optionId}
                      checked={finalAnswer === option.optionId}
                      onChange={() => handleAnswerChange(option.optionId)}
                      className="mt-1 accent-indigo-600"
                    />
                    <span className="text-sm text-slate-800">
                      <strong>{option.label}.</strong> {option.text}
                    </span>
                  </label>
                ))}
                {question.options.length === 0 && (
                  <p className="rounded-lg bg-amber-50 p-3 text-sm text-amber-800">
                    Câu hỏi trắc nghiệm chưa có lựa chọn khả dụng. Vui lòng báo giáo viên.
                  </p>
                )}
              </fieldset>
            ) : question?.questionType === "Essay" ? (
              <div>
                <textarea
                  ref={essayTextareaRef}
                  rows={7}
                  value={finalAnswer}
                  onFocus={() => setActiveInputTarget("answer")}
                  onChange={(e) => handleAnswerChange(e.target.value)}
                  disabled={isSubmitting}
                  placeholder="Trình bày câu trả lời tự luận của bạn (hỗ trợ công thức LaTeX)..."
                  className="mt-2 w-full rounded-lg border border-slate-300 p-3 text-sm text-slate-900 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 font-mono"
                />
                {finalAnswer.trim() && (
                  <MathFormulaPreview
                    formula={derivedDisplayLatex}
                    label="Xem trước bài làm (KaTeX)"
                    className="mt-2"
                  />
                )}
              </div>
            ) : (
              <div>
                <div className="flex items-center gap-2 mb-1">
                  {question?.answerEvaluationMode === "NumericRational" ? (
                    <span className="text-[11px] font-semibold px-2 py-0.5 rounded bg-blue-50 text-blue-700 border border-blue-200">
                      Chế độ chấm: Số hữu tỉ (hỗ trợ phân số, số thập phân, hỗn số)
                    </span>
                  ) : question?.answerEvaluationMode === "Manual" ? (
                    <span className="text-[11px] font-semibold px-2 py-0.5 rounded bg-amber-50 text-amber-700 border border-amber-200">
                      Chế độ chấm: Giáo viên chấm thủ công
                    </span>
                  ) : (
                    <span className="text-[11px] font-semibold px-2 py-0.5 rounded bg-slate-100 text-slate-700 border border-slate-200">
                      Chế độ chấm: Chuỗi chính xác
                    </span>
                  )}
                </div>
                <input
                  ref={shortAnswerInputRef}
                  type="text"
                  value={finalAnswer}
                  onFocus={() => setActiveInputTarget("answer")}
                  onChange={(e) => handleAnswerChange(e.target.value)}
                  disabled={isSubmitting}
                  placeholder={
                    question?.answerEvaluationMode === "NumericRational"
                      ? "Nhập đáp án số học / phân số (ví dụ: 3/2, 0.75, 1 1/2)..."
                      : "Nhập câu trả lời ngắn..."
                  }
                  className="mt-1 w-full rounded-lg border border-slate-300 px-4 py-2.5 text-slate-900 font-mono focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                />
                {finalAnswer.trim() && (
                  <div className="mt-2 space-y-1">
                    <MathFormulaPreview
                      formula={derivedDisplayLatex}
                      label="Xem trước công thức (KaTeX)"
                    />
                    {question?.answerEvaluationMode === "NumericRational" && derivedDisplayLatex !== finalAnswer && (
                      <p className="text-[11px] text-slate-500">
                        Hiển thị công thức KaTeX: <code className="text-indigo-600">{derivedDisplayLatex}</code> (giá trị chấm: <code className="text-slate-700">{finalAnswer}</code>)
                      </p>
                    )}
                  </div>
                )}
              </div>
            )}
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
              ref={reasoningTextareaRef}
              rows={4}
              value={reasoningText}
              onFocus={() => setActiveInputTarget("reasoning")}
              onChange={(e) => setReasoningText(e.target.value)}
              disabled={isSubmitting}
              placeholder="Mô tả từng bước bạn đã thực hiện để tìm ra kết quả..."
              className="mt-2 w-full rounded-lg border border-slate-300 p-3 text-slate-900 focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500 text-sm"
            />
            {reasoningText.trim() && /[\\[{^_\\]]/.test(reasoningText) && (
              <MathFormulaPreview
                formula={reasoningText}
                label="Xem trước công thức trong lập luận (KaTeX)"
                className="mt-2"
              />
            )}
            {currentUser && (
              <div className="mt-3 flex flex-wrap items-center gap-3 rounded-lg border border-dashed border-indigo-200 bg-indigo-50/50 p-3">
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-semibold text-indigo-950">Bảng vẽ nháp toán học</p>
                  <p className="text-xs text-indigo-800">
                    Vẽ hình, hệ trục hoặc các bước tính. Nháp được lưu cục bộ theo tài khoản và tự khôi phục khi tải lại trang.
                  </p>
                  {scratchpadPngBytes !== null && (
                    <p className="mt-1 text-xs font-medium text-emerald-700">Đã tạo PNG cục bộ ({Math.max(1, Math.ceil(scratchpadPngBytes / 1024))} KB).</p>
                  )}
                </div>
                <button
                  type="button"
                  onClick={() => setIsScratchpadOpen(true)}
                  disabled={isSubmitting}
                  className="rounded-md bg-white px-3 py-2 text-sm font-semibold text-indigo-700 shadow-sm ring-1 ring-inset ring-indigo-200 hover:bg-indigo-100 disabled:opacity-50"
                >
                  Mở bảng nháp
                </button>
              </div>
            )}
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
              disabled={
                isSubmitting ||
                !finalAnswer.trim() ||
                (question?.reasoningRequired === true && !reasoningText.trim()) ||
                (question?.questionType === "MultipleChoice" && question.options.length === 0)
              }
              className="rounded-lg bg-indigo-600 px-6 py-2.5 text-sm font-bold text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
            >
              {isSubmitting ? "Đang nộp bài..." : "Nộp bài & Phân tích"}
            </button>
          </div>
        </div>
      </div>

      {/* Scientific Calculator Drawer */}
      <ScientificCalculatorDrawer
        isOpen={isCalculatorOpen}
        onClose={() => setIsCalculatorOpen(false)}
        onInsertResult={(val) => insertTextAtCursor(val)}
      />
      {currentUser && (
        <ScratchpadCanvasModal
          isOpen={isScratchpadOpen}
          onClose={() => setIsScratchpadOpen(false)}
          centerId={currentUser.centerId}
          userId={currentUser.userId}
          clientSubmissionId={clientSubmissionIdRef.current}
          onExportPng={(png) => {
            setScratchpadPngBytes(png.size);
            setScratchpadPng(png);
            setDrawingUploadToken(null);
          }}
        />
      )}
    </div>
  );
};
