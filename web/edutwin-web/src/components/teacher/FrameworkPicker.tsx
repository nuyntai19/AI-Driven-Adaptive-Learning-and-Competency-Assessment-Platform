import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { knowledgeKeys } from "../../utils/knowledgeScope";
import { knowledgeGraphApi } from "../../api/knowledgeGraphApi";
import { permissions } from "../../auth/permissions";
import { frameworkTitle } from "../../utils/knowledgeFrameworkTitle";
import type { useKnowledgeDraftEditor } from "./useKnowledgeDraftEditor";

export function FrameworkPicker({ editor: e, subjectId, grade: initialGrade, onSelect }: { editor: ReturnType<typeof useKnowledgeDraftEditor>; subjectId: string; grade: number; onSelect: (id: string, grade: number) => void }) {
  const [grade, setGrade] = useState(initialGrade);
  const [status, setStatus] = useState("all");
  const [search, setSearch] = useState("");
  const [title, setTitle] = useState("");
  const q = useQuery({ queryKey: knowledgeKeys.versions(e.user?.centerId, e.user?.userId, subjectId, grade), queryFn: () => knowledgeGraphApi.listVersions(subjectId, grade) });
  const list = (q.data ?? []).filter(v => (status === "all" || v.status === status) && frameworkTitle(v).toLocaleLowerCase("vi").includes(search.trim().toLocaleLowerCase("vi")));
  return <div><div className="kg-segments" aria-label="Khối của khung">{[10, 11, 12].map(n => <button type="button" key={n} disabled={e.dirty || e.busy} aria-pressed={grade === n} onClick={() => setGrade(n)}>Khối {n}</button>)}</div>
    <div className="kg-segments" aria-label="Trạng thái khung">{[["all", "Tất cả"], ["Published", "Đã duyệt"], ["Draft", "Bản nháp"], ["Archived", "Lưu trữ"]].map(([value, label]) => <button type="button" key={value} aria-pressed={status === value} onClick={() => setStatus(value)}>{label}</button>)}</div>
    <input type="search" className="th-input w-full mt-3" aria-label="Tìm khung tri thức" placeholder="Tìm tên khung…" value={search} onChange={event => setSearch(event.target.value)} />
    {q.isLoading && <p role="status" className="kg-hint mt-3">Đang tải khung tri thức…</p>}{q.isError && <p role="alert" data-toast-mirror="off" className="kg-issue">Không tải được danh sách khung. <button type="button" onClick={() => q.refetch()}>Thử lại</button></p>}
    <div className="kg-version-cards">{list.map(v => <article key={v.graphVersionId} className="kg-version-card" data-current={v.isCurrent || undefined}><div className="kg-badges mb-2"><span>Khối {v.gradeLevel}</span><span>{v.isLegacy ? "Dữ liệu chuyển tiếp" : v.status === "Draft" ? "Bản nháp" : v.status === "Published" ? "Đã duyệt" : "Lưu trữ"}</span>{v.isCurrent && <span>Hiện hành</span>}</div><h3 className="font-semibold">{frameworkTitle(v)}</h3><p className="kg-muted">{v.nodeCount ?? v.nodeMappings?.length ?? "—"} chủ đề · {v.edgeCount ?? v.edgeMappings?.length ?? "—"} quan hệ</p><div className="kg-actions"><button type="button" className="th-primary-button" disabled={e.dirty || e.busy} onClick={() => onSelect(v.graphVersionId, v.gradeLevel)}>{v.status === "Draft" && !v.isLegacy && e.hasPermission(permissions.nodesUpdate) && e.hasPermission(permissions.edgesUpdate) ? "Tiếp tục soạn" : "Mở để xem"}</button></div></article>)}</div>
    {!q.isLoading && !q.isError && !list.length && <p className="kg-empty">Không có khung khớp bộ lọc. Chọn trạng thái khác hoặc tạo bản nháp mới.</p>}
    {e.hasPermission(permissions.nodesCreate) && e.hasPermission(permissions.edgesCreate) && <div className="kg-hint mt-4"><label className="kg-label" htmlFor="kg-new-framework-title">Tên bản nháp mới · Khối {grade}</label><input id="kg-new-framework-title" className="th-input w-full" maxLength={200} value={title} onChange={event => setTitle(event.target.value)} placeholder={`Khung tri thức Khối ${grade}`} /><div className="kg-actions"><button type="button" className="th-secondary-button" disabled={e.busy || e.dirty} onClick={() => e.create.mutate({ title, grade })}>Tạo khung trống</button>{e.selected && <button type="button" className="th-secondary-button" disabled={e.busy || e.dirty || e.selected.gradeLevel !== grade} onClick={() => e.create.mutate({ title, source: e.selected!.graphVersionId })}>Sao chép khung đang mở thành bản nháp</button>}</div></div>}
    {e.message?.error && <p role="alert" className="kg-issue" data-toast-mirror="off">{e.message.text}</p>}
  </div>;
}
