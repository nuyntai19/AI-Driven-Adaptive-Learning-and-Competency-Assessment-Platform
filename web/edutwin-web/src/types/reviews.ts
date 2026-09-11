export type ErrorType =
  | "None"
  | "Knowledge"
  | "Skill"
  | "Reasoning"
  | "Behavior"
  | "Presentation"
  | "Unknown";

export interface EvidenceDecisionDto {
  mode?: string;
  sourceType?: string;
  trustLevel?: string;
}

export interface TeacherReviewQueueItemDto {
  attemptId: string;
  studentId: string;
  studentName: string;
  questionId: string;
  questionText: string;
  analysisId: string;
  finalAnswer: string;
  reasoningText?: string | null;
  isFallback: boolean;
  reasoningQuality?: number | null;
  analysisFeedback?: string | null;
  analysisConfidence?: number | null;
  evidence: EvidenceDecisionDto;
  submittedAt: string;
}

export interface TeacherReviewQueueQuery {
  classId?: string;
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
