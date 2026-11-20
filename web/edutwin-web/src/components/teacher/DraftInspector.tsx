import { frameworkTitle } from "../../utils/knowledgeFrameworkTitle";
import { useState, type ReactNode } from "react";
import type { KnowledgeGraphNodeDto, KnowledgeGraphEdgeDto, KnowledgeGraphVersion } from "../../types/knowledgeGraph";
import type { useKnowledgeDraftEditor } from "./useKnowledgeDraftEditor";
import { permissions } from "../../auth/permissions";
import { graphNodeLabels, relationInfo, relationTypes } from "../../utils/knowledgeRelations";
import type { DraftIssue } from "../../utils/knowledgeDraftEditor";

type Editor = ReturnType<typeof useKnowledgeDraftEditor>;
export type DraftSelection = { kind: "node" | "edge"; id: string } | null;
interface Props {
  editor: Editor; selection: DraftSelection; topics: KnowledgeGraphNodeDto[]; relations: KnowledgeGraphEdgeDto[];
  versions: KnowledgeGraphVersion[]; grade: number; versionId?: string; issues: DraftIssue[]; activeIssue: DraftIssue | null;
  lockedIds: ReadonlySet<string>; parentField: boolean; topicActions?: (id: string) => ReactNode;
  onSelect: (selection: NonNullable<DraftSelection>) => void; onLink: (source: string) => void; onParent: (child: string) => void;
  onEditTopic: (node: KnowledgeGraphNodeDto) => void; onDeleteTopic: (node: KnowledgeGraphNodeDto) => void; onRemoveNode: (id: string) => void;
  onClear: () => void;
}
export function DraftInspector(p: Props) {
  const [search, setSearch] = useState("");
  const e = p.editor;
  const name = (id: string) => p.topics.find(n => n.nodeId === id)?.nodeName ?? `#${id}`;
  const node = p.selection?.kind === "node" ? p.topics.find(n => n.nodeId === p.selection?.id) : undefined;
  const mapping = node && e.nodes.find(n => n.nodeId === node.nodeId);
  const edge = p.selection?.kind === "edge" ? p.relations.find(n => n.edgeId === p.selection?.id) : undefined;
  const disabled = !e.editable || e.busy || !e.catalog.isSuccess;
  return <div className="kg-inspector-content"><span className="kg-eyebrow">CHI TIẾT TRONG KHUNG · KHỐI {p.grade}</span>
    {node ? <>
      <h3 data-detail-heading tabIndex={-1} className="mt-2 font-semibold">{node.nodeName}</h3><code>{node.nodeCode}</code>
      <div className="kg-badges my-3"><span>{graphNodeLabels[node.nodeType]}</span><span>{mapping ? "Trong khung" : "Ngoài khung"}</span>{e.dirty && <span>Cấu trúc chưa lưu</span>}</div>
      {node.description && <p className="kg-muted">{node.description}</p>}
      <section className="mt-4"><strong>Nhóm cha trong khung</strong><p className="kg-muted">{mapping?.parentNodeId ? name(mapping.parentNodeId) : "Cấp ngoài cùng"}</p><p className="kg-muted">Tổ chức nội dung; không yêu cầu học nhóm cha trước.</p>
        {e.editable && mapping && <><button data-parent-action type="button" className="th-secondary-button mt-2" disabled={disabled} aria-invalid={p.issues.some(i => i.kind === "parent" && i.nodeIds.includes(node.nodeId)) || undefined} onClick={() => p.onParent(node.nodeId)}>Đặt nhóm cha trên đồ thị</button>
          <details className="mt-3" open={p.parentField || undefined}><summary className="kg-muted cursor-pointer">Chọn nhóm cha bằng danh sách</summary><input className="th-input w-full mt-2" aria-label="Tìm nhóm cha" value={search} onChange={event => setSearch(event.target.value)} placeholder="Tên hoặc mã nhóm cha" />
            <div className="max-h-48 overflow-y-auto"><button type="button" className="th-secondary-button my-2" disabled={disabled} onClick={() => e.setParent(node.nodeId, null)}>Để ở cấp ngoài cùng</button>{e.nodes.filter(n => n.nodeId !== node.nodeId && `${name(n.nodeId)} ${p.topics.find(t => t.nodeId === n.nodeId)?.nodeCode ?? ""}`.toLocaleLowerCase("vi").includes(search.toLocaleLowerCase("vi"))).map(n => <button type="button" className="kg-hint block w-full text-left mt-1" key={n.nodeId} disabled={disabled} onClick={() => e.setParent(node.nodeId, n.nodeId)}>Đặt trong {name(n.nodeId)}</button>)}</div>
          </details></>}
      </section>
      {e.editable && mapping && <div className="kg-actions"><button type="button" className="th-primary-button" disabled={disabled || !e.hasPermission(permissions.edgesCreate) || e.nodes.length < 2} onClick={() => p.onLink(node.nodeId)}>Tạo liên kết từ đây</button><button type="button" className="th-secondary-button" disabled={disabled} onClick={() => p.onRemoveNode(node.nodeId)}>Gỡ chủ đề khỏi khung</button></div>}
      <section><strong>Các quan hệ của chủ đề</strong>{p.relations.filter(r => r.sourceNodeId === node.nodeId || r.targetNodeId === node.nodeId).map(r => <button type="button" className="kg-hint w-full text-left mt-2" key={r.edgeId} onClick={() => p.onSelect({ kind: "edge", id: r.edgeId })}>{relationInfo[r.relationType][r.sourceNodeId === node.nodeId ? "outgoing" : "incoming"]} <strong>{name(r.sourceNodeId === node.nodeId ? r.targetNodeId : r.sourceNodeId)}</strong></button>)}</section>
      {mapping && !e.dirty && p.topicActions && <section className="kg-actions">{p.topicActions(node.nodeId)}</section>}
      {e.editable && <section><strong>Nội dung trong thư viện môn</strong><p className="kg-muted">Sửa được ghi riêng vào thư viện. Hủy cấu trúc không hủy nội dung đã lưu.</p><div className="kg-actions">{e.hasPermission(permissions.nodesUpdate) && <button type="button" className="th-secondary-button" disabled={e.busy} onClick={() => p.onEditTopic(node)}>Sửa nội dung chủ đề</button>}{e.hasPermission(permissions.nodesDelete) && !p.lockedIds.has(node.nodeId) && <button type="button" className="th-danger-button" disabled={e.busy || e.dirty} onClick={() => p.onDeleteTopic(node)}>Xóa khỏi thư viện</button>}</div>{e.dirty && <p className="kg-muted">Lưu hoặc hủy cấu trúc trước khi xóa khỏi thư viện.</p>}</section>}
    </> : edge ? <>
      <h3 data-detail-heading tabIndex={-1} className="mt-2 font-semibold">{relationInfo[edge.relationType].label}</h3><p className="kg-hint my-3"><strong>{name(edge.sourceNodeId)}</strong> → <strong>{name(edge.targetNodeId)}</strong></p><p className="kg-muted">{relationInfo[edge.relationType].intent}. {relationInfo[edge.relationType].effect}</p>
      {e.editable ? <><label className="kg-label">Loại quan hệ trong bản nháp</label><div className="kg-choice-grid">{relationTypes.map(type => <button type="button" className="kg-choice" key={type} aria-pressed={edge.relationType === type} disabled={disabled} onClick={() => e.updateEdge(edge.edgeId, { relationType: type, sourceGraphVersionId: type === "PrerequisiteOf" ? edge.sourceGraphVersionId : null })}>{relationInfo[type].label}</button>)}</div>
        <label className="kg-label" htmlFor="kg-edge-weight">Trọng số quan hệ (0–1)</label><input id="kg-edge-weight" className="th-input w-full" type="number" min="0" max="1" step="0.05" disabled={disabled} value={Number.isNaN(edge.weight) ? "" : edge.weight} aria-invalid={!Number.isFinite(edge.weight) || edge.weight < 0 || edge.weight > 1 || undefined} onChange={event => e.updateEdge(edge.edgeId, { weight: event.target.value === "" ? NaN : Number(event.target.value) })} />
        {(!Number.isFinite(edge.weight) || edge.weight < 0 || edge.weight > 1) && <p className="kg-field-error">Nhập trọng số từ 0 đến 1.</p>}
        {edge.relationType === "PrerequisiteOf" && <><label htmlFor="kg-prerequisite-source" className="kg-label">Khung nguồn tiên quyết</label><select id="kg-prerequisite-source" className="th-select w-full" value={edge.sourceGraphVersionId ?? ""} disabled={disabled} onChange={event => e.updateEdge(edge.edgeId, { sourceGraphVersionId: event.target.value || null })}><option value="">{e.nodes.some(n => n.nodeId === edge.sourceNodeId) ? "Trong khung hiện tại" : "Chọn khung nguồn đã duyệt"}</option>{p.versions.filter(v => v.status === "Published" && !v.isLegacy && v.gradeLevel < p.grade && v.graphVersionId !== p.versionId && v.nodeMappings?.some(n => n.nodeId === edge.sourceNodeId)).map(v => <option key={v.graphVersionId} value={v.graphVersionId}>{frameworkTitle(v)} · Khối {v.gradeLevel} · Đã duyệt</option>)}</select></>}
        <div className="kg-actions">{e.hasPermission(permissions.edgesCreate) && edge.relationType === "PrerequisiteOf" && e.nodes.some(n => n.nodeId === edge.sourceNodeId) && e.nodes.some(n => n.nodeId === edge.targetNodeId) && <button type="button" className="th-secondary-button" disabled={e.busy} onClick={() => e.reverse.mutate(edge, { onSuccess: id => p.onSelect({ kind: "edge", id }) })}>Đảo chiều trong bản nháp</button>}<button type="button" className="th-secondary-button" disabled={disabled} onClick={() => { e.removeEdge(edge.edgeId); p.onClear(); }}>Gỡ quan hệ khỏi khung</button></div><p className="kg-muted mt-2">Quan hệ vẫn còn trong thư viện môn. Thay đổi có hiệu lực với khung này khi lưu bản nháp.</p>
        <details className="kg-muted mt-3"><summary>Thông tin thư viện</summary>Loại gốc: {relationInfo[e.catalog.data?.edges.find(r => r.edgeId === edge.edgeId)?.relationType ?? edge.relationType].label}. Loại bên trên là loại áp dụng trong bản nháp.</details>
      </> : <p className="kg-muted mt-3">Trọng số: {edge.weight} · Chỉ xem.</p>}
    </> : <div className="kg-empty"><strong>Chọn một chủ đề hoặc đường nối</strong><p className="kg-muted">Xem ý nghĩa và chỉnh cấu trúc ngay tại đây.</p></div>}
    {p.activeIssue && <div className="kg-issue mt-4"><strong>Cần xử lý</strong><p>{p.activeIssue.message}</p><p>{p.activeIssue.remedy}</p></div>}
    {p.selection && !node && !edge && e.editable && <div className="kg-issue"><p>Không tìm thấy {p.selection.kind === "node" ? "chủ đề" : "quan hệ"} #{p.selection.id} trong thư viện đã tải.</p><button type="button" className="th-secondary-button" disabled={e.busy} onClick={() => { if (p.selection?.kind === "node") e.removeNode(p.selection.id); else if (p.selection) e.removeEdge(p.selection.id); }}>Gỡ khỏi khung</button></div>}
  </div>;
}
