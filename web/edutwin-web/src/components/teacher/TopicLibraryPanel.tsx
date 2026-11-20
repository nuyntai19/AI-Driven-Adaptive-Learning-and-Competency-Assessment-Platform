import { frameworkTitle } from "../../utils/knowledgeFrameworkTitle";
import { useMemo, useState } from "react";
import type { KnowledgeGraphNodeDto, KnowledgeGraphVersion } from "../../types/knowledgeGraph";
import type { Curriculum } from "../../types/curriculum";
import { filterLibraryTopics, versionMatches, type LibraryFilter } from "../../utils/knowledgeDraftEditor";

interface Props {
  topics: KnowledgeGraphNodeDto[]; versions: KnowledgeGraphVersion[]; curricula: Curriculum[];
  selectedIds: ReadonlySet<string>; grade: number; ready: boolean; loading: boolean; failed: boolean;
  sourceLoading: boolean; sourceFailed: boolean; disabled: boolean;
  onRetry: () => void; onRetrySources: () => void; onAdd: (id: string) => void; onLocate: (id: string) => void; onCreate?: () => void;
}
export function TopicLibraryPanel(props: Props) {
  const defaults: LibraryFilter = { grade: props.grade, status: "all", source: "", membership: "all", search: "", legacy: false };
  const [filter, setFilter] = useState(defaults);
  const [limit, setLimit] = useState(40);
  const [notice, setNotice] = useState("");
  const sourceCurriculum = filter.source.startsWith("curriculum:") ? props.curricula.find(c => c.curriculumId === filter.source.slice(11)) : undefined;
  const filteredVersions = props.versions.filter(v => versionMatches(v, filter));
  const visible = useMemo(() => filterLibraryTopics(props.topics, props.versions, props.selectedIds, filter, sourceCurriculum?.nodeIds), [props.topics, props.versions, props.selectedIds, filter, sourceCurriculum]);
  const update = (patch: Partial<LibraryFilter>) => {
    setLimit(40);
    setFilter(previous => {
      const next = { ...previous, ...patch };
      if ((patch.grade !== undefined || patch.status !== undefined || patch.legacy !== undefined) && next.source) {
        next.source = ""; setNotice("Đã bỏ nguồn cụ thể để phù hợp bộ lọc mới.");
      }
      return next;
    });
  };
  return <div className="kg-library-content">
    <div className="kg-panel-heading"><div><span className="kg-eyebrow">TÌM VÀ THÊM NỘI DUNG</span><h3>Thư viện chủ đề</h3></div>{props.onCreate && <button type="button" className="th-secondary-button" disabled={props.disabled} onClick={props.onCreate}>+ Tạo mới</button>}</div>
    <p className="kg-muted">Khung đang soạn thuộc <strong>Khối {props.grade}</strong>. Bộ lọc bên dưới chỉ thay nguồn tìm kiếm.</p>
    <label className="kg-label">Tìm chủ đề từ khối</label>
    <div className="kg-segments" aria-label="Tìm chủ đề từ khối">{([10, 11, 12] as const).map(grade => <button type="button" key={grade} aria-pressed={filter.grade === grade} onClick={() => update({ grade })}>Khối {grade}</button>)}</div>
    <div className="kg-segments kg-subsegments"><button type="button" aria-pressed={filter.grade === "all"} onClick={() => update({ grade: "all" })}>Tất cả khối</button><button type="button" aria-pressed={filter.grade === "unknown"} onClick={() => update({ grade: "unknown", status: "all" })}>Chưa rõ khối</button></div>
    <label className="kg-label" htmlFor="kg-source-status">Trạng thái khung nguồn</label>
    <select id="kg-source-status" className="th-select w-full" value={filter.status} onChange={e => update({ status: e.target.value as LibraryFilter["status"] })}>
      <option value="all">Tất cả (trừ lưu trữ)</option><option value="Published">Đã duyệt</option><option value="Draft">Bản nháp</option><option value="Archived">Lưu trữ</option>
    </select>
    <label className="kg-label" htmlFor="kg-source-framework">Khung / giáo trình nguồn</label>
    <select id="kg-source-framework" className="th-select w-full" value={filter.source} onChange={e => update({ source: e.target.value })} disabled={props.sourceLoading}>
      <option value="">Các chủ đề của phạm vi đã lọc</option>
      <optgroup label="Theo khung tri thức">{filteredVersions.map(v => <option key={v.graphVersionId} value={v.graphVersionId}>{frameworkTitle(v)} · Khối {v.gradeLevel} · {v.status === "Draft" ? "Bản nháp" : v.status === "Published" ? "Đã duyệt" : "Lưu trữ"}{v.isLegacy ? " · Chuyển tiếp" : ""}</option>)}</optgroup>
      <optgroup label="Theo giáo trình">{props.curricula.filter(c => (filter.grade === "all" || c.gradeLevel === filter.grade) && (filter.status === "all" ? c.reviewStatus !== "Archived" : c.reviewStatus === filter.status)).map(c => <option key={c.curriculumId} value={`curriculum:${c.curriculumId}`}>{c.title} · Khối {c.gradeLevel ?? "?"} · {c.reviewStatus === "Draft" ? "Bản nháp" : c.reviewStatus === "Published" ? "Đã xuất bản" : "Lưu trữ"}</option>)}</optgroup>
    </select>
    <label className="kg-label" htmlFor="kg-library-membership">Trong khung đang soạn</label>
    <select id="kg-library-membership" className="th-select w-full" value={filter.membership} onChange={e => update({ membership: e.target.value as LibraryFilter["membership"] })}><option value="all">Tất cả</option><option value="available">Chưa thêm vào khung</option><option value="included">Đã có trong khung</option></select>
    <label className="kg-label" htmlFor="kg-library-search">Tìm tên hoặc mã chủ đề</label>
    <input id="kg-library-search" type="search" className="th-input w-full" value={filter.search} placeholder="Ví dụ: Đạo hàm…" onChange={e => update({ search: e.target.value })} />
    <details className="kg-muted mt-2"><summary>Nguồn bổ sung</summary><label className="flex gap-2 mt-2"><input type="checkbox" checked={filter.legacy} onChange={e => update({ legacy: e.target.checked })} />Hiện khung dữ liệu chuyển tiếp</label></details>
    <div className="kg-library-summary"><span role="status">{props.loading ? "Đang tải thư viện…" : `${visible.length} chủ đề trong phạm vi đọc`}</span><button type="button" onClick={() => { setFilter(defaults); setLimit(40); setNotice(""); }}>Xóa bộ lọc</button></div>
    <p className="kg-muted">Nguồn: {filter.grade === "all" ? "mọi khối" : filter.grade === "unknown" ? "chưa rõ khối" : `Khối ${filter.grade}`} · {filter.status === "all" ? "mọi trạng thái đang dùng" : filter.status === "Draft" ? "bản nháp" : filter.status === "Published" ? "đã duyệt" : "lưu trữ"}{sourceCurriculum ? ` · Giáo trình ${sourceCurriculum.title}` : ""}</p>
    {notice && <p className="kg-muted" role="status">{notice}</p>}
    {props.failed && <p role="alert" data-toast-mirror="off">Không tải được thư viện. <button type="button" onClick={props.onRetry}>Thử lại</button></p>}
    {props.sourceLoading && <p role="status" className="kg-muted">Đang tải thông tin khối và nơi sử dụng…</p>}
    {props.sourceFailed && <p role="alert" data-toast-mirror="off">Chưa tải đủ thông tin sử dụng. <button type="button" onClick={props.onRetrySources}>Thử lại nguồn</button></p>}
    <div className="kg-topic-cards">
      {visible.slice(0, limit).map(node => { const included = props.selectedIds.has(node.nodeId); const sources = props.versions.filter(v => v.nodeMappings?.some(n => n.nodeId === node.nodeId)); return <article key={node.nodeId} className="kg-topic-card">
        <strong>{node.nodeName}</strong><code>{node.nodeCode}</code>
        <div className="kg-badges">{sources.map(v => <span key={v.graphVersionId} title={frameworkTitle(v)}>Khối {v.gradeLevel} · {v.status === "Draft" ? "Bản nháp" : v.status === "Published" ? "Đã duyệt" : "Lưu trữ"}</span>)}{!sources.length && !props.sourceLoading && <span>{node.gradeLevels?.length ? `Khối ${node.gradeLevels.join(", ")}` : "Chưa xác định khối"}</span>}{!node.isActive && <span>Đã tắt</span>}</div>
        <button type="button" className={included ? "th-secondary-button" : "th-primary-button"} disabled={!included && (props.disabled || !props.ready)} aria-label={`${included ? "Định vị" : "Thêm vào khung"}: ${node.nodeName}`} onClick={() => included ? props.onLocate(node.nodeId) : props.onAdd(node.nodeId)}>{included ? "Đã có · Định vị" : "+ Thêm vào khung"}</button>
      </article>; })}
      {props.ready && !props.loading && !visible.length && <div className="kg-empty"><p>Không có chủ đề khớp bộ lọc.</p><button type="button" className="th-secondary-button" onClick={() => update({ grade: "unknown", status: "all", source: "" })}>Xem chủ đề chưa rõ khối</button><p className="kg-muted">Chủ đề đã thêm vẫn nằm trong khung, dù không khớp bộ lọc thư viện.</p></div>}
      {visible.length > limit && <button type="button" className="th-secondary-button" onClick={() => setLimit(n => n + 40)}>Xem thêm chủ đề</button>}
    </div>
  </div>;
}
