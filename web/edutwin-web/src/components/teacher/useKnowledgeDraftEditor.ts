import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { knowledgeGraphApi } from "../../api/knowledgeGraphApi";
import { curriculumApi } from "../../api/curriculumApi";
import { useAuthStore } from "../../stores/authStore";
import { knowledgeKeys } from "../../utils/knowledgeScope";
import { academicLifecycleError } from "../../utils/academicLifecycleError";
import { extractProblemDetails } from "../../utils/problemDetails";
import { findReversePrerequisite, replaceDraftPrerequisite } from "../../utils/knowledgeDraftEdges";
import type { CreateKnowledgeEdgeRequest, KnowledgeGraphEdgeDto, KnowledgeGraphVersion, KnowledgeRelationType, KnowledgeVersionEdgeMapping, KnowledgeVersionNodeMapping } from "../../types/knowledgeGraph";

export type DraftIncludeRequest = { kind: "node"; id: string; nonce: number } | { kind: "edge"; id: string; relationType: KnowledgeRelationType; weight: number; nonce: number };
export interface DraftEditorOptions {
  subjectId: string; gradeLevel: number; versionId?: string; canEdit: boolean;
  onSelect: (id: string, grade: number) => void; onDirtyChange?: (dirty: boolean) => void; includeRequest?: DraftIncludeRequest | null;
}

/** One local snapshot for canvas, inspector, diagnostics and save. No business rule lives here. */
export function useKnowledgeDraftEditor({ subjectId, gradeLevel, versionId, canEdit, onSelect, onDirtyChange, includeRequest }: DraftEditorOptions) {
  const user = useAuthStore(s => s.user);
  const hasPermission = useAuthStore(s => s.hasPermission);
  const client = useQueryClient();
  const versionKey = knowledgeKeys.versions(user?.centerId, user?.userId, subjectId, gradeLevel);
  const versions = useQuery({ queryKey: versionKey, queryFn: () => knowledgeGraphApi.listVersions(subjectId, gradeLevel), enabled: !!subjectId && !!gradeLevel });
  const selected = versions.data?.find(v => v.graphVersionId === versionId);
  const sourceVersions = useQuery({ queryKey: knowledgeKeys.versions(user?.centerId, user?.userId, subjectId), queryFn: () => knowledgeGraphApi.listVersions(subjectId), enabled: !!subjectId && canEdit });
  const catalog = useQuery({ queryKey: knowledgeKeys.graph(user?.centerId, user?.userId, subjectId, {}), queryFn: () => knowledgeGraphApi.getGraph(subjectId), enabled: !!subjectId && canEdit });
  const curricula = useQuery({ queryKey: ["teacherCurriculums", "version-panel", user?.centerId, user?.userId, subjectId], queryFn: () => curriculumApi.getAll({ subjectId }), select: response => response.data, enabled: !!subjectId && canEdit });
  const [nodes, setNodes] = useState<KnowledgeVersionNodeMapping[]>([]);
  const [edges, setEdges] = useState<KnowledgeVersionEdgeMapping[]>([]);
  const [baseVersion, setBaseVersion] = useState("");
  const [dirty, setDirty] = useState(false);
  const [reviewed, setReviewed] = useState(false);
  const [libraryWritten, setLibraryWritten] = useState(false);
  const [message, setMessage] = useState<{ text: string; error: boolean; traceId?: string | null } | null>(null);
  const [lastIncluded, setLastIncluded] = useState<string | null>(null);
  const [mountedAt] = useState(() => Date.now());
  useEffect(() => { if (!selected || dirty) return; setNodes(selected.nodeMappings ?? []); setEdges(selected.edgeMappings ?? []); setBaseVersion(selected.rowVersion); setReviewed(false); }, [selected, dirty]);
  const change = () => { setDirty(true); setReviewed(false); setMessage(null); };
  const notify = (text: string) => setMessage({ text, error: false });
  const reportError = (err: unknown) => {
    const details = extractProblemDetails(err);
    setMessage({ text: details.errorCode === "CONCURRENCY_CONFLICT" ? "Khung vừa thay đổi ở phiên khác. Thay đổi của bạn chưa được lưu. Hủy cấu trúc đang soạn rồi tải bản mới để tiếp tục."
      : academicLifecycleError(err, "Chưa hoàn tất thao tác. Kiểm tra kết nối, quyền truy cập rồi thử lại."), error: true, traceId: details.traceId });
  };
  useEffect(() => {
    if (!includeRequest || includeRequest.nonce < mountedAt) return;
    if (includeRequest.kind === "node") { setNodes(prev => prev.some(n => n.nodeId === includeRequest.id) ? prev : [...prev, { nodeId: includeRequest.id, parentNodeId: null }]); setLastIncluded(includeRequest.id); }
    else setEdges(prev => prev.some(e => e.edgeId === includeRequest.id) ? prev : [...prev, { edgeId: includeRequest.id, relationType: includeRequest.relationType, weight: includeRequest.weight }]);
    setDirty(true); setReviewed(false); setLibraryWritten(true);
    setMessage({ text: "Đã tạo trong thư viện và thêm vào cấu trúc đang soạn. Bấm “Lưu bản nháp” để ghi cấu trúc khung.", error: false });
  }, [includeRequest, mountedAt]);
  const refresh = () => client.invalidateQueries({ queryKey: knowledgeKeys.all });
  const create = useMutation({ mutationFn: ({ title, source, grade = gradeLevel }: { title: string; source?: string; grade?: number }) => knowledgeGraphApi.createVersion({ subjectId, gradeLevel: grade, title: title.trim() || undefined, sourceGraphVersionId: source }), onSuccess: async version => { await refresh(); onSelect(version.graphVersionId, version.gradeLevel); }, onError: reportError });
  const save = useMutation({ mutationFn: () => knowledgeGraphApi.saveVersion(versionId!, { rowVersion: baseVersion, nodeMappings: nodes, edgeMappings: edges }), onSuccess: async version => {
    client.setQueryData<KnowledgeGraphVersion[]>(versionKey, previous => previous?.map(v => v.graphVersionId === version.graphVersionId ? version : v));
    setNodes(version.nodeMappings ?? []); setEdges(version.edgeMappings ?? []); setBaseVersion(version.rowVersion); setDirty(false); setReviewed(false); setLibraryWritten(false); notify("Đã lưu cấu trúc bản nháp."); await refresh();
  }, onError: reportError });
  const publish = useMutation({ mutationFn: () => knowledgeGraphApi.publishVersion(versionId!, baseVersion), onSuccess: async () => { notify("Đã duyệt và dùng làm khung hiện hành cho khối."); await refresh(); }, onError: reportError });
  const reverse = useMutation({ mutationFn: async (edge: KnowledgeGraphEdgeDto) => {
    const existing = findReversePrerequisite(catalog.data?.edges ?? [], edge);
    if (existing) return existing.edgeId;
    try { const result = await knowledgeGraphApi.createEdge({ graphVersionId: versionId, subjectId, sourceNodeId: edge.targetNodeId, targetNodeId: edge.sourceNodeId, relationType: "PrerequisiteOf", weight: edge.weight }); setLibraryWritten(true); return result.edgeId; }
    catch (err) { if (extractProblemDetails(err).errorCode === "DUPLICATE_RESOURCE") { const latest = await knowledgeGraphApi.getGraph(subjectId); const found = findReversePrerequisite(latest.edges, edge); if (found) return found.edgeId; } throw err; }
  }, onSuccess: async (id, edge) => { setEdges(previous => replaceDraftPrerequisite(previous, edge.edgeId, id, previous.find(m => m.edgeId === edge.edgeId)?.weight ?? edge.weight)); change(); notify("Đã đảo chiều trong cấu trúc đang soạn. Cạnh mới có thể đã được ghi vào thư viện; lưu bản nháp để ghi cấu trúc khung."); await refresh(); }, onError: reportError });
  const createEdge = useMutation({ mutationFn: (request: CreateKnowledgeEdgeRequest) => knowledgeGraphApi.createEdge(request), onSuccess: async result => {
    setEdges(previous => previous.some(e => e.edgeId === result.edgeId) ? previous : [...previous, { edgeId: result.edgeId, relationType: result.relationType, weight: result.weight }]);
    change(); setLibraryWritten(true); notify("Đã tạo quan hệ trong thư viện. Lưu bản nháp để ghi cấu trúc khung."); await refresh();
  } });
  const removeTopic = useMutation({ mutationFn: (id: string) => knowledgeGraphApi.deleteNode(id), onSuccess: async () => { notify("Đã xóa chủ đề khỏi thư viện và các bản nháp sử dụng nó."); setLibraryWritten(true); await refresh(); } });
  const busy = create.isPending || save.isPending || publish.isPending || reverse.isPending || createEdge.isPending || removeTopic.isPending;
  useEffect(() => { onDirtyChange?.(dirty || busy); return () => onDirtyChange?.(false); }, [dirty, busy, onDirtyChange]);
  useEffect(() => {
    if (!dirty && !busy) return;
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty, busy]);
  return { user, hasPermission, versions, selected, sourceVersions, catalog, curricula, nodes, edges, baseVersion, dirty, reviewed, setReviewed, libraryWritten, setLibraryWritten, message, setMessage, lastIncluded,
    editable: canEdit && selected?.status === "Draft" && !selected.isLegacy, stale: dirty && !!selected && baseVersion !== selected.rowVersion, busy, change, notify, reportError, refresh, create, save, publish, reverse, createEdge, removeTopic,
    addNode: (id: string) => { setNodes(prev => prev.some(n => n.nodeId === id) ? prev : [...prev, { nodeId: id, parentNodeId: null }]); change(); },
    removeNode: (id: string) => { setNodes(prev => prev.filter(n => n.nodeId !== id).map(n => n.parentNodeId === id ? { ...n, parentNodeId: null } : n)); change(); },
    setParent: (id: string, parentNodeId: string | null) => { setNodes(prev => prev.map(n => n.nodeId === id ? { ...n, parentNodeId } : n)); change(); },
    addEdge: (edge: KnowledgeGraphEdgeDto) => { setEdges(prev => prev.some(m => m.edgeId === edge.edgeId) ? prev : [...prev, { edgeId: edge.edgeId, relationType: edge.relationType, weight: edge.weight }]); change(); },
    removeEdge: (id: string) => { setEdges(prev => prev.filter(e => e.edgeId !== id)); change(); },
    updateEdge: (id: string, patch: Partial<KnowledgeVersionEdgeMapping>) => { setEdges(prev => prev.map(e => e.edgeId === id ? { ...e, ...patch } : e)); change(); },
    discard: () => { setDirty(false); setReviewed(false); setMessage(null); void refresh(); },
  };
}
