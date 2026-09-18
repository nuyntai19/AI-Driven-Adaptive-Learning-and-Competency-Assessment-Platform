import { useState, useEffect, useRef, useMemo, useCallback } from "react";
import { Link, useSearchParams, useParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  getNextQuestion,
  submitAttempt,
  getAnalysisJobStatus,
  getAttemptFeedback,
  prepareAttemptAttachmentUpload,
} from "../api/learningFeedbackApi";
import { useStudentAssignment } from "../features/assignments/useStudentAssignment";
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
import { VisualMathField, type VisualMathFieldRef } from "../components/math/VisualMathField";
import { SideAssistantWorkspace, type AssistantToolTab } from "../components/math/SideAssistantWorkspace";
import {
  clearAttemptSessionId,
  createClientSubmissionId,
  getOrCreateAttemptSessionId,
  setAttemptSessionId,
} from "../utils/attemptSessionStorage";
import { useAuthStore } from "../stores/authStore";

interface StoredAnswer {
  finalAnswer: string;
  reasoningText: string;
  confidence: number;
  timeSpentSeconds: number;
  answerChanges: number;
  snapshotDataUrl?: string | null;
  snapshotTime?: string | null;
  drawingUploadToken?: string | null;
}

export const LearningPlayerPage = () => {
  const { questionId: routeQuestionId } = useParams<{ questionId?: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const assignmentId = searchParams.get("assignmentId");
  const subjectId = searchParams.get("subjectId") || "";
  const persistedJobId = searchParams.get("analysisJobId");

  const currentUser = useAuthStore((state) => state.user);
  const queryClient = useQueryClient();

  // Auto-restore subjectId from localStorage if entering adaptive learning directly
  useEffect(() => {
    if (!assignmentId && !subjectId) {
      try {
        const lastSubjectId = localStorage.getItem("edutwin_last_subject_id");
        if (lastSubjectId) {
          const newParams = new URLSearchParams(searchParams);
          newParams.set("subjectId", lastSubjectId);
          setSearchParams(newParams, { replace: true });
        }
      } catch {
        // Ignore localStorage errors
      }
    }
  }, [assignmentId, subjectId, searchParams, setSearchParams]);

  // Active question ID state (allowing seamless switching between questions in an assignment)
  const [activeQuestionId, setActiveQuestionId] = useState<string>(routeQuestionId || "");

  // Batch Assignment Answers state (mapped by questionId)
  const [assignmentAnswers, setAssignmentAnswers] = useState<Record<string, StoredAnswer>>(() => {
    if (!assignmentId) return {};
    try {
      const saved = localStorage.getItem(`edutwin_assignment_answers_${assignmentId}`);
      if (!saved) return {};
      const parsed = JSON.parse(saved);
      if (typeof parsed !== "object" || parsed === null) return {};
      const sanitized: Record<string, StoredAnswer> = {};
      for (const [k, v] of Object.entries(parsed)) {
        if (v && typeof v === "object") {
          sanitized[k] = {
            finalAnswer: typeof (v as any).finalAnswer === "string" ? (v as any).finalAnswer : "",
            reasoningText: typeof (v as any).reasoningText === "string" ? (v as any).reasoningText : "",
            confidence: typeof (v as any).confidence === "number" ? (v as any).confidence : 80,
            timeSpentSeconds: typeof (v as any).timeSpentSeconds === "number" ? (v as any).timeSpentSeconds : 0,
            answerChanges: typeof (v as any).answerChanges === "number" ? (v as any).answerChanges : 0,
            snapshotDataUrl: typeof (v as any).snapshotDataUrl === "string" ? (v as any).snapshotDataUrl : null,
            snapshotTime: typeof (v as any).snapshotTime === "string" ? (v as any).snapshotTime : null,
            drawingUploadToken: typeof (v as any).drawingUploadToken === "string" ? (v as any).drawingUploadToken : null,
          };
        }
      }
      return sanitized;
    } catch {
      return {};
    }
  });

  // Attached Scratchpad Snapshot State (Stored independently from scratchpad edits)
  const [attachedSnapshotDataUrl, setAttachedSnapshotDataUrl] = useState<string | null>(() => {
    if (!assignmentId) return null;
    try {
      return localStorage.getItem(`edutwin_assignment_snapshot_${assignmentId}`) || null;
    } catch {
      return null;
    }
  });
  const [attachedSnapshotBlob, setAttachedSnapshotBlob] = useState<Blob | null>(null);
  const [attachedSnapshotTime, setAttachedSnapshotTime] = useState<string | null>(() => {
    if (!assignmentId) return null;
    try {
      return localStorage.getItem(`edutwin_assignment_snapshot_time_${assignmentId}`) || null;
    } catch {
      return null;
    }
  });
  const [showFullSnapshotModal, setShowFullSnapshotModal] = useState<boolean>(false);

  // Current question input state
  const [finalAnswer, setFinalAnswer] = useState<string>("");
  const answerDisplayLatex = "";
  const [reasoningText, setReasoningText] = useState<string>("");
  const [confidence, setConfidence] = useState<number>(80);
  const [answerChanges, setAnswerChanges] = useState<number>(0);
  const [timeSpentSeconds, setTimeSpentSeconds] = useState<number>(0);

  // Assistant tools state
  const [activeSideTool, setActiveSideTool] = useState<AssistantToolTab | null>(null);
  const [showMathToolbar, setShowMathToolbar] = useState<boolean>(false);
  const [drawingUploadToken, setDrawingUploadToken] = useState<string | null>(null);

  // Input refs and cursor management
  const reasoningTextareaRef = useRef<HTMLTextAreaElement>(null);
  const visualMathFieldRef = useRef<VisualMathFieldRef>(null);
  const [activeInputTarget, setActiveInputTarget] = useState<"answer" | "reasoning">("answer");

  // Client submission token (unique per attempt session)
  const clientSubmissionIdRef = useRef<string>(createClientSubmissionId());

  // Workflow state
  const [isSubmitting, setIsSubmitting] = useState<boolean>(Boolean(persistedJobId));
  const [pollingJobId, setPollingJobId] = useState<string | null>(persistedJobId);
  const [pollingStatus, setPollingStatus] = useState<string>("Đang xử lý...");
  const [feedbackData, setFeedbackData] = useState<AttemptFeedbackDataDto | null>(null);
  const [submissionError, setSubmissionError] = useState<string | null>(null);
  const [canResubmit, setCanResubmit] = useState<boolean>(false);
  const [showBatchConfirmModal, setShowBatchConfirmModal] = useState<boolean>(false);
  const pollingAttemptRef = useRef(0);

  // Timer
  useEffect(() => {
    if (pollingJobId || feedbackData) return;
    const timer = setInterval(() => {
      setTimeSpentSeconds((prev) => prev + 1);
    }, 1000);
    return () => clearInterval(timer);
  }, [pollingJobId, feedbackData]);

  // Mode 1: Assignment Mode Query
  const {
    data: assignmentResponse,
    isLoading: assignmentLoading,
    isError: assignmentError,
  } = useStudentAssignment(assignmentId || undefined);

  const assignment = assignmentResponse?.data;
  const assignmentQuestions = assignment?.questions || [];

  // Initialize or update activeQuestionId when assignment loads
  const isInitializedRef = useRef(false);
  useEffect(() => {
    if (!assignmentQuestions.length) return;
    if (!isInitializedRef.current) {
      isInitializedRef.current = true;
      if (routeQuestionId && assignmentQuestions.some((q) => q.questionId === routeQuestionId)) {
        setActiveQuestionId(routeQuestionId);
      } else {
        const firstUnfinished = assignmentQuestions.find(
          (q) => q.attemptStatus !== "Completed" && q.attemptStatus !== "NeedsTeacherReview"
        );
        setActiveQuestionId(firstUnfinished?.questionId || assignmentQuestions[0].questionId);
      }
    }
  }, [assignmentQuestions, routeQuestionId]);

  // Current Question Object in Assignment
  const assignmentQuestion = useMemo(() => {
    if (!assignmentQuestions.length) return null;
    return (
      assignmentQuestions.find((q) => q.questionId === activeQuestionId) ||
      assignmentQuestions[0]
    );
  }, [assignmentQuestions, activeQuestionId]);

  const currentAssignmentIndex = assignmentQuestions.findIndex(
    (q) => q.questionId === assignmentQuestion?.questionId
  );

  const isAssignmentSubmitted = useMemo(() => {
    if (!assignment) return false;
    if (assignment.progress?.status === "Completed") return true;
    return (
      assignmentQuestions.length > 0 &&
      assignmentQuestions.every(
        (q) => q?.attemptStatus === "Completed" || q?.attemptStatus === "NeedsTeacherReview"
      )
    );
  }, [assignment, assignmentQuestions]);

  // Mode 2: Adaptive Practice Mode Query
  const {
    data: adaptiveQuestion,
    isLoading: adaptiveLoading,
    isError: adaptiveError,
    refetch: refetchAdaptiveQuestion,
  } = useQuery<NextQuestionDataDto>({
    queryKey: ["nextQuestion", subjectId],
    queryFn: () => getNextQuestion(subjectId),
    enabled: !assignmentId && !!subjectId && !feedbackData && !pollingJobId && !persistedJobId,
  });

  // Effective unified question object
  const question: NextQuestionDataDto | null = useMemo(() => {
    if (assignment && assignmentQuestion) {
      return {
        strategy: "AssignmentPractice",
        recommendationId: null,
        questionId: assignmentQuestion.questionId,
        topicNodeId: "",
        topicName: assignment.title,
        topicMastery: 0,
        questionType: assignmentQuestion.questionType,
        difficulty: assignmentQuestion.difficulty,
        questionText: assignmentQuestion.questionText,
        maxScore: 10,
        estimatedTimeSeconds: assignmentQuestion.estimatedTimeSeconds || 120,
        reasoningRequired: assignmentQuestion.reasoningRequired,
        languageCode: assignmentQuestion.languageCode || "vi",
        options: (assignmentQuestion.options || []).map((o, idx) => ({
          optionId: o.optionId,
          label: o.label,
          text: o.text,
          orderIndex: idx,
        })),
        explanation: assignment.instructions || "",
        answerEvaluationMode: assignmentQuestion.questionType === "Numeric" ? "NumericRational" : "Exact",
      };
    }
    return adaptiveQuestion || null;
  }, [assignment, assignmentQuestion, adaptiveQuestion]);

  const questionLoading = assignmentId ? assignmentLoading : adaptiveLoading;
  const questionError = assignmentId ? assignmentError : adaptiveError;

  // Sync form inputs when switching active question in assignment mode
  useEffect(() => {
    if (!question?.questionId || !assignmentId) return;
    const qId = question.questionId;
    const saved = assignmentAnswers[qId];
    if (saved) {
      setFinalAnswer(saved.finalAnswer || "");
      setReasoningText(saved.reasoningText || "");
      setConfidence(saved.confidence ?? 80);
      setTimeSpentSeconds(saved.timeSpentSeconds ?? 0);
      setAnswerChanges(saved.answerChanges ?? 0);
      setAttachedSnapshotDataUrl(saved.snapshotDataUrl || null);
      setAttachedSnapshotTime(saved.snapshotTime || null);
      setDrawingUploadToken(saved.drawingUploadToken || null);
    } else {
      setFinalAnswer("");
      setReasoningText("");
      setConfidence(80);
      setTimeSpentSeconds(0);
      setAnswerChanges(0);
      setAttachedSnapshotDataUrl(null);
      setAttachedSnapshotTime(null);
      setDrawingUploadToken(null);
    }
    setAttachedSnapshotBlob(null);
    setSubmissionError(null);
  }, [question?.questionId, assignmentId]);

  // Persist current question answer into assignmentAnswers & localStorage
  const persistCurrentAnswer = useCallback(
    (newFinalAnswer?: string, newReasoning?: string, newConf?: number) => {
      if (!assignmentId || !question?.questionId) return;
      const qId = question.questionId;
      const updatedEntry: StoredAnswer = {
        finalAnswer: newFinalAnswer !== undefined ? newFinalAnswer : finalAnswer,
        reasoningText: newReasoning !== undefined ? newReasoning : reasoningText,
        confidence: newConf !== undefined ? newConf : confidence,
        timeSpentSeconds,
        answerChanges,
        snapshotDataUrl: attachedSnapshotDataUrl,
        snapshotTime: attachedSnapshotTime,
        drawingUploadToken,
      };

      setAssignmentAnswers((prev) => {
        const next = { ...prev, [qId]: updatedEntry };
        try {
          localStorage.setItem(`edutwin_assignment_answers_${assignmentId}`, JSON.stringify(next));
        } catch {
          // Ignore localStorage quote limits
        }
        return next;
      });
    },
    [
      assignmentId,
      question?.questionId,
      finalAnswer,
      reasoningText,
      confidence,
      timeSpentSeconds,
      answerChanges,
      attachedSnapshotDataUrl,
      attachedSnapshotTime,
      drawingUploadToken,
    ]
  );

  // Switch to another question in the Palette
  const handleSwitchQuestion = (targetQId: string) => {
    if (targetQId === activeQuestionId) return;
    persistCurrentAnswer();
    setActiveQuestionId(targetQId);
    if (assignmentId) {
      window.history.replaceState(
        null,
        "",
        `/hoc-tap/luyen-tap/${targetQId}?assignmentId=${assignmentId}${subjectId ? `&subjectId=${subjectId}` : ""}`
      );
    }
  };

  const attemptSessionScope = useMemo(() => {
    if (!currentUser || !question) return null;
    return {
      centerId: currentUser.centerId,
      userId: currentUser.userId,
      subjectId: subjectId || (assignmentId ? "assignment" : ""),
      questionId: String(question.questionId),
    };
  }, [currentUser, question, subjectId, assignmentId]);

  useEffect(() => {
    if (!attemptSessionScope) return;
    clientSubmissionIdRef.current = getOrCreateAttemptSessionId(attemptSessionScope);
  }, [attemptSessionScope]);

  const getClientSubmissionId = useCallback(() => {
    if (!attemptSessionScope) return clientSubmissionIdRef.current;
    const id = getOrCreateAttemptSessionId(attemptSessionScope);
    clientSubmissionIdRef.current = id;
    return id;
  }, [attemptSessionScope]);

  const getClientSubmissionIdForQuestion = useCallback(
    (qId: string | number) => {
      if (!currentUser) return clientSubmissionIdRef.current;
      const scope = {
        centerId: currentUser.centerId,
        userId: currentUser.userId,
        subjectId: subjectId || (assignmentId ? "assignment" : ""),
        questionId: String(qId),
      };
      return getOrCreateAttemptSessionId(scope);
    },
    [currentUser, subjectId, assignmentId]
  );

  // Polling Job Status Mechanism
  useEffect(() => {
    if (!pollingJobId || feedbackData) return;

    let isSubscribed = true;
    let pollInterval = 1000;

    const poll = async () => {
      try {
        const result = await getAnalysisJobStatus(pollingJobId);
        if (!isSubscribed) return;

        const currentStatus = result.status;
        pollingAttemptRef.current += 1;

        if (isSuccessfulTerminalStatus(currentStatus)) {
          setPollingStatus("Hoàn tất đánh giá. Đang tải kết quả bài làm...");
          try {
            const feedbackRes = await getAttemptFeedback(result.attemptId);
            if (isSubscribed) {
              setFeedbackData(feedbackRes);
              setIsSubmitting(false);
              setPollingJobId(null);
              if (searchParams.get("analysisJobId")) {
                searchParams.delete("analysisJobId");
                setSearchParams(searchParams, { replace: true });
              }
              if (attemptSessionScope) {
                clearAttemptSessionId(attemptSessionScope);
              }
            }
          } catch {
            if (isSubscribed) {
              setSubmissionError("Không thể tải kết quả phân tích chi tiết. Vui lòng thử lại.");
              setIsSubmitting(false);
              setPollingJobId(null);
            }
          }
          return;
        }

        if (isTerminalStatus(currentStatus)) {
          setIsSubmitting(false);
          setPollingJobId(null);
          setSubmissionError(`Quá trình phân tích thất bại (${currentStatus}). Bạn có thể nộp lại.`);
          setCanResubmit(true);
          return;
        }

        if (shouldContinuePolling(currentStatus, pollingAttemptRef.current)) {
          setPollingStatus(
            currentStatus === "Processing" ? "AI đang phân tích toàn bộ câu trả lời..." : "Đang trong hàng đợi đánh giá..."
          );
          pollInterval = Math.min(pollInterval + 500, 3000);
          setTimeout(poll, pollInterval);
        } else {
          setIsSubmitting(false);
          setPollingJobId(null);
          setSubmissionError("Quá thời gian phân tích dự kiến. Bạn có thể nộp lại để thử lại.");
          setCanResubmit(true);
        }
      } catch {
        if (!isSubscribed) return;
        pollInterval = Math.min(pollInterval + 1000, 5000);
        setTimeout(poll, pollInterval);
      }
    };

    const initialTimeout = setTimeout(poll, 1000);
    return () => {
      isSubscribed = false;
      clearTimeout(initialTimeout);
    };
  }, [pollingJobId, feedbackData, searchParams, setSearchParams, attemptSessionScope]);

  // Answer change handlers
  const handleAnswerChange = (val: string) => {
    if (isReadOnly) return;
    setFinalAnswer(val);
    setAnswerChanges((prev) => prev + 1);
    persistCurrentAnswer(val, undefined, undefined);
  };

  const handleReasoningChange = (val: string) => {
    if (isReadOnly) return;
    setReasoningText(val);
    persistCurrentAnswer(undefined, val, undefined);
  };

  const handleConfidenceChange = (val: number) => {
    if (isReadOnly) return;
    setConfidence(val);
    persistCurrentAnswer(undefined, undefined, val);
  };

  // 1-Click insertion from Casio or Toolbar
  const insertTextAtCursor = (textToInsert: string) => {
    if (isReadOnly) return;

    if (activeInputTarget === "answer" && visualMathFieldRef.current) {
      visualMathFieldRef.current.insertAtCursor(textToInsert);
      setAnswerChanges((prev) => prev + 1);
      return;
    }

    const textarea = reasoningTextareaRef.current;
    if (!textarea) {
      if (activeInputTarget === "reasoning") {
        handleReasoningChange(reasoningText + textToInsert);
      } else {
        handleAnswerChange(finalAnswer + textToInsert);
      }
      return;
    }

    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const currentVal = textarea.value;
    const updatedVal = currentVal.substring(0, start) + textToInsert + currentVal.substring(end);
    handleReasoningChange(updatedVal);

    setTimeout(() => {
      textarea.focus();
      textarea.setSelectionRange(start + textToInsert.length, start + textToInsert.length);
    }, 0);
  };

  // Callback from Scratchpad: Save Snapshot independently of future draws
  const handleAttachSnapshot = (blob: Blob, dataUrl: string) => {
    if (isReadOnly) return;
    setAttachedSnapshotBlob(blob);
    setAttachedSnapshotDataUrl(dataUrl);
    const nowTime = new Date().toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
    setAttachedSnapshotTime(nowTime);

    if (assignmentId && question?.questionId) {
      const qId = question.questionId;
      setAssignmentAnswers((prev) => {
        const existing = prev[qId] || {
          finalAnswer,
          reasoningText,
          confidence,
          timeSpentSeconds,
          answerChanges,
        };
        const next = {
          ...prev,
          [qId]: {
            ...existing,
            snapshotDataUrl: dataUrl,
            snapshotTime: nowTime,
          },
        };
        try {
          localStorage.setItem(`edutwin_assignment_answers_${assignmentId}`, JSON.stringify(next));
        } catch {
          // Handle storage quota
        }
        return next;
      });
    }
  };

  const handleRemoveSnapshot = () => {
    if (isReadOnly) return;
    setAttachedSnapshotBlob(null);
    setAttachedSnapshotDataUrl(null);
    setAttachedSnapshotTime(null);
    setDrawingUploadToken(null);
    if (assignmentId && question?.questionId) {
      const qId = question.questionId;
      setAssignmentAnswers((prev) => {
        const existing = prev[qId];
        if (!existing) return prev;
        const next = {
          ...prev,
          [qId]: {
            ...existing,
            snapshotDataUrl: null,
            snapshotTime: null,
            drawingUploadToken: null,
          },
        };
        try {
          localStorage.setItem(`edutwin_assignment_answers_${assignmentId}`, JSON.stringify(next));
        } catch {
          // Handle storage quota
        }
        return next;
      });
    }
  };

  const uploadScratchpadAttachmentIfAny = async (): Promise<string | null> => {
    const blobToUpload = attachedSnapshotBlob;
    if (!blobToUpload || !currentUser) return drawingUploadToken;

    try {
      const uploadIntent = await prepareAttemptAttachmentUpload(blobToUpload);
      setDrawingUploadToken(uploadIntent.drawingUploadToken);
      return uploadIntent.drawingUploadToken;
    } catch (err) {
      console.warn("Attachment upload warning:", err);
      return null;
    }
  };

  // Submit flow:
  // - Assignment Mode: Sequentially submit each question attempt (generating 1 prompt per question + scratchpad)
  // - Adaptive Mode: Submit single active question
  const handleFinalSubmit = async () => {
    if (!question) return;
    persistCurrentAnswer();
    setShowBatchConfirmModal(false);

    setSubmissionError(null);
    setIsSubmitting(true);
    setCanResubmit(false);

    try {
      // ── BATCH ASSIGNMENT SUBMISSION ─────────────────────────────────────────
      if (assignmentId && assignmentQuestions.length > 0) {
        const currentQId = question.questionId;
        const currentSaved: StoredAnswer = {
          finalAnswer,
          reasoningText,
          confidence,
          timeSpentSeconds,
          answerChanges,
          snapshotDataUrl: attachedSnapshotDataUrl,
          snapshotTime: attachedSnapshotTime,
          drawingUploadToken,
        };

        const allAnswersMap: Record<string, StoredAnswer> = {
          ...assignmentAnswers,
          [currentQId]: currentSaved,
        };

        let lastJobId: string | null = null;
        let lastAttemptId: string | null = null;
        let submittedCount = 0;

        for (let i = 0; i < assignmentQuestions.length; i++) {
          const q = assignmentQuestions[i];
          // Skip if question was already evaluated
          if (q.attemptStatus === "Completed" || q.attemptStatus === "NeedsTeacherReview") {
            continue;
          }

          const qAnswer = allAnswersMap[q.questionId];
          const qFinalAnswer = qAnswer?.finalAnswer?.trim() || "";
          const qReasoning = qAnswer?.reasoningText?.trim() || undefined;
          const qConfidence = qAnswer?.confidence ?? 80;
          const qTimeSpent = qAnswer?.timeSpentSeconds ?? 0;
          const qAnswerChanges = qAnswer?.answerChanges ?? 0;
          const qSnapshotDataUrl = qAnswer?.snapshotDataUrl;
          let qToken = qAnswer?.drawingUploadToken || (q.questionId === currentQId ? drawingUploadToken : null);

          setPollingStatus(`Đang nộp câu ${i + 1}/${assignmentQuestions.length}...`);

          // Upload scratchpad snapshot if this question has an attached drawing
          if (qSnapshotDataUrl && !qToken) {
            try {
              setPollingStatus(`Đang tải ảnh nháp câu ${i + 1}/${assignmentQuestions.length}...`);
              const res = await fetch(qSnapshotDataUrl);
              const blob = await res.blob();
              const uploadIntent = await prepareAttemptAttachmentUpload(blob);
              qToken = uploadIntent.drawingUploadToken;
            } catch (attachErr) {
              console.warn(`Could not upload scratchpad for question ${q.questionId}:`, attachErr);
            }
          }

          // Submit attempt for this question
          const clientSubId = getClientSubmissionIdForQuestion(q.questionId);
          const submitted = await submitAttempt({
            questionId: q.questionId,
            assignmentId: assignmentId,
            finalAnswer: qFinalAnswer || "SKIPPED",
            reasoningText: qReasoning,
            timeSpentSeconds: qTimeSpent,
            confidence: qConfidence,
            answerChanges: qAnswerChanges,
            skipped: !qFinalAnswer,
            clientSubmissionId: clientSubId,
            drawingUploadToken: qToken || undefined,
          });

          submittedCount += 1;
          const resData = submitted.data;
          if (resData.analysisJobId || resData.jobId) {
            lastJobId = String(resData.analysisJobId || resData.jobId);
          }
          if (resData.attemptId) {
            lastAttemptId = String(resData.attemptId);
          }
        }

        // Invalidate TanStack queries so assignment and lists refresh with updated progress
        void queryClient.invalidateQueries({ queryKey: ["studentAssignment", assignmentId] });
        void queryClient.invalidateQueries({ queryKey: ["studentAssignments"] });

        // Clear local storage draft
        try {
          localStorage.removeItem(`edutwin_assignment_answers_${assignmentId}`);
        } catch {
          // ignore
        }

        if (lastJobId) {
          setPollingJobId(lastJobId);
          searchParams.set("analysisJobId", lastJobId);
          setSearchParams(searchParams, { replace: true });
          setPollingStatus(`Đã nộp thành công ${submittedCount} câu hỏi. AI đang phân tích và chấm điểm...`);
        } else if (lastAttemptId) {
          setPollingStatus("Đang tải kết quả bài làm...");
          const fbRes = await getAttemptFeedback(lastAttemptId);
          setFeedbackData(fbRes);
          setIsSubmitting(false);
        } else {
          setIsSubmitting(false);
        }
        return;
      }

      // ── SINGLE ADAPTIVE QUESTION SUBMISSION ──────────────────────────────────
      let tokenToUse: string | null = drawingUploadToken;
      if (attachedSnapshotDataUrl && !tokenToUse) {
        setPollingStatus("Đang tải lên bản vẽ nháp đính kèm...");
        tokenToUse = await uploadScratchpadAttachmentIfAny();
      }

      setPollingStatus("Đang gửi bài làm lên hệ thống AI...");
      const submitted = await submitAttempt({
        questionId: question.questionId,
        assignmentId: undefined,
        finalAnswer: finalAnswer.trim() || "SKIPPED",
        reasoningText: reasoningText.trim() || undefined,
        timeSpentSeconds,
        confidence,
        answerChanges,
        skipped: !finalAnswer.trim(),
        clientSubmissionId: getClientSubmissionId(),
        answerDisplayLatex: answerDisplayLatex.trim() || undefined,
        drawingUploadToken: tokenToUse || undefined,
      });

      const resData = submitted.data;
      const targetJobId = resData.analysisJobId || resData.jobId;

      if (targetJobId) {
        setPollingJobId(targetJobId);
        searchParams.set("analysisJobId", targetJobId);
        setSearchParams(searchParams, { replace: true });
        setPollingStatus("Đã tiếp nhận bài làm. AI đang chấm điểm...");
      } else if (resData.attemptId) {
        setPollingStatus("Đang tải kết quả bài làm...");
        const fbRes = await getAttemptFeedback(resData.attemptId);
        setFeedbackData(fbRes);
        setIsSubmitting(false);
      } else {
        setIsSubmitting(false);
        setSubmissionError("Phản hồi bất thường từ máy chủ. Vui lòng thử nộp lại.");
        setCanResubmit(true);
      }
    } catch (err: unknown) {
      setIsSubmitting(false);
      const errObj = err as { response?: { data?: { detail?: string } }; message?: string };
      setSubmissionError(
        errObj?.response?.data?.detail || errObj?.message || "Nộp bài thất bại. Vui lòng thử lại."
      );
      setCanResubmit(true);
    }
  };

  const handleResubmit = () => {
    if (attemptSessionScope) {
      const freshSubId = createClientSubmissionId();
      setAttemptSessionId(attemptSessionScope, freshSubId);
      clientSubmissionIdRef.current = freshSubId;
    }
    setSubmissionError(null);
    setCanResubmit(false);
    handleFinalSubmit();
  };

  // Calculate stats for Question Palette & Assignment submission state (must be declared before any early return)
  const totalQuestions = assignmentQuestions?.length || 0;
  const answeredCount = useMemo(() => {
    const keys = new Set(
      Object.entries(assignmentAnswers || {})
        .filter(([_, a]) => typeof a?.finalAnswer === "string" && a.finalAnswer.trim().length > 0)
        .map(([k]) => k)
    );
    if (question?.questionId && finalAnswer.trim().length > 0) {
      keys.add(question.questionId);
    }
    return keys.size;
  }, [assignmentAnswers, question?.questionId, finalAnswer]);

  const isReadOnly = isAssignmentSubmitted || isSubmitting;

  // Guard: if adaptive mode and no subject selected
  if (!assignmentId && !subjectId) {
    return <SubjectRequiredState onSelect={(id) => setSearchParams({ subjectId: id })} />;
  }

  // Loading skeleton
  if (questionLoading) {
    return (
      <div className="min-h-screen bg-[#f8fafc] dark:bg-[#090d16] p-6 text-slate-800 dark:text-slate-100 flex items-center justify-center">
        <div className="mx-auto max-w-3xl w-full rounded-3xl bg-white dark:bg-[#0f172a] p-8 shadow-xs border border-slate-200/80 dark:border-slate-800 space-y-6">
          <div className="h-8 w-1/3 animate-pulse rounded-xl bg-slate-200 dark:bg-slate-800" />
          <div className="h-36 animate-pulse rounded-2xl bg-slate-100 dark:bg-slate-800/60" />
          <div className="h-28 animate-pulse rounded-2xl bg-slate-100 dark:bg-slate-800/60" />
        </div>
      </div>
    );
  }

  // Error / Fallback screen
  if ((questionError || !question) && !feedbackData) {
    return (
      <div className="min-h-screen bg-[#f8fafc] dark:bg-[#090d16] p-6 flex items-center justify-center text-slate-800 dark:text-slate-100">
        <div className="mx-auto max-w-xl rounded-3xl bg-white dark:bg-[#0f172a] p-8 sm:p-10 text-center shadow-xs border border-slate-200/80 dark:border-slate-800 space-y-4">
          <div className="w-14 h-14 rounded-2xl bg-indigo-50 dark:bg-indigo-950/60 text-indigo-600 dark:text-indigo-400 flex items-center justify-center mx-auto text-2xl font-bold">
            🎯
          </div>
          <h2 className="text-xl font-bold text-slate-900 dark:text-white">
            Chưa tìm thấy câu hỏi thích hợp
          </h2>
          <p className="text-sm text-slate-500 dark:text-slate-400 leading-relaxed max-w-md mx-auto">
            {assignmentId
              ? "Không tìm thấy dữ liệu câu hỏi trong bài tập này hoặc bài tập đã đóng."
              : "Hiện tại bạn đã hoàn thành các câu hỏi đề xuất trong môn học này hoặc hệ thống đang đồng bộ câu hỏi mới."}
          </p>

          <div className="pt-4 flex flex-wrap justify-center gap-3">
            <Link
              to="/hoc-tap/bai-tap"
              className="rounded-xl bg-indigo-600 px-5 py-2.5 text-xs font-bold text-white shadow-xs hover:bg-indigo-500 transition-colors cursor-pointer"
            >
              📋 Làm bài tập được giao
            </Link>
            <Link
              to="/hoc-tap/tong-quan"
              className="rounded-xl bg-slate-100 dark:bg-slate-800 px-5 py-2.5 text-xs font-bold text-slate-700 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-slate-700 transition-colors cursor-pointer"
            >
              🏠 Về Dashboard
            </Link>
          </div>
        </div>
      </div>
    );
  }

  // 1. Polling Screen (Waiting for AI evaluation)
  if (pollingJobId) {
    return (
      <div className="min-h-screen bg-[#f8fafc] dark:bg-[#090d16] p-6 flex items-center justify-center text-slate-800 dark:text-slate-100">
        <div className="mx-auto max-w-xl rounded-3xl bg-white dark:bg-[#0f172a] p-10 sm:p-12 text-center shadow-xs border border-slate-200/80 dark:border-slate-800 space-y-4">
          <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-2xl bg-indigo-50 dark:bg-indigo-950/60">
            <div className="h-8 w-8 animate-spin rounded-full border-4 border-indigo-600 border-t-transparent" />
          </div>
          <h2 className="text-xl font-bold text-slate-900 dark:text-white">AI Đang Chấm Điểm & Phân Tích Bài Làm</h2>
          <p className="text-sm text-slate-500 dark:text-slate-400">{pollingStatus}</p>
          <div className="flex justify-center gap-2 pt-2">
            <span className="inline-block h-2.5 w-2.5 animate-bounce rounded-full bg-indigo-400" />
            <span className="inline-block h-2.5 w-2.5 animate-bounce rounded-full bg-indigo-500 [animation-delay:0.2s]" />
            <span className="inline-block h-2.5 w-2.5 animate-bounce rounded-full bg-indigo-600 [animation-delay:0.4s]" />
          </div>
          <p className="text-xs text-slate-400 dark:text-slate-500 pt-4 border-t border-slate-100 dark:border-slate-800">
            Hệ thống đang kiểm chứng các bước suy luận, phát hiện lỗ hổng kiến thức và cập nhật Hồ sơ Năng lực (Twin).
          </p>
        </div>
      </div>
    );
  }

  // 2. Feedback Screen (Results after submission)
  if (feedbackData) {
    const { grading, analysis, twinChange, recommendation } = feedbackData;

    return (
      <div className="min-h-screen bg-[#f8fafc] dark:bg-[#090d16] p-6 text-slate-800 dark:text-slate-100">
        <div className="mx-auto max-w-3xl space-y-6">
          {/* Header Result Card */}
          <div
            className={`rounded-3xl p-6 sm:p-8 text-white shadow-md ${
              grading.isCorrect === true
                ? "bg-gradient-to-r from-emerald-600 to-teal-700"
                : grading.isCorrect === false
                ? "bg-gradient-to-r from-rose-600 to-amber-700"
                : "bg-gradient-to-r from-slate-700 to-indigo-800"
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
                <h2 className="mt-2 text-2xl sm:text-3xl font-black">
                  Điểm số:{" "}
                  {grading.awardedScore === null || grading.awardedScore === undefined
                    ? `Chưa chấm / ${grading.maxScore}`
                    : `${grading.awardedScore} / ${grading.maxScore}`}
                </h2>
              </div>
              <div className="text-right">
                <span className="text-xs text-white/80">Lượt làm bài: #{feedbackData.attemptId.slice(0, 8)}</span>
                <p className="text-xs text-white/80 font-semibold mt-0.5">Trạng thái: {feedbackData.status}</p>
              </div>
            </div>
          </div>

          {/* Reasoning Analysis Card */}
          {analysis && (
            <div className="rounded-3xl bg-white dark:bg-[#0f172a] p-6 sm:p-8 shadow-xs border border-slate-200/80 dark:border-slate-800 space-y-5">
              <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-4">
                <h3 className="text-base font-extrabold text-slate-900 dark:text-white">
                  Phân Tích Tư Duy & Lập Luận (AI Reasoning Analysis)
                </h3>
                {analysis.qualityBand && (
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
                  <span className="font-bold">Quan niệm sai lầm: </span>
                  {analysis.misconception}
                </div>
              )}
            </div>
          )}

          {/* Digital Twin Change Card */}
          {twinChange && (
            <div className="rounded-3xl bg-white dark:bg-[#0f172a] p-6 shadow-xs border border-slate-200/80 dark:border-slate-800">
              <h3 className="text-base font-extrabold text-slate-900 dark:text-white">
                Tác Động Hồ Sơ Năng Lực (Digital Twin Delta)
              </h3>
              <div className="mt-3 flex items-center justify-between rounded-2xl bg-slate-50 dark:bg-slate-800/60 p-4">
                <div>
                  <p className="font-bold text-slate-900 dark:text-white text-sm">{twinChange.topicName}</p>
                  <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">{twinChange.explanation}</p>
                </div>
                <div className="text-right">
                  <span className="text-xs text-slate-400">
                    {twinChange.previousMastery.toFixed(1)}% →{" "}
                  </span>
                  <span className="text-base font-black text-slate-900 dark:text-white">
                    {twinChange.newMastery.toFixed(1)}%
                  </span>
                  <span
                    className={`ml-1 text-xs font-bold ${
                      twinChange.delta >= 0 ? "text-emerald-600 dark:text-emerald-400" : "text-rose-600"
                    }`}
                  >
                    ({twinChange.delta >= 0 ? `+${twinChange.delta.toFixed(1)}` : twinChange.delta.toFixed(1)}%)
                  </span>
                </div>
              </div>
            </div>
          )}

          {/* Next Recommendation Card if present */}
          {recommendation && (
            <div className="rounded-3xl bg-gradient-to-br from-indigo-50 to-purple-50 dark:from-indigo-950/40 dark:to-purple-950/40 p-6 shadow-xs border border-indigo-200/80 dark:border-indigo-800">
              <span className="inline-flex items-center rounded-full bg-indigo-100 dark:bg-indigo-900/60 px-2.5 py-0.5 text-xs font-bold text-indigo-800 dark:text-indigo-300">
                Gợi ý bước tiếp theo: {recommendation.type}
              </span>
              <h4 className="mt-2 text-base font-bold text-slate-900 dark:text-white">
                {recommendation.topicName}
              </h4>
              <p className="mt-1 text-sm text-slate-600 dark:text-slate-400">{recommendation.explanation}</p>
            </div>
          )}

          {/* Next Action Buttons */}
          <div className="flex flex-col sm:flex-row justify-between items-center gap-3 pt-4">
            <Link
              to={
                assignmentId
                  ? `/hoc-tap/bai-tap/${assignmentId}${subjectId ? `?subjectId=${subjectId}` : ""}`
                  : `/hoc-tap/tong-quan?subjectId=${subjectId}`
              }
              className="w-full sm:w-auto rounded-xl bg-white dark:bg-slate-800 px-5 py-2.5 text-xs font-bold text-slate-700 dark:text-slate-200 shadow-2xs border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700 text-center cursor-pointer"
            >
              {assignmentId ? "‹ Quay lại chi tiết bài tập" : "‹ Về Dashboard"}
            </Link>

            {!assignmentId ? (
              <button
                type="button"
                onClick={() => {
                  setFeedbackData(null);
                  setFinalAnswer("");
                  setReasoningText("");
                  setConfidence(80);
                  setTimeSpentSeconds(0);
                  if (refetchAdaptiveQuestion) {
                    refetchAdaptiveQuestion();
                  }
                }}
                className="w-full sm:w-auto rounded-xl bg-indigo-600 px-6 py-2.5 text-xs font-bold text-white shadow-xs hover:bg-indigo-500 text-center cursor-pointer"
              >
                Luyện câu tiếp theo →
              </button>
            ) : (
              <Link
                to="/hoc-tap/bai-tap"
                className="w-full sm:w-auto rounded-xl bg-indigo-600 px-6 py-2.5 text-xs font-bold text-white shadow-xs hover:bg-indigo-500 text-center cursor-pointer"
              >
                Xem danh sách bài tập khác →
              </Link>
            )}
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-[#f8fafc] dark:bg-[#090d16] text-slate-800 dark:text-slate-100 flex flex-col antialiased">
      {/* Top Header Bar */}
      <header className="sticky top-0 z-30 bg-white/95 dark:bg-[#0f172a]/95 backdrop-blur-md border-b border-slate-200/80 dark:border-slate-800 shadow-xs">
        <div className="max-w-[1700px] mx-auto px-4 sm:px-6 flex items-center justify-between h-16">
          <div className="flex items-center gap-4">
            <Link
              to={
                assignmentId
                  ? `/hoc-tap/bai-tap/${assignmentId}${subjectId ? `?subjectId=${subjectId}` : ""}`
                  : `/hoc-tap/tong-quan?subjectId=${subjectId}`
              }
              className="text-sm font-bold text-slate-600 dark:text-slate-300 hover:text-indigo-600 dark:hover:text-indigo-400 flex items-center gap-1.5 transition-colors group cursor-pointer"
            >
              <span className="text-base group-hover:-translate-x-0.5 transition-transform">‹</span>
              <span>{assignmentId ? "Quay lại chi tiết bài tập" : "Thoát ra Dashboard"}</span>
            </Link>

            <div className="h-5 w-px bg-slate-200 dark:bg-slate-800 hidden sm:block" />

            <span className="hidden sm:inline-flex items-center px-3.5 py-1 rounded-xl text-xs sm:text-sm font-extrabold bg-indigo-50 dark:bg-indigo-950/60 text-indigo-700 dark:text-indigo-300 border border-indigo-200/60 dark:border-indigo-800 shadow-2xs">
              {assignment ? `Bài tập: ${assignment.title}` : `Câu hỏi thích ứng`}
            </span>
          </div>

          <div className="flex items-center gap-3">
            {/* Timer */}
            <div className="flex items-center gap-2 px-3.5 py-1.5 rounded-xl bg-slate-100 dark:bg-slate-800 text-xs sm:text-sm font-mono font-black text-slate-700 dark:text-slate-200 shadow-2xs border border-slate-200/60 dark:border-slate-700/60">
              <span className="text-slate-400 text-sm">⏱</span>
              <span>
                {String(Math.floor(timeSpentSeconds / 60)).padStart(2, "0")}:
                {String(timeSpentSeconds % 60).padStart(2, "0")}
              </span>
            </div>

            {/* If assignment is submitted: show status badge and NO submit button */}
            {assignmentId && isAssignmentSubmitted && (
              <div className="inline-flex items-center gap-1.5 px-4 py-2 rounded-xl bg-emerald-50 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300 border border-emerald-300 dark:border-emerald-800 text-xs sm:text-sm font-black shadow-xs">
                <span>✓</span>
                <span>Đã nộp bài (Chỉ đọc)</span>
              </div>
            )}

            {/* If assignment is NOT submitted yet: show prominent submit button */}
            {assignmentId && !isAssignmentSubmitted && (
              <button
                type="button"
                onClick={() => setShowBatchConfirmModal(true)}
                disabled={isSubmitting}
                className="hidden sm:inline-flex items-center gap-2 px-5 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-500 text-white font-black text-xs sm:text-sm shadow-sm shadow-indigo-600/25 transition-all cursor-pointer disabled:opacity-50"
              >
                <span>🚀 Nộp bài tập</span>
                <span className="px-2 py-0.5 rounded-full bg-indigo-700 text-[11px] font-extrabold">
                  {answeredCount}/{totalQuestions}
                </span>
              </button>
            )}
          </div>
        </div>
      </header>

      {/* Main Dual-Pane Workspace */}
      <main className="flex-1 max-w-[1700px] w-full mx-auto p-4 sm:p-6">
        <div
          className={`mx-auto transition-all duration-200 ${
            activeSideTool
              ? "grid grid-cols-1 lg:grid-cols-12 gap-6 items-start"
              : "max-w-4xl space-y-6"
          }`}
        >
          {/* Left / Main Workspace */}
          <div className={activeSideTool ? "lg:col-span-7 xl:col-span-7 space-y-6" : "space-y-6"}>
            {submissionError && (
              <div className="rounded-2xl bg-rose-50 dark:bg-rose-950/40 p-4 text-sm font-semibold text-rose-800 dark:text-rose-300 border border-rose-200 dark:border-rose-800">
                <p>{submissionError}</p>
                {canResubmit && (
                  <div className="mt-2">
                    <button
                      type="button"
                      onClick={handleResubmit}
                      className="rounded-xl bg-rose-600 px-4 py-2 text-xs font-bold text-white hover:bg-rose-700 shadow-xs cursor-pointer"
                    >
                      Nộp lại bài làm
                    </button>
                  </div>
                )}
              </div>
            )}

            {/* 1. Question Palette (Thanh chọn câu hỏi 1, 2, 3...) for Assignments */}
            {assignmentId && totalQuestions > 1 && (
              <div className="rounded-2xl bg-white dark:bg-[#0f172a] p-4 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-wrap items-center justify-between gap-3">
                <div className="flex items-center gap-2">
                  <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
                    Danh sách câu:
                  </span>
                  <div className="flex flex-wrap items-center gap-1.5">
                    {assignmentQuestions.map((q, idx) => {
                      const isCurrent = q.questionId === (assignmentQuestion?.questionId || activeQuestionId);
                      const isAnswered = Boolean(assignmentAnswers[q.questionId]?.finalAnswer?.trim());

                      return (
                        <button
                          key={q.questionId}
                          type="button"
                          onClick={() => handleSwitchQuestion(q.questionId)}
                          className={`w-8 h-8 rounded-xl font-black text-xs transition-all cursor-pointer flex items-center justify-center ${
                            isCurrent
                              ? "bg-indigo-600 text-white shadow-md shadow-indigo-600/30 ring-2 ring-indigo-400 ring-offset-2 dark:ring-offset-slate-900 scale-105"
                              : isAnswered
                              ? "bg-emerald-500 text-white shadow-2xs hover:bg-emerald-600"
                              : "bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-slate-700 border border-slate-200/80 dark:border-slate-700"
                          }`}
                          title={`Câu ${idx + 1}: ${isAnswered ? "Đã làm" : "Chưa làm"}`}
                        >
                          {idx + 1}
                        </button>
                      );
                    })}
                  </div>
                </div>

                <div className="text-xs font-bold text-slate-500 dark:text-slate-400 flex items-center gap-1.5">
                  <span>Tiến độ:</span>
                  <span className="text-indigo-600 dark:text-indigo-400 font-extrabold">
                    {answeredCount} / {totalQuestions} câu đã trả lời
                  </span>
                </div>
              </div>
            )}

            {/* 2. Main Question Card */}
            <div className="rounded-3xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 p-6 sm:p-8 shadow-xs space-y-6">
              {/* Question Header */}
              <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-4">
                <div className="flex items-center gap-2.5">
                  <span className="text-sm font-black text-indigo-600 dark:text-indigo-400 bg-indigo-50 dark:bg-indigo-950/60 px-3 py-1 rounded-xl border border-indigo-200/70 dark:border-indigo-800">
                    Câu {currentAssignmentIndex >= 0 ? currentAssignmentIndex + 1 : 1}
                  </span>
                  <span className="rounded-lg bg-amber-50 dark:bg-amber-950/60 border border-amber-200/80 dark:border-amber-800 px-2.5 py-1 text-xs font-bold text-amber-800 dark:text-amber-300">
                    {question?.difficulty && question.difficulty >= 3 ? "Vận dụng" : "Cơ bản"}
                  </span>
                  <span className="text-xs font-medium text-slate-400">
                    Kỳ vọng: {question?.estimatedTimeSeconds || 90}s
                  </span>
                </div>
              </div>

              {/* Review status notice if question has an existing attempt */}
              {assignmentQuestion?.attemptStatus && (
                <div className="rounded-2xl p-3.5 flex flex-wrap items-center justify-between gap-2 bg-amber-50 dark:bg-amber-950/40 border border-amber-200/80 dark:border-amber-800 text-xs">
                  <div className="flex items-center gap-2 text-amber-900 dark:text-amber-200 font-semibold">
                    <span className="text-base">📋</span>
                    <span>
                      {assignmentQuestion.attemptStatus === "NeedsTeacherReview"
                        ? "Câu hỏi này đã được nộp bài và đang chờ giáo viên chấm/duyệt."
                        : assignmentQuestion.attemptStatus === "Completed"
                        ? "Câu hỏi này đã hoàn thành đánh giá."
                        : "Câu hỏi này đang trong hàng đợi phân tích."}
                    </span>
                  </div>
                  <span className="px-2.5 py-1 rounded-full font-bold bg-amber-200/80 dark:bg-amber-900/60 text-amber-900 dark:text-amber-200 shrink-0">
                    Đã nộp · Chế độ xem lại
                  </span>
                </div>
              )}

              {/* Question Text Statement */}
              <div>
                <div className="text-base sm:text-lg font-bold text-slate-900 dark:text-white leading-relaxed whitespace-pre-wrap">
                  {question?.questionText}
                </div>
                {question?.questionText && /[\\[{^_\\]]/.test(question.questionText) && (
                  <div className="mt-3">
                    <MathFormulaPreview
                      formula={question.questionText}
                      label="Hiển thị công thức Toán (KaTeX)"
                    />
                  </div>
                )}
              </div>

              {/* 3. NỔI BẬT: Thanh 3 Nút Công Cụ Trợ Lý (Casio, Nháp, Đồ Thị) bên dưới đề bài */}
              <div className="pt-2">
                <div className="flex items-center justify-between mb-2">
                  <span className="text-[11px] font-extrabold uppercase tracking-wider text-slate-400">
                    Bộ công cụ trợ lý làm bài (Bấm để mở bên phải)
                  </span>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                  {/* Button 1: Máy tính Casio fx-580VN */}
                  <button
                    type="button"
                    onClick={() => setActiveSideTool(activeSideTool === "casio" ? null : "casio")}
                    className={`flex items-center justify-center gap-2.5 p-3.5 rounded-2xl font-bold text-sm transition-all shadow-sm cursor-pointer ${
                      activeSideTool === "casio"
                        ? "bg-amber-500 text-slate-950 shadow-amber-500/30 ring-2 ring-amber-300"
                        : "bg-gradient-to-r from-amber-600 to-amber-500 hover:from-amber-500 hover:to-amber-400 text-white shadow-amber-600/20"
                    }`}
                  >
                    <span className="text-xl leading-none">🖩</span>
                    <div className="text-left">
                      <div className="text-xs font-black">Máy tính Casio</div>
                      <div className="text-[10px] font-medium opacity-90">fx-580VN X chuẩn</div>
                    </div>
                  </button>

                  {/* Button 2: Bảng nháp vẽ tay */}
                  <button
                    type="button"
                    onClick={() => setActiveSideTool(activeSideTool === "scratchpad" ? null : "scratchpad")}
                    className={`flex items-center justify-center gap-2.5 p-3.5 rounded-2xl font-bold text-sm transition-all shadow-sm cursor-pointer ${
                      activeSideTool === "scratchpad"
                        ? "bg-emerald-600 text-white shadow-emerald-600/30 ring-2 ring-emerald-300"
                        : "bg-gradient-to-r from-emerald-600 to-emerald-500 hover:from-emerald-500 hover:to-emerald-400 text-white shadow-emerald-600/20"
                    }`}
                  >
                    <span className="text-xl leading-none">✏️</span>
                    <div className="text-left">
                      <div className="text-xs font-black">Bảng vẽ nháp</div>
                      <div className="text-[10px] font-medium opacity-90">
                        {attachedSnapshotDataUrl ? "✓ Đã đính kèm ảnh" : "Thu phóng & Vẽ tự do"}
                      </div>
                    </div>
                  </button>

                  {/* Button 3: Khảo sát đồ thị */}
                  <button
                    type="button"
                    onClick={() => setActiveSideTool(activeSideTool === "graph" ? null : "graph")}
                    className={`flex items-center justify-center gap-2.5 p-3.5 rounded-2xl font-bold text-sm transition-all shadow-sm cursor-pointer ${
                      activeSideTool === "graph"
                        ? "bg-indigo-600 text-white shadow-indigo-600/30 ring-2 ring-indigo-300"
                        : "bg-gradient-to-r from-indigo-600 to-indigo-500 hover:from-indigo-500 hover:to-indigo-400 text-white shadow-indigo-600/20"
                    }`}
                  >
                    <span className="text-xl leading-none">📈</span>
                    <div className="text-left">
                      <div className="text-xs font-black">Vẽ đồ thị</div>
                      <div className="text-[10px] font-medium opacity-90">Khảo sát hàm số Oxy</div>
                    </div>
                  </button>
                </div>
              </div>

              {/* 4. Answer Section */}
              <div className="border-t border-slate-100 dark:border-slate-800 pt-6">
                <div className="flex items-center justify-between mb-3">
                  <div className="flex items-center gap-2">
                    <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
                      Đáp án của bạn
                    </span>
                    {isAssignmentSubmitted && (
                      <span className="text-xs font-bold text-emerald-700 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-950/50 px-2.5 py-0.5 rounded-lg border border-emerald-200 dark:border-emerald-800">
                        🔒 Đã nộp bài · Chỉ đọc
                      </span>
                    )}
                  </div>
                  {question?.questionType !== "MultipleChoice" && !isReadOnly && (
                    <button
                      type="button"
                      onClick={() => setShowMathToolbar(!showMathToolbar)}
                      className="text-xs font-bold px-2.5 py-1 rounded-lg bg-indigo-50 dark:bg-indigo-950/60 hover:bg-indigo-100 text-indigo-700 dark:text-indigo-300 border border-indigo-200/80 dark:border-indigo-800 transition-colors flex items-center gap-1 cursor-pointer"
                    >
                      <span>∑ Bảng gõ ký hiệu Toán</span>
                      <span>{showMathToolbar ? "▲" : "▼"}</span>
                    </button>
                  )}
                </div>

                {showMathToolbar && question?.questionType !== "MultipleChoice" && !isReadOnly && (
                  <div className="mb-4">
                    <MathInputToolbar onInsert={(sym) => insertTextAtCursor(sym)} disabled={isReadOnly} />
                  </div>
                )}

                {/* Multiple choice grid */}
                {question?.questionType === "MultipleChoice" ? (
                  <fieldset className="grid grid-cols-1 sm:grid-cols-2 gap-3" disabled={isReadOnly}>
                    <legend className="sr-only">Chọn một đáp án</legend>
                    {question.options.map((option) => {
                      const isSelected = finalAnswer === option.optionId;
                      return (
                        <button
                          key={option.optionId}
                          type="button"
                          disabled={isReadOnly}
                          onClick={() => {
                            if (!isReadOnly) handleAnswerChange(option.optionId);
                          }}
                          className={`flex items-center gap-3 p-4 rounded-2xl border text-left transition-all ${
                            isReadOnly ? "cursor-default" : "cursor-pointer"
                          } ${
                            isSelected
                              ? "border-indigo-600 bg-indigo-50/70 dark:bg-indigo-950/60 text-indigo-950 dark:text-indigo-200 ring-2 ring-indigo-500/30 shadow-xs"
                              : "border-slate-200/80 dark:border-slate-800 bg-white dark:bg-slate-900/60 hover:bg-slate-50/80 dark:hover:bg-slate-800/80 text-slate-800 dark:text-slate-200"
                          }`}
                        >
                          <span
                            className={`w-7 h-7 rounded-full font-bold text-xs flex items-center justify-center shrink-0 transition-colors ${
                              isSelected
                                ? "bg-indigo-600 text-white"
                                : "bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300 border border-slate-300 dark:border-slate-700"
                            }`}
                          >
                            {option.label}
                          </span>
                          <div className="flex-1 min-w-0">
                            <span className="text-sm font-semibold truncate block">
                              {option.text}
                            </span>
                            {isSelected && isAssignmentSubmitted && (
                              <span className="text-[11px] font-bold text-indigo-600 dark:text-indigo-400 block mt-0.5">
                                ✓ Đáp án bạn đã nộp
                              </span>
                            )}
                          </div>
                        </button>
                      );
                    })}
                  </fieldset>
                ) : (
                  <div>
                    {/* Visual Math Field (MathLive) */}
                    <VisualMathField
                      ref={visualMathFieldRef}
                      value={finalAnswer}
                      onChange={(val) => handleAnswerChange(val)}
                      disabled={isReadOnly}
                      placeholder={isAssignmentSubmitted ? "Chưa có đáp số" : "Gõ công thức hoặc đáp số cuối cùng (hoặc dùng Casio để tự chèn)..."}
                      autoFocus={!isReadOnly}
                    />
                  </div>
                )}
              </div>

              {/* 5. Reasoning / Solution Section (DUY NHẤT 1 Ô VĂN BẢN, KHÔNG TRÙNG LẶP) */}
              <div className="border-t border-slate-100 dark:border-slate-800 pt-6">
                <div className="flex items-center justify-between mb-2">
                  <label className="text-xs font-bold uppercase tracking-wider text-slate-400">
                    Lập luận tư duy & Trình bày các bước giải
                  </label>
                  {isAssignmentSubmitted ? (
                    <span className="text-xs font-bold text-emerald-700 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-950/50 px-2.5 py-0.5 rounded-lg border border-emerald-200 dark:border-emerald-800">
                      🔒 Đã nộp bài · Chỉ đọc
                    </span>
                  ) : question?.reasoningRequired ? (
                    <span className="text-xs font-bold text-rose-600 dark:text-rose-400 bg-rose-50 dark:bg-rose-950/40 px-2 py-0.5 rounded">
                      Bắt buộc
                    </span>
                  ) : null}
                </div>

                <textarea
                  ref={reasoningTextareaRef}
                  rows={4}
                  value={reasoningText}
                  onFocus={() => setActiveInputTarget("reasoning")}
                  onChange={(e) => {
                    if (!isReadOnly) handleReasoningChange(e.target.value);
                  }}
                  disabled={isReadOnly}
                  readOnly={isAssignmentSubmitted}
                  placeholder={
                    isAssignmentSubmitted
                      ? "Chưa có nội dung lập luận cho câu hỏi này."
                      : "Trình bày các bước biến đổi, suy luận toán học để AI phân tích chất lượng tư duy..."
                  }
                  className={`w-full rounded-2xl border p-4 text-sm font-medium leading-relaxed ${
                    isAssignmentSubmitted
                      ? "bg-slate-50 dark:bg-slate-900/60 border-slate-200 dark:border-slate-800 text-slate-700 dark:text-slate-300 cursor-default select-text"
                      : "bg-white dark:bg-slate-900 border-slate-200 dark:border-slate-700 text-slate-900 dark:text-white focus:border-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500/20"
                  }`}
                />

                {reasoningText.trim() && /[\\[{^_\\]]/.test(reasoningText) && (
                  <div className="mt-2">
                    <MathFormulaPreview
                      formula={reasoningText}
                      label="Xem trước công thức trong lập luận (KaTeX)"
                    />
                  </div>
                )}

                {/* 6. Attached Scratchpad Snapshot Card (Cố định, không bị ảnh hưởng khi vẽ tiếp hay F5) */}
                {attachedSnapshotDataUrl && (
                  <div className="mt-4 rounded-2xl bg-emerald-50/80 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800 p-4 flex items-center justify-between gap-4">
                    <div className="flex items-center gap-3">
                      <img
                        src={attachedSnapshotDataUrl}
                        alt="Bản vẽ nháp đã đính kèm"
                        onClick={() => setShowFullSnapshotModal(true)}
                        className="w-16 h-12 object-cover rounded-xl border border-emerald-300 dark:border-emerald-700 shadow-2xs cursor-pointer hover:scale-105 transition-transform bg-white"
                      />
                      <div>
                        <div className="flex items-center gap-1.5">
                          <span className="text-xs font-black text-emerald-800 dark:text-emerald-300">
                            ✏️ Đã đính kèm ảnh nháp vào bài
                          </span>
                          {attachedSnapshotTime && (
                            <span className="text-[10px] text-emerald-600 dark:text-emerald-400 font-medium">
                              (Lưu lúc {attachedSnapshotTime})
                            </span>
                          )}
                        </div>
                        <p className="text-[11px] text-slate-500 dark:text-slate-400 mt-0.5">
                          Ảnh này đã được lưu cố định và sẽ được gửi kèm khi nộp bài.
                        </p>
                      </div>
                    </div>

                    <div className="flex items-center gap-2">
                      <button
                        type="button"
                        onClick={() => setShowFullSnapshotModal(true)}
                        className="px-2.5 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs cursor-pointer"
                      >
                        🔍 Xem to
                      </button>
                      {!isAssignmentSubmitted && (
                        <button
                          type="button"
                          onClick={handleRemoveSnapshot}
                          className="px-2 py-1.5 rounded-lg text-rose-600 hover:bg-rose-100 dark:hover:bg-rose-950/40 font-bold text-xs cursor-pointer"
                          title="Gỡ ảnh đính kèm này"
                        >
                          Gỡ bỏ
                        </button>
                      )}
                    </div>
                  </div>
                )}
              </div>

              {/* Confidence Slider */}
              <div className="border-t border-slate-100 dark:border-slate-800 pt-6">
                <div className="flex items-center justify-between mb-2">
                  <span className="text-xs font-bold text-slate-700 dark:text-slate-300 uppercase tracking-wider">
                    Mức độ tự tin với câu trả lời
                  </span>
                  <span className="text-sm font-extrabold text-indigo-600 dark:text-indigo-400">
                    {confidence}%
                  </span>
                </div>

                <div className="relative flex items-center">
                  <input
                    type="range"
                    min={10}
                    max={100}
                    step={5}
                    value={confidence}
                    onChange={(e) => {
                      if (!isReadOnly) handleConfidenceChange(Number(e.target.value));
                    }}
                    disabled={isReadOnly}
                    className={`w-full h-2 rounded-lg bg-slate-200 dark:bg-slate-700 appearance-none accent-indigo-600 focus:outline-none ${
                      isReadOnly ? "cursor-default opacity-80" : "cursor-pointer"
                    }`}
                  />
                </div>
              </div>

              {/* Bottom Actions for Question */}
              <div className="flex flex-wrap items-center justify-between border-t border-slate-100 dark:border-slate-800 pt-6 gap-3">
                {/* Previous / Next buttons */}
                {assignmentId && totalQuestions > 1 ? (
                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      disabled={currentAssignmentIndex <= 0}
                      onClick={() => handleSwitchQuestion(assignmentQuestions[currentAssignmentIndex - 1].questionId)}
                      className="px-4 py-2.5 rounded-xl bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-200 font-bold text-xs disabled:opacity-40 cursor-pointer"
                    >
                      ← Câu trước
                    </button>
                    {currentAssignmentIndex < totalQuestions - 1 && (
                      <button
                        type="button"
                        onClick={() => handleSwitchQuestion(assignmentQuestions[currentAssignmentIndex + 1].questionId)}
                        className="px-4 py-2.5 rounded-xl bg-indigo-50 dark:bg-indigo-950/60 hover:bg-indigo-100 text-indigo-700 dark:text-indigo-300 font-bold text-xs cursor-pointer"
                      >
                        Câu tiếp theo →
                      </button>
                    )}
                  </div>
                ) : (
                  <button
                    type="button"
                    onClick={() => handleFinalSubmit()}
                    disabled={isSubmitting}
                    className="text-xs font-bold text-slate-400 hover:text-slate-600 cursor-pointer"
                  >
                    Bỏ qua câu này
                  </button>
                )}

                {/* Final Submit Button (ONLY shown if assignment is NOT submitted yet) */}
                {!isAssignmentSubmitted ? (
                  <button
                    type="button"
                    onClick={() => {
                      if (assignmentId) {
                        setShowBatchConfirmModal(true);
                      } else {
                        handleFinalSubmit();
                      }
                    }}
                    disabled={isSubmitting}
                    className="flex items-center gap-2 rounded-2xl bg-indigo-600 hover:bg-indigo-500 px-7 py-3 text-sm font-bold text-white shadow-md shadow-indigo-600/25 transition-all disabled:opacity-50 cursor-pointer"
                  >
                    <span>
                      {assignmentId
                        ? `🚀 Nộp toàn bộ bài tập (${answeredCount}/${totalQuestions})`
                        : "Nộp bài & Phân tích tư duy AI ✨"}
                    </span>
                  </button>
                ) : (
                  <div className="flex flex-wrap items-center gap-3">
                    <span className="text-xs text-slate-500 dark:text-slate-400 font-medium">
                      🔒 Bài tập đã nộp. Chỉ làm lại khi có yêu cầu từ giáo viên.
                    </span>
                    <Link
                      to={`/hoc-tap/bai-tap/${assignmentId}${subjectId ? `?subjectId=${subjectId}` : ""}`}
                      className="px-5 py-2.5 rounded-xl bg-slate-900 hover:bg-slate-800 text-white dark:bg-slate-700 dark:hover:bg-slate-600 font-bold text-xs shadow-xs transition-colors cursor-pointer"
                    >
                      ‹ Quay lại bài tập
                    </Link>
                  </div>
                )}
              </div>
            </div>
          </div>

          {/* Right Pane: Tall 780px Assistant Workspace */}
          {activeSideTool && (
            <div className="lg:col-span-5 xl:col-span-5 sticky top-4 max-h-[calc(100vh-2rem)] flex flex-col">
              <SideAssistantWorkspace
                activeTab={activeSideTool}
                onChangeTab={(tab) => setActiveSideTool(tab)}
                onClose={() => setActiveSideTool(null)}
                onInsertResult={!isReadOnly ? (val) => insertTextAtCursor(val) : undefined}
                centerId={currentUser?.centerId ?? ""}
                userId={currentUser?.userId ?? ""}
                clientSubmissionId={getClientSubmissionId()}
                isScratchpadAttached={Boolean(attachedSnapshotDataUrl)}
                isReadOnly={isReadOnly}
                onAttachSnapshot={!isReadOnly ? handleAttachSnapshot : undefined}
              />
            </div>
          )}
        </div>
      </main>

      {/* Confirmation Modal when submitting all questions in Assignment Mode */}
      {showBatchConfirmModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs animate-in fade-in duration-150">
          <div className="bg-white dark:bg-slate-900 rounded-3xl max-w-md w-full p-6 shadow-2xl border border-slate-200 dark:border-slate-800 space-y-4">
            <div className="w-12 h-12 rounded-2xl bg-indigo-50 dark:bg-indigo-950/60 text-indigo-600 dark:text-indigo-400 flex items-center justify-center text-2xl font-bold mx-auto">
              📋
            </div>
            <h3 className="text-lg font-black text-center text-slate-900 dark:text-white">
              Xác nhận nộp toàn bộ bài tập
            </h3>
            <p className="text-xs text-center text-slate-500 dark:text-slate-400 leading-relaxed">
              Bạn đã hoàn thành <strong className="text-indigo-600 dark:text-indigo-400">{answeredCount}</strong> / {totalQuestions} câu hỏi.
              {attachedSnapshotDataUrl && (
                <span className="block mt-1 text-emerald-600 dark:text-emerald-400 font-bold">
                  ✓ Kèm theo 1 bản vẽ nháp đã đính kèm.
                </span>
              )}
            </p>

            <div className="pt-2 flex items-center justify-end gap-3">
              <button
                type="button"
                onClick={() => setShowBatchConfirmModal(false)}
                className="px-4 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-700 dark:text-slate-200 font-bold text-xs cursor-pointer"
              >
                Kiểm tra lại
              </button>
              <button
                type="button"
                onClick={handleFinalSubmit}
                className="px-5 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-500 text-white font-black text-xs shadow-md shadow-indigo-600/30 cursor-pointer"
              >
                Xác nhận nộp bài
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Snapshot Full Preview Modal */}
      {showFullSnapshotModal && attachedSnapshotDataUrl && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm animate-in fade-in duration-150">
          <div className="bg-white dark:bg-slate-900 rounded-3xl max-w-3xl w-full p-6 shadow-2xl border border-slate-200 dark:border-slate-800 space-y-4">
            <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
              <span className="text-sm font-bold text-slate-900 dark:text-white">
                Bản vẽ nháp đã đính kèm {attachedSnapshotTime ? `(${attachedSnapshotTime})` : ""}
              </span>
              <button
                type="button"
                onClick={() => setShowFullSnapshotModal(false)}
                className="p-1 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-white font-bold"
              >
                ✕
              </button>
            </div>
            <div className="max-h-[60vh] overflow-auto flex items-center justify-center bg-slate-50 dark:bg-slate-950 p-2 rounded-2xl border border-slate-200 dark:border-slate-800">
              <img
                src={attachedSnapshotDataUrl}
                alt="Full Scratchpad Snapshot"
                className="max-w-full max-h-[55vh] object-contain rounded-xl"
              />
            </div>
            <div className="flex justify-end">
              <button
                type="button"
                onClick={() => setShowFullSnapshotModal(false)}
                className="px-4 py-2 rounded-xl bg-slate-900 text-white dark:bg-white dark:text-slate-900 font-bold text-xs"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
