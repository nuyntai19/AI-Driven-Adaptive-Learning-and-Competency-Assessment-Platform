import { useState } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter } from "react-router-dom";
import { KnowledgeVersionPanel } from "../../src/components/teacher/KnowledgeVersionPanel";
import { knowledgeGraphApi } from "../../src/api/knowledgeGraphApi";
import { curriculumApi } from "../../src/api/curriculumApi";
import { organizationApi } from "../../src/api/organizationApi";
import { TeacherKnowledgeGraphView } from "../../src/pages/teacher/TeacherKnowledgeGraphView";
import { useAuthStore } from "../../src/stores/authStore";
import { permissions } from "../../src/auth/permissions";
import { applyTheme, useThemeMode } from "../../src/utils/themeMode";
import type { KnowledgeGraphNodeDto, KnowledgeGraphEdgeDto, KnowledgeGraphVersion } from "../../src/types/knowledgeGraph";
import "../../src/index.css";
import "../../src/components/teacher/teacherDesignSystem.css";
localStorage.setItem("edutwin-theme", "dark");
applyTheme("dark");

const topic = (id: string, nodeName: string): KnowledgeGraphNodeDto => ({ nodeId: id, nodeName, nodeCode: `MATH12_T_${id}`, nodeType: "Topic", isActive: true, parentNodeId: null, description: "Nội dung chủ đề để kiểm tra giao diện.", orderIndex: 0, examImportance: 3, estimatedLearningMinutes: 45, rowVersion: "1" });
let topics = [topic("1", "Giải tích thực và phức"), topic("2", "Đại số trừu tượng"), topic("3", "Tô-pô đại cương"), topic("4", "Hình học vi phân"), topic("5", "Lý thuyết số")];
const edges: KnowledgeGraphEdgeDto[] = [{ edgeId: "10", sourceNodeId: "1", targetNodeId: "2", relationType: "RelatedTo", weight: 0, rowVersion: "1" }, { edgeId: "11", sourceNodeId: "1", targetNodeId: "4", relationType: "PrerequisiteOf", weight: 1, rowVersion: "1" }];
let version: KnowledgeGraphVersion;
const state = { writes: [] as { action: string; payload: unknown }[], fail: "", readOnly: false, scope: "draft12" };
const fail = () => { if (!state.fail) return; throw Object.assign(new Error("fixture error"), { isAxiosError: true, response: { status: state.fail === "network" ? 500 : 409, data: { errorCode: state.fail === "conflict" ? "CONCURRENCY_CONFLICT" : "INVALID_STATE_TRANSITION", detail: state.fail === "delete" ? "Chủ đề còn câu hỏi đang sử dụng." : "fixture error", traceId: "fixture-trace" } } }); };
const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
const sources = (): KnowledgeGraphVersion[] => [version, ...[null, "", "   "].map((title, i) => ({ ...version, graphVersionId: `untitled-${i}`, title, status: "Published" as const, isCurrent: false, nodeMappings: [], edgeMappings: [] })), { ...version, graphVersionId: "d10", title: "Khung nháp Khối 10", status: "Draft", isCurrent: false, gradeLevel: 10, nodeMappings: [{ nodeId: "5", parentNodeId: null }], edgeMappings: [] }, { ...version, graphVersionId: "p11", title: "Khung đã duyệt Khối 11", status: "Published", gradeLevel: 11, isCurrent: true, nodeMappings: [{ nodeId: "5", parentNodeId: null }], edgeMappings: [] }];
knowledgeGraphApi.listVersions = async (_subject, grade) => structuredClone(sources().filter(v => !grade || v.gradeLevel === grade));
knowledgeGraphApi.getGraph = async (_subject, scope = {}) => structuredClone({ subjectId: "math", nodes: scope.graphVersionId ? topics.filter(n => version.nodeMappings?.some(m => m.nodeId === n.nodeId)).map(n => ({ ...n, parentNodeId: version.nodeMappings?.find(m => m.nodeId === n.nodeId)?.parentNodeId ?? null, gradeLevel: version.gradeLevel })) : topics, edges: scope.graphVersionId ? edges.filter(r => version.edgeMappings?.some(m => m.edgeId === r.edgeId)).map(r => ({ ...r, ...version.edgeMappings?.find(m => m.edgeId === r.edgeId) })) : edges, resolvedScope: { subjectId: "math", kind: scope.graphVersionId ? "Version" : "Subject", gradeLevel: scope.gradeLevel, graphVersionId: scope.graphVersionId } });
organizationApi.listSubjects = async () => ({ data: [{ subjectId: "math", subjectCode: "MATH", subjectName: "Toán", isActive: true, rowVersion: "1", description: null }], meta: { traceId: "fixture", timestamp: "2026-11-21" } }) as never;
organizationApi.listClasses = async () => ({ data: [], meta: { page: 1, pageSize: 100, totalItems: 0, totalPages: 1 } }) as never;
knowledgeGraphApi.suggestNodeCode = async (_subject, prefix) => ({ prefix, code: `${prefix}_01`, sequence: 1 });
knowledgeGraphApi.createNode = async request => { const node = { ...request, parentNodeId: request.parentNodeId ?? null, description: request.description ?? null, nodeId: String(topics.length + 1), rowVersion: "1" }; state.writes.push({ action: "createNode", payload: structuredClone(request) }); topics.push(node); return node; };
curriculumApi.getAll = async () => ({ data: [], meta: { page: 1, pageSize: 100, totalItems: 0, totalPages: 1 } }) as never;
knowledgeGraphApi.saveVersion = async (_id, request) => { state.writes.push({ action: "save", payload: structuredClone(request) }); fail(); Object.assign(version, request, { rowVersion: String(Number(version.rowVersion) + 1) }); return structuredClone(version); };
knowledgeGraphApi.publishVersion = async () => { state.writes.push({ action: "publish", payload: {} }); fail(); version.status = "Published"; return structuredClone(version); };
knowledgeGraphApi.createEdge = async request => { state.writes.push({ action: "createEdge", payload: structuredClone(request) }); fail(); const edge = { subjectId: "math", edgeId: String(20 + edges.length), sourceNodeId: request.sourceNodeId, targetNodeId: request.targetNodeId, relationType: request.relationType, weight: request.weight, rowVersion: "1" }; edges.push(edge); return structuredClone(edge); };
knowledgeGraphApi.deleteNode = async id => { state.writes.push({ action: "deleteNode", payload: id }); fail(); topics = topics.filter(n => n.nodeId !== id); version.nodeMappings = version.nodeMappings?.filter(n => n.nodeId !== id); };
knowledgeGraphApi.createVersion = async request => { state.writes.push({ action: "createVersion", payload: structuredClone(request) }); const source = sources().find(v => v.graphVersionId === request.sourceGraphVersionId); version = { ...version, graphVersionId: "newDraft", gradeLevel: request.gradeLevel, title: request.title || "Khung mới", status: "Draft", isCurrent: false, nodeMappings: source?.nodeMappings ?? [], edgeMappings: source?.edgeMappings ?? [] }; return structuredClone(version); };
knowledgeGraphApi.updateNode = async (id, request) => { state.writes.push({ action: "updateNode", payload: structuredClone(request) }); fail(); const node = topics.find(n => n.nodeId === id)!; Object.assign(node, request); return { ...node, subjectId: "math" }; };
useAuthStore.setState({ user: { userId: "teacher", centerId: "center", centerName: "Center", username: "teacher", displayName: "Teacher", accountType: "Teacher", role: "Teacher", roles: [], authorizationVersion: 1, permissions: Object.values(permissions) }, sessionStatus: "authenticated", accessToken: "fixture" });
function Fixture() {
  const { theme } = useThemeMode();
  const [revision, setRevision] = useState(0);
  const [scope, setScope] = useState("draft12");
  const [grade, setGrade] = useState(12);
  const [include, setInclude] = useState<{ kind: "node"; id: string; nonce: number } | null>(null);
  const reset = (cycle = false, readonly = false) => {
    client.clear(); state.writes = []; state.fail = ""; state.readOnly = readonly;
    version = { graphVersionId: "draft12", subjectId: "math", createdBy: "teacher", gradeLevel: 12, title: "Toán siêu cấp", status: readonly ? "Published" : "Draft", isCurrent: readonly, rowVersion: "1", nodeMappings: ["1", "2", "3", "4"].map(id => ({ nodeId: id, parentNodeId: cycle && id === "1" ? "2" : cycle && id === "2" ? "1" : null })), edgeMappings: [{ edgeId: "10", relationType: "RelatedTo", weight: 0 }, { edgeId: "11", relationType: "PrerequisiteOf", weight: 1 }] };
    setScope("draft12"); setGrade(12); setInclude(null); setRevision(n => n + 1);
  };
  Object.assign(window, { kgFixture: state, kgReset: reset, kgRefetch: () => client.invalidateQueries(), kgAddLibrary: () => { const id = String(topics.length + 1); topics.push(topic(id, "Chủ đề mới chưa phân khối")); setInclude({ kind: "node", id, nonce: Date.now() }); void client.invalidateQueries(); } });
  if (!version) { version = { graphVersionId: "draft12", subjectId: "math", createdBy: "teacher", gradeLevel: 12, title: "Toán siêu cấp", status: "Draft", isCurrent: false, rowVersion: "1", nodeMappings: ["1", "2", "3", "4"].map(id => ({ nodeId: id, parentNodeId: null })), edgeMappings: [{ edgeId: "10", relationType: "RelatedTo", weight: 0 }, { edgeId: "11", relationType: "PrerequisiteOf", weight: 1 }] }; }
  return <main data-actor="teacher" data-theme={theme} style={{ padding: 20, minHeight: "100vh", background: "var(--th-bg)" }}>{new URLSearchParams(location.search).has("full") ? <TeacherKnowledgeGraphView /> : <KnowledgeVersionPanel key={revision + scope} subjectId="math" subjectCode="MATH" subjectName="Toán" gradeLevel={grade} versionId={scope || undefined} canEdit={!state.readOnly} readGraph={{ subjectId: "math", nodes: topics.filter(n => version.nodeMappings?.some(m => m.nodeId === n.nodeId)), edges: edges.filter(r => version.edgeMappings?.some(m => m.edgeId === r.edgeId)).map(r => ({ ...r, ...version.edgeMappings?.find(m => m.edgeId === r.edgeId) })) }} includeRequest={include} onSelect={(id, g) => { state.scope = id; setScope(id); setGrade(g); }} onAddTopic={() => (window as unknown as { kgAddLibrary: () => void }).kgAddLibrary()} />}</main>;
}
createRoot(document.getElementById("root")!).render(<QueryClientProvider client={client}><BrowserRouter><Fixture /></BrowserRouter></QueryClientProvider>);
