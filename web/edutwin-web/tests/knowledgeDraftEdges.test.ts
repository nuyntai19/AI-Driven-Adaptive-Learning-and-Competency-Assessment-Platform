import test from "node:test";
import assert from "node:assert/strict";
import { findReversePrerequisite, replaceDraftPrerequisite } from "../src/utils/knowledgeDraftEdges.ts";
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
