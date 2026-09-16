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

  // Group nodes by their topological rank
  const rankGroups = new Map<number, KnowledgeGraphNodeDto[]>();
  let maxRank = 0;

  for (const node of nodes) {
    const r = rankMap.get(node.nodeId) ?? 0;
    if (r > maxRank) maxRank = r;
    if (!rankGroups.has(r)) {
      rankGroups.set(r, []);
    }
    rankGroups.get(r)!.push(node);
  }

  // Deterministically sort nodes within each rank column
  for (let r = 0; r <= maxRank; r++) {
    const group = rankGroups.get(r);
    if (!group) continue;
    group.sort((a, b) => {
      // 1. Primary: orderIndex
      const orderDiff = (a.orderIndex ?? 0) - (b.orderIndex ?? 0);
      if (orderDiff !== 0) return orderDiff;

      // 2. Secondary: nodeType hierarchy order
      const typeA = nodeTypeLayerOrder[a.nodeType] ?? 2;
      const typeB = nodeTypeLayerOrder[b.nodeType] ?? 2;
      if (typeA !== typeB) return typeA - typeB;

      // 3. Tertiary: nodeCode alphanumeric
      const codeDiff = (a.nodeCode ?? "").localeCompare(b.nodeCode ?? "");
      if (codeDiff !== 0) return codeDiff;

      // 4. Stable tie-breaker: nodeId
      return a.nodeId.localeCompare(b.nodeId);
    });
  }

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
