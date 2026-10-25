import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { knowledgeGraphApi } from "../../api/knowledgeGraphApi";
import { useAuthStore } from "../../stores/authStore";
import { knowledgeKeys } from "../../utils/knowledgeScope";
import type { KnowledgeGraphEdgeDto, KnowledgeVersionNodeMapping, KnowledgeVersionEdgeMapping, KnowledgeRelationType } from "../../types/knowledgeGraph";
import { extractProblemDetails } from "../../utils/problemDetails";
import { permissions } from "../../auth/permissions";
import { findReversePrerequisite, replaceDraftPrerequisite } from "../../utils/knowledgeDraftEdges";

interface Props { subjectId: string; gradeLevel?: number; versionId?: string; canEdit: boolean; onSelect: (id: string, grade: number) => void; onDirtyChange?: (dirty: boolean) => void }
const relationLabels: Record<KnowledgeRelationType, string> = { PrerequisiteOf: "Tiên quyết", RelatedTo: "Liên quan", PartOf: "Thuộc về", CausesErrorIn: "Gây lỗi" };

/** Explicit review of applicability. Existing usage is never treated as an approved grade automatically. */
export function KnowledgeVersionPanel({ subjectId, gradeLevel, versionId, canEdit, onSelect, onDirtyChange }: Props) {
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
  useEffect(() => {
    if (!selected || dirty) return;
    setNodes(selected.nodeMappings ?? []); setEdges(selected.edgeMappings ?? []);
    setBaseVersion(selected.rowVersion); setReviewed(false);
  }, [selected, dirty]);
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
  const busy = create.isPending || save.isPending || publish.isPending || reverse.isPending;
  useEffect(() => { onDirtyChange?.(dirty || busy); return () => onDirtyChange?.(false); }, [dirty, busy, onDirtyChange]);
  const catalogNodes = (catalog.data?.nodes ?? []).filter(node => !gradeLevel || !node.gradeLevels?.length ||
    node.gradeLevels.includes(gradeLevel) || nodes.some(mapping => mapping.nodeId === node.nodeId));
  const catalogEdges = catalog.data?.edges ?? [];
  const invalidEdges = edges.filter(mapping => { const edge = catalogEdges.find(e => e.edgeId === mapping.edgeId);
    return !edge || !nodes.some(n => n.nodeId === edge.targetNodeId) || (!nodes.some(n => n.nodeId === edge.sourceNodeId) && !mapping.sourceGraphVersionId) ||
      (!!mapping.sourceGraphVersionId && mapping.relationType !== "PrerequisiteOf"); });
  return <details className="th-surface rounded-xl p-4 my-4" open={!!versionId}>
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
        <p className="text-sm">Chọn những điểm tri thức thực sự áp dụng cho Khối {gradeLevel}. Một điểm có thể dùng nhiều khối; mỗi phiên bản có nút cha và trọng số riêng.</p>
        <fieldset disabled={busy || catalog.isPending || catalog.isError} className="space-y-2 max-h-80 overflow-y-auto">
          <legend className="font-semibold">Chủ đề và cây cấu trúc ({nodes.length})</legend>
          {catalogNodes.map(node => { const mapping = nodes.find(n => n.nodeId === node.nodeId); return <div key={node.nodeId} className="flex flex-wrap items-center gap-2 border-b border-[var(--th-border)] py-2">
            <label className="flex items-center gap-2 flex-1"><input type="checkbox" checked={!!mapping} onChange={e => { setDirty(true); setReviewed(false);
              setNodes(prev => e.target.checked ? [...prev, { nodeId: node.nodeId, parentNodeId: null }] : prev.filter(n => n.nodeId !== node.nodeId).map(n => n.parentNodeId === node.nodeId ? { ...n, parentNodeId: null } : n)); }} />{node.nodeName} <small>({node.nodeCode})</small></label>
            {mapping && <select aria-label={`Nút cha của ${node.nodeName}`} className="th-select" value={mapping.parentNodeId ?? ""} onChange={e => { setDirty(true); setReviewed(false); setNodes(prev => prev.map(n => n.nodeId === node.nodeId ? { ...n, parentNodeId: e.target.value || null } : n)); }}>
              <option value="">Không có nút cha</option>{catalogNodes.filter(n => n.nodeId !== node.nodeId && nodes.some(m => m.nodeId === n.nodeId)).map(n => <option key={n.nodeId} value={n.nodeId}>{n.nodeName}</option>)}
            </select>}
          </div>; })}
        </fieldset>
        <fieldset disabled={busy || catalog.isError} className="space-y-2 max-h-60 overflow-y-auto">
          <legend className="font-semibold">Quan hệ áp dụng ({edges.length})</legend>
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
        <div className="flex flex-wrap gap-2"><button className="th-primary-button" disabled={busy || !dirty || stale || catalog.isError || invalidEdges.length > 0} onClick={() => save.mutate()}>Lưu cấu trúc nháp</button>
          {dirty && <button className="th-secondary-button" disabled={busy} onClick={() => { setDirty(false); setReviewed(false); }}>Hủy lựa chọn chưa lưu</button>}</div>
        <label className="flex gap-2 text-sm"><input type="checkbox" checked={reviewed} disabled={dirty || busy} onChange={e => setReviewed(e.target.checked)} />Tôi đã rà soát chủ đề, cây và quan hệ phù hợp Khối {gradeLevel}.</label>
        <button className="th-primary-button" disabled={busy || dirty || !reviewed || !nodes.length} onClick={() => publish.mutate()}>Duyệt và dùng làm khung hiện hành</button>
      </>}
      {message && <p role="status" className="text-sm">{message}</p>}
    </div>}
  </details>;
}
