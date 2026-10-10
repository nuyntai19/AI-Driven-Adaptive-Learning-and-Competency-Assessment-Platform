// Subject IDs are GUIDs; knowledge node IDs are UInt64 strings, not JS numbers.
export function isSubjectId(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
    && value !== "00000000-0000-0000-0000-000000000000";
}

export function isKnowledgeNodeId(value: string): boolean {
  return /^[1-9]\d{0,19}$/.test(value)
    && (value.length < 20 || value <= "18446744073709551615");
}

export function sameSubject(left: string | undefined, right: string | undefined): boolean {
  return Boolean(left && right && left.toLowerCase() === right.toLowerCase());
}

export type KnowledgeTopicContext =
  | { kind: "none" }
  | { kind: "invalid"; message: string }
  | { kind: "topic"; subjectId: string; topicId: string };

export function parseKnowledgeTopicContext(
  params: URLSearchParams,
  fallbackSubjectId?: string,
): KnowledgeTopicContext {
  // A subject-only link is an existing, non-KG workflow.
  if (!params.has("topicNodeId")) return { kind: "none" };
  const topicId = params.get("topicNodeId") ?? "";
  const subjectId = params.get("subjectId") ?? fallbackSubjectId ?? "";
  if (params.getAll("topicNodeId").length !== 1 || params.getAll("subjectId").length > 1
    || !isKnowledgeNodeId(topicId) || !isSubjectId(subjectId)) {
    return { kind: "invalid", message: "Liên kết chủ đề không hợp lệ hoặc thiếu môn học. Chưa áp dụng bộ lọc." };
  }
  return { kind: "topic", subjectId: subjectId.toLowerCase(), topicId };
}

export function topicQuickActionSearch(subjectId: string, topicId: string): string | null {
  if (!isSubjectId(subjectId) || !isKnowledgeNodeId(topicId)) return null;
  return new URLSearchParams({ subjectId, topicNodeId: topicId }).toString();
}

interface ContextSubject { subjectId: string; subjectName: string; isActive: boolean }
interface ContextNode { nodeId: string; subjectId: string; nodeName: string; nodeType: string; isActive: boolean }

export interface TopicContextValidation {
  status: "none" | "pending" | "error" | "invalid" | "ready";
  message: string;
}

export function validateKnowledgeTopicContext(
  context: KnowledgeTopicContext,
  data: {
    canRead: boolean;
    isError?: boolean;
    subjects?: readonly ContextSubject[];
    nodes?: readonly ContextNode[];
  },
): TopicContextValidation {
  if (context.kind === "none") return { status: "none", message: "" };
  if (context.kind === "invalid") return { status: "invalid", message: context.message };
  if (!data.canRead) return { status: "error", message: "Bạn không có quyền xác minh môn học/chủ đề này. Chưa tải câu hỏi." };
  if (data.isError) return { status: "error", message: "Không thể xác minh chủ đề từ đồ thị. Chưa tải câu hỏi; vui lòng thử lại." };
  const subject = data.subjects?.find((item) => sameSubject(item.subjectId, context.subjectId));
  if (data.subjects && (!subject || !subject.isActive)) {
    return { status: "invalid", message: "Môn học trong liên kết không khả dụng. Chưa tải câu hỏi." };
  }
  const hint = `Môn: ${subject?.subjectName ?? context.subjectId} · Chủ đề #${context.topicId}`;
  if (!data.subjects || !data.nodes) return { status: "pending", message: `${hint} · Đang xác minh…` };
  const node = data.nodes.find((item) => item.nodeId === context.topicId && sameSubject(item.subjectId, context.subjectId));
  if (!node || node.nodeType !== "Topic" || !node.isActive) {
    return { status: "invalid", message: "Chủ đề không thuộc môn học này, không còn hoạt động hoặc không phải Topic. Chưa tải câu hỏi." };
  }
  return { status: "ready", message: `Đang lọc chủ đề: ${node.nodeName} · Môn: ${subject!.subjectName}` };
}

export function topicClassDisposition(contextSubjectId: string, classSubjectId?: string) {
  if (!classSubjectId) return "waiting";
  return sameSubject(contextSubjectId, classSubjectId) ? "same" : "mismatch";
}

export function withoutTopicContext(params: URLSearchParams): URLSearchParams {
  const next = new URLSearchParams(params);
  next.delete("topicNodeId");
  next.delete("subjectId");
  return next;
}
