import type { GraphScene, SceneEdge } from "./knowledgeGraphScene";
import { SpatialGrid, inflateRect, rectsOverlap, segmentIntersectsRect, segmentLength, segmentsOf, type Point, type Rect, type Segment } from "./knowledgeGraphGeometry.ts";

/**
 * Weight-label placement. Candidates sit on long route segments (35/50/65% of a segment, centred
 * on the line or touching it on either side); corners, ports and arrowheads are avoided. A label
 * is accepted only if it does not touch any card, arrowhead, other label (with `margin`) or any
 * other edge's line. Edges are processed in the caller's priority order; edges without a free
 * spot are reported in `unplaced` so the UI can show a counter and a fallback panel.
 */

export interface EdgeLabelBox extends Rect { edgeId: string; text: string; cx: number; cy: number }

export interface LabelPlacement {
  placed: Map<string, EdgeLabelBox>;
  unplaced: string[];
  eligible: number;
}

export interface LabelPlacementOptions {
  fontSize?: number;
  paddingX?: number;
  height?: number;
  /** Clearance between a label and other labels/cards/arrowheads (graph units). */
  margin?: number;
  /** Upper bound of candidate tests per edge (bounded work on dense graphs). */
  maxCandidates?: number;
  /**
   * Leading entries of `orderedEdgeIds` (selected / focused / hovered / neighbourhood) placed in the
   * given order before anything else; remaining edges are placed most-constrained first.
   */
  priorityCount?: number;
}

const weightFormatter = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 });

/** Single formatter for canvas badges, accessible names and lists; inspector keeps the raw value. */
export function formatEdgeWeight(weight: number): string {
  return Number.isFinite(weight) ? weightFormatter.format(weight) : String(weight);
}

/** Deterministic fallback when the browser cannot measure text (tests, SSR, fonts pending). */
export function estimateTextWidth(text: string, fontSize: number): number {
  return Math.ceil(text.length * fontSize * 0.62);
}

const arrowLength = 13;
const arrowHalfWidth = 6;
const portGap = 8;

export function arrowheadRect(edge: SceneEdge): Rect | null {
  const n = edge.points.length;
  if (n < 2) return null;
  const end = edge.points[n - 1], before = edge.points[n - 2];
  const len = Math.hypot(end.x - before.x, end.y - before.y) || 1;
  const back = { x: end.x - ((end.x - before.x) / len) * arrowLength, y: end.y - ((end.y - before.y) / len) * arrowLength };
  const x = Math.min(end.x, back.x), y = Math.min(end.y, back.y);
  return inflateRect({ x, y, width: Math.abs(end.x - back.x), height: Math.abs(end.y - back.y) }, arrowHalfWidth);
}

interface Candidate { cx: number; cy: number; segment: number }

function candidatesFor(edge: SceneEdge, width: number, height: number, max: number): Candidate[] {
  const segments = segmentsOf(edge.points);
  const order = segments.map((s, i) => ({ s, i, len: segmentLength(s) }))
    .sort((a, b) => b.len - a.len || a.i - b.i);
  const out: Candidate[] = [];
  for (const { s, i, len } of order) {
    const horizontal = Math.abs(s.a.y - s.b.y) < 1e-9;
    const along = horizontal ? width : height;
    const startGap = i === 0 ? portGap : portGap / 2;
    const endGap = i === segments.length - 1 ? arrowLength + portGap : portGap / 2;
    const room = len - startGap - endGap - along;
    if (room < 0) continue;
    const ux = (s.b.x - s.a.x) / len, uy = (s.b.y - s.a.y) / len;
    const offsets = horizontal ? [0, -(height / 2 + 2), height / 2 + 2] : [0, width / 2 + 2, -(width / 2 + 2)];
    for (const fraction of [0.5, 0.35, 0.65, 0.15, 0.85]) {
      const dist = startGap + along / 2 + room * fraction;
      const base: Point = { x: s.a.x + ux * dist, y: s.a.y + uy * dist };
      for (const offset of offsets) {
        out.push(horizontal ? { cx: base.x, cy: base.y + offset, segment: i } : { cx: base.x + offset, cy: base.y, segment: i });
        if (out.length >= max) return out;
      }
    }
  }
  return out;
}

type Obstacle = { kind: "node" | "arrow" } | { kind: "segment"; edgeId: string; segment: Segment };
const obstacleCache = new WeakMap<GraphScene, SpatialGrid<Obstacle>>();

/** Cards, arrowheads and route segments never change for a scene: index them once and reuse. */
function staticObstacles(scene: GraphScene): SpatialGrid<Obstacle> {
  const cached = obstacleCache.get(scene);
  if (cached) return cached;
  const grid = new SpatialGrid<Obstacle>(96);
  for (const node of scene.nodes.values()) grid.insert(node, { kind: "node" });
  for (const edge of scene.edges) {
    const arrow = arrowheadRect(edge);
    if (arrow) grid.insert(arrow, { kind: "arrow" });
    for (const segment of segmentsOf(edge.points)) {
      grid.insert(inflateRect({ x: Math.min(segment.a.x, segment.b.x), y: Math.min(segment.a.y, segment.b.y),
        width: Math.abs(segment.a.x - segment.b.x), height: Math.abs(segment.a.y - segment.b.y) }, 1), { kind: "segment", edgeId: edge.edgeId, segment });
    }
  }
  obstacleCache.set(scene, grid);
  return grid;
}

export function placeEdgeLabels(
  scene: GraphScene,
  orderedEdgeIds: string[],
  measure: (text: string, fontSize: number) => number = estimateTextWidth,
  options: LabelPlacementOptions = {}
): LabelPlacement {
  const fontSize = options.fontSize ?? 12;
  const paddingX = options.paddingX ?? 7;
  const height = options.height ?? 20;
  const margin = options.margin ?? 4;
  const maxCandidates = options.maxCandidates ?? 60;
  if (!orderedEdgeIds.length) return { placed: new Map(), unplaced: [], eligible: 0 };
  const grid = staticObstacles(scene);

  // Phase 1: candidates that are free of static obstacles (cards, arrowheads, other edges' lines).
  const entries: { edgeId: string; index: number; text: string; boxes: Rect[] }[] = [];
  const seen = new Set<string>();
  orderedEdgeIds.forEach((edgeId) => {
    if (seen.has(edgeId)) return;
    const edge = scene.edgeById.get(edgeId);
    if (!edge) return;
    seen.add(edgeId);
    const text = formatEdgeWeight(edge.weight);
    const width = Math.max(height + 4, Math.ceil(measure(text, fontSize)) + 2 * paddingX);
    const candidates = edge.legacyMid ? [{ cx: edge.legacyMid.x, cy: edge.legacyMid.y, segment: -1 }] : candidatesFor(edge, width, height, maxCandidates);
    const boxes: Rect[] = [];
    for (const c of candidates) {
      const box: Rect = { x: c.cx - width / 2, y: c.cy - height / 2, width, height };
      const padded = inflateRect(box, margin);
      const blocked = grid.query(padded).some(({ rect, item }) => item.kind === "segment"
        ? item.edgeId !== edgeId && segmentIntersectsRect(item.segment, inflateRect(box, 1), 0)
        : rectsOverlap(padded, rect));
      if (!blocked) boxes.push(box);
    }
    entries.push({ edgeId, index: entries.length, text, boxes });
  });

  // Phase 2: priority edges first in the caller's order, then the most constrained edges first
  // (fewest free candidates), so a free-space edge does not take the only spot of a crowded one.
  const pinned = Math.min(options.priorityCount ?? 0, entries.length);
  const order = [...entries.slice(0, pinned),
    ...entries.slice(pinned).sort((a, b) => a.boxes.length - b.boxes.length || a.index - b.index)];
  const labels = new SpatialGrid<Rect>(96);
  const placed = new Map<string, EdgeLabelBox>();
  const unplaced: string[] = [];
  for (const entry of order) {
    const box = entry.boxes.find(b => !labels.query(inflateRect(b, margin)).some(({ rect }) => rectsOverlap(inflateRect(b, margin), rect)));
    if (!box) { unplaced.push(entry.edgeId); continue; }
    placed.set(entry.edgeId, { ...box, edgeId: entry.edgeId, text: entry.text, cx: box.x + box.width / 2, cy: box.y + box.height / 2 });
    labels.insert(box, box);
  }
  return { placed, unplaced, eligible: entries.length };
}
