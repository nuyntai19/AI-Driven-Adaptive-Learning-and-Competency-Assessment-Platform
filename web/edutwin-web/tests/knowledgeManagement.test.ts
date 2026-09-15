import assert from "node:assert/strict";
import test from "node:test";
import { canAccess } from "../src/auth/capabilities.ts";
import { permissions } from "../src/auth/permissions.ts";
import type {
  KnowledgeGraphEdgeDto,
  KnowledgeGraphNodeDto,
} from "../src/types/knowledgeGraph.ts";

const manager = (grants: string[]) => ({
  accountType: "CenterManager" as const,
  permissions: grants,
});

test("Subject list route requires only subjects.read and keeps mutations capability-first", () => {
  const readOnly = manager([permissions.subjectsRead]);
  assert.equal(canAccess(readOnly, { allOf: [permissions.subjectsRead] }), true);
  assert.equal(canAccess(readOnly, { allOf: [permissions.subjectsCreate] }), false);
  assert.equal(canAccess(readOnly, { allOf: [permissions.subjectsUpdate] }), false);
  assert.equal(canAccess(readOnly, { allOf: [permissions.subjectsDelete] }), false);
});

test("Knowledge graph route requires subject, node, and edge read capabilities", () => {
  const fullReader = manager([
    permissions.subjectsRead,
    permissions.nodesRead,
    permissions.edgesRead,
  ]);
  const partialReader = manager([permissions.subjectsRead, permissions.nodesRead]);
  const requirement = {
    allOf: [permissions.subjectsRead, permissions.nodesRead, permissions.edgesRead],
  };

  assert.equal(canAccess(fullReader, requirement), true);
  assert.equal(canAccess(partialReader, requirement), false);
});

test("Node and edge mutation capabilities remain independent", () => {
  const nodeEditor = manager([permissions.nodesUpdate]);
  assert.equal(canAccess(nodeEditor, { allOf: [permissions.nodesUpdate] }), true);
  assert.equal(canAccess(nodeEditor, { allOf: [permissions.nodesDelete] }), false);
  assert.equal(canAccess(nodeEditor, { allOf: [permissions.edgesUpdate] }), false);
});

test("Graph lifecycle DTOs require canonical RowVersion instead of a client fallback", () => {
  const node: KnowledgeGraphNodeDto = {
    nodeId: "101",
    parentNodeId: null,
    nodeType: "Topic",
    nodeCode: "MATH.LOG",
    nodeName: "Mũ và Logarit",
    description: null,
    orderIndex: 2,
    examImportance: 20,
    estimatedLearningMinutes: 180,
    isActive: true,
    rowVersion: "7",
  };
  const edge: KnowledgeGraphEdgeDto = {
    edgeId: "501",
    sourceNodeId: "100",
    targetNodeId: "101",
    relationType: "PrerequisiteOf",
    weight: 0.8,
    rowVersion: "9",
  };

  assert.equal(node.rowVersion, "7");
  assert.equal(edge.rowVersion, "9");
});
