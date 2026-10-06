export type ErrorType =
  | "None"
  | "Knowledge"
  | "Skill"
  | "Reasoning"
  | "Behavior"
  | "Presentation"
  | "Unknown";

export interface EvidenceDecisionDto {
  evidenceAssessmentId: string;
  sourceType: string;
  trustLevel: string;
  decisionMode: string;
  reasoningWeight: number;
  reasonCodes: string[];
  requiresTeacherReview: boolean;
  policyVersion: string;
  analysisOverrideVersion: number;
  evaluatedAt: string;
}

export interface TeacherReviewQuestionOptionDto {
  optionId: string;
  optionLabel: string;
  optionText: string;
  isCorrect: boolean;
}

export interface RubricScoreInput { criterionId: string; awardedScore: number; comment?: string | null; }
export interface RubricGrade { maxScore: number; awardedScore: number;
  criteria: (RubricScoreInput & { title: string; description: string; maxScore: number })[]; }

export interface TeacherReviewQueueItemDto {
  gradingCriteria?: import("./questions").GradingCriteria | null;
  rubricGrade?: RubricGrade | null;
  attemptId: string;
  assignmentId?: string;
  assignmentTitle?: string;
  studentId: string;
  studentName: string;
  questionId: string;
  questionOrderIndex?: number | null;
  assignmentQuestionCount?: number;
  subjectId: string;
  questionText: string;
  questionType?: string;
  attemptStatus?: string;
  answerEvaluationMode?: string;
  teacherSolution?: string | null;
  expectedReasoning?: string | null;
  methodDetected?: string | null;
  hasAttachment?: boolean;
  answerDisplayLatex?: string | null;
  options?: TeacherReviewQuestionOptionDto[];
  analysisId: string;
  finalAnswer: string;
  reasoningText?: string | null;
  isFallback: boolean;
  reasoningQuality?: number | null;
  originalReasoningQuality?: number | null;
  analysisFeedback?: string | null;
  feedbackOrigin?: string | null;
  aiSolution?: string | null;
  analysisConfidence?: number | null;
  errorType?: string | null;
  evidence: EvidenceDecisionDto;
  submittedAt: string;
  hasStudentReviewRequest?: boolean;
  studentReviewReason?: string | null;
  teacherFinalReviewStatus?: "Pending" | "Approved";
  finalReviewVersion?: number;
  correctAnswer?: string | null;
  maxScore?: number | null;
  awardedScore?: number | null;
  isCorrect?: boolean | null;
  hasTeacherOverride?: boolean;
  overrideAwardedScore?: number | null;
  overrideReason?: string | null;
  teacherFeedback?: string | null;
  reviewDecision?: string | null;
  overrideVersion?: number;
}

export interface TeacherReviewQueueQuery {
  classId?: string;
  assignmentId?: string;
  studentId?: string;
  fromDate?: string;
  toDate?: string;
  includeAllQuestions?: boolean;
  page?: number;
  pageSize?: number;
}

export interface TeacherReviewQueueResponse {
  data: TeacherReviewQueueItemDto[];
  meta: {
    page: number;
    pageSize: number;
    totalItems: number;
    totalPages: number;
    traceId?: string;
    timestamp: string;
  };
}

export interface TeacherOverrideRequest {
  rubricScores?: RubricScoreInput[];
  reasoningQuality: number;
  errorType: ErrorType;
  feedback: string;
  isCorrect: boolean;
  awardedScore?: number | null;
  reason: string;
  overrideVersion: number;
}

export interface TeacherOverrideReplayDto {
  studentId: string;
  topicNodeId: string;
  attemptsReplayed: number;
  previousMastery: number;
  newMastery: number;
  newRiskScore: number;
  recommendationRecalculated: boolean;
}

export interface TeacherOverrideDataDto {
  analysisId: string;
  hasTeacherOverride: boolean;
  overrideVersion: number;
  overriddenAt: string;
  overrideAwardedScore?: number | null;
  effectiveAwardedScore?: number | null;
  replay: TeacherOverrideReplayDto;
}

export interface TeacherOverrideResponse {
  data: TeacherOverrideDataDto;
  meta: {
    traceId?: string;
    timestamp: string;
  };
}
