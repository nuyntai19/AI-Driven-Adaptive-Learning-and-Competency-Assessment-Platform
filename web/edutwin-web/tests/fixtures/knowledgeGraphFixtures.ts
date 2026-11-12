import type { KnowledgeGraphEdgeDto, KnowledgeGraphNodeDto, KnowledgeRelationType } from "../../src/types/knowledgeGraph.ts";
import type { GraphScene } from "../../src/utils/knowledgeGraphScene.ts";
import type { LabelPlacement } from "../../src/utils/knowledgeGraphLabels.ts";
import { arrowheadRect } from "../../src/utils/knowledgeGraphLabels.ts";
import { collinearOverlap, inflateRect, rectContains, rectsOverlap, segmentIntersectsRect, segmentsCross, segmentsOf, type Segment } from "../../src/utils/knowledgeGraphGeometry.ts";

export const card = { cardWidth: 240, cardHeight: 118 };

export function node(id: string, order = 0, parent: string | null = null, name = `Chủ đề ${id}`): KnowledgeGraphNodeDto {
  return { nodeId: id, nodeName: name, nodeCode: `TOAN12-${id}`, nodeType: "Topic", parentNodeId: parent, isActive: true,
    orderIndex: order, examImportance: 3, estimatedLearningMinutes: 45, rowVersion: "1", description: null };
}

export function edge(id: string, source: string, target: string, relationType: KnowledgeRelationType = "PrerequisiteOf", weight = 1): KnowledgeGraphEdgeDto {
  return { edgeId: id, sourceNodeId: source, targetNodeId: target, relationType, weight, rowVersion: "1" };
}

/**
 * Reproduces the reported screenshot region: two columns of Toán 12 cards whose prerequisite
 * edges all converge in the corridor between them, repeated weight 1 badges, a same-column
 * overlay on the right column, a parallel relation and a reciprocal (backward) overlay.
 */
export function screenshotFixture() {
  const left = ["A1", "A2", "A3", "A4", "A5", "A6"].map((id, i) => node(id, i, null, `Toán 12 – Bài ${i + 1}: Ứng dụng đạo hàm và khảo sát hàm số`));
  const right = ["B1", "B2", "B3", "B4", "B5", "B6"].map((id, i) => node(id, i, null, `Toán 12 – Bài ${i + 7}: Nguyên hàm, tích phân và ứng dụng`));
  const pairs: [string, string][] = [["A1", "B4"], ["A2", "B1"], ["A3", "B6"], ["A4", "B2"], ["A5", "B3"], ["A6", "B5"],
    ["A1", "B2"], ["A3", "B1"], ["A5", "B6"], ["A2", "B5"], ["A6", "B3"], ["A4", "B6"]];
  const edges = pairs.map(([s, t]) => edge(`${s}-${t}`, s, t));
  edges.push(edge("B2~B5", "B2", "B5", "RelatedTo"));
  edges.push(edge("A1!B4", "A1", "B4", "CausesErrorIn"));
  edges.push(edge("B4~A1", "B4", "A1", "RelatedTo"));
  return { nodes: [...left, ...right], edges };
}

/** Seeded PRNG (mulberry32) for reproducible random graphs. */
export function seeded(seed: number) {
  return () => {
    seed |= 0; seed = (seed + 0x6d2b79f5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export function shuffled<T>(items: T[], seed: number): T[] {
  const rnd = seeded(seed), out = [...items];
  for (let i = out.length - 1; i > 0; i--) { const j = Math.floor(rnd() * (i + 1)); [out[i], out[j]] = [out[j], out[i]]; }
  return out;
}

/** Layered random DAG with optional overlay relations; parents create sibling blocks. */
export function layeredGraph(seed: number, layers: number, perLayer: number, edgesPerNode: number, overlayRatio = 0.15) {
  const rnd = seeded(seed);
  const nodes: KnowledgeGraphNodeDto[] = [];
  const byLayer: string[][] = [];
  for (let l = 0; l < layers; l++) {
    byLayer.push([]);
    for (let i = 0; i < perLayer; i++) { const id = `L${l}N${i}`; byLayer[l].push(id); nodes.push(node(id, i)); }
  }
  const edges: KnowledgeGraphEdgeDto[] = [];
  for (let l = 1; l < layers; l++) for (const target of byLayer[l]) {
    const sources = new Set<string>();
    sources.add(byLayer[l - 1][Math.floor(rnd() * perLayer)]); // guarantees the rank
    for (let k = 1; k < edgesPerNode; k++) {
      const from = Math.floor(rnd() * l);
      sources.add(byLayer[from][Math.floor(rnd() * perLayer)]);
    }
    for (const s of sources) edges.push(edge(`${s}>${target}`, s, target));
  }
  const overlays = Math.floor(edges.length * overlayRatio);
  for (let k = 0; k < overlays; k++) {
    const a = nodes[Math.floor(rnd() * nodes.length)].nodeId, b = nodes[Math.floor(rnd() * nodes.length)].nodeId;
    if (a !== b) edges.push(edge(`${a}~${b}#${k}`, a, b, rnd() < 0.5 ? "RelatedTo" : "CausesErrorIn"));
  }
  return { nodes, edges };
}

export interface GeometryReport {
  nodeOverlaps: string[];
  edgeThroughCard: string[];
  sharedPaths: string[];
  outOfBounds: string[];
  nonOrthogonal: string[];
  crossings: number;
}

/** Exact geometric invariants of a routed scene (see plan §12 "Geometry"). */
export function inspectScene(scene: GraphScene): GeometryReport {
  const nodes = [...scene.nodes.values()];
  const report: GeometryReport = { nodeOverlaps: [], edgeThroughCard: [], sharedPaths: [], outOfBounds: [], nonOrthogonal: [], crossings: 0 };
  for (let i = 0; i < nodes.length; i++) for (let j = i + 1; j < nodes.length; j++)
    if (rectsOverlap(nodes[i], nodes[j])) report.nodeOverlaps.push(`${nodes[i].nodeId}/${nodes[j].nodeId}`);
  const segs: { edgeId: string; s: Segment }[] = [];
  for (const e of scene.edges) {
    for (const s of segmentsOf(e.points)) {
      segs.push({ edgeId: e.edgeId, s });
      if (Math.abs(s.a.x - s.b.x) > 1e-9 && Math.abs(s.a.y - s.b.y) > 1e-9) report.nonOrthogonal.push(e.edgeId);
      // Includes the edge's own endpoint cards: a route may touch them only at its port.
      for (const n of nodes) if (segmentIntersectsRect(s, n, 0.5)) report.edgeThroughCard.push(`${e.edgeId}@${n.nodeId}`);
    }
    if (!rectContains(scene.bounds, e.bounds)) report.outOfBounds.push(e.edgeId);
  }
  for (const n of nodes) if (!rectContains(scene.bounds, n)) report.outOfBounds.push(n.nodeId);
  for (let i = 0; i < segs.length; i++) for (let j = i + 1; j < segs.length; j++) {
    if (segs[i].edgeId === segs[j].edgeId) continue;
    if (collinearOverlap(segs[i].s, segs[j].s) > 0.5) report.sharedPaths.push(`${segs[i].edgeId}|${segs[j].edgeId}`);
    if (segmentsCross(segs[i].s, segs[j].s)) report.crossings++;
  }
  return report;
}

export interface LabelReport { labelLabel: string[]; labelNode: string[]; labelArrow: string[]; labelOtherEdge: string[]; outOfBounds: string[] }

export function inspectLabels(scene: GraphScene, placement: LabelPlacement, margin: number): LabelReport {
  const report: LabelReport = { labelLabel: [], labelNode: [], labelArrow: [], labelOtherEdge: [], outOfBounds: [] };
  const labels = [...placement.placed.values()];
  for (let i = 0; i < labels.length; i++) {
    const padded = inflateRect(labels[i], margin - 1e-6);
    for (let j = i + 1; j < labels.length; j++) if (rectsOverlap(padded, labels[j])) report.labelLabel.push(`${labels[i].edgeId}|${labels[j].edgeId}`);
    for (const n of scene.nodes.values()) if (rectsOverlap(padded, n)) report.labelNode.push(`${labels[i].edgeId}@${n.nodeId}`);
    for (const e of scene.edges) {
      const arrow = arrowheadRect(e);
      if (arrow && rectsOverlap(padded, arrow)) report.labelArrow.push(`${labels[i].edgeId}@${e.edgeId}`);
      if (e.edgeId === labels[i].edgeId) continue;
      for (const s of segmentsOf(e.points)) if (segmentIntersectsRect(s, labels[i], 0)) report.labelOtherEdge.push(`${labels[i].edgeId}@${e.edgeId}`);
    }
    if (!rectContains(scene.bounds, labels[i])) report.outOfBounds.push(labels[i].edgeId);
  }
  return report;
}

export function deepFreeze<T>(value: T): T {
  if (value && typeof value === "object") { Object.values(value).forEach(deepFreeze); Object.freeze(value); }
  return value;
}
