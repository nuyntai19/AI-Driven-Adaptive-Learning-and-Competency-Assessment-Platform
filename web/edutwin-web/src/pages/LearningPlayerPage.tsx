import { useState, useEffect, useRef, useMemo, useCallback } from "react";
import { Link, useSearchParams, useParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  getNextQuestion,
  submitAttempt,
  getAnalysisJobStatus,
  getAttemptFeedback,
  prepareAttemptAttachmentUpload,
  retryAttemptAIAnalysis,
} from "../api/learningFeedbackApi";
import {
  startStudentAssignment,
  saveAssignmentDraft,
  submitStudentAssignment,
} from "../api/assignmentsApi";
import { useStudentAssignment } from "../features/assignments/useStudentAssignment";
import type {
  NextQuestionDataDto,
  AttemptFeedbackDataDto,
} from "../types/learning";
import {
  isSuccessfulTerminalStatus,
  isTerminalStatus,
  shouldContinuePolling,
  ANALYSIS_FOREGROUND_WAIT_MS,
  shouldShowAnalysisWaitingScreen,
} from "../utils/polling";
import { StudentSubjectRequiredState } from "../components/student/StudentSubjectRequiredState";
import { AttemptFeedbackHierarchy } from "../components/student/AttemptFeedbackHierarchy";
import { AttemptScratchpadAttachment } from "../components/student/AttemptScratchpadAttachment";
import { AssignmentReviewReceipt } from "../components/student/AssignmentReviewReceipt";
import { isAssignmentWorkSubmitted, shouldStartAssignment, getAssignmentReviewTiming, isActiveAssignmentExpired } from "../utils/assignmentReviewTiming";
import { getAttemptFeedbackPresentation, normalizeQuestionScore, fallbackAssignmentGrade } from "../utils/attemptFeedbackPresentation";
import { MathFormulaPreview } from "../components/math/MathFormulaPreview";
import { type VisualMathFieldRef } from "../components/math/VisualMathField";
import { RichMathText } from "../components/math/RichMathText";
import { RichMathEditor, type RichMathEditorRef } from "../components/math/RichMathEditor";
import { ModeAwareAnswerEditor } from "../components/math/answer-editor/ModeAwareAnswerEditor";
import type { AnswerEditorRef } from "../components/math/answer-editor/answerEditorHelpers";
import { SideAssistantWorkspace, type AssistantToolTab } from "../components/math/SideAssistantWorkspace";
import {
  clearAttemptSessionId,
  createClientSubmissionId,
  getOrCreateAttemptSessionId,
  setAttemptSessionId,
} from "../utils/attemptSessionStorage";
import { useAuthStore } from "../stores/authStore";
import {
  isAssignmentReviewHydrated,
  isFeedbackForQuestion,
  resolveQuestionReviewAttemptId,
  isQuestionSubmissionLocked,
  canSubmitLearningWork,
} from "../utils/questionReview";
import {
  buildAssignmentDraftKey,
  clearLegacyAssignmentDraftsForAssignment,
  readAssignmentDraft,
  removeAssignmentDraft,
  writeAssignmentDraft,
  readAssignmentRemainingSeconds,
  writeAssignmentRemainingSeconds,
  removeAssignmentRemainingSeconds,
  type AssignmentDraftScope,
} from "../utils/assignmentDraftStorage";
import {
  executeSnapshotUpload,
  pruneMismatchedSnapshotUploads,
  type SnapshotUploadTracker,
} from "../utils/assignmentSnapshotUploader";

interface StoredAnswer {
  finalAnswer: string;
  answerDisplayLatex: string;
  reasoningText: string;
  confidence: number;
  timeSpentSeconds: number;
  answerChanges: number;
  snapshotDataUrl?: string | null;
  snapshotTime?: string | null;
  drawingUploadToken?: string | null;
}

const EMPTY_ASSIGNMENT_ANSWERS: Record<string, StoredAnswer> = {};

function loadStoredAssignmentAnswers(scope: AssignmentDraftScope | null): Record<string, StoredAnswer> {
  if (!scope) return {};

  try {
    const saved = readAssignmentDraft(scope);
    if (!saved) return {};
    const parsed = JSON.parse(saved);
    if (typeof parsed !== "object" || parsed === null) return {};

    const sanitized: Record<string, StoredAnswer> = {};
    for (const [key, value] of Object.entries(parsed)) {
      if (!value || typeof value !== "object") continue;
      const stored = value as Partial<StoredAnswer>;
      sanitized[key] = {
        finalAnswer: typeof stored.finalAnswer === "string" ? stored.finalAnswer : "",
        answerDisplayLatex:
          typeof stored.answerDisplayLatex === "string"
            ? stored.answerDisplayLatex
            : (typeof stored.finalAnswer === "string" ? stored.finalAnswer : ""),
        reasoningText: typeof stored.reasoningText === "string" ? stored.reasoningText : "",
        confidence: typeof stored.confidence === "number" ? stored.confidence : 80,
        timeSpentSeconds: typeof stored.timeSpentSeconds === "number" ? stored.timeSpentSeconds : 0,
        answerChanges: typeof stored.answerChanges === "number" ? stored.answerChanges : 0,
        snapshotDataUrl: typeof stored.snapshotDataUrl === "string" ? stored.snapshotDataUrl : null,
        snapshotTime: typeof stored.snapshotTime === "string" ? stored.snapshotTime : null,
        drawingUploadToken: typeof stored.drawingUploadToken === "string" ? stored.drawingUploadToken : null,
      };
    }
    return sanitized;
  } catch {
    return {};
  }
}

export const LearningPlayerPage = () => {
  const { questionId: routeQuestionId } = useParams<{ questionId?: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const assignmentId = searchParams.get("assignmentId");
  const subjectId = searchParams.get("subjectId") || "";
  const persistedJobId = searchParams.get("analysisJobId");

  const currentUser = useAuthStore((state) => state.user);
  const queryClient = useQueryClient();

  const assignmentDraftScope = useMemo<AssignmentDraftScope | null>(() => {
    if (!assignmentId || !currentUser) return null;
    return {
      centerId: currentUser.centerId,
      userId: currentUser.userId,
      assignmentId,
    };
  }, [assignmentId, currentUser]);
  const assignmentDraftKey = assignmentDraftScope
    ? buildAssignmentDraftKey(assignmentDraftScope)
    : null;

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
  const activeQuestionIdRef = useRef<string>(routeQuestionId || "");
  activeQuestionIdRef.current = activeQuestionId;

  // Batch Assignment Answers state (mapped by questionId)
  const [assignmentAnswersState, setAssignmentAnswersState] = useState<{
    scopeKey: string | null;
    answers: Record<string, StoredAnswer>;
  }>(() => ({
    scopeKey: assignmentDraftKey,
    answers: loadStoredAssignmentAnswers(assignmentDraftScope),
  }));
  const assignmentAnswers = assignmentAnswersState.scopeKey === assignmentDraftKey
    ? assignmentAnswersState.answers
    : EMPTY_ASSIGNMENT_ANSWERS;
  const assignmentAnswersRef = useRef(assignmentAnswers);
  assignmentAnswersRef.current = assignmentAnswers;
  const [assignmentDraftLoadVersion, setAssignmentDraftLoadVersion] = useState(0);
  const setAssignmentAnswers = useCallback((
    update:
      | Record<string, StoredAnswer>
      | ((previous: Record<string, StoredAnswer>) => Record<string, StoredAnswer>)
  ) => {
    setAssignmentAnswersState((previousState) => {
      const previousAnswers = previousState.scopeKey === assignmentDraftKey
        ? previousState.answers
        : EMPTY_ASSIGNMENT_ANSWERS;
      const nextAnswers = typeof update === "function" ? update(previousAnswers) : update;
      return { scopeKey: assignmentDraftKey, answers: nextAnswers };
    });
  }, [assignmentDraftKey]);

  // Attached Scratchpad Snapshot State (Stored independently from scratchpad edits)
  const [attachedSnapshotDataUrl, setAttachedSnapshotDataUrl] = useState<string | null>(null);
  const attachedSnapshotDataUrlRef = useRef(attachedSnapshotDataUrl);
  attachedSnapshotDataUrlRef.current = attachedSnapshotDataUrl;
  const effectiveQuestionIdRef = useRef<string | null>(null);
  const [attachedSnapshotBlob, setAttachedSnapshotBlob] = useState<Blob | null>(null);
  const [attachedSnapshotTime, setAttachedSnapshotTime] = useState<string | null>(null);
  const [showFullSnapshotModal, setShowFullSnapshotModal] = useState<boolean>(false);

  // Current question input state
  const [finalAnswer, setFinalAnswer] = useState<string>("");
  const [answerDisplayLatex, setAnswerDisplayLatex] = useState<string>("");
  const [reasoningText, setReasoningText] = useState<string>("");
  const [confidence, setConfidence] = useState<number>(80);
  const [answerChanges, setAnswerChanges] = useState<number>(0);
  const answerChangesRef = useRef<number>(0);
  const [timeSpentSeconds, setTimeSpentSeconds] = useState<number>(0);

  // Assistant tools state
  const [activeSideTool, setActiveSideTool] = useState<AssistantToolTab | null>(null);
  const [drawingUploadToken, setDrawingUploadToken] = useState<string | null>(null);

  // Input refs and cursor management
  const reasoningTextareaRef = useRef<HTMLTextAreaElement>(null);
  const reasoningEditorRef = useRef<RichMathEditorRef>(null);
  const visualMathFieldRef = useRef<VisualMathFieldRef>(null);
  const answerEditorRef = useRef<AnswerEditorRef>(null);
  const [activeInputTarget, setActiveInputTarget] = useState<"answer" | "reasoning">("answer");

  // Client submission token (unique per attempt session)
  const clientSubmissionIdRef = useRef<string>(createClientSubmissionId());

  // Local submission state flag to immediately transition to submitted/result mode once save completes
  const [isLocallySubmitted, setIsLocallySubmitted] = useState<boolean>(false);

  // Workflow state
  const [isSubmitting, setIsSubmitting] = useState<boolean>(Boolean(persistedJobId));
  const [pollingJobId, setPollingJobId] = useState<string | null>(persistedJobId);
  const [backgroundAnalysisJobId, setBackgroundAnalysisJobId] = useState<string | null>(null);
  const [pollingStatus, setPollingStatus] = useState<string>("Đang xử lý...");
  const [feedbackData, setFeedbackData] = useState<AttemptFeedbackDataDto | null>(null);
  const [isRefreshingAssignmentReview, setIsRefreshingAssignmentReview] = useState(false);

  // Submission error (only for actual network/validation failure before attempt is saved to DB)
  const [submissionSaveError, setSubmissionSaveError] = useState<string | null>(null);
  const [canRetrySubmission, setCanRetrySubmission] = useState<boolean>(false);
  const [startAssignmentError, setStartAssignmentError] = useState<string | null>(null);
  const targetEndTimestampRef = useRef<number | null>(null);

  // Asynchronous AI Analysis Banner state (completely separate from submission status)
  const [aiBanner, setAiBanner] = useState<{
    type: "info" | "warning";
    message: string;
    action?: "retry_ai" | "resume_poll" | null;
    attemptIdForRetry?: string | null;
  } | null>(null);
  const [isRetryingAiFromBanner, setIsRetryingAiFromBanner] = useState<boolean>(false);

  const [showBatchConfirmModal, setShowBatchConfirmModal] = useState<boolean>(false);
  const pollingAttemptRef = useRef(0);
  const consecutiveNetworkErrorsRef = useRef(0);
  const [networkErrorPaused, setNetworkErrorPaused] = useState<boolean>(false);

  // Frozen payload reference to guarantee identical timeSpentSeconds across retries
  const frozenPayloadRef = useRef<Record<string, {
    finalAnswer: string;
    answerDisplayLatex?: string;
    reasoningText?: string;
    confidence: number;
    timeSpentSeconds: number;
    answerChanges: number;
    drawingUploadToken?: string;
  }> | null>(null);

  // Mode 1: Assignment Mode Query
  const {
    data: assignmentResponse,
    isLoading: assignmentLoading,
    isError: assignmentError,
    refetch: refetchAssignment,
  } = useStudentAssignment(assignmentId || undefined);

  const assignment = assignmentResponse?.data;
  const assignmentQuestions = useMemo(
    () => assignment?.questions || [],
    [assignment?.questions]
  );
  const isAssignmentSubmitted = isAssignmentWorkSubmitted(assignment, isLocallySubmitted);
  const canStartAssignment = shouldStartAssignment(assignment, isLocallySubmitted);
  const submissionTiming = getAssignmentReviewTiming(assignment);
  const assignmentReviewRef = useRef(isAssignmentSubmitted);
  assignmentReviewRef.current = isAssignmentSubmitted;

  const storeAttemptFeedback = useCallback((data: AttemptFeedbackDataDto) => {
    queryClient.setQueryData(
      ["attempt-feedback", currentUser?.centerId, currentUser?.userId, String(data.attemptId)],
      data,
    );
    setFeedbackData(data);
  }, [queryClient, currentUser?.centerId, currentUser?.userId]);

  const refreshSubmittedAssignmentData = useCallback(async (): Promise<boolean> => {
    if (!assignmentId) return false;

    try {
      const [detailResult] = await Promise.all([
        refetchAssignment(),
        queryClient.invalidateQueries({ queryKey: ["student-assignments"] }),
        queryClient.invalidateQueries({ queryKey: ["attempt-feedback", currentUser?.centerId, currentUser?.userId] }),
      ]);
      const refreshedQuestions = detailResult.data?.data?.questions ?? [];
      const isHydrated = detailResult.isSuccess && isAssignmentReviewHydrated(
        refreshedQuestions,
        assignmentQuestions.length
      );

      if (isHydrated) {
        if (assignmentDraftScope) {
          removeAssignmentDraft(assignmentDraftScope);
          removeAssignmentRemainingSeconds(assignmentDraftScope);
        }
        setAssignmentAnswers({});
      }

      return isHydrated;
    } catch {
      // Submission has already been accepted. Keep the scoped local draft so
      // review mode can still render answers until a later refresh succeeds.
      return false;
    }
  }, [
    assignmentDraftScope,
    assignmentId,
    assignmentQuestions.length,
    currentUser?.centerId,
    currentUser?.userId,
    queryClient,
    refetchAssignment,
    setAssignmentAnswers,
  ]);

  // Assignment Timers:
  // - assignmentRemainingSeconds: absolute server deadline; stops in review mode.
  // - dueRemainingSeconds: due date countdown (ONLY shown when assignment has no active time limit, but has a due date).
  const [assignmentRemainingSeconds, setAssignmentRemainingSeconds] = useState<number | null>(null);
  const [dueRemainingSeconds, setDueRemainingSeconds] = useState<number | null>(null);
  const [draftSaveStatus, setDraftSaveStatus] = useState<"idle" | "saving" | "saved" | "error">("idle");
  const [lastDraftSavedTime, setLastDraftSavedTime] = useState<string | null>(null);
  const firstUnsavedChangeTimeRef = useRef<number | null>(null);
  const saveVersionRef = useRef<number>(0);
  const lastSavedVersionRef = useRef<number>(0);
  const latestQueuedVersionRef = useRef<number>(0);
  const saveQueueRef = useRef<Promise<void>>(Promise.resolve());
  const isDraftSavingRef = useRef<boolean>(false);
  const hasAutoSubmittedRef = useRef(false);
  const submissionInFlightRef = useRef(false);
  const isInitializedRef = useRef(false);
  const timerInitializedForAssignmentRef = useRef<string | null>(null);
  const previousAssignmentDraftKeyRef = useRef(assignmentDraftKey);
  const snapshotUploadSeqRef = useRef<number>(0);
  const snapshotUploadsRef = useRef<Record<string, SnapshotUploadTracker>>({});
  const snapshotScopeKey = assignmentDraftKey ??
    `${currentUser?.centerId ?? ""}:${currentUser?.userId ?? ""}:adaptive:${subjectId ?? ""}`;
  const snapshotScopeRef = useRef(snapshotScopeKey);
  snapshotScopeRef.current = snapshotScopeKey;

  useEffect(() => {
    snapshotUploadsRef.current = {};
    return () => {
      // Invalidate callbacks on scope change or unmount without reusing upload IDs.
      snapshotUploadsRef.current = {};
    };
  }, [snapshotScopeKey]);

  const [draftConflict, setDraftConflict] = useState<{
    serverVersion?: number;
    message: string;
  } | null>(null);
  const isDraftConflictRef = useRef<boolean>(false);

  const hasTimeLimit = Boolean(assignment?.timeLimitMinutes && assignment.timeLimitMinutes > 0);

  // Never carry local component state across assignment or authenticated-user scopes.
  useEffect(() => {
    if (assignmentId) {
      clearLegacyAssignmentDraftsForAssignment(assignmentId);
    }

    if (previousAssignmentDraftKeyRef.current === assignmentDraftKey) return;
    previousAssignmentDraftKeyRef.current = assignmentDraftKey;

    setAssignmentAnswers(loadStoredAssignmentAnswers(assignmentDraftScope));
    setAssignmentDraftLoadVersion((version) => version + 1);
    setActiveQuestionId(routeQuestionId || "");
    setAttachedSnapshotDataUrl(null);
    setAttachedSnapshotBlob(null);
    setAttachedSnapshotTime(null);
    setShowFullSnapshotModal(false);
    setFinalAnswer("");
    setAnswerDisplayLatex("");
    setReasoningText("");
    setConfidence(80);
    setAnswerChanges(0);
    answerChangesRef.current = 0;
    setTimeSpentSeconds(0);
    setActiveSideTool(null);
    setDrawingUploadToken(null);
    setActiveInputTarget("answer");
    clientSubmissionIdRef.current = createClientSubmissionId();
    setIsLocallySubmitted(false);
    setIsSubmitting(false);
    setPollingJobId(null);
    setPollingStatus("Đang xử lý...");
    setFeedbackData(null);
    setIsRefreshingAssignmentReview(false);
    setSubmissionSaveError(null);
    setCanRetrySubmission(false);
    setAiBanner(null);
    setIsRetryingAiFromBanner(false);
    setShowBatchConfirmModal(false);
    pollingAttemptRef.current = 0;
    consecutiveNetworkErrorsRef.current = 0;
    setNetworkErrorPaused(false);
    frozenPayloadRef.current = null;
    setAssignmentRemainingSeconds(null);
    setDueRemainingSeconds(null);
    targetEndTimestampRef.current = null;
    setStartAssignmentError(null);
    hasAutoSubmittedRef.current = false;
    isInitializedRef.current = false;
    timerInitializedForAssignmentRef.current = null;
  }, [
    assignmentDraftKey,
    assignmentDraftScope,
    assignmentId,
    routeQuestionId,
    setAssignmentAnswers,
  ]);

  // Start only unsubmitted work, after the detail query has established its state.
  useEffect(() => {
    if (!assignmentId || !assignmentDraftKey || assignmentLoading ||
        assignment?.assignmentId !== assignmentId || !canStartAssignment) return;
    let cancelled = false;
    startStudentAssignment(assignmentId)
      .then((res) => {
        if (cancelled || assignmentReviewRef.current) return;
        setStartAssignmentError(null);
        queryClient.setQueryData(["student-assignment", currentUser?.centerId, currentUser?.userId, assignmentId], res);
      })
      .catch(async (err: unknown) => {
        if (cancelled || assignmentReviewRef.current) return;
        const latest = await refetchAssignment();
        if (cancelled || assignmentReviewRef.current || isAssignmentWorkSubmitted(latest.data?.data)) return;
        const errObj = err as { response?: { data?: { error?: { message?: string }; message?: string } } };
        const msg =
          errObj?.response?.data?.error?.message ||
          errObj?.response?.data?.message ||
          "Không thể bắt đầu làm bài tập hoặc bài tập đã hết hạn.";
        setStartAssignmentError(msg);
      });
    return () => { cancelled = true; };
  }, [assignmentId, assignmentDraftKey, assignmentLoading, assignment?.assignmentId, canStartAssignment,
    isAssignmentSubmitted, queryClient, currentUser?.centerId, currentUser?.userId, refetchAssignment]);



  // Initialize or update activeQuestionId when assignment loads
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

  const immediateQuestionFeedback = feedbackData && assignmentQuestion &&
    isFeedbackForQuestion(assignmentQuestion.questionId, feedbackData.questionId)
    ? feedbackData : null;
  const reviewAttemptId = resolveQuestionReviewAttemptId(assignmentQuestion) ??
    immediateQuestionFeedback?.attemptId ?? null;

  const reviewFeedbackQuery = useQuery<AttemptFeedbackDataDto>({
    queryKey: ["attempt-feedback", currentUser?.centerId, currentUser?.userId, reviewAttemptId == null ? "none" : String(reviewAttemptId)],
    queryFn: () => getAttemptFeedback(reviewAttemptId!),
    enabled: Boolean(assignmentId && reviewAttemptId),
    initialData: immediateQuestionFeedback ?? undefined,
    retry: 1,
    refetchInterval: ({ state }) => ["PendingAnalysis", "Processing"].includes(state.data?.status ?? "") ? 3000 : false,
  });

  const isCurrentQuestionSubmitted = isQuestionSubmissionLocked(assignmentQuestion) ||
    Boolean(immediateQuestionFeedback);

  // Reviewing submitted work must not change the recorded working time.
  useEffect(() => {
    if (pollingJobId || feedbackData || isAssignmentSubmitted || isCurrentQuestionSubmitted) return;
    const timer = setInterval(() => setTimeSpentSeconds((prev) => prev + 1), 1000);
    return () => clearInterval(timer);
  }, [pollingJobId, feedbackData, isAssignmentSubmitted, isCurrentQuestionSubmitted]);

  // Initialize timer for timed or untimed assignments from server authoritative remainingSeconds or effectiveExpiresAt
  useEffect(() => {
    if (!assignment || !assignmentId) return;
    if (isAssignmentSubmitted) {
      targetEndTimestampRef.current = null;
      setAssignmentRemainingSeconds(null);
      setDueRemainingSeconds(null);
      setStartAssignmentError(null);
      if (assignmentDraftScope) removeAssignmentRemainingSeconds(assignmentDraftScope);
      return;
    }

    if (hasTimeLimit) {
      if (assignment.effectiveExpiresAt) {
        const targetMs = new Date(assignment.effectiveExpiresAt).getTime();
        targetEndTimestampRef.current = targetMs;
        const initialSeconds = Math.max(0, Math.floor((targetMs - Date.now()) / 1000));
        setAssignmentRemainingSeconds(initialSeconds);
        timerInitializedForAssignmentRef.current = assignmentId;
        if (assignmentDraftScope) {
          writeAssignmentRemainingSeconds(assignmentDraftScope, initialSeconds);
        }
      } else if (assignment.remainingSeconds !== undefined && assignment.remainingSeconds !== null) {
        targetEndTimestampRef.current = Date.now() + assignment.remainingSeconds * 1000;
        setAssignmentRemainingSeconds(assignment.remainingSeconds);
        timerInitializedForAssignmentRef.current = assignmentId;
        if (assignmentDraftScope) {
          writeAssignmentRemainingSeconds(assignmentDraftScope, assignment.remainingSeconds);
        }
      } else {
        const savedRemaining = assignmentDraftScope ? readAssignmentRemainingSeconds(assignmentDraftScope) : null;
        if (savedRemaining !== null) {
          targetEndTimestampRef.current = Date.now() + savedRemaining * 1000;
          setAssignmentRemainingSeconds(savedRemaining);
          timerInitializedForAssignmentRef.current = assignmentId;
        } else if (timerInitializedForAssignmentRef.current !== assignmentId) {
          const initialSeconds = assignment.timeLimitMinutes! * 60;
          targetEndTimestampRef.current = Date.now() + initialSeconds * 1000;
          setAssignmentRemainingSeconds(initialSeconds);
          timerInitializedForAssignmentRef.current = assignmentId;
        }
      }
    } else {
      targetEndTimestampRef.current = null;
      setAssignmentRemainingSeconds(null);
      timerInitializedForAssignmentRef.current = assignmentId;
    }
  }, [assignment, assignmentId, hasTimeLimit, assignmentDraftScope, isAssignmentSubmitted]);

  const isTimerTicking =
    hasTimeLimit &&
    !isAssignmentSubmitted &&
    !isLocallySubmitted &&
    assignmentRemainingSeconds !== null &&
    assignmentRemainingSeconds > 0;

  // Active test countdown tick for timed assignments (authoritative continuous timer calculating from absolute timestamp)
  useEffect(() => {
    if (!isTimerTicking) return;

    const calcRemaining = () => {
      if (!targetEndTimestampRef.current) return;
      const msLeft = targetEndTimestampRef.current - Date.now();
      const diff = Math.max(0, Math.ceil(msLeft / 1000));
      setAssignmentRemainingSeconds(diff);
      if (assignmentDraftScope) {
        writeAssignmentRemainingSeconds(assignmentDraftScope, diff);
      }
    };

    const timer = setInterval(calcRemaining, 1000);

    const handleVisibilityOrFocus = () => {
      if (document.visibilityState === "visible") {
        calcRemaining();
      }
    };

    window.addEventListener("focus", calcRemaining);
    document.addEventListener("visibilitychange", handleVisibilityOrFocus);

    return () => {
      clearInterval(timer);
      window.removeEventListener("focus", calcRemaining);
      document.removeEventListener("visibilitychange", handleVisibilityOrFocus);
    };
  }, [isTimerTicking, assignmentDraftScope]);

  // Countdown to deadline ONLY when teacher configured NO time limit (không giới hạn thời gian)
  useEffect(() => {
    if (hasTimeLimit || !assignment?.dueAt || isAssignmentSubmitted || isLocallySubmitted) {
      setDueRemainingSeconds(null);
      return;
    }

    const calcDueRemaining = () => {
      const msLeft = new Date(assignment.dueAt!).getTime() - Date.now();
      const diff = Math.max(0, Math.ceil(msLeft / 1000));
      setDueRemainingSeconds(diff);
    };

    calcDueRemaining();
    const interval = setInterval(calcDueRemaining, 1000);
    return () => clearInterval(interval);
  }, [hasTimeLimit, assignment?.dueAt, isAssignmentSubmitted, isLocallySubmitted]);

  // Separation: True authoritative expiration timestamp vs integer seconds for display
  const isAssignmentExpired = isActiveAssignmentExpired(isAssignmentSubmitted,
    hasTimeLimit ? targetEndTimestampRef.current : assignment?.dueAt ? Date.parse(assignment.dueAt) : null);

  // Hydrate draft answers from server if local draft is empty
  useEffect(() => {
    if (!assignment?.draftAnswers || assignment.draftAnswers.length === 0) return;
    if (isAssignmentSubmitted || isLocallySubmitted) return;

    if (assignment.draftVersion) {
      saveVersionRef.current = Math.max(saveVersionRef.current, assignment.draftVersion);
      lastSavedVersionRef.current = Math.max(lastSavedVersionRef.current, assignment.draftVersion);
    }

    setAssignmentAnswers((prev) => {
      if (Object.keys(prev).length > 0) return prev;
      const serverAnswers: Record<string, StoredAnswer> = {};
      for (const da of assignment.draftAnswers!) {
        serverAnswers[String(da.questionId)] = {
          finalAnswer: da.finalAnswer || "",
          answerDisplayLatex: da.answerDisplayLatex || "",
          reasoningText: da.reasoningText || "",
          confidence: da.confidence ?? 80,
          timeSpentSeconds: da.timeSpentSeconds ?? 0,
          answerChanges: da.answerChanges ?? 0,
          drawingUploadToken: da.drawingUploadToken || null,
        };
      }
      return serverAnswers;
    });
  }, [assignment?.draftAnswers, assignment?.draftVersion, isAssignmentSubmitted, isLocallySubmitted, setAssignmentAnswers]);

  const uploadQuestionSnapshot = useCallback(
    async (
      qId: string,
      dataUrl: string,
      blob?: Blob | null
    ): Promise<string | null> => {
      if (!qId || !dataUrl || !currentUser) return null;

      return executeSnapshotUpload({
        qId,
        dataUrl,
        blob,
        snapshotUploadSeqRef,
        snapshotUploadsRef,
        isCurrentScope: () => snapshotScopeRef.current === snapshotScopeKey,
        getCurrentDraftAnswers: () => assignmentDraftScope
          ? assignmentAnswersRef.current
          : effectiveQuestionIdRef.current === qId
            ? { [qId]: { snapshotDataUrl: attachedSnapshotDataUrlRef.current } }
            : {},
        prepareUpload: prepareAttemptAttachmentUpload,
        getActiveQuestionId: () => assignmentDraftScope
          ? activeQuestionIdRef.current
          : effectiveQuestionIdRef.current,
        onActiveQuestionTokenUpdated: setDrawingUploadToken,
        onTokenStored: (questionId, token) => {
          if (!assignmentDraftScope) return;
          setAssignmentAnswers((prev) => {
            const existingAns = prev[questionId];
            if (!existingAns || existingAns.snapshotDataUrl !== dataUrl) return prev;
            const next = {
              ...prev,
              [questionId]: {
                ...existingAns,
                drawingUploadToken: token,
              },
            };
            writeAssignmentDraft(assignmentDraftScope, JSON.stringify(next));
            return next;
          });
        },
      });
    },
    [currentUser, assignmentDraftScope, setAssignmentAnswers, snapshotScopeKey]
  );

  const performSaveDraft = useCallback(
    async (answersToSave: Record<string, StoredAnswer>, version: number) => {
      if (!assignmentId || isAssignmentSubmitted || isLocallySubmitted || isAssignmentExpired) return;
      if (isDraftConflictRef.current) return;
      if (Object.keys(answersToSave).length === 0) return;

      latestQueuedVersionRef.current = Math.max(latestQueuedVersionRef.current, version);

      saveQueueRef.current = saveQueueRef.current.then(async () => {
        if (isDraftConflictRef.current) return;
        if (version < latestQueuedVersionRef.current) {
          // A newer version was queued while this task was waiting
          return;
        }

        // Upload scratchpad snapshots before saving draft using unified upload mechanism
        for (const [qId, ans] of Object.entries(answersToSave)) {
          if (ans.snapshotDataUrl && !ans.drawingUploadToken) {
            const token = await uploadQuestionSnapshot(qId, ans.snapshotDataUrl);
            if (token) {
              ans.drawingUploadToken = token;
            }
          }
        }

        if (isDraftConflictRef.current) return;

        const answersPayload = Object.entries(answersToSave).map(([qId, ans]) => ({
          questionId: Number(qId),
          finalAnswer: ans.finalAnswer || undefined,
          answerDisplayLatex: ans.answerDisplayLatex || undefined,
          reasoningText: ans.reasoningText || undefined,
          confidence: ans.confidence,
          timeSpentSeconds: ans.timeSpentSeconds,
          answerChanges: ans.answerChanges,
          drawingUploadToken: ans.drawingUploadToken || undefined,
        }));

        setDraftSaveStatus("saving");
        isDraftSavingRef.current = true;
        try {
          const res = (await saveAssignmentDraft(assignmentId, { answers: answersPayload, draftVersion: version })) as {
            success?: boolean;
            draftVersion?: number;
          };
          const savedVersion = res?.draftVersion ?? version;
          if (savedVersion >= lastSavedVersionRef.current) {
            lastSavedVersionRef.current = savedVersion;
            saveVersionRef.current = Math.max(saveVersionRef.current, savedVersion);
            setDraftSaveStatus("saved");
            const nowStr = new Date().toLocaleTimeString("vi-VN", {
              hour: "2-digit",
              minute: "2-digit",
              second: "2-digit",
            });
            setLastDraftSavedTime(nowStr);
            firstUnsavedChangeTimeRef.current = null;
          }
        } catch (err: unknown) {
          const errObj = err as {
            response?: {
              status?: number;
              data?: {
                detail?: string;
                draftVersion?: number;
                extensions?: { draftVersion?: number; errorCode?: string };
              };
            };
            message?: string;
          };

          const is409 =
            errObj?.response?.status === 409 ||
            errObj?.response?.data?.extensions?.errorCode === "CONCURRENCY_CONFLICT";

          if (is409) {
            isDraftConflictRef.current = true;
            const serverVersion =
              errObj?.response?.data?.draftVersion ??
              errObj?.response?.data?.extensions?.draftVersion;

            setDraftConflict({
              serverVersion,
              message: "Phát hiện phiên bản mới hơn trên máy chủ từ thiết bị hoặc thẻ duyệt khác. Tự động lưu đã tạm dừng để bảo vệ bài làm của bạn.",
            });
            setDraftSaveStatus("error");
            console.warn("Draft concurrency conflict (409): Halting autosaves until resolved. Server version:", serverVersion);
            return;
          }

          console.warn("Draft auto-save notice:", errObj?.message);
          setDraftSaveStatus("error");
        } finally {
          isDraftSavingRef.current = false;
        }
      });
    },
    [assignmentId, isAssignmentSubmitted, isLocallySubmitted, isAssignmentExpired, uploadQuestionSnapshot]
  );

  const handleSyncWithServer = useCallback(async () => {
    try {
      setDraftSaveStatus("saving");
      const refreshed = await refetchAssignment();
      if (!refreshed.isSuccess || !refreshed.data?.data) {
        setDraftSaveStatus("error");
        console.warn("Failed to refetch assignment draft from server (isSuccess=false or missing data). Preserving local draft and conflict state.");
        return;
      }

      const serverData = refreshed.data.data;
      const serverDraft = serverData.draftAnswers || [];
      const serverVersion = serverData.draftVersion ?? 0;

      const serverAnswers: Record<string, StoredAnswer> = {};
      for (const da of serverDraft) {
        serverAnswers[da.questionId.toString()] = {
          finalAnswer: da.finalAnswer || "",
          answerDisplayLatex: da.answerDisplayLatex || "",
          reasoningText: da.reasoningText || "",
          confidence: da.confidence ?? 80,
          timeSpentSeconds: da.timeSpentSeconds ?? 0,
          answerChanges: da.answerChanges ?? 0,
          drawingUploadToken: da.drawingUploadToken || null,
        };
      }

      setAssignmentAnswers(serverAnswers);
      if (assignmentDraftScope) {
        writeAssignmentDraft(assignmentDraftScope, JSON.stringify(serverAnswers));
      }
      pruneMismatchedSnapshotUploads(snapshotUploadsRef, serverAnswers);

      // Synchronize the currently active question inputs (answer, reasoning, behavioral, snapshot)
      const activeQId = assignmentQuestion?.questionId ? assignmentQuestion.questionId.toString() : activeQuestionIdRef.current;
      const activeAnswer = serverAnswers[activeQId];
      if (activeAnswer) {
        setFinalAnswer(activeAnswer.finalAnswer || "");
        setAnswerDisplayLatex(assignmentQuestion?.questionType === "MultipleChoice" ? "" : (activeAnswer.answerDisplayLatex || activeAnswer.finalAnswer || ""));
        setReasoningText(activeAnswer.reasoningText || "");
        setConfidence(activeAnswer.confidence ?? 80);
        setTimeSpentSeconds(activeAnswer.timeSpentSeconds ?? 0);
        const initialChanges = activeAnswer.answerChanges ?? 0;
        answerChangesRef.current = initialChanges;
        setAnswerChanges(initialChanges);
        setAttachedSnapshotDataUrl(activeAnswer.snapshotDataUrl || null);
        setAttachedSnapshotBlob(null);
        setAttachedSnapshotTime(activeAnswer.snapshotTime || null);
        setDrawingUploadToken(activeAnswer.drawingUploadToken || null);
      } else {
        setFinalAnswer("");
        setAnswerDisplayLatex("");
        setReasoningText("");
        setConfidence(80);
        setTimeSpentSeconds(0);
        answerChangesRef.current = 0;
        setAnswerChanges(0);
        setAttachedSnapshotDataUrl(null);
        setAttachedSnapshotBlob(null);
        setAttachedSnapshotTime(null);
        setDrawingUploadToken(null);
      }
      setAssignmentDraftLoadVersion((v) => v + 1);

      saveVersionRef.current = serverVersion;
      lastSavedVersionRef.current = serverVersion;
      latestQueuedVersionRef.current = serverVersion;
      isDraftConflictRef.current = false;
      setDraftConflict(null);
      setDraftSaveStatus("saved");
      const nowStr = new Date().toLocaleTimeString("vi-VN", {
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
      });
      setLastDraftSavedTime(nowStr);
    } catch (e) {
      console.error("Failed to sync draft with server:", e);
      setDraftSaveStatus("error");
    }
  }, [
    refetchAssignment,
    assignmentDraftScope,
    assignmentQuestion?.questionId,
    assignmentQuestion?.questionType,
    setAssignmentAnswers,
  ]);

  const handleForceOverwriteLocal = useCallback(() => {
    const targetVersion = (draftConflict?.serverVersion ?? saveVersionRef.current) + 1;
    saveVersionRef.current = targetVersion;
    lastSavedVersionRef.current = targetVersion - 1;
    latestQueuedVersionRef.current = targetVersion;
    isDraftConflictRef.current = false;
    setDraftConflict(null);
    performSaveDraft(assignmentAnswers, targetVersion);
  }, [assignmentAnswers, draftConflict?.serverVersion, performSaveDraft]);

  // Periodic auto-save draft to server before deadline with max wait limit and version tracking
  const autoSaveTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  useEffect(() => {
    if (!assignmentId || isAssignmentSubmitted || isLocallySubmitted || isAssignmentExpired || isDraftConflictRef.current) return;
    if (Object.keys(assignmentAnswers).length === 0) return;

    saveVersionRef.current += 1;
    const currentVersion = saveVersionRef.current;
    const now = Date.now();

    if (firstUnsavedChangeTimeRef.current === null) {
      firstUnsavedChangeTimeRef.current = now;
    }

    const elapsed = now - firstUnsavedChangeTimeRef.current;
    const MAX_WAIT_MS = 5000;
    const DEBOUNCE_MS = 2000;

    if (elapsed >= MAX_WAIT_MS) {
      if (autoSaveTimeoutRef.current) {
        clearTimeout(autoSaveTimeoutRef.current);
        autoSaveTimeoutRef.current = null;
      }
      performSaveDraft(assignmentAnswers, currentVersion);
    } else {
      const remainingWait = MAX_WAIT_MS - elapsed;
      const delay = Math.min(DEBOUNCE_MS, Math.max(0, remainingWait));

      if (autoSaveTimeoutRef.current) {
        clearTimeout(autoSaveTimeoutRef.current);
      }

      autoSaveTimeoutRef.current = setTimeout(() => {
        performSaveDraft(assignmentAnswers, currentVersion);
      }, delay);
    }

    return () => {
      if (autoSaveTimeoutRef.current) {
        clearTimeout(autoSaveTimeoutRef.current);
      }
    };
  }, [assignmentId, assignmentAnswers, isAssignmentSubmitted, isLocallySubmitted, isAssignmentExpired, performSaveDraft]);

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
        answerEvaluationMode: assignmentQuestion.answerEvaluationMode || "TextExact",
      };
    }
    return adaptiveQuestion || null;
  }, [assignment, assignmentQuestion, adaptiveQuestion]);
  effectiveQuestionIdRef.current = question ? String(question.questionId) : null;

  const questionLoading = assignmentId ? assignmentLoading : adaptiveLoading;
  const questionError = assignmentId ? assignmentError : adaptiveError;

  // Sync form inputs when switching active question in assignment mode
  useEffect(() => {
    if (!question?.questionId || !assignmentId) return;
    const qId = question.questionId;
    const saved = assignmentAnswersRef.current[qId];

    const isQuestionSubmitted =
      assignmentQuestion?.attemptStatus === "Completed" ||
      assignmentQuestion?.attemptStatus === "NeedsTeacherReview" ||
      Boolean(assignmentQuestion?.latestAttempt) ||
      Boolean(assignmentQuestion?.submittedAnswer) ||
      isAssignmentSubmitted;

    const effectiveSubmittedAnswer =
      assignmentQuestion?.submittedAnswer !== undefined && assignmentQuestion?.submittedAnswer !== null
        ? (assignmentQuestion.submittedAnswer === "SKIPPED" ? "" : assignmentQuestion.submittedAnswer)
        : (assignmentQuestion?.latestAttempt?.finalAnswer === "SKIPPED" ? "" : (assignmentQuestion?.latestAttempt?.finalAnswer || null));

    const effectiveSubmittedAnswerDisplayLatex =
      assignmentQuestion?.submittedAnswerDisplayLatex ??
      assignmentQuestion?.latestAttempt?.answerDisplayLatex ??
      effectiveSubmittedAnswer;

    const effectiveSubmittedReasoning =
      assignmentQuestion?.submittedReasoning !== undefined && assignmentQuestion?.submittedReasoning !== null
        ? assignmentQuestion.submittedReasoning
        : (assignmentQuestion?.latestAttempt?.reasoningText || null);

    const hasSubmittedData =
      (effectiveSubmittedAnswer !== null && effectiveSubmittedAnswer !== undefined) ||
      (effectiveSubmittedReasoning !== null && effectiveSubmittedReasoning !== undefined);

    if (isQuestionSubmitted && hasSubmittedData) {
      setFinalAnswer(effectiveSubmittedAnswer || "");
      setAnswerDisplayLatex(question?.questionType === "MultipleChoice" ? "" : (effectiveSubmittedAnswerDisplayLatex || ""));
      setReasoningText(effectiveSubmittedReasoning || "");
      setConfidence(assignmentQuestion?.latestAttempt?.confidence ?? saved?.confidence ?? 80);
      setTimeSpentSeconds(assignmentQuestion?.latestAttempt?.timeSpentSeconds ?? saved?.timeSpentSeconds ?? 0);
      const initialChanges = assignmentQuestion?.latestAttempt?.answerChanges ?? saved?.answerChanges ?? 0;
      answerChangesRef.current = initialChanges;
      setAnswerChanges(initialChanges);
      // Submitted images are downloaded separately and never become draft uploads.
      setAttachedSnapshotDataUrl(null);
      setAttachedSnapshotTime(null);
      setDrawingUploadToken(null);
    } else if (saved) {
      setFinalAnswer(saved.finalAnswer || "");
      setAnswerDisplayLatex(question?.questionType === "MultipleChoice" ? "" : (saved.answerDisplayLatex || saved.finalAnswer || ""));
      setReasoningText(saved.reasoningText || "");
      setConfidence(saved.confidence ?? 80);
      setTimeSpentSeconds(saved.timeSpentSeconds ?? 0);
      const initialChanges = saved.answerChanges ?? 0;
      answerChangesRef.current = initialChanges;
      setAnswerChanges(initialChanges);
      setAttachedSnapshotDataUrl(saved.snapshotDataUrl || null);
      setAttachedSnapshotTime(saved.snapshotTime || null);
      setDrawingUploadToken(saved.drawingUploadToken || null);
    } else if (hasSubmittedData) {
      setFinalAnswer(effectiveSubmittedAnswer || "");
      setAnswerDisplayLatex(question?.questionType === "MultipleChoice" ? "" : (effectiveSubmittedAnswerDisplayLatex || ""));
      setReasoningText(effectiveSubmittedReasoning || "");
      setConfidence(assignmentQuestion?.latestAttempt?.confidence ?? 80);
      setTimeSpentSeconds(assignmentQuestion?.latestAttempt?.timeSpentSeconds ?? 0);
      const initialChanges = assignmentQuestion?.latestAttempt?.answerChanges ?? 0;
      answerChangesRef.current = initialChanges;
      setAnswerChanges(initialChanges);
      setAttachedSnapshotDataUrl(null);
      setAttachedSnapshotTime(null);
      setDrawingUploadToken(null);
    } else {
      setFinalAnswer("");
      setAnswerDisplayLatex("");
      setReasoningText("");
      setConfidence(80);
      setTimeSpentSeconds(0);
      answerChangesRef.current = 0;
      setAnswerChanges(0);
      setAttachedSnapshotDataUrl(null);
      setAttachedSnapshotTime(null);
      setDrawingUploadToken(null);
    }
    setAttachedSnapshotBlob(null);
    setSubmissionSaveError(null);
  }, [
    question?.questionId,
    assignmentId,
    assignmentQuestion?.attemptStatus,
    assignmentQuestion?.submittedAnswer,
    assignmentQuestion?.submittedAnswerDisplayLatex,
    assignmentQuestion?.submittedReasoning,
    assignmentQuestion?.latestAttempt,
    isAssignmentSubmitted,
    assignmentDraftKey,
    assignmentDraftLoadVersion,
  ]);

  // Persist current question answer into assignmentAnswers & localStorage
  const persistCurrentAnswer = useCallback(
    (
      newFinalAnswer?: string,
      newReasoning?: string,
      newConf?: number,
      newAnswerDisplayLatex?: string,
      newAnswerChanges?: number
    ) => {
      if (!assignmentDraftScope || !question?.questionId) return;
      if (isQuestionSubmissionLocked(assignmentQuestion)) return;
      const qId = question.questionId;
      const updatedEntry: StoredAnswer = {
        finalAnswer: newFinalAnswer !== undefined ? newFinalAnswer : finalAnswer,
        answerDisplayLatex:
          newAnswerDisplayLatex !== undefined ? newAnswerDisplayLatex : answerDisplayLatex,
        reasoningText: newReasoning !== undefined ? newReasoning : reasoningText,
        confidence: newConf !== undefined ? newConf : confidence,
        timeSpentSeconds,
        answerChanges: newAnswerChanges !== undefined ? newAnswerChanges : answerChanges,
        snapshotDataUrl: attachedSnapshotDataUrl,
        snapshotTime: attachedSnapshotTime,
        drawingUploadToken,
      };

      setAssignmentAnswers((prev) => {
        const next = { ...prev, [qId]: updatedEntry };
        writeAssignmentDraft(assignmentDraftScope, JSON.stringify(next));
        return next;
      });
    },
    [
      assignmentDraftScope,
      question?.questionId,
      assignmentQuestion,
      finalAnswer,
      answerDisplayLatex,
      reasoningText,
      confidence,
      timeSpentSeconds,
      answerChanges,
      attachedSnapshotDataUrl,
      attachedSnapshotTime,
      drawingUploadToken,
      setAssignmentAnswers,
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


  // Do not keep a submitted assignment behind a full-screen spinner indefinitely.
  // Polling continues and the submitted question workspace is read-only.
  useEffect(() => {
    if (!assignmentId || !pollingJobId || networkErrorPaused) return;
    const timeout = setTimeout(() => {
      setBackgroundAnalysisJobId(pollingJobId);
      setIsSubmitting(false);
      setAiBanner({
        type: "info",
        message: "✓ Bài làm đã được lưu. AI đang tiếp tục phân tích ở chế độ nền; bạn có thể xem lại bài đã nộp. Kết quả sẽ cập nhật khi hoàn tất.",
        action: null,
      });
    }, ANALYSIS_FOREGROUND_WAIT_MS);
    return () => clearTimeout(timeout);
  }, [assignmentId, pollingJobId, networkErrorPaused]);

  // Polling Job Status Mechanism
  useEffect(() => {
    if (!pollingJobId || feedbackData || networkErrorPaused) return;

    let isSubscribed = true;
    let pollInterval = 1000;
    let pollTimeout: ReturnType<typeof setTimeout>;

    const poll = async () => {
      if (!isSubscribed) return;
      try {
        const result = await getAnalysisJobStatus(pollingJobId);
        if (!isSubscribed) return;

        const currentStatus = result.status;
        pollingAttemptRef.current += 1;
        consecutiveNetworkErrorsRef.current = 0;

        if (isSuccessfulTerminalStatus(currentStatus)) {
          setPollingStatus("Hoàn tất đánh giá. Đang tải kết quả bài làm...");
          try {
            const feedbackRes = await getAttemptFeedback(result.attemptId);
            if (isSubscribed) {
              storeAttemptFeedback(feedbackRes);
              setIsSubmitting(false);
              setPollingJobId(null);
              setAiBanner(null);
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
              setIsSubmitting(false);
              setPollingJobId(null);
            }
          }
          if (assignmentId) {
            await refreshSubmittedAssignmentData();
          }
          return;
        }

        if (isTerminalStatus(currentStatus)) {
          // Terminal failure of AI (e.g. FailedTerminal). Submission was already saved safely.
          setIsSubmitting(false);
          setPollingJobId(null);
          if (isSubscribed) {
            setAiBanner({
              type: "warning",
              message: "✓ Bài làm đã được lưu thành công. AI chưa thể hoàn tất phân tích lúc này.",
              action: "retry_ai",
              attemptIdForRetry: String(result.attemptId),
            });
            try {
              const feedbackRes = await getAttemptFeedback(result.attemptId);
              if (isSubscribed) {
                storeAttemptFeedback(feedbackRes);
              }
            } catch {
              // fallback
            }
            if (assignmentId) {
              await refreshSubmittedAssignmentData();
            }
          }
          return;
        }

        if (shouldContinuePolling(currentStatus, pollingAttemptRef.current)) {
          setPollingStatus(
            currentStatus === "Processing" ? "AI đang phân tích toàn bộ câu trả lời..." : "Đang trong hàng đợi đánh giá..."
          );
          pollInterval = Math.min(pollInterval + 500, 3000);
          pollTimeout = setTimeout(poll, pollInterval);
        } else {
          // Polling threshold reached: Frontend stops waiting in foreground.
          // Submission is confirmed SAVED. AI job continues in background.
          setIsSubmitting(false);
          setPollingJobId(null);
          if (isSubscribed) {
            setAiBanner({
              type: "info",
              message: "✓ Bài làm đã được ghi nhận. AI đang mất nhiều thời gian hơn dự kiến để phân tích. Bạn vẫn có thể xem bài đã nộp. Lời giải và kết quả AI sẽ hiển thị khi quá trình xử lý hoàn tất.",
              action: null,
            });
            try {
              const feedbackRes = await getAttemptFeedback(result.attemptId);
              if (isSubscribed) {
                storeAttemptFeedback(feedbackRes);
              }
            } catch {
              // fallback
            }
            if (assignmentId) {
              await refreshSubmittedAssignmentData();
            }
          }
        }
      } catch {
        if (!isSubscribed) return;
        consecutiveNetworkErrorsRef.current += 1;
        if (consecutiveNetworkErrorsRef.current >= 5) {
          setNetworkErrorPaused(true);
          setIsSubmitting(false);
          setAiBanner({
            type: "info",
            message: "Kết nối tới máy chủ tạm thời gián đoạn khi cập nhật kết quả AI. Bài nộp của bạn đã được ghi nhận an toàn trên hệ thống. Bạn có thể bấm nút kiểm tra lại.",
            action: "resume_poll",
          });
          return;
        }
        pollInterval = Math.min(pollInterval + 1000, 5000);
        pollTimeout = setTimeout(poll, pollInterval);
      }
    };

    pollTimeout = setTimeout(poll, 1000);
    return () => {
      isSubscribed = false;
      clearTimeout(pollTimeout);
    };
  }, [pollingJobId, feedbackData, networkErrorPaused, searchParams, setSearchParams, attemptSessionScope, assignmentId, refreshSubmittedAssignmentData, storeAttemptFeedback]);

  // Answer change handlers
  const handleAnswerChange = (plainText: string, latex: string) => {
    if (isReadOnly) return;
    const nextAnswerChanges = answerChangesRef.current + 1;
    answerChangesRef.current = nextAnswerChanges;
    setFinalAnswer(plainText);
    setAnswerDisplayLatex(latex);
    setAnswerChanges(nextAnswerChanges);
    persistCurrentAnswer(plainText, undefined, undefined, latex, nextAnswerChanges);
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

    if (activeInputTarget === "answer") {
      if (answerEditorRef.current?.insertAtCursor) {
        answerEditorRef.current.insertAtCursor(textToInsert);
      } else if (answerEditorRef.current?.insertLatex) {
        answerEditorRef.current.insertLatex(textToInsert);
      } else if (visualMathFieldRef.current) {
        visualMathFieldRef.current.insertAtCursor(textToInsert);
      } else {
        handleAnswerChange(finalAnswer + textToInsert, answerDisplayLatex + textToInsert);
      }
      return;
    }

    if (activeInputTarget === "reasoning") {
      if (reasoningEditorRef.current) {
        if (/[\\[{^_\\]]/.test(textToInsert)) {
          const formula = textToInsert.replace(/^\$+|\$+$/g, "");
          reasoningEditorRef.current.insertLatex(formula);
        } else {
          reasoningEditorRef.current.insertText(textToInsert);
        }
      } else {
        const textarea = reasoningTextareaRef.current;
        if (!textarea) {
          handleReasoningChange(reasoningText + textToInsert);
          return;
        }

        const start = textarea.selectionStart ?? textarea.value.length;
        const end = textarea.selectionEnd ?? textarea.value.length;
        const currentVal = textarea.value;
        const updatedVal = currentVal.substring(0, start) + textToInsert + currentVal.substring(end);
        handleReasoningChange(updatedVal);

        setTimeout(() => {
          textarea.focus();
          textarea.setSelectionRange(start + textToInsert.length, start + textToInsert.length);
        }, 0);
      }
      return;
    }
  };

  // Callback from Scratchpad: Save Snapshot independently of future draws
  const handleAttachSnapshot = (blob: Blob, dataUrl: string) => {
    if (isReadOnly) return;
    setAttachedSnapshotBlob(blob);
    setAttachedSnapshotDataUrl(dataUrl);
    const nowTime = new Date().toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
    setAttachedSnapshotTime(nowTime);

    // Immediately clear previous token since image has changed
    setDrawingUploadToken(null);

    if (assignmentDraftScope && question?.questionId) {
      const qId = question.questionId;

      setAssignmentAnswers((prev) => {
        const existing = prev[qId] || {
          finalAnswer,
          answerDisplayLatex,
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
            drawingUploadToken: null,
          },
        };
        writeAssignmentDraft(assignmentDraftScope, JSON.stringify(next));
        return next;
      });

      // Unified upload mechanism
      uploadQuestionSnapshot(qId, dataUrl, blob);
    }
  };

  const handleRemoveSnapshot = () => {
    if (isReadOnly) return;
    setAttachedSnapshotBlob(null);
    setAttachedSnapshotDataUrl(null);
    setAttachedSnapshotTime(null);
    setDrawingUploadToken(null);
    if (assignmentDraftScope && question?.questionId) {
      const qId = question.questionId;
      // Invalidate any in-flight upload for this question so its callback will be discarded
      delete snapshotUploadsRef.current[qId];

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
        writeAssignmentDraft(assignmentDraftScope, JSON.stringify(next));
        return next;
      });
    }
  };

  const uploadScratchpadAttachmentIfAny = async (): Promise<string | null> => {
    if (!attachedSnapshotDataUrl || !question?.questionId) return drawingUploadToken;
    return await uploadQuestionSnapshot(question.questionId, attachedSnapshotDataUrl, attachedSnapshotBlob);
  };

  // Submit flow:
  // - Assignment Mode: Sequentially submit each question attempt (generating 1 prompt per question + scratchpad)
  // - Adaptive Mode: Submit single active question
  const handleFinalSubmit = async (autoSubmitArg?: boolean | React.MouseEvent) => {
    const isAutoSubmit = typeof autoSubmitArg === "boolean" ? autoSubmitArg : false;
    if (!question || submissionInFlightRef.current || !canSubmitLearningWork({
      submitted: isAssignmentSubmitted || Boolean(feedbackData),
      submitting: isSubmitting,
      pendingAnalysis: Boolean(pollingJobId),
      expired: isAssignmentExpired,
      autoSubmit: isAutoSubmit,
    })) return;
    const pendingQuestions = assignmentQuestions.filter((q) => !isQuestionSubmissionLocked(q));
    if (assignmentId && pendingQuestions.length === 0) return;
    submissionInFlightRef.current = true;
    persistCurrentAnswer();
    setShowBatchConfirmModal(false);
    setActiveSideTool(null);

    setSubmissionSaveError(null);
    setCanRetrySubmission(false);
    setIsSubmitting(true);
    setAiBanner(null);

    try {
      // ── BATCH ASSIGNMENT SUBMISSION ─────────────────────────────────────────
      if (assignmentId && assignmentQuestions.length > 0) {
        const currentQId = question.questionId;
        const currentSaved: StoredAnswer = {
          finalAnswer,
          answerDisplayLatex,
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

        let currentToken = drawingUploadToken;
        if (!isQuestionSubmissionLocked(assignmentQuestion) && attachedSnapshotDataUrl && !currentToken) {
          setPollingStatus("Đang tải lên bản vẽ nháp đính kèm...");
          try {
            currentToken = await uploadScratchpadAttachmentIfAny();
            if (!currentToken) {
              throw new Error("Không nhận được token sau khi tải lên");
            }
          } catch {
            setIsSubmitting(false);
            setSubmissionSaveError("Không thể tải lên ảnh vẽ nháp đính kèm. Vui lòng kiểm tra kết nối mạng và thử lại.");
            frozenPayloadRef.current = null;
            return;
          }
        }
        currentSaved.drawingUploadToken = currentToken;

        // Ensure all questions with attached drawings have uploaded tokens before submission
        for (const q of pendingQuestions) {
          const ans = allAnswersMap[q.questionId];
          if (ans && !ans.drawingUploadToken && ans.snapshotDataUrl) {
            setPollingStatus(`Đang tải lên bản vẽ nháp câu ${assignmentQuestions.indexOf(q) + 1}...`);
            const token = await uploadQuestionSnapshot(q.questionId, ans.snapshotDataUrl);
            if (!token) {
              setIsSubmitting(false);
              setSubmissionSaveError(`Không thể tải lên bản vẽ nháp cho câu ${assignmentQuestions.indexOf(q) + 1}. Vui lòng thử lại.`);
              frozenPayloadRef.current = null;
              return;
            }
            ans.drawingUploadToken = token;
          }
        }

        // Freeze payload snapshot on first attempt so timeSpentSeconds and answers do not drift on retries
        if (!frozenPayloadRef.current) {
          const frozen: Record<string, {
            finalAnswer: string;
            answerDisplayLatex?: string;
            reasoningText?: string;
            confidence: number;
            timeSpentSeconds: number;
            answerChanges: number;
            drawingUploadToken?: string;
          }> = {};

          for (const q of assignmentQuestions) {
            const src = allAnswersMap[q.questionId];
            const answerTrimmed = src?.finalAnswer?.trim() || "";
            const reasoningTrimmed = src?.reasoningText?.trim() || undefined;
            frozen[q.questionId] = {
              finalAnswer: answerTrimmed,
              answerDisplayLatex: src?.answerDisplayLatex?.trim() || undefined,
              reasoningText: reasoningTrimmed,
              confidence: src?.confidence ?? 80,
              timeSpentSeconds: src?.timeSpentSeconds ?? 0,
              answerChanges: src?.answerChanges ?? 0,
              drawingUploadToken: src?.drawingUploadToken || (q.questionId === currentQId ? currentToken || undefined : undefined),
            };
          }
          frozenPayloadRef.current = frozen;
        }

        // Pre-validate all questions requiring reasoning before starting batch submit (only when manual submit)
        if (!isAutoSubmit) {
          for (let i = 0; i < assignmentQuestions.length; i++) {
            const q = assignmentQuestions[i];
            if (q.isVoided || q.latestAttempt || q.attemptStatus === "Completed" || q.attemptStatus === "NeedsTeacherReview") {
              continue;
            }
            const qAnswer = frozenPayloadRef.current[q.questionId];
            const hasAnswer = Boolean(qAnswer?.finalAnswer?.trim());
            const hasReasoning = Boolean(qAnswer?.reasoningText?.trim());
            if (q.reasoningRequired && hasAnswer && !hasReasoning) {
              setActiveQuestionId(q.questionId);
              setIsSubmitting(false);
              setSubmissionSaveError(`Câu ${i + 1} yêu cầu phải có phần lập luận / giải trình trước khi nộp bài. Vui lòng hoàn thành câu này.`);
              frozenPayloadRef.current = null;
              return;
            }
          }
        }

        setPollingStatus(isAutoSubmit ? "Hết giờ: Đang tự động nộp bài làm..." : "Đang nộp toàn bộ bài làm lên hệ thống...");

        const answersToSubmit = pendingQuestions
          .map((q) => {
            const qAnswer = frozenPayloadRef.current?.[q.questionId] || allAnswersMap[q.questionId];
            const rawAnswer = qAnswer?.finalAnswer?.trim();
            const isSkipped = !rawAnswer;
            const qFinalAnswer = rawAnswer || "SKIPPED";
            return {
              questionId: Number(q.questionId),
              finalAnswer: isSkipped ? "SKIPPED" : qFinalAnswer,
              skipped: isSkipped,
              answerDisplayLatex: qAnswer?.answerDisplayLatex?.trim() || undefined,
              reasoningText: rawAnswer ? (qAnswer?.reasoningText?.trim() || undefined) : undefined,
              confidence: qAnswer?.confidence ?? 80,
              timeSpentSeconds: qAnswer?.timeSpentSeconds ?? 0,
              answerChanges: qAnswer?.answerChanges ?? 0,
              drawingUploadToken: qAnswer?.drawingUploadToken || undefined,
            };
          });

        const submitRes = await submitStudentAssignment(assignmentId, {
          answers: answersToSubmit,
        });

        const lastJobId = submitRes.lastAnalysisJobId || null;
        const submittedCount = submitRes.submittedAttemptsCount ?? answersToSubmit.length;

        // Clean up frozen payload on success
        frozenPayloadRef.current = null;

        // Hydrate the detail query before discarding the local fallback. Without
        // this refetch, review mode sees the pre-submit cache until a full reload.
        // Stop active work immediately on server acceptance, even if refetch is slow/offline.
        assignmentReviewRef.current = true;
        setIsLocallySubmitted(true);
        setIsSubmitting(false);
        await refreshSubmittedAssignmentData();
        if (assignmentDraftScope) {
          removeAssignmentDraft(assignmentDraftScope);
          removeAssignmentRemainingSeconds(assignmentDraftScope);
        }

        if (isAutoSubmit) {
          setAiBanner({
            type: "info",
            message: "⏱ Hết thời gian làm bài! Hệ thống đã tự động nộp bài làm của bạn. Các câu chưa làm đã được tự động bỏ qua.",
            action: null,
          });
        } else if (lastJobId) {
          setPollingJobId(lastJobId);
          searchParams.set("analysisJobId", lastJobId);
          setSearchParams(searchParams, { replace: true });
          setAiBanner({
            type: "info",
            message: `✓ Đã nộp thành công ${submittedCount > 0 ? `${submittedCount} câu hỏi` : "bài làm"}. Hệ thống đang đối soát đáp án và phân tích lập luận...`,
            action: null,
          });
        }
        return;
      }

      // ── SINGLE ADAPTIVE QUESTION SUBMISSION ──────────────────────────────────
      let tokenToUse: string | null = drawingUploadToken;
      if (attachedSnapshotDataUrl && !tokenToUse) {
        setPollingStatus("Đang tải lên bản vẽ nháp đính kèm...");
        tokenToUse = await uploadScratchpadAttachmentIfAny();
      }

      const reasoningToSubmit = reasoningText.trim() || undefined;
      if (question.reasoningRequired && finalAnswer.trim() && !reasoningToSubmit) {
        if (!isAutoSubmit) {
          setIsSubmitting(false);
          setSubmissionSaveError("Câu hỏi này yêu cầu phải có phần lập luận / giải trình trước khi nộp bài.");
          return;
        }
      }

      setPollingStatus("Đang gửi bài làm lên hệ thống AI...");
      const submitted = await submitAttempt({
        questionId: question.questionId,
        assignmentId: undefined,
        finalAnswer: finalAnswer.trim() || "SKIPPED",
        reasoningText: reasoningToSubmit,
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
        setAiBanner({
          type: "info",
          message: "✓ Bài làm đã được ghi nhận. Hệ thống đang đối soát đáp án và phân tích lập luận...",
          action: null,
        });
      } else if (resData.attemptId) {
        setPollingStatus("Đang tải kết quả bài làm...");
        const fbRes = await getAttemptFeedback(resData.attemptId);
        storeAttemptFeedback(fbRes);
        setIsSubmitting(false);
      } else {
        setIsSubmitting(false);
        setSubmissionSaveError("Phản hồi bất thường từ máy chủ. Vui lòng thử nộp lại.");
        setCanRetrySubmission(true);
      }
    } catch (err: unknown) {
      setIsSubmitting(false);
      const errObj = err as { response?: { data?: { detail?: string } }; message?: string };
      setSubmissionSaveError(
        errObj?.response?.data?.detail || errObj?.message || "Nộp bài thất bại. Vui lòng kiểm tra kết nối mạng và thử nộp lại."
      );
      setCanRetrySubmission(true);
    } finally {
      submissionInFlightRef.current = false;
    }
  };

  const handleRetrySubmission = async () => {
    if (attemptSessionScope) {
      const freshSubId = createClientSubmissionId();
      setAttemptSessionId(attemptSessionScope, freshSubId);
      clientSubmissionIdRef.current = freshSubId;
    }
    setSubmissionSaveError(null);
    setCanRetrySubmission(false);
    handleFinalSubmit(isAssignmentExpired);
  };

  // Auto-submit when test time limit or due deadline expires
  useEffect(() => {
    if (
      assignmentId &&
      isAssignmentExpired &&
      !isAssignmentSubmitted &&
      !isSubmitting &&
      !hasAutoSubmittedRef.current &&
      question &&
      assignmentQuestions.length > 0
    ) {
      hasAutoSubmittedRef.current = true;
      handleFinalSubmit(true);
    }
  }, [
    assignmentId,
    isAssignmentExpired,
    isAssignmentSubmitted,
    isSubmitting,
    question,
    assignmentQuestions.length,
  ]);

  const handleRetryAiFromBanner = async (attemptId: string) => {
    if (isRetryingAiFromBanner) return;
    try {
      setIsRetryingAiFromBanner(true);
      const current = await getAttemptFeedback(attemptId);
      if (!current.retryQuota?.canRetry) {
        storeAttemptFeedback(current);
        setAiBanner(null);
        return;
      }
      const res = await retryAttemptAIAnalysis(attemptId);
      setIsRetryingAiFromBanner(false);
      setAiBanner(null);
      if (res.jobId) {
        setFeedbackData(null);
        pollingAttemptRef.current = 0;
        setPollingJobId(res.jobId);
        setIsSubmitting(true);
      } else {
        if (assignmentId) {
          await refreshSubmittedAssignmentData();
        }
      }
    } catch (err: any) {
      setIsRetryingAiFromBanner(false);
      const msg = err?.response?.data?.detail || err?.response?.data?.message || err?.message || "Không thể kích hoạt chấm lại AI lúc này.";
      setAiBanner({
        type: "warning",
        message: `✓ Bài làm đã được ghi nhận an toàn. ${msg}`,
        action: "retry_ai",
        attemptIdForRetry: attemptId,
      });
    } finally {
      setIsRetryingAiFromBanner(false);
    }
  };

  const handleReviewAssignmentQuestions = async () => {
    if (!assignmentId || isRefreshingAssignmentReview) return;

    setIsRefreshingAssignmentReview(true);
    try {
      // Wait for the detail query, not only the assignment list, before leaving
      // the feedback screen. The scoped local draft remains as a safe fallback
      // when this request is temporarily unavailable.
      await refreshSubmittedAssignmentData();
      const firstQuestionId = assignmentQuestions[0]?.questionId;
      setFeedbackData(null);
      if (firstQuestionId) {
        setActiveQuestionId(firstQuestionId);
        window.history.replaceState(
          null,
          "",
          `/hoc-tap/luyen-tap/${firstQuestionId}?assignmentId=${assignmentId}${subjectId ? `&subjectId=${subjectId}` : ""}`
        );
      }
    } finally {
      setIsRefreshingAssignmentReview(false);
    }
  };

  // Check if a question has all required fields (answer and required reasoning)
  const checkQuestionCompletion = useCallback(
    (q: (typeof assignmentQuestions)[0]) => {
      if (q.isVoided) return true;
      if (q.latestAttempt) {
        if (q.latestAttempt.skipped || !q.latestAttempt.finalAnswer?.trim()) return false;
        if (q.reasoningRequired && !q.latestAttempt.reasoningText?.trim()) return false;
        return true;
      }
      if (q.submittedAnswer && q.submittedAnswer !== "SKIPPED") {
        if (q.reasoningRequired && !q.submittedReasoning?.trim()) return false;
        return true;
      }
      const isCurrent = q.questionId === question?.questionId;
      const ans = isCurrent ? finalAnswer : assignmentAnswers[q.questionId]?.finalAnswer;
      const res = isCurrent ? reasoningText : assignmentAnswers[q.questionId]?.reasoningText;
      if (!ans?.trim()) return false;
      if (q.reasoningRequired && !res?.trim()) return false;
      return true;
    },
    [question?.questionId, finalAnswer, reasoningText, assignmentAnswers]
  );

  // Calculate stats for Question Palette & Assignment submission state (must be declared before any early return)
  const totalQuestions = assignmentQuestions?.length || 0;

  const voidedQuestionsCount = useMemo(() => {
    return assignmentQuestions.filter((q) => q.isVoided).length;
  }, [assignmentQuestions]);

  const answeredCount = useMemo(() => {
    if (!assignmentQuestions.length) return 0;
    return assignmentQuestions.filter((q) => {
      if (q.latestAttempt) {
        return !q.latestAttempt.skipped && Boolean(q.latestAttempt.finalAnswer?.trim());
      }
      if (q.submittedAnswer && q.submittedAnswer !== "SKIPPED") {
        return Boolean(q.submittedAnswer.trim());
      }
      const isCurrent = q.questionId === question?.questionId;
      const ans = isCurrent ? finalAnswer : assignmentAnswers[q.questionId]?.finalAnswer;
      return Boolean(ans?.trim());
    }).length;
  }, [assignmentQuestions, question?.questionId, finalAnswer, assignmentAnswers]);

  const unansweredQuestionIndices = useMemo(() => {
    if (!assignmentQuestions.length) return [];
    const missing: number[] = [];
    assignmentQuestions.forEach((q, idx) => {
      if (q.isVoided) return;
      if (!checkQuestionCompletion(q)) {
        missing.push(idx + 1);
      }
    });
    return missing;
  }, [assignmentQuestions, checkQuestionCompletion]);

  // Assignment-level statistics when submitted
  const assignmentStats = useMemo(() => {
    if (!assignment || !isAssignmentSubmitted) return null;

    let evaluatedCount = 0;
    let aiProcessingCount = 0;
    let teacherReviewCount = 0;

    for (const q of assignmentQuestions) {
      if (q.isVoided) {
        evaluatedCount++;
        continue;
      }

      const status = q.latestAttempt?.status || q.attemptStatus;
      const isEvaluated = status === "Completed";
      const isReview = status === "NeedsTeacherReview";
      const isProcessing = status === "PendingAnalysis" || status === "Processing";

      if (isEvaluated) evaluatedCount++;
      if (isReview) teacherReviewCount++;
      if (isProcessing) aiProcessingCount++;
    }

    // Prefer authoritative backend summary for overall assignment score to maintain consistent scale
    if (assignment.summary) {
      const summary = assignment.summary;
      const awarded = summary.internalAwardedScore ?? 0;
      const maxScore = summary.internalMaxScore || 10;
      return {
        awardedTotal: Math.round(awarded * 100) / 100,
        maxTotalForDetermined: Math.round(maxScore * 100) / 100,
        overallMaxTotal: Math.round(maxScore * 100) / 100,
        correctCount: summary.correctQuestionCount,
        voidedCount: summary.voidedQuestionCount ?? 0,
        evaluatedCount,
        aiProcessingCount,
        teacherReviewCount,
        pendingCount: teacherReviewCount,
        totalCount: summary.totalQuestionCount || assignmentQuestions.length,
        isFullyEvaluated: evaluatedCount === assignmentQuestions.length && assignmentQuestions.length > 0,
      };
    }

    // Fallback when summary not yet populated: maintain consistent score scales
    const fallbackGrade = fallbackAssignmentGrade(assignmentQuestions.map(q => ({
      isVoided: q.isVoided, score: q.latestAttempt?.awardedScore, maxScore: q.latestAttempt?.maxScore ?? 10,
    })));
    let correctCount = 0;
    let voidedCount = 0;

    for (const q of assignmentQuestions) {
      if (q.isVoided) {
        voidedCount++;
        continue;
      }

      if ((q.effectiveIsCorrect ?? q.latestAttempt?.isCorrect) === true) {
        correctCount++;
      }
    }

    return {
      awardedTotal: fallbackGrade.awardedScore,
      maxTotalForDetermined: fallbackGrade.maxScore,
      overallMaxTotal: fallbackGrade.maxScore,
      correctCount,
      voidedCount,
      evaluatedCount,
      aiProcessingCount,
      teacherReviewCount,
      pendingCount: teacherReviewCount,
      totalCount: assignmentQuestions.length,
      isFullyEvaluated: evaluatedCount === assignmentQuestions.length && assignmentQuestions.length > 0,
    };
  }, [assignment, isAssignmentSubmitted, assignmentQuestions]);

  const isReadOnly =
    isAssignmentSubmitted ||
    isCurrentQuestionSubmitted ||
    isSubmitting ||
    Boolean(pollingJobId) ||
    isAssignmentExpired ||
    Boolean(assignmentQuestion?.isVoided);

  // Guard: if adaptive mode and no subject selected
  if (!assignmentId && !subjectId) {
    return <StudentSubjectRequiredState onSelect={(id) => setSearchParams({ subjectId: id })} />;
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
  if (shouldShowAnalysisWaitingScreen(pollingJobId, backgroundAnalysisJobId, networkErrorPaused)) {
    return (
      <div className="min-h-screen bg-[#f8fafc] dark:bg-[#090d16] p-6 flex items-center justify-center text-slate-800 dark:text-slate-100">
        <div className="mx-auto max-w-xl rounded-3xl bg-white dark:bg-[#0f172a] p-10 sm:p-12 text-center shadow-xs border border-slate-200/80 dark:border-slate-800 space-y-4">
          <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-2xl bg-indigo-50 dark:bg-indigo-950/60">
            <div className="h-8 w-8 animate-spin rounded-full border-4 border-indigo-600 border-t-transparent" />
          </div>
          <h2 className="text-xl font-bold text-slate-900 dark:text-white">Đang Đối Soát Đáp Án & Phân Tích Lập Luận</h2>
          <p className="text-sm text-slate-500 dark:text-slate-400">{pollingStatus}</p>
          <p className="text-sm font-semibold text-emerald-700 dark:text-emerald-400">✓ Bài làm đã được lưu an toàn. Bạn không cần nộp lại.</p>
          <div className="flex justify-center gap-2 pt-2">
            <span className="inline-block h-2.5 w-2.5 animate-bounce rounded-full bg-indigo-400" />
            <span className="inline-block h-2.5 w-2.5 animate-bounce rounded-full bg-indigo-500 [animation-delay:0.2s]" />
            <span className="inline-block h-2.5 w-2.5 animate-bounce rounded-full bg-indigo-600 [animation-delay:0.4s]" />
          </div>
          <p className="text-xs text-slate-400 dark:text-slate-500 pt-4 border-t border-slate-100 dark:border-slate-800">
            Hệ thống đang kiểm chứng các bước suy luận, phát hiện lỗ hổng kiến thức và cập nhật Hồ sơ Năng lực (Twin).
          </p>
          <Link to={assignmentId ? "/hoc-tap/bai-tap" : "/hoc-tap/tong-quan"}
            className="inline-flex rounded-xl border border-slate-300 dark:border-slate-600 px-4 py-2 text-sm font-semibold hover:bg-slate-50 dark:hover:bg-slate-800">
            {assignmentId ? "Về danh sách bài tập · AI tiếp tục xử lý" : "Về tổng quan · AI tiếp tục xử lý"}
          </Link>
        </div>
      </div>
    );
  }

  // Assignments stay in the same read-only question workspace after submission
  // and when reopened. Only independent adaptive practice uses this result screen.
  if (feedbackData && !assignmentId) {
    const { grading, twinChange, recommendation } = feedbackData;
    const displayedGrade = normalizeQuestionScore(grading.awardedScore, grading.maxScore,
      assignmentId ? assignmentQuestions.length : undefined);
    const feedbackPresentation = getAttemptFeedbackPresentation(grading, feedbackData.analysis, feedbackData.status);

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
                  {displayedGrade.awardedScore === null
                    ? `Chưa chấm / ${displayedGrade.maxScore}`
                    : `${displayedGrade.awardedScore} / ${displayedGrade.maxScore}`}
                </h2>
                <p className="mt-1 text-xs text-white/80">Nguồn điểm: {feedbackPresentation.scoreSourceLabel}</p>
              </div>
              <div className="text-right">
                <span className="text-xs text-white/80">Lượt làm bài: #{feedbackData.attemptId.slice(0, 8)}</span>
                <p className="text-xs text-white/80 font-semibold mt-0.5">Trạng thái: {feedbackData.status}</p>
              </div>
            </div>
          </div>

          {/* 4-Tier Hierarchy: Student Work -> AI Reasoning -> Teacher Solution -> Teacher Evaluation */}
          <AttemptFeedbackHierarchy
            feedbackData={feedbackData}
            assignmentQuestionCount={assignmentId ? assignmentQuestions.length : undefined}
            questionType={
              assignmentQuestions.find((item) => item.questionId === feedbackData.questionId)?.questionType ??
              question?.questionType
            }
            answerOptions={
              assignmentQuestions.find((item) => item.questionId === feedbackData.questionId)?.options ??
              question?.options ??
              []
            }
            onRefreshFeedback={async () => {
              const res = await getAttemptFeedback(feedbackData.attemptId);
              storeAttemptFeedback(res);
            }}
            onPollJob={(jobId) => {
              setFeedbackData(null);
              pollingAttemptRef.current = 0;
              setNetworkErrorPaused(false);
              setPollingJobId(jobId);
              searchParams.set("analysisJobId", jobId);
              setSearchParams(searchParams, { replace: true });
            }}
          />

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
              <div className="flex w-full sm:w-auto flex-col sm:flex-row gap-3">
                <button
                  type="button"
                  onClick={() => void handleReviewAssignmentQuestions()}
                  disabled={isRefreshingAssignmentReview}
                  className="w-full sm:w-auto rounded-xl bg-indigo-600 px-6 py-2.5 text-xs font-bold text-white shadow-xs hover:bg-indigo-500 text-center cursor-pointer disabled:cursor-wait disabled:opacity-60"
                >
                  {isRefreshingAssignmentReview ? "Đang tải bài làm..." : "Xem lại từng câu →"}
                </button>
                <Link
                  to="/hoc-tap/bai-tap"
                  className="w-full sm:w-auto rounded-xl bg-white dark:bg-slate-800 px-5 py-2.5 text-xs font-bold text-slate-700 dark:text-slate-200 border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700 text-center cursor-pointer"
                >
                  Bài tập khác
                </Link>
              </div>
            )}
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-stone-50/50 dark:bg-[#101623] text-stone-800 dark:text-stone-100 flex flex-col antialiased student-shell">
      {/* Top Header Bar */}
      <header className="sticky top-0 z-30 bg-white/95 dark:bg-[#151d2f]/95 backdrop-blur-md border-b border-stone-200/80 dark:border-stone-800/80 shadow-xs">
        <div className="max-w-[1600px] mx-auto px-4 sm:px-6 flex items-center justify-between h-14 sm:h-16">
          <div className="flex items-center gap-3">
            <Link
              to={
                assignmentId
                  ? `/hoc-tap/bai-tap/${assignmentId}${subjectId ? `?subjectId=${subjectId}` : ""}`
                  : `/hoc-tap/tong-quan?subjectId=${subjectId}`
              }
              className="text-xs sm:text-sm font-semibold text-stone-600 dark:text-stone-400 hover:text-stone-900 dark:hover:text-stone-100 flex items-center gap-1.5 transition-colors cursor-pointer"
            >
              <span>←</span>
              <span>{assignmentId ? "Về bài tập" : "Thoát"}</span>
            </Link>

            <div className="h-4 w-px bg-stone-200 dark:bg-stone-800 hidden sm:block" />

            <span className="hidden sm:inline-flex items-center px-3 py-1 rounded-lg text-xs font-semibold bg-stone-100 dark:bg-stone-800 text-stone-800 dark:text-stone-200 border border-stone-200/80 dark:border-stone-700/80">
              {assignment ? assignment.title : `Luyện tập thích ứng`}
            </span>
          </div>

          <div className="flex items-center gap-2.5">
            {/* Timer / Countdown */}
            {isAssignmentSubmitted ? (
              <AssignmentReviewReceipt {...submissionTiming} />
            ) : (
            <div
              className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-mono font-bold border ${
                isAssignmentExpired
                  ? "bg-rose-50 text-rose-800 border-rose-200 dark:bg-rose-950/60 dark:text-rose-200 dark:border-rose-900"
                  : hasTimeLimit && assignmentRemainingSeconds !== null && assignmentRemainingSeconds <= 300
                  ? "bg-amber-50 text-amber-800 border-amber-200 dark:bg-amber-950/60 dark:text-amber-200 dark:border-amber-900 animate-pulse"
                  : !hasTimeLimit && dueRemainingSeconds !== null && dueRemainingSeconds <= 3600
                  ? "bg-amber-50 text-amber-800 border-amber-200 dark:bg-amber-950/60 dark:text-amber-200 dark:border-amber-900 animate-pulse"
                  : "bg-stone-100 dark:bg-stone-800 text-stone-700 dark:text-stone-300 border-stone-200/60 dark:border-stone-700/60"
              }`}
              title={
                hasTimeLimit
                  ? "Thời gian làm bài còn lại"
                  : dueRemainingSeconds !== null
                  ? "Hạn chót nộp bài"
                  : "Thời gian làm bài"
              }
            >
              <span className="text-stone-400 text-xs">⏱</span>
              <span>
                {hasTimeLimit && assignmentRemainingSeconds !== null
                  ? `${String(Math.floor(assignmentRemainingSeconds / 3600)).padStart(2, "0")}:${String(Math.floor((assignmentRemainingSeconds % 3600) / 60)).padStart(2, "0")}:${String(assignmentRemainingSeconds % 60).padStart(2, "0")}`
                  : !hasTimeLimit && dueRemainingSeconds !== null
                  ? `${String(Math.floor(dueRemainingSeconds / 3600)).padStart(2, "0")}:${String(Math.floor((dueRemainingSeconds % 3600) / 60)).padStart(2, "0")}:${String(dueRemainingSeconds % 60).padStart(2, "0")}`
                  : `${String(Math.floor(timeSpentSeconds / 60)).padStart(2, "0")}:${String(timeSpentSeconds % 60).padStart(2, "0")}`}
              </span>
              {hasTimeLimit && assignmentRemainingSeconds !== null && assignmentRemainingSeconds <= 300 && assignmentRemainingSeconds > 0 && (
                <span className="text-[10px] font-bold text-rose-600 dark:text-rose-400 ml-1">SẮP HẾT GIỜ!</span>
              )}
              {isAssignmentExpired && (
                <span className="text-[10px] font-bold text-rose-600 dark:text-rose-400 ml-1">HẾT GIỜ</span>
              )}
            </div>
            )}

            {/* Draft Auto-save status badge */}
            {assignmentId && !isAssignmentSubmitted && !isLocallySubmitted && (
              draftConflict ? (
                <div
                  className="hidden sm:inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-[11px] font-medium border bg-amber-50 text-amber-800 border-amber-300 dark:bg-amber-950/60 dark:text-amber-200 dark:border-amber-800 cursor-pointer"
                  title={draftConflict.message}
                  onClick={() => {
                    const el = document.getElementById("assignment-draft-conflict-banner");
                    el?.scrollIntoView({ behavior: "smooth" });
                  }}
                >
                  <span className="text-amber-600 dark:text-amber-400 font-bold">⚠️</span>
                  <span>Xung đột phiên bản (Đã dừng tự lưu)</span>
                </div>
              ) : draftSaveStatus !== "idle" ? (
                <div
                  className={`hidden sm:inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-[11px] font-medium border transition-colors ${
                    draftSaveStatus === "saving"
                      ? "bg-sky-50 text-sky-700 border-sky-200 dark:bg-sky-950/40 dark:text-sky-300 dark:border-sky-800"
                      : draftSaveStatus === "error"
                      ? "bg-rose-50 text-rose-700 border-rose-200 dark:bg-rose-950/40 dark:text-rose-300 dark:border-rose-800"
                      : "bg-stone-50 text-stone-600 border-stone-200 dark:bg-stone-800/60 dark:text-stone-300 dark:border-stone-700"
                  }`}
                  title={
                    draftSaveStatus === "saving"
                      ? "Đang tự động lưu nháp..."
                      : draftSaveStatus === "error"
                      ? "Chưa lưu được nháp, hệ thống sẽ tự thử lại."
                      : `Bản nháp đã lưu lúc ${lastDraftSavedTime ?? ""}`
                  }
                >
                  {draftSaveStatus === "saving" && (
                    <>
                      <span className="w-1.5 h-1.5 rounded-full bg-sky-500 animate-ping" />
                      <span>Đang lưu nháp...</span>
                    </>
                  )}
                  {draftSaveStatus === "saved" && (
                    <>
                      <span className="text-emerald-500">✓</span>
                      <span>Đã lưu nháp{lastDraftSavedTime ? ` (${lastDraftSavedTime})` : ""}</span>
                    </>
                  )}
                  {draftSaveStatus === "error" && (
                    <>
                      <span className="text-rose-500">⚠</span>
                      <span>Lỗi lưu nháp</span>
                    </>
                  )}
                </div>
              ) : null
            )}

            {/* If assignment is submitted: show status badge and retake button if allowed */}
            {assignmentId && isAssignmentSubmitted && (
              <div className="flex items-center gap-2">
                <div className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg bg-emerald-50 dark:bg-emerald-950/40 text-emerald-800 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800 text-xs font-semibold">
                  <span>✓</span>
                  <span>Đã nộp bài (Chỉ đọc)</span>
                </div>
                {assignment?.canRetake && (
                  <button
                    type="button"
                    onClick={() => {
                      if (window.confirm("Bạn có chắc chắn muốn làm lại bài tập này?")) {
                        if (assignmentDraftScope) {
                          removeAssignmentDraft(assignmentDraftScope);
                          removeAssignmentRemainingSeconds(assignmentDraftScope);
                        }
                        timerInitializedForAssignmentRef.current = null;
                        if (hasTimeLimit && assignment?.timeLimitMinutes) {
                          const freshSeconds = assignment.timeLimitMinutes * 60;
                          if (assignmentDraftScope) {
                            writeAssignmentRemainingSeconds(assignmentDraftScope, freshSeconds);
                          }
                          setAssignmentRemainingSeconds(freshSeconds);
                        }
                        setAssignmentAnswers({});
                        setFinalAnswer("");
                        setReasoningText("");
                        setFeedbackData(null);
                        setSubmissionSaveError(null);
                        setAiBanner(null);
                        setIsLocallySubmitted(false);
                        hasAutoSubmittedRef.current = false;
                        if (assignmentQuestions[0]) {
                          handleSwitchQuestion(assignmentQuestions[0].questionId);
                        }
                      }
                    }}
                    className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg bg-stone-900 hover:bg-stone-800 dark:bg-stone-100 dark:hover:bg-stone-200 text-white dark:text-stone-900 font-semibold text-xs cursor-pointer"
                  >
                    <span>Làm lại bài</span>
                  </button>
                )}
              </div>
            )}

            {/* If assignment is NOT submitted yet: show prominent submit button on ALL viewports */}
            {assignmentId && !isAssignmentSubmitted && (
              <button
                type="button"
                onClick={() => setShowBatchConfirmModal(true)}
                disabled={isSubmitting || isAssignmentExpired}
                className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-lg bg-stone-900 hover:bg-stone-800 dark:bg-stone-100 dark:hover:bg-stone-200 text-white dark:text-stone-900 font-semibold text-xs transition-colors cursor-pointer disabled:opacity-50 shrink-0"
              >
                <span>{isAssignmentExpired ? (isSubmitting ? "Đang tự động nộp..." : "Đã hết giờ") : "Nộp bài"}</span>
                <span className="px-1.5 py-0.2 rounded-full bg-stone-700 dark:bg-stone-300 text-white dark:text-stone-900 text-[10px] font-mono font-bold">
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
          {/* Left / Main Question Area ~70% */}
          <div className={activeSideTool ? "lg:col-span-8 xl:col-span-8 space-y-6" : "space-y-6"}>
            {!isAssignmentSubmitted && isAssignmentExpired && (
              <div className="rounded-2xl bg-rose-50 dark:bg-rose-950/60 p-4 text-xs font-bold text-rose-800 dark:text-rose-300 border border-rose-300 dark:border-rose-800 flex items-center justify-between">
                <span>⏰ Đã hết thời gian làm bài. Bài làm không thể nộp thêm câu mới. Các câu đã nộp trước đó được giữ nguyên.</span>
              </div>
            )}
            {!isAssignmentSubmitted && startAssignmentError && (
              <div className="rounded-2xl bg-amber-50 dark:bg-amber-950/40 p-4 text-sm font-semibold text-amber-900 dark:text-amber-200 border border-amber-200 dark:border-amber-800 flex items-center justify-between gap-3">
                <div className="flex items-center gap-2">
                  <span className="text-base">⚠️</span>
                  <p>{startAssignmentError}</p>
                </div>
                <button
                  type="button"
                  onClick={() => {
                    setStartAssignmentError(null);
                    refetchAssignment();
                  }}
                  className="rounded-xl bg-amber-200 dark:bg-amber-900/60 px-3 py-1.5 text-xs font-bold text-amber-900 dark:text-amber-100 hover:bg-amber-300 dark:hover:bg-amber-900 shadow-xs cursor-pointer shrink-0"
                >
                  Tải lại
                </button>
              </div>
            )}
            {submissionSaveError && (
              <div className="rounded-2xl bg-rose-50 dark:bg-rose-950/40 p-4 text-sm font-semibold text-rose-800 dark:text-rose-300 border border-rose-200 dark:border-rose-800 space-y-2">
                <div className="flex items-center gap-2">
                  <span className="text-base">❌</span>
                  <p>{submissionSaveError}</p>
                </div>
                {canRetrySubmission && (
                  <div className="pt-1">
                    <button
                      type="button"
                      onClick={handleRetrySubmission}
                      className="rounded-xl bg-rose-600 px-4 py-2 text-xs font-bold text-white hover:bg-rose-700 shadow-xs cursor-pointer"
                    >
                      Thử nộp lại
                    </button>
                  </div>
                )}
              </div>
            )}

            {aiBanner && (
              <div
                className={`rounded-2xl p-4 text-sm font-semibold border space-y-2 ${
                  aiBanner.type === "warning"
                    ? "bg-amber-50 dark:bg-amber-950/40 text-amber-900 dark:text-amber-200 border-amber-200 dark:border-amber-800"
                    : "bg-blue-50 dark:bg-blue-950/40 text-blue-900 dark:text-blue-200 border-blue-200 dark:border-blue-800"
                }`}
              >
                <div className="flex items-start gap-2.5">
                  <span className="text-base shrink-0">{aiBanner.type === "warning" ? "⚠️" : "ℹ️"}</span>
                  <div className="space-y-1">
                    <p className="leading-relaxed">{aiBanner.message}</p>
                  </div>
                </div>
                {aiBanner.action === "retry_ai" && aiBanner.attemptIdForRetry && (
                  <div className="pt-1 pl-6">
                    <button
                      type="button"
                      disabled={isRetryingAiFromBanner}
                      onClick={() => handleRetryAiFromBanner(aiBanner.attemptIdForRetry!)}
                      className="rounded-xl bg-amber-600 px-4 py-2 text-xs font-bold text-white hover:bg-amber-700 shadow-xs cursor-pointer disabled:opacity-50 inline-flex items-center gap-1.5"
                    >
                      <span>{isRetryingAiFromBanner ? "⏳" : "🔄"}</span>
                      <span>{isRetryingAiFromBanner ? "Đang gửi chấm lại..." : "Chấm lại bằng AI"}</span>
                    </button>
                  </div>
                )}
                {aiBanner.action === "resume_poll" && (
                  <div className="pt-1 pl-6">
                    <button
                      type="button"
                      onClick={() => {
                        consecutiveNetworkErrorsRef.current = 0;
                        setNetworkErrorPaused(false);
                        setAiBanner(null);
                        setIsSubmitting(true);
                        setPollingJobId(persistedJobId || pollingJobId);
                      }}
                      className="rounded-xl bg-indigo-600 px-4 py-2 text-xs font-bold text-white hover:bg-indigo-500 shadow-xs cursor-pointer"
                    >
                      🔄 Tiếp tục kiểm tra kết quả AI
                    </button>
                  </div>
                )}
              </div>
            )}

            {/* 0. Assignment Result Overview Banner when submitted */}
            {assignmentId && isAssignmentSubmitted && assignmentStats && (
              <div className="rounded-3xl bg-white dark:bg-[#0f172a] border border-slate-200/80 dark:border-slate-800 p-6 sm:p-7 shadow-xs space-y-4">
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-100 dark:border-slate-800 pb-5">
                  <div>
                    <div className="flex items-center gap-2">
                      <span className="text-xl">🏆</span>
                      <h2 className="text-lg sm:text-xl font-black text-slate-900 dark:text-white">
                        Kết quả tổng quan bài tập
                      </h2>
                    </div>
                    <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
                      {assignment?.title} · {assignmentStats.totalCount} câu hỏi
                    </p>
                  </div>

                  <div className="flex items-center gap-3">
                    <div className="rounded-2xl bg-indigo-50 dark:bg-indigo-950/60 border border-indigo-200/80 dark:border-indigo-800 px-4 py-2.5 text-center">
                      <div className="text-[10px] uppercase font-bold text-indigo-500 dark:text-indigo-400">
                        {assignmentStats.aiProcessingCount > 0
                          ? "Điểm đã xác định"
                          : assignmentStats.teacherReviewCount > 0
                          ? "Điểm sơ bộ"
                          : "Tổng điểm"}
                      </div>
                      <div className="text-xl sm:text-2xl font-black text-indigo-700 dark:text-indigo-300">
                        {assignmentStats.evaluatedCount > 0 ? (
                          <>
                            {assignmentStats.awardedTotal}{" "}
                            <span className="text-xs font-semibold text-slate-400">
                              / {assignmentStats.maxTotalForDetermined}
                            </span>
                          </>
                        ) : (
                          <span className="text-sm font-bold text-slate-500 dark:text-slate-400">
                            {assignmentStats.aiProcessingCount > 0 ? "Chờ phân tích" : "Chờ giáo viên chấm"}
                          </span>
                        )}
                      </div>
                    </div>

                    <button
                      type="button"
                      onClick={() => {
                        void refreshSubmittedAssignmentData();
                      }}
                      className="rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 px-3 py-2 text-xs font-bold text-slate-700 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-700 cursor-pointer inline-flex items-center gap-1.5"
                      title="Làm mới kết quả bài làm"
                    >
                      <span>🔄</span>
                      <span>Làm mới</span>
                    </button>
                  </div>
                </div>

                <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                  <div className="rounded-2xl bg-slate-50 dark:bg-slate-900/60 border border-slate-100 dark:border-slate-800 p-3.5">
                    <div className="text-[11px] font-semibold text-slate-500 dark:text-slate-400">Số câu đúng</div>
                    <div className="text-base sm:text-lg font-black text-emerald-600 dark:text-emerald-400 mt-0.5">
                      {assignmentStats.correctCount} / {assignmentStats.totalCount}
                    </div>
                    {assignmentStats.voidedCount > 0 && (
                      <div className="text-[10px] text-purple-600 dark:text-purple-400 font-bold mt-0.5">
                        +{assignmentStats.voidedCount} câu được miễn điểm
                      </div>
                    )}
                  </div>

                  <div className="rounded-2xl bg-slate-50 dark:bg-slate-900/60 border border-slate-100 dark:border-slate-800 p-3.5">
                    <div className="text-[11px] font-semibold text-slate-500 dark:text-slate-400">Đã chấm xong</div>
                    <div className="text-base sm:text-lg font-black text-slate-800 dark:text-slate-200 mt-0.5">
                      {assignmentStats.evaluatedCount} / {assignmentStats.totalCount} câu
                    </div>
                  </div>

                  <div className="rounded-2xl bg-slate-50 dark:bg-slate-900/60 border border-slate-100 dark:border-slate-800 p-3.5">
                    <div className="text-[11px] font-semibold text-slate-500 dark:text-slate-400">
                      {assignmentStats.aiProcessingCount > 0 ? "AI đang phân tích" : "Chờ GV duyệt"}
                    </div>
                    <div
                      className={`text-base sm:text-lg font-black mt-0.5 ${
                        assignmentStats.aiProcessingCount > 0
                          ? "text-indigo-600 dark:text-indigo-400"
                          : "text-amber-600 dark:text-amber-400"
                      }`}
                    >
                      {assignmentStats.aiProcessingCount > 0
                        ? `${assignmentStats.aiProcessingCount} câu`
                        : `${assignmentStats.teacherReviewCount} câu`}
                    </div>
                  </div>

                  <div className="rounded-2xl bg-slate-50 dark:bg-slate-900/60 border border-slate-100 dark:border-slate-800 p-3.5">
                    <div className="text-[11px] font-semibold text-slate-500 dark:text-slate-400">Trạng thái bài tập</div>
                    <div className="text-xs sm:text-sm font-bold text-indigo-600 dark:text-indigo-400 mt-1">
                      {assignmentStats.aiProcessingCount > 0
                        ? "AI đang phân tích..."
                        : assignmentStats.teacherReviewCount > 0
                        ? "Chờ duyệt tự luận"
                        : "Đã hoàn thành"}
                    </div>
                  </div>
                </div>

                {assignmentStats.aiProcessingCount > 0 ? (
                  <div className="rounded-xl bg-blue-50 dark:bg-blue-950/40 border border-blue-200 dark:border-blue-800 p-3 text-xs text-blue-900 dark:text-blue-200 flex items-center gap-2">
                    <span>⏳</span>
                    <span>
                      Đang chờ hoàn tất phân tích {assignmentStats.aiProcessingCount} câu hỏi... Điểm số và nhận xét tổng thể sẽ tự động cập nhật khi AI hoàn tất.
                    </span>
                  </div>
                ) : assignmentStats.teacherReviewCount > 0 ? (
                  <div className="rounded-xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800 p-3 text-xs text-amber-900 dark:text-amber-200 flex items-center gap-2">
                    <span>⏳</span>
                    <span>
                      Bài tập đang có {assignmentStats.teacherReviewCount} câu chờ giáo viên chấm/duyệt lập luận. Điểm số cuối cùng sẽ được cập nhật sau khi giáo viên hoàn tất chấm bài.
                    </span>
                  </div>
                ) : null}
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
                      const isVoided = Boolean(q.isVoided);
                      const qStatus = q.latestAttempt?.status || q.attemptStatus;
                      const isProcessing = qStatus === "PendingAnalysis" || qStatus === "Processing";
                      const isReview = qStatus === "NeedsTeacherReview";
                      const isCompleted = qStatus === "Completed";
                      const isSubmitted = isProcessing || isReview || isCompleted || Boolean(q.latestAttempt) || Boolean(q.submittedAttemptId);
                      const isAnswered = isSubmitted || checkQuestionCompletion(q);

                      return (
                        <button
                          key={q.questionId}
                          type="button"
                          onClick={() => handleSwitchQuestion(q.questionId)}
                          className={`w-8 h-8 rounded-xl font-black text-xs transition-all cursor-pointer flex items-center justify-center ${
                            isCurrent
                              ? "bg-indigo-600 text-white shadow-md shadow-indigo-600/30 ring-2 ring-indigo-400 ring-offset-2 dark:ring-offset-slate-900 scale-105"
                              : isVoided
                              ? "bg-purple-600 text-white shadow-2xs hover:bg-purple-700 ring-1 ring-purple-400/50"
                              : isProcessing
                              ? "bg-indigo-500 text-white shadow-2xs hover:bg-indigo-600"
                              : isCompleted
                              ? "bg-emerald-600 text-white shadow-2xs hover:bg-emerald-700"
                              : isReview
                              ? "bg-amber-500 text-white shadow-2xs hover:bg-amber-600"
                              : isAnswered
                              ? "bg-emerald-500 text-white shadow-2xs hover:bg-emerald-600"
                              : "bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-slate-700 border border-slate-200/80 dark:border-slate-700"
                          }`}
                          title={`Câu ${idx + 1}: ${isVoided ? "Được miễn / Tính trọn điểm do lỗi đề" : isProcessing ? "AI đang phân tích" : isCompleted ? "Đã chấm xong" : isReview ? "Chờ GV duyệt" : isAnswered ? "Đã làm" : "Chưa làm"}`}
                        >
                          {idx + 1}
                        </button>
                      );
                    })}
                  </div>
                </div>

                <div className="text-xs font-bold text-slate-500 dark:text-slate-400 flex items-center gap-1.5 flex-wrap">
                  <span>Tiến độ:</span>
                  <span className="text-indigo-600 dark:text-indigo-400 font-extrabold">
                    {answeredCount} / {totalQuestions} câu đã trả lời
                  </span>
                  {voidedQuestionsCount > 0 && (
                    <span className="text-purple-600 dark:text-purple-400 font-bold">
                      ({voidedQuestionsCount} câu được miễn)
                    </span>
                  )}
                </div>
              </div>
            )}

            {/* Conflict Banner when 409 happens */}
            {assignmentId && !isAssignmentSubmitted && !isLocallySubmitted && draftConflict && (
              <div
                id="assignment-draft-conflict-banner"
                className="rounded-3xl border border-amber-500/40 bg-amber-50 dark:bg-amber-950/40 p-5 sm:p-6 shadow-sm space-y-3"
              >
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                  <div className="flex items-start gap-3">
                    <span className="text-2xl mt-0.5">⚠️</span>
                    <div>
                      <h3 className="text-sm sm:text-base font-bold text-amber-900 dark:text-amber-200">
                        Xung đột phiên bản bản nháp
                      </h3>
                      <p className="text-xs text-amber-800 dark:text-amber-300 mt-1">
                        {draftConflict.message}
                        {draftConflict.serverVersion !== undefined && (
                          <span className="font-semibold"> (Phiên bản máy chủ: {draftConflict.serverVersion})</span>
                        )}
                      </p>
                      <p className="text-[11px] text-amber-700/80 dark:text-amber-400/80 mt-0.5">
                        Bài làm trên máy này vẫn được lưu an toàn tại trình duyệt và chưa bị mất.
                      </p>
                    </div>
                  </div>

                  <div className="flex items-center gap-2.5 shrink-0 self-end sm:self-center">
                    <button
                      type="button"
                      id="btn-sync-server-draft"
                      onClick={handleSyncWithServer}
                      className="px-3.5 py-2 text-xs font-bold rounded-xl bg-amber-600 hover:bg-amber-700 text-white shadow-xs transition-colors cursor-pointer"
                    >
                      Tải lại từ máy chủ
                    </button>
                    <button
                      type="button"
                      id="btn-force-overwrite-draft"
                      onClick={handleForceOverwriteLocal}
                      className="px-3.5 py-2 text-xs font-bold rounded-xl border border-amber-600/50 hover:bg-amber-600/20 text-amber-900 dark:text-amber-200 transition-colors cursor-pointer"
                    >
                      Giữ bài làm này & Ghi đè
                    </button>
                  </div>
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

              {/* Notice for voided question or existing attempt */}
              {assignmentQuestion?.isVoided ? (
                <div className="rounded-2xl p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3 bg-purple-50 dark:bg-purple-950/40 border border-purple-200 dark:border-purple-800 text-xs">
                  <div className="space-y-1">
                    <div className="flex items-center gap-2 text-purple-900 dark:text-purple-200 font-bold text-sm">
                      <span>🛡️</span>
                      <span>
                        Câu hỏi này đã được giáo viên miễn và tính trọn điểm
                        {assignmentQuestion.voidedScore != null ? ` (+${assignmentQuestion.voidedScore} điểm)` : " (+điểm tối đa)"}.
                      </span>
                    </div>
                    <p className="text-purple-700 dark:text-purple-300">
                      {assignmentQuestion.voidReason ? `Lý do: ${assignmentQuestion.voidReason}` : "Đề bài có sai sót, toàn bộ học sinh được cộng đủ điểm câu này mà không cần làm."}
                    </p>
                  </div>
                  <span className="px-3 py-1 rounded-full font-bold bg-purple-100 dark:bg-purple-900/60 text-purple-900 dark:text-purple-200 shrink-0 self-start sm:self-auto">
                    Được miễn · Đã tính trọn điểm
                  </span>
                </div>
              ) : (assignmentQuestion?.attemptStatus || assignmentQuestion?.latestAttempt || isCurrentQuestionSubmitted) ? (
                <div className="rounded-2xl p-3.5 flex flex-wrap items-center justify-between gap-2 bg-indigo-50/70 dark:bg-indigo-950/40 border border-indigo-200/80 dark:border-indigo-800 text-xs">
                  <div className="flex items-center gap-2 text-indigo-950 dark:text-indigo-200 font-semibold">
                    <span className="text-base">📋</span>
                    <span>
                      {assignmentQuestion?.latestAttempt?.skipped
                        ? "Câu hỏi này đã được bỏ qua khi nộp bài."
                        : assignmentQuestion?.attemptStatus === "PendingAnalysis" || assignmentQuestion?.attemptStatus === "Processing" || assignmentQuestion?.latestAttempt?.status === "PendingAnalysis" || assignmentQuestion?.latestAttempt?.status === "Processing"
                        ? "Câu hỏi này đã nộp bài thành công. AI đang phân tích bài làm..."
                        : assignmentQuestion?.attemptStatus === "NeedsTeacherReview" || assignmentQuestion?.latestAttempt?.status === "NeedsTeacherReview"
                        ? "Câu hỏi này đã được nộp bài và đang chờ giáo viên chấm/duyệt."
                        : assignmentQuestion?.attemptStatus === "Completed" || assignmentQuestion?.latestAttempt?.status === "Completed"
                        ? "Câu hỏi này đã hoàn thành đánh giá."
                        : "Câu hỏi này đã nộp bài thành công."}
                    </span>
                  </div>
                  <span className="px-2.5 py-1 rounded-full font-bold bg-indigo-100 dark:bg-indigo-900/60 text-indigo-900 dark:text-indigo-200 shrink-0">
                    {assignmentQuestion?.attemptStatus === "PendingAnalysis" || assignmentQuestion?.attemptStatus === "Processing" || assignmentQuestion?.latestAttempt?.status === "PendingAnalysis" || assignmentQuestion?.latestAttempt?.status === "Processing"
                      ? "Đã nộp · AI đang phân tích"
                      : assignmentQuestion?.attemptStatus === "Completed" || assignmentQuestion?.latestAttempt?.status === "Completed"
                      ? "Đã đánh giá"
                      : "Đã nộp · Chế độ xem lại"}
                  </span>
                </div>
              ) : null}

              {/* Question Text Statement */}
              <div>
                <div className="text-base sm:text-lg font-bold text-slate-900 dark:text-white leading-relaxed whitespace-pre-wrap">
                  <RichMathText content={question?.questionText} text={question?.questionText} />
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

              {/* 3. Thanh 3 Nút Công Cụ Trợ Lý (Casio, Nháp, Đồ Thị) */}
              <div className="pt-2">
                  <div className="flex items-center justify-between mb-2">
                    <span className="text-[11px] font-bold uppercase tracking-wider text-stone-400">
                      Công cụ hỗ trợ làm bài
                    </span>
                  </div>

                  <div className="grid grid-cols-1 sm:grid-cols-3 gap-2.5">
                    {/* Button 1: Máy tính Casio fx-580VN */}
                    <button
                      type="button"
                      onClick={() => setActiveSideTool(activeSideTool === "casio" ? null : "casio")}
                      className={`flex items-center gap-2.5 p-3 rounded-xl font-medium text-xs sm:text-sm transition-all border text-left cursor-pointer ${
                        activeSideTool === "casio"
                          ? "border-amber-500 bg-amber-50 dark:bg-amber-950/40 text-amber-900 dark:text-amber-200 ring-1 ring-amber-500"
                          : "border-stone-200 dark:border-stone-800 bg-stone-50/70 dark:bg-stone-900/50 hover:bg-stone-100 dark:hover:bg-stone-800 text-stone-700 dark:text-stone-300"
                      }`}
                    >
                      <span className="text-base leading-none">🖩</span>
                      <div className="min-w-0">
                        <div className="font-bold text-xs text-stone-900 dark:text-stone-100">Máy tính Casio</div>
                        <div className="text-[10px] text-stone-500 dark:text-stone-400 truncate">fx-580VN X</div>
                      </div>
                    </button>

                    {/* Button 2: Bảng nháp vẽ tay */}
                    <button
                      type="button"
                      onClick={() => setActiveSideTool(activeSideTool === "scratchpad" ? null : "scratchpad")}
                      className={`flex items-center gap-2.5 p-3 rounded-xl font-medium text-xs sm:text-sm transition-all border text-left cursor-pointer ${
                        activeSideTool === "scratchpad"
                          ? "border-emerald-500 bg-emerald-50 dark:bg-emerald-950/40 text-emerald-900 dark:text-emerald-200 ring-1 ring-emerald-500"
                          : "border-stone-200 dark:border-stone-800 bg-stone-50/70 dark:bg-stone-900/50 hover:bg-stone-100 dark:hover:bg-stone-800 text-stone-700 dark:text-stone-300"
                      }`}
                    >
                      <span className="text-base leading-none">✏️</span>
                      <div className="min-w-0">
                        <div className="font-bold text-xs text-stone-900 dark:text-stone-100">Bảng vẽ nháp</div>
                        <div className="text-[10px] text-stone-500 dark:text-stone-400 truncate">
                          {attachedSnapshotDataUrl ? "✓ Đã đính kèm ảnh" : "Thu phóng & vẽ tự do"}
                        </div>
                      </div>
                    </button>

                    {/* Button 3: Khảo sát đồ thị */}
                    <button
                      type="button"
                      onClick={() => setActiveSideTool(activeSideTool === "graph" ? null : "graph")}
                      className={`flex items-center gap-2.5 p-3 rounded-xl font-medium text-xs sm:text-sm transition-all border text-left cursor-pointer ${
                        activeSideTool === "graph"
                          ? "border-sky-500 bg-sky-50 dark:bg-sky-950/40 text-sky-900 dark:text-sky-200 ring-1 ring-sky-500"
                          : "border-stone-200 dark:border-stone-800 bg-stone-50/70 dark:bg-stone-900/50 hover:bg-stone-100 dark:hover:bg-stone-800 text-stone-700 dark:text-stone-300"
                      }`}
                    >
                      <span className="text-base leading-none">📈</span>
                      <div className="min-w-0">
                        <div className="font-bold text-xs text-stone-900 dark:text-stone-100">Vẽ đồ thị</div>
                        <div className="text-[10px] text-stone-500 dark:text-stone-400 truncate">Khảo sát hàm Oxy</div>
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
                </div>

                {/* Multiple choice grid */}
                {question?.questionType === "MultipleChoice" ? (
                  <fieldset className="grid grid-cols-1 sm:grid-cols-2 gap-3" disabled={isReadOnly}>
                    <legend className="sr-only">Chọn một đáp án</legend>
                    {question.options.map((option) => {
                      const isSelected =
                        finalAnswer === option.optionId ||
                        finalAnswer?.trim().toUpperCase() === option.label.toUpperCase() ||
                        finalAnswer?.startsWith(option.label + ".") ||
                        finalAnswer === option.text;
                      return (
                        <button
                          key={option.optionId}
                          type="button"
                          disabled={isReadOnly}
                          onClick={() => {
                            if (!isReadOnly) handleAnswerChange(option.optionId, "");
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
                              <RichMathText content={option.text} />
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
                    <ModeAwareAnswerEditor
                      ref={answerEditorRef}
                      profile="answering"
                      variant="student"
                      questionType={question?.questionType || "ShortAnswer"}
                      evaluationMode={question?.answerEvaluationMode || "NumericRational"}
                      value={{ rawText: finalAnswer, displayLatex: answerDisplayLatex }}
                      onChange={(val) => handleAnswerChange(val.rawText, val.displayLatex)}
                      onFocus={() => setActiveInputTarget("answer")}
                      disabled={isReadOnly}
                      readOnly={isAssignmentSubmitted}
                      placeholder={
                        isAssignmentSubmitted
                          ? "Chưa có đáp số"
                          : question?.questionType === "Essay"
                          ? "Nhập kết luận hoặc đáp án; dùng Chèn công thức để thêm biểu thức toán..."
                          : question?.answerEvaluationMode === "Manual"
                          ? "Nhập câu trả lời ngắn; có thể kết hợp chữ và công thức toán..."
                          : "Gõ công thức hoặc đáp số cuối cùng (hoặc dùng Casio để tự chèn)..."
                      }
                      showPreview={false}
                      showSyntaxHint={false}
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

                <RichMathEditor
                  ref={reasoningEditorRef}
                  variant="student"
                  value={reasoningText}
                  onChange={(val) => {
                    if (!isReadOnly) handleReasoningChange(val);
                  }}
                  onFocus={() => setActiveInputTarget("reasoning")}
                  disabled={isReadOnly}
                  minHeight="120px"
                  placeholder={
                    isAssignmentSubmitted
                      ? "Chưa có nội dung lập luận cho câu hỏi này."
                      : "Trình bày các bước biến đổi, suy luận toán học để AI phân tích chất lượng tư duy... (Gõ $ hoặc Ctrl+M để chèn công thức toán)"
                  }
                  className={isAssignmentSubmitted ? "opacity-90" : ""}
                />

                {/* 6. Attached Scratchpad Snapshot Card (Cố định, không bị ảnh hưởng khi vẽ tiếp hay F5) */}
                {isCurrentQuestionSubmitted && reviewAttemptId &&
                  (assignmentQuestion?.hasAttachment || reviewFeedbackQuery.data?.studentSubmission?.attachmentUrl || immediateQuestionFeedback?.studentSubmission?.attachmentUrl) ? (
                  <AttemptScratchpadAttachment attemptId={reviewAttemptId} />
                ) : !isCurrentQuestionSubmitted && attachedSnapshotDataUrl ? (
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
                      {!isReadOnly && (
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
                ) : null}
              </div>

              {/* Persisted question-level grading. Navigation only changes the attempt GET key. */}
              {assignmentId && isCurrentQuestionSubmitted && (
                <div className="border-t border-slate-100 dark:border-slate-800 pt-6">
                  {reviewFeedbackQuery.isLoading ? (
                    <div
                      role="status"
                      className="rounded-2xl border border-indigo-200 dark:border-indigo-800 bg-indigo-50/70 dark:bg-indigo-950/40 p-5 text-sm font-semibold text-indigo-800 dark:text-indigo-200"
                    >
                      Đang tải phân tích và lời giải đã lưu của câu này...
                    </div>
                  ) : reviewFeedbackQuery.isError ? (
                    <div
                      role="alert"
                      className="rounded-2xl border border-rose-200 dark:border-rose-800 bg-rose-50 dark:bg-rose-950/40 p-5 text-sm text-rose-800 dark:text-rose-200"
                    >
                      <p className="font-bold">Không thể tải kết quả chi tiết của câu này.</p>
                      <p className="mt-1 text-xs">Bài làm đã được lưu. Bạn có thể tải lại dữ liệu mà không chấm AI lại.</p>
                      <button
                        type="button"
                        onClick={() => void reviewFeedbackQuery.refetch()}
                        className="mt-3 rounded-xl border border-rose-300 dark:border-rose-700 px-3 py-2 text-xs font-bold hover:bg-rose-100 dark:hover:bg-rose-900/40"
                      >
                        Tải lại kết quả
                      </button>
                    </div>
                  ) : reviewFeedbackQuery.data &&
                    isFeedbackForQuestion(assignmentQuestion?.questionId ?? "", reviewFeedbackQuery.data.questionId) ? (
                    <AttemptFeedbackHierarchy
                      feedbackData={reviewFeedbackQuery.data}
                      assignmentQuestionCount={assignmentQuestions.length}
                      questionType={assignmentQuestion?.questionType}
                      showStudentSubmission={false}
                      answerOptions={assignmentQuestion?.options ?? []}
                      onRefreshFeedback={async () => {
                        await reviewFeedbackQuery.refetch();
                      }}
                      onPollJob={(jobId) => {
                        setFeedbackData(null);
                        pollingAttemptRef.current = 0;
                        setNetworkErrorPaused(false);
                        setPollingJobId(jobId);
                      }}
                    />
                  ) : reviewFeedbackQuery.data ? (
                    <div role="alert" className="rounded-2xl border border-rose-200 bg-rose-50 p-5 text-sm font-bold text-rose-800">
                      Dữ liệu kết quả không khớp với câu hỏi đang xem. Vui lòng tải lại trang.
                    </div>
                  ) : null}
                </div>
              )}

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
                  <div className="flex items-center justify-between w-full">
                    <button
                      type="button"
                      disabled={currentAssignmentIndex <= 0}
                      onClick={() => handleSwitchQuestion(assignmentQuestions[currentAssignmentIndex - 1].questionId)}
                      className="px-4 py-2.5 rounded-xl bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-200 font-bold text-xs disabled:opacity-40 cursor-pointer"
                    >
                      ← Câu trước
                    </button>
                    <button
                      type="button"
                      disabled={currentAssignmentIndex >= totalQuestions - 1}
                      onClick={() => handleSwitchQuestion(assignmentQuestions[currentAssignmentIndex + 1].questionId)}
                      className="px-5 py-2.5 rounded-xl bg-indigo-50 dark:bg-indigo-950/60 hover:bg-indigo-100 text-indigo-700 dark:text-indigo-300 font-bold text-xs disabled:opacity-40 cursor-pointer border border-indigo-200/80 dark:border-indigo-800"
                    >
                      Câu tiếp theo →
                    </button>
                  </div>
                ) : isReadOnly ? (
                  <p role="status" className="text-xs text-slate-500 dark:text-slate-400">
                    {isCurrentQuestionSubmitted || isAssignmentSubmitted
                      ? "Bài làm đã nộp — chỉ có thể xem lại."
                      : isAssignmentExpired ? "Bài tập đã hết hạn." : "Đang nộp bài…"}
                  </p>
                ) : (
                  <div className="flex items-center justify-between w-full">
                    <button
                      type="button"
                      onClick={() => handleFinalSubmit()}
                      disabled={isSubmitting}
                      className="text-xs font-semibold text-stone-400 hover:text-stone-700 dark:hover:text-stone-200 cursor-pointer"
                    >
                      Bỏ qua câu này
                    </button>
                    <button
                      type="button"
                      onClick={() => handleFinalSubmit()}
                      disabled={isSubmitting}
                      className="flex items-center gap-1.5 rounded-xl bg-stone-900 hover:bg-stone-800 dark:bg-stone-100 dark:hover:bg-stone-200 dark:text-stone-900 px-5 py-2.5 text-xs sm:text-sm font-semibold text-white transition-colors disabled:opacity-50 cursor-pointer"
                    >
                      <span>Nộp bài & Phân tích tư duy</span>
                      <span>→</span>
                    </button>
                  </div>
                )}
              </div>
            </div>
          </div>

          {/* Right Pane: Assistant Workspace ~30% */}
          {activeSideTool && (
            <div className="lg:col-span-4 xl:col-span-4 sticky top-4 max-h-[calc(100vh-2rem)] flex flex-col">
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
                submittedAttemptId={reviewAttemptId}
                hasSubmittedScratchpad={Boolean(assignmentQuestion?.hasAttachment || reviewFeedbackQuery.data?.studentSubmission?.attachmentUrl || immediateQuestionFeedback?.studentSubmission?.attachmentUrl)}
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

            {unansweredQuestionIndices.length > 0 && (
              <div className="p-3.5 rounded-2xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800 text-xs text-amber-900 dark:text-amber-300 space-y-1 text-left">
                <div className="font-bold flex items-center gap-1.5">
                  <span>⚠️</span>
                  <span>Các câu chưa điền đáp án:</span>
                </div>
                <p className="leading-relaxed">
                  <span className="font-black text-amber-800 dark:text-amber-200">
                    {unansweredQuestionIndices.map((n) => `Câu ${n}`).join(", ")}
                  </span>{" "}
                  sẽ được hệ thống ghi nhận là <strong>Bỏ qua (0 điểm)</strong>.
                </p>
              </div>
            )}

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
