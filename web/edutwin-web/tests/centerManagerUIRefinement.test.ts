import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import {
  computeDeterministicDagLayout,
  computeEdgePath,
} from "../src/utils/knowledgeGraphLayout.ts";
import type {
  KnowledgeGraphNodeDto,
  KnowledgeGraphEdgeDto,
} from "../src/types/knowledgeGraph.ts";

const readSource = (relativePath: string) =>
  readFileSync(new URL(relativePath, import.meta.url), "utf8");

const knowledgeGraphPageSource = readSource("../src/pages/KnowledgeGraphPage.tsx");
const designSystemCssSource = readSource("../src/components/centerManager/centerManagerDesignSystem.css");
const themeModeSource = readSource("../src/utils/themeMode.ts");
const themeToggleSource = readSource("../src/components/ThemeToggle.tsx");
const themeScopeSource = readSource("../src/components/centerManager/CenterManagerThemeScope.tsx");
const layoutSource = readSource("../src/layouts/CenterManagerLayout.tsx");

// Helper to create test node
function makeTestNode(
  id: string,
  type: KnowledgeGraphNodeDto["nodeType"] = "Topic",
  orderIndex = 0,
  code?: string
): KnowledgeGraphNodeDto {
  return {
    nodeId: id,
    nodeType: type,
    nodeCode: code ?? `CODE-${id}`,
    nodeName: `Node ${id}`,
    orderIndex,
    examImportance: 10,
    rowVersion: "AAAAAAA=",
    description: null,
    estimatedLearningMinutes: 30,
    isActive: true,
    parentNodeId: null,
  };
}

// Helper to create test edge
function makeTestEdge(
  id: string,
  sourceId: string,
  targetId: string,
  relationType: KnowledgeGraphEdgeDto["relationType"] = "PrerequisiteOf",
  weight = 1.0
): KnowledgeGraphEdgeDto {
  return {
    edgeId: id,
    sourceNodeId: sourceId,
    targetNodeId: targetId,
    relationType,
    weight,
    rowVersion: "AAAAAAA=",
  };
}

test("DAG Layout: linear chain A -> B -> C arranges nodes into sequential ranks with increasing X", () => {
  const nodes = [makeTestNode("A"), makeTestNode("B"), makeTestNode("C")];
  const edges = [
    makeTestEdge("e1", "A", "B", "PrerequisiteOf"),
    makeTestEdge("e2", "B", "C", "PrerequisiteOf"),
  ];

  const result = computeDeterministicDagLayout(nodes, edges);

  assert.equal(result.rankCount, 3);
  const posA = result.positions.get("A");
  const posB = result.positions.get("B");
  const posC = result.positions.get("C");

  assert.ok(posA && posB && posC);
  assert.equal(posA.rank, 0);
  assert.equal(posB.rank, 1);
  assert.equal(posC.rank, 2);
  assert.ok(posA.x < posB.x, "X coordinate of A should be strictly less than B");
  assert.ok(posB.x < posC.x, "X coordinate of B should be strictly less than C");
});

test("DAG Layout: branching A -> B and A -> C places children in the same rank with unique rows", () => {
  const nodes = [makeTestNode("A"), makeTestNode("B", "Topic", 0), makeTestNode("C", "Topic", 1)];
  const edges = [
    makeTestEdge("e1", "A", "B", "PrerequisiteOf"),
    makeTestEdge("e2", "A", "C", "PrerequisiteOf"),
  ];

  const result = computeDeterministicDagLayout(nodes, edges);

  const posA = result.positions.get("A");
  const posB = result.positions.get("B");
  const posC = result.positions.get("C");

  assert.ok(posA && posB && posC);
  assert.equal(posA.rank, 0);
  assert.equal(posB.rank, 1);
  assert.equal(posC.rank, 1);
  assert.equal(posB.x, posC.x);
  assert.notEqual(posB.y, posC.y, "Branching children must not collide on the Y axis");
});

test("DAG Layout: longest path calculation ensures multi-step prerequisite takes precedence", () => {
  // A -> B -> C and A -> C (direct shortcut)
  // C must be placed at rank 2 (after B), not collapsed to rank 1
  const nodes = [makeTestNode("A"), makeTestNode("B"), makeTestNode("C")];
  const edges = [
    makeTestEdge("e1", "A", "B", "PrerequisiteOf"),
    makeTestEdge("e2", "B", "C", "PrerequisiteOf"),
    makeTestEdge("e3", "A", "C", "PrerequisiteOf"),
  ];

  const result = computeDeterministicDagLayout(nodes, edges);

  const posC = result.positions.get("C");
  assert.ok(posC);
  assert.equal(posC.rank, 2, "Longest dependency chain determines rank of C");
});

test("DAG Layout: RelatedTo edges are lateral and do NOT advance topological rank", () => {
  const nodes = [makeTestNode("A"), makeTestNode("B")];
  const edges = [makeTestEdge("e1", "A", "B", "RelatedTo")];

  const result = computeDeterministicDagLayout(nodes, edges);

  const posA = result.positions.get("A");
  const posB = result.positions.get("B");
  assert.ok(posA && posB);
  assert.equal(posA.rank, 0);
  assert.equal(posB.rank, 0, "RelatedTo edge must not push B to rank 1");
  assert.equal(posA.x, posB.x, "Both nodes remain in the same column");
  assert.notEqual(posA.y, posB.y, "Nodes are vertically spaced in row order");
});

test("DAG Layout: dependency edges (PartOf) advance rank while non-DAG edges (CausesErrorIn) do NOT", () => {
  const nodes = [makeTestNode("Parent"), makeTestNode("Child"), makeTestNode("ErrorTarget")];
  const edges = [
    makeTestEdge("e1", "Parent", "Child", "PartOf"),
    makeTestEdge("e2", "Child", "ErrorTarget", "CausesErrorIn"),
  ];

  const result = computeDeterministicDagLayout(nodes, edges);

  assert.equal(result.positions.get("Parent")?.rank, 0);
  assert.equal(result.positions.get("Child")?.rank, 1, "PartOf must advance rank");
  assert.equal(
    result.positions.get("ErrorTarget")?.rank,
    0,
    "CausesErrorIn is not a DAG dependency in KnowledgeGraphValidator and must NOT advance rank"
  );
});

test("DAG Layout: disconnected nodes without edges are placed at rank 0 without crashing", () => {
  const nodes = [makeTestNode("N1"), makeTestNode("N2"), makeTestNode("N3")];
  const edges: KnowledgeGraphEdgeDto[] = [];

  const result = computeDeterministicDagLayout(nodes, edges);

  assert.equal(result.rankCount, 1);
  nodes.forEach((n) => {
    assert.equal(result.positions.get(n.nodeId)?.rank, 0);
  });
  assert.ok(result.width >= 1000);
  assert.ok(result.height >= 650);
});

test("DAG Layout: cyclic dependency fails closed in O(V+E) and assigns deterministic fallback rank", () => {
  // Cycle: A -> B -> C -> A
  const nodes = [makeTestNode("A"), makeTestNode("B"), makeTestNode("C")];
  const edges = [
    makeTestEdge("e1", "A", "B", "PrerequisiteOf"),
    makeTestEdge("e2", "B", "C", "PrerequisiteOf"),
    makeTestEdge("e3", "C", "A", "PrerequisiteOf"),
  ];

  const result = computeDeterministicDagLayout(nodes, edges);

  // Must terminate and assign positions to all 3 nodes
  assert.equal(result.positions.size, 3);
  nodes.forEach((n) => {
    const pos = result.positions.get(n.nodeId);
    assert.ok(pos, `Node ${n.nodeId} must have a position`);
    assert.ok(Number.isFinite(pos.x));
    assert.ok(Number.isFinite(pos.y));
  });
});

test("Edge Path: routes forward, lateral arc for same-column, and backward smoothly", () => {
  // 1. Forward edge
  const forward = computeEdgePath({ x: 60, y: 60 }, { x: 330, y: 60 }, 190, 75);
  assert.match(forward.d, /^M 250 97\.5 C/);
  assert.ok(forward.midX > 60 && forward.midX < 330);

  // 2. Same column edge (x1 === x2)
  const sameCol = computeEdgePath({ x: 60, y: 60 }, { x: 60, y: 170 }, 190, 75);
  assert.match(sameCol.d, /^M 250 97\.5 C/);
  assert.ok(sameCol.midX > 250, "Same column edge must arc outward past the card right edge");

  // 3. Backward edge (x1 > x2)
  const backward = computeEdgePath({ x: 330, y: 60 }, { x: 60, y: 60 }, 190, 75);
  assert.match(backward.d, /^M 330 97\.5 C/);
});

test("KnowledgeGraphPage: duplicate create buttons removed from sidebar while preserving PageHeader actions", () => {
  // Overview sidebar duplicate buttons must be gone
  assert.doesNotMatch(
    knowledgeGraphPageSource,
    /\+ Tạo nút kiến thức mới/,
    "Duplicate '+ Tạo nút kiến thức mới' button in overview sidebar must be removed"
  );
  assert.doesNotMatch(
    knowledgeGraphPageSource,
    /\+ Tạo liên kết mới/,
    "Duplicate '+ Tạo liên kết mới' button in overview sidebar must be removed"
  );

  // Canonical actions in PageHeader must remain intact
  assert.match(
    knowledgeGraphPageSource,
    /id="btn-open-create-node"/,
    "Canonical create node button in PageHeader must exist"
  );
  assert.match(
    knowledgeGraphPageSource,
    /id="btn-open-create-edge"/,
    "Canonical create edge button in PageHeader must exist"
  );
  assert.match(
    knowledgeGraphPageSource,
    /canCreateNodes && selectedSubjectId/,
    "Create node action must be capability-gated"
  );
  assert.match(
    knowledgeGraphPageSource,
    /canCreateEdges && selectedSubjectId && nodes\.length >= 2/,
    "Create edge action must be capability-gated"
  );
});

test("KnowledgeGraphPage: uses computeDeterministicDagLayout and theme CSS variables for SVG cards", () => {
  // Uses deterministic layout helper
  assert.match(
    knowledgeGraphPageSource,
    /computeDeterministicDagLayout\(nodes, edges\)/,
    "KnowledgeGraphPage must use computeDeterministicDagLayout"
  );
  assert.match(
    knowledgeGraphPageSource,
    /computeEdgePath\(sourcePos, targetPos, 190, 75\)/,
    "KnowledgeGraphPage must use computeEdgePath for clean Bezier routing"
  );

  // Uses CSS variables for card styling instead of hardcoded dark colors
  assert.match(
    knowledgeGraphPageSource,
    /fill="var\(--cm-surface\)"/,
    "SVG card background and badges must use var(--cm-surface) for theme adaptability"
  );
  assert.match(
    knowledgeGraphPageSource,
    /fill="var\(--cm-text\)"/,
    "SVG text must use var(--cm-text)"
  );
});

test("Design System: Light Mode tokens and native select contrast are strictly configured", () => {
  // Light mode tokens under actor scope
  assert.match(
    designSystemCssSource,
    /\[data-actor="center-manager"\]\[data-theme="light"\]/,
    "Design system must contain light mode tokens scoped to center-manager"
  );
  assert.match(designSystemCssSource, /--cm-bg:\s*#f8fafc/);
  assert.match(designSystemCssSource, /--cm-surface:\s*#ffffff/);
  assert.match(designSystemCssSource, /--cm-text:\s*#0f172a/);

  // Dropdown / Select contrast
  assert.match(designSystemCssSource, /\.cm-select/);
  assert.match(designSystemCssSource, /select\.cm-field/);
  assert.match(
    designSystemCssSource,
    /\[data-actor="center-manager"\] option/,
    "Dark mode options must have explicit background and color"
  );
  assert.match(
    designSystemCssSource,
    /\[data-actor="center-manager"\]\[data-theme="light"\] option/,
    "Light mode options must have explicit white background and dark text"
  );
});

test("Theme Integration: ThemeToggle, CenterManagerThemeScope, and CenterManagerLayout are wired together", () => {
  // themeMode utility exports
  assert.match(
    themeModeSource,
    /export const THEME_STORAGE_KEY = "edutwin-theme"/,
    "Must reuse canonical edutwin-theme storage key without creating redundant stores"
  );
  assert.match(
    themeModeSource,
    /export function useThemeMode\(\)/,
    "Must export useThemeMode hook for synchronized theme state"
  );

  // ThemeToggle uses useThemeMode
  assert.match(
    themeToggleSource,
    /useThemeMode\(\)/,
    "ThemeToggle must use useThemeMode hook"
  );

  // ThemeScope applies data-theme
  assert.match(
    themeScopeSource,
    /data-theme=\{theme\}/,
    "CenterManagerThemeScope must apply data-theme attribute"
  );
  assert.match(
    themeScopeSource,
    /useThemeMode\(\)/,
    "CenterManagerThemeScope must use useThemeMode"
  );

  // CenterManagerLayout embeds ThemeToggle in header
  assert.match(
    layoutSource,
    /<ThemeToggle \/>/,
    "CenterManagerLayout must include ThemeToggle in header"
  );
  assert.doesNotMatch(
    layoutSource,
    /className=".*bg-#0f172a.*"/,
    "CenterManagerLayout must not hardcode dark hex in layout shell"
  );
  assert.doesNotMatch(
    layoutSource,
    /hover:text-white/,
    "CenterManagerLayout navigation links must not use hover:text-white to avoid white-on-white text in Light mode"
  );
  assert.doesNotMatch(
    layoutSource,
    /bg-white\/\[0\.04\]/,
    "CenterManagerLayout context card must use theme token rather than bg-white/[0.04]"
  );
});
