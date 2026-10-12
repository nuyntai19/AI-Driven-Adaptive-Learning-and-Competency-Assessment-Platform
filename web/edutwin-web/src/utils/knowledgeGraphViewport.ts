import type { KnowledgeGraphNodeDto, KnowledgeGraphEdgeDto } from "../types/knowledgeGraph";

export interface GraphCamera { x: number; y: number; scale: number }
export interface GraphSize { width: number; height: number }

/** Scale is pixels per graph unit, not a CSS transform of a scroll container. */
export function fitGraphCamera(content: GraphSize, viewport: GraphSize): GraphCamera {
  return { x: content.width / 2, y: content.height / 2,
    scale: Math.min(Math.max(1, viewport.width) / Math.max(1, content.width),
      Math.max(1, viewport.height) / Math.max(1, content.height), 1) };
}

export function cameraViewBox(camera: GraphCamera, viewport: GraphSize) {
  const width = viewport.width / camera.scale;
  const height = viewport.height / camera.scale;
  return { x: camera.x - width / 2, y: camera.y - height / 2, width, height };
}

export function zoomGraphCamera(camera: GraphCamera, factor: number, fitScale: number): GraphCamera {
  return { ...camera, scale: Math.min(2, Math.max(Math.min(fitScale, 0.1), camera.scale * factor)) };
}

export function panGraphCamera(camera: GraphCamera, dxPixels: number, dyPixels: number): GraphCamera {
  return { ...camera, x: camera.x - dxPixels / camera.scale, y: camera.y - dyPixels / camera.scale };
}

/** Focus includes only the chosen node and real direct neighbors; no invented relationships. */
export function graphNeighborhood(nodes: KnowledgeGraphNodeDto[], edges: KnowledgeGraphEdgeDto[], focusId: string | null) {
  const available = new Set(nodes.map(n => n.nodeId));
  const ids = focusId && available.has(focusId) ? new Set([focusId]) : available;
  if (focusId && available.has(focusId)) {
    for (const edge of edges) {
      if (edge.sourceNodeId === focusId && available.has(edge.targetNodeId)) ids.add(edge.targetNodeId);
      if (edge.targetNodeId === focusId && available.has(edge.sourceNodeId)) ids.add(edge.sourceNodeId);
    }
    const focus = nodes.find(n => n.nodeId === focusId)!;
    if (focus.parentNodeId && available.has(focus.parentNodeId)) ids.add(focus.parentNodeId);
    nodes.filter(n => n.parentNodeId === focusId).forEach(n => ids.add(n.nodeId));
  }
  return { nodes: nodes.filter(n => ids.has(n.nodeId)),
    edges: edges.filter(e => ids.has(e.sourceNodeId) && ids.has(e.targetNodeId)) };
}

export function graphLabelLines(label: string, maxChars = 29): string[] {
  const lines: string[] = [];
  let line = "";
  const words = label.split(/\s+/).flatMap(word => {
    const chunks: string[] = [];
    for (let i = 0; i < word.length; i += maxChars) chunks.push(word.slice(i, i + maxChars));
    return chunks;
  });
  for (const word of words) {
    if (line && `${line} ${word}`.length > maxChars) { lines.push(line); line = word; }
    else line = line ? `${line} ${word}` : word;
  }
  if (line) lines.push(line);
  return lines.length > 2 ? [lines[0], `${lines[1].slice(0, maxChars - 1)}…`] : lines;
}
