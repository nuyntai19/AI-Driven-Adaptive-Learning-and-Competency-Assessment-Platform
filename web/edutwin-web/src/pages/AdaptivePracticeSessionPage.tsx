import React, { useState, useEffect, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { adaptivePracticeApi } from "../api/adaptivePracticeApi";
import { RichMathText } from "../components/math/RichMathText";
import { ScratchpadInlinePanel, type ScratchpadInlinePanelHandle } from "../components/math/ScratchpadInlinePanel";
import { transcribeHandwriting } from "../api/handwritingApi";
import { readScratchpadPhoto } from "../utils/scratchpadPngImport";
import { deriveFinalAnswer } from "../utils/finalAnswer";
import { answerReasoningPlaceholder, answerWorkPlaceholder, usesMathInput } from "../utils/answerPlaceholders";
import { QuestionImage } from "../components/QuestionImage";
import { WorkToolbar } from "../components/learning/WorkToolbar";
import { BookmarkButton } from "../components/learning/BookmarkButton";
import { GradedAnswerPreview } from "../components/learning/GradedAnswerPreview";
import { useWorkInputMode } from "../hooks/useWorkInputMode";
import { WorkTextBox } from "../components/learning/WorkTextBox";
import type {
  AdaptivePracticeHint,
  AdaptivePracticeAnswerFeedback,
} from "../types/adaptivePractice";

const PANEL = "rounded-2xl border border-slate-200/80 bg-white p-4 sm:p-5 dark:border-slate-800 dark:bg-[#0f172a]";
const PRESS = "transition-[background-color,color,transform] duration-150 ease-out active:scale-[0.97] motion-reduce:transform-none";
const PRIMARY = `rounded-lg bg-indigo-600 hover:bg-indigo-700 px-5 py-2.5 text-sm font-semibold text-white cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed ${PRESS}`;
const SECONDARY = `rounded-lg border border-slate-300 px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-100 cursor-pointer disabled:opacity-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800 ${PRESS}`;

const questionStartKey = (itemId: string) => `edutwin-practice-question-start:${itemId}`;

function rememberQuestionStart(itemId: string, startedAt: number): void {
  try {
    sessionStorage.setItem(questionStartKey(itemId), String(startedAt));
  } catch {
    // Without storage a reload only restarts this question's clock.
  }
}

/** When this question was first shown, kept across a page reload. */
function questionStartedAt(itemId: string): number {
  let stored = 0;
  try {
    stored = Number(sessionStorage.getItem(questionStartKey(itemId))) || 0;
  } catch {
    stored = 0;
  }
  const startedAt = stored > 0 ? stored : Date.now();
  rememberQuestionStart(itemId, startedAt);
  return startedAt;
}

export const AdaptivePracticeSessionPage: React.FC = () => {
  const { sessionId } = useParams<{ sessionId: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [selectedOption, setSelectedOption] = useState<string>("");
  const [reasoningText, setReasoningText] = useState<string>("");
  const [activeHint, setActiveHint] = useState<AdaptivePracticeHint | null>(null);
  const [highestHintRead, setHighestHintRead] = useState<number>(0);
  const [hintLoading, setHintLoading] = useState<boolean>(false);
  const [isHintOpen, setIsHintOpen] = useState<boolean>(false);
  const [feedback, setFeedback] = useState<AdaptivePracticeAnswerFeedback | null>(null);
  const [isPaused, setIsPaused] = useState<boolean>(false);
  const [showExitConfirm, setShowExitConfirm] = useState<boolean>(false);
  const [isStopping, setIsStopping] = useState<boolean>(false);
  const [stopError, setStopError] = useState<string | null>(null);
  const [elapsedSeconds, setElapsedSeconds] = useState<number>(0);

  // Ba phương thức nhập: bàn phím / vẽ trên A4 / ảnh chụp — tất cả cùng
  // ghi vào một ô bài làm duy nhất như trang làm bài tập.
  const [isScanning, setIsScanning] = useState<boolean>(false);
  const [scanNotice, setScanNotice] = useState<string | null>(null);
  const [scanError, setScanError] = useState<string | null>(null);
  const [photoDataUrl, setPhotoDataUrl] = useState<string | null>(null);
  const boardRef = useRef<ScratchpadInlinePanelHandle>(null);
  const photoInputRef = useRef<HTMLInputElement>(null);

  const startTimeRef = useRef<number>(Date.now());
  const pausedAtRef = useRef<number | null>(null);
  // One id per question, so a retry after a lost response stays idempotent.
  const submissionIdRef = useRef<string | null>(null);

  // 1. Fetch Session Info
  const sessionQuery = useQuery({
    queryKey: ["adaptive-practice", "session", sessionId],
    queryFn: () => adaptivePracticeApi.getSessionById(sessionId!),
    enabled: !!sessionId,
    refetchOnWindowFocus: false,
  });
  const session = sessionQuery.data;
  // Remembered per subject, keyboard first (see useWorkInputMode).
  const [inputMode, setInputMode] = useWorkInputMode(session?.subjectId);

  // 2. Fetch Current Question
  const questionQuery = useQuery({
    queryKey: ["adaptive-practice", "question", sessionId],
    queryFn: () => adaptivePracticeApi.getNextQuestion(sessionId!),
    enabled: !!sessionId && session?.status === "InProgress" && !feedback,
    refetchOnWindowFocus: false,
  });
  const question = questionQuery.data;

  // The server closes the session when no question is left, so the cached
  // status is stale; refresh it to show the ended state instead of nothing.
  const questionAnsweredEmpty = questionQuery.isSuccess && !questionQuery.data && !feedback;
  const refetchSession = sessionQuery.refetch;
  useEffect(() => {
    if (questionAnsweredEmpty) void refetchSession();
  }, [questionAnsweredEmpty, questionQuery.dataUpdatedAt, refetchSession]);

  // The server's count survives a reload; the page only ticks between syncs.
  useEffect(() => {
    if (session) setElapsedSeconds(session.elapsedSeconds);
  }, [session?.elapsedSeconds]); // eslint-disable-line react-hooks/exhaustive-deps

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
      setReasoningText("");
      setActiveHint(null);
      setIsHintOpen(false);
      setFeedback(null);
      setPhotoDataUrl(null);
      setScanNotice(null);
      setScanError(null);
      setHighestHintRead(0);
      startTimeRef.current = questionStartedAt(String(question.sessionItemId));
      submissionIdRef.current = null;
    }
  }, [question?.sessionItemId]);

  // Submit Answer Mutation
  const submitMutation = useMutation({
    mutationFn: async () => {
      if (!question || !sessionId) return;
      // One single work box: the submitted answer is derived from the work
      // — the whole text for essays and language subjects, its last non-empty line otherwise.
      const derivedAnswer = deriveFinalAnswer(reasoningText, question.questionType, question.answerEvaluationMode, !usesMathInput(session?.subjectName));
      const finalAnswer = question.questionType === "MultipleChoice" && question.options.length > 0 ? selectedOption : derivedAnswer;
      const timeSpent = Math.max(1, Math.round((Date.now() - startTimeRef.current) / 1000));

      return await adaptivePracticeApi.submitAnswer(sessionId, {
        clientSubmissionId: (submissionIdRef.current ??= crypto.randomUUID()),
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
    // The session may have ended meanwhile (time up, stopped in another tab): reload it so the ended screen shows.
    onError: () => { void queryClient.invalidateQueries({ queryKey: ["adaptive-practice", "session", sessionId] }); },
  });

  // Đưa bản chép vào ô bài làm duy nhất; đáp án nộp đi được suy ra từ bài
  // làm khi bấm nộp (tự luận lấy toàn bộ, trả lời ngắn lấy dòng cuối).
  const applyScan = (text: string) => {
    const lines = text.split("\n").map((s) => s.trim()).filter(Boolean);
    if (!lines.length) {
      setScanError("Không đọc được chữ viết trong ảnh. Hãy viết rõ hơn hoặc gõ bằng bàn phím.");
      return;
    }
    setReasoningText(text.trim());
    setScanNotice("Đã quét nội dung vào bài làm. Hãy kiểm tra lại trước khi nộp.");
  };

  const runScan = async (imageDataUrl: string) => {
    setIsScanning(true);
    setScanError(null);
    setScanNotice(null);
    try {
      applyScan(await transcribeHandwriting(imageDataUrl));
    } catch (e) {
      const err = e as { response?: { data?: { detail?: string } } };
      setScanError(err?.response?.data?.detail || "Không thể nhận dạng chữ viết lúc này. Hãy thử lại hoặc gõ bằng bàn phím.");
    } finally {
      setIsScanning(false);
    }
  };

  const handleScanBoard = async () => {
    if (isScanning) return;
    const snap = boardRef.current ? await boardRef.current.exportSnapshot() : null;
    if (!snap) {
      setScanError("Hãy viết bài giải lên giấy A4 trước khi quét.");
      return;
    }
    setPhotoDataUrl(snap.dataUrl);
    await runScan(snap.dataUrl);
  };

  const handlePhotoFile = async (file: File | null) => {
    if (!file || isScanning) return;
    try {
      const { dataUrl } = await readScratchpadPhoto(file);
      setPhotoDataUrl(dataUrl);
      await runScan(dataUrl);
    } catch (e) {
      setScanError(e instanceof Error ? e.message : "Không đọc được tệp ảnh.");
    }
  };

  // Fetch Progressive Hint
  const handleFetchHint = async (level: number) => {
    if (!sessionId || !question) return;
    setHintLoading(true);
    try {
      const hint = await adaptivePracticeApi.getHint(sessionId, question.sessionItemId, level);
      setActiveHint(hint);
      setIsHintOpen(true);
      setHighestHintRead((current) => Math.max(current, hint.hintLevel));
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
    pausedAtRef.current = Date.now();
    setIsPaused(true);
  };

  // Resume Session
  const handleResume = async () => {
    if (!sessionId) return;
    await adaptivePracticeApi.resumeSession(sessionId);
    if (pausedAtRef.current !== null && question) {
      startTimeRef.current += Date.now() - pausedAtRef.current;
      rememberQuestionStart(String(question.sessionItemId), startTimeRef.current);
      pausedAtRef.current = null;
    }
    setIsPaused(false);
    queryClient.invalidateQueries({ queryKey: ["adaptive-practice", "session", sessionId] });
  };

  // Close Session voluntarily
  const handleStopSession = async () => {
    if (!sessionId || isStopping) return;
    setIsStopping(true);
    setStopError(null);
    try {
      await adaptivePracticeApi.closeSession(sessionId);
      queryClient.invalidateQueries({ queryKey: ["adaptive-practice"] });
      navigate(`/hoc-tap/luyen-tap/phien/${sessionId}/tong-ket`);
    } catch {
      setStopError("Chưa dừng được phiên. Hãy thử lại.");
    } finally {
      setIsStopping(false);
    }
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
      <div className="min-h-screen bg-stone-50/50 dark:bg-[#101623] student-shell flex flex-col items-center justify-center p-6 space-y-4">
        <div className="w-12 h-12 border-4 border-indigo-500 border-t-transparent rounded-full animate-spin" />
        <p className="text-sm font-semibold text-slate-600 dark:text-slate-300">
          Đang chuẩn bị câu hỏi thích ứng tiếp theo...
        </p>
      </div>
    );
  }

  if (session?.status === "Closed" && !feedback) {
    return (
      <div className="min-h-screen bg-stone-50/50 dark:bg-[#101623] student-shell flex flex-col items-center justify-center p-6 space-y-4">
        <h2 className="text-xl font-bold text-slate-800 dark:text-white">
          Phiên luyện tập đã kết thúc
        </h2>
        {session.closeReason === "OutOfQuestions" && (
          <p className="text-sm text-slate-500 dark:text-slate-400 max-w-md text-center">
            Phiên kết thúc sớm vì chủ đề này đã hết câu hỏi khả dụng cho bạn. Hãy quay lại sau hoặc chọn chủ đề khác để luyện tập.
          </p>
        )}
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

  const mathInput = usesMathInput(session?.subjectName);
  const isMcq = question?.questionType === "MultipleChoice" && question.options.length > 0;
  // Follow what is on screen: a multiple-choice question that arrives without options shows the text box, so the
  // typed answer must be enough to submit (otherwise the button stays disabled forever).
  const answerEntered = isMcq ? !!selectedOption : reasoningText.trim().length > 0;
  // The A4 sheet takes the whole right column, so the typed work moves beside it.
  const boardBeside = inputMode === "draw" && !isMcq && !feedback;
  const correctOption = isMcq
    ? question?.options.find((o) => o.optionId === feedback?.correctAnswer || o.label === feedback?.correctAnswer)
    : undefined;
  const correctAnswerText = correctOption ? `${correctOption.label}. ${correctOption.text}` : feedback?.correctAnswer;

  const workEditor = (minHeight: string) => question && (
    <div className="space-y-2">
      <label htmlFor="work-text" className="block text-sm font-semibold text-slate-700 dark:text-slate-300">
        {inputMode === "keyboard" ? "Bài làm của bạn" : "Bài làm của bạn (kết quả quét, hãy kiểm tra lại)"}
      </label>
      <WorkTextBox
        math={mathInput}
        id="work-text"
        label="Bài làm của bạn"
        value={reasoningText}
        onChange={setReasoningText}
        placeholder={answerWorkPlaceholder(mathInput)}
        minHeight={minHeight}
      />
      <GradedAnswerPreview
        answer={deriveFinalAnswer(reasoningText, question.questionType, question.answerEvaluationMode, !mathInput)}
        questionType={question.questionType}
        evaluationMode={question.answerEvaluationMode}
        wholeWork={!mathInput}
      />
    </div>
  );

  const actionBar = question && (
    <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 pt-3 dark:border-slate-800">
      <div className="flex items-center gap-2">
        <button type="button" onClick={() => handleFetchHint(1)} disabled={hintLoading} className={SECONDARY}>
          {hintLoading ? "Đang chuẩn bị gợi ý..." : "Gợi ý thông minh"}
        </button>
        {Math.max(question.hintsUsedCount, highestHintRead) > 0 && (
          <span className="text-xs text-slate-500 dark:text-slate-400">Đã dùng {Math.max(question.hintsUsedCount, highestHintRead)} mức</span>
        )}
      </div>
      <button type="button" onClick={() => submitMutation.mutate()} disabled={!answerEntered || submitMutation.isPending} className={PRIMARY}>
        {submitMutation.isPending ? "Đang chấm..." : "Nộp câu trả lời"}
      </button>
    </div>
  );

  return (
    <div className="min-h-screen bg-stone-50/50 dark:bg-[#101623] text-slate-900 dark:text-white flex flex-col antialiased student-shell">
      <header className="sticky top-0 z-30 bg-white/95 dark:bg-[#151d2f]/95 backdrop-blur-md border-b border-slate-200 dark:border-slate-800">
        <div className="w-full max-w-[1700px] mx-auto px-4 sm:px-6 h-16 flex items-center justify-between gap-4">
          <div className="min-w-0">
            <div className="truncate text-sm font-bold text-slate-900 dark:text-white">{session?.topicName}</div>
            <div className="text-xs tabular-nums text-slate-500 dark:text-slate-400">
              Câu {question?.sequenceNumber || session?.questionCount || 1} · dự kiến {session?.minimumQuestions}–{session?.maximumQuestions} câu · mục tiêu {session?.targetMastery.toFixed(0)}%
            </div>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            <span className="px-2 text-sm font-semibold tabular-nums text-slate-700 dark:text-slate-300" aria-label="Thời gian đã làm trên thời gian dự kiến">{formatTime(elapsedSeconds)} / {session?.timeBudgetMinutes}:00</span>
            <button type="button" onClick={handlePause} disabled={isPaused || session?.status !== "InProgress"} className={SECONDARY}>Tạm dừng</button>
            <button type="button" onClick={() => setShowExitConfirm(true)} className={`rounded-lg px-3 py-2 text-xs font-semibold text-rose-700 hover:bg-rose-50 cursor-pointer dark:text-rose-400 dark:hover:bg-rose-950/40 ${PRESS}`}>
              Dừng phiên
            </button>
          </div>
        </div>
      </header>

      <main className="flex-1 w-full max-w-[1700px] mx-auto px-4 sm:px-6 py-3 space-y-3">
        {!question && !feedback && (questionQuery.isError || session?.status === "Paused") && (
          <div role={questionQuery.isError ? "alert" : "status"} className={`${PANEL} text-sm`}>
            <p className="font-semibold text-slate-900 dark:text-white">
              {questionQuery.isError ? "Không tải được câu hỏi tiếp theo." : "Phiên luyện tập đang tạm dừng."}
            </p>
            <button
              type="button"
              onClick={() => (questionQuery.isError ? void questionQuery.refetch() : void handleResume())}
              className={`mt-3 ${PRIMARY}`}
            >
              {questionQuery.isError ? "Thử lại" : "Tiếp tục luyện tập"}
            </button>
          </div>
        )}

        {question && (
          <div className="grid grid-cols-1 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)] gap-3 items-start">
            <section aria-label="Đề bài" className={`${PANEL} min-w-0 space-y-4 lg:sticky lg:top-20`}>
              <div className="flex flex-wrap items-start justify-between gap-x-4 gap-y-1 text-xs">
                <p className={question.isPrerequisiteCheck ? "font-semibold text-amber-700 dark:text-amber-400" : "text-slate-500 dark:text-slate-400"}>
                  {question.selectionReasonText}
                </p>
                <span className="flex shrink-0 items-center gap-3 tabular-nums text-slate-500 dark:text-slate-400">
                  Độ khó {question.difficulty}/5
                  <BookmarkButton questionId={question.questionId} />
                </span>
              </div>

              <div className="space-y-3 text-base sm:text-lg font-semibold leading-relaxed text-slate-900 dark:text-white">
                <RichMathText content={question.questionText} text={question.questionText} />
                <QuestionImage questionId={question.questionId} hasImage={question.hasImage} />
              </div>

              {isHintOpen && activeHint && (
                <div className="space-y-2 border-t border-slate-100 pt-3 dark:border-slate-800">
                  <div className="flex items-baseline justify-between gap-3">
                    <h2 className="text-sm font-semibold text-amber-700 dark:text-amber-400">{activeHint.hintTitle}</h2>
                    <button type="button" onClick={() => setIsHintOpen(false)} className="text-xs font-semibold text-slate-500 hover:text-slate-800 cursor-pointer dark:text-slate-400 dark:hover:text-slate-200">
                      Ẩn gợi ý
                    </button>
                  </div>
                  <div className="whitespace-pre-wrap text-sm leading-relaxed text-slate-700 dark:text-slate-300">
                    <RichMathText text={activeHint.hintContent} />
                  </div>
                  <p className="text-xs text-slate-500 dark:text-slate-400">{activeHint.supportLevelDescription}</p>
                  {activeHint.hintLevel < 3 && (
                    <button type="button" onClick={() => handleFetchHint(activeHint.hintLevel + 1)} disabled={hintLoading} className={SECONDARY}>
                      {hintLoading ? "Đang chuẩn bị..." : `Xem gợi ý mức ${activeHint.hintLevel + 1}`}
                    </button>
                  )}
                </div>
              )}

              {boardBeside && (
                <>
                  {workEditor("120px")}
                  {actionBar}
                </>
              )}
            </section>

            <section aria-label={feedback ? "Kết quả câu trả lời" : "Làm bài"} className={`${PANEL} min-w-0 space-y-4`}>
              {feedback ? (
                <>
                  <div className="space-y-2">
                    <h2 className={`text-lg font-bold ${feedback.isCorrect ? "text-emerald-700 dark:text-emerald-400" : "text-rose-700 dark:text-rose-400"}`}>
                      {feedback.isCorrect ? "Chính xác" : "Chưa chính xác"}
                    </h2>
                    {!feedback.isCorrect && correctAnswerText && (
                      <p className="text-sm text-slate-700 dark:text-slate-300">
                        Đáp án đúng: <strong className="text-slate-900 dark:text-white"><RichMathText text={correctAnswerText} /></strong>
                      </p>
                    )}
                    <div className="whitespace-pre-wrap text-sm leading-relaxed text-slate-700 dark:text-slate-300">
                      <RichMathText text={feedback.stepGuidance} />
                    </div>
                    {feedback.misconceptionHypothesis && (
                      <p className="text-sm text-amber-700 dark:text-amber-400">{feedback.misconceptionHypothesis}</p>
                    )}
                  </div>
                  <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 pt-3 dark:border-slate-800">
                    <p className="text-sm tabular-nums text-slate-600 dark:text-slate-400">
                      Năng lực ước lượng: <strong className="text-slate-900 dark:text-white">{feedback.updatedEstimatedMastery.toFixed(0)}%</strong>
                    </p>
                    <button type="button" onClick={handleNextQuestion} className={PRIMARY}>
                      {feedback.isSessionClosed ? "Xem tổng kết" : "Câu tiếp theo"}
                    </button>
                  </div>
                </>
              ) : isMcq ? (
                <>
                  <fieldset className="grid grid-cols-1 xl:grid-cols-2 gap-2">
                    <legend className="sr-only">Chọn một đáp án</legend>
                    {question.options.map((opt) => {
                      const isSelected = selectedOption === opt.label || selectedOption === opt.optionId;
                      return (
                        <button
                          key={opt.optionId}
                          type="button"
                          aria-pressed={isSelected}
                          onClick={() => setSelectedOption(opt.label)}
                          className={`flex items-start gap-3 rounded-xl border p-3 text-left cursor-pointer ${PRESS} ${
                            isSelected
                              ? "border-indigo-500 bg-indigo-50 text-indigo-950 dark:bg-indigo-950/60 dark:text-indigo-100"
                              : "border-slate-200 text-slate-800 hover:bg-slate-50 dark:border-slate-800 dark:text-slate-200 dark:hover:bg-slate-800/50"
                          }`}
                        >
                          <span className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-lg text-xs font-bold ${isSelected ? "bg-indigo-600 text-white" : "bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300"}`}>
                            {opt.label}
                          </span>
                          <span className="pt-0.5 text-sm leading-snug"><RichMathText content={opt.text} /></span>
                        </button>
                      );
                    })}
                  </fieldset>
                  <div className="space-y-2">
                    <span className="block text-sm font-semibold text-slate-700 dark:text-slate-300">Giải thích vì sao em chọn đáp án này (không bắt buộc)</span>
                    <WorkTextBox math={mathInput} label="Giải thích vì sao em chọn đáp án này" value={reasoningText} onChange={setReasoningText} placeholder={answerReasoningPlaceholder(mathInput)} minHeight="120px" />
                  </div>
                  {actionBar}
                </>
              ) : (
                <>
                  <WorkToolbar showModes mode={inputMode} onModeChange={setInputMode} showTools={false} activeTool={null} onToggleTool={() => undefined} />

                  {inputMode === "draw" && (
                    <div className="space-y-3">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <p className="text-sm text-slate-600 dark:text-slate-400">Viết bài làm trên giấy A4, rồi quét để đưa vào bài làm.</p>
                        <button type="button" onClick={() => void handleScanBoard()} disabled={isScanning} className={PRIMARY}>
                          {isScanning ? "Đang quét..." : "Quét bài làm"}
                        </button>
                      </div>
                      <div className="h-[calc(100vh-18.5rem)] min-h-[460px]">
                        <ScratchpadInlinePanel
                          ref={boardRef}
                          centerId={session?.centerId ?? ""}
                          userId={session?.studentId ?? ""}
                          clientSubmissionId={`adaptive:${sessionId}:${question.sessionItemId}`}
                          backgroundImageUrl={photoDataUrl}
                          onAttachSnapshot={(_blob, url) => setPhotoDataUrl(url)}
                          questionNode={<RichMathText text={question.questionText} />}
                        />
                      </div>
                    </div>
                  )}

                  {inputMode === "photo" && (
                    <div className="space-y-3">
                      <input
                        ref={photoInputRef}
                        type="file"
                        accept="image/png,image/jpeg,image/webp"
                        className="hidden"
                        aria-label="Chọn ảnh bài giải"
                        onChange={(e) => {
                          const f = e.target.files?.[0] ?? null;
                          e.target.value = "";
                          void handlePhotoFile(f);
                        }}
                      />
                      {photoDataUrl ? (
                        <div className="flex flex-wrap items-center gap-3">
                          <img src={photoDataUrl} alt="Ảnh bài giải đã tải lên" className="h-16 w-24 rounded-lg border border-slate-300 bg-white object-cover dark:border-slate-600" />
                          <p className="min-w-[160px] flex-1 text-xs text-slate-500 dark:text-slate-400">Ảnh chỉ dùng để quét nội dung vào bài làm, không lưu kèm phiên luyện tập.</p>
                          <div className="flex gap-2">
                            <button type="button" onClick={() => void runScan(photoDataUrl)} disabled={isScanning} className={SECONDARY}>{isScanning ? "Đang quét..." : "Quét lại"}</button>
                            <button type="button" onClick={() => photoInputRef.current?.click()} disabled={isScanning} className={SECONDARY}>Đổi ảnh</button>
                          </div>
                        </div>
                      ) : (
                        <button
                          type="button"
                          onClick={() => photoInputRef.current?.click()}
                          disabled={isScanning}
                          className={`w-full rounded-xl border border-dashed border-slate-300 px-4 py-5 text-center cursor-pointer hover:bg-slate-50 disabled:opacity-60 dark:border-slate-600 dark:hover:bg-slate-800/50 ${PRESS}`}
                        >
                          <span className="block text-sm font-semibold text-slate-800 dark:text-slate-200">{isScanning ? "Đang quét ảnh..." : "Chụp hoặc tải ảnh bài giải lên"}</span>
                          <span className="mt-0.5 block text-xs text-slate-500 dark:text-slate-400">Nội dung sẽ được quét vào ô bài làm bên dưới</span>
                        </button>
                      )}
                    </div>
                  )}

                  {scanNotice && <p role="status" className="text-xs font-medium text-indigo-700 dark:text-indigo-300">{scanNotice}</p>}
                  {scanError && <p role="alert" className="text-xs font-medium text-rose-700 dark:text-rose-300">{scanError}</p>}

                  {!boardBeside && workEditor(inputMode === "keyboard" ? "clamp(220px, 42vh, 460px)" : "180px")}
                  {!boardBeside && actionBar}
                </>
              )}
              {submitMutation.isError && !feedback && (
                <p role="alert" className="text-xs font-medium text-rose-700 dark:text-rose-300">{(submitMutation.error as { response?: { data?: { detail?: string } } })?.response?.data?.detail || "Chưa nộp được câu trả lời. Hãy thử lại."}</p>
              )}
            </section>
          </div>
        )}
      </main>

      {/* Pause Modal */}
      {isPaused && (
        <div role="dialog" aria-modal="true" className="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="w-full max-w-md rounded-2xl bg-white dark:bg-[#0f172a] border border-slate-200 dark:border-slate-800 p-6 text-center space-y-4 shadow-xl">
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
        <div role="dialog" aria-modal="true" className="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="w-full max-w-md rounded-2xl bg-white dark:bg-[#0f172a] border border-slate-200 dark:border-slate-800 p-6 space-y-4 shadow-xl">
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
                disabled={isStopping}
                className="px-5 py-2 rounded-xl bg-rose-600 hover:bg-rose-700 text-white font-bold text-xs cursor-pointer shadow-md disabled:opacity-50"
              >
                {isStopping ? "Đang dừng phiên..." : "Dừng và xem kết quả"}
              </button>
            </div>
            {stopError && <p role="alert" className="text-xs font-medium text-rose-700 dark:text-rose-300">{stopError}</p>}
          </div>
        </div>
      )}
    </div>
  );
};
