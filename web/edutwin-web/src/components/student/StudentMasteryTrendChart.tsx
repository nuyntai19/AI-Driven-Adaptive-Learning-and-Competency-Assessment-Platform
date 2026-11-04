import React, { useMemo } from "react";
import {
  ResponsiveContainer,
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
} from "recharts";
import type { TwinUpdateHistoryItemDto } from "../../types/digitalTwin";

export interface StudentMasteryTrendChartProps {
  history: TwinUpdateHistoryItemDto[];
}

const PALETTE = ["#0d9488", "#2563eb", "#d97706", "#dc2626", "#7c3aed", "#db2777", "#65a30d", "#0891b2", "#ea580c", "#4f46e5"];
const MAX_LINES = 8;

const shorten = (name: string) => (name.length > 24 ? `${name.slice(0, 23)}…` : name);
const fmtDay = (t: number) => new Date(t).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit" });
const fmtFull = (t: number) => new Date(t).toLocaleString("vi-VN", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });

interface TopicSeries { id: string; name: string; events: { t: number; v: number }[] }

/**
 * Stock-style multi-line trend: one line per topic, a point at every recorded mastery
 * change, last value carried forward between changes. Legend sits under the chart.
 */
export const StudentMasteryTrendChart: React.FC<StudentMasteryTrendChartProps> = ({ history }) => {
  const { rows, topics, hiddenCount } = useMemo(() => {
    const byTopic = new Map<string, TopicSeries>();
    for (const item of [...history].sort((a, b) => +new Date(a.recordedAt) - +new Date(b.recordedAt))) {
      const topic = byTopic.get(item.topicNodeId) ?? { id: item.topicNodeId, name: item.topicName, events: [] };
      topic.events.push({ t: +new Date(item.recordedAt), v: item.newMastery });
      byTopic.set(item.topicNodeId, topic);
    }
    const all = [...byTopic.values()].sort((a, b) =>
      b.events.length - a.events.length || b.events[b.events.length - 1].t - a.events[a.events.length - 1].t);
    const picked = all.slice(0, MAX_LINES);
    const times = [...new Set(picked.flatMap(t => t.events.map(e => e.t)))].sort((a, b) => a - b);
    const ptr = new Map<string, number>();
    const lastVal = new Map<string, number>();
    const rows = times.map(t => {
      const row: Record<string, number> = { t };
      for (const topic of picked) {
        let p = ptr.get(topic.id) ?? 0;
        while (p < topic.events.length && topic.events[p].t <= t) { lastVal.set(topic.id, topic.events[p].v); p++; }
        ptr.set(topic.id, p);
        const v = lastVal.get(topic.id);
        if (v !== undefined) row[topic.id] = v;
      }
      return row;
    });
    return { rows, topics: picked, hiddenCount: all.length - picked.length };
  }, [history]);

  if (history.length === 0) return (
    <p className="mt-4 text-sm text-stone-500 dark:text-stone-400" data-testid="student-mastery-trend-empty">
      Chưa có lần thay đổi năng lực nào được ghi nhận. Hãy làm bài kiểm tra đầu vào hoặc luyện tập để bắt đầu đường biểu đồ.
    </p>
  );

  return (
    <div className="mt-4" data-testid="student-mastery-trend">
      <div className="h-80 w-full min-w-0">
        <ResponsiveContainer width="100%" height="100%" minWidth={0}>
          <LineChart data={rows} margin={{ top: 8, right: 12, bottom: 0, left: -12 }}>
            <CartesianGrid stroke="#e5e7eb" className="dark:opacity-20" vertical={false} />
            <XAxis dataKey="t" type="number" domain={["dataMin", "dataMax"]} tickFormatter={fmtDay}
              tick={{ fill: "#6b7280", fontSize: 11 }} tickCount={6} />
            <YAxis domain={[0, 100]} tickFormatter={(v: number) => `${v}%`}
              tick={{ fill: "#6b7280", fontSize: 11 }} />
            <Tooltip labelFormatter={(t) => fmtFull(Number(t))}
              formatter={(value, name) => [`${Number(value).toFixed(0)}%`, name]}
              contentStyle={{ borderRadius: "12px", fontSize: "12px", border: "1px solid #e5e7eb", backgroundColor: "rgba(255, 255, 255, 0.95)" }} />
            {topics.map((topic, i) => (
              <Line key={topic.id} type="linear" dataKey={topic.id} name={topic.name}
                stroke={PALETTE[i % PALETTE.length]} strokeWidth={2}
                dot={rows.length < 3 ? { r: 2.5 } : false} activeDot={{ r: 3.5 }} connectNulls />
            ))}
            <Legend verticalAlign="bottom" height={40} formatter={(value: string) => shorten(value)}
              wrapperStyle={{ fontSize: "12px" }} />
          </LineChart>
        </ResponsiveContainer>
      </div>
      <p className="mt-1 text-xs text-stone-500 dark:text-stone-400">
        Mỗi đường là một chủ đề, mỗi điểm là một lần độ thành thạo thay đổi sau hoạt động học; giữa hai lần thay đổi đường giữ mức gần nhất.
        {hiddenCount > 0 ? ` Chỉ hiện ${MAX_LINES} chủ đề thay đổi nhiều nhất, còn ${hiddenCount} chủ đề khác.` : ""}
      </p>
    </div>
  );
};

export default StudentMasteryTrendChart;
