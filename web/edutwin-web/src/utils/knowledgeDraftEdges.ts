import type { KnowledgeGraphEdgeDto, KnowledgeVersionEdgeMapping, KnowledgeVersionNodeMapping } from "../types/knowledgeGraph";

/**
 * Draft relations that cannot be saved: unknown in the subject library, target outside the draft,
 * source outside the draft without a published source framework, or a cross-framework link that is
 * not a prerequisite.
 *
 * Returns `null` while the library is not loaded: validity is then unknown, not invalid. Treating an
 * empty, still-loading library as "every relation is missing" raised a false error on open.
 * While a loaded library is being refreshed (`refreshing`), a relation it does not know yet (e.g. the
 * reverse prerequisite just created) is also unknown rather than missing; other checks still apply.
 */
export function invalidDraftEdges(
  edges: KnowledgeVersionEdgeMapping[],
  nodes: KnowledgeVersionNodeMapping[],
  catalogEdges: KnowledgeGraphEdgeDto[] | null | undefined,
  refreshing = false,
): KnowledgeVersionEdgeMapping[] | null {
  if (!catalogEdges) return null;
  const byId = new Map(catalogEdges.map(edge => [edge.edgeId, edge]));
  const inDraft = new Set(nodes.map(node => node.nodeId));
  return edges.filter(mapping => {
    const edge = byId.get(mapping.edgeId);
    if (!edge) return !refreshing;
    return !inDraft.has(edge.targetNodeId) || (!inDraft.has(edge.sourceNodeId) && !mapping.sourceGraphVersionId) ||
      (!!mapping.sourceGraphVersionId && mapping.relationType !== "PrerequisiteOf");
  });
}

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
