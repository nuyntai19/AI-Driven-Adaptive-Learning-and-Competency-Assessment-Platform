import test from "node:test";
import assert from "node:assert/strict";
import { computeGraphScene, computeLegacyGraphScene, roundedOrthogonalPath } from "../src/utils/knowledgeGraphScene.ts";
import { placeEdgeLabels, formatEdgeWeight } from "../src/utils/knowledgeGraphLabels.ts";
import { fitGraphBounds, cameraViewBox, nextGraphLod } from "../src/utils/knowledgeGraphViewport.ts";
import { rectContains } from "../src/utils/knowledgeGraphGeometry.ts";
import { card, deepFreeze, edge, inspectLabels, inspectScene, layeredGraph, node, screenshotFixture, shuffled, type GeometryReport } from "./fixtures/knowledgeGraphFixtures.ts";

const clean = (r: GeometryReport, label: string) => {
  assert.deepEqual(r.nodeOverlaps, [], `${label}: cards overlap`);
  assert.deepEqual(r.edgeThroughCard, [], `${label}: route passes through a card`);
  assert.deepEqual(r.sharedPaths, [], `${label}: two different edges share a path segment`);
  assert.deepEqual(r.outOfBounds, [], `${label}: bounds do not contain every card/route`);
  assert.deepEqual(r.nonOrthogonal, [], `${label}: route is not orthogonal`);
};

test("screenshot fixture: no route through cards, no shared paths, every weight badge placed without collisions", () => {
  const { nodes, edges } = screenshotFixture();
  const scene = computeGraphScene(nodes, edges, card);
  clean(inspectScene(scene), "screenshot");
  assert.equal(scene.edges.length, edges.length, "every relation is drawn");
  assert.deepEqual(scene.hiddenEdges, []);
  const placement = placeEdgeLabels(scene, scene.edges.map(e => e.edgeId), undefined, { margin: 5 });
  const labels = inspectLabels(scene, placement, 5);
  assert.deepEqual(labels, { labelLabel: [], labelNode: [], labelArrow: [], labelOtherEdge: [], outOfBounds: [] });
  assert.equal(placement.placed.size, edges.length, `all ${edges.length} labels visible at detail/100%: unplaced ${placement.unplaced}`);
});

test("screenshot fixture: parallel, reciprocal and same-column relations stay distinct and keep direction", () => {
  const { nodes, edges } = screenshotFixture();
  const scene = computeGraphScene(nodes, edges, card);
  const prereq = scene.edgeById.get("A1-B4")!, error = scene.edgeById.get("A1!B4")!, back = scene.edgeById.get("B4~A1")!;
  assert.notEqual(prereq.d, error.d, "parallel relations between the same cards use different routes");
  const a1 = scene.nodes.get("A1")!, b4 = scene.nodes.get("B4")!, b2 = scene.nodes.get("B2")!;
  // Arrow end lies on the target card boundary, start on the source boundary.
  for (const e of [prereq, error]) {
    assert.equal(e.points[0].x, a1.x + a1.width); assert.equal(e.points[e.points.length - 1].x, b4.x);
  }
  assert.equal(back.kind, "backward");
  assert.equal(back.points[0].x, b4.x, "reciprocal overlay leaves B4 on its left side");
  assert.equal(back.points[back.points.length - 1].x, a1.x + a1.width, "and enters A1 from the right, pointing at A1");
  const same = scene.edgeById.get("B2~B5")!;
  assert.equal(same.kind, "sameColumn");
  assert.ok(same.points.every(p => p.x >= b2.x + b2.width), "same-column loop stays right of the column");
  assert.ok(rectContains(scene.bounds, same.bounds), "fit bounds include the right-hand loop");
});

test("crossing reduction keeps ranks, keeps sibling order and cuts crossings by at least 30% on a reorderable fixture", () => {
  const { nodes, edges } = screenshotFixture();
  const baseline = computeGraphScene(nodes, edges, { ...card, crossingMinimization: false });
  const reduced = computeGraphScene(nodes, edges, card);
  const before = inspectScene(baseline).crossings, after = inspectScene(reduced).crossings;
  assert.ok(after <= before * 0.7, `crossings ${before} → ${after}`);
  for (const n of nodes) assert.equal(reduced.nodes.get(n.nodeId)!.rank, baseline.nodes.get(n.nodeId)!.rank);

  // Siblings under one parent keep academic order and stay contiguous.
  const kids = ["K1", "K2", "K3"].map((id, i) => node(id, i, "P"));
  const parent = node("P", 0);
  const others = [node("X", 1), node("Y", 2)];
  const sources = [node("S1", 0), node("S2", 1)];
  const graph = [...sources, parent, ...kids, ...others];
  const rels = [edge("S1-Y", "S1", "Y"), edge("S1-X", "S1", "X"), edge("S2-K3", "S2", "K3"), edge("S1-K2", "S1", "K2"),
    edge("S2-K1", "S2", "K1"), edge("S2-P", "S2", "P")];
  const scene = computeGraphScene(graph, rels, card);
  for (const id of ["P", "K1", "K2", "K3", "X", "Y"]) assert.equal(scene.nodes.get(id)!.rank, 1);
  const rows = ["K1", "K2", "K3"].map(id => scene.nodes.get(id)!.row);
  assert.deepEqual(rows, [...rows].sort((a, b) => a - b));
  assert.equal(rows[2] - rows[0], 2, "sibling block stays contiguous");

  // A graph without crossings never gets more.
  const chain = computeGraphScene([node("a"), node("b"), node("c")], [edge("ab", "a", "b"), edge("bc", "b", "c")], card);
  assert.equal(inspectScene(chain).crossings, 0);
});

test("topologies: chain, wide layer, disconnected, diamond, long-span, backward overlay, self-loop and cycle route cleanly", () => {
  const cases: [string, ReturnType<typeof screenshotFixture>][] = [
    ["chain", { nodes: ["a", "b", "c", "d", "e"].map((id, i) => node(id, i)), edges: [edge("1", "a", "b"), edge("2", "b", "c"), edge("3", "c", "d"), edge("4", "d", "e")] }],
    ["wide", { nodes: [node("r"), ...Array.from({ length: 30 }, (_, i) => node(`w${i}`, i))], edges: Array.from({ length: 30 }, (_, i) => edge(`r${i}`, "r", `w${i}`)) }],
    ["disconnected", { nodes: [node("x"), node("y"), node("z"), node("q")], edges: [edge("xy", "x", "y")] }],
    ["diamond", { nodes: [node("t"), node("l", 0), node("rr", 1), node("bt")], edges: [edge("1", "t", "l"), edge("2", "t", "rr"), edge("3", "l", "bt"), edge("4", "rr", "bt"), edge("5", "t", "bt")] }],
    ["long-span+backward", { nodes: ["a", "b", "c", "d", "e"].map((id, i) => node(id, i)),
      edges: [edge("ab", "a", "b"), edge("bc", "b", "c"), edge("cd", "c", "d"), edge("de", "d", "e"), edge("ae", "a", "e"), edge("ad", "a", "d"),
        edge("e~a", "e", "a", "RelatedTo"), edge("d~b", "d", "b", "CausesErrorIn"), edge("c~a", "c", "a", "RelatedTo")] }],
    ["self-loop", { nodes: [node("s"), node("t")], edges: [edge("ss", "s", "s", "RelatedTo"), edge("st", "s", "t")] }],
    ["cycle", { nodes: [node("A"), node("B"), node("C"), node("D")], edges: [edge("1", "A", "B"), edge("2", "B", "C"), edge("3", "C", "A"), edge("4", "D", "A")] }],
    ["random 60/≈150", layeredGraph(7, 5, 12, 2, 0.2)],
  ];
  for (const [name, g] of cases) {
    const scene = computeGraphScene(g.nodes, g.edges, card);
    clean(inspectScene(scene), name);
    assert.equal(scene.edges.length + scene.hiddenEdges.length, g.edges.length, `${name}: every relation is drawn or reported hidden`);
    for (const e of scene.edges) {
      const s = scene.nodes.get(e.sourceNodeId)!, t = scene.nodes.get(e.targetNodeId)!;
      const first = e.points[0], last = e.points[e.points.length - 1];
      assert.ok(first.x === s.x || first.x === s.x + s.width, `${name}/${e.edgeId}: starts on the source card side`);
      assert.ok(last.x === t.x || last.x === t.x + t.width, `${name}/${e.edgeId}: ends on the target card side`);
      assert.ok(first.y > s.y && first.y < s.y + s.height && last.y > t.y && last.y < t.y + t.height);
      assert.ok(Math.abs(e.points[e.points.length - 2].y - last.y) < 1e-9, `${name}/${e.edgeId}: arrow enters horizontally`);
    }
  }
  const cycle = computeGraphScene(cases[6][1].nodes, cases[6][1].edges, card);
  assert.equal(cycle.hasDependencyCycle, true, "invalid prerequisite cycle is reported, not silently repaired");
});

test("hub above port capacity reports hidden relations instead of squeezing ports, keeping backbone edges first", () => {
  const leaves = Array.from({ length: 200 }, (_, i) => node(`h${i}`, i));
  const edges = leaves.map((l, i) => edge(`hub-${String(i).padStart(3, "0")}`, "hub", l.nodeId));
  edges.push(edge("hub~x", "hub", "h0", "RelatedTo"));
  const scene = computeGraphScene([node("hub"), ...leaves], edges, card);
  clean(inspectScene(scene), "hub");
  assert.ok(scene.hiddenEdges.length > 0);
  assert.equal(scene.edges.length + scene.hiddenEdges.length, edges.length);
  assert.ok(scene.hiddenEdges.some(h => h.edgeId === "hub~x"), "overlay yields to backbone");
  assert.ok(scene.hiddenEdges.every(h => h.reason === "port-overflow"));
});

test("dangling edges are reported and nothing crashes; DTOs are never mutated", () => {
  const nodes = deepFreeze([node("a"), node("b")]);
  const edges = deepFreeze([edge("ab", "a", "b"), edge("ax", "a", "missing")]);
  const scene = computeGraphScene(nodes, edges, card);
  assert.deepEqual(scene.invalidEdgeIds, ["ax"]);
  assert.equal(scene.edges.length, 1);
  assert.ok(!("x" in nodes[0]) && !("points" in edges[0]));
  const empty = computeGraphScene([], [], card);
  assert.equal(empty.edges.length, 0); assert.ok(Number.isFinite(empty.bounds.width));
});

test("output is deterministic for permuted input order", () => {
  const g = layeredGraph(11, 4, 10, 3, 0.2);
  const a = computeGraphScene(g.nodes, g.edges, card);
  const b = computeGraphScene(shuffled(g.nodes, 3), shuffled(g.edges, 5), card);
  assert.deepEqual([...b.nodes.entries()].sort(), [...a.nodes.entries()].sort());
  const byId = (s: typeof a) => s.edges.map(e => [e.edgeId, e.d]).sort();
  assert.deepEqual(byId(b), byId(a));
  const la = placeEdgeLabels(a, a.edges.map(e => e.edgeId)), lb = placeEdgeLabels(b, a.edges.map(e => e.edgeId));
  assert.deepEqual([...lb.placed.entries()].sort(), [...la.placed.entries()].sort());
});

test("label solver: priority order wins contested space, every visible label is collision-free, unplaced ones are counted", () => {
  const g = layeredGraph(3, 4, 14, 4, 0.3);
  const scene = computeGraphScene(g.nodes, g.edges, card);
  const ids = scene.edges.map(e => e.edgeId);
  const placement = placeEdgeLabels(scene, ids, undefined, { margin: 5 });
  assert.deepEqual(inspectLabels(scene, placement, 5), { labelLabel: [], labelNode: [], labelArrow: [], labelOtherEdge: [], outOfBounds: [] });
  assert.equal(placement.placed.size + placement.unplaced.length, placement.eligible);
  // The selected edge is processed first, so it is placed whenever any candidate is free.
  const target = placement.unplaced[0] ?? ids[ids.length - 1];
  const prioritized = placeEdgeLabels(scene, [target, ...ids], undefined, { margin: 5, priorityCount: 1 });
  const alone = placeEdgeLabels(scene, [target], undefined, { margin: 5 });
  assert.equal(prioritized.placed.has(target), alone.placed.has(target));
});

test("weights use one formatter for decimals and zero", () => {
  assert.equal(formatEdgeWeight(1), "1");
  assert.equal(formatEdgeWeight(0), "0");
  assert.equal(formatEdgeWeight(0.75), "0,75");
  assert.equal(formatEdgeWeight(2.3456), "2,35");
});

test("fit contains cards, loops, arrowheads and label allowance at 320/768/1240 px", () => {
  const { nodes, edges } = screenshotFixture();
  const scene = computeGraphScene(nodes, edges, card);
  for (const width of [320, 768, 1240]) {
    const viewport = { width, height: 580 };
    const view = cameraViewBox(fitGraphBounds(scene.bounds, viewport), viewport);
    assert.ok(rectContains(view, scene.bounds), `bounds fit in ${width}px`);
  }
});

test("level of detail uses hysteresis around 0.4 and 0.8", () => {
  assert.equal(nextGraphLod("overview", 0.42), "overview");
  assert.equal(nextGraphLod("overview", 0.46), "explore");
  assert.equal(nextGraphLod("explore", 0.37), "explore");
  assert.equal(nextGraphLod("explore", 0.34), "overview");
  assert.equal(nextGraphLod("explore", 0.86), "detail");
  assert.equal(nextGraphLod("detail", 0.77), "detail");
  assert.equal(nextGraphLod("detail", 0.74), "explore");
  assert.equal(nextGraphLod("overview", 1.5), "detail");
  assert.equal(nextGraphLod("detail", 0.1), "overview");
});

test("rounded corners never leave the orthogonal corner box", () => {
  const d = roundedOrthogonalPath([{ x: 0, y: 0 }, { x: 10, y: 0 }, { x: 10, y: 3 }, { x: 30, y: 3 }], 6);
  const nums = d.match(/-?\d+(\.\d+)?/g)!.map(Number);
  for (let i = 0; i < nums.length; i += 2) { assert.ok(nums[i] >= 0 && nums[i] <= 30); assert.ok(nums[i + 1] >= 0 && nums[i + 1] <= 3); }
});

test("legacy rollback scene keeps the previous Bezier helper output", () => {
  const scene = computeLegacyGraphScene([node("a"), node("b")], [edge("ab", "a", "b")], card);
  assert.equal(scene.engine, "legacy-bezier");
  assert.match(scene.edges[0].d, /^M \d+ [\d.]+ C/);
});

test("bounded sizes: 1k/5k scene builds with finite bounds (timing printed, budgets need a browser trace)", () => {
  const g = layeredGraph(42, 10, 100, 5, 0.1);
  const t0 = performance.now();
  const scene = computeGraphScene(g.nodes, g.edges, card);
  const t1 = performance.now();
  const placement = placeEdgeLabels(scene, scene.edges.slice(0, 300).map(e => e.edgeId));
  const t2 = performance.now();
  assert.ok(Number.isFinite(scene.bounds.width) && scene.bounds.width > 0);
  assert.equal(scene.edges.length + scene.hiddenEdges.length, g.edges.length);
  console.log(`[scene] ${g.nodes.length} nodes / ${g.edges.length} edges: layout+route ${(t1 - t0).toFixed(0)} ms, 300 labels ${(t2 - t1).toFixed(0)} ms (Node, unverified vs. browser budget)`);
  assert.ok(placement.eligible === 300);
});
