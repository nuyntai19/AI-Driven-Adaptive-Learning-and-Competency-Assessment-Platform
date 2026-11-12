import { deserializeGraphScene, serializeGraphScene, type GraphScene } from "./knowledgeGraphScene.ts";
import type { LabelPlacement, LabelPlacementOptions } from "./knowledgeGraphLabels.ts";
import type { LayoutInput, LayoutRequest, LayoutResponse } from "./knowledgeGraphLayoutProtocol.ts";

/**
 * Main-thread side of the layout worker.
 * - Every job carries a request ID; results are matched by ID and anything late is ignored.
 * - Jobs are serialized per lane: while one runs, newer requests replace the queued one
 *   (the replaced promise rejects with "superseded"), so rapid scope/filter changes coalesce.
 * - A job not answered within `timeoutMs` terminates the worker (computation cannot be
 *   interrupted otherwise); all in-flight jobs reject and the next request starts a new worker.
 * - Scenes are cached in an LRU bounded by entry count and total routed edges.
 */

export interface WorkerLike {
  postMessage(message: LayoutRequest): void;
  terminate(): void;
  onmessage: ((event: { data: LayoutResponse }) => void) | null;
  onerror: ((event: unknown) => void) | null;
}

export type LayoutJobErrorCode = "superseded" | "timeout" | "worker-error" | "unavailable" | "failed";

export class LayoutJobError extends Error {
  readonly code: LayoutJobErrorCode;
  constructor(code: LayoutJobErrorCode, message?: string) {
    super(message ?? code);
    this.code = code;
    this.name = "LayoutJobError";
  }
}

export interface LayoutClientOptions {
  timeoutMs?: number;
  cacheEntries?: number;
  /** Upper bound of routed edges kept across all cached scenes (memory ceiling). */
  cacheEdgeBudget?: number;
}

export interface SceneResult { scene: GraphScene; ms: number; fromCache: boolean }

interface Pending { resolve: (r: LayoutResponse) => void; reject: (e: LayoutJobError) => void; timer: ReturnType<typeof setTimeout> }
interface Lane { running: boolean; queued: { run: () => void; reject: (e: LayoutJobError) => void } | null }

export class GraphLayoutClient {
  private worker: WorkerLike | null = null;
  private nextId = 1;
  private readonly pending = new Map<number, Pending>();
  private readonly lanes = new Map<string, Lane>();
  private readonly cache = new Map<string, GraphScene>();
  private readonly createWorker: () => WorkerLike | null;
  private readonly timeoutMs: number;
  private readonly cacheEntries: number;
  private readonly cacheEdgeBudget: number;
  /** Diagnostics for tests and the status line. */
  workersStarted = 0;

  constructor(createWorker: () => WorkerLike | null, options: LayoutClientOptions = {}) {
    this.createWorker = createWorker;
    this.timeoutMs = options.timeoutMs ?? 3000;
    this.cacheEntries = options.cacheEntries ?? 8;
    this.cacheEdgeBudget = options.cacheEdgeBudget ?? 60000;
  }

  cached(key: string): GraphScene | undefined {
    const scene = this.cache.get(key);
    if (scene) { this.cache.delete(key); this.cache.set(key, scene); }
    return scene;
  }

  get cacheSize(): number { return this.cache.size; }

  requestScene(lane: string, key: string, input: LayoutInput): Promise<SceneResult> {
    const hit = this.cached(key);
    if (hit) return Promise.resolve({ scene: hit, ms: 0, fromCache: true });
    return this.inLane(lane, async () => {
      const again = this.cached(key); // a queued duplicate may have been answered meanwhile
      if (again) return { scene: again, ms: 0, fromCache: true };
      const response = await this.send({ type: "scene", id: 0, key, input });
      if (response.type !== "scene") throw this.toError(response);
      const scene = deserializeGraphScene(response.scene);
      this.store(key, scene);
      return { scene, ms: response.ms, fromCache: false };
    });
  }

  requestLabels(lane: string, sceneKey: string, scene: GraphScene, ids: string[], widths: [string, number][], options: LabelPlacementOptions): Promise<LabelPlacement & { ms: number }> {
    return this.inLane(lane, async () => {
      const ask = () => this.send({ type: "labels", id: 0, sceneKey, ids, widths, options });
      let response = await ask();
      if (response.type === "error" && response.code === "scene-missing") {
        // Worker restarted or evicted the scene: hand it the geometry we already hold, then retry once.
        const adopted = await this.send({ type: "adopt", id: 0, key: sceneKey, scene: serializeGraphScene(scene) });
        if (adopted.type !== "adopted") throw this.toError(adopted);
        response = await ask();
      }
      if (response.type !== "labels") throw this.toError(response);
      return { placed: new Map(response.placed.map(l => [l.edgeId, l])), unplaced: response.unplaced, eligible: response.eligible, ms: response.ms };
    });
  }

  /** Drops cached geometry and the worker (logout, tenant/actor change). */
  clear(): void {
    this.cache.clear();
    this.restart(new LayoutJobError("superseded", "cleared"));
  }

  dispose(): void { this.clear(); }

  private toError(response: LayoutResponse): LayoutJobError {
    return new LayoutJobError("failed", response.type === "error" ? `${response.code}${response.message ? `: ${response.message}` : ""}` : `unexpected ${response.type}`);
  }

  private store(key: string, scene: GraphScene) {
    this.cache.delete(key);
    this.cache.set(key, scene);
    let edges = 0;
    for (const s of this.cache.values()) edges += s.edges.length;
    while (this.cache.size > 1 && (this.cache.size > this.cacheEntries || edges > this.cacheEdgeBudget)) {
      const oldest = this.cache.keys().next().value!;
      edges -= this.cache.get(oldest)!.edges.length;
      this.cache.delete(oldest);
    }
  }

  private inLane<T>(name: string, job: () => Promise<T>): Promise<T> {
    let lane = this.lanes.get(name);
    if (!lane) this.lanes.set(name, (lane = { running: false, queued: null }));
    const current = lane;
    return new Promise<T>((resolve, reject) => {
      const run = () => {
        current.running = true;
        job().then(resolve, reject).finally(() => {
          current.running = false;
          const next = current.queued;
          current.queued = null;
          next?.run();
        });
      };
      if (!current.running) { run(); return; }
      current.queued?.reject(new LayoutJobError("superseded"));
      current.queued = { run, reject };
    });
  }

  private ensureWorker(): WorkerLike {
    if (this.worker) return this.worker;
    const worker = this.createWorker();
    if (!worker) throw new LayoutJobError("unavailable", "Web Worker is not available");
    worker.onmessage = (event) => {
      const entry = this.pending.get(event.data.id);
      if (!entry) return; // stale answer from a superseded/timed-out job
      clearTimeout(entry.timer);
      this.pending.delete(event.data.id);
      entry.resolve(event.data);
    };
    worker.onerror = () => this.restart(new LayoutJobError("worker-error"));
    this.worker = worker;
    this.workersStarted++;
    return worker;
  }

  private send(request: LayoutRequest): Promise<LayoutResponse> {
    return new Promise((resolve, reject) => {
      let worker: WorkerLike;
      try { worker = this.ensureWorker(); } catch (error) { reject(error); return; }
      const id = this.nextId++;
      const timer = setTimeout(() => this.restart(new LayoutJobError("timeout", `layout job exceeded ${this.timeoutMs} ms`)), this.timeoutMs);
      this.pending.set(id, { resolve, reject, timer });
      worker.postMessage({ ...request, id });
    });
  }

  private restart(reason: LayoutJobError) {
    this.worker?.terminate();
    this.worker = null;
    const entries = [...this.pending.values()];
    this.pending.clear();
    for (const entry of entries) { clearTimeout(entry.timer); entry.reject(reason); }
  }
}
