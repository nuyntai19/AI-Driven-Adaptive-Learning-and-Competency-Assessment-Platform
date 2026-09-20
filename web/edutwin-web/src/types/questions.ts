export type QuestionType = "MultipleChoice" | "ShortAnswer" | "Essay";
export type QuestionStatus = "Draft" | "Active" | "Archived";
export type QuestionAnswerEvaluationMode = "TextExact" | "NumericRational" | "Manual";

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
}

export interface Question {
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
  answerEvaluationMode?: QuestionAnswerEvaluationMode;
  options?: QuestionOption[];
  knowledgeMappings: KnowledgeMapping[];
  createdByTeacherId: string;
  rowVersion: string;
}

export interface CreateQuestionRequest {
  teacherId?: string | null;
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
  answerEvaluationMode?: QuestionAnswerEvaluationMode;
  options?: { optionLabel: string; optionText: string; isCorrect: boolean; orderIndex: number; misconception?: string | null }[];
  knowledgeMappings?: KnowledgeMapping[];
}

export interface UpdateQuestionRequest {
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
  rowVersion: string;
}

export interface ActivateQuestionRequest {
  rowVersion: string;
}

export interface ArchiveQuestionRequest {
  rowVersion: string;
}

export interface QuestionFilter {
  subjectId?: string;
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
  questions?: QuestionImportItemDto[];
}

export interface QuestionImportConfirmDataDto {
  importedCount: number;
  message: string;
}

export interface QuestionImportConfirmResponse {
  data: QuestionImportConfirmDataDto;
  meta: { traceId: string; timestamp: string };
}
