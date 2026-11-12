import type { KnowledgeGraphEdgeDto, KnowledgeGraphNodeDto } from "../types/knowledgeGraph";
import { computeGraphScene, computeLegacyGraphScene, deserializeGraphScene, serializeGraphScene, type GraphScene, type GraphSceneOptions, type SerializedGraphScene } from "./knowledgeGraphScene.ts";
import { estimateTextWidth, placeEdgeLabels, type EdgeLabelBox, type LabelPlacementOptions } from "./knowledgeGraphLabels.ts";

/**
 * Message protocol shared by the layout worker and its client. The pure handler runs inside the
 * worker in the browser and in-process in tests, so both paths execute the same code.
 */

/** Bump when routing/label output changes so cached geometry from an older engine is never reused. */
export const layoutEngineVersion = "kg-layout@1";

export type LayoutEngine = "orthogonal" | "legacy";

/** Only the fields layout needs are cloned to the worker (no names, descriptions or provenance). */
export type LayoutNodeInput = Pick<KnowledgeGraphNodeDto, "nodeId" | "parentNodeId" | "orderIndex" | "nodeType" | "nodeCode">;
export type LayoutEdgeInput = Pick<KnowledgeGraphEdgeDto, "edgeId" | "sourceNodeId" | "targetNodeId" | "relationType" | "weight">;

export interface LayoutInput {
  engine: LayoutEngine;
  nodes: LayoutNodeInput[];
  edges: LayoutEdgeInput[];
  options: GraphSceneOptions;
}

export function toLayoutInput(
  nodes: KnowledgeGraphNodeDto[],
  edges: KnowledgeGraphEdgeDto[],
  engine: LayoutEngine,
  options: GraphSceneOptions
): LayoutInput {
  return {
    engine, options: { ...options },
    nodes: nodes.map(n => ({ nodeId: n.nodeId, parentNodeId: n.parentNodeId, orderIndex: n.orderIndex, nodeType: n.nodeType, nodeCode: n.nodeCode })),
    edges: edges.map(e => ({ edgeId: e.edgeId, sourceNodeId: e.sourceNodeId, targetNodeId: e.targetNodeId, relationType: e.relationType, weight: e.weight })),
  };
}

export function computeSceneForInput(input: LayoutInput): GraphScene {
  // Layout reads only the projected fields above; the cast keeps DTO typings at the public API.
  const nodes = input.nodes as KnowledgeGraphNodeDto[], edges = input.edges as KnowledgeGraphEdgeDto[];
  return input.engine === "legacy"
    ? computeLegacyGraphScene(nodes, edges, input.options)
    : computeGraphScene(nodes, edges, input.options);
}

/**
 * Cache key: engine version + engine + caller scope (tenant/actor/subject/grade/version/class…)
 * + options + a 64-bit content hash of every field that affects geometry or badge text.
 */
export function layoutKey(scope: string, input: LayoutInput): string {
  let h1 = 0x811c9dc5, h2 = 0x9747b28c;
  const feed = (s: string) => {
    for (let i = 0; i < s.length; i++) {
      const c = s.charCodeAt(i);
      h1 = Math.imul(h1 ^ c, 0x01000193);
      h2 = Math.imul(h2 ^ c, 0x5bd1e995); h2 ^= h2 >>> 13;
    }
    h1 = Math.imul(h1 ^ 0x1f, 0x01000193); h2 = Math.imul(h2 ^ 0x1f, 0x5bd1e995);
  };
  for (const n of input.nodes) { feed(n.nodeId); feed(n.parentNodeId ?? ""); feed(String(n.orderIndex)); feed(n.nodeType); feed(n.nodeCode ?? ""); }
  feed("|");
  for (const e of input.edges) { feed(e.edgeId); feed(e.sourceNodeId); feed(e.targetNodeId); feed(e.relationType); feed(String(e.weight)); }
  const hash = (h1 >>> 0).toString(36) + "." + (h2 >>> 0).toString(36);
  return `${layoutEngineVersion}|${input.engine}|${scope}|${JSON.stringify(input.options)}|n${input.nodes.length}e${input.edges.length}|${hash}`;
}

export type LayoutRequest =
  | { type: "scene"; id: number; key: string; input: LayoutInput }
  | { type: "adopt"; id: number; key: string; scene: SerializedGraphScene }
  | { type: "labels"; id: number; sceneKey: string; ids: string[]; widths: [string, number][]; options: LabelPlacementOptions };

export type LayoutResponse =
  | { type: "scene"; id: number; key: string; scene: SerializedGraphScene; ms: number }
  | { type: "adopted"; id: number }
  | { type: "labels"; id: number; placed: EdgeLabelBox[]; unplaced: string[]; eligible: number; ms: number }
  | { type: "error"; id: number; code: "scene-missing" | "failed"; message?: string };

export interface LayoutWorkerState { scenes: Map<string, GraphScene>; maxScenes: number }

export function createLayoutWorkerState(maxScenes = 4): LayoutWorkerState {
  return { scenes: new Map(), maxScenes };
}

function remember(state: LayoutWorkerState, key: string, scene: GraphScene) {
  state.scenes.delete(key);
  state.scenes.set(key, scene);
  while (state.scenes.size > state.maxScenes) state.scenes.delete(state.scenes.keys().next().value!);
}

const now = () => (typeof performance !== "undefined" ? performance.now() : Date.now());

export function handleLayoutRequest(state: LayoutWorkerState, req: LayoutRequest): LayoutResponse {
  try {
    if (req.type === "scene") {
      const t0 = now();
      const scene = computeSceneForInput(req.input);
      remember(state, req.key, scene);
      return { type: "scene", id: req.id, key: req.key, scene: serializeGraphScene(scene), ms: now() - t0 };
    }
    if (req.type === "adopt") {
      remember(state, req.key, deserializeGraphScene(req.scene));
      return { type: "adopted", id: req.id };
    }
    const scene = state.scenes.get(req.sceneKey);
    if (!scene) return { type: "error", id: req.id, code: "scene-missing" };
    remember(state, req.sceneKey, scene);
    const t0 = now();
    const widths = new Map(req.widths);
    const placement = placeEdgeLabels(scene, req.ids, (text, size) => widths.get(text) ?? estimateTextWidth(text, size), req.options);
    return { type: "labels", id: req.id, placed: [...placement.placed.values()], unplaced: placement.unplaced, eligible: placement.eligible, ms: now() - t0 };
  } catch (error) {
    return { type: "error", id: req.id, code: "failed", message: error instanceof Error ? error.message : String(error) };
  }
}
