export const ATLAS_PAGE_SIZE = 6;
export interface CompetencyInput {
  name: string; score: number; fullMark: number; groupId?: string; groupName?: string;
  weight?: number; evidenceCount?: number;
}
export function groupCompetencies(items: CompetencyInput[]) {
  const groups = new Map<string, { name: string; items: CompetencyInput[] }>();
  for (const item of items) {
    const id = item.groupId || item.name;
    const group = groups.get(id) || { name: item.groupName || item.name, items: [] };
    group.items.push(item); groups.set(id, group);
  }
  return [...groups.entries()].map(([id, group]) => {
    const observed = group.items.filter(x => (x.evidenceCount ?? 0) > 0);
    const totalWeight = observed.reduce((s, x) => s + Math.max(0, x.weight ?? 1), 0);
    const score = observed.length === 0 ? null : totalWeight > 0
      ? observed.reduce((s, x) => s + x.score * Math.max(0, x.weight ?? 1), 0) / totalWeight
      : observed.reduce((s, x) => s + x.score, 0) / observed.length;
    return { id, name: group.name, score, fullMark: 100, topicCount: group.items.length, assessedCount: observed.length };
  });
}
export function wrapTopicLabel(name: string) {
  const lines: string[] = []; let line = "";
  for (const word of name.split(/\s+/)) {
    if (line && `${line} ${word}`.length > 22) { lines.push(line); line = word; } else line = line ? `${line} ${word}` : word;
  }
  if (line) lines.push(line);
  return lines.length > 3 ? [...lines.slice(0, 2), `${lines[2].slice(0, 19)}…`] : lines;
}
