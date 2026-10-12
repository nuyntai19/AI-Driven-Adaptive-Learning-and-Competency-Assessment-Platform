export type QuestionType = "MultipleChoice" | "ShortAnswer" | "Essay";
export type QuestionStatus = "Draft" | "Active" | "Archived";
export type MaterialVisibility = "Private" | "Shared";
export interface RubricCriterion { criterionId: string; title: string; description: string; maxScore: number; visualRequirements?: string[]; }
export type QuestionAnswerEvaluationMode = "TextExact" | "NumericRational" | "Coordinate2D" | "MathEquivalent" | "Manual";

export interface QuestionOption {
  optionId: string;
  label?: string;
  optionLabel?: string;
  text?: string;
  optionText?: string;
  isCorrect: boolean;
  orderIndex: number;
  misconception?: string | null;
}

export interface KnowledgeMapping {
  nodeId: string;
  mappingRole: "Primary" | "Secondary";
}

export interface GradingCriteria {
  schemaVersion: string;
  requiredIdeas: string[];
  commonErrors: string[];
  scoringNotes: string;
  criteria?: RubricCriterion[];
}

export interface Question {
  hasImage?: boolean;
  questionId: string;
  subjectId: string;
  primaryTopicNodeId: string;
  questionType: QuestionType;
  difficulty: number;
  questionText: string;
  correctAnswer?: string;
  solution?: string;
  expectedReasoning?: string;
  gradingCriteria?: GradingCriteria;
  maxScore: number;
  estimatedTimeSeconds: number;
  reasoningRequired: boolean;
  languageCode: string;
  status: QuestionStatus;
  gradeLevel?: number | null;
  answerEvaluationMode?: QuestionAnswerEvaluationMode;
  options?: QuestionOption[];
  knowledgeMappings: KnowledgeMapping[];
  createdByTeacherId: string;
  visibility?: MaterialVisibility;
  rowVersion: string;
}

export interface CreateQuestionRequest {
  imageDataUrl?: string;
  copyImageFromQuestionId?: string;
  visibility?: MaterialVisibility;
  teacherId?: string | null;
  subjectId: string;
  gradeLevel?: number | null;
  primaryTopicNodeId: string;
  questionType: QuestionType;
  difficulty: number;
  questionText: string;
  correctAnswer?: string;
  solution?: string;
  expectedReasoning?: string;
  gradingCriteria?: GradingCriteria;
  maxScore: number;
  estimatedTimeSeconds: number;
  reasoningRequired: boolean;
  languageCode: string;
  answerEvaluationMode?: QuestionAnswerEvaluationMode;
  options?: { optionLabel: string; optionText: string; isCorrect: boolean; orderIndex: number; misconception?: string | null }[];
  knowledgeMappings?: KnowledgeMapping[];
}

export interface UpdateQuestionRequest {
  imageDataUrl?: string;
  removeImage?: boolean;
  visibility?: MaterialVisibility;
  primaryTopicNodeId: string;
  gradeLevel?: number | null;
  questionType: QuestionType;
  difficulty: number;
  questionText: string;
  correctAnswer?: string;
  solution?: string;
  expectedReasoning?: string;
  gradingCriteria?: GradingCriteria;
  maxScore: number;
  estimatedTimeSeconds: number;
  reasoningRequired: boolean;
  languageCode: string;
  answerEvaluationMode?: QuestionAnswerEvaluationMode;
  options?: { optionLabel: string; optionText: string; isCorrect: boolean; orderIndex: number; misconception?: string | null }[];
  knowledgeMappings?: KnowledgeMapping[];
  rowVersion: string;
}

export interface ActivateQuestionRequest {
  rowVersion: string;
}

export interface ArchiveQuestionRequest {
  rowVersion: string;
}

export interface QuestionFilter {
  visibility?: MaterialVisibility;
  ownedOnly?: boolean;
  subjectId?: string;
  gradeLevel?: number;
  topicId?: string;
  type?: QuestionType;
  difficulty?: number;
  status?: QuestionStatus;
  page?: number;
  pageSize?: number;
}

export interface QuestionImportOptionInput {
  optionLabel: string;
  optionText: string;
  isCorrect: boolean;
  orderIndex: number;
  misconception?: string | null;
}

export interface QuestionImportItemDto {
  rowIndex: number;
  questionType: QuestionType;
  difficulty: number;
  questionText: string;
  correctAnswer: string;
  solution: string;
  expectedReasoning?: string | null;
  maxScore: number;
  estimatedTimeSeconds: number;
  reasoningRequired: boolean;
  answerEvaluationMode?: QuestionAnswerEvaluationMode;
  options: QuestionImportOptionInput[];
  requiredIdeas: string[];
  commonErrors: string[];
}

export interface QuestionImportRowErrorDto {
  rowIndex: number;
  field: string;
  errorMessage: string;
  rawValue?: string | null;
}

export interface QuestionImportPreviewDataDto {
  previewToken: string;
  totalRows: number;
  validCount: number;
  invalidCount: number;
  validQuestions: QuestionImportItemDto[];
  errors: QuestionImportRowErrorDto[];
}

export interface QuestionImportPreviewResponse {
  data: QuestionImportPreviewDataDto;
  meta: { traceId: string; timestamp: string };
}

export interface QuestionImportConfirmRequest {
  previewToken: string;
  subjectId: string;
  primaryTopicNodeId: number | string;
}

export interface QuestionImportConfirmDataDto {
  importedCount: number;
  message: string;
}

export interface QuestionImportConfirmResponse {
  data: QuestionImportConfirmDataDto;
  meta: { traceId: string; timestamp: string };
}
