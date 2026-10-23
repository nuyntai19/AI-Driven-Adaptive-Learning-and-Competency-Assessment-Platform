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
  gradeLevels?: number[];
  occurrenceId?: string;
  canonicalNodeId?: string;
  parentOccurrenceId?: string | null;
  graphVersionId?: string | null;
  gradeLevel?: number | null;
  curriculumIds?: string[];
  sources?: KnowledgeGraphSource[];
  provenance?: KnowledgeNodeProvenance[];
  nodeId: string;
  nodeType: KnowledgeNodeType;
  nodeCode: string;
  nodeName: string;
  orderIndex: number;
  examImportance: number;
  rowVersion: string;
  description: string | null;
  estimatedLearningMinutes: number;
  isActive: boolean;
  parentNodeId: string | null;
}

export interface KnowledgeGraphEdgeDto {
  sourceGraphVersionId?: string | null;
  sourceNodeName?: string | null;
  sourceGradeLevel?: number | null;
  external?: boolean;
  occurrenceId?: string;
  canonicalEdgeId?: string;
  sourceOccurrenceId?: string;
  targetOccurrenceId?: string;
  graphVersionId?: string | null;
  gradeLevel?: number | null;
  edgeId: string;
  sourceNodeId: string;
  targetNodeId: string;
  relationType: KnowledgeRelationType;
  weight: number;
  rowVersion: string;
}

export interface KnowledgeGraphDto {
  resolvedScope?: ResolvedGraphScope;
  externalPrerequisites?: KnowledgeGraphEdgeDto[];
  subjectId: string;
  nodes: KnowledgeGraphNodeDto[];
  edges: KnowledgeGraphEdgeDto[];
}

export interface KnowledgeGraphResponse {
  data: KnowledgeGraphDto;
  meta: Meta;
}

export interface KnowledgeNodeDto {
  gradeLevels?: number[];
  provenance?: KnowledgeNodeProvenance[];
  graphVersionId?: string | null;
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

export interface KnowledgeNodeListResponse {
  data: KnowledgeNodeDto[];
  meta: Meta;
}

export interface CreateKnowledgeNodeRequest {
  graphVersionId?: string;
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
  graphVersionId?: string;
  subjectId: string;
  sourceNodeId: string;
  targetNodeId: string;
  relationType: KnowledgeRelationType;
  weight: number;
}

export interface UpdateKnowledgeNodeRequest {
  graphVersionId?: string;
  parentNodeId?: string | null;
  nodeName: string;
  description?: string | null;
  orderIndex: number;
  examImportance: number;
  estimatedLearningMinutes: number;
  isActive: boolean;
  rowVersion: string;
}

export interface UpdateKnowledgeEdgeRequest {
  graphVersionId?: string;
  weight: number;
  rowVersion: string;
}

export interface GraphScopeQuery {
  gradeLevel?: number;
  graphVersionId?: string;
  curriculumId?: string;
  classId?: string;
  academicYear?: string;
  history?: boolean;
}

export interface KnowledgeGraphSource {
  curriculumId: string;
  curriculumTitle?: string;
  graphVersionId?: string;
  gradeLevel?: number | null;
  applicationRole?: string;
  applicationId?: string | null;
  isGradeException?: boolean;
  gradeMismatchReason?: string | null;
}

export interface ResolvedGraphScope extends GraphScopeQuery {
  subjectId?: string;
  kind?: string;
  className?: string;
  curriculumTitle?: string;
  graphVersionTitle?: string;
  emptyReason?: string | null;
  message?: string | null;
  isLegacy?: boolean;
  readOnly?: boolean;
}

export interface KnowledgeGraphVersion {
  graphVersionId: string;
  subjectId: string;
  gradeLevel: number;
  title: string;
  status: "Draft" | "Published" | "Archived";
  isCurrent: boolean;
  rowVersion: string;
  isLegacy?: boolean;
  sourceGraphVersionId?: string | null;
  nodeCount?: number;
  edgeCount?: number;
  nodeMappings?: KnowledgeVersionNodeMapping[];
  edgeMappings?: KnowledgeVersionEdgeMapping[];
}

export interface KnowledgeNodeProvenance {
  occurrenceId: string;
  nodeId: string;
  graphVersionId: string;
  gradeLevel: number;
  curriculumId?: string | null;
  curriculumTitle?: string | null;
  applicationId?: string | null;
  applicationRole?: string | null;
  isGradeException?: boolean;
  gradeMismatchReason?: string | null;
  parentNodeId?: string | null;
}
export interface KnowledgeVersionNodeMapping { nodeId: string; parentNodeId?: string | null }
export interface KnowledgeVersionEdgeMapping {
  edgeId: string;
  relationType: KnowledgeRelationType;
  weight: number;
  sourceGraphVersionId?: string | null;
}
