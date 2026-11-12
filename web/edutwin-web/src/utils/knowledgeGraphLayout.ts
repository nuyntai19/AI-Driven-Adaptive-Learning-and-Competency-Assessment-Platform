import type {
  KnowledgeGraphNodeDto,
  KnowledgeGraphEdgeDto,
  KnowledgeNodeType,
} from "../types/knowledgeGraph";

export interface NodePosition {
  x: number;
  y: number;
  rank: number;
  row: number;
}

export interface DagLayoutOptions {
  cardWidth?: number;
  cardHeight?: number;
  gapX?: number;
  gapY?: number;
  paddingX?: number;
  paddingY?: number;
  minWidth?: number;
  minHeight?: number;
  /**
   * Reorders nodes inside each rank with bounded barycenter sweeps to reduce edge crossings.
   * Ordering contract: siblings sharing a non-null parentNodeId keep their academic order
   * (orderIndex → type → code → id) as a hard constraint and stay contiguous; sibling blocks
   * and parentless nodes may be permuted. Ranks never change.
   */
  crossingMinimization?: boolean;
}

export interface DagLayoutResult {
  positions: Map<string, NodePosition>;
  width: number;
  height: number;
  rankCount: number;
}

export interface EdgePathResult {
  d: string;
  midX: number;
  midY: number;
}

const nodeTypeLayerOrder: Record<KnowledgeNodeType, number> = {
  Subject: 0,
  Chapter: 1,
  Topic: 2,
  Skill: 3,
  Concept: 4,
};

/**
 * Computes a deterministic DAG topological layout based on nodes and edges.
 *
 * Requirements:
 * 1. Kahn / longest-path topological ranking on DAG in true O(V + E) time.
 * 2. Only dependency edges (PrerequisiteOf, PartOf) advance rank: rank(v) >= rank(u) + 1,
 *    aligning with backend KnowledgeGraphValidator.
 * 3. RelatedTo and CausesErrorIn edges DO NOT advance topological rank.
 * 4. Deterministic fail-closed cycle handling in O(V + E): cycle nodes are assigned fallback rank (maxRank + 1)
 *    and sorted deterministically to prevent infinite loops.
 * 5. Deterministic sorting within each column/rank.
 * 6. Never mutates, creates, or stores fake coordinate fields on node/edge DTOs.
 */
export function computeDeterministicDagLayout(
  nodes: KnowledgeGraphNodeDto[],
  edges: KnowledgeGraphEdgeDto[],
  options?: DagLayoutOptions
): DagLayoutResult {
  const cardWidth = options?.cardWidth ?? 190;
  const cardHeight = options?.cardHeight ?? 75;
  const gapX = options?.gapX ?? 80;
  const gapY = options?.gapY ?? 35;
  const paddingX = options?.paddingX ?? 60;
  const paddingY = options?.paddingY ?? 60;
  const minWidth = options?.minWidth ?? 1000;
  const minHeight = options?.minHeight ?? 650;

  if (!nodes || nodes.length === 0) {
    return {
      positions: new Map<string, NodePosition>(),
      width: minWidth,
      height: minHeight,
      rankCount: 0,
    };
  }

  const validNodeIds = new Set(nodes.map((n) => n.nodeId));

  // Build graph for rank calculation:
  // Only PrerequisiteOf and PartOf are DAG dependency edges that advance topological rank,
  // matching backend KnowledgeGraphValidator.cs.
  const inDegree = new Map<string, number>();
  const outAdj = new Map<string, string[]>();

  for (const node of nodes) {
    inDegree.set(node.nodeId, 0);
    outAdj.set(node.nodeId, []);
  }

  for (const edge of edges) {
    if (!validNodeIds.has(edge.sourceNodeId) || !validNodeIds.has(edge.targetNodeId)) {
      continue;
    }
    if (edge.sourceNodeId === edge.targetNodeId) {
      continue; // Ignore self-loops for rank computation
    }

    // Only PrerequisiteOf and PartOf form DAG dependency hierarchy
    if (edge.relationType !== "PrerequisiteOf" && edge.relationType !== "PartOf") {
      continue;
    }

    inDegree.set(edge.targetNodeId, (inDegree.get(edge.targetNodeId) ?? 0) + 1);
    outAdj.get(edge.sourceNodeId)!.push(edge.targetNodeId);
  }

  // Pre-sort adjacency lists for deterministic traversal
  for (const neighbors of outAdj.values()) {
    neighbors.sort((a, b) => a.localeCompare(b));
  }

  // Topological ranking with longest-path tracking
  const rankMap = new Map<string, number>();
  for (const node of nodes) {
    rankMap.set(node.nodeId, 0);
  }

  const currentInDegree = new Map(inDegree);
  // Deterministic initial queue sorted once
  const queue: string[] = nodes
    .filter((n) => currentInDegree.get(n.nodeId) === 0)
    .map((n) => n.nodeId)
    .sort((a, b) => a.localeCompare(b));

  let head = 0;
  let visitedCount = 0;

  // True O(V + E) traversal using queue index without expensive array shifts or inner sorting
  while (head < queue.length) {
    const u = queue[head++];
    visitedCount++;
    const uRank = rankMap.get(u) ?? 0;
    const neighbors = outAdj.get(u) ?? [];

    for (const v of neighbors) {
      const vCurrentRank = rankMap.get(v) ?? 0;
      if (uRank + 1 > vCurrentRank) {
        rankMap.set(v, uRank + 1);
      }
      const remainingDeg = (currentInDegree.get(v) ?? 1) - 1;
      currentInDegree.set(v, remainingDeg);
      if (remainingDeg === 0) {
        queue.push(v);
      }
    }
  }

  // Fail-closed cycle handling:
  // If cycles exist in dependency edges, nodes with remaining in-degree > 0
  // are assigned to a fallback rank (maxRank + 1).
  if (visitedCount < nodes.length) {
    let maxRank = 0;
    for (const r of rankMap.values()) {
      if (r > maxRank) maxRank = r;
    }
    const fallbackRank = maxRank + 1;
    for (const node of nodes) {
      if ((currentInDegree.get(node.nodeId) ?? 0) > 0) {
        rankMap.set(node.nodeId, fallbackRank);
      }
    }
  }

  // Group nodes by topological rank and order each column deterministically:
  // orderIndex → nodeType → nodeCode → nodeId, optionally followed by bounded crossing reduction.
  const rankGroups = orderRankGroups(nodes, rankMap, edges, options?.crossingMinimization ?? false);
  let maxRank = 0;
  for (const r of rankGroups.keys()) if (r > maxRank) maxRank = r;

  // Assign 2D coordinates (x, y)
  const positions = new Map<string, NodePosition>();
  let maxNodesInRank = 0;

  for (let r = 0; r <= maxRank; r++) {
    const group = rankGroups.get(r) ?? [];
    if (group.length > maxNodesInRank) {
      maxNodesInRank = group.length;
    }

    const x = paddingX + r * (cardWidth + gapX);

    group.forEach((node, rowIndex) => {
      const y = paddingY + rowIndex * (cardHeight + gapY);
      positions.set(node.nodeId, {
        x,
        y,
        rank: r,
        row: rowIndex,
      });
    });
  }

  const rankCount = maxRank + 1;
  const computedWidth = paddingX * 2 + rankCount * (cardWidth + gapX) - gapX;
  const computedHeight = paddingY * 2 + maxNodesInRank * (cardHeight + gapY) - gapY;

  return {
    positions,
    width: Math.max(minWidth, computedWidth),
    height: Math.max(minHeight, computedHeight),
    rankCount,
  };
}

function academicCompare(a: KnowledgeGraphNodeDto, b: KnowledgeGraphNodeDto): number {
  const orderDiff = (a.orderIndex ?? 0) - (b.orderIndex ?? 0);
  if (orderDiff !== 0) return orderDiff;
  const typeA = nodeTypeLayerOrder[a.nodeType] ?? 2;
  const typeB = nodeTypeLayerOrder[b.nodeType] ?? 2;
  if (typeA !== typeB) return typeA - typeB;
  const codeDiff = (a.nodeCode ?? "").localeCompare(b.nodeCode ?? "");
  if (codeDiff !== 0) return codeDiff;
  return a.nodeId.localeCompare(b.nodeId);
}

const crossingSweeps = 6;

/** Groups nodes by rank; optionally reorders sibling blocks with barycenter sweeps. */
function orderRankGroups(
  nodes: KnowledgeGraphNodeDto[],
  rankMap: Map<string, number>,
  edges: KnowledgeGraphEdgeDto[],
  crossingMinimization: boolean
): Map<number, KnowledgeGraphNodeDto[]> {
  const rankGroups = new Map<number, KnowledgeGraphNodeDto[]>();
  for (const node of nodes) {
    const r = rankMap.get(node.nodeId) ?? 0;
    let group = rankGroups.get(r);
    if (!group) rankGroups.set(r, (group = []));
    group.push(node);
  }
  for (const group of rankGroups.values()) group.sort(academicCompare);
  if (!crossingMinimization || rankGroups.size < 2) return rankGroups;

  // Undirected neighbours across different ranks; overlay relations count half.
  const neighbours = new Map<string, { id: string; w: number }[]>();
  for (const node of nodes) neighbours.set(node.nodeId, []);
  for (const edge of edges) {
    const a = neighbours.get(edge.sourceNodeId), b = neighbours.get(edge.targetNodeId);
    if (!a || !b || edge.sourceNodeId === edge.targetNodeId) continue;
    if (rankMap.get(edge.sourceNodeId) === rankMap.get(edge.targetNodeId)) continue;
    const w = edge.relationType === "PrerequisiteOf" || edge.relationType === "PartOf" ? 1 : 0.5;
    a.push({ id: edge.targetNodeId, w });
    b.push({ id: edge.sourceNodeId, w });
  }
  const ranks = [...rankGroups.keys()].sort((a, b) => a - b);
  const position = new Map<string, number>();
  const refresh = (r: number) => rankGroups.get(r)!.forEach((n, i) => position.set(n.nodeId, i));
  ranks.forEach(refresh);

  const reorder = (r: number, includesRank: (other: number) => boolean) => {
    const blocks = new Map<string, KnowledgeGraphNodeDto[]>();
    for (const node of rankGroups.get(r)!) {
      const key = node.parentNodeId ? `p:${node.parentNodeId}` : `n:${node.nodeId}`;
      let block = blocks.get(key);
      if (!block) blocks.set(key, (block = []));
      block.push(node);
    }
    const scored = [...blocks.values()].map((block) => {
      let sum = 0, weight = 0;
      for (const node of block) for (const nb of neighbours.get(node.nodeId)!) {
        if (!includesRank(rankMap.get(nb.id) ?? 0)) continue;
        sum += (position.get(nb.id) ?? 0) * nb.w; weight += nb.w;
      }
      const current = position.get(block[0].nodeId) ?? 0;
      // Blocks without neighbours on the reference side keep their current slot.
      return { block, key: weight > 0 ? sum / weight : current, current };
    });
    scored.sort((a, b) => a.key - b.key || a.current - b.current);
    rankGroups.set(r, scored.flatMap((s) => s.block));
    refresh(r);
  };

  // Keep the best ordering seen (sweeps can oscillate); the academic order is the baseline.
  const pairs = crossingPairs(edges, rankMap, neighbours);
  const snapshot = () => new Map([...rankGroups].map(([r, g]) => [r, [...g]]));
  let best = snapshot();
  let bestCrossings = countOrderCrossings(pairs, position);
  for (let sweep = 0; sweep < crossingSweeps && bestCrossings > 0; sweep++) {
    if (sweep % 2 === 0) for (const r of ranks.slice(1)) reorder(r, (o) => o < r);
    else for (const r of ranks.slice(0, -1).reverse()) reorder(r, (o) => o > r);
    const crossings = countOrderCrossings(pairs, position);
    if (crossings < bestCrossings) { bestCrossings = crossings; best = snapshot(); }
  }
  return best;
}

/** Edge endpoint pairs grouped by (lowRank, highRank); only same-span edges are compared. */
function crossingPairs(edges: KnowledgeGraphEdgeDto[], rankMap: Map<string, number>, known: Map<string, unknown>) {
  const groups = new Map<string, [string, string][]>();
  for (const edge of edges) {
    if (!known.has(edge.sourceNodeId) || !known.has(edge.targetNodeId)) continue;
    const rs = rankMap.get(edge.sourceNodeId) ?? 0, rt = rankMap.get(edge.targetNodeId) ?? 0;
    if (rs === rt) continue;
    const [a, b] = rs < rt ? [edge.sourceNodeId, edge.targetNodeId] : [edge.targetNodeId, edge.sourceNodeId];
    const key = `${Math.min(rs, rt)}:${Math.max(rs, rt)}`;
    let group = groups.get(key);
    if (!group) groups.set(key, (group = []));
    group.push([a, b]);
  }
  return [...groups.values()];
}

const maxCrossingGroup = 1500;

function countOrderCrossings(groups: [string, string][][], position: Map<string, number>): number {
  let total = 0;
  for (const group of groups) {
    if (group.length > maxCrossingGroup) continue; // bounded work on very dense rank pairs
    for (let i = 0; i < group.length; i++) {
      const a1 = position.get(group[i][0])!, b1 = position.get(group[i][1])!;
      for (let j = i + 1; j < group.length; j++) {
        const a2 = position.get(group[j][0])!, b2 = position.get(group[j][1])!;
        if ((a1 - a2) * (b1 - b2) < 0) total++;
      }
    }
  }
  return total;
}

/**
 * Computes smooth SVG Bezier curve and label midpoint between two nodes.
 * Handles left-to-right (forward), same-column (lateral arc), and backward edges.
 */
export function computeEdgePath(
  sourcePos: { x: number; y: number },
  targetPos: { x: number; y: number },
  cardWidth = 190,
  cardHeight = 75
): EdgePathResult {
  const isForward = sourcePos.x + cardWidth <= targetPos.x;
  const isSameColumn = Math.abs(sourcePos.x - targetPos.x) < 10;

  if (isForward) {
    // Normal forward flow: Source Right -> Target Left
    const startX = sourcePos.x + cardWidth;
    const startY = sourcePos.y + cardHeight / 2;
    const endX = targetPos.x;
    const endY = targetPos.y + cardHeight / 2;

    const dx = endX - startX;
    const curveOffset = Math.max(35, dx * 0.45);

    const cp1x = startX + curveOffset;
    const cp1y = startY;
    const cp2x = endX - curveOffset;
    const cp2y = endY;

    const d = `M ${startX} ${startY} C ${cp1x} ${cp1y}, ${cp2x} ${cp2y}, ${endX} ${endY}`;
    const midX = (startX + endX) / 2;
    const midY = (startY + endY) / 2;

    return { d, midX, midY };
  }

  if (isSameColumn) {
    // Same column lateral arc: route outward to the right so it doesn't overlap cards
    const startX = sourcePos.x + cardWidth;
    const startY = sourcePos.y + cardHeight / 2;
    const endX = targetPos.x + cardWidth;
    const endY = targetPos.y + cardHeight / 2;

    const distY = Math.abs(endY - startY);
    const arcOffsetX = Math.min(65, 35 + distY * 0.15);

    const cp1x = startX + arcOffsetX;
    const cp1y = startY;
    const cp2x = endX + arcOffsetX;
    const cp2y = endY;

    const d = `M ${startX} ${startY} C ${cp1x} ${cp1y}, ${cp2x} ${cp2y}, ${endX} ${endY}`;
    const midX = startX + arcOffsetX * 0.75;
    const midY = (startY + endY) / 2;

    return { d, midX, midY };
  }

  // Backward flow: Source Left -> Target Right (looping back)
  const startX = sourcePos.x;
  const startY = sourcePos.y + cardHeight / 2;
  const endX = targetPos.x + cardWidth;
  const endY = targetPos.y + cardHeight / 2;

  const dx = Math.abs(startX - endX);
  const curveOffset = Math.max(45, dx * 0.4);

  const cp1x = startX - curveOffset;
  const cp1y = startY;
  const cp2x = endX + curveOffset;
  const cp2y = endY;

  const d = `M ${startX} ${startY} C ${cp1x} ${cp1y}, ${cp2x} ${cp2y}, ${endX} ${endY}`;
  const midX = (startX + endX) / 2;
  const midY = (startY + endY) / 2;

  return { d, midX, midY };
}
