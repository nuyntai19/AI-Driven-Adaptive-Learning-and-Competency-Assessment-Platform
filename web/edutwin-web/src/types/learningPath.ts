export interface LearningPathTopicNodeDto {
  topicNodeId: string;
  nodeName: string;
  currentMastery: number;
  evidenceCount: number;
  description?: string | null;
}

export interface LearningPathTopicsResponse {
  data: LearningPathTopicNodeDto[];
}

export interface StudentLearningPathPreferenceDto {
  subjectId: string;
  selfAssessedLevel: 'VeryWeak' | 'Weak' | 'Medium' | 'Good' | 'Excellent' | string;
  weakTopicNodeIds: number[];
  focusTopicNodeIds: number[];
  goalType: 'Foundation' | 'KeepUp' | 'ImproveGrade' | 'ExamPrep' | 'Advanced' | string;
  targetMastery: number;
  targetWeeks: number;
  minutesPerDay: number;
  daysPerWeek: number;
  pace: 'Gentle' | 'Moderate' | 'Accelerated' | string;
  preferredMode: 'TheoryHeavy' | 'PracticeHeavy' | 'Balanced' | string;
  note?: string | null;
  updatedAt?: string;
}

export interface StudentLearningPathPreferenceResponse {
  data: StudentLearningPathPreferenceDto | null;
}

export interface GenerateLearningPathRequest {
  subjectId: string;
  selfAssessedLevel: string;
  weakTopicNodeIds: number[];
  focusTopicNodeIds: number[];
  goalType: string;
  targetMastery: number;
  targetWeeks: number;
  minutesPerDay: number;
  daysPerWeek: number;
  pace: string;
  preferredMode: string;
  note?: string | null;
}

export interface LearningPathItemDetailDto {
  learningPathItemId: string;
  topicNodeId: string;
  topicName: string;
  currentMastery: number;
  targetMastery: number;
  rankOrder: number;
  opportunityScore?: number | null;
  reason: string;
  prerequisites?: string | null;
  estimatedMinutes: number;
  status: 'Pending' | 'InProgress' | 'Completed' | string;
  recommendedQuestionId?: string | null;
}

export interface LearningPathPhaseDto {
  phaseNumber: number;
  phaseName: string;
  timeframe: string;
  description: string;
  progressPercentage: number;
  weeks: LearningPathWeekDto[];
}

export interface LearningPathWeekDto {
  weekNumber: number;
  title: string;
  objective: string;
  progressPercentage: number;
  sessions: LearningPathSessionDto[];
  checkpoint: LearningPathCheckpointDto;
}

export interface LearningPathSessionDto {
  sessionId: string;
  title: string;
  type: string;
  topicNodeId: string;
  topicName: string;
  objective: string;
  estimatedMinutes: number;
  onlineActivities: string[];
  offlineActivities: string[];
  recommendedQuestionIds: string[];
  requiredCorrectRate: number;
  targetMastery: number;
  completionCriteria: string[];
  status: 'NotStarted' | 'InProgress' | 'Completed' | 'NeedsReview' | 'Skipped' | string;
}

export interface LearningPathCheckpointDto {
  type: 'Weekly' | 'Monthly' | string;
  currentMastery: number;
  targetMastery: number;
  requiredCorrectRate: number;
  completedTasks: number;
  weakTopics: string[];
  recommendation: string;
}

export interface DetailedLearningPathDto {
  learningPathId: string;
  studentId: string;
  subjectId: string;
  subjectName: string;
  strategy: string;
  version: number;
  status: string;
  generatedAt: string;
  currentOverallMastery: number;
  targetMastery: number;
  estimatedWeeks: number;
  minutesPerDay: number;
  daysPerWeek: number;
  progressPercentage: number;
  goalType: string;
  totalScheduledMinutes: number;
  capacityMinutes: number;
  generationStatus: string;
  adaptationMessage?: string | null;
  nextSession?: LearningPathSessionDto | null;
  recommendationRationale: string;
  phases: LearningPathPhaseDto[];
}

export interface DetailedLearningPathResponse {
  data: DetailedLearningPathDto | null;
}
