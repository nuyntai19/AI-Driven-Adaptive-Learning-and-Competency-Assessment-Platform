import { useEffect, useId, useMemo, useRef, useState } from "react";
import type { KnowledgeGraphNodeDto, KnowledgeGraphEdgeDto, KnowledgeNodeType, KnowledgeRelationType } from "../../types/knowledgeGraph";
import { computeDeterministicDagLayout, computeEdgePath } from "../../utils/knowledgeGraphLayout";
import { cameraViewBox, fitGraphCamera, graphLabelLines, graphNeighborhood, panGraphCamera, zoomGraphCamera, type GraphCamera } from "../../utils/knowledgeGraphViewport";

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
}

const relations: { type: KnowledgeRelationType; label: string }[] = [
  { type: "PrerequisiteOf", label: "Tiên quyết" }, { type: "PartOf", label: "Thuộc về" },
  { type: "RelatedTo", label: "Liên quan" }, { type: "CausesErrorIn", label: "Gây lỗi" },
];
const cardWidth = 240, cardHeight = 100;

/** Read-only visualization. Authoring stays in the existing permission-checked inspector. */
export function KnowledgeGraphCanvas(props: Props) {
  const [query, setQuery] = useState("");
  const [focusId, setFocusId] = useState<string | null>(null);
  const [camera, setCamera] = useState<GraphCamera | null>(null);
  const [size, setSize] = useState({ width: 800, height: 580 });
  const frame = useRef<HTMLDivElement>(null);
  const drag = useRef<{ x: number; y: number; camera: GraphCamera; moved: boolean } | null>(null);
  const suppressClick = useRef(false);
  const markerId = useId().replace(/:/g, "");
  const graph = useMemo(() => graphNeighborhood(props.nodes, props.edges, focusId), [props.nodes, props.edges, focusId]);
  const layout = useMemo(() => computeDeterministicDagLayout(graph.nodes, graph.edges, {
    cardWidth, cardHeight, gapX: 85, gapY: 28, paddingX: 50, paddingY: 50, minWidth: 340, minHeight: 200,
  }), [graph]);
  const fitted = fitGraphCamera(layout, size);
  const activeCamera = camera ?? fitted;
  const view = cameraViewBox(activeCamera, size);
  const focusNode = props.nodes.find(n => n.nodeId === focusId);
  const results = props.nodes.filter(n => `${n.nodeName} ${n.nodeCode}`.toLocaleLowerCase("vi").includes(query.trim().toLocaleLowerCase("vi")));
  const adjacentIds = new Set(graph.edges.filter(e => e.sourceNodeId === props.selectedNodeId || e.targetNodeId === props.selectedNodeId)
    .flatMap(e => [e.sourceNodeId, e.targetNodeId]));

  useEffect(() => {
    const element = frame.current;
    if (!element) return;
    const observer = new ResizeObserver(([entry]) => {
      setSize({ width: Math.max(1, entry.contentRect.width), height: Math.max(1, entry.contentRect.height) });
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  useEffect(() => { setCamera(null); }, [layout]);

  const focus = (id: string) => {
    setFocusId(id); setCamera(null); setQuery(""); props.onSelectNode(id);
  };
  const overview = () => { setFocusId(null); setCamera(null); };
  const zoom = (factor: number) => setCamera(zoomGraphCamera(activeCamera, factor, fitted.scale));

  return <section aria-label="Bản đồ tri thức tương tác" data-testid="knowledge-graph-canvas">
    <div className="border-b border-[var(--th-border)] bg-[var(--th-surface-raised)] p-3 space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <button type="button" className="th-secondary-button" onClick={overview}>Toàn cảnh</button>
        <button type="button" className="th-secondary-button" onClick={() => setCamera(null)}>Vừa khung</button>
        <button type="button" className="th-secondary-button" disabled={!props.selectedNodeId || !props.nodes.some(n => n.nodeId === props.selectedNodeId)}
          onClick={() => props.selectedNodeId && focus(props.selectedNodeId)}>Nút & liên kết gần</button>
        <div className="flex items-center gap-1 ml-auto">
          <button type="button" className="th-icon-button" aria-label="Thu nhỏ đồ thị" onClick={() => zoom(1 / 1.25)}>−</button>
          <output className="w-12 text-center text-xs tabular-nums" aria-label="Mức phóng đồ thị">{Math.round(activeCamera.scale * 100)}%</output>
          <button type="button" className="th-icon-button" aria-label="Phóng to đồ thị" onClick={() => zoom(1.25)}>+</button>
          <button type="button" className="th-secondary-button" onClick={() => setCamera({ ...activeCamera, scale: 1 })}>100%</button>
        </div>
      </div>
      <div className="flex flex-wrap items-center gap-3 text-xs text-[var(--th-text-secondary)]">
        <span>{focusNode ? `Đang xem quanh: ${focusNode.nodeName}` : "Toàn bộ đồ thị môn học"} · {graph.nodes.length}/{props.nodes.length} nút · {graph.edges.length} liên kết</span>
        <label className="flex items-center gap-2 ml-auto"><input type="checkbox" checked={props.hideInactive} onChange={e => props.onHideInactive(e.target.checked)} />Ẩn nút đã tắt ({props.inactiveCount})</label>
      </div>
      {props.nodes.some(n => n.nodeType === "Chapter") && <label className="flex flex-wrap items-center gap-2 text-sm font-semibold">
        Chương / nhóm:
        <select className="th-select max-w-full min-w-0" aria-label="Chương hoặc nhóm tri thức đang xem"
          value={focusNode?.nodeType === "Chapter" ? focusNode.nodeId : focusNode ? "custom" : ""}
          onChange={e => e.target.value ? focus(e.target.value) : overview()}>
          <option value="">Toàn bộ đồ thị</option>
          {focusNode && focusNode.nodeType !== "Chapter" && <option value="custom">Cụm quanh nút đã chọn</option>}
          {props.nodes.filter(n => n.nodeType === "Chapter").map(n => <option key={n.nodeId} value={n.nodeId}>{n.nodeName} ({props.nodes.filter(child => child.parentNodeId === n.nodeId).length} nút con)</option>)}
        </select>
      </label>}
      <div className="relative">
        <input className="th-input w-full" aria-label="Tìm điểm tri thức" placeholder="Tìm tên hoặc mã nút để xem rõ một nhóm liên quan…" value={query} onChange={e => setQuery(e.target.value)} />
        {query.trim() && <div className="absolute top-full z-10 mt-1 max-h-64 w-full overflow-y-auto rounded-lg border border-[var(--th-border)] bg-[var(--th-surface)] shadow-lg">
          <p className="p-2 text-xs text-[var(--th-text-muted)]">{results.length} kết quả · chọn để tập trung</p>
          {results.map(node => <button type="button" key={node.nodeId} className="th-focus-ring block w-full border-t border-[var(--th-border-subtle)] p-3 text-left hover:bg-[var(--th-surface-raised)]" onClick={() => focus(node.nodeId)}>
            <span className="block text-sm font-semibold">{node.nodeName}</span><span className="text-xs text-[var(--th-text-muted)]">{node.nodeCode} · {props.nodeLabels[node.nodeType]}</span>
          </button>)}
          {!results.length && <p className="p-3 text-sm">Không có nút phù hợp.</p>}
        </div>}
      </div>
      <div className="flex flex-wrap gap-x-4 gap-y-2 text-xs text-[var(--th-text-secondary)]">
        {relations.map(r => <span className="flex items-center gap-1.5" key={r.type}><span className="h-2 w-5 rounded" style={{ background: props.edgeColors[r.type] }} />{r.label}</span>)}
        <span>Kéo nền để di chuyển · dùng +/− để phóng/thu · Toàn cảnh để trở về</span>
      </div>
    </div>
    <div ref={frame} className="relative h-[580px] min-w-0 overflow-hidden bg-[var(--th-bg)]" data-testid="graph-viewport">
      {!graph.nodes.length ? <p className="p-8 text-center text-[var(--th-text-muted)]">Không có điểm tri thức trong phạm vi đang xem.</p> : <>
        <svg width="100%" height="100%" viewBox={`${view.x} ${view.y} ${view.width} ${view.height}`} preserveAspectRatio="none"
          aria-label="Sơ đồ liên kết tri thức; chọn nút để xem chi tiết" tabIndex={0} className="th-focus-ring select-none" style={{ touchAction: "none", cursor: "grab" }}
          onPointerDown={e => {
            if (e.button !== 0 || (e.target as Element).closest("[data-graph-interactive]")) return;
            drag.current = { x: e.clientX, y: e.clientY, camera: activeCamera, moved: false }; suppressClick.current = false;
            e.currentTarget.setPointerCapture(e.pointerId);
          }}
          onPointerMove={e => {
            if (!drag.current) return;
            const dx = e.clientX - drag.current.x, dy = e.clientY - drag.current.y;
            if (Math.hypot(dx, dy) > 4) drag.current.moved = true;
            if (drag.current.moved) setCamera(panGraphCamera(drag.current.camera, dx, dy));
          }}
          onPointerUp={() => { suppressClick.current = drag.current?.moved ?? false; drag.current = null; }}
          onPointerCancel={() => { drag.current = null; suppressClick.current = true; }}
          onClick={() => { if (!suppressClick.current) props.onClear(); suppressClick.current = false; }}
          onKeyDown={e => {
            const deltas: Record<string, [number, number]> = { ArrowLeft: [60, 0], ArrowRight: [-60, 0], ArrowUp: [0, 60], ArrowDown: [0, -60] };
            if (deltas[e.key]) { e.preventDefault(); setCamera(panGraphCamera(activeCamera, ...deltas[e.key])); }
            else if (e.key === "+" || e.key === "=") { e.preventDefault(); zoom(1.25); }
            else if (e.key === "-") { e.preventDefault(); zoom(1 / 1.25); }
            else if (e.key === "Escape") { overview(); props.onClear(); }
          }}>
          <defs>{relations.map(r => <marker key={r.type} id={`${markerId}-${r.type}`} viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse"><path d="M 0 1 L 10 5 L 0 9 z" fill={props.edgeColors[r.type]} /></marker>)}</defs>
          {graph.edges.map(edge => {
            const source = layout.positions.get(edge.sourceNodeId), target = layout.positions.get(edge.targetNodeId);
            if (!source || !target) return null;
            const selected = edge.edgeId === props.selectedEdgeId;
            const connected = edge.sourceNodeId === props.selectedNodeId || edge.targetNodeId === props.selectedNodeId;
            const color = props.edgeColors[edge.relationType];
            const path = computeEdgePath(source, target, cardWidth, cardHeight);
            return <g key={edge.edgeId} data-graph-interactive="edge" role="button" tabIndex={0}
              aria-label={`${relations.find(r => r.type === edge.relationType)?.label}: ${props.nodes.find(n => n.nodeId === edge.sourceNodeId)?.nodeName} → ${props.nodes.find(n => n.nodeId === edge.targetNodeId)?.nodeName}`}
              style={{ cursor: "pointer" }}
              onClick={e => { e.stopPropagation(); props.onSelectEdge(edge.edgeId); }}
              onKeyDown={e => { e.stopPropagation(); if (e.key === "Enter" || e.key === " ") { e.preventDefault(); props.onSelectEdge(edge.edgeId); } }}>
              <path d={path.d} fill="none" stroke="transparent" strokeWidth={18} />
              <path d={path.d} fill="none" stroke={color} strokeWidth={selected || connected ? 3 : 1.8} opacity={props.selectedNodeId && !connected ? 0.18 : 0.8}
                strokeDasharray={edge.relationType === "RelatedTo" ? "6 5" : undefined} markerEnd={`url(#${markerId}-${edge.relationType})`} />
              {(activeCamera.scale >= 0.6 || selected) && <g><rect x={path.midX - 18} y={path.midY - 10} width={36} height={20} rx={5} fill="var(--th-surface)" stroke={color} />
                <text x={path.midX} y={path.midY + 4} textAnchor="middle" fontSize={11} fill={color}>{edge.weight}</text></g>}
            </g>;
          })}
          {graph.nodes.map(node => {
            const pos = layout.positions.get(node.nodeId)!;
            const selected = node.nodeId === props.selectedNodeId;
            const color = node.isActive ? props.nodeColors[node.nodeType] : "#94a3b8";
            return <g key={node.nodeId} transform={`translate(${pos.x},${pos.y})`} data-graph-interactive="node" role="button" tabIndex={0}
              aria-label={`Điểm tri thức: ${node.nodeName}`} style={{ cursor: "pointer", opacity: props.selectedNodeId && !selected && !adjacentIds.has(node.nodeId) ? 0.45 : 1 }}
              onClick={e => { e.stopPropagation(); props.onSelectNode(node.nodeId); }}
              onDoubleClick={e => { e.stopPropagation(); focus(node.nodeId); }}
              onKeyDown={e => { e.stopPropagation(); if (e.key === "Enter" || e.key === " ") { e.preventDefault(); props.onSelectNode(node.nodeId); } }}>
              <title>{node.nodeName} ({node.nodeCode}) · {props.nodeLabels[node.nodeType]}{node.isActive ? "" : " · Đã tắt"}</title>
              <rect width={cardWidth} height={cardHeight} rx={10} fill="var(--th-surface)" stroke={selected ? "var(--th-teal)" : "var(--th-border)"} strokeWidth={selected ? 3 : 1.5} strokeDasharray={node.isActive ? undefined : "6 4"} />
              <rect width={cardWidth} height={5} rx={2} fill={color} />
              <text x={12} y={25} fontSize={11} fill={color} fontFamily="monospace">{node.nodeCode.length > 30 ? `${node.nodeCode.slice(0, 29)}…` : node.nodeCode}</text>
              {graphLabelLines(node.nodeName).map((line, i) => <text key={i} x={12} y={46 + i * 17} fontSize={13} fontWeight={700} fill="var(--th-text)">{line}</text>)}
              <text x={12} y={85} fontSize={10} fill="var(--th-text-muted)">{props.nodeLabels[node.nodeType]} · {node.isActive ? `${node.examImportance}% thi · ${node.estimatedLearningMinutes}p` : "Đã tắt hoạt động"}</text>
            </g>;
          })}
        </svg>
        <div className="absolute bottom-3 right-3 hidden sm:block rounded-lg border border-[var(--th-border)] bg-[var(--th-surface)] p-2 shadow-md">
          <p className="mb-1 text-[11px] text-[var(--th-text-muted)]">Định vị · bấm để di chuyển</p>
          <svg width={180} height={100} viewBox={`0 0 ${layout.width} ${layout.height}`} preserveAspectRatio="none" aria-label="Bản đồ thu nhỏ" role="img" style={{ cursor: "crosshair" }}
            onClick={e => { const rect = e.currentTarget.getBoundingClientRect(); setCamera({ ...activeCamera, x: (e.clientX - rect.left) / rect.width * layout.width, y: (e.clientY - rect.top) / rect.height * layout.height }); }}>
            {graph.nodes.map(node => { const p = layout.positions.get(node.nodeId)!; return <rect key={node.nodeId} x={p.x} y={p.y} width={cardWidth} height={cardHeight} fill={node.nodeId === props.selectedNodeId ? "var(--th-teal)" : props.nodeColors[node.nodeType]} opacity={0.7} />; })}
            <rect x={view.x} y={view.y} width={view.width} height={view.height} fill="none" stroke="var(--th-text)" strokeWidth={2} vectorEffect="non-scaling-stroke" />
          </svg>
        </div>
      </>}
    </div>
    <p className="border-t border-[var(--th-border)] px-3 py-2 text-xs text-[var(--th-text-muted)]">Toàn cảnh giữ đủ mọi nút. Khi chữ quá nhỏ, tìm một chủ đề hoặc chọn nút rồi bấm “Nút & liên kết gần”. Đây chỉ là bộ lọc xem, không thay đổi đồ thị.</p>
  </section>;
}
