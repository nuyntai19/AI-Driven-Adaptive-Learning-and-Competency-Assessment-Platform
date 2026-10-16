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
  classId?: string;
  history?: boolean;
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
  forceRegenerate?: boolean;
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
  classId?: string | null;
  startedAtUtc?: string | null;
  rowVersion?: string | null;
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

export interface StartSessionRequest {
  subjectId: string;
  classId?: string;
}

export interface StartSessionDto {
  sessionRunId: number;
  sessionId: string;
  runNumber: number;
  status: string;
  evaluationStatus: string;
  currentQuestionId: string | null;
  checkpointQuestionId: string | null;
  isResumed: boolean;
  currentQuestion?: any | null;
}

export interface StartSessionResponse {
  data: StartSessionDto;
  meta: {
    timestamp: string;
    traceId?: string;
  };
}

export interface ResumeSessionDto {
  sessionRunId: number | null;
  sessionId: string;
  legacySessionId: string | null;
  sessionTitle: string;
  sessionStatus: string;
  runNumber: number;
  runStatus: string;
  evaluationStatus: string;
  outcome: 'Passed' | 'NotPassed' | 'Grading' | 'NeedsReview' | 'InsufficientEvidence' | 'InsufficientQuestionPool' | string | null;
  startedAt: string;
  lastActivityAt: string;
  completedAt: string | null;
  totalQuestions: number;
  questionsAttempted: number;
  questionsCorrect: number;
  scoreRate: number;
  currentQuestionId: string | null;
  currentQuestion?: any | null;
  attemptedQuestionIds: string[];
  isCompleted: boolean;
  isGrading: boolean;
  canStart: boolean;
}

export interface ResumeSessionResponse {
  data: ResumeSessionDto;
  meta: {
    timestamp: string;
    traceId?: string;
  };
}

export interface EvaluateSessionRequest {
  subjectId?: string;
  classId?: string;
  sessionRunId?: number;
}

export interface EvaluateSessionDto {
  sessionRunId: number;
  sessionId: string;
  legacySessionId: string | null;
  sessionTitle: string;
  sessionStatus: string;
  runNumber: number;
  runStatus: string;
  evaluationStatus: string;
  outcome: 'Passed' | 'NotPassed' | 'Grading' | 'NeedsReview' | 'InsufficientEvidence' | 'InsufficientQuestionPool' | string | null;
  totalQuestions: number;
  questionsAttempted: number;
  checkpointsAttempted: number;
  checkpointsCorrect: number;
  checkpointScoreRate: number;
  overallScoreRate: number;
  requiresTeacherReview: boolean;
  hasPendingAIGrading: boolean;
  evaluationDetail: string | null;
  evaluatedAt: string | null;
  isCompleted: boolean;
  passed: boolean;
}

export interface EvaluateSessionResponse {
  data: EvaluateSessionDto;
  meta: {
    timestamp: string;
    traceId?: string;
  };
}

// --- V4 Personalized Learning Path Interfaces ---
export interface LearningGoalDto {
  goalId: string;
  studentId: string;
  subjectId: string;
  classId?: string | null;
  goalType: string;
  title: string;
  description?: string | null;
  targetSkillIds: string[];
  deadlineDate?: string | null;
  availableMinutesPerDay: number;
  availableDaysPerWeek: number;
  preferredPace: string;
  preferredMode: string;
  status: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateLearningGoalRequest {
  subjectId: string;
  classId?: string;
  goalType?: string;
  title?: string;
  description?: string;
  targetSkillIds?: string[];
  deadlineDate?: string | null;
  availableMinutesPerDay?: number;
  availableDaysPerWeek?: number;
  preferredPace?: string;
  preferredMode?: string;
}

export interface ProposedMissionDto {
  contentVersionId?: string | null;
  canStart?: boolean;
  missionId: string;
  title: string;
  purpose: string;
  skillId: string;
  estimatedMinutes: number;
  activityTypes: string[];
  selectionReason: string;
}

export interface ProposedMilestoneDto {
  order: number;
  title: string;
  outcomeDescription: string;
  skillIds: string[];
  missions: ProposedMissionDto[];
  hasTransferChallenge: boolean;
}

export interface PathProposalDto {
  proposalId: string;
  goalId: string;
  subjectId: string;
  goalTitle: string;
  startingBaselineSummary: string;
  confirmedSkills: string[];
  developingSkills: string[];
  unknownSkills: string[];
  milestones: ProposedMilestoneDto[];
  initialMission?: ProposedMissionDto | null;
  alternativeOption?: ProposedMissionDto | null;
  feasibilityStatus: 'Feasible' | 'TightBudget' | 'InsufficientTime' | string;
  feasibilityMessage?: string | null;
  estimatedWeeks: number;
  expiresAt: string;
  status: string;
}

export interface GeneratePathProposalRequest {
  goalId: string;
  subjectId: string;
  classId?: string;
}

export interface AcceptPathProposalResponse {
  learningPathId: string;
  version: number;
  status: string;
  message: string;
  firstMission?: ProposedMissionDto | null;
}

export interface SkillCapabilityStatusDto {
  skillId: string;
  skillName: string;
  topicName: string;
  description: string;
  state: 'Unknown' | 'Developing' | 'Demonstrated' | 'Confirmed' | 'ReviewDue' | string;
  availability: 'Available' | 'PrerequisiteBlocked' | 'ContentUnavailable' | 'OutOfScope' | string;
  prerequisiteSkillIds: string[];
  evidenceCount: number;
  masteryScore?: number | null;
  requiresProbe: boolean;
  rationale: string;
}

export interface CapabilityMapDto {
  subjectId: string;
  studentId: string;
  subjectName: string;
  overallProgressRate: number;
  skills: SkillCapabilityStatusDto[];
  provenCapabilities: string[];
  areasNeedingSupport: string[];
  reviewDueSkills: string[];
  lastUpdatedAt: string;
}

export interface AgendaItemDto {
  missionId: string;
  legacySessionId?: string | null;
  title: string;
  objective: string;
  purpose: string;
  skillId: string;
  skillName: string;
  estimatedDurationMinutes: number;
  activityTypes: string[];
  recommendationReason: string;
  canStart: boolean;
  status: string;
  activeRunId?: number | null;
}

export interface LearningAgendaDto {
  learningPathId?: string | null;
  goalTitle?: string | null;
  missions: AgendaItemDto[];
  date: string;
  availableMinutes: number;
  agendaMessage: string;
  primaryMission?: AgendaItemDto | null;
  alternativeOptions: AgendaItemDto[];
  ongoingRuns: AgendaItemDto[];
  reviewDueTasks: AgendaItemDto[];
}

export interface ActivityContentDto {
  contentVersionId: string;
  skillId: string;
  title: string;
  description?: string | null;
  activityType: string;
  estimatedDurationMinutes: number;
  payload?: any;
  rubric?: any;
}

export interface ActivityRunDto {
  question?: { question: { questionId: string; questionText: string; questionType: string; reasoningRequired: boolean; hasImage?: boolean; options: { optionId: string; label: string; text: string }[] } } | null;
  activityRunId: number;
  sessionRunId: number;
  activityOrder: number;
  activityType: 'WorkedExample' | 'FindError' | 'IndependentCheck' | 'Explore' | 'TransferChallenge' | string;
  status: 'NotStarted' | 'InProgress' | 'Completed' | 'NeedsReview' | string;
  draftPayload?: string | null;
  draftRevision: number;
  assistanceRequestedCount: number;
  assistanceMode: string;
  score?: number | null;
  maxScore?: number | null;
  isMastered?: boolean | null;
  feedback?: string | null;
  startedAt: string;
  completedAt?: string | null;
  content?: ActivityContentDto | null;
}

export interface SaveActivityDraftRequest {
  draftPayload: string;
  expectedRevision: number;
}

export interface SaveActivityDraftResponse {
  activityRunId: number;
  draftRevision: number;
  savedAt: string;
  hasConflict: boolean;
}

export interface RequestAssistanceRequest {
  stepIndex: number;
  studentQuery?: string;
  questionId?: number;
}

export interface AssistanceResponseDto {
  assistanceMode: 'Normal' | 'Blocked' | 'HintOnly' | string;
  explanation: string;
  hint?: string | null;
  fallbackUsed: boolean;
  isBlocked: boolean;
  policyNotice: string;
}

export interface SubmitActivityRequest {
  clientSubmissionId: string;
  questionId?: number | string;
  finalAnswer: string;
  reasoningText?: string;
  answerDisplayLatex?: string;
  timeSpentSeconds?: number;
  drawingUploadToken?: string;
  selectedErrorStep?: string;
  misconceptionReason?: string;
}

export interface MissionRunDto {
  sessionRunId: number;
  sessionId: string;
  title: string;
  status: string;
  activities: ActivityRunDto[];
}

export interface ActivitySubmissionResponseDto {
  activityRunId: number;
  attemptId?: number | null;
  isCorrect?: boolean | null;
  awardedScore?: number | null;
  activityStatus: string;
  outcomeFeedback: string;
  nextActivityOrder: number;
  isRunFinished: boolean;
}

export interface V4SessionEvaluationResultDto {
  sessionRunId: number;
  sessionId: string;
  outcome: 'Passed' | 'NeedsSupport' | 'NeedsReview' | string;
  totalActivities: number;
  completedActivities: number;
  provenSkills: string[];
  skillsNeedingSupport: string[];
  unmeasuredSkills: string[];
  decisionReasons: string[];
  nextActions: ProposedMissionDto[];
  hasPendingReview: boolean;
  evaluatedAt: string;
}
