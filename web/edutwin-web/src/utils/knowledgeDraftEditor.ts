import type { KnowledgeGraphDto, KnowledgeGraphEdgeDto, KnowledgeGraphNodeDto, KnowledgeGraphVersion, KnowledgeVersionEdgeMapping, KnowledgeVersionNodeMapping } from "../types/knowledgeGraph";
import { relationInfo } from "./knowledgeRelations.ts";

export interface LibraryFilter {
  grade: number | "all" | "unknown";
  status: KnowledgeGraphVersion["status"] | "all";
  source: string;
  membership: "all" | "included" | "available";
  search: string;
  legacy: boolean;
}
export const versionMatches = (v: KnowledgeGraphVersion, f: Pick<LibraryFilter, "grade" | "status" | "legacy">) =>
  (f.grade === "all" || v.gradeLevel === f.grade) && (f.status === "all" ? v.status !== "Archived" : v.status === f.status) && (f.legacy || !v.isLegacy);

/** Membership, never the spelling of a code, determines the source grade/status. */
export function filterLibraryTopics(topics: KnowledgeGraphNodeDto[], versions: KnowledgeGraphVersion[], selectedIds: ReadonlySet<string>, filter: LibraryFilter, curriculumIds?: readonly string[]) {
  const candidates = versions.filter(v => versionMatches(v, filter) && (!filter.source || v.graphVersionId === filter.source));
  const matching = new Set(candidates.flatMap(v => v.nodeMappings?.map(n => n.nodeId) ?? []));
  const classified = new Set(versions.filter(v => !v.isLegacy).flatMap(v => v.nodeMappings?.map(n => n.nodeId) ?? []));
  const query = filter.search.trim().toLocaleLowerCase("vi");
  return topics.filter(n => {
    const unknown = !classified.has(n.nodeId) && !n.gradeLevels?.length;
    const sourceMatch = curriculumIds ? curriculumIds.includes(n.nodeId) : filter.grade === "unknown" ? unknown
      : matching.has(n.nodeId) || (!filter.source && filter.status === "all" && (filter.grade === "all" || n.gradeLevels?.includes(filter.grade)));
    return sourceMatch && (filter.membership === "all" || selectedIds.has(n.nodeId) === (filter.membership === "included")) &&
      (!query || `${n.nodeName} ${n.nodeCode}`.toLocaleLowerCase("vi").includes(query));
  });
}

export function effectiveDraftEdges(mappings: KnowledgeVersionEdgeMapping[], catalog: KnowledgeGraphEdgeDto[]) {
  const byId = new Map(catalog.map(e => [e.edgeId, e]));
  return mappings.flatMap(mapping => { const e = byId.get(mapping.edgeId); return e ? [{ ...e, ...mapping }] : []; });
}

/** Only a view projection. Incomplete mappings stay in editor state and in the save payload. */
export function draftPreview(catalog: KnowledgeGraphDto | undefined, nodes: KnowledgeVersionNodeMapping[], edges: KnowledgeVersionEdgeMapping[], grade: number) {
  const selected = new Map(nodes.map(n => [n.nodeId, n]));
  const viewNodes: KnowledgeGraphNodeDto[] = (catalog?.nodes ?? []).filter(n => selected.has(n.nodeId)).map(n => ({ ...n, parentNodeId: selected.get(n.nodeId)?.parentNodeId ?? null, gradeLevel: grade }));
  const inView = new Set(viewNodes.map(n => n.nodeId));
  const viewEdges: KnowledgeGraphEdgeDto[] = [];
  for (const e of effectiveDraftEdges(edges, catalog?.edges ?? [])) {
    if (!inView.has(e.targetNodeId)) continue;
    if (e.sourceGraphVersionId && !inView.has(e.sourceNodeId)) {
      const source = catalog?.nodes.find(n => n.nodeId === e.sourceNodeId);
      if (!source) continue;
      const contextId = `external:${e.sourceGraphVersionId}:${source.nodeId}`;
      if (!inView.has(contextId)) {
        viewNodes.push({ ...source, nodeId: contextId, canonicalNodeId: source.nodeId, nodeName: `${source.nodeName} · Tiên quyết ngoài khung`, parentNodeId: null, gradeLevel: undefined });
        inView.add(contextId);
      }
      viewEdges.push({ ...e, sourceNodeId: contextId, external: true });
    } else if (inView.has(e.sourceNodeId)) viewEdges.push(e);
  }
  return { nodes: viewNodes, edges: viewEdges };
}

export interface DraftIssue { id: string; kind: "parent" | "edge" | "node"; message: string; remedy: string; nodeIds: string[]; edgeIds: string[]; fullPath?: string }

/** Back edges identify concrete cycles; unrelated prefixes are never blamed. */
function cycles(adjacency: Map<string, { to: string; edge?: string }[]>) {
  const done = new Set<string>();
  const found: { nodes: string[]; edges: string[] }[] = [];
  for (const start of adjacency.keys()) {
    if (done.has(start)) continue;
    const stack = [{ id: start, next: 0, via: "" }];
    const active = new Map<string, number>([[start, 0]]);
    while (stack.length) {
      const frame = stack[stack.length - 1];
      const links = adjacency.get(frame.id) ?? [];
      if (frame.next >= links.length) { active.delete(frame.id); done.add(frame.id); stack.pop(); continue; }
      const link = links[frame.next++];
      const at = active.get(link.to);
      if (at !== undefined) found.push({ nodes: [...stack.slice(at).map(f => f.id), link.to], edges: [...stack.slice(at + 1).map(f => f.via), link.edge ?? ""].filter(Boolean) });
      else if (!done.has(link.to)) { active.set(link.to, stack.length); stack.push({ id: link.to, next: 0, via: link.edge ?? "" }); }
    }
  }
  return found;
}

/** UI diagnostics only; do not use this list as a new save/publish business gate. */
export function diagnoseDraft(nodes: KnowledgeVersionNodeMapping[], mappings: KnowledgeVersionEdgeMapping[], catalog: KnowledgeGraphDto | undefined, versions: KnowledgeGraphVersion[], grade: number, refreshing = false, versionsReady = true): DraftIssue[] {
  if (!catalog) return [];
  const issues: DraftIssue[] = [];
  const byNode = new Map(catalog.nodes.map(n => [n.nodeId, n]));
  const name = (id: string) => { const n = byNode.get(id); return n ? `${n.nodeName} (${n.nodeCode})` : `#${id}`; };
  const selected = new Set(nodes.map(n => n.nodeId));
  const parents = new Map(nodes.map(n => [n.nodeId, n.parentNodeId && selected.has(n.parentNodeId) ? [{ to: n.parentNodeId }] : []]));
  for (const n of nodes) {
    if ((!byNode.has(n.nodeId) && !refreshing) || byNode.get(n.nodeId)?.isActive === false) issues.push({ id: `node:${n.nodeId}`, kind: "node", nodeIds: [n.nodeId], edgeIds: [], message: `Chủ đề ${name(n.nodeId)} ${byNode.has(n.nodeId) ? "đã tắt hoạt động" : "không tìm thấy trong thư viện đã tải"}.`, remedy: "Kiểm tra thư viện hoặc gỡ chủ đề khỏi khung." });
    if (n.parentNodeId && !selected.has(n.parentNodeId)) issues.push({ id: `parent:${n.nodeId}`, kind: "parent", nodeIds: [n.nodeId], edgeIds: [], message: `Nhóm cha của ${name(n.nodeId)} không nằm trong khung này.`, remedy: "Chọn nhóm cha trong khung hoặc để ở cấp ngoài cùng." });
  }
  for (const c of cycles(parents)) {
    const path = c.nodes.map(name).join(" → ");
    issues.push({ id: `parent-cycle:${c.nodes.join(":")}`, kind: "parent", nodeIds: [...new Set(c.nodes)], edgeIds: [], message: `Vòng nhóm cha: ${c.nodes.length > 8 ? `${c.nodes.slice(0, 4).map(name).join(" → ")} → … → ${name(c.nodes[0])} (${c.nodes.length - 1} chủ đề)` : path}.`, fullPath: c.nodes.length > 8 ? path : undefined, remedy: "Các chủ đề đang nằm trong nhau. Đổi nhóm cha của một chủ đề hoặc đưa về cấp ngoài cùng." });
  }
  const byEdge = new Map(catalog.edges.map(e => [e.edgeId, e]));
  const dependencies = new Map<string, { to: string; edge?: string }[]>(nodes.map(n => [n.nodeId, []]));
  for (const m of mappings) {
    const e = byEdge.get(m.edgeId);
    const base = { kind: "edge" as const, edgeIds: [m.edgeId], nodeIds: e ? [e.sourceNodeId, e.targetNodeId] : [] };
    if (!e) { if (!refreshing) issues.push({ ...base, id: `edge:${m.edgeId}`, message: `Không tìm thấy quan hệ #${m.edgeId} trong thư viện đã tải.`, remedy: "Tải lại thư viện hoặc gỡ quan hệ khỏi khung." }); continue; }
    const label = `${name(e.sourceNodeId)} → ${name(e.targetNodeId)} · ${relationInfo[m.relationType].label}`;
    if (!selected.has(e.targetNodeId) || (!selected.has(e.sourceNodeId) && !m.sourceGraphVersionId)) issues.push({ ...base, id: `endpoints:${m.edgeId}`, message: `Quan hệ ${label} thiếu chủ đề ${!selected.has(e.targetNodeId) ? name(e.targetNodeId) : name(e.sourceNodeId)} trong khung.`, remedy: "Thêm lại chủ đề hoặc gỡ quan hệ khỏi khung." });
    if (m.sourceGraphVersionId) {
      const source = versions.find(v => v.graphVersionId === m.sourceGraphVersionId);
      if (m.relationType !== "PrerequisiteOf" || (versionsReady && (!source || source.status !== "Published" || source.isLegacy || source.gradeLevel >= grade || !source.nodeMappings?.some(n => n.nodeId === e.sourceNodeId)))) issues.push({ ...base, id: `source:${m.edgeId}`, message: `Quan hệ ${label} chưa có khung nguồn tiên quyết hợp lệ.`, remedy: "Chọn khung đã duyệt ở khối thấp hơn, có chủ đề nguồn, hoặc gỡ quan hệ." });
    } else if ((m.relationType === "PrerequisiteOf" || m.relationType === "PartOf") && selected.has(e.targetNodeId) && selected.has(e.sourceNodeId)) dependencies.get(e.sourceNodeId)!.push({ to: e.targetNodeId, edge: m.edgeId });
    if (!Number.isFinite(m.weight) || m.weight < 0 || m.weight > 1) issues.push({ ...base, id: `weight:${m.edgeId}`, message: `Trọng số của ${label} phải từ 0 đến 1.`, remedy: "Sửa trường trọng số trong chi tiết quan hệ." });
  }
  for (const c of cycles(dependencies)) {
    const parts = c.nodes.map((n, i) => i < c.edges.length ? `${name(n)} —${relationInfo[mappings.find(m => m.edgeId === c.edges[i])!.relationType].label}→` : name(n));
    issues.push({ id: `edge-cycle:${c.edges.join(":")}`, kind: "edge", nodeIds: [...new Set(c.nodes)], edgeIds: c.edges, message: `Vòng quan hệ: ${c.nodes.length > 8 ? `${parts.slice(0, 4).join(" ")} … ${name(c.nodes[0])} (${c.edges.length} quan hệ)` : parts.join(" ")}.`, fullPath: c.nodes.length > 8 ? parts.join(" ") : undefined, remedy: "Chọn một quan hệ trong vòng để sửa hoặc gỡ. Tiên quyết và Thuộc về được kiểm tra vòng cùng nhau." });
  }
  return issues;
}
