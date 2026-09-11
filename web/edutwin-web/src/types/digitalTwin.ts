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
  reasoningQuality?: number | null;
  recordedAt: string;
}
