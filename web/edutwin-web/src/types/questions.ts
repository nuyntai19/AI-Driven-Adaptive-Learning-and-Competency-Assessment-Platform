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
  options?: { optionLabel: string; optionText: string; isCorrect: boolean; orderIndex: number }[];
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
  options?: { optionLabel: string; optionText: string; isCorrect: boolean; orderIndex: number }[];
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
