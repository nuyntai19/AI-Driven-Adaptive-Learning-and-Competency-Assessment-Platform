export interface SubmitAttemptRequest {
  questionId: string | number;
  assignmentId?: string | null;
  finalAnswer: string;
  reasoningText?: string | null;
  timeSpentSeconds: number;
  confidence: number;
  answerChanges: number;
  skipped: boolean;
  reasoningLanguage: string;
  clientSubmissionId: string;
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
  jobId: string;
  attemptId: string;
  status: string;
  terminal: boolean;
  error?: string | null;
  feedbackUrl?: string | null;
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

export interface AttemptFeedbackDataDto {
  attemptId: string;
  questionId: string;
  status: string;
  grading: AttemptFeedbackGradingDto;
  analysis?: AttemptFeedbackAnalysisDto | null;
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
}
