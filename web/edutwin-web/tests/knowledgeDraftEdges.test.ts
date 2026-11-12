import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import { findReversePrerequisite, invalidDraftEdges, replaceDraftPrerequisite } from "../src/utils/knowledgeDraftEdges.ts";
import type { KnowledgeGraphEdgeDto, KnowledgeVersionEdgeMapping } from "../src/types/knowledgeGraph.ts";

const edge = (edgeId: string, sourceNodeId: string, targetNodeId: string,
  relationType: KnowledgeGraphEdgeDto["relationType"] = "PrerequisiteOf"): KnowledgeGraphEdgeDto => ({
  edgeId, sourceNodeId, targetNodeId, relationType, weight: 0.8, rowVersion: "1",
});

test("reverse lookup uses direction and relation type rather than treating the same pair as a duplicate", () => {
  const wrong = edge("wrong", "vp1", "cc2");
  const reverse = edge("reverse", "cc2", "vp1");
  assert.equal(findReversePrerequisite([wrong, edge("related", "cc2", "vp1", "RelatedTo"), reverse], wrong)?.edgeId, "reverse");
  assert.equal(findReversePrerequisite([wrong], wrong), undefined);
});

test("reversing a Draft relation removes its old mapping while preserving other relations", () => {
  const mappings: KnowledgeVersionEdgeMapping[] = [
    { edgeId: "wrong", relationType: "PrerequisiteOf", weight: 0.6 },
    { edgeId: "other", relationType: "PrerequisiteOf", weight: 1 },
  ];
  assert.deepEqual(replaceDraftPrerequisite(mappings, "wrong", "reverse", 0.6), [
    mappings[1], { edgeId: "reverse", relationType: "PrerequisiteOf", weight: 0.6 },
  ]);
  assert.equal(mappings.length, 2);
  assert.equal(replaceDraftPrerequisite([], "wrong", "reverse", 1).length, 1);
});

// "Toán siêu cấp" (Khối 12): five valid relations between five selected topics.
const draftNodes = ["25016", "25017", "25018", "25019", "25020"].map(nodeId => ({ nodeId, parentNodeId: null }));
const draftCatalog = [
  edge("25011", "25016", "25017", "RelatedTo"), edge("25012", "25017", "25018", "PartOf"),
  edge("25013", "25019", "25020", "RelatedTo"), edge("25014", "25018", "25019", "CausesErrorIn"),
  edge("25015", "25016", "25019", "PrerequisiteOf"),
];
const draftMappings: KnowledgeVersionEdgeMapping[] = draftCatalog.map(e => ({ edgeId: e.edgeId, relationType: e.relationType, weight: e.weight }));

test("draft relations are not reported missing while the subject library is still loading", () => {
  assert.equal(invalidDraftEdges(draftMappings, draftNodes, undefined), null, "unknown, not invalid");
  assert.equal(invalidDraftEdges(draftMappings, draftNodes, null), null);
  assert.deepEqual(invalidDraftEdges(draftMappings, draftNodes, draftCatalog), [], "all five are valid once loaded");
});

test("loaded library still reports genuinely invalid relations", () => {
  const withoutTarget = draftNodes.filter(n => n.nodeId !== "25020");
  assert.deepEqual(invalidDraftEdges(draftMappings, withoutTarget, draftCatalog)?.map(m => m.edgeId), ["25013"]);
  const unknown: KnowledgeVersionEdgeMapping = { edgeId: "deleted-edge", relationType: "PrerequisiteOf", weight: 1 };
  assert.deepEqual(invalidDraftEdges([...draftMappings, unknown], draftNodes, draftCatalog)?.map(m => m.edgeId), ["deleted-edge"]);
  const crossNonPrerequisite: KnowledgeVersionEdgeMapping = { ...draftMappings[0], sourceGraphVersionId: "grade-11" };
  assert.deepEqual(invalidDraftEdges([crossNonPrerequisite], draftNodes, draftCatalog)?.map(m => m.edgeId), ["25011"]);
  const sourceOutside = draftNodes.filter(n => n.nodeId !== "25016");
  assert.deepEqual(invalidDraftEdges([draftMappings[4]], sourceOutside, draftCatalog)?.map(m => m.edgeId), ["25015"]);
  assert.deepEqual(invalidDraftEdges([{ ...draftMappings[4], sourceGraphVersionId: "grade-11" }], sourceOutside, draftCatalog), [],
    "a prerequisite from a published lower-grade framework is allowed");
});

test("a relation created just now (library refreshing) is unknown, other checks still apply", () => {
  const reverse: KnowledgeVersionEdgeMapping = { edgeId: "new-reverse", relationType: "PrerequisiteOf", weight: 1 };
  assert.deepEqual(invalidDraftEdges([...draftMappings, reverse], draftNodes, draftCatalog, true), []);
  const withoutTarget = draftNodes.filter(n => n.nodeId !== "25020");
  assert.deepEqual(invalidDraftEdges([...draftMappings, reverse], withoutTarget, draftCatalog, true)?.map(m => m.edgeId), ["25013"]);
});

test("version panel only raises the alert from a loaded library and blocks saving until it is ready", () => {
  const panel = fs.readFileSync(new URL("../src/components/teacher/KnowledgeVersionPanel.tsx", import.meta.url), "utf8");
  assert.match(panel, /invalidDraftEdges\(edges, nodes, catalogReady \? catalog\.data\.edges : null, catalog\.isFetching\)/);
  assert.match(panel, /const catalogReady = catalog\.isSuccess;/);
  assert.match(panel, /disabled=\{busy \|\| !dirty \|\| stale \|\| !catalogReady \|\| catalog\.isFetching \|\| invalidEdges\.length > 0\}/);
  assert.match(panel, /catalog\.isPending && <p role="status"/, "loading is a status, never an alert toast");
  assert.doesNotMatch(panel, /catalogEdges\.find\(e => e\.edgeId === mapping\.edgeId\)/);
});
