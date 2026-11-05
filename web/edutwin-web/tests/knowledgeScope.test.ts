import test from "node:test";
import assert from "node:assert/strict";
import { curriculumGraphLinkScope, graphOccurrences, incompatibleKnowledgeNodes, knowledgeKeys, knowledgeScopeSearch, readKnowledgeScope } from "../src/utils/knowledgeScope.ts";
import type { KnowledgeGraphDto } from "../src/types/knowledgeGraph.ts";

test("grade, pinned version, class, year and history survive graph navigation", () => {
  const scope = { gradeLevel: 11, graphVersionId: "version-b", curriculumId: "curr-b", classId: "class-b", academicYear: "2026-2027", history: true };
  const search = knowledgeScopeSearch("math", scope);
  assert.deepEqual(readKnowledgeScope(new URLSearchParams(search)), scope);
  assert.equal(new URLSearchParams(search).get("subjectId"), "math");
});

test("an invalid explicit grade never silently becomes an unscoped subject request", () => {
  assert.equal(readKnowledgeScope(new URLSearchParams("gradeLevel=9")).gradeLevel, 9);
  assert.equal(readKnowledgeScope(new URLSearchParams("gradeLevel=oops")).gradeLevel, -1);
  assert.equal(readKnowledgeScope(new URLSearchParams()).gradeLevel, undefined);
});

test("changing grade or class, year, history and actor cannot reuse another library cache entry", () => {
  const baseline = knowledgeKeys.graph("center", "teacher", "math", { gradeLevel: 10, classId: "a", academicYear: "2026", history: false });
  for (const scope of [{ gradeLevel: 11, classId: "a", academicYear: "2026", history: false },
    { gradeLevel: 10, classId: "b", academicYear: "2026", history: false },
    { gradeLevel: 10, classId: "a", academicYear: "2027", history: false },
    { gradeLevel: 10, classId: "a", academicYear: "2026", history: true }]) {
    assert.notDeepEqual(knowledgeKeys.graph("center", "teacher", "math", scope), baseline);
  }
  assert.notDeepEqual(knowledgeKeys.nodes("center", "other", "math", {}), knowledgeKeys.nodes("center", "teacher", "math", {}));
});

test("scope changes report incompatible selections without mutating teacher choices", () => {
  const selected = ["old", "shared"];
  assert.deepEqual(incompatibleKnowledgeNodes(selected, [{ nodeId: "shared" }, { nodeId: "new" }]), ["old"]);
  assert.deepEqual(selected, ["old", "shared"]);
  assert.deepEqual(incompatibleKnowledgeNodes(selected, []), selected);
});

test("class projections keep one canonical topic in two distinct parent trees and discard dangling edges", () => {
  const node = (nodeId: string, parent: string | null) => ({ nodeId, nodeType: "Topic", nodeCode: nodeId, nodeName: nodeId,
    parentNodeId: parent, orderIndex: 0, examImportance: 10, estimatedLearningMinutes: 45, isActive: true, description: null, rowVersion: "1" });
  const graph = { subjectId: "math", nodes: [
    { ...node("parent-a", null), provenance: [{ occurrenceId: "a-parent", nodeId: "parent-a", graphVersionId: "v10", gradeLevel: 10, curriculumId: "c10" }] },
    { ...node("parent-b", null), provenance: [{ occurrenceId: "b-parent", nodeId: "parent-b", graphVersionId: "v11", gradeLevel: 11, curriculumId: "c11" }] },
    { ...node("shared", null), provenance: [
      { occurrenceId: "a-shared", nodeId: "shared", graphVersionId: "v10", gradeLevel: 10, curriculumId: "c10", parentNodeId: "parent-a" },
      { occurrenceId: "b-shared", nodeId: "shared", graphVersionId: "v11", gradeLevel: 11, curriculumId: "c11", parentNodeId: "parent-b" }] },
  ], edges: [
    { edgeId: "edge", occurrenceId: "a-edge", sourceNodeId: "parent-a", targetNodeId: "shared", sourceOccurrenceId: "a-parent", targetOccurrenceId: "a-shared", relationType: "PartOf", weight: 1, rowVersion: "1" },
    { edgeId: "outside", sourceNodeId: "missing", targetNodeId: "shared", relationType: "PrerequisiteOf", weight: 1, rowVersion: "1" },
  ] } as KnowledgeGraphDto;
  const projection = graphOccurrences(graph);
  const shared = projection.nodes.filter(n => n.canonicalNodeId === "shared");
  assert.deepEqual(shared.map(n => [n.nodeId, n.parentNodeId, n.gradeLevel]), [["a-shared", "a-parent", 10], ["b-shared", "b-parent", 11]]);
  assert.equal(projection.edges.length, 1);
  assert.equal(projection.edges[0].canonicalEdgeId, "edge");
  assert.equal(projection.edges[0].targetNodeId, "a-shared");
});

test("an explicitly empty projection stays empty", () => {
  assert.deepEqual(graphOccurrences({ subjectId: "math", nodes: [], edges: [] }), { nodes: [], edges: [] });
});

test("unsaved curriculum grade, version and topic order never produce a contradictory curriculum graph link", () => {
  const saved = { gradeLevel: 10, graphVersionId: "v10", nodeIds: ["a", "b"] };
  assert.equal(curriculumGraphLinkScope("curriculum", saved, saved).curriculumId, "curriculum");
  for (const current of [{ ...saved, gradeLevel: 12, graphVersionId: "v12" }, { ...saved, graphVersionId: "new-v10" },
    { ...saved, nodeIds: ["b", "a"] }, { ...saved, nodeIds: ["a"] }]) {
    const link = curriculumGraphLinkScope("curriculum", current, saved);
    assert.equal(link.curriculumId, undefined);
    assert.equal(link.gradeLevel, current.gradeLevel);
    assert.equal(link.graphVersionId, current.graphVersionId);
  }
  assert.equal(curriculumGraphLinkScope(undefined, saved).curriculumId, undefined);
});

test("class occurrences carry their own curriculum title, application role and grade exception", () => {
  const graph = { subjectId: "math", edges: [], nodes: [{ nodeId: "shared", nodeType: "Topic", nodeCode: "shared", nodeName: "Shared",
    parentNodeId: null, orderIndex: 0, examImportance: 10, estimatedLearningMinutes: 45, isActive: true, description: null, rowVersion: "1",
    provenance: [
      { occurrenceId: "primary", nodeId: "shared", graphVersionId: "v12", gradeLevel: 12, curriculumId: "c12", curriculumTitle: "Chính 12", applicationId: "app-a", applicationRole: "Primary" },
      { occurrenceId: "supplemental", nodeId: "shared", graphVersionId: "v10", gradeLevel: 10, curriculumId: "c10", curriculumTitle: "Ôn nền 10", applicationId: "app-b", applicationRole: "Supplemental", isGradeException: true, gradeMismatchReason: "Ôn kiến thức nền" },
    ] }] } as KnowledgeGraphDto;
  const projection = graphOccurrences(graph);
  assert.equal(projection.nodes.length, 2);
  assert.equal(projection.nodes[0].sources?.[0].curriculumTitle, "Chính 12");
  assert.equal(projection.nodes[0].sources?.[0].applicationRole, "Primary");
  assert.equal(projection.nodes[0].sources?.[0].isGradeException, undefined);
  assert.equal(projection.nodes[1].sources?.[0].curriculumTitle, "Ôn nền 10");
  assert.equal(projection.nodes[1].sources?.[0].applicationRole, "Supplemental");
  assert.equal(projection.nodes[1].sources?.[0].gradeMismatchReason, "Ôn kiến thức nền");
  assert.equal(projection.nodes[1].canonicalNodeId, projection.nodes[0].canonicalNodeId);
});

test("reapplying the same historical curriculum keeps each child's parent in the same application", () => {
  const node = (nodeId: string) => ({ nodeId, nodeType: "Topic", nodeCode: nodeId, nodeName: nodeId, parentNodeId: null,
    orderIndex: 0, examImportance: 10, estimatedLearningMinutes: 45, isActive: true, description: null, rowVersion: "1" });
  const source = (applicationId: string, nodeId: string, parentNodeId?: string) => ({ occurrenceId: `${applicationId}-${nodeId}`, nodeId,
    graphVersionId: "version", gradeLevel: 10, curriculumId: "same-curriculum", applicationId, parentNodeId });
  const graph = { subjectId: "math", edges: [], nodes: [
    { ...node("parent"), provenance: [source("first", "parent"), source("second", "parent")] },
    { ...node("child"), provenance: [source("first", "child", "parent"), source("second", "child", "parent")] },
  ] } as KnowledgeGraphDto;
  assert.deepEqual(graphOccurrences(graph).nodes.filter(n => n.canonicalNodeId === "child").map(n => [n.nodeId, n.parentNodeId]),
    [["first-child", "first-parent"], ["second-child", "second-parent"]]);
});
