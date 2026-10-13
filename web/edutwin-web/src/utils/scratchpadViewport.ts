import type { ScratchpadPoint } from "../types/scratchpad";

/** Uniform paper fit: the same scale is used for both axes, including pointer input. */
export function scratchpadTransform(width: number, height: number, zoom: number, pan: ScratchpadPoint) {
  const fit = Math.min(Math.max(1, width) / 1200, Math.max(1, height) / 800);
  return { scale: fit * zoom, x: (width - 1200 * fit) / 2 + pan.x, y: (height - 800 * fit) / 2 + pan.y };
}

export function scratchpadPoint(point: ScratchpadPoint, width: number, height: number, zoom: number, pan: ScratchpadPoint): ScratchpadPoint {
  const transform = scratchpadTransform(width, height, zoom, pan);
  return { x: (point.x - transform.x) / transform.scale, y: (point.y - transform.y) / transform.scale };
}
