import { useEffect, useId, useMemo, useState } from "react";
import type { KnowledgeGraphEdgeDto, KnowledgeGraphNodeDto } from "../../types/knowledgeGraph";
import type { GraphScene, GraphSceneOptions } from "../../utils/knowledgeGraphScene";
import { formatEdgeWeight, placeEdgeLabels, type LabelPlacement, type LabelPlacementOptions } from "../../utils/knowledgeGraphLabels";
import { computeSceneForInput, layoutKey, toLayoutInput, type LayoutEngine } from "../../utils/knowledgeGraphLayoutProtocol";
import { GraphLayoutClient, LayoutJobError, type WorkerLike } from "../../utils/knowledgeGraphLayoutClient";
import { useAuthStore } from "../../stores/authStore";

/** Below these sizes the main thread finishes well inside one frame budget; above, use the worker. */
export const syncSceneLimit = { nodes: 150, edges: 400 };
export const syncLabelLimit = 250;

let client: GraphLayoutClient | null | undefined;

function getLayoutClient(): GraphLayoutClient | null {
  if (client !== undefined) return client;
  if (typeof Worker === "undefined") return (client = null);
  client = new GraphLayoutClient(() => {
    try {
      return new Worker(new URL("../../workers/knowledgeGraphLayout.worker.ts", import.meta.url), { type: "module" }) as unknown as WorkerLike;
    } catch {
      return null;
    }
  });
  return client;
}

/** Cached geometry is tenant/actor specific: drop it whenever the signed-in identity changes. */
export function clearKnowledgeGraphLayoutCache(): void {
  client?.clear();
}
useAuthStore.subscribe((state, previous) => {
  if (state.user?.userId !== previous.user?.userId || state.user?.centerId !== previous.user?.centerId) clearKnowledgeGraphLayoutCache();
});

export type LayoutStatus = "ready" | "pending" | "fallback";

export interface GraphLayoutState {
  scene: GraphScene | null;
  sceneKey: string | null;
  /** "pending" while a newer layout is computed; `scene` may then be the previous one (same scope). */
  status: LayoutStatus;
  source: "main" | "worker" | "cache" | "fallback";
  message?: string;
  ms?: number;
}

export function useGraphScene(
  nodes: KnowledgeGraphNodeDto[],
  edges: KnowledgeGraphEdgeDto[],
  engine: LayoutEngine,
  options: GraphSceneOptions,
  cacheScope: string
): GraphLayoutState {
  const lane = useId();
  const optionsKey = JSON.stringify(options);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const input = useMemo(() => toLayoutInput(nodes, edges, engine, options), [nodes, edges, engine, optionsKey]);
  const key = useMemo(() => layoutKey(cacheScope, input), [cacheScope, input]);
  const runSync = engine === "legacy" || (nodes.length <= syncSceneLimit.nodes && edges.length <= syncSceneLimit.edges) || !getLayoutClient();
  const syncScene = useMemo(() => (runSync ? computeSceneForInput(input) : null), [runSync, input]);
  const [result, setResult] = useState<(GraphLayoutState & { forKey: string }) | null>(null);

  useEffect(() => {
    if (runSync) return;
    const layoutClient = getLayoutClient()!;
    const hit = layoutClient.cached(key);
    if (hit) { setResult({ forKey: key, scene: hit, sceneKey: key, status: "ready", source: "cache", ms: 0 }); return; }
    let alive = true;
    layoutClient.requestScene(lane, key, input).then(r => {
      if (alive) setResult({ forKey: key, scene: r.scene, sceneKey: key, status: "ready", source: r.fromCache ? "cache" : "worker", ms: r.ms });
    }).catch((error: unknown) => {
      if (!alive || (error instanceof LayoutJobError && error.code === "superseded")) return;
      // Finite fallback: the previous grid + curve layout is cheap and always available.
      const fallback = computeSceneForInput({ ...input, engine: "legacy" });
      const timeout = error instanceof LayoutJobError && error.code === "timeout";
      setResult({ forKey: key, scene: fallback, sceneKey: `${key}#fallback`, status: "fallback", source: "fallback",
        message: timeout
          ? "Bố cục chi tiết cần quá 3 giây nên đang hiển thị kiểu đường cong cũ. Hãy chọn chương hoặc tìm một nút để xem rõ hơn."
          : "Không tính được bố cục chi tiết trong nền nên đang hiển thị kiểu đường cong cũ." });
    });
    return () => { alive = false; };
  }, [runSync, key, input, lane]);

  if (syncScene) return { scene: syncScene, sceneKey: key, status: "ready", source: "main" };
  if (result) {
    const { forKey, ...state } = result;
    return forKey === key ? state : { ...state, status: "pending", message: undefined };
  }
  return { scene: null, sceneKey: null, status: "pending", source: "worker" };
}

const emptyPlacement: LabelPlacement = { placed: new Map(), unplaced: [], eligible: 0 };

export function useLabelPlacement(
  scene: GraphScene | null,
  sceneKey: string | null,
  ids: string[],
  options: LabelPlacementOptions,
  measure: (text: string, fontSize: number) => number,
  fontsVersion: number
): { placement: LabelPlacement; pending: boolean } {
  const lane = useId();
  const idsKey = ids.join("\u0001");
  const optionsKey = JSON.stringify(options);
  // Large scenes always go to the worker: even one label needs the scene-wide obstacle index,
  // which is built once per scene and would otherwise be a long main-thread task.
  const runSync = !scene || !ids.length || (ids.length <= syncLabelLimit && scene.edges.length <= syncSceneLimit.edges) || !getLayoutClient();
  const sync = useMemo(() => (scene && runSync ? placeEdgeLabels(scene, ids, measure, options) : null),
    // ids/options are tracked by value; fontsVersion re-measures once web fonts are ready
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [scene, idsKey, optionsKey, runSync, fontsVersion]);
  const [result, setResult] = useState<{ sceneKey: string; request: string; placement: LabelPlacement } | null>(null);
  const request = `${idsKey}|${optionsKey}|${fontsVersion}`;

  useEffect(() => {
    if (runSync || !scene || !sceneKey) return;
    const texts = new Set<string>();
    for (const id of ids) { const e = scene.edgeById.get(id); if (e) texts.add(formatEdgeWeight(e.weight)); }
    const size = options.fontSize ?? 12;
    const widths = [...texts].map(t => [t, measure(t, size)] as [string, number]);
    let alive = true;
    getLayoutClient()!.requestLabels(lane, sceneKey, scene, ids, widths, options).then(p => {
      if (alive) setResult({ sceneKey, request, placement: p });
    }).catch((error: unknown) => {
      if (!alive || (error instanceof LayoutJobError && error.code === "superseded")) return;
      // Keep the priority labels (selected/focused/neighbourhood) readable even if the worker failed.
      const head = ids.slice(0, options.priorityCount ?? 0);
      setResult({ sceneKey, request, placement: placeEdgeLabels(scene, head, measure, options) });
    });
    return () => { alive = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [runSync, scene, sceneKey, request, lane]);

  if (sync) return { placement: sync, pending: false };
  if (result && result.sceneKey === sceneKey) return { placement: result.placement, pending: result.request !== request };
  return { placement: emptyPlacement, pending: !!scene };
}
