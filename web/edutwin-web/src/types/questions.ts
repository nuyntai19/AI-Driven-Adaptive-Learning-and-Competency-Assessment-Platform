export type QuestionType = "MultipleChoice" | "ShortAnswer" | "Essay";
export type QuestionStatus = "Draft" | "Active" | "Archived";

export interface QuestionOption {
  optionId: string;
  optionLabel: string;
  optionText: string;
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
  options?: QuestionOption[];
  knowledgeMappings: KnowledgeMapping[];
  rowVersion: string;
}

export interface CreateQuestionRequest {
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
  options?: { optionLabel: string; optionText: string; isCorrect: boolean; orderIndex: number }[];
  knowledgeMappings?: KnowledgeMapping[];
}

export interface UpdateQuestionRequest extends CreateQuestionRequest {
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
}
