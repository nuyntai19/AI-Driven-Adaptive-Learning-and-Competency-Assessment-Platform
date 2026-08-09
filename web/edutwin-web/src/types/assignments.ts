export type AssignmentStatus = 'Draft' | 'Published' | 'Closed' | 'Archived';
export type TargetMode = 'WholeClass' | 'SelectedStudents';
export type TargetSource = TargetMode | 'GapGroup';
export type ProgressStatus = 'NotStarted' | 'InProgress' | 'Completed' | 'Overdue';
export type AttemptStatus = 'PendingAnalysis' | 'Processing' | 'Completed' | 'NeedsTeacherReview';

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

export interface StudentAssignmentQuestionDto {
  questionId: string;
  questionType: string;
  difficulty: number;
  questionText: string;
  estimatedTimeSeconds: number;
  reasoningRequired: boolean;
  languageCode: string;
  options?: StudentAssignmentQuestionOptionDto[];
  attemptStatus: AttemptStatus | null;
}

export interface StudentAssignmentDetailDto {
  assignmentId: string;
  title: string;
  instructions: string | null;
  dueAt: string | null;
  progress: StudentProgressDto;
  questions: StudentAssignmentQuestionDto[];
}

export interface StudentAssignmentListItemDto {
  assignmentId: string;
  title: string;
  instructions: string | null;
  dueAt: string | null;
  progress: StudentProgressDto;
}

export interface AssignmentProgressItemDto {
  studentId: string;
  fullName: string;
  status: ProgressStatus;
  completedQuestionCount: number;
  totalQuestionCount: number;
}
