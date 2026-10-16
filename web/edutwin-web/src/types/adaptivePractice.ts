export type AdaptivePracticeMode =
  | 'Weakness'
  | 'LearningPath'
  | 'CustomTopic'
  | 'SpacedReview'
  | 'ExamReview';

export type AdaptivePracticeSessionStatus =
  | 'Created'
  | 'InProgress'
  | 'AwaitingEvaluation'
  | 'Paused'
  | 'Closed';

export type AdaptivePracticeCloseReason =
  | 'GoalAchievedWithEvidence'
  | 'QuestionLimitReached'
  | 'TimeBudgetReached'
  | 'RemediationNeeded'
  | 'StudentStopped'
  | 'OutOfQuestions'
  | 'Expired'
  | 'EvaluationFailed';

export interface AdaptivePracticeTopicOption {
  topicNodeId: string;
  topicName: string;
  currentMastery: number;
  hasPrerequisites: boolean;
}

export interface AdaptivePracticeProposal {
  subjectId: string;
  subjectName: string;
  topicNodeId: string;
  topicName: string;
  focusSkillDescription: string;
  currentMastery: number;
  targetMastery: number;
  selectionReason: string;
  recommendedTimeMinutes: number;
  isDiagnosticRecommended: boolean;
  defaultMode: AdaptivePracticeMode;
  availableTopics: AdaptivePracticeTopicOption[];
}

export interface CreateAdaptivePracticeSessionRequest {
  subjectId: string;
  topicNodeId: string;
  practiceMode?: AdaptivePracticeMode;
  timeBudgetMinutes?: number;
  customTargetMastery?: number;
  learningPathId?: string;
  learningPathItemId?: string;
}

export interface AdaptivePracticeSession {
  sessionId: string;
  studentId: string;
  centerId: string;
  subjectId: string;
  subjectName: string;
  topicNodeId: string;
  topicName: string;
  practiceMode: AdaptivePracticeMode;
  startingMastery: number;
  targetMastery: number;
  currentMastery: number;
  timeBudgetMinutes: number;
  minimumQuestions: number;
  maximumQuestions: number;
  questionCount: number;
  status: AdaptivePracticeSessionStatus;
  closeReason?: AdaptivePracticeCloseReason | null;
  selectionReason: string;
  startedAt?: string | null;
  pausedAt?: string | null;
  completedAt?: string | null;
  elapsedSeconds: number;
  isDiagnosticMode: boolean;
  currentDifficulty: number;
}

export interface AdaptivePracticeQuestionOption {
  optionId: string;
  label: string;
  text: string;
  orderIndex: number;
}

export interface AdaptivePracticeQuestion {
  sessionItemId: string;
  questionId: string;
  topicNodeId: string;
  topicName: string;
  difficulty: number;
  questionText: string;
  questionType: 'MultipleChoice' | 'ShortAnswer' | 'Essay';
  options: AdaptivePracticeQuestionOption[];
  hasImage: boolean;
  maxScore: number;
  estimatedTimeSeconds: number;
  selectionReasonCode: string;
  selectionReasonText: string;
  sequenceNumber: number;
  totalQuestionsEstimatedMin: number;
  totalQuestionsEstimatedMax: number;
  availableHintsCount: number;
  hintsUsedCount: number;
  isPrerequisiteCheck: boolean;
}

export interface AdaptivePracticeHint {
  hintLevel: number;
  hintTitle: string;
  hintContent: string;
  supportLevelDescription: string;
}

export interface SubmitAdaptivePracticeAnswerRequest {
  clientSubmissionId: string;
  questionId: string;
  sessionItemId: string;
  finalAnswer: string;
  reasoningText?: string;
  studentConfidence?: number;
  timeSpentSeconds: number;
}

export interface AdaptivePracticeSummary {
  sessionId: string;
  totalQuestions: number;
  correctCount: number;
  incorrectCount: number;
  partialCount: number;
  pendingCount: number;
  timeSpentSeconds: number;
  startingMastery: number;
  endingMastery?: number | null;
  goalAchievementStatus: 'Achieved' | 'PartiallyAchieved' | 'NotAchieved' | 'RemediationRecommended' | string;
  strongSkills: string[];
  weakSkills: string[];
  remediationSkills: string[];
  summaryConclusion: string;
  recommendedNextAction: string;
  recommendedNextTopicId?: string | null;
  recommendedNextTopicName?: string | null;
}

export interface AdaptivePracticeAnswerFeedback {
  isCorrect: boolean;
  awardedScore: number;
  correctAnswer: string;
  solutionExplanation: string;
  stepGuidance?: string | null;
  hintsUsed: number;
  misconceptionHypothesis?: string | null;
  nextActionRecommendation: string;
  isSessionClosed: boolean;
  closeReason?: AdaptivePracticeCloseReason | null;
  sessionSummary?: AdaptivePracticeSummary | null;
  updatedEstimatedMastery: number;
}

export interface AdaptivePracticeHistoryItem {
  sessionId: string;
  subjectName: string;
  topicName: string;
  practiceMode: AdaptivePracticeMode;
  totalQuestions: number;
  correctCount: number;
  startingMastery: number;
  endingMastery?: number | null;
  status: AdaptivePracticeSessionStatus;
  closeReason?: AdaptivePracticeCloseReason | null;
  completedAt?: string | null;
  timeSpentSeconds: number;
}

export interface TeacherAdaptivePracticeSession {
  sessionId: string;
  studentId: string;
  studentName: string;
  className: string;
  subjectName: string;
  topicName: string;
  totalQuestions: number;
  correctCount: number;
  goalAchievementStatus: string;
  closeReason?: AdaptivePracticeCloseReason | null;
  completedAt?: string | null;
  requiresIntervention: boolean;
  interventionReason?: string | null;
}

export interface TeacherAdaptivePracticeAlert {
  alertId: string;
  studentId: string;
  studentName: string;
  className: string;
  subjectId: string;
  subjectName: string;
  topicNodeId: string;
  topicName: string;
  alertType: string;
  description: string;
  createdAt: string;
}
