import { useState } from "react";
import { relationInfo, relationTypes } from "../../utils/knowledgeRelations";
import { extractProblemDetails } from "../../utils/problemDetails";
import { academicLifecycleError } from "../../utils/academicLifecycleError";
import type { KnowledgeRelationType } from "../../types/knowledgeGraph";
import type { useKnowledgeDraftEditor } from "./useKnowledgeDraftEditor";
import { TeacherModal } from "./TeacherOverlays";

export function DraftRelationDialog({ editor: e, source, target, subjectId, versionId, name, onClose, onSelect, onHelp, suspended }: {
  editor: ReturnType<typeof useKnowledgeDraftEditor>; source: string; target: string; subjectId: string; versionId?: string;
  name: (id: string) => string; onClose: () => void; onSelect: (id: string) => void; onHelp: () => void;
  suspended?: boolean;
}) {
  const [type, setType] = useState<KnowledgeRelationType>("PrerequisiteOf");
  const [weight, setWeight] = useState("1");
  const [error, setError] = useState("");
  const existing = e.catalog.data?.edges.find(r => r.sourceNodeId === source && r.targetNodeId === target && r.relationType === type);
  const included = existing && e.edges.some(m => m.edgeId === existing.edgeId);
  const submit = async () => {
    if (e.busy) return;
    if (!weight.trim() || !Number.isFinite(Number(weight)) || Number(weight) < 0 || Number(weight) > 1) { setError("Nhập trọng số từ 0 đến 1."); return; }
    if (existing) { if (!included) e.addEdge(existing); onSelect(existing.edgeId); onClose(); return; }
    try { const edge = await e.createEdge.mutateAsync({ graphVersionId: versionId, subjectId, sourceNodeId: source, targetNodeId: target, relationType: type, weight: Number(weight) }); onClose(); onSelect(edge.edgeId); }
    catch (err) { if (extractProblemDetails(err).errorCode === "DUPLICATE_RESOURCE") { await e.catalog.refetch(); setError("Quan hệ vừa được tạo ở phiên khác. Kiểm tra quan hệ có sẵn rồi thêm vào khung."); } else setError(academicLifecycleError(err, "Chưa tạo được quan hệ. Kiểm tra kết nối và quyền truy cập rồi thử lại.")); }
  };
  return <TeacherModal isOpen={!suspended} title="Tạo liên kết giữa hai chủ đề" description="Kiểm tra chiều và ý nghĩa trước khi xác nhận." size="lg" onClose={() => !e.busy && onClose()}>
    <form onSubmit={event => { event.preventDefault(); void submit(); }}><p className="kg-hint"><strong>Nguồn: {name(source)}</strong> → <strong>Đích: {name(target)}</strong></p><div className="kg-choice-grid">{relationTypes.map(t => <button type="button" className="kg-choice" key={t} aria-pressed={type === t} onClick={() => setType(t)}><strong>{relationInfo[t].label}</strong><span>{relationInfo[t].intent}</span></button>)}</div><p className="kg-hint">{name(source)} → {name(target)} · {relationInfo[type].intent}.<br />{relationInfo[type].effect}</p>
      <label className="kg-label" htmlFor="kg-create-weight">Trọng số (0–1)</label><input id="kg-create-weight" className="th-input w-full" type="number" min="0" max="1" step="0.05" value={weight} aria-invalid={!!error || undefined} aria-describedby={error ? "kg-create-edge-error" : undefined} onChange={event => { setWeight(event.target.value); setError(""); }} />
      {existing && <p className="kg-hint mt-3">{included ? "Quan hệ đã có trong khung này." : "Quan hệ đã có trong thư viện. Dùng quan hệ có sẵn, không cần tạo lại."}</p>}
      {error && <p id="kg-create-edge-error" className="kg-field-error" role="alert" data-toast-mirror="off">{error}</p>}
      <div className="kg-actions"><button type="button" className="th-secondary-button" onClick={onHelp}>? Giải thích quan hệ</button><button type="button" className="th-secondary-button" disabled={e.busy} onClick={onClose}>Hủy</button><button type="submit" className="th-primary-button" disabled={e.busy}>{e.busy ? "Đang tạo…" : included ? "Định vị quan hệ" : existing ? "Thêm quan hệ có sẵn vào khung" : "Tạo liên kết"}</button></div>
    </form>
  </TeacherModal>;
}
