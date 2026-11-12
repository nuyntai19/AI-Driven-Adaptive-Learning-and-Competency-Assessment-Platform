import { useMemo, useState, type CSSProperties, type FormEvent } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { knowledgeGraphApi } from "../../api/knowledgeGraphApi";
import { useAuthStore } from "../../stores/authStore";
import type { KnowledgeGraphNodeDto, KnowledgeNodeType } from "../../types/knowledgeGraph";
import { extractProblemDetails } from "../../utils/problemDetails";
import { buildNodeCodePrefix, isStandardNodeCode, maxNodeCodeLength, nodeCodeSyntax, resolveEditedNodeCode, type EditCodeMode } from "../../utils/knowledgeNodeCode";
import { TeacherModal } from "./TeacherOverlays";

interface Props {
  node: KnowledgeGraphNodeDto;
  subjectId: string;
  subjectCode?: string;
  subjectName?: string;
  gradeLevel?: number;
  onClose: () => void;
  onSaved: (node: { nodeId: string; nodeCode: string }) => void;
}

const typeOptions: [KnowledgeNodeType, string][] = [["Chapter", "Chương (Chapter)"], ["Topic", "Chủ đề (Topic)"], ["Skill", "Kỹ năng (Skill)"], ["Concept", "Khái niệm (Concept)"]];
const label: CSSProperties = { display: "block", fontSize: "0.8rem", fontWeight: 600, color: "var(--th-text-secondary)", marginBottom: 6 };

/**
 * Edit a library topic from the framework editor. The code follows the standard
 * [Môn][Khối]_[Loại]_[Tên viết tắt]_[STT]: it is re-derived when the name or level changes (same
 * sequence when the prefix is unchanged, otherwise the server's next free number), can be kept
 * as is, or typed manually. Topics in approved frameworks/published curricula are refused by the
 * server with an explanation; the dialog shows it unchanged.
 */
export function KnowledgeNodeEditDialog({ node, subjectId, subjectCode, subjectName, gradeLevel, onClose, onSaved }: Props) {
  const user = useAuthStore(s => s.user);
  const [name, setName] = useState(node.nodeName);
  const [type, setType] = useState<KnowledgeNodeType>(node.nodeType === "Subject" ? "Topic" : node.nodeType);
  const [importance, setImportance] = useState(Number(node.examImportance));
  const [minutes, setMinutes] = useState(Number(node.estimatedLearningMinutes) || 45);
  const [description, setDescription] = useState(node.description ?? "");
  const [active, setActive] = useState(node.isActive);
  const [mode, setMode] = useState<EditCodeMode>("follow");
  const [manualCode, setManualCode] = useState(node.nodeCode);
  const [error, setError] = useState("");

  const nameOrTypeChanged = name.trim() !== node.nodeName || type !== node.nodeType;
  const prefix = useMemo(() => subjectCode
    ? buildNodeCodePrefix({ subjectCode, subjectName, gradeLevel, nodeType: type, nodeName: name })
    : null, [subjectCode, subjectName, gradeLevel, type, name]);
  const preliminary = resolveEditedNodeCode({ currentCode: node.nodeCode, prefix, mode, manualCode, nameOrTypeChanged });
  const suggestion = useQuery({
    queryKey: ["knowledge-node-code", user?.centerId, user?.userId, subjectId, prefix],
    queryFn: ({ signal }) => knowledgeGraphApi.suggestNodeCode(subjectId, prefix!, signal),
    enabled: preliminary.reason === "pending" && !!prefix,
    staleTime: 0,
    retry: false,
  });
  const resolved = resolveEditedNodeCode({ currentCode: node.nodeCode, prefix, mode, manualCode, nameOrTypeChanged, suggestion: suggestion.data });
  const code = resolved.code;
  const codeChanged = !!code && code !== node.nodeCode;

  const save = useMutation({
    mutationFn: () => knowledgeGraphApi.updateNode(node.nodeId, {
      parentNodeId: node.parentNodeId, nodeName: name.trim(), description: description.trim() || null,
      orderIndex: node.orderIndex, examImportance: importance, estimatedLearningMinutes: minutes, isActive: active,
      rowVersion: node.rowVersion,
      ...(type !== node.nodeType ? { nodeType: type } : {}),
      ...(codeChanged ? { nodeCode: code! } : {}),
    }),
    onSuccess: saved => onSaved({ nodeId: saved.nodeId, nodeCode: saved.nodeCode }),
    onError: async err => {
      const details = extractProblemDetails(err);
      if (details.errorCode === "DUPLICATE_RESOURCE" && resolved.reason === "suggested") {
        const next = await suggestion.refetch();
        setError(`Mã ${code} vừa được dùng.${next.data?.code ? ` Đã cập nhật mã mới ${next.data.code}, bấm Lưu lần nữa.` : ""}`);
      } else if (details.errorCode === "CONCURRENCY_CONFLICT") {
        setError("Chủ đề vừa được thay đổi ở nơi khác. Đóng hộp thoại để tải lại rồi sửa lại.");
      } else setError(details.detail || "Không thể lưu chủ đề. Vui lòng thử lại.");
    },
  });

  const submit = (e: FormEvent) => {
    e.preventDefault();
    setError("");
    if (!name.trim()) return setError("Vui lòng nhập tên chủ đề.");
    if (importance < 0 || importance > 100) return setError("Trọng số thi phải nằm trong khoảng 0–100%.");
    if (minutes <= 0) return setError("Thời lượng học phải lớn hơn 0 phút.");
    if (!code) return setError(suggestion.isError ? "Không lấy được số thứ tự cho mã mới. Thử lại hoặc giữ mã cũ." : "Đang lấy số thứ tự cho mã mới, vui lòng đợi.");
    if (codeChanged && (code.length > maxNodeCodeLength || !isStandardNodeCode(code)))
      return setError(`Mã mới phải theo cú pháp ${nodeCodeSyntax}, ví dụ MATH12_T_GTTP_01.`);
    save.mutate();
  };

  const hint = resolved.reason === "manual" ? (code === node.nodeCode ? "Giữ nguyên mã hiện tại." : isStandardNodeCode(code ?? "") ? "Mã nhập tay, đúng cú pháp." : `⚠ Mã cần đúng cú pháp ${nodeCodeSyntax}.`)
    : resolved.reason === "kept" ? (mode === "keep" ? "Giữ mã cũ theo lựa chọn của bạn." : "Mã giữ nguyên cho tới khi bạn đổi tên hoặc cấp độ.")
    : resolved.reason === "sameSequence" ? "Tên và cấp độ vẫn cho cùng tiền tố nên giữ số thứ tự hiện tại."
    : resolved.reason === "suggested" ? `Mã sẽ đổi ${node.nodeCode} → ${code} (số thứ tự đã kiểm tra trên toàn môn).`
    : suggestion.isError ? "⚠ Không lấy được số thứ tự từ máy chủ." : "Đang kiểm tra số thứ tự trên toàn môn…";
  const legacy = !isStandardNodeCode(node.nodeCode);

  return <TeacherModal isOpen title="Sửa chủ đề" maxWidth="560px" onClose={() => !save.isPending && onClose()}>
    <form onSubmit={submit} style={{ display: "flex", flexDirection: "column", gap: 14 }}>
      <div>
        <label htmlFor="edit-node-name" style={label}>Tên chủ đề / Khái niệm *</label>
        <input id="edit-node-name" className="th-input" style={{ width: "100%" }} maxLength={200} value={name} onChange={e => setName(e.target.value)} />
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))", gap: 12 }}>
        <div>
          <label htmlFor="edit-node-type" style={label}>Cấp độ phân loại</label>
          <select id="edit-node-type" className="th-select" style={{ width: "100%" }} value={type} onChange={e => setType(e.target.value as KnowledgeNodeType)}>
            {typeOptions.map(([value, text]) => <option key={value} value={value}>{text}</option>)}
          </select>
        </div>
        <div>
          <label htmlFor="edit-node-importance" style={label}>Trọng số thi (%)</label>
          <input id="edit-node-importance" className="th-input" style={{ width: "100%" }} type="number" min={0} max={100} value={importance} onChange={e => setImportance(Number(e.target.value))} />
        </div>
        <div>
          <label htmlFor="edit-node-minutes" style={label}>Thời lượng (phút)</label>
          <input id="edit-node-minutes" className="th-input" style={{ width: "100%" }} type="number" min={5} max={300} value={minutes} onChange={e => setMinutes(Number(e.target.value))} />
        </div>
      </div>
      <div>
        <div style={{ display: "flex", justifyContent: "space-between", gap: 8 }}>
          <label htmlFor="edit-node-code" style={label}>Mã điểm tri thức</label>
          <span style={{ fontSize: "0.75rem", color: "var(--th-text-muted)" }}>Hiện tại: <code>{node.nodeCode}</code></span>
        </div>
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
          <input id="edit-node-code" className="th-input" style={{ flex: "1 1 220px", minWidth: 0, fontFamily: "monospace" }} maxLength={maxNodeCodeLength} spellCheck={false}
            aria-describedby="edit-node-code-hint" placeholder={`Cú pháp tự sinh: ${nodeCodeSyntax}`}
            value={mode === "manual" ? manualCode : code ?? ""} onChange={e => { setMode("manual"); setManualCode(e.target.value.toUpperCase()); }} />
          {mode === "manual" || mode === "keep"
            ? <button type="button" className="th-secondary-button" onClick={() => setMode("follow")}>Dùng mã tự sinh</button>
            : resolved.reason === "suggested"
              ? <button type="button" className="th-secondary-button" onClick={() => setMode("keep")}>Giữ mã cũ</button>
              : legacy && resolved.reason === "kept" && prefix
                ? <button type="button" className="th-secondary-button" onClick={() => setMode("standard")}>Chuẩn hoá mã</button>
                : null}
        </div>
        <p id="edit-node-code-hint" role="status" style={{ margin: "6px 0 0", fontSize: "0.75rem", color: "var(--th-text-muted)", overflowWrap: "anywhere" }}>{hint}</p>
      </div>
      <div>
        <label htmlFor="edit-node-description" style={label}>Mô tả chi tiết</label>
        <textarea id="edit-node-description" className="th-input" style={{ width: "100%", minHeight: 70 }} maxLength={1000} value={description} onChange={e => setDescription(e.target.value)} />
      </div>
      <label style={{ display: "flex", gap: 8, alignItems: "center", fontSize: "0.85rem" }}>
        <input type="checkbox" checked={active} onChange={e => setActive(e.target.checked)} />Đang hoạt động
      </label>
      {error && <p role="alert" style={{ margin: 0, color: "var(--th-danger)", fontSize: "0.85rem", fontWeight: 600 }}>{error}</p>}
      <div style={{ display: "flex", justifyContent: "flex-end", gap: 10 }}>
        <button type="button" className="th-secondary-button" disabled={save.isPending} onClick={onClose}>Hủy</button>
        <button type="submit" className="th-primary-button" disabled={save.isPending}>{save.isPending ? "Đang lưu…" : "Lưu chủ đề"}</button>
      </div>
    </form>
  </TeacherModal>;
}
