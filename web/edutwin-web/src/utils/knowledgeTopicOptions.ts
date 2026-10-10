import type { KnowledgeGraphNodeDto } from "../types/knowledgeGraph.ts";

export type TopicOptionNode = Pick<KnowledgeGraphNodeDto, "nodeId" | "nodeName" | "nodeCode" | "nodeType" | "parentNodeId" | "isActive" | "orderIndex">;
export interface TopicOptionGroup { id: string; name: string; topics: TopicOptionNode[] }
export function normalizeTopicSearch(value: string) {
  return value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/đ/g, "d").replace(/Đ/g, "D").toLowerCase().trim();
}
export function knowledgeTopicGroups(nodes: readonly TopicOptionNode[], query = "", groupId = ""): TopicOptionGroup[] {
  const map = new Map(nodes.map(n => [n.nodeId, n]));
  const groups = new Map<string, TopicOptionGroup>();
  const search = normalizeTopicSearch(query);
  for (const topic of nodes) {
    if (topic.nodeType !== "Topic" || !topic.isActive) continue;
    let parent = topic.parentNodeId ? map.get(topic.parentNodeId) : undefined;
    const seen = new Set([topic.nodeId]);
    while (parent && parent.nodeType !== "Chapter" && !seen.has(parent.nodeId)) {
      seen.add(parent.nodeId); parent = parent.parentNodeId ? map.get(parent.parentNodeId) : undefined;
    }
    const chapter = parent?.nodeType === "Chapter" ? parent : undefined;
    const id = chapter?.nodeId || "ungrouped", name = chapter?.nodeName || "Chủ đề chung / chưa phân nhóm";
    if (groupId && id !== groupId) continue;
    if (search && !normalizeTopicSearch(`${topic.nodeName} ${topic.nodeCode} ${name}`).includes(search)) continue;
    if (!groups.has(id)) groups.set(id, { id, name, topics: [] });
    groups.get(id)!.topics.push(topic);
  }
  return [...groups.values()].sort((a, b) => (map.get(a.id)?.orderIndex ?? -1) - (map.get(b.id)?.orderIndex ?? -1) || a.name.localeCompare(b.name, "vi"))
    .map(group => ({ ...group, topics: group.topics.sort((a, b) => a.orderIndex - b.orderIndex || a.nodeName.localeCompare(b.nodeName, "vi")) }));
}
