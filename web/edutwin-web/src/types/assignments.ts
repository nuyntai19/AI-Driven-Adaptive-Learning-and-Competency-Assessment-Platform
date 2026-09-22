export type AssignmentStatus = 'Draft' | 'Published' | 'Closed' | 'Archived';
export type TargetMode = 'WholeClass' | 'SelectedStudents';
export type TargetSource = TargetMode | 'GapGroup';
export type ProgressStatus = 'NotStarted' | 'InProgress' | 'Completed' | 'Overdue';
export type AttemptStatus = 'PendingAnalysis' | 'Processing' | 'Completed' | 'NeedsTeacherReview' | 'AnalysisFailed';

export type TeacherFinalReviewStatus = 'Pending' | 'Approved';
export type AssignmentResultStatus = 'Processing' | 'Provisional' | 'Final';

export interface AssignmentResultSummaryDto {
  totalQuestionCount: number;
  answeredQuestionCount: number;
  evaluatedQuestionCount: number;
  correctQuestionCount: number;
  incorrectQuestionCount: number;
  pendingQuestionCount: number;
  resultStatus: AssignmentResultStatus;
  teacherFinalReviewStatus: TeacherFinalReviewStatus;
  internalAwardedScore?: number | null;
  internalMaxScore: number;
  overallAiComment: string | null;
  overallAiCommentGeneratedAt?: string | null;
  finalTeacherNote?: string | null;
  finalReviewedByName?: string | null;
  finalReviewedAt?: string | null;
}

export interface AssignmentQuestionDto {
  questionId: string;
  orderIndex: number;
  points: number;
}

export interface AssignmentTargetDto {
  studentId: string;
  targetSource: TargetSource;
}

export interface AssignmentDto {
  assignmentId: string;
  classId: string;
  title: string;
  instructions: string | null;
  dueAt: string | null;
  status: AssignmentStatus;
  questionCount: number;
  targetStudentCount: number;
  questions: AssignmentQuestionDto[];
  targets: AssignmentTargetDto[];
  rowVersion: string;
}

export interface CreateAssignmentRequest {
  classId: string;
  title: string;
  instructions?: string | null;
  dueAt?: string | null;
  questionIds: string[];
  targetMode: TargetMode;
  studentIds?: string[];
}

export interface UpdateAssignmentRequest {
  title: string;
  instructions?: string | null;
  dueAt?: string | null;
  questionIds: string[];
  targetMode: TargetMode;
  studentIds?: string[];
  rowVersion: string;
}

export interface PublishAssignmentRequest {
  rowVersion: string;
}

export interface CloseAssignmentRequest {
  rowVersion: string;
}

export interface StudentProgressDto {
  status: ProgressStatus;
  completedQuestionCount: number;
  totalQuestionCount: number;
}

export interface StudentAssignmentQuestionOptionDto {
  optionId: string;
  label: string;
  text: string;
}

export interface StudentQuestionAttemptDto {
  attemptId: string | number;
  status: AttemptStatus;
  finalAnswer: string | null;
  reasoningText: string | null;
  confidence: number | null;
  timeSpentSeconds: number;
  answerChanges: number;
  skipped: boolean;
  submittedAt: string;
  isCorrect?: boolean | null;
  awardedScore?: number | null;
  maxScore?: number | null;
}

export interface StudentAssignmentQuestionDto {
  questionId: string;
  questionType: string;
  difficulty: number;
  questionText: string;
  estimatedTimeSeconds: number;
  reasoningRequired: boolean;
  languageCode: string;
  answerEvaluationMode?: string;
  options?: StudentAssignmentQuestionOptionDto[];
  attemptStatus: AttemptStatus | null;
  latestAttempt?: StudentQuestionAttemptDto | null;
  submittedAnswer?: string | null;
  submittedReasoning?: string | null;
  submittedAttemptId?: string | number | null;
  hasAttachment?: boolean;
  effectiveIsCorrect?: boolean | null;
}

export interface StudentAssignmentDetailDto {
  assignmentId: string;
  title: string;
  instructions: string | null;
  dueAt: string | null;
  subjectId?: string | null;
  subjectName?: string | null;
  timeLimitMinutes?: number | null;
  startedAt?: string | null;
  effectiveExpiresAt?: string | null;
  remainingSeconds?: number | null;
  canRetake?: boolean;
  progress: StudentProgressDto;
  questions: StudentAssignmentQuestionDto[];
  summary?: AssignmentResultSummaryDto | null;
}

export interface StudentAssignmentListItemDto {
  assignmentId: string;
  title: string;
  instructions: string | null;
  dueAt: string | null;
  subjectId?: string | null;
  subjectName?: string | null;
  progress: StudentProgressDto;
  summary?: AssignmentResultSummaryDto | null;
}

export interface AssignmentProgressItemDto {
  studentId: string;
  fullName: string;
  status: ProgressStatus;
  completedQuestionCount: number;
  totalQuestionCount: number;
}
