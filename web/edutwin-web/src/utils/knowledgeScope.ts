import type { GraphScopeQuery, KnowledgeGraphDto } from "../types/knowledgeGraph";

export function readKnowledgeScope(params: URLSearchParams): GraphScopeQuery {
  const rawGrade = params.get("gradeLevel");
  const grade = rawGrade ? Number(rawGrade) : undefined;
  return {
    // Preserve invalid explicit scope for server validation; never broaden it to the whole subject.
    gradeLevel: grade === undefined ? undefined : Number.isFinite(grade) ? grade : -1,
    graphVersionId: params.get("graphVersionId") || undefined,
    curriculumId: params.get("curriculumId") || undefined,
    classId: params.get("classId") || undefined,
    academicYear: params.get("academicYear") || undefined,
    history: params.get("history") === "true",
  };
}

export function knowledgeScopeSearch(subjectId: string, scope: GraphScopeQuery): string {
  const params = new URLSearchParams({ subjectId });
  for (const [key, value] of Object.entries(scope)) {
    if (value !== undefined && value !== null && value !== "" && value !== false) params.set(key, String(value));
  }
  return params.toString();
}

export const knowledgeKeys = {
  all: ["knowledge"] as const,
  nodes: (center: string | undefined, actor: string | undefined, subject: string, scope: GraphScopeQuery) =>
    ["knowledge", "nodes", center, actor, subject, scope] as const,
  graph: (center: string | undefined, actor: string | undefined, subject: string, scope: GraphScopeQuery) =>
    ["knowledge", "graph", center, actor, subject, scope] as const,
  versions: (center: string | undefined, actor: string | undefined, subject: string, grade?: number) =>
    ["knowledge", "versions", center, actor, subject, grade] as const,
};

export function incompatibleKnowledgeNodes(selected: string[], available: { nodeId: string }[]): string[] {
  const allowed = new Set(available.map(node => node.nodeId));
  return selected.filter(id => !allowed.has(id));
}

interface CurriculumComposition {
  gradeLevel?: number | null;
  graphVersionId?: string | null;
  nodeIds: readonly string[];
}

/** Unsaved structure opens the selected library; curriculum scope always refers to the persisted draft. */
export function curriculumGraphLinkScope(curriculumId: string | undefined, current: CurriculumComposition, saved?: CurriculumComposition): GraphScopeQuery {
  const matchesSaved = saved && current.gradeLevel === saved.gradeLevel &&
    (current.graphVersionId || "") === (saved.graphVersionId || "") && current.nodeIds.join(",") === saved.nodeIds.join(",");
  return { gradeLevel: current.gradeLevel ?? undefined, graphVersionId: current.graphVersionId || undefined,
    curriculumId: matchesSaved ? curriculumId : undefined };
}

/** Occurrences remain separate in class unions; mastery and mutations still use canonical IDs. */
export function graphOccurrences(graph: KnowledgeGraphDto | null | undefined) {
  const nodes = (graph?.nodes ?? []).flatMap(node => node.provenance?.length
    ? node.provenance.map(source => ({ ...node, canonicalNodeId: node.nodeId, nodeId: source.occurrenceId,
      occurrenceId: source.occurrenceId, graphVersionId: source.graphVersionId, gradeLevel: source.gradeLevel,
      curriculumIds: source.curriculumId ? [source.curriculumId] : [],
      sources: source.curriculumId ? [{ curriculumId: source.curriculumId, curriculumTitle: source.curriculumTitle || undefined,
        graphVersionId: source.graphVersionId, gradeLevel: source.gradeLevel, applicationId: source.applicationId,
        applicationRole: source.applicationRole || undefined, isGradeException: source.isGradeException,
        gradeMismatchReason: source.gradeMismatchReason }] : [],
      parentNodeId: source.parentNodeId ? (graph?.nodes ?? []).flatMap(n => n.provenance ?? [])
        .find(p => p.nodeId === source.parentNodeId && p.graphVersionId === source.graphVersionId && p.curriculumId === source.curriculumId && p.applicationId === source.applicationId)?.occurrenceId ?? null : null }))
    : [{ ...node, canonicalNodeId: node.nodeId, nodeId: node.occurrenceId || node.nodeId,
      parentNodeId: node.parentOccurrenceId ?? node.parentNodeId }]);
  const edges = (graph?.edges ?? []).map(edge => ({ ...edge, canonicalEdgeId: edge.edgeId,
    edgeId: edge.occurrenceId || edge.edgeId,
    sourceNodeId: edge.sourceOccurrenceId || edge.sourceNodeId,
    targetNodeId: edge.targetOccurrenceId || edge.targetNodeId }));
  const ids = new Set(nodes.map(node => node.nodeId));
  return { nodes, edges: edges.filter(edge => ids.has(edge.sourceNodeId) && ids.has(edge.targetNodeId)) };
}
