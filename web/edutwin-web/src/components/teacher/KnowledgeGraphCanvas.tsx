import { memo, useCallback, useEffect, useId, useMemo, useRef, useState, type KeyboardEvent as ReactKeyboardEvent } from "react";
import type { KnowledgeGraphNodeDto, KnowledgeGraphEdgeDto, KnowledgeNodeType, KnowledgeRelationType } from "../../types/knowledgeGraph";
import { cameraViewBox, fitGraphBounds, graphLabelLines, graphNeighborhood, nextGraphLod, panGraphCamera, zoomGraphCamera, type GraphCamera, type GraphLod } from "../../utils/knowledgeGraphViewport";
import type { GraphScene, SceneEdge } from "../../utils/knowledgeGraphScene";
import { estimateTextWidth, formatEdgeWeight } from "../../utils/knowledgeGraphLabels";
import { inflateRect, rectsOverlap, type Rect } from "../../utils/knowledgeGraphGeometry";
import { useGraphScene, useLabelPlacement } from "./useKnowledgeGraphLayout";

interface Props {
  nodes: KnowledgeGraphNodeDto[];
  edges: KnowledgeGraphEdgeDto[];
  selectedNodeId: string | null;
  selectedEdgeId: string | null;
  onSelectNode: (id: string) => void;
  onSelectEdge: (id: string) => void;
  onClear: () => void;
  hideInactive: boolean;
  inactiveCount: number;
  onHideInactive: (hide: boolean) => void;
  nodeColors: Record<KnowledgeNodeType, string>;
  nodeLabels: Record<KnowledgeNodeType, string>;
  edgeColors: Record<KnowledgeRelationType, string>;
  /** Tenant/actor/scope identity for the layout cache; geometry is never reused across scopes. */
  cacheScope?: string;
}

/** Colour is never the only cue: each relation type also has its own dash pattern and name. */
const relations: { type: KnowledgeRelationType; label: string; dash?: string }[] = [
  { type: "PrerequisiteOf", label: "Tiên quyết" }, { type: "PartOf", label: "Thuộc về", dash: "12 4" },
  { type: "RelatedTo", label: "Liên quan", dash: "6 5" }, { type: "CausesErrorIn", label: "Gây lỗi", dash: "2 4" },
];
const relationInfo = new Map(relations.map(r => [r.type, r]));
const cardWidth = 240, cardHeight = 118;
/** Starting budgets from the plan (§6); beyond them the canvas stays in a reduced overview. */
const detailBudget = { nodes: 300, edges: 1500 };
const maxBadges = 80;
const listPageSize = 50;
const labelFontSize = 12;
const routingFlagKey = "edutwin.graph.routing";

type LabelMode = "auto" | "focus" | "all";
type RoutingEngine = "orthogonal" | "legacy";
type ActiveItem = { kind: "node" | "edge"; id: string };

function readRoutingEngine(): RoutingEngine {
  try { return localStorage.getItem(routingFlagKey) === "legacy" ? "legacy" : "orthogonal"; } catch { return "orthogonal"; }
}

const widthCache = new Map<string, number>();
let measureContext: CanvasRenderingContext2D | null | undefined;
/** Real text width after fonts load; deterministic estimate when canvas measuring is unavailable. */
function measureLabel(text: string, fontSize: number): number {
  const key = `${fontSize}|${text}`;
  const cached = widthCache.get(key);
  if (cached !== undefined) return cached;
  if (measureContext === undefined) {
    try { measureContext = document.createElement("canvas").getContext("2d"); } catch { measureContext = null; }
  }
  let width = estimateTextWidth(text, fontSize);
  if (measureContext) {
    measureContext.font = `600 ${fontSize}px ${getComputedStyle(document.body).fontFamily || "sans-serif"}`;
    width = measureContext.measureText(text).width;
  }
  widthCache.set(key, width);
  return width;
}

function useDebounced<T>(value: T, ms: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => { const t = setTimeout(() => setDebounced(value), ms); return () => clearTimeout(t); }, [value, ms]);
  return debounced;
}

const sceneOptions = { cardWidth, cardHeight };
const emptyScene: GraphScene = { engine: "orthogonal-v1", nodes: new Map(), edges: [], edgeById: new Map(), hiddenEdges: [], invalidEdgeIds: [],
  bounds: { x: 0, y: 0, width: 0, height: 0 }, cardWidth, cardHeight, rankCount: 0, hasDependencyCycle: false };

interface NodeCardHandlers {
  register: (key: string, element: SVGGElement | null) => void;
  select: (id: string) => void;
  focusSubgraph: (id: string) => void;
  activate: (id: string) => void;
  keyDown: (event: ReactKeyboardEvent, id: string) => void;
}

/**
 * Memoised card: selecting a node only re-renders the cards whose state changes. Dimming of
 * unrelated cards is a CSS rule on the SVG (`data-dim` + `data-near`), not a per-card prop.
 */
const NodeCard = memo(function NodeCard({ node, x, y, selected, active, near, overview, color, typeLabel, handlers }: {
  node: KnowledgeGraphNodeDto; x: number; y: number; selected: boolean; active: boolean; near: boolean; overview: boolean;
  color: string; typeLabel: string; handlers: NodeCardHandlers;
}) {
  const ref = useCallback((element: SVGGElement | null) => handlers.register(`node:${node.nodeId}`, element), [handlers, node.nodeId]);
  return <g ref={ref} transform={`translate(${x},${y})`} data-graph-interactive="node" data-node-id={node.nodeId} data-near={near || undefined}
    role="button" tabIndex={-1} aria-pressed={selected}
    aria-label={`Điểm tri thức: ${node.nodeName}`} style={{ cursor: "pointer" }}
    onFocus={() => handlers.activate(node.nodeId)}
    onClick={e => { e.stopPropagation(); handlers.select(node.nodeId); }}
    onDoubleClick={e => { e.stopPropagation(); handlers.focusSubgraph(node.nodeId); }}
    onKeyDown={e => handlers.keyDown(e, node.nodeId)}>
    <title>{node.nodeName} ({node.nodeCode}) · {typeLabel}{node.gradeLevel ? ` · Khối ${node.gradeLevel}` : node.gradeLevels?.length ? ` · Khối ${node.gradeLevels.join(", ")}` : " · Chưa xác định khối"}{node.sources?.map(s => ` · ${s.curriculumTitle || "Giáo trình"}`).join("")}{node.isActive ? "" : " · Đã tắt"}</title>
    <rect width={cardWidth} height={cardHeight} rx={10} fill="var(--th-surface)" stroke={selected || active ? "var(--th-teal)" : "var(--th-border)"} strokeWidth={selected ? 3 : active ? 2.5 : 1.5} strokeDasharray={node.isActive ? undefined : "6 4"} />
    <rect width={cardWidth} height={5} rx={2} fill={color} />
    {overview ? graphLabelLines(node.nodeName, 15).map((line, i) => <text key={i} x={12} y={48 + i * 32} fontSize={26} fontWeight={700} fill="var(--th-text)">{line}</text>) : <>
      <text x={12} y={25} fontSize={11} fill={color} fontFamily="monospace">{node.nodeCode.length > 30 ? `${node.nodeCode.slice(0, 29)}…` : node.nodeCode}</text>
      {graphLabelLines(node.nodeName).map((line, i) => <text key={i} x={12} y={46 + i * 17} fontSize={13} fontWeight={700} fill="var(--th-text)">{line}</text>)}
      <text x={12} y={85} fontSize={10} fill="var(--th-text-muted)">{typeLabel} · {node.isActive ? `${node.examImportance}% thi · ${node.estimatedLearningMinutes}p` : "Đã tắt hoạt động"}</text>
      <text x={12} y={104} fontSize={10} fill="var(--th-teal)">{node.gradeLevel ? `Khối ${node.gradeLevel}` : node.gradeLevels?.length ? `Khối ${node.gradeLevels.join(", ")}` : "Chưa xác định khối"}{node.sources?.[0]?.curriculumTitle ? ` · ${node.sources[0].curriculumTitle.slice(0, 27)}` : ""}</text>
    </>}
  </g>;
});

const nodeRect = (n: { x: number; y: number; width: number; height: number }): Rect => ({ x: n.x, y: n.y, width: n.width, height: n.height });

/** Read-only visualization. Authoring stays in the existing permission-checked inspector. */
export function KnowledgeGraphCanvas(props: Props) {
  const [query, setQuery] = useState("");
  const [focusId, setFocusId] = useState<string | null>(null);
  const [camera, setCamera] = useState<GraphCamera | null>(null);
  const [size, setSize] = useState({ width: 800, height: 580 });
  const [lodState, setLodState] = useState<GraphLod>("detail");
  const [labelMode, setLabelMode] = useState<LabelMode>("auto");
  const [engine, setEngine] = useState<RoutingEngine>(readRoutingEngine);
  const [hoverEdgeId, setHoverEdgeId] = useState<string | null>(null);
  const [activeItem, setActiveItem] = useState<ActiveItem | null>(null);
  const [fontsVersion, setFontsVersion] = useState(0);
  const [listLimit, setListLimit] = useState(listPageSize);
  const [listRelation, setListRelation] = useState<KnowledgeRelationType | "">("");
  const frame = useRef<HTMLDivElement>(null);
  const svgRef = useRef<SVGSVGElement>(null);
  const itemRefs = useRef(new Map<string, SVGGElement>());
  const edgeAnchor = useRef<string | null>(null);
  const drag = useRef<{ x: number; y: number; camera: GraphCamera; moved: boolean } | null>(null);
  const suppressClick = useRef(false);
  const rafId = useRef<number | null>(null);
  const pendingCamera = useRef<GraphCamera | null>(null);
  const markerId = useId().replace(/:/g, "");

  // Indexes built once per data revision (no .find inside render loops).
  const nodeById = useMemo(() => new Map(props.nodes.map(n => [n.nodeId, n])), [props.nodes]);
  const edgeDtoById = useMemo(() => new Map(props.edges.map(e => [e.edgeId, e])), [props.edges]);
  const childCountByParent = useMemo(() => {
    const counts = new Map<string, number>();
    for (const child of props.nodes) if (child.parentNodeId) counts.set(child.parentNodeId, (counts.get(child.parentNodeId) ?? 0) + 1);
    return counts;
  }, [props.nodes]);
  const duplicateNames = useMemo(() => {
    const seen = new Set<string>(), dup = new Set<string>();
    for (const n of props.nodes) { if (seen.has(n.nodeName)) dup.add(n.nodeName); seen.add(n.nodeName); }
    return dup;
  }, [props.nodes]);

  const graph = useMemo(() => graphNeighborhood(props.nodes, props.edges, focusId), [props.nodes, props.edges, focusId]);
  const overBudget = graph.nodes.length > detailBudget.nodes || graph.edges.length > detailBudget.edges;
  // Small graphs are laid out synchronously; larger ones in the layout worker (cached per scope).
  const layout = useGraphScene(graph.nodes, graph.edges, engine, sceneOptions, props.cacheScope ?? "");
  const scene = layout.scene ?? emptyScene;
  const hiddenEdgeIds = useMemo(() => new Set(scene.hiddenEdges.map(h => h.edgeId)), [scene]);
  const fitted = fitGraphBounds(scene.bounds, size);
  const activeCamera = camera ?? fitted;
  const view = cameraViewBox(activeCamera, size);
  const lod: GraphLod = overBudget ? "overview" : lodState;
  const focusNode = focusId ? nodeById.get(focusId) : undefined;
  const results = useMemo(() => {
    const q = query.trim().toLocaleLowerCase("vi");
    return q ? props.nodes.filter(n => `${n.nodeName} ${n.nodeCode}`.toLocaleLowerCase("vi").includes(q)) : [];
  }, [props.nodes, query]);
  const nodeName = (id: string) => {
    const n = nodeById.get(id);
    if (!n) return `#${id}`;
    const source = n.sources?.[0]?.curriculumTitle;
    return duplicateNames.has(n.nodeName) && source ? `${n.nodeName} (${source})` : n.nodeName;
  };
  const edgeText = (e: { relationType: KnowledgeRelationType; sourceNodeId: string; targetNodeId: string; weight: number }) =>
    `${relationInfo.get(e.relationType)?.label}: ${nodeName(e.sourceNodeId)} → ${nodeName(e.targetNodeId)} · trọng số ${formatEdgeWeight(e.weight)}`;

  const selectedNodeId = props.selectedNodeId;
  const incidentToSelected = useMemo(() => new Set(selectedNodeId
    ? scene.edges.filter(e => e.sourceNodeId === selectedNodeId || e.targetNodeId === selectedNodeId).map(e => e.edgeId) : []), [scene, selectedNodeId]);
  const adjacentIds = useMemo(() => {
    const ids = new Set<string>();
    for (const id of incidentToSelected) { const e = scene.edgeById.get(id)!; ids.add(e.sourceNodeId); ids.add(e.targetNodeId); }
    return ids;
  }, [incidentToSelected, scene]);

  useEffect(() => {
    const element = frame.current;
    if (!element) return;
    const observer = new ResizeObserver(([entry]) => {
      setSize({ width: Math.max(1, entry.contentRect.width), height: Math.max(1, entry.contentRect.height) });
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, []);
  useEffect(() => {
    let alive = true;
    document.fonts?.ready.then(() => { if (alive) { widthCache.clear(); setFontsVersion(v => v + 1); } });
    return () => { alive = false; if (rafId.current !== null) cancelAnimationFrame(rafId.current); };
  }, []);
  // Fit only when the geometry itself changes (scope, focus, filter, engine) — never on select/hover.
  useEffect(() => { setCamera(null); }, [scene]);
  useEffect(() => { setLodState(prev => nextGraphLod(prev, activeCamera.scale)); }, [activeCamera.scale]);
  useEffect(() => {
    if (activeItem) itemRefs.current.get(`${activeItem.kind}:${activeItem.id}`)?.focus({ preventScroll: true });
  }, [activeItem]);

  // ---- Weight labels: priority edges first, then (by mode/LOD) everything through the solver.
  const activeEdgeId = activeItem?.kind === "edge" ? activeItem.id : null;
  const labelRequest = useMemo(() => {
    const head = [props.selectedEdgeId, activeEdgeId, ...[...incidentToSelected].sort()]
      .filter((id): id is string => !!id && scene.edgeById.has(id));
    const wantsAll = lod !== "overview" && (labelMode === "all" || (labelMode === "auto" && lod === "detail"));
    const ids = lod === "overview" ? head.filter(id => id === props.selectedEdgeId || id === activeEdgeId)
      : [...new Set([...head, ...(wantsAll ? scene.edges.map(e => e.edgeId) : [])])];
    const headSet = new Set(head);
    return { ids, priorityCount: ids.filter(id => headSet.has(id)).length };
  }, [props.selectedEdgeId, activeEdgeId, incidentToSelected, scene, lod, labelMode]);
  const labelMargin = lod === "detail" ? 5 : 10; // ≥4 CSS px at the lower bound of each LOD band
  const { placement, pending: labelsPending } = useLabelPlacement(layout.scene, layout.sceneKey, labelRequest.ids,
    { fontSize: labelFontSize, margin: labelMargin, priorityCount: labelRequest.priorityCount }, measureLabel, fontsVersion);

  // ---- Culling by node bounds and *route* bounds (an edge can cross the view with both ends outside).
  const cullRect = inflateRect(view, Math.max(view.width, view.height) * 0.25);
  const pinnedNodes = new Set([props.selectedNodeId, activeItem?.kind === "node" ? activeItem.id : null].filter(Boolean));
  const pinnedEdges = new Set([props.selectedEdgeId, activeEdgeId].filter(Boolean));
  const visibleNodes = graph.nodes.filter(n => { const s = scene.nodes.get(n.nodeId); return s && (pinnedNodes.has(n.nodeId) || rectsOverlap(nodeRect(s), cullRect)); });
  const visibleEdges = scene.edges.filter(e => pinnedEdges.has(e.edgeId) || rectsOverlap(e.bounds, cullRect));
  const highlighted = (e: SceneEdge) => e.edgeId === props.selectedEdgeId || e.edgeId === hoverEdgeId || e.edgeId === activeEdgeId || incidentToSelected.has(e.edgeId);
  // Overview draws every edge as one non-interactive path per relation type (no per-edge DOM).
  // The merged geometry depends on the scene only, so selecting/hovering/panning never rebuilds it;
  // highlighted edges are drawn on top as individual, focusable elements. Every edge stays in the list.
  const mergeOrdinary = lod === "overview";
  const edgeOrder = [...(mergeOrdinary ? [] : visibleEdges.filter(e => !highlighted(e))), ...visibleEdges.filter(highlighted)];
  const mergedPaths = useMemo(() => mergeOrdinary
    ? relations.map(r => ({ type: r.type, d: scene.edges.filter(e => e.relationType === r.type).map(e => e.d).join(" ") })).filter(p => p.d)
    : [], [scene, mergeOrdinary]);
  const priorityIndex = new Map(labelRequest.ids.map((id, i) => [id, i]));
  const shownLabels = [...placement.placed.values()]
    .filter(l => pinnedEdges.has(l.edgeId) || rectsOverlap(l, view))
    .sort((a, b) => priorityIndex.get(a.edgeId)! - priorityIndex.get(b.edgeId)!)
    .slice(0, maxBadges);
  const shownLabelIds = new Set(shownLabels.map(l => l.edgeId));

  const calloutEdgeId = [props.selectedEdgeId, activeEdgeId, hoverEdgeId].find(id => id && edgeDtoById.has(id) && !shownLabelIds.has(id)) ?? null;
  const calloutEdge = calloutEdgeId ? edgeDtoById.get(calloutEdgeId)! : null;
  const statusText = `${lod === "overview" ? "Toàn cảnh rút gọn" : lod === "explore" ? "Khám phá" : "Chi tiết"} · ${graph.nodes.length} nút, ${graph.edges.length} liên kết · nhãn trọng số hiển thị ${shownLabels.length}/${labelRequest.ids.length}${scene.hiddenEdges.length ? ` · ${scene.hiddenEdges.length} liên kết tạm ẩn` : ""}`;
  const liveStatus = useDebounced(statusText, 600);

  const scheduleCamera = (next: GraphCamera) => {
    pendingCamera.current = next;
    if (rafId.current !== null) return;
    rafId.current = requestAnimationFrame(() => { rafId.current = null; if (pendingCamera.current) setCamera(pendingCamera.current); });
  };
  const focus = (id: string) => {
    setFocusId(id); setCamera(null); setQuery(""); props.onSelectNode(id);
  };
  const overview = () => { setFocusId(null); setCamera(null); };
  const zoom = (factor: number) => setCamera(zoomGraphCamera(activeCamera, factor, fitted.scale));
  const revealRect = (r: Rect) => {
    if (!rectsOverlap(r, view)) setCamera({ ...activeCamera, x: r.x + r.width / 2, y: r.y + r.height / 2 });
  };
  const focusItem = (item: ActiveItem) => {
    const rect = item.kind === "node" ? scene.nodes.get(item.id) : scene.edgeById.get(item.id)?.bounds;
    if (rect) revealRect(nodeRect(rect));
    setActiveItem(item);
  };
  const nearestNode = (fromId: string, key: string) => {
    const from = scene.nodes.get(fromId);
    if (!from) return null;
    const [ax, ay] = ({ ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] } as Record<string, [number, number]>)[key];
    let best: { id: string; score: number } | null = null;
    for (const n of scene.nodes.values()) {
      if (n.nodeId === fromId) continue;
      const dx = n.x - from.x, dy = n.y - from.y;
      const primary = dx * ax + dy * ay;
      if (primary <= 0) continue;
      const score = primary + 2 * Math.abs(ax ? dy : dx);
      if (!best || score < best.score || (score === best.score && n.nodeId < best.id)) best = { id: n.nodeId, score };
    }
    return best?.id ?? null;
  };
  const incidentEdges = (nodeId: string) => scene.edges.filter(e => e.sourceNodeId === nodeId || e.targetNodeId === nodeId).map(e => e.edgeId).sort();
  const cycleEdge = (nodeId: string, currentEdge: string | null) => {
    const list = incidentEdges(nodeId);
    if (!list.length) return;
    edgeAnchor.current = nodeId;
    focusItem({ kind: "edge", id: list[(list.indexOf(currentEdge ?? "") + 1) % list.length] });
  };
  const leaveItems = () => { setActiveItem(null); svgRef.current?.focus(); };
  const onNodeKey = (e: ReactKeyboardEvent, nodeId: string) => {
    e.stopPropagation();
    if (e.key === "Enter" || e.key === " ") { e.preventDefault(); props.onSelectNode(nodeId); }
    else if (e.key.startsWith("Arrow")) { e.preventDefault(); const next = nearestNode(nodeId, e.key); if (next) focusItem({ kind: "node", id: next }); }
    else if (e.key === "e" || e.key === "E") { e.preventDefault(); cycleEdge(nodeId, null); }
    else if (e.key === "Escape") { e.preventDefault(); leaveItems(); }
  };
  const onEdgeKey = (e: ReactKeyboardEvent, edge: SceneEdge) => {
    e.stopPropagation();
    if (e.key === "Enter" || e.key === " ") { e.preventDefault(); props.onSelectEdge(edge.edgeId); }
    else if (e.key === "ArrowLeft") { e.preventDefault(); focusItem({ kind: "node", id: edge.sourceNodeId }); }
    else if (e.key === "ArrowRight") { e.preventDefault(); focusItem({ kind: "node", id: edge.targetNodeId }); }
    else if (e.key === "e" || e.key === "E") { e.preventDefault(); cycleEdge(edgeAnchor.current ?? edge.sourceNodeId, edge.edgeId); }
    else if (e.key === "Escape") { e.preventDefault(); const anchor = edgeAnchor.current; if (anchor && scene.nodes.has(anchor)) focusItem({ kind: "node", id: anchor }); else leaveItems(); }
  };
  const selectFromList = (edgeId: string) => {
    props.onSelectEdge(edgeId);
    const routed = scene.edgeById.get(edgeId);
    if (routed) revealRect(routed.bounds);
  };
  const setRouting = (next: RoutingEngine) => {
    setEngine(next);
    try { if (next === "legacy") localStorage.setItem(routingFlagKey, "legacy"); else localStorage.removeItem(routingFlagKey); } catch { /* per-viewer convenience only */ }
  };

  // Stable handler object for memoised cards; it always calls the latest render's closures.
  const latest = useRef({ select: props.onSelectNode, focusSubgraph: focus, keyDown: onNodeKey });
  useEffect(() => { latest.current = { select: props.onSelectNode, focusSubgraph: focus, keyDown: onNodeKey }; });
  const nodeHandlers = useMemo<NodeCardHandlers>(() => ({
    register: (key, element) => { if (element) itemRefs.current.set(key, element); else itemRefs.current.delete(key); },
    select: id => latest.current.select(id),
    focusSubgraph: id => latest.current.focusSubgraph(id),
    activate: id => setActiveItem(a => (a?.kind === "node" && a.id === id ? a : { kind: "node", id })),
    keyDown: (event, id) => latest.current.keyDown(event, id),
  }), []);

  const hitWidth = Math.max(18, 14 / activeCamera.scale); // ≥14 CSS px target at any zoom
  const listEdges = graph.edges.filter(e => !listRelation || e.relationType === listRelation);

  return <section aria-label="Bản đồ tri thức tương tác" data-testid="knowledge-graph-canvas">
    <div className="border-b border-[var(--th-border)] bg-[var(--th-surface-raised)] p-3 space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <button type="button" className="th-secondary-button" onClick={overview}>Toàn cảnh</button>
        <button type="button" className="th-secondary-button" onClick={() => setCamera(null)}>Vừa khung</button>
        <button type="button" className="th-secondary-button" disabled={!props.selectedNodeId || !nodeById.has(props.selectedNodeId)}
          onClick={() => props.selectedNodeId && focus(props.selectedNodeId)}>Nút & liên kết gần</button>
        <div className="flex items-center gap-1 ml-auto">
          <button type="button" className="th-icon-button" aria-label="Thu nhỏ đồ thị" onClick={() => zoom(1 / 1.25)}>−</button>
          <output className="w-12 text-center text-xs tabular-nums" aria-label="Mức phóng đồ thị">{Math.round(activeCamera.scale * 100)}%</output>
          <button type="button" className="th-icon-button" aria-label="Phóng to đồ thị" onClick={() => zoom(1.25)}>+</button>
          <button type="button" className="th-secondary-button" onClick={() => setCamera({ ...activeCamera, scale: 1 })}>100%</button>
        </div>
      </div>
      <div className="flex flex-wrap items-center gap-3 text-xs text-[var(--th-text-secondary)]">
        <span>{focusNode ? `Đang xem quanh: ${focusNode.nodeName} (nút được chọn và liên kết trực tiếp)` : "Toàn bộ đồ thị trong phạm vi đang chọn"} · {graph.nodes.length}/{props.nodes.length} nút · {graph.edges.length} liên kết · đang vẽ {visibleNodes.length} nút, {visibleEdges.length} đường</span>
        <label className="flex items-center gap-2 ml-auto"><input type="checkbox" checked={props.hideInactive} onChange={e => props.onHideInactive(e.target.checked)} />Ẩn nút đã tắt ({props.inactiveCount})</label>
      </div>
      <div className="flex flex-wrap items-center gap-3 text-xs">
        <label className="flex items-center gap-2">Nhãn trọng số
          <select className="th-select" aria-label="Chế độ hiển thị nhãn trọng số" value={labelMode} onChange={e => setLabelMode(e.target.value as LabelMode)}>
            <option value="auto">Tự động theo mức phóng</option>
            <option value="focus">Chỉ liên kết đang xem</option>
            <option value="all">Yêu cầu hiện trọng số</option>
          </select>
        </label>
        <span data-testid="graph-label-counter">{labelsPending ? "Đang xếp nhãn… · " : ""}Nhãn hiển thị {shownLabels.length}/{labelRequest.ids.length}{placement.unplaced.length ? ` · ${placement.unplaced.length} nhãn ẩn vì hết chỗ (xem danh sách liên kết)` : ""}{lod === "overview" ? " · Toàn cảnh: đường gộp theo loại, phóng to hoặc chọn nút để thấy chiều mũi tên" : ""}</span>
        <label className="flex items-center gap-2 ml-auto">Kiểu đường nối
          <select className="th-select" aria-label="Kiểu đường nối" value={engine} onChange={e => setRouting(e.target.value as RoutingEngine)}>
            <option value="orthogonal">Vuông góc, tránh thẻ</option>
            <option value="legacy">Đường cong cũ (dự phòng)</option>
          </select>
        </label>
      </div>
      {props.nodes.some(n => n.nodeType === "Chapter") && <label className="flex flex-wrap items-center gap-2 text-sm font-semibold">
        Chương / nhóm:
        <select className="th-select max-w-full min-w-0" aria-label="Chương hoặc nhóm tri thức đang xem"
          value={focusNode?.nodeType === "Chapter" ? focusNode.nodeId : focusNode ? "custom" : ""}
          onChange={e => e.target.value ? focus(e.target.value) : overview()}>
          <option value="">Toàn bộ đồ thị</option>
          {focusNode && focusNode.nodeType !== "Chapter" && <option value="custom">Cụm quanh nút đã chọn</option>}
          {props.nodes.filter(n => n.nodeType === "Chapter").map(n => <option key={n.nodeId} value={n.nodeId}>{n.nodeName} ({childCountByParent.get(n.nodeId) ?? 0} nút con)</option>)}
        </select>
      </label>}
      <div className="relative">
        <input className="th-input w-full" aria-label="Tìm điểm tri thức" placeholder="Tìm tên hoặc mã nút để xem rõ một nhóm liên quan…" value={query} onChange={e => setQuery(e.target.value)} />
        {query.trim() && <div className="absolute top-full z-10 mt-1 max-h-64 w-full overflow-y-auto rounded-lg border border-[var(--th-border)] bg-[var(--th-surface)] shadow-lg">
          <p className="p-2 text-xs text-[var(--th-text-muted)]">{results.length} kết quả{results.length > listPageSize ? ` · hiện ${listPageSize} đầu tiên, gõ thêm để thu hẹp` : ""} · chọn để tập trung</p>
          {results.slice(0, listPageSize).map(node => <button type="button" key={node.nodeId} className="th-focus-ring block w-full border-t border-[var(--th-border-subtle)] p-3 text-left hover:bg-[var(--th-surface-raised)]" onClick={() => focus(node.nodeId)}>
            <span className="block text-sm font-semibold">{node.nodeName}</span><span className="text-xs text-[var(--th-text-muted)]">{node.nodeCode} · {props.nodeLabels[node.nodeType]}{node.sources?.[0]?.curriculumTitle ? ` · ${node.sources[0].curriculumTitle}` : ""}</span>
          </button>)}
          {!results.length && <p className="p-3 text-sm">Không có nút phù hợp.</p>}
        </div>}
      </div>
      <div className="flex flex-wrap gap-x-4 gap-y-2 text-xs text-[var(--th-text-secondary)]">
        {relations.map(r => <span className="flex items-center gap-1.5" key={r.type}>
          <svg width="28" height="8" aria-hidden="true"><line x1="0" y1="4" x2="28" y2="4" stroke={props.edgeColors[r.type]} strokeWidth="2.5" strokeDasharray={r.dash} /></svg>{r.label}</span>)}
        <span>Kéo nền để di chuyển · +/− phóng/thu · Tab vào sơ đồ, Enter chọn nút, mũi tên sang nút gần, E duyệt liên kết, Esc thoát</span>
      </div>
      {overBudget && <p role="status" className="text-sm text-amber-600">Phạm vi có {graph.nodes.length} nút / {graph.edges.length} liên kết, vượt ngân sách xem chi tiết ({detailBudget.nodes} nút / {detailBudget.edges} liên kết). Sơ đồ đang ở dạng toàn cảnh rút gọn; chọn chương hoặc tìm một nút để xem chi tiết. Danh sách liên kết bên dưới vẫn có đủ quan hệ.</p>}
      {scene.hasDependencyCycle && <p role="status" className="text-sm text-amber-600">Phát hiện vòng tiên quyết không hợp lệ: các nút trong vòng được đặt ở cột cuối để vẫn hiển thị được. Dữ liệu không bị sửa.</p>}
      {layout.message && <p role="status" className="text-sm text-amber-600">{layout.message}</p>}
    </div>
    <div ref={frame} className="relative h-[580px] min-w-0 overflow-hidden bg-[var(--th-bg)]" data-testid="graph-viewport" data-graph-engine={scene.engine} data-graph-lod={lod}
      data-layout-status={layout.status} data-layout-source={layout.source} aria-busy={layout.status === "pending" || labelsPending}>
      {!graph.nodes.length ? <p className="p-8 text-center text-[var(--th-text-muted)]">Không có điểm tri thức trong phạm vi đang xem.</p>
        : !layout.scene ? <p role="status" className="p-8 text-center text-[var(--th-text-muted)]">Đang tính bố cục cho {graph.nodes.length} nút và {graph.edges.length} liên kết…</p> : <>
        {layout.status === "pending" && <p role="status" className="absolute right-3 top-3 z-10 rounded-md border border-[var(--th-border)] bg-[var(--th-surface)] px-2 py-1 text-xs shadow">Đang tính lại bố cục…</p>}
        <svg ref={svgRef} width="100%" height="100%" viewBox={`${view.x} ${view.y} ${view.width} ${view.height}`} preserveAspectRatio="none"
          aria-label="Sơ đồ liên kết tri thức; Enter để vào các nút, mũi tên để di chuyển" tabIndex={0} className={`th-focus-ring select-none kg-${markerId}`} data-dim={props.selectedNodeId ? "1" : undefined} style={{ touchAction: "none", cursor: "grab" }}
          onPointerDown={e => {
            if (e.button !== 0 || (e.target as Element).closest("[data-graph-interactive]")) return;
            drag.current = { x: e.clientX, y: e.clientY, camera: activeCamera, moved: false }; suppressClick.current = false;
            e.currentTarget.setPointerCapture(e.pointerId);
          }}
          onPointerMove={e => {
            if (!drag.current) return;
            const dx = e.clientX - drag.current.x, dy = e.clientY - drag.current.y;
            if (Math.hypot(dx, dy) > 4) drag.current.moved = true;
            if (drag.current.moved) scheduleCamera(panGraphCamera(drag.current.camera, dx, dy));
          }}
          onPointerUp={() => { suppressClick.current = drag.current?.moved ?? false; drag.current = null; }}
          onPointerCancel={() => { drag.current = null; suppressClick.current = true; }}
          onClick={() => { if (!suppressClick.current) props.onClear(); suppressClick.current = false; }}
          onKeyDown={e => {
            if (e.target !== e.currentTarget) return;
            const deltas: Record<string, [number, number]> = { ArrowLeft: [60, 0], ArrowRight: [-60, 0], ArrowUp: [0, 60], ArrowDown: [0, -60] };
            if (deltas[e.key]) { e.preventDefault(); setCamera(panGraphCamera(activeCamera, ...deltas[e.key])); }
            else if (e.key === "+" || e.key === "=") { e.preventDefault(); zoom(1.25); }
            else if (e.key === "-") { e.preventDefault(); zoom(1 / 1.25); }
            else if (e.key === "Enter" || e.key === " ") {
              e.preventDefault();
              const start = props.selectedNodeId && scene.nodes.has(props.selectedNodeId) ? props.selectedNodeId
                : [...scene.nodes.values()].sort((a, b) => a.rank - b.rank || a.row - b.row)[0]?.nodeId;
              if (start) focusItem({ kind: "node", id: start });
            }
            else if (e.key === "Escape") { overview(); props.onClear(); }
          }}>
          <style>{`.kg-${markerId}[data-dim="1"] g[data-node-id]:not([data-near]){opacity:.45}`}</style>
          <defs>{relations.map(r => <marker key={r.type} id={`${markerId}-${r.type}`} viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse"><path d="M 0 1 L 10 5 L 0 9 z" fill={props.edgeColors[r.type]} /></marker>)}</defs>
          {mergedPaths.map(p => <path key={`merged-${p.type}`} data-overview-relation={p.type} d={p.d} fill="none" stroke={props.edgeColors[p.type]}
            strokeWidth={1.2} vectorEffect="non-scaling-stroke" opacity={props.selectedNodeId ? 0.3 : 0.7} strokeDasharray={relationInfo.get(p.type)?.dash} pointerEvents="none" aria-hidden="true" />)}
          {edgeOrder.map(edge => {
            const selected = edge.edgeId === props.selectedEdgeId;
            const strong = selected || edge.edgeId === hoverEdgeId || edge.edgeId === activeEdgeId || incidentToSelected.has(edge.edgeId);
            const color = props.edgeColors[edge.relationType];
            return <g key={edge.edgeId} ref={el => { if (el) itemRefs.current.set(`edge:${edge.edgeId}`, el); else itemRefs.current.delete(`edge:${edge.edgeId}`); }}
              data-graph-interactive="edge" data-edge-id={edge.edgeId} role="button" tabIndex={-1} aria-pressed={selected}
              aria-label={edgeText(edge)} style={{ cursor: "pointer" }}
              onMouseEnter={() => setHoverEdgeId(edge.edgeId)} onMouseLeave={() => setHoverEdgeId(h => h === edge.edgeId ? null : h)}
              onFocus={() => setActiveItem(a => a?.kind === "edge" && a.id === edge.edgeId ? a : { kind: "edge", id: edge.edgeId })}
              onClick={e => { e.stopPropagation(); props.onSelectEdge(edge.edgeId); }}
              onKeyDown={e => onEdgeKey(e, edge)}>
              <title>{edgeText(edge)}</title>
              <path d={edge.d} fill="none" stroke="transparent" strokeWidth={hitWidth} />
              <path d={edge.d} fill="none" stroke={color} strokeWidth={selected ? 3.4 : strong ? 2.6 : 1.8} opacity={props.selectedNodeId && !strong ? 0.28 : 0.9}
                strokeLinejoin="round" strokeDasharray={relationInfo.get(edge.relationType)?.dash} markerEnd={`url(#${markerId}-${edge.relationType})`} />
              {edge.edgeId === activeEdgeId && <path d={edge.d} fill="none" stroke="var(--th-teal)" strokeWidth={7} opacity={0.35} />}
            </g>;
          })}
          {shownLabels.map(label => {
            const edge = scene.edgeById.get(label.edgeId);
            if (!edge) return null;
            const color = props.edgeColors[edge.relationType];
            return <g key={`label-${label.edgeId}`} data-graph-interactive="edge-label" data-edge-label={label.edgeId} aria-hidden="true" style={{ cursor: "pointer" }}
              onClick={e => { e.stopPropagation(); props.onSelectEdge(label.edgeId); }}>
              <rect x={label.x} y={label.y} width={label.width} height={label.height} rx={5} fill="var(--th-surface)" stroke={color} strokeWidth={label.edgeId === props.selectedEdgeId ? 2.4 : 1.4} />
              <text x={label.cx} y={label.cy + labelFontSize * 0.36} textAnchor="middle" fontSize={labelFontSize} fontWeight={600} fill="var(--th-text)">{label.text}</text>
            </g>;
          })}
          {visibleNodes.map(node => {
            const pos = scene.nodes.get(node.nodeId)!;
            const selected = node.nodeId === props.selectedNodeId;
            return <NodeCard key={node.nodeId} node={node} x={pos.x} y={pos.y} selected={selected}
              active={activeItem?.kind === "node" && activeItem.id === node.nodeId}
              near={selected || adjacentIds.has(node.nodeId)} overview={lod === "overview"}
              color={node.isActive ? props.nodeColors[node.nodeType] : "#94a3b8"} typeLabel={props.nodeLabels[node.nodeType]} handlers={nodeHandlers} />;
          })}
        </svg>
        {calloutEdge && <div className="absolute left-3 top-3 max-w-[min(420px,calc(100%-24px))] rounded-lg border border-[var(--th-border)] bg-[var(--th-surface)] p-2 text-xs shadow-md" data-testid="graph-edge-callout">
          <p className="font-semibold">{calloutEdge.edgeId === props.selectedEdgeId ? "Liên kết đang chọn" : "Liên kết đang xem"}{hiddenEdgeIds.has(calloutEdge.edgeId) ? " (tạm ẩn trên sơ đồ)" : ""}</p>
          <p style={{ overflowWrap: "anywhere" }}>{edgeText(calloutEdge)}</p>
        </div>}
        <div className="absolute bottom-3 right-3 hidden sm:block rounded-lg border border-[var(--th-border)] bg-[var(--th-surface)] p-2 shadow-md">
          <p className="mb-1 text-[11px] text-[var(--th-text-muted)]">Định vị · bấm để di chuyển</p>
          <svg width={180} height={100} viewBox={`${scene.bounds.x} ${scene.bounds.y} ${scene.bounds.width} ${scene.bounds.height}`} preserveAspectRatio="none" aria-label="Bản đồ thu nhỏ" role="img" style={{ cursor: "crosshair" }}
            onClick={e => { const rect = e.currentTarget.getBoundingClientRect(); setCamera({ ...activeCamera, x: scene.bounds.x + (e.clientX - rect.left) / rect.width * scene.bounds.width, y: scene.bounds.y + (e.clientY - rect.top) / rect.height * scene.bounds.height }); }}>
            {graph.nodes.map(node => { const p = scene.nodes.get(node.nodeId); if (!p) return null; return <rect key={node.nodeId} x={p.x} y={p.y} width={cardWidth} height={cardHeight} fill={node.nodeId === props.selectedNodeId ? "var(--th-teal)" : props.nodeColors[node.nodeType]} opacity={0.7} />; })}
            <rect x={view.x} y={view.y} width={view.width} height={view.height} fill="none" stroke="var(--th-text)" strokeWidth={2} vectorEffect="non-scaling-stroke" />
          </svg>
        </div>
      </>}
    </div>
    <p className="sr-only" aria-live="polite">{liveStatus}</p>
    <details className="border-t border-[var(--th-border)] px-3 py-2 text-sm" data-testid="graph-edge-list">
      <summary className="cursor-pointer font-semibold">Danh sách liên kết trong sơ đồ ({graph.edges.length}){scene.hiddenEdges.length ? ` · ${scene.hiddenEdges.length} tạm ẩn trên sơ đồ` : ""}</summary>
      <div className="mt-2 flex flex-wrap items-center gap-2 text-xs">
        <label className="flex items-center gap-2">Loại liên kết
          <select className="th-select" value={listRelation} onChange={e => { setListRelation(e.target.value as KnowledgeRelationType | ""); setListLimit(listPageSize); }}>
            <option value="">Tất cả</option>
            {relations.map(r => <option key={r.type} value={r.type}>{r.label}</option>)}
          </select>
        </label>
        <span>{listEdges.length} liên kết · hiện {Math.min(listLimit, listEdges.length)}</span>
      </div>
      <div className="mt-2 overflow-x-auto">
        <table className="w-full text-xs" style={{ borderCollapse: "collapse" }}>
          <thead><tr className="text-left text-[var(--th-text-muted)]"><th className="p-1.5">Nguồn</th><th className="p-1.5">Loại</th><th className="p-1.5">Đích</th><th className="p-1.5">Trọng số</th><th className="p-1.5">Trên sơ đồ</th><th className="p-1.5"><span className="sr-only">Thao tác</span></th></tr></thead>
          <tbody>
            {listEdges.slice(0, listLimit).map(e => <tr key={e.edgeId} className="border-t border-[var(--th-border-subtle)]">
              <td className="p-1.5" style={{ overflowWrap: "anywhere" }}>{nodeName(e.sourceNodeId)}</td>
              <td className="p-1.5">{relationInfo.get(e.relationType)?.label}</td>
              <td className="p-1.5" style={{ overflowWrap: "anywhere" }}>{nodeName(e.targetNodeId)}</td>
              <td className="p-1.5 tabular-nums">{formatEdgeWeight(e.weight)}</td>
              <td className="p-1.5">{hiddenEdgeIds.has(e.edgeId) ? "Tạm ẩn (nút có quá nhiều liên kết)" : !scene.edgeById.has(e.edgeId) ? "Không vẽ được" : placement.placed.has(e.edgeId) ? "Có nhãn" : "Đường, nhãn ẩn"}</td>
              <td className="p-1.5"><button type="button" className="th-secondary-button" style={{ minHeight: 24 }} aria-pressed={e.edgeId === props.selectedEdgeId} aria-label={`Chọn liên kết ${edgeText(e)}`} onClick={() => selectFromList(e.edgeId)}>{e.edgeId === props.selectedEdgeId ? "Đang chọn" : "Chọn"}</button></td>
            </tr>)}
          </tbody>
        </table>
      </div>
      {listEdges.length > listLimit && <button type="button" className="th-secondary-button mt-2" onClick={() => setListLimit(l => l + listPageSize)}>Xem thêm {Math.min(listPageSize, listEdges.length - listLimit)} liên kết</button>}
    </details>
    <p className="border-t border-[var(--th-border)] px-3 py-2 text-xs text-[var(--th-text-muted)]">Toàn cảnh giữ đủ mọi nút. Khi chữ quá nhỏ, tìm một chủ đề hoặc chọn nút rồi bấm “Nút & liên kết gần”. Đây chỉ là bộ lọc xem, không thay đổi đồ thị.</p>
  </section>;
}
