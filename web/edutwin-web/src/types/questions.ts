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
  optionMisconceptions?: Record<string, string>;
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
  questionKey: string;
  sheetName: string;
  rowIndex: number;
  topicCode: string;
  topicName: string;
  primaryTopicNodeId: string;
  gradeLevel: number;
  visibility: MaterialVisibility;
  languageCode: string;
  scoringNotes: string;
  defaultedFields: string[];
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
  code: string;
  sheetName: string;
  columnName?: string | null;
  questionKey?: string | null;
  rowIndex: number;
  field: string;
  errorMessage: string;
  rawValue?: string | null;
}

export interface QuestionImportPreviewDataDto {
  previewToken: string;
  revision: number;
  subjectId: string;
  expiresAt: string;
  templateVersion: string;
  errorCount: number;
  warningCount: number;
  warnings: QuestionImportRowErrorDto[];
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
  revision: number;
  selectedQuestionKeys: string[];
  importMode: "AllValid" | "SelectedValid";
  targetStatus: "Draft";
}

export interface QuestionImportPreviewRequest {
  subjectId: string;
  teacherId?: string;
  templateVersion: "1.0";
  legacyMode: boolean;
  defaultTopicNodeId?: string;
  gradeLevel?: number;
  difficulty: number;
  maxScore: number;
  estimatedTimeSeconds: number;
  reasoningRequired: boolean;
  visibility: MaterialVisibility;
  languageCode: string;
  shortAnswerEvaluationMode: QuestionAnswerEvaluationMode;
}

export interface QuestionImportConfirmDataDto {
  importedCount: number;
  skippedCount: number;
  questionIds: string[];
  message: string;
}

export interface QuestionImportConfirmResponse {
  data: QuestionImportConfirmDataDto;
  meta: { traceId: string; timestamp: string };
}
