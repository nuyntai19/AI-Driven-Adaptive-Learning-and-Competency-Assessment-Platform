import { frameworkTitle } from "../../utils/knowledgeFrameworkTitle";
import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { permissions } from "../../auth/permissions";
import { lockedTopicIds } from "../../utils/knowledgeDraftTopics";
import { invalidDraftEdges } from "../../utils/knowledgeDraftEdges";
import { diagnoseDraft, draftPreview, effectiveDraftEdges, type DraftIssue } from "../../utils/knowledgeDraftEditor";
import { graphEdgeColors, graphNodeColors, graphNodeLabels, relationInfo } from "../../utils/knowledgeRelations";
import { academicLifecycleError } from "../../utils/academicLifecycleError";
import type { KnowledgeGraphDto, KnowledgeGraphNodeDto } from "../../types/knowledgeGraph";
import { KnowledgeGraphCanvas } from "./KnowledgeGraphCanvas";
import { KnowledgeNodeEditDialog } from "./KnowledgeNodeEditDialog";
import { TeacherConfirmDialog, TeacherDrawer, TeacherModal } from "./TeacherOverlays";
import { RelationHelpDialog } from "./RelationHelpDialog";
import { TopicLibraryPanel } from "./TopicLibraryPanel";
import { FrameworkPicker } from "./FrameworkPicker";
import { DraftInspector, type DraftSelection } from "./DraftInspector";
import { DraftRelationDialog } from "./DraftRelationDialog";
import { useKnowledgeDraftEditor, type DraftIncludeRequest } from "./useKnowledgeDraftEditor";
import "./knowledgeDraftEditor.css";

export type { DraftIncludeRequest } from "./useKnowledgeDraftEditor";
interface Props {
  subjectId: string; gradeLevel?: number; versionId?: string; canEdit: boolean;
  onSelect: (id: string, grade: number) => void; onDirtyChange?: (dirty: boolean) => void;
  subjectCode?: string; subjectName?: string; onAddTopic?: () => void; includeRequest?: DraftIncludeRequest | null;
  readGraph?: KnowledgeGraphDto; onExit?: () => void; topicActions?: (id: string) => ReactNode;
  readLoading?: boolean; readError?: unknown; onRetryRead?: () => void;
}
type Mode = { kind: "link"; source: string | null } | { kind: "parent"; child: string } | null;
const smallScreen = () => !window.matchMedia("(min-width: 1700px)").matches;

export function KnowledgeVersionPanel(props: Props) {
  return props.gradeLevel ? <FrameworkEditor key={`${props.subjectId}:${props.gradeLevel}:${props.versionId ?? ""}`} {...props} gradeLevel={props.gradeLevel} /> : null;
}
function FrameworkEditor(props: Props & { gradeLevel: number }) {
  const e = useKnowledgeDraftEditor(props);
  const [selection, setSelection] = useState<DraftSelection>(null);
  const [mode, setMode] = useState<Mode>(null);
  const [drawer, setDrawer] = useState<"library" | "inspector" | null>(null);
  const [help, setHelp] = useState(false);
  const [picker, setPicker] = useState(false);
  const [editingTopic, setEditingTopic] = useState<KnowledgeGraphNodeDto | null>(null);
  const [deletingTopic, setDeletingTopic] = useState<KnowledgeGraphNodeDto | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [removingNode, setRemovingNode] = useState<string | null>(null);
  const [discarding, setDiscarding] = useState(false);
  const [review, setReview] = useState(false);
  const [showParents, setShowParents] = useState(false);
  const [hideInactive, setHideInactive] = useState(false);
  const [listSearch, setListSearch] = useState("");
  const [listLimit, setListLimit] = useState(40);
  const [reveal, setReveal] = useState<{ nodeIds: string[]; edgeIds: string[]; nonce: number }>();
  const [activeIssue, setActiveIssue] = useState<DraftIssue | null>(null);
  const [edgeForm, setEdgeForm] = useState<{ source: string; target: string } | null>(null);
  const [parentField, setParentField] = useState(false);
  const [screenWidth, setScreenWidth] = useState(() => window.innerWidth);
  const inspectorRef = useRef<HTMLDivElement>(null);
  useEffect(() => { const resize = () => setScreenWidth(window.innerWidth); window.addEventListener("resize", resize); return () => window.removeEventListener("resize", resize); }, []);
  const topics = e.catalog.data?.nodes ?? props.readGraph?.nodes ?? [];
  const catalogEdges = e.catalog.data?.edges ?? props.readGraph?.edges ?? [];
  const ids = useMemo(() => new Set(e.nodes.map(n => n.nodeId)), [e.nodes]);
  const sourceVersions = useMemo(() => (e.sourceVersions.data ?? e.versions.data ?? []).map(v => v.graphVersionId === e.selected?.graphVersionId ? { ...v, nodeMappings: e.nodes, edgeMappings: e.edges } : v), [e.sourceVersions.data, e.versions.data, e.selected?.graphVersionId, e.nodes, e.edges]);
  const preview = useMemo(() => e.editable ? draftPreview(e.catalog.data, e.nodes, e.edges, props.gradeLevel) : { nodes: props.readGraph?.nodes ?? [], edges: props.readGraph?.edges ?? [] }, [e.editable, e.catalog.data, e.nodes, e.edges, props.gradeLevel, props.readGraph]);
  const relations = effectiveDraftEdges(e.edges, catalogEdges);
  const issues = useMemo(() => e.editable ? diagnoseDraft(e.nodes, e.edges, e.catalog.data, sourceVersions, props.gradeLevel, e.catalog.isFetching, e.sourceVersions.isSuccess) : [], [e.editable, e.nodes, e.edges, e.catalog.data, e.catalog.isFetching, sourceVersions, props.gradeLevel, e.sourceVersions.isSuccess]);
  const issueNodes = new Set(issues.flatMap(i => i.nodeIds)), issueEdges = new Set(issues.flatMap(i => i.edgeIds));
  useEffect(() => { setActiveIssue(previous => previous ? issues.find(i => i.id === previous.id) ?? null : null); }, [issues]);
  const invalidEdges = invalidDraftEdges(e.edges, e.nodes, e.catalog.isSuccess ? e.catalog.data.edges : null, e.catalog.isFetching) ?? [];
  const name = (id: string) => topics.find(n => n.nodeId === id)?.nodeName ?? `#${id}`;
  const disabled = !e.editable || e.busy || !e.catalog.isSuccess;
  const select = (next: NonNullable<DraftSelection>, focus = false) => {
    setSelection(next); setParentField(false); setActiveIssue(null);
    if (focus) { setHideInactive(false); setReveal({ nodeIds: next.kind === "node" ? [next.id] : [], edgeIds: next.kind === "edge" ? [next.id] : [], nonce: Date.now() }); }
    if (smallScreen()) setDrawer("inspector");
  };
  useEffect(() => { if (!e.lastIncluded) return; setSelection({ kind: "node", id: e.lastIncluded }); setReveal({ nodeIds: [e.lastIncluded], edgeIds: [], nonce: Date.now() }); }, [e.lastIncluded]);
  const locateIssue = (issue: DraftIssue, id?: string) => {
    setActiveIssue(issue); setHideInactive(false); if (issue.kind === "parent") setShowParents(true);
    setSelection(issue.kind === "edge" && issue.edgeIds[0] ? { kind: "edge", id: issue.edgeIds[0] } : { kind: "node", id: id ?? issue.nodeIds[0] });
    setReveal({ nodeIds: issue.nodeIds, edgeIds: issue.edgeIds, nonce: Date.now() }); setParentField(issue.kind === "parent");
    if (smallScreen()) setDrawer("inspector");
    requestAnimationFrame(() => inspectorRef.current?.querySelector<HTMLElement>(issue.kind === "parent" ? "[data-parent-action]" : "[data-detail-heading]")?.focus());
  };
  const startLink = (source: string | null = null) => { setMode({ kind: "link", source }); setDrawer(null); };
  const chooseNode = (id: string) => {
    if (e.busy) return;
    if (mode?.kind === "link") { if (!ids.has(id) || !topics.some(n => n.nodeId === id)) return; if (!mode.source) setMode({ kind: "link", source: id }); else if (mode.source !== id) { setEdgeForm({ source: mode.source, target: id }); setMode(null); } }
    else if (mode?.kind === "parent") { if (mode.child === id || !ids.has(id)) return; e.setParent(mode.child, id); select({ kind: "node", id: mode.child }, true); setMode(null); setShowParents(true); }
    else select({ kind: "node", id: preview.nodes.find(n => n.nodeId === id)?.canonicalNodeId ?? id });
  };
  const inspector = <DraftInspector editor={e} selection={selection} topics={topics} relations={relations} versions={sourceVersions} grade={props.gradeLevel} versionId={props.versionId} issues={issues} activeIssue={activeIssue} lockedIds={lockedTopicIds(e.sourceVersions.data, e.curricula.data)} parentField={parentField} topicActions={props.topicActions}
    onClear={() => setSelection(null)} onSelect={s => select(s, true)} onLink={startLink} onParent={child => { setMode({ kind: "parent", child }); setParentField(true); setDrawer(null); }} onRemoveNode={setRemovingNode}
    onEditTopic={n => { setDrawer(null); setEditingTopic(n); }} onDeleteTopic={n => { setDeleteError(null); setDrawer(null); setDeletingTopic(n); }} />;
  const library = <TopicLibraryPanel topics={topics} versions={sourceVersions} curricula={e.curricula.data ?? []} selectedIds={ids} grade={props.gradeLevel} ready={e.catalog.isSuccess} loading={e.catalog.isPending} failed={e.catalog.isError}
    sourceLoading={e.sourceVersions.isPending || e.curricula.isPending} sourceFailed={e.sourceVersions.isError || e.curricula.isError} disabled={disabled}
    onRetry={() => e.catalog.refetch()} onRetrySources={() => { void e.sourceVersions.refetch(); void e.curricula.refetch(); }} onAdd={id => { e.addNode(id); setReveal({ nodeIds: [id], edgeIds: [], nonce: Date.now() }); }} onLocate={id => select({ kind: "node", id }, true)} onCreate={props.onAddTopic} />;
  const pickerContent = <FrameworkPicker editor={e} subjectId={props.subjectId} grade={props.gradeLevel} onSelect={(id, grade) => { setPicker(false); props.onSelect(id, grade); }} />;
  const shownNodes = useMemo(() => preview.nodes.filter(n => !hideInactive || n.isActive), [preview.nodes, hideInactive]);
  const shownEdges = useMemo(() => { const shownIds = new Set(shownNodes.map(n => n.nodeId)); return preview.edges.filter(r => shownIds.has(r.sourceNodeId) && shownIds.has(r.targetNodeId)); }, [preview.edges, shownNodes]);
  const saveDisabled = e.busy || !e.dirty || e.stale || !e.catalog.isSuccess || e.catalog.isFetching || invalidEdges.length > 0;
  const q = listSearch.toLocaleLowerCase("vi");
  const availableEdges = catalogEdges.filter(r => !e.edges.some(m => m.edgeId === r.edgeId) && ids.has(r.targetNodeId) && (ids.has(r.sourceNodeId) || r.relationType === "PrerequisiteOf"));
  const deleteBlockers = deletingTopic && { relations: catalogEdges.filter(r => r.sourceNodeId === deletingTopic.nodeId || r.targetNodeId === deletingTopic.nodeId), children: topics.filter(n => n.parentNodeId === deletingTopic.nodeId) };
  return <section id="kg-version-panel" className="kg-editor" data-testid="kg-draft-editor" onKeyDownCapture={event => { if (event.key === "Escape" && mode && !(event.target as Element).closest("input,textarea,select")) { event.preventDefault(); event.stopPropagation(); setMode(null); } }}>
    <header className="kg-editor-header"><div><span className="kg-eyebrow">{props.subjectName || props.subjectCode || "KHUNG TRI THỨC"} / KHỐI {props.gradeLevel}</span><h2>{e.selected ? frameworkTitle(e.selected) : "Chọn khung tri thức"}</h2><div className="kg-badges mt-2">{e.selected && <span>{e.selected.isLegacy ? "Dữ liệu chuyển tiếp" : e.selected.status === "Draft" ? "Bản nháp" : e.selected.status === "Published" ? "Đã duyệt" : "Lưu trữ"}</span>}{e.selected?.isCurrent && <span>Hiện hành</span>}<span>{e.dirty ? "Cấu trúc chưa lưu" : "Cấu trúc đã lưu"}</span></div></div><div className="flex flex-wrap gap-2"><button type="button" className="th-secondary-button" disabled={e.dirty || e.busy} onClick={() => setPicker(true)}>Đổi khung</button><button type="button" className="th-secondary-button" onClick={() => setHelp(true)}>? Giải thích quan hệ</button>{e.editable && <button type="button" className="th-secondary-button" onClick={() => document.getElementById("kg-save-actions")?.scrollIntoView({ block: "end", behavior: "smooth" })}>Đến lưu / duyệt</button>}{props.onExit && <button type="button" className="th-secondary-button" disabled={e.dirty || e.busy} onClick={props.onExit}>Thoát chế độ soạn</button>}</div></header>
    {(e.dirty || e.busy) && <p role="status" className="kg-hint rounded-none">{e.busy ? "Đang xử lý, vui lòng đợi…" : "Lưu hoặc hủy cấu trúc trước khi đổi môn, khối hoặc khung."}</p>}
    {!props.versionId ? <div className="th-surface p-4">{pickerContent}</div> : <>
      {e.versions.isError && <p role="alert" className="kg-issue" data-toast-mirror="off">Không tải được phiên bản. <button type="button" onClick={() => e.versions.refetch()}>Thử lại</button></p>}
      {!e.editable && <p className="kg-hint rounded-none">{e.selected?.isLegacy ? "Dữ liệu chuyển tiếp cần sao chép và rà soát." : "Bạn đang xem phiên bản này. Soạn cấu trúc cần bản nháp và quyền phù hợp."} {e.selected && e.hasPermission(permissions.nodesCreate) && e.hasPermission(permissions.edgesCreate) && <button type="button" className="th-secondary-button ml-2" disabled={e.busy} onClick={() => e.create.mutate({ title: "", source: e.selected!.graphVersionId })}>Sao chép thành bản nháp</button>}</p>}
      <div className="kg-editor-toolbar" role="toolbar" aria-label="Thao tác soạn khung tri thức" data-testid="kg-draft-toolbar">
        {e.editable && <><button type="button" className="th-secondary-button kg-open-library" onClick={() => setDrawer("library")}>Thư viện chủ đề</button>{props.onAddTopic && <button type="button" className="th-primary-button" disabled={e.busy} onClick={props.onAddTopic}>+ Tạo chủ đề mới</button>}<button type="button" className="th-secondary-button" disabled={disabled || !e.hasPermission(permissions.edgesCreate) || e.nodes.length < 2} onClick={() => startLink()}>Tạo liên kết</button></>}
        <button type="button" className="th-secondary-button kg-open-inspector" onClick={() => setDrawer("inspector")}>Chi tiết đang chọn</button><button type="button" className="th-secondary-button" aria-pressed={showParents} onClick={() => setShowParents(v => !v)}>{showParents ? "Ẩn" : "Hiện"} cấu trúc cha–con</button><span className="kg-muted ml-auto">{e.nodes.length} chủ đề · {e.edges.length} quan hệ trong khung</span>
      </div>
      {mode && <div className="kg-mode-banner" role="status"><strong>{mode.kind === "link" ? mode.source ? `Bước 2/2: Chọn đích cho ${name(mode.source)}` : "Bước 1/2: Chọn chủ đề nguồn" : `Chọn nhóm cha cho ${name(mode.child)}`}</strong><button type="button" className="th-secondary-button" onClick={() => setMode(null)}>Hủy thao tác · Esc</button></div>}
      <div className="kg-editor-grid" data-readonly={!e.editable || undefined}>
        {e.editable && screenWidth >= 1280 && <aside className="kg-sidebar kg-library" aria-label="Thư viện chủ đề">{library}</aside>}
        <div className="kg-main">
          {!e.editable && props.readLoading ? <p className="kg-empty" role="status">Đang tải đồ thị của phiên bản…</p> : !e.editable && props.readError ? <p role="alert" className="kg-issue" data-toast-mirror="off">Không tải được đồ thị của phiên bản. <button type="button" onClick={props.onRetryRead}>Thử lại</button></p> : e.editable && e.catalog.isPending ? <p className="kg-empty" role="status">Đang tải thư viện để dựng cấu trúc đang soạn…</p> : e.editable && e.catalog.isError ? <p role="alert" className="kg-issue" data-toast-mirror="off">Không tải được thư viện. <button type="button" onClick={() => e.catalog.refetch()}>Thử lại</button></p> : <KnowledgeGraphCanvas cacheScope={`${e.user?.centerId}:${e.user?.userId}:${props.subjectId}:${props.versionId}`} nodes={shownNodes} edges={shownEdges}
            selectedNodeId={selection?.kind === "node" ? selection.id : null} selectedEdgeId={selection?.kind === "edge" ? selection.id : null} onSelectNode={chooseNode} onSelectEdge={id => { if (!mode) select({ kind: "edge", id }); }} onClear={() => { if (!mode) setSelection(null); }}
            hideInactive={hideInactive} onHideInactive={setHideInactive} inactiveCount={preview.nodes.filter(n => !n.isActive).length} nodeColors={graphNodeColors} nodeLabels={graphNodeLabels} edgeColors={graphEdgeColors}
            issueNodeIds={issueNodes} issueEdgeIds={issueEdges} nodeMarkers={mode?.kind === "link" && mode.source ? new Map([[mode.source, "Nguồn"]]) : undefined} reveal={reveal} showParents={showParents} compactToolbar />}
          {!e.nodes.length && e.editable && <div className="kg-empty"><strong>Bắt đầu bằng một chủ đề</strong><p className="kg-muted">Thêm từ thư viện hoặc tạo chủ đề mới. Tạo liên kết sau khi có hai chủ đề.</p><button type="button" className="th-secondary-button" onClick={() => setDrawer("library")}>Mở thư viện chủ đề</button></div>}
          <details className="kg-editor-list" open={!!mode || undefined}><summary>Chủ đề và quan hệ trong khung · thao tác bằng danh sách</summary><input type="search" className="th-input w-full my-3" aria-label="Tìm trong cấu trúc đang soạn" value={listSearch} onChange={event => { setListSearch(event.target.value); setListLimit(40); }} placeholder="Tìm chủ đề hoặc quan hệ…" />
            {e.nodes.filter(n => `${name(n.nodeId)} ${topics.find(t => t.nodeId === n.nodeId)?.nodeCode ?? ""}`.toLocaleLowerCase("vi").includes(q)).slice(0, listLimit).map(n => <div className="kg-list-row" key={n.nodeId} data-error={issueNodes.has(n.nodeId) || undefined}><span>{issueNodes.has(n.nodeId) ? "⚠ " : ""}{name(n.nodeId)}<small className="block kg-muted">Nhóm cha: {n.parentNodeId ? name(n.parentNodeId) : "Cấp ngoài cùng"}</small></span><button type="button" className="th-secondary-button" disabled={mode?.kind === "parent" && mode.child === n.nodeId} onClick={() => mode ? chooseNode(n.nodeId) : select({ kind: "node", id: n.nodeId }, true)}>{mode ? "Chọn chủ đề này" : "Chi tiết"}</button></div>)}
            {e.edges.filter(m => { const r = catalogEdges.find(r => r.edgeId === m.edgeId); return `${r ? name(r.sourceNodeId) + name(r.targetNodeId) : m.edgeId} ${relationInfo[m.relationType].label}`.toLocaleLowerCase("vi").includes(q); }).slice(0, listLimit).map(m => { const r = catalogEdges.find(r => r.edgeId === m.edgeId); return <div className="kg-list-row" key={m.edgeId} data-error={issueEdges.has(m.edgeId) || undefined}><span>{issueEdges.has(m.edgeId) ? "⚠ " : ""}{r ? `${name(r.sourceNodeId)} → ${name(r.targetNodeId)}` : `Quan hệ #${m.edgeId}`}<small className="block kg-muted">{relationInfo[m.relationType].label} · Trọng số {m.weight}</small></span><button type="button" className="th-secondary-button" onClick={() => select({ kind: "edge", id: m.edgeId }, true)}>Chi tiết quan hệ</button></div>; })}
            {(e.nodes.length > listLimit || e.edges.length > listLimit) && <button type="button" className="th-secondary-button mt-2" onClick={() => setListLimit(n => n + 40)}>Xem thêm</button>}
          </details>
          {e.editable && <details className="kg-editor-list"><summary>Quan hệ có sẵn trong thư viện · thêm vào khung</summary>{availableEdges.slice(0, listLimit).map(r => <div className="kg-list-row" key={r.edgeId}><span>{name(r.sourceNodeId)} → {name(r.targetNodeId)}<small className="block kg-muted">{relationInfo[r.relationType].label}</small></span><button type="button" className="th-secondary-button" disabled={disabled} onClick={() => { e.addEdge(r); select({ kind: "edge", id: r.edgeId }, true); }}>Thêm quan hệ vào khung</button></div>)}{availableEdges.length > listLimit && <button type="button" className="th-secondary-button mt-2" onClick={() => setListLimit(n => n + 40)}>Xem thêm quan hệ</button>}</details>}
        </div>
        {screenWidth >= 1700 && <aside className="kg-sidebar kg-inspector" aria-label="Chi tiết đang chọn"><div ref={inspectorRef}>{inspector}</div></aside>}
      </div>
      {e.editable && <div className="kg-issues" data-testid="kg-draft-issues" data-has-errors={issues.length > 0 || e.stale} data-toast-mirror="off"><h3 className="font-semibold">{issues.length ? `⚠ Cần xử lý (${issues.length})` : "Kiểm tra cấu trúc"}</h3>
        {e.catalog.isFetching && <p role="status" className="kg-muted">Đang tải dữ liệu để kiểm tra; chưa kết luận các mục còn thiếu.</p>}{e.stale && <p role="alert" className="kg-issue">Khung đã thay đổi ở phiên khác. Hủy cấu trúc đang soạn rồi tải phiên bản mới; không tự ghi đè.</p>}
        {issues.length > 0 && <div role="alert">{issues.map(issue => <article className="kg-issue" key={issue.id}><p><strong>{issue.message}</strong></p><p>{issue.remedy}</p>{issue.fullPath && <details><summary>Toàn bộ vòng</summary><p>{issue.fullPath}</p></details>}<div className="kg-actions"><button type="button" className="th-secondary-button" onClick={() => locateIssue(issue)}>Đến vị trí lỗi</button>{issue.kind === "parent" && issue.nodeIds.slice(0, 6).map(id => <button type="button" className="th-secondary-button" key={id} onClick={() => locateIssue(issue, id)}>Đến {name(id)}</button>)}{issue.kind === "edge" && issue.nodeIds.filter(id => !ids.has(id) && topics.some(n => n.nodeId === id)).map(id => <button type="button" className="th-secondary-button" disabled={disabled} key={id} onClick={() => e.addNode(id)}>Thêm lại {name(id)}</button>)}</div></article>)}</div>}
        {!issues.length && !e.catalog.isFetching && <p className="kg-muted">{e.catalog.isSuccess ? "Chưa phát hiện lỗi từ dữ liệu đã tải. Máy chủ kiểm tra lại khi lưu." : "Chưa đủ dữ liệu để xác định lỗi cấu trúc."}</p>}
      </div>}
      {e.message && !review && <div className={e.message.error ? "kg-issue" : "kg-hint rounded-none"} role={e.message.error ? "alert" : "status"} data-toast-mirror="off"><p>{e.message.text}</p>{e.message.error && !issues.length && <p className="kg-muted">Chưa xác định được vị trí từ dữ liệu hiện có.</p>}{e.message.traceId && <details className="kg-muted"><summary>Chi tiết hỗ trợ</summary>Mã hỗ trợ: {e.message.traceId}</details>}</div>}
      {e.editable && <footer id="kg-save-actions" className="kg-actionbar"><span className="kg-save-status">{e.dirty ? "● Cấu trúc chưa lưu" : "✓ Cấu trúc đã lưu"}<small className="block kg-muted">{e.libraryWritten ? "Có nội dung thư viện đã được ghi riêng." : `${e.nodes.length} chủ đề · ${e.edges.length} quan hệ`}</small></span>{e.dirty && <button type="button" className="th-secondary-button" disabled={e.busy} onClick={() => setDiscarding(true)}>Hủy thay đổi cấu trúc</button>}<button type="button" className="th-primary-button" disabled={saveDisabled} onClick={() => e.save.mutate()}>{e.save.isPending ? "Đang lưu…" : "Lưu bản nháp"}</button><button type="button" className="th-secondary-button" disabled={e.busy || e.dirty || !e.nodes.length} onClick={() => setReview(true)}>Rà soát & duyệt</button>{e.dirty && saveDisabled && <p className="kg-muted w-full">{e.stale ? "Khung vừa thay đổi ở phiên khác." : invalidEdges.length ? "Xử lý các quan hệ thiếu đầu nối/khung nguồn phía trên trước khi lưu." : "Chờ dữ liệu tải xong hoặc thao tác đang chạy để lưu."}</p>}</footer>}
    </>}
    <TeacherModal isOpen={picker && !!props.versionId} title="Chọn khung tri thức" size="lg" onClose={() => setPicker(false)}>{pickerContent}</TeacherModal>
    <TeacherDrawer isOpen={drawer === "library"} title="Thư viện chủ đề" onClose={() => setDrawer(null)}><div className="kg-editor">{drawer === "library" && library}</div></TeacherDrawer>
    <TeacherDrawer isOpen={drawer === "inspector" && screenWidth < 1700} title="Chi tiết đang chọn" onClose={() => setDrawer(null)}><div ref={screenWidth < 1700 ? inspectorRef : undefined} className="kg-editor kg-mobile-inspector">{drawer === "inspector" && inspector}</div></TeacherDrawer>
    <RelationHelpDialog open={help} onClose={() => setHelp(false)} />
    {edgeForm && <DraftRelationDialog suspended={help} editor={e} {...edgeForm} subjectId={props.subjectId} versionId={props.versionId} name={name} onClose={() => setEdgeForm(null)} onSelect={id => select({ kind: "edge", id }, true)} onHelp={() => setHelp(true)} />}
    <TeacherConfirmDialog isOpen={!!removingNode} title="Gỡ chủ đề khỏi khung?" description={`Gỡ ${removingNode ? name(removingNode) : ""} khỏi cấu trúc đang soạn. Chủ đề vẫn còn trong thư viện.`} confirmLabel="Gỡ khỏi khung" onClose={() => setRemovingNode(null)} onConfirm={() => { if (removingNode) e.removeNode(removingNode); setRemovingNode(null); setSelection(null); }}><p className="kg-muted">{e.nodes.filter(n => n.parentNodeId === removingNode).length} chủ đề con sẽ về cấp ngoài cùng. {relations.filter(r => r.sourceNodeId === removingNode || r.targetNodeId === removingNode).length} quan hệ còn tham chiếu cần bạn xử lý trước khi lưu. Không tự xóa quan hệ.</p></TeacherConfirmDialog>
    <TeacherConfirmDialog isOpen={discarding} title="Hủy thay đổi cấu trúc chưa lưu?" description="Khôi phục chủ đề, nhóm cha và quan hệ trong khung về lần lưu gần nhất." confirmLabel="Hủy thay đổi cấu trúc" onClose={() => setDiscarding(false)} onConfirm={() => { e.discard(); setDiscarding(false); setMode(null); setSelection(null); setActiveIssue(null); }}><p className="kg-muted">Nội dung/chủ đề/quan hệ đã tạo hoặc sửa trong thư viện sẽ không bị hủy hay xóa bởi thao tác này.</p></TeacherConfirmDialog>
    <TeacherModal isOpen={review} title={`Rà soát khung Khối ${props.gradeLevel}`} onClose={() => !e.busy && setReview(false)}><div className="kg-hint">{e.nodes.length} chủ đề · {e.edges.length} quan hệ. Duyệt sẽ dùng bản này làm khung hiện hành của khối.</div><p className="kg-muted my-3">{issues.length ? `Có ${issues.length} vấn đề trong bảng Cần xử lý. Máy chủ kiểm tra lại khi duyệt.` : "Rà soát chủ đề, nhóm cha và chiều quan hệ trước khi duyệt."}</p><label className="flex gap-2 text-sm"><input type="checkbox" checked={e.reviewed} disabled={e.busy || e.dirty} onChange={event => e.setReviewed(event.target.checked)} />Tôi đã rà soát chủ đề, cây và quan hệ phù hợp Khối {props.gradeLevel}.</label>{e.message?.error && <p role="alert" className="kg-issue" data-toast-mirror="off">{e.message.text}</p>}<div className="kg-actions"><button type="button" className="th-primary-button" disabled={e.busy || e.dirty || !e.reviewed || !e.nodes.length} onClick={() => e.publish.mutate(undefined, { onSuccess: () => setReview(false) })}>Duyệt và dùng làm khung hiện hành</button></div></TeacherModal>
    {editingTopic && <KnowledgeNodeEditDialog node={editingTopic} subjectId={props.subjectId} subjectCode={props.subjectCode} subjectName={props.subjectName} gradeLevel={props.gradeLevel} onClose={() => setEditingTopic(null)} onSaved={async () => { setEditingTopic(null); e.setLibraryWritten(true); e.notify("Đã lưu nội dung chủ đề trong thư viện. Hủy cấu trúc không hủy thay đổi thư viện này."); await e.refresh(); }} />}
    <TeacherConfirmDialog isOpen={!!deletingTopic} tone="danger" title="Xóa chủ đề khỏi thư viện?" description={deletingTopic ? `Xóa mềm “${deletingTopic.nodeName}” khỏi thư viện môn và các bản nháp sử dụng nó. Máy chủ kiểm tra phụ thuộc trước khi xóa.` : ""} confirmLabel="Xóa khỏi thư viện" isConfirming={e.removeTopic.isPending} confirmDisabled={!!deleteBlockers && (deleteBlockers.relations.length > 0 || deleteBlockers.children.length > 0)} error={deleteError} onConfirm={() => deletingTopic && e.removeTopic.mutate(deletingTopic.nodeId, { onSuccess: () => { setDeletingTopic(null); setSelection(null); }, onError: err => setDeleteError(academicLifecycleError(err, "Chưa xóa được chủ đề. Kiểm tra kết nối và quyền truy cập rồi thử lại.")) })} onClose={() => !e.removeTopic.isPending && setDeletingTopic(null)}>
      {deleteBlockers && <div data-testid="topic-delete-blockers"><p className="kg-muted">{deleteBlockers.children.length} chủ đề con và {deleteBlockers.relations.length} quan hệ trong thư viện còn tham chiếu.</p>{deleteBlockers.relations.map(r => <p className="kg-muted" key={r.edgeId}>{name(r.sourceNodeId)} → {name(r.targetNodeId)} · {relationInfo[r.relationType].label}</p>)}<p className="kg-muted">Gỡ khỏi bản nháp chưa đủ để xóa khỏi thư viện. Không tự gỡ phụ thuộc để vượt khóa.</p></div>}
    </TeacherConfirmDialog>
  </section>;
}
