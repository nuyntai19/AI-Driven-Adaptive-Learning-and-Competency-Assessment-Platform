import type { Meta } from "./auth";

export type KnowledgeNodeType =
  | "Subject"
  | "Chapter"
  | "Topic"
  | "Skill"
  | "Concept";

export type KnowledgeRelationType =
  | "PrerequisiteOf"
  | "RelatedTo"
  | "PartOf"
  | "CausesErrorIn";

export interface KnowledgeGraphNodeDto {
  nodeId: string;
  nodeType: KnowledgeNodeType;
  nodeCode: string;
  nodeName: string;
  orderIndex: number;
  examImportance: number;
}

export interface KnowledgeGraphEdgeDto {
  edgeId: string;
  sourceNodeId: string;
  targetNodeId: string;
  relationType: KnowledgeRelationType;
  weight: number;
}

export interface KnowledgeGraphDto {
  subjectId: string;
  nodes: KnowledgeGraphNodeDto[];
  edges: KnowledgeGraphEdgeDto[];
}

export interface KnowledgeGraphResponse {
  data: KnowledgeGraphDto;
  meta: Meta;
}

export interface KnowledgeNodeDto {
  nodeId: string;
  subjectId: string;
  parentNodeId: string | null;
  nodeType: KnowledgeNodeType;
  nodeCode: string;
  nodeName: string;
  description: string | null;
  orderIndex: number;
  examImportance: number;
  estimatedLearningMinutes: number;
  isActive: boolean;
  rowVersion: string;
}

export interface KnowledgeNodeResponse {
  data: KnowledgeNodeDto;
  meta: Meta;
}

export interface CreateKnowledgeNodeRequest {
  subjectId: string;
  parentNodeId: string | null;
  nodeType: KnowledgeNodeType;
  nodeCode: string;
  nodeName: string;
  description?: string | null;
  orderIndex: number;
  examImportance: number;
  estimatedLearningMinutes: number;
  isActive: boolean;
}

export interface KnowledgeEdgeDto {
  edgeId: string;
  subjectId: string;
  sourceNodeId: string;
  targetNodeId: string;
  relationType: KnowledgeRelationType;
  weight: number;
  rowVersion: string;
}

export interface KnowledgeEdgeResponse {
  data: KnowledgeEdgeDto;
  meta: Meta;
}

export interface CreateKnowledgeEdgeRequest {
  subjectId: string;
  sourceNodeId: string;
  targetNodeId: string;
  relationType: KnowledgeRelationType;
  weight: number;
}
