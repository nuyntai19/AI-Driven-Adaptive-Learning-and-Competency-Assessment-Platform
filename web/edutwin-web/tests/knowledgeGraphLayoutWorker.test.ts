import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import { GraphLayoutClient, LayoutJobError, type WorkerLike } from "../src/utils/knowledgeGraphLayoutClient.ts";
import { computeSceneForInput, createLayoutWorkerState, handleLayoutRequest, layoutKey, toLayoutInput, type LayoutRequest, type LayoutResponse } from "../src/utils/knowledgeGraphLayoutProtocol.ts";
import { computeGraphScene, deserializeGraphScene, serializeGraphScene } from "../src/utils/knowledgeGraphScene.ts";
import { placeEdgeLabels } from "../src/utils/knowledgeGraphLabels.ts";
import { card, edge, layeredGraph, node, screenshotFixture } from "./fixtures/knowledgeGraphFixtures.ts";

/** In-process stand-in for the browser worker: same handler, asynchronous delivery, optional hang. */
class FakeWorker implements WorkerLike {
  onmessage: ((event: { data: LayoutResponse }) => void) | null = null;
  onerror: ((event: unknown) => void) | null = null;
  readonly state = createLayoutWorkerState(2);
  readonly received: LayoutRequest[] = [];
  terminated = false;
  hang = false;
  delayMs = 1;
  postMessage(message: LayoutRequest) {
    this.received.push(structuredClone(message));
    if (this.hang) return;
    setTimeout(() => {
      if (this.terminated) return;
      this.onmessage?.({ data: structuredClone(handleLayoutRequest(this.state, structuredClone(message))) });
    }, this.delayMs);
  }
  terminate() { this.terminated = true; }
}

const fixtureInput = () => { const g = screenshotFixture(); return toLayoutInput(g.nodes, g.edges, "orthogonal", card); };

test("serialized scenes round-trip through structured clone without losing geometry or IDs", () => {
  const g = layeredGraph(5, 4, 8, 2, 0.2);
  const scene = computeGraphScene(g.nodes, g.edges, card);
  const copy = deserializeGraphScene(structuredClone(serializeGraphScene(scene)));
  assert.deepEqual([...copy.nodes.entries()], [...scene.nodes.entries()]);
  assert.deepEqual(copy.edges, scene.edges);
  assert.deepEqual([...copy.edgeById.keys()], [...scene.edgeById.keys()]);
  assert.deepEqual(copy.bounds, scene.bounds);
});

test("worker input carries only layout fields; same handler output as the main-thread path", () => {
  const g = screenshotFixture();
  const input = toLayoutInput(g.nodes, g.edges, "orthogonal", card);
  assert.deepEqual(Object.keys(input.nodes[0]).sort(), ["nodeCode", "nodeId", "nodeType", "orderIndex", "parentNodeId"]);
  assert.ok(!JSON.stringify(input).includes("Toán 12"), "node names are not cloned to the worker");
  const state = createLayoutWorkerState();
  const response = handleLayoutRequest(state, { type: "scene", id: 1, key: "k", input });
  assert.equal(response.type, "scene");
  const direct = computeGraphScene(g.nodes, g.edges, card);
  assert.deepEqual(deserializeGraphScene((response as Extract<LayoutResponse, { type: "scene" }>).scene).edges, direct.edges);
  const labels = handleLayoutRequest(state, { type: "labels", id: 2, sceneKey: "k", ids: direct.edges.map(e => e.edgeId), widths: [["1", 7]], options: { margin: 5 } });
  assert.equal(labels.type, "labels");
  const expected = placeEdgeLabels(direct, direct.edges.map(e => e.edgeId), (t, s) => (t === "1" ? 7 : t.length * s), { margin: 5 });
  assert.deepEqual((labels as Extract<LayoutResponse, { type: "labels" }>).placed.map(l => l.edgeId).sort(), [...expected.placed.keys()].sort());
  assert.deepEqual(handleLayoutRequest(state, { type: "labels", id: 3, sceneKey: "missing", ids: [], widths: [], options: {} }), { type: "error", id: 3, code: "scene-missing" });
});

test("cache key separates scope, engine, options and every geometry/weight field", () => {
  const g = screenshotFixture();
  const base = toLayoutInput(g.nodes, g.edges, "orthogonal", card);
  const k = layoutKey("c1:u1:grade=12", base);
  assert.equal(layoutKey("c1:u1:grade=12", toLayoutInput(g.nodes, g.edges, "orthogonal", card)), k, "stable for identical input");
  assert.notEqual(layoutKey("c2:u1:grade=12", base), k, "tenant");
  assert.notEqual(layoutKey("c1:u2:grade=12", base), k, "actor");
  assert.notEqual(layoutKey("c1:u1:grade=12", { ...base, engine: "legacy" }), k, "engine");
  assert.notEqual(layoutKey("c1:u1:grade=12", { ...base, options: { ...card, gapY: 64 } }), k, "options");
  const reweighted = g.edges.map((e, i) => (i === 0 ? { ...e, weight: 2 } : e));
  assert.notEqual(layoutKey("c1:u1:grade=12", toLayoutInput(g.nodes, reweighted, "orthogonal", card)), k, "weight changes badge text");
  const reordered = g.nodes.map((n, i) => (i === 0 ? { ...n, orderIndex: 99 } : n));
  assert.notEqual(layoutKey("c1:u1:grade=12", toLayoutInput(reordered, g.edges, "orthogonal", card)), k, "order changes layout");
});

test("scene jobs run in the worker, results are cached and a cache hit posts nothing", async () => {
  const workers: FakeWorker[] = [];
  const client = new GraphLayoutClient(() => { const w = new FakeWorker(); workers.push(w); return w; });
  const input = fixtureInput();
  const first = await client.requestScene("lane", "k1", input);
  assert.equal(first.fromCache, false);
  assert.equal(first.scene.edges.length, input.edges.length);
  const second = await client.requestScene("lane", "k1", input);
  assert.equal(second.fromCache, true);
  assert.equal(second.scene, first.scene);
  assert.equal(workers.length, 1);
  assert.equal(workers[0].received.length, 1);
});

test("rapid requests coalesce: the running job finishes, intermediate ones are superseded, the latest wins", async () => {
  const workers: FakeWorker[] = [];
  const client = new GraphLayoutClient(() => { const w = new FakeWorker(); w.delayMs = 20; workers.push(w); return w; });
  const g = (n: number) => toLayoutInput([node("a"), ...Array.from({ length: n }, (_, i) => node(`n${i}`, i))],
    Array.from({ length: n }, (_, i) => edge(`e${i}`, "a", `n${i}`)), "orthogonal", card);
  const a = client.requestScene("lane", "A", g(1));
  const b = client.requestScene("lane", "B", g(2));
  const c = client.requestScene("lane", "C", g(3));
  await assert.rejects(b, (e: unknown) => e instanceof LayoutJobError && e.code === "superseded");
  assert.equal((await a).scene.edges.length, 1);
  assert.equal((await c).scene.edges.length, 3);
  assert.deepEqual(workers[0].received.map(r => r.type === "scene" ? r.key : r.type), ["A", "C"], "B was never computed");
});

test("a hung job times out, terminates the worker, and the next request starts a fresh one", async () => {
  const workers: FakeWorker[] = [];
  const client = new GraphLayoutClient(() => { const w = new FakeWorker(); w.hang = workers.length === 0; workers.push(w); return w; }, { timeoutMs: 30 });
  const input = fixtureInput();
  await assert.rejects(client.requestScene("lane", "slow", input), (e: unknown) => e instanceof LayoutJobError && e.code === "timeout");
  assert.equal(workers[0].terminated, true);
  const ok = await client.requestScene("lane", "slow", input);
  assert.equal(ok.fromCache, false);
  assert.equal(workers.length, 2);
  assert.equal(client.cached("slow"), ok.scene);
});

test("labels for a scene the worker no longer holds are re-adopted from the client copy", async () => {
  const workers: FakeWorker[] = [];
  const client = new GraphLayoutClient(() => { const w = new FakeWorker(); workers.push(w); return w; });
  const input = fixtureInput();
  const scene = computeSceneForInput(input); // e.g. computed on the main thread
  const ids = scene.edges.map(e => e.edgeId);
  const placement = await client.requestLabels("labels", "main-key", scene, ids, [["1", 7]], { margin: 5 });
  assert.equal(placement.placed.size, ids.length);
  assert.deepEqual(workers[0].received.map(r => r.type), ["labels", "adopt", "labels"]);
  await client.requestLabels("labels", "main-key", scene, ids.slice(0, 3), [["1", 7]], { margin: 5 });
  assert.deepEqual(workers[0].received.map(r => r.type).slice(3), ["labels"], "no second adopt while the worker holds it");
});

test("LRU respects entry and edge budgets; clear() drops cache and worker (logout / tenant switch)", async () => {
  const workers: FakeWorker[] = [];
  const client = new GraphLayoutClient(() => { const w = new FakeWorker(); workers.push(w); return w; }, { cacheEntries: 2, cacheEdgeBudget: 1000 });
  const input = fixtureInput();
  await client.requestScene("l", "k1", input);
  await client.requestScene("l", "k2", input);
  client.cached("k1"); // touch → k2 becomes oldest
  await client.requestScene("l", "k3", input);
  assert.equal(client.cacheSize, 2);
  assert.ok(client.cached("k1") && client.cached("k3") && !client.cached("k2"));
  const big = layeredGraph(9, 8, 50, 4, 0);
  assert.ok(big.edges.length > 1100);
  await client.requestScene("l", "big", toLayoutInput(big.nodes, big.edges, "orthogonal", card));
  assert.equal(client.cacheSize, 1, "edge budget evicts older scenes but keeps the newest");
  client.clear();
  assert.equal(client.cacheSize, 0);
  assert.equal(workers[0].terminated, true);
});

test("no worker available rejects with 'unavailable' so the canvas falls back to the main thread", async () => {
  const client = new GraphLayoutClient(() => null);
  await assert.rejects(client.requestScene("l", "k", fixtureInput()), (e: unknown) => e instanceof LayoutJobError && e.code === "unavailable");
});

test("canvas wiring: worker is lazy (route chunk only), small graphs stay synchronous, scope identity reaches the cache key", () => {
  const hook = fs.readFileSync(new URL("../src/components/teacher/useKnowledgeGraphLayout.ts", import.meta.url), "utf8");
  assert.match(hook, /new Worker\(new URL\("\.\.\/\.\.\/workers\/knowledgeGraphLayout\.worker\.ts", import\.meta\.url\), \{ type: "module" \}\)/);
  assert.match(hook, /useAuthStore\.subscribe/);
  const view = fs.readFileSync(new URL("../src/pages/teacher/TeacherKnowledgeGraphView.tsx", import.meta.url), "utf8");
  assert.match(view, /cacheScope=\{`\$\{user\?\.centerId \?\? ""\}:\$\{user\?\.userId \?\? ""\}:\$\{scopeKey\}`\}/);
  const main = fs.readFileSync(new URL("../src/main.tsx", import.meta.url), "utf8") + fs.readFileSync(new URL("../src/App.tsx", import.meta.url), "utf8");
  assert.doesNotMatch(main, /useKnowledgeGraphLayout|knowledgeGraphLayoutClient/);
});
