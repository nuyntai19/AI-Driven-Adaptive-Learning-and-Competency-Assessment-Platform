import test from "node:test";
import assert from "node:assert/strict";
import { diagnoseDraft, draftPreview, filterLibraryTopics, effectiveDraftEdges } from "../src/utils/knowledgeDraftEditor.ts";
import type { KnowledgeGraphDto, KnowledgeGraphNodeDto, KnowledgeGraphVersion, KnowledgeVersionEdgeMapping } from "../src/types/knowledgeGraph.ts";
const node = (id: string, code = `CODE_${id}`): KnowledgeGraphNodeDto => ({ nodeId: id, nodeName: `Nút ${id}`, nodeCode: code, nodeType: "Topic", isActive: true, parentNodeId: null, description: null, orderIndex: 0, examImportance: 3, estimatedLearningMinutes: 45, rowVersion: "1" });
const catalog: KnowledgeGraphDto = { subjectId: "math", nodes: [node("9007199254740993", "MATH12_A"), node("2"), node("3")], edges: [{ edgeId: "10", sourceNodeId: "9007199254740993", targetNodeId: "2", relationType: "RelatedTo", weight: 0, rowVersion: "1" }, { edgeId: "11", sourceNodeId: "2", targetNodeId: "9007199254740993", relationType: "PartOf", weight: 1, rowVersion: "1" }] };
const a = catalog.nodes[0].nodeId;
const versions = [{ graphVersionId: "d10", gradeLevel: 10, status: "Draft", nodeMappings: [{ nodeId: a }] }, { graphVersionId: "p12", gradeLevel: 12, status: "Published", nodeMappings: [{ nodeId: "2" }] }] as KnowledgeGraphVersion[];
const filter = { grade: 10 as const, status: "Draft" as const, source: "", membership: "all" as const, search: "", legacy: false };
test("source filters use draft memberships even without gradeLevels, never code prefixes, and do not inject selected mismatches", () => {
  assert.deepEqual(filterLibraryTopics(catalog.nodes, versions, new Set(["2"]), filter).map(n => n.nodeId), [a]);
  assert.deepEqual(filterLibraryTopics(catalog.nodes, versions, new Set(), { ...filter, grade: 12 }).map(n => n.nodeId), []);
  assert.deepEqual(filterLibraryTopics(catalog.nodes, versions, new Set(), { ...filter, grade: "unknown", status: "all" }).map(n => n.nodeId), ["3"]);
  assert.deepEqual(filterLibraryTopics(catalog.nodes, versions, new Set([a]), { ...filter, membership: "available" }), []);
  assert.deepEqual(filterLibraryTopics(catalog.nodes, versions, new Set([a]), { ...filter, membership: "included", search: "math12_a" }).map(n => n.nodeId), [a]);
});
test("preview overlays the local parent, effective type and zero weight without changing library DTOs", () => {
  const nodes = [{ nodeId: a, parentNodeId: "2" }, { nodeId: "2" }];
  const mappings: KnowledgeVersionEdgeMapping[] = [{ edgeId: "10", relationType: "CausesErrorIn", weight: 0 }];
  const before = JSON.stringify(catalog);
  const p = draftPreview(catalog, nodes, mappings, 12);
  assert.equal(p.nodes[0].parentNodeId, "2"); assert.equal(p.edges[0].relationType, "CausesErrorIn"); assert.equal(p.edges[0].weight, 0);
  assert.equal(p.nodes[0].nodeId, a); assert.equal(JSON.stringify(catalog), before);
  assert.equal(effectiveDraftEdges(mappings, catalog.edges)[0].relationType, "CausesErrorIn");
});
test("incomplete mappings remain diagnosed even if they cannot be rendered, loading is unknown", () => {
  const mappings: KnowledgeVersionEdgeMapping[] = [{ edgeId: "10", relationType: "PrerequisiteOf", weight: 1 }, { edgeId: "missing", relationType: "RelatedTo", weight: 0 }];
  const snapshot = JSON.stringify(mappings);
  assert.equal(draftPreview(catalog, [{ nodeId: a }], mappings, 12).edges.length, 0);
  assert.ok(diagnoseDraft([{ nodeId: a }], mappings, catalog, versions, 12).some(i => i.id === "endpoints:10"));
  assert.ok(diagnoseDraft([{ nodeId: a }], mappings, catalog, versions, 12).some(i => i.id === "edge:missing"));
  assert.ok(!diagnoseDraft([{ nodeId: a }], mappings, catalog, versions, 12, true).some(i => i.id === "edge:missing"));
  assert.deepEqual(diagnoseDraft([], [], undefined, versions, 12), []); assert.equal(JSON.stringify(mappings), snapshot);
});
test("two-node and long parent cycles identify the exact loop, not its unrelated prefix", () => {
  const n = [{ nodeId: "3", parentNodeId: a }, { nodeId: a, parentNodeId: "2" }, { nodeId: "2", parentNodeId: a }];
  const cycle = diagnoseDraft(n, [], catalog, versions, 12).find(i => i.id.startsWith("parent-cycle"))!;
  assert.deepEqual(cycle.nodeIds, [a, "2"]); assert.match(cycle.message, /Nút 9007199254740993.*Nút 2.*Nút 9007199254740993/);
  assert.deepEqual(diagnoseDraft([{ nodeId: a }, { nodeId: "2" }], [], catalog, versions, 12), []);
});
test("effective PrerequisiteOf/PartOf cycles are diagnosed together, semantic overlays are not prerequisites", () => {
  const nodes = [{ nodeId: a }, { nodeId: "2" }];
  const mappings: KnowledgeVersionEdgeMapping[] = [{ edgeId: "10", relationType: "PrerequisiteOf", weight: 1 }, { edgeId: "11", relationType: "PartOf", weight: 1 }];
  const cycle = diagnoseDraft(nodes, mappings, catalog, versions, 12).find(i => i.id.startsWith("edge-cycle"))!;
  assert.deepEqual(cycle.edgeIds, ["10", "11"]); assert.match(cycle.message, /Tiên quyết.*Thuộc về/);
  assert.deepEqual(diagnoseDraft(nodes, mappings.map(m => ({ ...m, relationType: "RelatedTo" })), catalog, versions, 12), []);
  assert.deepEqual(diagnoseDraft(nodes, mappings.map(m => ({ ...m, relationType: "CausesErrorIn" })), catalog, versions, 12), []);
});
test("external source is a display-only context; its parent and membership never enter the target snapshot", () => {
  const nodes = [{ nodeId: "2" }];
  const mappings: KnowledgeVersionEdgeMapping[] = [{ edgeId: "10", relationType: "PrerequisiteOf", weight: 1, sourceGraphVersionId: "p10" }];
  const p10 = { ...versions[0], graphVersionId: "p10", status: "Published" as const };
  const p = draftPreview(catalog, nodes, mappings, 12);
  assert.equal(p.nodes.length, 2); assert.match(p.nodes[1].nodeId, /^external:/); assert.equal(p.nodes[1].parentNodeId, null); assert.equal(nodes.length, 1);
  assert.deepEqual(diagnoseDraft(nodes, mappings, catalog, [p10], 12), []);
  assert.ok(diagnoseDraft(nodes, mappings, catalog, [{ ...p10, gradeLevel: 12 }], 12).some(i => i.id === "source:10"));
  assert.ok(diagnoseDraft(nodes, mappings.map(m => ({ ...m, relationType: "RelatedTo" })), catalog, [p10], 12).some(i => i.id === "source:10"));
  assert.ok(!diagnoseDraft(nodes, mappings, catalog, [], 12, false, false).some(i => i.id === "source:10"), "not-yet-loaded source versions are unknown, not invalid");
});
test("long cycles have a bounded summary and an expandable complete path", () => {
  const topics = Array.from({ length: 40 }, (_, i) => node(String(i + 1)));
  const mappings = topics.map((n, i) => ({ nodeId: n.nodeId, parentNodeId: topics[(i + 1) % topics.length].nodeId }));
  const issues = diagnoseDraft(mappings, [], { ...catalog, nodes: topics, edges: [] }, [], 12);
  assert.equal(issues.length, 1); assert.ok(issues[0].message.length < 500); assert.match(issues[0].fullPath!, /Nút 40/); assert.equal(issues[0].nodeIds.length, 40);
});
