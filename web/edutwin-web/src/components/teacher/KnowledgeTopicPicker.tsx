import { useEffect, useId, useMemo, useRef, useState } from "react";
import { knowledgeTopicGroups, type TopicOptionNode } from "../../utils/knowledgeTopicOptions";

interface Props { id?: string; nodes: readonly TopicOptionNode[]; value: string; onChange: (value: string) => void;
  disabled?: boolean; placeholder?: string; emptyLabel?: string; label?: string }

export function KnowledgeTopicPicker({ id, nodes, value, onChange, disabled, placeholder = "Chọn chủ đề kiến thức", emptyLabel, label = "Chủ đề kiến thức" }: Props) {
  const generatedId = useId(), panelId = `${generatedId}-panel`;
  const root = useRef<HTMLDivElement>(null), trigger = useRef<HTMLButtonElement>(null);
  const [open, setOpen] = useState(false), [query, setQuery] = useState(""), [groupId, setGroupId] = useState("");
  const groups = useMemo(() => knowledgeTopicGroups(nodes), [nodes]);
  const filtered = useMemo(() => knowledgeTopicGroups(nodes, query, groupId), [nodes, query, groupId]);
  const selected = nodes.find(n => n.nodeId === value);
  const close = () => { setOpen(false); trigger.current?.focus(); };
  const choose = (next: string) => { onChange(next); close(); };
  useEffect(() => {
    if (!open) return;
    const outside = (event: PointerEvent) => { if (!root.current?.contains(event.target as Node)) setOpen(false); };
    document.addEventListener("pointerdown", outside);
    return () => document.removeEventListener("pointerdown", outside);
  }, [open]);
  return <div ref={root} className="relative min-w-0" onKeyDown={e => { if (e.key === "Escape") { e.preventDefault(); close(); } }}>
    <button ref={trigger} id={id} type="button" aria-label={label} aria-haspopup="dialog" aria-expanded={open && !disabled} aria-controls={panelId}
      disabled={disabled} className="th-select flex min-h-10 w-full items-center justify-between gap-2 text-left text-xs"
      onClick={() => { setQuery(""); setGroupId(""); setOpen(!open); }}>
      <span className="min-w-0 break-words">{selected?.nodeName || (value ? `Chủ đề #${value} · chưa xác minh` : placeholder)}</span><span aria-hidden="true">⌄</span>
    </button>
    {open && !disabled && <div id={panelId} role="dialog" aria-label={`Chọn ${label.toLowerCase()}`} className="th-surface absolute left-0 right-0 top-full z-50 mt-2 min-w-0 rounded-xl border p-3 shadow-xl">
      <div className="flex items-center justify-between gap-2 mb-2"><span className="text-xs font-semibold">Tìm theo tên hoặc mã chủ đề</span><button type="button" onClick={close} className="th-secondary-button px-2 py-1" aria-label="Đóng bộ chọn chủ đề">×</button></div>
      <input autoFocus type="search" value={query} onChange={e => setQuery(e.target.value)} aria-label="Tìm chủ đề" placeholder="Ví dụ: vectơ, tam giác…" className="th-input w-full text-sm" />
      <select aria-label="Lọc chương / nhóm" value={groupId} onChange={e => setGroupId(e.target.value)} className="th-select mt-2 w-full text-xs">
        <option value="">Tất cả chương / nhóm</option>{groups.map(g => <option key={g.id} value={g.id}>{g.name} ({g.topics.length})</option>)}
      </select>
      <div className="mt-2 max-h-72 overflow-y-auto overscroll-contain space-y-2" aria-label="Danh sách chủ đề">
        {emptyLabel && <button type="button" onClick={() => choose("")} className="th-secondary-button w-full text-left text-xs">{emptyLabel}</button>}
        {filtered.map(g => <section key={g.id}><h3 className="sticky top-0 bg-[var(--th-surface)] py-2 text-xs font-bold text-[var(--th-text-secondary)]">{g.name}</h3>
          {g.topics.map(n => <button key={n.nodeId} type="button" aria-pressed={n.nodeId === value} onClick={() => choose(n.nodeId)}
            className={`block w-full rounded-lg p-2 text-left hover:bg-[var(--th-surface-subtle)] focus-visible:outline-2 focus-visible:outline-teal-500 ${n.nodeId === value ? "bg-teal-500/15 ring-1 ring-teal-500" : ""}`}>
            <span className="block break-words text-sm font-medium">{n.nodeName}</span><span className="block break-all text-[10px] text-[var(--th-text-muted)]">{n.nodeCode}</span>
          </button>)}</section>)}
        {!filtered.length && <p role="status" className="py-3 text-xs text-[var(--th-text-muted)]">Không tìm thấy chủ đề hoạt động phù hợp.</p>}
      </div>
      <p className="mt-2 text-[10px] text-[var(--th-text-muted)]">Chương là nhóm tổ chức; chỉ chọn các nút Chủ đề đang hoạt động.</p>
    </div>}
  </div>;
}
