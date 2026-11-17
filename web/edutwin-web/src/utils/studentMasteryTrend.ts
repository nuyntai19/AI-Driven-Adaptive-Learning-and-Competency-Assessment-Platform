import type { TwinUpdateHistoryItemDto } from "../types/digitalTwin";

export function buildMasteryTrend(history: TwinUpdateHistoryItemDto[], selected: string | null = null) {
  const events = history.filter(event => Number.isFinite(Date.parse(event.recordedAt))
    && Number.isFinite(event.previousMastery) && Number.isFinite(event.newMastery))
    .sort((a, b) => Date.parse(a.recordedAt) - Date.parse(b.recordedAt)
      || a.historyId.localeCompare(b.historyId, "en", { numeric: true }));
  const topics = new Map<string, { id: string; name: string; initial: number; current: number; count: number }>();
  for (const event of events) {
    const topic = topics.get(event.topicNodeId) ?? {
      id: event.topicNodeId, name: event.topicName, initial: event.previousMastery, current: event.previousMastery, count: 0,
    };
    topic.current = event.newMastery;
    topic.count++;
    topics.set(topic.id, topic);
  }
  const focused = selected ? topics.get(selected) : undefined;
  const included = focused ? [focused] : [...topics.values()];
  const values = new Map(included.map(topic => [topic.id, topic.initial]));
  const average = () => values.size ? [...values.values()].reduce((sum, value) => sum + value, 0) / values.size : 0;
  const rows: { index: number; value: number; values: Record<string, number>; event: TwinUpdateHistoryItemDto | null }[] = [
    { index: 0, value: average(), values: Object.fromEntries(values), event: null },
  ];
  for (const event of events) {
    if (!values.has(event.topicNodeId)) continue;
    values.set(event.topicNodeId, event.newMastery);
    rows.push({ index: rows.length, value: average(), values: Object.fromEntries(values), event });
  }
  const current = rows[rows.length - 1].value;
  return { topics: [...topics.values()], focused, rows, current, delta: current - rows[0].value };
}
