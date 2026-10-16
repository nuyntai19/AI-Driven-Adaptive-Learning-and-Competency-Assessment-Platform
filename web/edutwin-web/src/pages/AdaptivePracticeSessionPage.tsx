import React, { useState, useEffect, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { adaptivePracticeApi } from "../api/adaptivePracticeApi";
import { RichMathText } from "../components/math/RichMathText";
import type {
  AdaptivePracticeHint,
  AdaptivePracticeAnswerFeedback,
} from "../types/adaptivePractice";

export const AdaptivePracticeSessionPage: React.FC = () => {
  const { sessionId } = useParams<{ sessionId: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [selectedOption, setSelectedOption] = useState<string>("");
  const [textAnswer, setTextAnswer] = useState<string>("");
  const [reasoningText, setReasoningText] = useState<string>("");
  const [activeHint, setActiveHint] = useState<AdaptivePracticeHint | null>(null);
  const [hintLoading, setHintLoading] = useState<boolean>(false);
  const [isHintOpen, setIsHintOpen] = useState<boolean>(false);
  const [feedback, setFeedback] = useState<AdaptivePracticeAnswerFeedback | null>(null);
  const [isPaused, setIsPaused] = useState<boolean>(false);
  const [showExitConfirm, setShowExitConfirm] = useState<boolean>(false);
  const [elapsedSeconds, setElapsedSeconds] = useState<number>(0);

  const startTimeRef = useRef<number>(Date.now());

  // 1. Fetch Session Info
  const sessionQuery = useQuery({
    queryKey: ["adaptive-practice", "session", sessionId],
    queryFn: () => adaptivePracticeApi.getSessionById(sessionId!),
    enabled: !!sessionId,
    refetchOnWindowFocus: false,
  });
  const session = sessionQuery.data;

  // 2. Fetch Current Question
  const questionQuery = useQuery({
    queryKey: ["adaptive-practice", "question", sessionId],
    queryFn: () => adaptivePracticeApi.getNextQuestion(sessionId!),
    enabled: !!sessionId && session?.status === "InProgress" && !feedback,
    refetchOnWindowFocus: false,
  });
  const question = questionQuery.data;

  // Timer effect
  useEffect(() => {
    if (session?.status === "InProgress" && !isPaused) {
      const interval = setInterval(() => {
        setElapsedSeconds((prev) => prev + 1);
      }, 1000);
      return () => clearInterval(interval);
    }
  }, [session?.status, isPaused]);

  // Reset inputs when a new question arrives
  useEffect(() => {
    if (question) {
      setSelectedOption("");
      setTextAnswer("");
      setReasoningText("");
      setActiveHint(null);
      setIsHintOpen(false);
      setFeedback(null);
      startTimeRef.current = Date.now();
    }
  }, [question?.sessionItemId]);

  // Submit Answer Mutation
  const submitMutation = useMutation({
    mutationFn: async () => {
      if (!question || !sessionId) return;
      const finalAnswer = question.questionType === "MultipleChoice" ? selectedOption : textAnswer;
      const timeSpent = Math.max(1, Math.round((Date.now() - startTimeRef.current) / 1000));

      return await adaptivePracticeApi.submitAnswer(sessionId, {
        clientSubmissionId: crypto.randomUUID(),
        questionId: question.questionId,
        sessionItemId: question.sessionItemId,
        finalAnswer,
        reasoningText: reasoningText || undefined,
        timeSpentSeconds: timeSpent,
      });
    },
    onSuccess: (data) => {
      if (data) {
        setFeedback(data);
        queryClient.invalidateQueries({ queryKey: ["adaptive-practice", "session", sessionId] });
      }
    },
  });

  // Fetch Progressive Hint
  const handleFetchHint = async (level: number) => {
    if (!sessionId || !question) return;
    setHintLoading(true);
    try {
      const hint = await adaptivePracticeApi.getHint(sessionId, question.sessionItemId, level);
      setActiveHint(hint);
      setIsHintOpen(true);
    } catch {
      // ignore
    } finally {
      setHintLoading(false);
    }
  };

  // Pause Session
  const handlePause = async () => {
    if (!sessionId) return;
    await adaptivePracticeApi.pauseSession(sessionId);
    setIsPaused(true);
  };

  // Resume Session
  const handleResume = async () => {
    if (!sessionId) return;
    await adaptivePracticeApi.resumeSession(sessionId);
    setIsPaused(false);
    queryClient.invalidateQueries({ queryKey: ["adaptive-practice", "session", sessionId] });
  };

  // Close Session voluntarily
  const handleStopSession = async () => {
    if (!sessionId) return;
    await adaptivePracticeApi.closeSession(sessionId, "StudentStopped");
    queryClient.invalidateQueries({ queryKey: ["adaptive-practice"] });
    navigate(`/hoc-tap/luyen-tap/phien/${sessionId}/tong-ket`);
  };

  // Advance to next question or summary
  const handleNextQuestion = () => {
    if (feedback?.isSessionClosed) {
      navigate(`/hoc-tap/luyen-tap/phien/${sessionId}/tong-ket`);
    } else {
      setFeedback(null);
      queryClient.invalidateQueries({ queryKey: ["adaptive-practice", "question", sessionId] });
    }
  };

  // Formatting time display
  const formatTime = (secs: number) => {
    const mins = Math.floor(secs / 60);
    const rem = secs % 60;
    return `${mins}:${rem < 10 ? "0" : ""}${rem}`;
  };

  if (sessionQuery.isLoading || questionQuery.isLoading) {
    return (
      <div className="min-h-screen bg-slate-50 dark:bg-slate-950 flex flex-col items-center justify-center p-6 space-y-4">
        <div className="w-12 h-12 border-4 border-indigo-500 border-t-transparent rounded-full animate-spin" />
        <p className="text-sm font-semibold text-slate-600 dark:text-slate-300">
          Đang chuẩn bị câu hỏi thích ứng tiếp theo...
        </p>
      </div>
    );
  }

  if (session?.status === "Closed" && !feedback) {
    return (
      <div className="min-h-screen bg-slate-50 dark:bg-slate-950 flex flex-col items-center justify-center p-6 space-y-4">
        <h2 className="text-xl font-bold text-slate-800 dark:text-white">
          Phiên luyện tập đã kết thúc
        </h2>
        <button
          type="button"
          onClick={() => navigate(`/hoc-tap/luyen-tap/phien/${sessionId}/tong-ket`)}
          className="px-6 py-2.5 rounded-xl bg-indigo-600 text-white font-semibold cursor-pointer"
        >
          Xem kết quả phiên
        </button>
      </div>
    );
  }

  const answerEntered = question?.questionType === "MultipleChoice" ? !!selectedOption : textAnswer.trim().length > 0;

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-slate-950 text-slate-900 dark:text-white flex flex-col font-sans antialiased">
      {/* Top Navigation & Session Status Bar */}
      <header className="sticky top-0 z-30 bg-white/95 dark:bg-slate-900/95 backdrop-blur-md border-b border-slate-200 dark:border-slate-800 shadow-2xs">
        <div className="w-full max-w-6xl mx-auto px-4 sm:px-6 h-16 flex items-center justify-between gap-4">
          {/* Topic & Goal Info */}
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <span className="text-xs font-bold text-indigo-600 dark:text-indigo-400 truncate max-w-[200px] sm:max-w-xs">
                {session?.topicName}
              </span>
              <span className="hidden sm:inline-block text-slate-300 dark:text-slate-700">·</span>
              <span className="text-[11px] font-medium text-slate-500 dark:text-slate-400 hidden sm:inline">
                Mục tiêu: Đạt {session?.targetMastery.toFixed(0)}% năng lực
              </span>
            </div>
            {/* Dynamic Question Counter (BR-26) */}
            <div className="text-xs font-bold text-slate-800 dark:text-slate-200">
              Câu {question?.sequenceNumber || session?.questionCount || 1} · dự kiến {session?.minimumQuestions}–{session?.maximumQuestions} câu
            </div>
          </div>

          {/* Timer & Session Control Actions */}
          <div className="flex items-center gap-3 shrink-0">
            <div className="px-3 py-1 rounded-lg bg-slate-100 dark:bg-slate-800 text-xs font-mono font-bold text-slate-700 dark:text-slate-300 border border-slate-200 dark:border-slate-700">
              ⏱ {formatTime(elapsedSeconds)}
            </div>

            <button
              type="button"
              onClick={handlePause}
              className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-300 transition-colors cursor-pointer"
            >
              Tạm dừng
            </button>

            <button
              type="button"
              onClick={() => setShowExitConfirm(true)}
              className="px-3 py-1.5 rounded-lg text-xs font-semibold text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/40 transition-colors cursor-pointer"
            >
              Dừng phiên
            </button>
          </div>
        </div>
      </header>

      {/* Main Workspace Area */}
      <main className="flex-1 w-full max-w-4xl mx-auto px-4 sm:px-6 py-8 space-y-6">
        {question && (
          <div className="p-6 sm:p-8 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800/80 shadow-xs space-y-6">
            {/* Selection Reason Pill & Difficulty Badge */}
            <div className="flex flex-wrap items-center justify-between gap-2 pb-4 border-b border-slate-100 dark:border-slate-800">
              <div className="flex items-center gap-2">
                <span
                  className={`px-3 py-1 rounded-full text-xs font-bold ${
                    question.isPrerequisiteCheck
                      ? "bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-300 border border-amber-300 dark:border-amber-700"
                      : "bg-indigo-50 text-indigo-700 dark:bg-indigo-950 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-800"
                  }`}
                >
                  {question.selectionReasonText}
                </span>
              </div>

              <div className="flex items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400 font-semibold">
                <span>Độ khó:</span>
                <span className="font-bold text-indigo-600 dark:text-indigo-400">
                  {"★".repeat(question.difficulty)}{"☆".repeat(5 - question.difficulty)}
                </span>
              </div>
            </div>

            {/* Question Text */}
            <div className="text-base sm:text-lg leading-relaxed text-slate-800 dark:text-slate-100 font-medium space-y-3">
              <RichMathText content={question.questionText} text={question.questionText} />
            </div>

            {/* Answer Options / Inputs */}
            {!feedback && (
              <div className="pt-4 space-y-4">
                {question.questionType === "MultipleChoice" && question.options.length > 0 ? (
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    {question.options.map((opt) => {
                      const isSelected = selectedOption === opt.label || selectedOption === opt.optionId;
                      return (
                        <button
                          key={opt.optionId}
                          type="button"
                          onClick={() => setSelectedOption(opt.label)}
                          className={`p-4 rounded-2xl border text-left flex items-start gap-3 transition-all cursor-pointer ${
                            isSelected
                              ? "border-indigo-600 bg-indigo-50/70 dark:bg-indigo-950/60 ring-2 ring-indigo-500/20 text-indigo-950 dark:text-indigo-100"
                              : "border-slate-200 dark:border-slate-800 hover:border-slate-300 dark:hover:border-slate-700 bg-white dark:bg-slate-900 text-slate-800 dark:text-slate-200"
                          }`}
                        >
                          <span
                            className={`w-7 h-7 rounded-xl flex items-center justify-center font-bold text-xs shrink-0 ${
                              isSelected
                                ? "bg-indigo-600 text-white"
                                : "bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300"
                            }`}
                          >
                            {opt.label}
                          </span>
                          <div className="text-sm pt-0.5 leading-snug">
                            <RichMathText content={opt.text} />
                          </div>
                        </button>
                      );
                    })}
                  </div>
                ) : (
                  <div className="space-y-3">
                    <label htmlFor="text-ans" className="text-xs font-bold text-slate-700 dark:text-slate-300">
                      Nhập đáp án của bạn:
                    </label>
                    <input
                      id="text-ans"
                      type="text"
                      value={textAnswer}
                      onChange={(e) => setTextAnswer(e.target.value)}
                      placeholder="Nhập số, biểu thức hoặc kết quả..."
                      className="w-full px-4 py-3 rounded-xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-900 dark:text-white text-base focus:outline-indigo-500"
                    />
                  </div>
                )}

                {/* Optional Reasoning Text Field */}
                <div className="pt-2">
                  <details className="text-xs text-slate-500 dark:text-slate-400">
                    <summary className="cursor-pointer hover:text-slate-700 dark:hover:text-slate-200 font-semibold select-none">
                      + Thêm lời giải thích / lập luận (không bắt buộc)
                    </summary>
                    <textarea
                      value={reasoningText}
                      onChange={(e) => setReasoningText(e.target.value)}
                      placeholder="Ghi lại các bước suy luận của bạn..."
                      rows={3}
                      className="w-full mt-2 p-3 rounded-xl border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-800/50 text-slate-800 dark:text-slate-200 text-xs focus:outline-indigo-500"
                    />
                  </details>
                </div>

                {/* Action Bar: Hints & Submit */}
                <div className="pt-4 flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 dark:border-slate-800">
                  {/* Progressive Hint Button */}
                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      onClick={() => handleFetchHint(1)}
                      disabled={hintLoading}
                      className="px-4 py-2 rounded-xl text-xs font-semibold bg-amber-50 hover:bg-amber-100 dark:bg-amber-950/40 dark:hover:bg-amber-950/60 text-amber-800 dark:text-amber-300 border border-amber-300 dark:border-amber-800 transition-colors flex items-center gap-1.5 cursor-pointer"
                    >
                      <span>💡</span>
                      <span>{hintLoading ? "Đang chuẩn bị gợi ý..." : "Gợi ý thông minh"}</span>
                    </button>
                    {question.hintsUsedCount > 0 && (
                      <span className="text-[11px] text-slate-400">
                        (Đã dùng {question.hintsUsedCount} mức)
                      </span>
                    )}
                  </div>

                  {/* Submit Button */}
                  <button
                    type="button"
                    onClick={() => submitMutation.mutate()}
                    disabled={!answerEntered || submitMutation.isPending}
                    className="px-8 py-3 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-sm shadow-md hover:shadow-indigo-500/25 transition-all cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed flex items-center gap-2"
                  >
                    {submitMutation.isPending ? "Đang chấm..." : "Nộp câu trả lời"}
                  </button>
                </div>
              </div>
            )}

            {/* Instant Answer Feedback Area */}
            {feedback && (
              <div className="pt-4 space-y-4 border-t border-slate-100 dark:border-slate-800 animate-fadeIn">
                <div
                  className={`p-5 rounded-2xl border flex items-start gap-4 ${
                    feedback.isCorrect
                      ? "bg-emerald-50/80 dark:bg-emerald-950/40 border-emerald-300 dark:border-emerald-800/80 text-emerald-900 dark:text-emerald-200"
                      : "bg-rose-50/80 dark:bg-rose-950/40 border-rose-300 dark:border-rose-800/80 text-rose-900 dark:text-rose-200"
                  }`}
                >
                  <div
                    className={`w-10 h-10 rounded-full flex items-center justify-center font-bold text-lg shrink-0 ${
                      feedback.isCorrect ? "bg-emerald-600 text-white" : "bg-rose-600 text-white"
                    }`}
                  >
                    {feedback.isCorrect ? "✓" : "✗"}
                  </div>
                  <div className="space-y-1.5 flex-1 min-w-0">
                    <h4 className="text-base font-bold">
                      {feedback.isCorrect ? "Chính xác!" : "Chưa chính xác"}
                    </h4>
                    <p className="text-xs leading-relaxed opacity-90">
                      {feedback.stepGuidance}
                    </p>
                    {feedback.misconceptionHypothesis && (
                      <div className="mt-2 p-2.5 rounded-lg bg-amber-50 dark:bg-amber-950/60 border border-amber-200 dark:border-amber-800 text-[11px] text-amber-800 dark:text-amber-200">
                        <strong>Phân tích dấu hiệu:</strong> {feedback.misconceptionHypothesis}
                      </div>
                    )}
                    <div className="pt-2 text-xs font-semibold">
                      Năng lực ước lượng hiện tại: <strong>{feedback.updatedEstimatedMastery.toFixed(0)}%</strong>
                    </div>
                  </div>
                </div>

                <div className="flex justify-end pt-2">
                  <button
                    type="button"
                    onClick={handleNextQuestion}
                    className="px-8 py-3 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-sm shadow-md transition-all cursor-pointer flex items-center gap-2"
                  >
                    <span>{feedback.isSessionClosed ? "Xem tổng kết phiên luyện tập" : "Câu tiếp theo"}</span>
                    <span>→</span>
                  </button>
                </div>
              </div>
            )}
          </div>
        )}
      </main>

      {/* Progressive Hint Drawer / Modal */}
      {isHintOpen && activeHint && (
        <div className="fixed inset-0 z-50 bg-slate-900/40 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="w-full max-w-lg rounded-3xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 p-6 space-y-4 shadow-xl">
            <div className="flex items-center justify-between pb-2 border-b border-slate-100 dark:border-slate-800">
              <div className="flex items-center gap-2">
                <span className="text-xl">💡</span>
                <h3 className="text-base font-bold text-slate-900 dark:text-white">
                  {activeHint.hintTitle}
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setIsHintOpen(false)}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 font-bold text-lg"
              >
                ✕
              </button>
            </div>

            <div className="p-4 rounded-xl bg-slate-50 dark:bg-slate-800/60 text-xs text-slate-800 dark:text-slate-200 leading-relaxed whitespace-pre-wrap">
              <RichMathText text={activeHint.hintContent} />
            </div>

            <div className="text-[11px] text-amber-700 dark:text-amber-400 italic">
              ℹ {activeHint.supportLevelDescription}
            </div>

            {/* Stepped Hint Escalation */}
            <div className="pt-2 flex items-center justify-between gap-2 border-t border-slate-100 dark:border-slate-800">
              {activeHint.hintLevel < 3 ? (
                <button
                  type="button"
                  onClick={() => handleFetchHint(activeHint.hintLevel + 1)}
                  disabled={hintLoading}
                  className="px-4 py-2 rounded-xl text-xs font-semibold bg-amber-100 hover:bg-amber-200 dark:bg-amber-950 dark:hover:bg-amber-900 text-amber-900 dark:text-amber-200 transition-colors"
                >
                  {hintLoading ? "Đang chuẩn bị..." : `Xem gợi ý mức ${activeHint.hintLevel + 1} (Chi tiết hơn)`}
                </button>
              ) : (
                <span className="text-[11px] text-slate-400">Đã đạt mức gợi ý cao nhất</span>
              )}

              <button
                type="button"
                onClick={() => setIsHintOpen(false)}
                className="px-5 py-2 rounded-xl bg-slate-900 hover:bg-slate-800 dark:bg-slate-100 dark:hover:bg-slate-200 text-white dark:text-slate-900 font-bold text-xs"
              >
                Đã hiểu, quay lại giải
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Pause Modal */}
      {isPaused && (
        <div className="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="w-full max-w-md rounded-3xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 p-6 text-center space-y-4 shadow-xl">
            <div className="w-12 h-12 mx-auto rounded-full bg-amber-100 dark:bg-amber-950/60 flex items-center justify-center text-xl text-amber-600">
              ⏸
            </div>
            <h3 className="text-lg font-bold text-slate-900 dark:text-white">
              Phiên luyện tập đang tạm dừng
            </h3>
            <p className="text-xs text-slate-500 dark:text-slate-400">
              Tiến độ câu hỏi và thời gian làm bài của bạn được bảo lưu an toàn.
            </p>
            <div className="pt-2 flex items-center justify-center gap-3">
              <button
                type="button"
                onClick={handleResume}
                className="px-6 py-2.5 rounded-xl bg-indigo-600 text-white font-bold text-xs shadow-md cursor-pointer"
              >
                Tiếp tục ngay
              </button>
              <button
                type="button"
                onClick={() => navigate("/hoc-tap/luyen-tap")}
                className="px-4 py-2.5 rounded-xl text-xs font-semibold text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800"
              >
                Rời khỏi trang
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Confirm Exit Modal */}
      {showExitConfirm && (
        <div className="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="w-full max-w-md rounded-3xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 p-6 space-y-4 shadow-xl">
            <h3 className="text-base font-bold text-slate-900 dark:text-white">
              Bạn có chắc chắn muốn dừng phiên luyện tập?
            </h3>
            <p className="text-xs text-slate-500 dark:text-slate-400">
              Kết quả của các câu bạn đã làm sẽ được ghi nhận và tạo báo cáo tổng kết tiến độ hiện tại.
            </p>
            <div className="pt-2 flex justify-end gap-3">
              <button
                type="button"
                onClick={() => setShowExitConfirm(false)}
                className="px-4 py-2 rounded-xl text-xs font-semibold text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                Quay lại làm tiếp
              </button>
              <button
                type="button"
                onClick={handleStopSession}
                className="px-5 py-2 rounded-xl bg-rose-600 hover:bg-rose-700 text-white font-bold text-xs cursor-pointer shadow-md"
              >
                Dừng và xem kết quả
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
