import type { KnowledgeGraphEdgeDto, KnowledgeGraphNodeDto, KnowledgeRelationType } from "../types/knowledgeGraph";
import { computeDeterministicDagLayout, computeEdgePath } from "./knowledgeGraphLayout.ts";
import { inflateRect, pointsBounds, unionRects, type Point, type Rect } from "./knowledgeGraphGeometry.ts";

/**
 * Graph view model: node rectangles, per-edge ports and orthogonal routes, and full bounds.
 * DTOs are never mutated and never receive coordinates; every route keeps its own edge ID.
 *
 * Routing contract (detail mode):
 * - Rank comes from PrerequisiteOf/PartOf only (see computeDeterministicDagLayout); overlay
 *   relations are routed around the placed cards without changing rank.
 * - Every edge owns one port slot on each endpoint side. Left and right slots sit on disjoint
 *   8-unit grids, so stubs of different edges are never collinear.
 * - Vertical pieces run in the corridors between columns on distinct lanes; pieces that must
 *   cross intermediate columns use horizontal tracks in the shared row gaps (or outside the grid).
 *   Routes therefore never pass through a card that is not their endpoint.
 * - A node side with more edges than port slots keeps backbone edges first; the rest are reported
 *   in `hiddenEdges` (reason "port-overflow") and stay reachable through the relation list.
 */

export type EdgeRouteKind = "adjacent" | "longForward" | "sameColumn" | "backward" | "selfLoop";

export interface SceneNode {
  nodeId: string;
  x: number;
  y: number;
  width: number;
  height: number;
  rank: number;
  row: number;
}

export interface SceneEdge {
  edgeId: string;
  sourceNodeId: string;
  targetNodeId: string;
  relationType: KnowledgeRelationType;
  weight: number;
  backbone: boolean;
  kind: EdgeRouteKind;
  /** Orthogonal polyline; empty for legacy curves. */
  points: Point[];
  d: string;
  bounds: Rect;
  /** Legacy fallback only: label anchor of the old Bezier midpoint. */
  legacyMid?: Point;
}

export interface HiddenSceneEdge { edgeId: string; reason: "port-overflow" }

export interface GraphScene {
  engine: "orthogonal-v1" | "legacy-bezier";
  nodes: Map<string, SceneNode>;
  edges: SceneEdge[];
  edgeById: Map<string, SceneEdge>;
  hiddenEdges: HiddenSceneEdge[];
  /** Edges whose endpoints are not part of the rendered node set. */
  invalidEdgeIds: string[];
  /** Cards + routes + arrowheads + label allowance. */
  bounds: Rect;
  cardWidth: number;
  cardHeight: number;
  rankCount: number;
  hasDependencyCycle: boolean;
}

export interface GraphSceneOptions {
  cardWidth: number;
  cardHeight: number;
  /** Row gap; rounded up so that (cardHeight + gapY) is a multiple of the port grid. */
  gapY?: number;
  clearance?: number;
  laneGap?: number;
  minCorridor?: number;
  /** Free strip on each side of a corridor's lanes (port stubs + weight badges). */
  labelRoom?: number;
  crossingMinimization?: boolean;
}

const portGrid = 8;
const portMarginTop = 12;
const trackClearance = 12;
const cornerRadius = 6;
const labelAllowance = 26;
const nearestChannels = 8;

const isBackbone = (t: KnowledgeRelationType) => t === "PrerequisiteOf" || t === "PartOf";

interface PortRequest { edgeId: string; otherY: number; backbone: boolean }
interface VerticalPiece { edgeId: string; corridor: number; y1: number; y2: number; lane: number }
interface RouteDraft {
  edge: KnowledgeGraphEdgeDto;
  kind: EdgeRouteKind;
  rs: number; rt: number;
  sy: number; ty: number;
  sourceSide: "L" | "R"; targetSide: "L" | "R";
  channelY?: number;
  pieces: VerticalPiece[];
}

export function computeGraphScene(
  nodes: KnowledgeGraphNodeDto[],
  edges: KnowledgeGraphEdgeDto[],
  options: GraphSceneOptions
): GraphScene {
  const w = options.cardWidth, h = options.cardHeight;
  const rawGapY = options.gapY ?? 50;
  const gapY = rawGapY + ((portGrid - ((h + rawGapY) % portGrid)) % portGrid);
  const clearance = options.clearance ?? 16;
  const laneGap = options.laneGap ?? 8;
  const minCorridor = options.minCorridor ?? 120;
  const labelRoom = options.labelRoom ?? 56;
  const pitch = h + gapY;

  const layout = computeDeterministicDagLayout(nodes, edges, {
    cardWidth: w, cardHeight: h, gapX: 0, gapY, paddingX: 0, paddingY: 0, minWidth: 0, minHeight: 0,
    crossingMinimization: options.crossingMinimization ?? true,
  });
  const nodeIds = new Set(nodes.map(n => n.nodeId));
  const rankOf = (id: string) => layout.positions.get(id)!.rank;
  const rowOf = (id: string) => layout.positions.get(id)!.row;
  const top = (id: string) => rowOf(id) * pitch;
  const centerY = (id: string) => top(id) + h / 2;
  let maxRow = 0;
  for (const p of layout.positions.values()) maxRow = Math.max(maxRow, p.row);
  const rowCount = nodes.length ? maxRow + 1 : 0;

  const invalidEdgeIds: string[] = [];
  const valid: KnowledgeGraphEdgeDto[] = [];
  for (const edge of edges) {
    if (nodeIds.has(edge.sourceNodeId) && nodeIds.has(edge.targetNodeId)) valid.push(edge);
    else invalidEdgeIds.push(edge.edgeId);
  }
  valid.sort((a, b) => a.edgeId.localeCompare(b.edgeId));

  const classify = (edge: KnowledgeGraphEdgeDto) => {
    const rs = rankOf(edge.sourceNodeId), rt = rankOf(edge.targetNodeId);
    const kind: EdgeRouteKind = edge.sourceNodeId === edge.targetNodeId ? "selfLoop"
      : rt === rs + 1 ? "adjacent" : rt > rs ? "longForward" : rt === rs ? "sameColumn" : "backward";
    const sourceSide: "L" | "R" = kind === "backward" ? "L" : "R";
    const targetSide: "L" | "R" = kind === "adjacent" || kind === "longForward" ? "L" : "R";
    return { rs, rt, kind, sourceSide, targetSide };
  };

  // ---- Ports: one slot per edge end; overflow hides lowest-priority edges (never silently).
  const slotCount = Math.floor((h - portMarginTop - (portMarginTop + portGrid / 2)) / portGrid) + 1;
  const hidden = new Set<string>();
  const buildRequests = () => {
    const sides = new Map<string, PortRequest[]>();
    const add = (key: string, req: PortRequest) => {
      let list = sides.get(key);
      if (!list) sides.set(key, (list = []));
      list.push(req);
    };
    for (const edge of valid) {
      if (hidden.has(edge.edgeId)) continue;
      const c = classify(edge);
      const backbone = isBackbone(edge.relationType);
      if (c.kind === "selfLoop") {
        add(`${edge.sourceNodeId}:R`, { edgeId: `${edge.edgeId}#out`, otherY: centerY(edge.sourceNodeId) - 0.5, backbone });
        add(`${edge.targetNodeId}:R`, { edgeId: `${edge.edgeId}#in`, otherY: centerY(edge.targetNodeId) + 0.5, backbone });
        continue;
      }
      add(`${edge.sourceNodeId}:${c.sourceSide}`, { edgeId: `${edge.edgeId}#out`, otherY: centerY(edge.targetNodeId), backbone });
      add(`${edge.targetNodeId}:${c.targetSide}`, { edgeId: `${edge.edgeId}#in`, otherY: centerY(edge.sourceNodeId), backbone });
    }
    return sides;
  };
  const byPriority = (a: PortRequest, b: PortRequest) => Number(b.backbone) - Number(a.backbone) || a.edgeId.localeCompare(b.edgeId);
  for (const list of buildRequests().values()) {
    if (list.length <= slotCount) continue;
    [...list].sort(byPriority).slice(slotCount).forEach(r => hidden.add(r.edgeId.replace(/#(out|in)$/, "")));
  }
  const portY = new Map<string, number>();
  for (const [key, list] of buildRequests()) {
    const [nodeId, side] = [key.slice(0, key.lastIndexOf(":")), key.slice(key.lastIndexOf(":") + 1)];
    list.sort((a, b) => a.otherY - b.otherY || byPriority(a, b));
    const n = list.length;
    const step = n <= 1 ? 0 : Math.min(2, Math.floor((slotCount - 1) / (n - 1)));
    const start = Math.floor((slotCount - 1 - (n - 1) * step) / 2);
    const base = top(nodeId) + portMarginTop + (side === "L" ? portGrid / 2 : 0);
    list.forEach((req, i) => portY.set(req.edgeId, base + portGrid * (start + i * step)));
  }

  // ---- Drafts and horizontal channel tracks (row gaps shared by every column).
  const drafts: RouteDraft[] = [];
  for (const edge of valid) {
    if (hidden.has(edge.edgeId)) continue;
    const c = classify(edge);
    const sy = portY.get(`${edge.edgeId}#out`)!;
    let ty = portY.get(`${edge.edgeId}#in`)!;
    // Left/right slots are offset by half a grid step; a near-level adjacent edge is drawn straight
    // at the source slot height (still unique: no other stub shares that y in this corridor).
    if (c.kind === "adjacent" && Math.abs(sy - ty) <= portGrid / 2) ty = sy;
    drafts.push({ edge, ...c, sy, ty, pieces: [] });
  }
  const interiorCapacity = Math.max(1, Math.floor((gapY - 2 * trackClearance) / laneGap) + 1);
  const tracks = new Map<number, { lo: number; hi: number }[][]>();
  const channelCenter = (k: number) => k === 0 ? -trackClearance : k >= rowCount ? (rowCount - 1) * pitch + h + trackClearance : (k - 1) * pitch + h + gapY / 2;
  const trackY = (k: number, t: number) => k === 0 ? -trackClearance - t * laneGap
    : k >= rowCount ? (rowCount - 1) * pitch + h + trackClearance + t * laneGap
    : (k - 1) * pitch + h + trackClearance + t * laneGap;
  const reserveTrack = (lo: number, hi: number, desiredY: number): number => {
    const interior = Array.from({ length: Math.max(0, rowCount - 1) }, (_, i) => i + 1)
      .sort((a, b) => Math.abs(channelCenter(a) - desiredY) - Math.abs(channelCenter(b) - desiredY) || a - b)
      .slice(0, nearestChannels);
    const outer = Math.abs(channelCenter(0) - desiredY) <= Math.abs(channelCenter(rowCount) - desiredY) ? [0, rowCount] : [rowCount, 0];
    for (const k of [...interior, ...outer]) {
      let channel = tracks.get(k);
      if (!channel) tracks.set(k, (channel = []));
      const capacity = k === 0 || k >= rowCount ? Infinity : interiorCapacity;
      for (let t = 0; t < Math.min(capacity, channel.length + 1); t++) {
        const used = channel[t] ?? (channel[t] = []);
        if (used.every(iv => hi < iv.lo || lo > iv.hi)) { used.push({ lo, hi }); return trackY(k, t); }
      }
    }
    return trackY(rowCount, 0); // unreachable: outer channels are unbounded
  };
  const channelOrder = [...drafts].sort((a, b) =>
    Number(isBackbone(b.edge.relationType)) - Number(isBackbone(a.edge.relationType)) || a.edge.edgeId.localeCompare(b.edge.edgeId));
  for (const d of channelOrder) {
    if (d.kind === "longForward") {
      d.channelY = reserveTrack(d.rs, d.rt - 1, (d.sy + d.ty) / 2);
    } else if (d.kind === "backward" && d.rs - d.rt >= 2) {
      d.channelY = reserveTrack(d.rt, d.rs - 1, (d.sy + d.ty) / 2);
    }
  }

  // ---- Vertical pieces per corridor (corridor c lies between rank c and rank c + 1).
  for (const d of drafts) {
    const piece = (corridor: number, y1: number, y2: number) => d.pieces.push({ edgeId: d.edge.edgeId, corridor, y1, y2, lane: 0 });
    if (d.kind === "adjacent") { if (d.sy !== d.ty) piece(d.rs, d.sy, d.ty); }
    else if (d.kind === "sameColumn" || d.kind === "selfLoop") piece(d.rs, d.sy, d.ty);
    else if (d.kind === "longForward") { piece(d.rs, d.sy, d.channelY!); piece(d.rt - 1, d.channelY!, d.ty); }
    else if (d.channelY === undefined) piece(d.rt, d.sy, d.ty);
    else { piece(d.rs - 1, d.sy, d.channelY); piece(d.rt, d.channelY, d.ty); }
  }
  const corridorPieces = new Map<number, VerticalPiece[]>();
  for (const d of drafts) for (const p of d.pieces) {
    let list = corridorPieces.get(p.corridor);
    if (!list) corridorPieces.set(p.corridor, (list = []));
    list.push(p);
  }
  const laneCount = new Map<number, number>();
  for (const [corridor, list] of corridorPieces) {
    // Downward pieces that start later take inner (left) lanes; upward pieces mirror this.
    const down = list.filter(p => p.y2 > p.y1).sort((a, b) => b.y1 - a.y1 || a.edgeId.localeCompare(b.edgeId));
    const up = list.filter(p => p.y2 <= p.y1).sort((a, b) => a.y1 - b.y1 || a.edgeId.localeCompare(b.edgeId));
    const lanes: { lo: number; hi: number }[][] = [];
    for (const p of [...down, ...up]) {
      const lo = Math.min(p.y1, p.y2), hi = Math.max(p.y1, p.y2);
      let lane = lanes.findIndex(used => used.every(iv => hi + laneGap < iv.lo || lo - laneGap > iv.hi));
      if (lane < 0) { lane = lanes.length; lanes.push([]); }
      lanes[lane].push({ lo, hi });
      p.lane = lane;
    }
    laneCount.set(corridor, lanes.length);
  }

  // ---- Column x positions from corridor widths (corridors widen with their lane count).
  const rankCount = layout.rankCount;
  // Lanes are centred; the free strip between cards and the outermost lanes is where port stubs
  // carry their weight badges, so it grows to `labelRoom` once a corridor has any lane.
  const corridorWidth = (c: number) => {
    const lanes = laneCount.get(c) ?? 0;
    const needed = lanes ? 2 * Math.max(clearance, labelRoom) + (lanes - 1) * laneGap : 0;
    return c >= rankCount - 1 ? needed : Math.max(minCorridor, needed);
  };
  const colX: number[] = [0];
  for (let r = 1; r < rankCount; r++) colX[r] = colX[r - 1] + w + corridorWidth(r - 1);
  const laneX = (corridor: number, lane: number) => {
    const lanes = laneCount.get(corridor) ?? 1;
    const left = colX[corridor] + w;
    return left + (corridorWidth(corridor) - (lanes - 1) * laneGap) / 2 + lane * laneGap;
  };

  const sceneNodes = new Map<string, SceneNode>();
  for (const node of nodes) {
    const p = layout.positions.get(node.nodeId)!;
    sceneNodes.set(node.nodeId, { nodeId: node.nodeId, x: colX[p.rank], y: p.row * pitch, width: w, height: h, rank: p.rank, row: p.row });
  }

  const sceneEdges: SceneEdge[] = drafts.map(d => {
    const s = sceneNodes.get(d.edge.sourceNodeId)!, t = sceneNodes.get(d.edge.targetNodeId)!;
    const sx = d.sourceSide === "R" ? s.x + w : s.x;
    const tx = d.targetSide === "R" ? t.x + w : t.x;
    const lx = d.pieces.map(p => laneX(p.corridor, p.lane));
    let points: Point[];
    if (!d.pieces.length) points = [{ x: sx, y: d.sy }, { x: tx, y: d.ty }];
    else if (d.pieces.length === 1) points = [{ x: sx, y: d.sy }, { x: lx[0], y: d.sy }, { x: lx[0], y: d.ty }, { x: tx, y: d.ty }];
    else points = [{ x: sx, y: d.sy }, { x: lx[0], y: d.sy }, { x: lx[0], y: d.channelY! },
      { x: lx[1], y: d.channelY! }, { x: lx[1], y: d.ty }, { x: tx, y: d.ty }];
    points = simplifyOrthogonal(points);
    return {
      edgeId: d.edge.edgeId, sourceNodeId: d.edge.sourceNodeId, targetNodeId: d.edge.targetNodeId,
      relationType: d.edge.relationType, weight: d.edge.weight, backbone: isBackbone(d.edge.relationType),
      kind: d.kind, points, d: roundedOrthogonalPath(points, cornerRadius), bounds: inflateRect(pointsBounds(points), 6),
    };
  });

  const bounds = unionRects([...[...sceneNodes.values()].map(n => ({ x: n.x, y: n.y, width: n.width, height: n.height })),
    ...sceneEdges.map(e => e.bounds)]);
  const hasDependencyCycle = sceneEdges.some(e => e.backbone && (e.kind === "backward" || e.kind === "sameColumn"));
  return {
    engine: "orthogonal-v1", nodes: sceneNodes, edges: sceneEdges, edgeById: new Map(sceneEdges.map(e => [e.edgeId, e])),
    hiddenEdges: [...hidden].sort().map(edgeId => ({ edgeId, reason: "port-overflow" as const })),
    invalidEdgeIds, bounds: bounds ? inflateRect(bounds, labelAllowance) : { x: 0, y: 0, width: 0, height: 0 },
    cardWidth: w, cardHeight: h, rankCount, hasDependencyCycle,
  };
}

/** Rollback path: previous grid layout + Bezier curves, wrapped in the same view model. */
export function computeLegacyGraphScene(
  nodes: KnowledgeGraphNodeDto[],
  edges: KnowledgeGraphEdgeDto[],
  options: { cardWidth: number; cardHeight: number }
): GraphScene {
  const { cardWidth: w, cardHeight: h } = options;
  const layout = computeDeterministicDagLayout(nodes, edges, { cardWidth: w, cardHeight: h, gapX: 85, gapY: 28, paddingX: 50, paddingY: 50, minWidth: 340, minHeight: 200 });
  const sceneNodes = new Map<string, SceneNode>();
  for (const node of nodes) {
    const p = layout.positions.get(node.nodeId)!;
    sceneNodes.set(node.nodeId, { nodeId: node.nodeId, x: p.x, y: p.y, width: w, height: h, rank: p.rank, row: p.row });
  }
  const invalidEdgeIds: string[] = [];
  const sceneEdges: SceneEdge[] = [];
  for (const edge of edges) {
    const s = layout.positions.get(edge.sourceNodeId), t = layout.positions.get(edge.targetNodeId);
    if (!s || !t) { invalidEdgeIds.push(edge.edgeId); continue; }
    const path = computeEdgePath(s, t, w, h);
    const nums = (path.d.match(/-?\d+(\.\d+)?/g) ?? []).map(Number);
    const pts: Point[] = [];
    for (let i = 0; i + 1 < nums.length; i += 2) pts.push({ x: nums[i], y: nums[i + 1] });
    sceneEdges.push({ edgeId: edge.edgeId, sourceNodeId: edge.sourceNodeId, targetNodeId: edge.targetNodeId,
      relationType: edge.relationType, weight: edge.weight, backbone: isBackbone(edge.relationType), kind: "adjacent",
      points: [], d: path.d, bounds: inflateRect(pointsBounds(pts), 6), legacyMid: { x: path.midX, y: path.midY } });
  }
  const bounds = unionRects([{ x: 0, y: 0, width: layout.width, height: layout.height }, ...sceneEdges.map(e => e.bounds)])!;
  return { engine: "legacy-bezier", nodes: sceneNodes, edges: sceneEdges, edgeById: new Map(sceneEdges.map(e => [e.edgeId, e])),
    hiddenEdges: [], invalidEdgeIds, bounds, cardWidth: w, cardHeight: h, rankCount: layout.rankCount, hasDependencyCycle: false };
}

/** Removes duplicate and collinear interior points of an orthogonal polyline. */
export function simplifyOrthogonal(points: Point[]): Point[] {
  const out: Point[] = [];
  for (const p of points) {
    const last = out[out.length - 1];
    if (last && Math.abs(last.x - p.x) < 1e-9 && Math.abs(last.y - p.y) < 1e-9) continue;
    if (out.length >= 2) {
      const a = out[out.length - 2], b = last!;
      const collinear = (Math.abs(a.x - b.x) < 1e-9 && Math.abs(b.x - p.x) < 1e-9) || (Math.abs(a.y - b.y) < 1e-9 && Math.abs(b.y - p.y) < 1e-9);
      if (collinear) { out[out.length - 1] = p; continue; }
    }
    out.push(p);
  }
  return out;
}

const fmt = (n: number) => Number(n.toFixed(2));

/**
 * SVG path with rounded corners. Each corner arc stays inside the bounding box of its two
 * adjacent orthogonal segments, so smoothing cannot cut into an obstacle the polyline avoids.
 */
export function roundedOrthogonalPath(points: Point[], radius: number): string {
  if (!points.length) return "";
  let d = `M ${fmt(points[0].x)} ${fmt(points[0].y)}`;
  for (let i = 1; i < points.length - 1; i++) {
    const prev = points[i - 1], cur = points[i], next = points[i + 1];
    const lenIn = Math.hypot(cur.x - prev.x, cur.y - prev.y), lenOut = Math.hypot(next.x - cur.x, next.y - cur.y);
    const r = Math.min(radius, lenIn / 2, lenOut / 2);
    const inX = cur.x - ((cur.x - prev.x) / lenIn) * r, inY = cur.y - ((cur.y - prev.y) / lenIn) * r;
    const outX = cur.x + ((next.x - cur.x) / lenOut) * r, outY = cur.y + ((next.y - cur.y) / lenOut) * r;
    d += ` L ${fmt(inX)} ${fmt(inY)} Q ${fmt(cur.x)} ${fmt(cur.y)} ${fmt(outX)} ${fmt(outY)}`;
  }
  const last = points[points.length - 1];
  return `${d} L ${fmt(last.x)} ${fmt(last.y)}`;
}

/** Structured-clone friendly form of a scene (Maps flattened) for worker transfer. */
export interface SerializedGraphScene extends Omit<GraphScene, "nodes" | "edgeById"> { nodes: SceneNode[] }

export function serializeGraphScene(scene: GraphScene): SerializedGraphScene {
  const { nodes, edgeById: _edgeById, ...rest } = scene;
  void _edgeById;
  return { ...rest, nodes: [...nodes.values()] };
}

export function deserializeGraphScene(data: SerializedGraphScene): GraphScene {
  return { ...data, nodes: new Map(data.nodes.map(n => [n.nodeId, n])), edgeById: new Map(data.edges.map(e => [e.edgeId, e])) };
}
