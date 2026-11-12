/** Pure geometry helpers for the knowledge-graph view model. All values are graph units. */

export interface Point { x: number; y: number }
export interface Rect { x: number; y: number; width: number; height: number }
export interface Segment { a: Point; b: Point }

export const emptyRect = (): Rect => ({ x: 0, y: 0, width: 0, height: 0 });

export function inflateRect(r: Rect, m: number): Rect {
  return { x: r.x - m, y: r.y - m, width: r.width + 2 * m, height: r.height + 2 * m };
}

/** True when the rectangles share positive area (touching edges do not count). */
export function rectsOverlap(a: Rect, b: Rect): boolean {
  return a.x < b.x + b.width && b.x < a.x + a.width && a.y < b.y + b.height && b.y < a.y + a.height;
}

export function rectContains(outer: Rect, inner: Rect, eps = 1e-6): boolean {
  return inner.x >= outer.x - eps && inner.y >= outer.y - eps &&
    inner.x + inner.width <= outer.x + outer.width + eps && inner.y + inner.height <= outer.y + outer.height + eps;
}

export function unionRects(rects: Iterable<Rect>): Rect | null {
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
  for (const r of rects) {
    minX = Math.min(minX, r.x); minY = Math.min(minY, r.y);
    maxX = Math.max(maxX, r.x + r.width); maxY = Math.max(maxY, r.y + r.height);
  }
  return Number.isFinite(minX) ? { x: minX, y: minY, width: maxX - minX, height: maxY - minY } : null;
}

export function pointsBounds(points: Point[]): Rect {
  return unionRects(points.map(p => ({ x: p.x, y: p.y, width: 0, height: 0 }))) ?? emptyRect();
}

export function segmentsOf(points: Point[]): Segment[] {
  const out: Segment[] = [];
  for (let i = 1; i < points.length; i++) out.push({ a: points[i - 1], b: points[i] });
  return out;
}

export function segmentLength(s: Segment): number {
  return Math.hypot(s.b.x - s.a.x, s.b.y - s.a.y);
}

export function segmentBounds(s: Segment): Rect {
  return pointsBounds([s.a, s.b]);
}

/**
 * Liang–Barsky clip: true when a segment passes through the rectangle interior by more than `eps`.
 * A segment that only touches the boundary (e.g. a port on a card edge) does not intersect.
 */
export function segmentIntersectsRect(s: Segment, r: Rect, eps = 0.5): boolean {
  const inner = inflateRect(r, -eps);
  if (inner.width <= 0 || inner.height <= 0) return false;
  const dx = s.b.x - s.a.x, dy = s.b.y - s.a.y;
  let t0 = 0, t1 = 1;
  const clip = (p: number, q: number) => {
    if (p === 0) return q > 0; // parallel: inside only if strictly within the slab
    const t = q / p;
    if (p < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
    else { if (t < t0) return false; if (t < t1) t1 = t; }
    return true;
  };
  if (!clip(-dx, s.a.x - inner.x)) return false;
  if (!clip(dx, inner.x + inner.width - s.a.x)) return false;
  if (!clip(-dy, s.a.y - inner.y)) return false;
  if (!clip(dy, inner.y + inner.height - s.a.y)) return false;
  return t1 > t0;
}

const cross = (o: Point, a: Point, b: Point) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

/** Length shared by two collinear segments (0 when not collinear or merely touching). */
export function collinearOverlap(s1: Segment, s2: Segment, eps = 1e-6): number {
  if (Math.abs(cross(s1.a, s1.b, s2.a)) > eps || Math.abs(cross(s1.a, s1.b, s2.b)) > eps) return 0;
  const len = segmentLength(s1);
  if (len < eps) return 0;
  const ux = (s1.b.x - s1.a.x) / len, uy = (s1.b.y - s1.a.y) / len;
  const proj = (p: Point) => (p.x - s1.a.x) * ux + (p.y - s1.a.y) * uy;
  const lo = Math.max(0, Math.min(proj(s2.a), proj(s2.b)));
  const hi = Math.min(len, Math.max(proj(s2.a), proj(s2.b)));
  return Math.max(0, hi - lo);
}

/** Proper crossing of two segments, excluding shared endpoints and collinear overlap. */
export function segmentsCross(s1: Segment, s2: Segment, eps = 1e-6): boolean {
  const d1 = cross(s2.a, s2.b, s1.a), d2 = cross(s2.a, s2.b, s1.b);
  const d3 = cross(s1.a, s1.b, s2.a), d4 = cross(s1.a, s1.b, s2.b);
  return ((d1 > eps && d2 < -eps) || (d1 < -eps && d2 > eps)) && ((d3 > eps && d4 < -eps) || (d3 < -eps && d4 > eps));
}

/** Uniform grid index for rectangles; queries return candidates, callers do exact tests. */
export class SpatialGrid<T> {
  private readonly cells = new Map<string, { rect: Rect; item: T }[]>();
  private readonly cellSize: number;
  constructor(cellSize = 96) { this.cellSize = cellSize; }

  private keys(r: Rect): string[] {
    const keys: string[] = [];
    const x0 = Math.floor(r.x / this.cellSize), x1 = Math.floor((r.x + r.width) / this.cellSize);
    const y0 = Math.floor(r.y / this.cellSize), y1 = Math.floor((r.y + r.height) / this.cellSize);
    for (let x = x0; x <= x1; x++) for (let y = y0; y <= y1; y++) keys.push(`${x},${y}`);
    return keys;
  }

  insert(rect: Rect, item: T): void {
    const entry = { rect, item };
    for (const key of this.keys(rect)) {
      let cell = this.cells.get(key);
      if (!cell) this.cells.set(key, (cell = []));
      cell.push(entry);
    }
  }

  query(rect: Rect): { rect: Rect; item: T }[] {
    const seen = new Set<{ rect: Rect; item: T }>();
    for (const key of this.keys(rect)) for (const entry of this.cells.get(key) ?? []) seen.add(entry);
    return [...seen];
  }
}
