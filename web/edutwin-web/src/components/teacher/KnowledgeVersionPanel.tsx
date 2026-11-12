import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { knowledgeGraphApi } from "../../api/knowledgeGraphApi";
import { curriculumApi } from "../../api/curriculumApi";
import { useAuthStore } from "../../stores/authStore";
import { knowledgeKeys } from "../../utils/knowledgeScope";
import type { KnowledgeGraphEdgeDto, KnowledgeGraphNodeDto, KnowledgeVersionNodeMapping, KnowledgeVersionEdgeMapping, KnowledgeRelationType } from "../../types/knowledgeGraph";
import { extractProblemDetails } from "../../utils/problemDetails";
import { permissions } from "../../auth/permissions";
import { findReversePrerequisite, invalidDraftEdges, replaceDraftPrerequisite } from "../../utils/knowledgeDraftEdges";
import { filterDraftTopics, lockedTopicIds, type DraftTopicFilter } from "../../utils/knowledgeDraftTopics";
import { KnowledgeNodeEditDialog } from "./KnowledgeNodeEditDialog";
import { TeacherConfirmDialog } from "./TeacherOverlays";

/** A topic/relation just created elsewhere on the page, to tick in this draft (nonce makes repeats distinct). */
export type DraftIncludeRequest =
  | { kind: "node"; id: string; nonce: number }
  | { kind: "edge"; id: string; relationType: KnowledgeRelationType; weight: number; nonce: number };

interface Props {
  subjectId: string; gradeLevel?: number; versionId?: string; canEdit: boolean;
  onSelect: (id: string, grade: number) => void; onDirtyChange?: (dirty: boolean) => void;
  /** Used to derive standard codes when a topic is edited. */
  subjectCode?: string; subjectName?: string;
  /** Open the page's create dialogs right from the lists (no scrolling back to the header). */
  onAddTopic?: () => void; onAddRelation?: () => void;
  includeRequest?: DraftIncludeRequest | null;
}
const relationLabels: Record<KnowledgeRelationType, string> = { PrerequisiteOf: "Tiên quyết", RelatedTo: "Liên quan", PartOf: "Thuộc về", CausesErrorIn: "Gây lỗi" };
const rowId = (nodeId: string) => `kg-draft-topic-${nodeId}`;

/** Explicit review of applicability. Existing usage is never treated as an approved grade automatically. */
export function KnowledgeVersionPanel({ subjectId, gradeLevel, versionId, canEdit, onSelect, onDirtyChange, subjectCode, subjectName, onAddTopic, onAddRelation, includeRequest }: Props) {
  const user = useAuthStore(s => s.user);
  const hasPermission = useAuthStore(s => s.hasPermission);
  const canCreateVersion = hasPermission(permissions.nodesCreate) && hasPermission(permissions.edgesCreate);
  const client = useQueryClient();
  const [title, setTitle] = useState("");
  const [nodes, setNodes] = useState<KnowledgeVersionNodeMapping[]>([]);
  const [edges, setEdges] = useState<KnowledgeVersionEdgeMapping[]>([]);
  const [baseVersion, setBaseVersion] = useState("");
  const [dirty, setDirty] = useState(false);
  const [reviewed, setReviewed] = useState(false);
  const [message, setMessage] = useState("");
  const versions = useQuery({ queryKey: knowledgeKeys.versions(user?.centerId, user?.userId, subjectId, gradeLevel),
    queryFn: () => knowledgeGraphApi.listVersions(subjectId, gradeLevel), enabled: !!subjectId && !!gradeLevel });
  const selected = versions.data?.find(v => v.graphVersionId === versionId);
  const sourceVersions = useQuery({ queryKey: knowledgeKeys.versions(user?.centerId, user?.userId, subjectId),
    queryFn: () => knowledgeGraphApi.listVersions(subjectId), enabled: !!subjectId && !!gradeLevel && canEdit });
  const catalog = useQuery({ queryKey: knowledgeKeys.graph(user?.centerId, user?.userId, subjectId, {}),
    queryFn: () => knowledgeGraphApi.getGraph(subjectId), enabled: !!subjectId && !!gradeLevel && canEdit });
  const curricula = useQuery({ queryKey: ["teacherCurriculums", "version-panel", user?.centerId, user?.userId, subjectId],
    queryFn: () => curriculumApi.getAll({ subjectId }), select: response => response.data, enabled: !!subjectId && !!gradeLevel && canEdit });
  const [topicFilter, setTopicFilter] = useState<DraftTopicFilter>("grade");
  const [topicSearch, setTopicSearch] = useState("");
  const [editingTopic, setEditingTopic] = useState<KnowledgeGraphNodeDto | null>(null);
  const [deletingTopic, setDeletingTopic] = useState<KnowledgeGraphNodeDto | null>(null);
  const [flashId, setFlashId] = useState<string | null>(null);
  // The panel remounts per version; a request made for an earlier version must never apply here.
  const [mountedAt] = useState(() => Date.now());
  useEffect(() => {
    if (!selected || dirty) return;
    setNodes(selected.nodeMappings ?? []); setEdges(selected.edgeMappings ?? []);
    setBaseVersion(selected.rowVersion); setReviewed(false);
  }, [selected, dirty]);
  // Tick what was just created on the page and bring it into view, instead of making the teacher find it.
  useEffect(() => {
    if (!includeRequest || includeRequest.nonce < mountedAt) return;
    if (includeRequest.kind === "node") {
      setNodes(prev => prev.some(n => n.nodeId === includeRequest.id) ? prev : [...prev, { nodeId: includeRequest.id, parentNodeId: null }]);
      setTopicSearch(""); setTopicFilter(current => current === "unselected" ? "grade" : current);
      setFlashId(includeRequest.id);
    } else {
      setEdges(prev => prev.some(e => e.edgeId === includeRequest.id) ? prev
        : [...prev, { edgeId: includeRequest.id, relationType: includeRequest.relationType, weight: includeRequest.weight }]);
    }
    setDirty(true); setReviewed(false);
    setMessage(includeRequest.kind === "node"
      ? "Đã tích chọn chủ đề vừa tạo vào bản nháp. Chọn nút cha nếu cần rồi bấm “Lưu cấu trúc nháp”."
      : "Đã tích chọn liên kết vừa tạo vào bản nháp. Bấm “Lưu cấu trúc nháp” để ghi.");
  }, [includeRequest, mountedAt]);
  useEffect(() => {
    if (!flashId) return;
    const frame = requestAnimationFrame(() => document.getElementById(rowId(flashId))?.scrollIntoView({ block: "nearest", behavior: "smooth" }));
    const timer = setTimeout(() => setFlashId(null), 2500);
    return () => { cancelAnimationFrame(frame); clearTimeout(timer); };
  }, [flashId, catalog.data]);
  const editable = canEdit && selected?.status === "Draft" && !selected.isLegacy;
  const stale = dirty && !!selected && baseVersion !== selected.rowVersion;
  const refresh = async () => { await client.invalidateQueries({ queryKey: knowledgeKeys.all }); };
  const error = (err: unknown) => setMessage(extractProblemDetails(err).detail || (err as Error).message);
  const create = useMutation({ mutationFn: (source?: string) => knowledgeGraphApi.createVersion({ subjectId,
    gradeLevel: gradeLevel!, title: title.trim() || undefined, sourceGraphVersionId: source }),
    onSuccess: async version => { setDirty(false); setMessage("Đã tạo bản nháp. Rà soát chủ đề, nút cha và quan hệ trước khi duyệt.");
      await refresh(); onSelect(version.graphVersionId, version.gradeLevel); }, onError: error });
  const save = useMutation({ mutationFn: () => knowledgeGraphApi.saveVersion(versionId!, { rowVersion: baseVersion, nodeMappings: nodes, edgeMappings: edges }),
    onSuccess: async version => { setBaseVersion(version.rowVersion); setDirty(false); setReviewed(false); setMessage("Đã lưu cấu trúc nháp."); await refresh(); }, onError: error });
  const publish = useMutation({ mutationFn: () => knowledgeGraphApi.publishVersion(versionId!, baseVersion),
    onSuccess: async () => { setMessage("Đã duyệt phiên bản khung tri thức cho khối đã chọn."); await refresh(); }, onError: error });
  const reverse = useMutation({ mutationFn: async (edge: KnowledgeGraphEdgeDto) => {
    const existing = findReversePrerequisite(catalog.data?.edges ?? [], edge);
    if (existing) return existing.edgeId;
    try {
      const created = await knowledgeGraphApi.createEdge({ graphVersionId: versionId, subjectId,
        sourceNodeId: edge.targetNodeId, targetNodeId: edge.sourceNodeId,
        relationType: "PrerequisiteOf", weight: edge.weight });
      return created.edgeId;
    } catch (err) {
      // Another editor may have created the reverse edge after the catalogue loaded.
      if (extractProblemDetails(err).errorCode === "DUPLICATE_RESOURCE") {
        const latest = await knowledgeGraphApi.getGraph(subjectId);
        const found = findReversePrerequisite(latest.edges, edge);
        if (found) return found.edgeId;
      }
      throw err;
    }
  }, onSuccess: async (reverseId, edge) => {
    setEdges(previous => replaceDraftPrerequisite(previous, edge.edgeId, reverseId,
      previous.find(mapping => mapping.edgeId === edge.edgeId)?.weight ?? edge.weight));
    setDirty(true); setReviewed(false);
    setMessage("Đã đảo chiều trong bản nháp. Bấm “Lưu cấu trúc nháp” để ghi thay đổi; liên kết cũ trong thư viện và các bản đã duyệt không đổi.");
    await refresh();
  }, onError: error });
  // Deleting a library topic: the server refuses topics used by approved frameworks, curricula,
  // questions, relations… and removes the topic from Draft frameworks it was ticked in.
  const removeTopic = useMutation({ mutationFn: (topic: KnowledgeGraphNodeDto) => knowledgeGraphApi.deleteNode(topic.nodeId),
    onSuccess: async (_, topic) => { setDeletingTopic(null); setMessage(`Đã xóa chủ đề “${topic.nodeName}” khỏi thư viện môn và các bản nháp.`); await refresh(); },
    onError: err => { setDeletingTopic(null); error(err); } });
  const busy = create.isPending || save.isPending || publish.isPending || reverse.isPending || removeTopic.isPending;
  useEffect(() => { onDirtyChange?.(dirty || busy); return () => onDirtyChange?.(false); }, [dirty, busy, onDirtyChange]);
  // Validate only against a loaded library; while it loads (or failed) validity is unknown, not invalid.
  const catalogReady = catalog.isSuccess;
  const allTopics = catalog.data?.nodes ?? [];
  const selectedIds = new Set(nodes.map(n => n.nodeId));
  const catalogNodes = filterDraftTopics({ topics: allTopics, gradeLevel, selectedIds, lockedIds: new Set(), filter: "grade" });
  const lockedIds = lockedTopicIds(sourceVersions.data, curricula.data);
  const visibleTopics = filterDraftTopics({ topics: allTopics, gradeLevel, selectedIds, lockedIds, versions: sourceVersions.data,
    curricula: curricula.data, filter: topicFilter, search: topicSearch });
  const knownTopicIds = new Set(allTopics.map(n => n.nodeId));
  const orphanSelections = catalogReady ? nodes.filter(n => !knownTopicIds.has(n.nodeId)) : [];
  const canEditTopics = hasPermission(permissions.nodesUpdate);
  const canDeleteTopics = hasPermission(permissions.nodesDelete);
  const catalogEdges = catalog.data?.edges ?? [];
  const invalidEdges = invalidDraftEdges(edges, nodes, catalogReady ? catalog.data.edges : null, catalog.isFetching) ?? [];
  return <details id="kg-version-panel" className="th-surface rounded-xl p-4 my-4 scroll-mt-24" open={!!versionId}>
    <summary className="font-semibold cursor-pointer">Phiên bản khung tri thức {gradeLevel ? `· Khối ${gradeLevel}` : "· Chọn khối để quản lý"}</summary>
    {gradeLevel && <div className="space-y-3 mt-3">
      <p className="text-sm">Mỗi khối có cấu trúc được duyệt riêng. Giáo trình đã xuất bản giữ phiên bản đã chọn; thay cấu trúc bằng bản nháp mới.</p>
      {versions.isError && <p role="alert">Không tải được phiên bản. <button onClick={() => versions.refetch()}>Tải lại</button></p>}
      <label className="block text-sm">Phiên bản
        <select className="th-select block mt-1" value={versionId ?? ""} disabled={dirty || busy} onChange={e => onSelect(e.target.value, gradeLevel)}>
          <option value="">Phiên bản hiện hành đã duyệt</option>
          {versions.data?.map(v => <option key={v.graphVersionId} value={v.graphVersionId}>{v.title || `Khối ${v.gradeLevel}`} · {v.isLegacy ? "Dữ liệu chuyển tiếp — cần rà soát" : v.status === "Draft" ? "Bản nháp" : v.status === "Published" ? "Đã duyệt" : "Lưu trữ"}{v.isCurrent ? " · Hiện hành" : ""}</option>)}
        </select>
      </label>
      {canCreateVersion && <div className="flex flex-wrap gap-2 items-end">
        <label className="text-sm">Tên bản nháp<input className="th-input block mt-1" maxLength={200} value={title} onChange={e => setTitle(e.target.value)} placeholder={`Khung tri thức Khối ${gradeLevel}`} /></label>
        <button className="th-secondary-button" disabled={busy || dirty} onClick={() => create.mutate(undefined)}>Tạo khung trống</button>
        <button className="th-secondary-button" disabled={busy || dirty || !selected} onClick={() => selected && create.mutate(selected.graphVersionId)}>Sao chép thành bản nháp</button>
      </div>}
      {selected?.isLegacy && <p className="text-sm text-amber-500">Phiên bản chuyển tiếp giữ nguyên dữ liệu cũ. Sao chép thành bản nháp để xác nhận phạm vi khối trước khi dùng cho giáo trình mới.</p>}
      {editable && <>
        {catalog.isError && <p role="alert">Không tải được thư viện gốc. <button onClick={() => catalog.refetch()}>Tải lại</button></p>}
        {catalog.isPending && <p role="status" className="text-sm text-[var(--th-text-muted)]">Đang tải thư viện gốc để kiểm tra chủ đề và quan hệ…</p>}
        <p className="text-sm">Chọn những điểm tri thức thực sự áp dụng cho Khối {gradeLevel}. Một điểm có thể dùng nhiều khối; mỗi phiên bản có nút cha và trọng số riêng.</p>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h4 className="font-semibold m-0" id="kg-draft-topics-heading">Chủ đề và cây cấu trúc ({nodes.length} đang chọn)</h4>
          {onAddTopic && <button type="button" className="th-primary-button" disabled={busy} onClick={onAddTopic}>+ Thêm chủ đề mới</button>}
        </div>
        <div className="flex flex-wrap items-end gap-2 text-sm">
          <label className="flex flex-col gap-1">Hiển thị
            <select className="th-select" aria-label="Lọc chủ đề theo khung tri thức hoặc giáo trình" value={topicFilter} onChange={e => setTopicFilter(e.target.value as DraftTopicFilter)}>
              <option value="grade">Phù hợp Khối {gradeLevel}</option>
              <option value="selected">Đang chọn trong bản nháp này</option>
              <option value="unselected">Chưa chọn</option>
              <option value="draftOnly">Chủ đề nháp (chưa thuộc khung đã duyệt/giáo trình đã xuất bản)</option>
              <option value="all">Tất cả chủ đề của môn</option>
              {!!sourceVersions.data?.some(v => v.graphVersionId !== versionId) && <optgroup label="Theo khung tri thức">
                {sourceVersions.data.filter(v => v.graphVersionId !== versionId).map(v => <option key={v.graphVersionId} value={`version:${v.graphVersionId}`}>
                  {v.title || "Khung"} · Khối {v.gradeLevel} · {v.status === "Draft" ? "Bản nháp" : v.status === "Published" ? "Đã duyệt" : "Lưu trữ"}</option>)}
              </optgroup>}
              {!!curricula.data?.length && <optgroup label="Theo giáo trình">
                {curricula.data.map(c => <option key={c.curriculumId} value={`curriculum:${c.curriculumId}`}>{c.title}{c.gradeLevel ? ` · Khối ${c.gradeLevel}` : ""}</option>)}
              </optgroup>}
            </select>
          </label>
          <label className="flex flex-col gap-1 flex-1 min-w-[12rem]">Tìm chủ đề
            <input className="th-input" type="search" value={topicSearch} onChange={e => setTopicSearch(e.target.value)} placeholder="Tên hoặc mã chủ đề" />
          </label>
          <span className="text-xs text-[var(--th-text-muted)]" role="status">Hiển thị {visibleTopics.length}/{allTopics.length} chủ đề</span>
        </div>
        {!!orphanSelections.length && <div className="rounded-lg border border-[var(--th-danger)] p-2 text-sm">
          <p className="m-0 font-semibold">{orphanSelections.length} chủ đề đang chọn không còn trong thư viện môn (đã bị xóa). Bỏ chọn để lưu được bản nháp.</p>
          {orphanSelections.map(o => <button key={o.nodeId} type="button" className="th-secondary-button mt-2 mr-2" onClick={() => { setDirty(true); setReviewed(false);
            setNodes(prev => prev.filter(n => n.nodeId !== o.nodeId).map(n => n.parentNodeId === o.nodeId ? { ...n, parentNodeId: null } : n)); }}>Bỏ chọn #{o.nodeId}</button>)}
        </div>}
        <fieldset disabled={busy || catalog.isPending || catalog.isError} aria-labelledby="kg-draft-topics-heading" className="space-y-2 max-h-96 overflow-y-auto">
          {catalogReady && !visibleTopics.length && <p className="text-sm text-[var(--th-text-muted)]">Không có chủ đề phù hợp bộ lọc.</p>}
          {visibleTopics.map(node => { const mapping = nodes.find(n => n.nodeId === node.nodeId); const draftOnly = !lockedIds.has(node.nodeId);
            return <div key={node.nodeId} id={rowId(node.nodeId)} className="flex flex-wrap items-center gap-2 border-b border-[var(--th-border)] py-2"
              style={flashId === node.nodeId ? { background: "rgba(20, 184, 166, 0.14)", borderRadius: 8 } : undefined}>
            <label className="flex items-center gap-2 flex-1 min-w-[12rem]"><input type="checkbox" checked={!!mapping} onChange={e => { setDirty(true); setReviewed(false);
              setNodes(prev => e.target.checked ? [...prev, { nodeId: node.nodeId, parentNodeId: null }] : prev.filter(n => n.nodeId !== node.nodeId).map(n => n.parentNodeId === node.nodeId ? { ...n, parentNodeId: null } : n)); }} />
              <span>{node.nodeName} <small>({node.nodeCode})</small>{!node.isActive && <small> · Đã tắt</small>}{draftOnly && <small className="text-[var(--th-text-muted)]"> · Nháp</small>}</span></label>
            {mapping && <select aria-label={`Nút cha của ${node.nodeName}`} className="th-select" value={mapping.parentNodeId ?? ""} onChange={e => { setDirty(true); setReviewed(false); setNodes(prev => prev.map(n => n.nodeId === node.nodeId ? { ...n, parentNodeId: e.target.value || null } : n)); }}>
              <option value="">Không có nút cha</option>{allTopics.filter(n => n.nodeId !== node.nodeId && selectedIds.has(n.nodeId)).map(n => <option key={n.nodeId} value={n.nodeId}>{n.nodeName}</option>)}
            </select>}
            {canEditTopics && <button type="button" className="th-secondary-button" aria-label={`Sửa chủ đề ${node.nodeName}`} onClick={() => setEditingTopic(node)}>Sửa</button>}
            {canDeleteTopics && draftOnly && <button type="button" className="th-secondary-button" aria-label={`Xóa chủ đề ${node.nodeName}`}
              disabled={dirty} title={dirty ? "Lưu hoặc hủy thay đổi của bản nháp trước khi xóa chủ đề." : "Xóa chủ đề nháp khỏi thư viện môn"}
              onClick={() => setDeletingTopic(node)}>Xóa</button>}
          </div>; })}
        </fieldset>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h4 className="font-semibold m-0" id="kg-draft-relations-heading">Quan hệ áp dụng ({edges.length})</h4>
          {onAddRelation && <button type="button" className="th-secondary-button" disabled={busy || nodes.length < 2} title={nodes.length < 2 ? "Chọn ít nhất hai chủ đề trong bản nháp trước." : undefined} onClick={onAddRelation}>+ Thêm liên kết</button>}
        </div>
        <fieldset disabled={busy || catalog.isError} aria-labelledby="kg-draft-relations-heading" className="space-y-2 max-h-60 overflow-y-auto">
          <p className="text-sm">Chỉ các quan hệ được tích chọn mới thuộc bản nháp. Bỏ chọn rồi lưu để gỡ khỏi khung này; danh mục và các bản đã duyệt không đổi.</p>
          {catalogEdges.filter(edge => edges.some(mapping => mapping.edgeId === edge.edgeId) ||
            nodes.some(n => n.nodeId === edge.sourceNodeId) && nodes.some(n => n.nodeId === edge.targetNodeId))
            .sort((left, right) => Number(edges.some(mapping => mapping.edgeId === right.edgeId)) - Number(edges.some(mapping => mapping.edgeId === left.edgeId)))
            .map(edge => { const mapping = edges.find(e => e.edgeId === edge.edgeId); const sourceInside = nodes.some(n => n.nodeId === edge.sourceNodeId);
            const targetInside = nodes.some(n => n.nodeId === edge.targetNodeId); return <div key={edge.edgeId} className="flex flex-wrap items-center gap-2 py-1">
              <label className="flex items-center gap-2 flex-1"><input type="checkbox" disabled={!targetInside && !mapping} checked={!!mapping} onChange={e => { setDirty(true); setReviewed(false);
                setEdges(prev => e.target.checked ? [...prev, { edgeId: edge.edgeId, relationType: edge.relationType, weight: edge.weight }] : prev.filter(m => m.edgeId !== edge.edgeId)); }} />{catalogNodes.find(n => n.nodeId === edge.sourceNodeId)?.nodeName} → {catalogNodes.find(n => n.nodeId === edge.targetNodeId)?.nodeName} · {edge.relationType}</label>
              <span className="text-xs">{mapping ? "Đang áp dụng" : "Chưa áp dụng"}</span>
              {mapping && <button type="button" className="th-secondary-button" onClick={() => { setDirty(true); setReviewed(false);
                setEdges(previous => previous.filter(item => item.edgeId !== edge.edgeId)); }}>Bỏ khỏi bản nháp</button>}
              {hasPermission(permissions.edgesCreate) && sourceInside && targetInside && (mapping?.relationType ?? edge.relationType) === "PrerequisiteOf" &&
                <button type="button" className="th-secondary-button" onClick={() => reverse.mutate(edge)}>Đảo chiều trong bản nháp</button>}
              {mapping && <><select className="th-select" aria-label="Loại quan hệ trong phiên bản" value={mapping.relationType} onChange={e => { setDirty(true); setReviewed(false); setEdges(prev => prev.map(m => m.edgeId === edge.edgeId ? { ...m, relationType: e.target.value as KnowledgeRelationType, sourceGraphVersionId: e.target.value === "PrerequisiteOf" ? m.sourceGraphVersionId : null } : m)); }}>{Object.entries(relationLabels).map(([type, label]) => <option key={type} value={type}>{label}</option>)}</select>
                <input className="th-input w-24" aria-label="Trọng số quan hệ" type="number" min={0} max={1} step={0.05} value={mapping.weight} onChange={e => { setDirty(true); setReviewed(false); setEdges(prev => prev.map(m => m.edgeId === edge.edgeId ? { ...m, weight: Number(e.target.value) } : m)); }} />
                {mapping.relationType === "PrerequisiteOf" && <label className="text-sm">Khung nguồn tiên quyết<select className="th-select ml-2" value={mapping.sourceGraphVersionId ?? ""} onChange={e => { setDirty(true); setReviewed(false); setEdges(prev => prev.map(m => m.edgeId === edge.edgeId ? { ...m, sourceGraphVersionId: e.target.value || null } : m)); }}><option value="">{sourceInside ? "Trong khung hiện tại" : "Chọn khung nguồn đã duyệt"}</option>{sourceVersions.data?.filter(v => v.status === "Published" && !v.isLegacy && v.gradeLevel < gradeLevel && v.graphVersionId !== versionId && v.nodeMappings?.some(n => n.nodeId === edge.sourceNodeId)).map(v => <option key={v.graphVersionId} value={v.graphVersionId}>{v.title} · Khối {v.gradeLevel}</option>)}</select></label>}</>}
            </div>; })}
        </fieldset>
        {stale && <p role="alert">Phiên bản đã được thay đổi ở nơi khác. Hủy lựa chọn và tải lại trước khi lưu.</p>}
        {!!invalidEdges.length && <p role="alert">Có {invalidEdges.length} quan hệ thiếu điểm đích trong khung hoặc thiếu khung nguồn tiên quyết. Bổ sung điểm tri thức hoặc bỏ chọn quan hệ trước khi lưu.</p>}
        {/* Stays at the bottom of the screen while there are unsaved changes, so saving never needs a scroll. */}
        <div className={`flex flex-wrap items-center gap-2 ${dirty ? "sticky bottom-0 z-10 rounded-lg border border-[var(--th-border)] bg-[var(--th-surface-raised)] p-2 shadow-lg" : ""}`}>
          <button className="th-primary-button" disabled={busy || !dirty || stale || !catalogReady || catalog.isFetching || invalidEdges.length > 0} onClick={() => save.mutate()}>Lưu cấu trúc nháp</button>
          {dirty && <button className="th-secondary-button" disabled={busy} onClick={() => { setDirty(false); setReviewed(false); }}>Hủy lựa chọn chưa lưu</button>}
          {dirty && <span className="text-xs text-[var(--th-text-muted)]">Có thay đổi chưa lưu · {nodes.length} chủ đề, {edges.length} quan hệ</span>}
        </div>
        <label className="flex gap-2 text-sm"><input type="checkbox" checked={reviewed} disabled={dirty || busy} onChange={e => setReviewed(e.target.checked)} />Tôi đã rà soát chủ đề, cây và quan hệ phù hợp Khối {gradeLevel}.</label>
        <button className="th-primary-button" disabled={busy || dirty || !reviewed || !nodes.length} onClick={() => publish.mutate()}>Duyệt và dùng làm khung hiện hành</button>
      </>}
      {message && <p role="status" className="text-sm">{message}</p>}
    </div>}
    {editingTopic && <KnowledgeNodeEditDialog node={editingTopic} subjectId={subjectId} subjectCode={subjectCode} subjectName={subjectName} gradeLevel={gradeLevel}
      onClose={() => setEditingTopic(null)}
      onSaved={async saved => { setEditingTopic(null); setMessage(`Đã cập nhật chủ đề (mã ${saved.nodeCode}).`); setFlashId(saved.nodeId); await refresh(); }} />}
    <TeacherConfirmDialog isOpen={!!deletingTopic} tone="danger" title="Xóa chủ đề nháp?"
      description={deletingTopic ? `Xóa “${deletingTopic.nodeName}” (${deletingTopic.nodeCode}) khỏi thư viện môn và khỏi các bản nháp đang chọn nó. Không xóa được nếu chủ đề còn liên kết, nút con, câu hỏi, giáo trình hoặc dữ liệu học tập — hệ thống sẽ nêu lý do.` : ""}
      confirmLabel="Xóa chủ đề" isConfirming={removeTopic.isPending}
      onConfirm={() => deletingTopic && removeTopic.mutate(deletingTopic)} onClose={() => !removeTopic.isPending && setDeletingTopic(null)} />
  </details>;
}
