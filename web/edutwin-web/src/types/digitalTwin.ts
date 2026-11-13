export interface TopicTwinNodeDto {
  topicNodeId: string;
  topicName: string;
  masteryPercentage: number;
  evidenceCount: number;
  lastReasoningQuality?: number | null;
  lastAttemptId?: string | null;
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
  studentId: string;
  subjectId: string;
  topics: TopicTwinNodeDto[];
  behavior: BehaviorTwinMetricsDto;
  /** Why no topics are in scope (e.g. the class has no published curriculum applied). */
  scopeMessage?: string | null;
}

export interface TwinUpdateHistoryItemDto {
  historyId: string;
  topicNodeId: string;
  topicName: string;
  eventSource: string;
  learningContext: "Assignment" | "AdaptivePractice" | "Unknown";
  contextLabel: string;
  questionText?: string | null;
  previousMastery: number;
  newMastery: number;
  delta: number;
  explanation: string;
  reasoningQuality?: number | null;
  recordedAt: string;
}
