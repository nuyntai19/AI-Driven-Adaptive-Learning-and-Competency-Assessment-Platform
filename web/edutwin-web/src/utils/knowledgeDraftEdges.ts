import type { KnowledgeGraphEdgeDto, KnowledgeVersionEdgeMapping } from "../types/knowledgeGraph";

type EdgeEndpoints = Pick<KnowledgeGraphEdgeDto, "sourceNodeId" | "targetNodeId">;

export function findReversePrerequisite(edges: KnowledgeGraphEdgeDto[], edge: EdgeEndpoints) {
  return edges.find(candidate => candidate.relationType === "PrerequisiteOf" &&
    candidate.sourceNodeId === edge.targetNodeId && candidate.targetNodeId === edge.sourceNodeId);
}

export function replaceDraftPrerequisite(
  mappings: KnowledgeVersionEdgeMapping[], originalEdgeId: string, reverseEdgeId: string, weight: number,
): KnowledgeVersionEdgeMapping[] {
  const remaining = mappings.filter(mapping => mapping.edgeId !== originalEdgeId && mapping.edgeId !== reverseEdgeId);
  return [...remaining, { edgeId: reverseEdgeId, relationType: "PrerequisiteOf", weight }];
}
