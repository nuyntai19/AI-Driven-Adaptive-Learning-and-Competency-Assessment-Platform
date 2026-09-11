export interface StudentTwinBasicInfoDto {
  studentId: string;
  fullName: string;
}

export interface StudentTwinSubjectDto {
  subjectId: string;
  subjectName: string;
}

export interface CognitiveGrowthDto {
  overallMastery: number;
  growthVelocity: number;
  currentPredictedScore: number;
  targetScore: number;
  riskScore: number;
}

export interface KnowledgeTwinItemDto {
  topicNodeId: string;
  topicName: string;
  mastery: number;
  evidenceCount: number;
  lastReasoningQuality?: number | null;
  lastAttemptAt?: string | null;
}

export interface BehaviorTwinMetricsDto {
  avgTimeSpentSeconds: number;
  skipRate: number;
  changeAnswerRate: number;
  avgConfidence: number;
  confidenceCalibration: number;
  attemptCount: number;
}

export interface StudentTwinDataDto {
  student: StudentTwinBasicInfoDto;
  subject: StudentTwinSubjectDto;
  cognitiveGrowth: CognitiveGrowthDto;
  knowledgeTwin: KnowledgeTwinItemDto[];
  behaviorTwin: BehaviorTwinMetricsDto;
}

export interface TwinUpdateHistoryItemDto {
  historyId: string;
  topicNodeId: string;
  topicName: string;
  eventSource: string;
  previousMastery: number;
  newMastery: number;
  delta: number;
  explanation: string;
  createdAt: string;
}

export interface PagedList<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
