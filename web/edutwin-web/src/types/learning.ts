export interface SubmitAttemptRequest {
  questionId: string | number;
  assignmentId?: string | null;
  finalAnswer: string;
  reasoningText?: string | null;
  timeSpentSeconds: number;
  confidence: number;
  answerChanges: number;
  skipped: boolean;
  clientSubmissionId: string;
  answerDisplayLatex?: string | null;
  drawingUploadToken?: string | null;
}

export interface SubmitAttemptDataDto {
  attemptId: string;
  analysisJobId?: string;
  jobId?: string;
  attemptStatus?: string;
  jobStatus?: string;
  status?: string;
  pollUrl?: string;
  feedbackUrl?: string;
}

export interface AnalysisJobStatusDataDto {
  analysisJobId: string;
  attemptId: string;
  status: string;
  retryCount: number;
  terminal: boolean;
  feedbackUrl?: string | null;
  updatedAt: string;
  attemptStatus?: string | null;
  errorCode?: string | null;
}

export interface AttemptFeedbackGradingDto {
  isCorrect?: boolean | null;
  awardedScore?: number | null;
  maxScore: number;
}

export interface AttemptFeedbackRootCauseNodeDto {
  nodeId: string;
  nodeName: string;
}

export interface AttemptFeedbackAnalysisDto {
  analysisId: string;
  schemaVersion: string;
  methodDetected?: string | null;
  reasoningQuality?: number | null;
  qualityBand?: string | null;
  errorType?: string | null;
  misconception?: string | null;
  missingSteps: string[];
  rootCauseNodes: AttemptFeedbackRootCauseNodeDto[];
  confidence?: number | null;
  feedback: string;
  isFallback: boolean;
  needsTeacherReview: boolean;
  hasTeacherOverride: boolean;
  solutionType?: "REFINED" | "CORRECTED" | "GENERATED" | "MODEL_ANSWER" | string | null;
  aiSolution?: string | null;
}

export interface AttemptFeedbackTwinChangeDto {
  topicNodeId: string;
  topicName: string;
  previousMastery: number;
  newMastery: number;
  delta: number;
  explanation: string;
}

export interface AttemptFeedbackRecommendationDto {
  recommendationId: string;
  type: string;
  topicNodeId: string;
  topicName: string;
  questionId?: string | null;
  opportunityScore: number;
  explanation: string;
}

export interface AttemptFeedbackStudentSubmissionDto {
  finalAnswer: string;
  reasoningText?: string | null;
  confidence: number;
  timeSpentSeconds: number;
  answerChanges: number;
  attachmentUrl?: string | null;
}

export interface AttemptFeedbackGradingCriteriaDto {
  scoringNotes: string;
  requiredIdeas: string[];
  commonErrors: string[];
}

export interface AttemptFeedbackTeacherSolutionDto {
  correctAnswer: string;
  solution: string;
  expectedReasoning?: string | null;
  gradingCriteria?: AttemptFeedbackGradingCriteriaDto | null;
}

export interface AttemptFeedbackTeacherFinalEvaluationDto {
  hasTeacherOverride: boolean;
  isApprovedAsIs?: boolean;
  reviewDecision?: string | null;
  teacherReviewNote?: string | null;
  teacherIsCorrect?: boolean | null;
  teacherScore?: number | null;
  teacherFeedback?: string | null;
  reviewedByTeacherName?: string | null;
  reviewedAt?: string | null;
  originalAIRawGrade?: AttemptFeedbackGradingDto | null;
}

export interface StudentReviewRequestDto {
  requestId: number | string;
  attemptId: number | string;
  studentId: string;
  questionId: number | string;
  studentComment: string;
  status: 'Pending' | 'Resolved' | 'Rejected' | string;
  teacherNote?: string | null;
  resolvedByTeacherId?: string | null;
  resolvedAt?: string | null;
  createdAt: string;
}

export interface RetryQuotaDto {
  manualRetriesUsed: number;
  manualRetriesRemaining: number;
  cooldownRemainingSeconds: number;
  canRetry: boolean;
  nextRetryAllowedAt?: string | null;
}

export interface AttemptFeedbackDataDto {
  attemptId: string;
  questionId: string;
  status: string;
  grading: AttemptFeedbackGradingDto;
  studentSubmission?: AttemptFeedbackStudentSubmissionDto | null;
  teacherSolution?: AttemptFeedbackTeacherSolutionDto | null;
  analysis?: AttemptFeedbackAnalysisDto | null;
  teacherFinalEvaluation?: AttemptFeedbackTeacherFinalEvaluationDto | null;
  reviewRequest?: StudentReviewRequestDto | null;
  retryQuota?: RetryQuotaDto | null;
  twinChange?: AttemptFeedbackTwinChangeDto | null;
  recommendation?: AttemptFeedbackRecommendationDto | null;
}

export interface NextQuestionDataDto {
  strategy: string;
  recommendationId?: string | null;
  questionId: string;
  topicNodeId: string;
  topicName: string;
  topicMastery: number;
  questionType: string;
  difficulty: number;
  questionText: string;
  maxScore: number;
  estimatedTimeSeconds: number;
  reasoningRequired: boolean;
  languageCode: string;
  options: Array<{
    optionId: string;
    label: string;
    text: string;
    orderIndex: number;
  }>;
  explanation: string;
  answerEvaluationMode: string;
}

export interface RetryAIAnalysisResponse {
  attemptId: string;
  analysisJobId?: string | null;
  status: string;
  pollUrl?: string | null;
  feedbackUrl?: string | null;
  retryQuota?: RetryQuotaDto | null;
}

export interface CreateStudentReviewRequestDto {
  studentComment: string;
  disputeCategory?: string;
}
