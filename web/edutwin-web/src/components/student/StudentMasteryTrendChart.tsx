import React, { useMemo, useState } from "react";
import { ResponsiveContainer, LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip } from "recharts";
import type { TwinUpdateHistoryItemDto } from "../../types/digitalTwin";
import { buildMasteryTrend } from "../../utils/studentMasteryTrend";

export interface StudentMasteryTrendChartProps { history: TwinUpdateHistoryItemDto[] }
const percent = (value: number) => value.toLocaleString("vi-VN", { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const fullDate = (value: string) => new Date(value).toLocaleString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
type TrendRow = ReturnType<typeof buildMasteryTrend>["rows"][number];
const COLORS = ["#ea580c", "#0284c7", "#059669", "#8b5cf6", "#db2777", "#ca8a04", "#0891b2", "#6366f1"];
const topicColor = (index: number) => COLORS[index] ?? `hsl(${(index * 137.5) % 360} 65% 45%)`;

function TrendTooltip({ active, payload }: { active?: boolean; payload?: { payload: TrendRow; name?: string; value?: number; color?: string }[] }) {
  const row = payload?.[0]?.payload;
  if (!active || !row) return null;
  return <div className="max-w-64 rounded-2xl border border-[var(--student-border)] bg-[var(--student-surface)] p-4 text-xs shadow-[var(--student-shadow-lg)]">
    <p className="font-semibold text-[var(--student-muted)]">{row.event ? fullDate(row.event.recordedAt) : "Trước lần ghi nhận đầu tiên"}</p>
    <div className="mt-3 space-y-2">{payload?.map(item => <p key={item.name} className="flex items-center justify-between gap-5"><span className="flex items-center gap-2"><span className="h-2 w-2 shrink-0 rounded-full" style={{ background: item.color }} />{item.name}</span><b>{percent(Number(item.value))}%</b></p>)}</div>
    {row.event && <><p className="mt-3 border-t border-[var(--student-border)] pt-2 font-semibold">{row.event.topicName}</p><p className="mt-1 text-[var(--student-muted)]">{percent(row.event.previousMastery)}% → {percent(row.event.newMastery)}% · {row.event.contextLabel}</p></>}
  </div>;
}

export const StudentMasteryTrendChart: React.FC<StudentMasteryTrendChartProps> = ({ history }) => {
  const [hidden, setHidden] = useState<string[]>([]);
  const [showAverage, setShowAverage] = useState(false);
  const { topics, rows, current: average, delta: averageDelta } = useMemo(() => buildMasteryTrend(history), [history]);
  const visible = topics.filter(topic => !hidden.includes(topic.id));
  const focused = visible.length === 1 && !showAverage ? visible[0] : undefined;
  const current = focused?.current ?? average;
  const delta = focused ? focused.current - focused.initial : averageDelta;
  const peak = rows.reduce((max, row) => Math.max(max, ...visible.map(topic => row.values[topic.id]), showAverage ? row.value : 0), 0);
  const upper = Math.min(100, Math.max(20, Math.ceil((peak + 5) / 10) * 10));
  const ticks = Array.from({ length: 5 }, (_, index) => upper * index / 4);
  const lastIndex = rows.length - 1;
  const axisTicks = [...new Set([0, Math.round(lastIndex / 3), Math.round(lastIndex * 2 / 3), lastIndex])];
  const isFlat = rows.every(row => focused ? row.values[focused.id] === focused.initial : row.value === rows[0].value);
  const tone = delta < 0 ? "text-rose-700 dark:text-rose-400" : "text-emerald-700 dark:text-emerald-400";
  const toggleTopic = (id: string) => setHidden(previous => previous.includes(id)
    ? previous.filter(value => value !== id)
    : visible.length === 1 && !showAverage ? previous : [...previous, id]);

  return <div data-testid="student-mastery-trend">
    <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
      <div><p className="st-eyebrow">Quá trình học tập</p><h2 id="trend-heading" className="mt-1 text-xl font-extrabold">Diễn biến năng lực</h2><p className="mt-1.5 text-sm text-[var(--student-muted)]">Nhìn lại sự thay đổi sau mỗi lần học và luyện tập.</p></div>
      <span className="self-start rounded-full border border-[var(--student-border)] px-3 py-1.5 text-xs font-semibold text-[var(--student-muted)]">{Math.max(0, lastIndex)} lần ghi nhận</span>
    </div>
    {topics.length === 0 ? <div data-testid="student-mastery-trend-empty" className="mt-5 rounded-2xl bg-[var(--student-surface-subtle)] px-6 py-12 text-center"><p className="font-bold">Chưa có dữ liệu năng lực</p><p className="mt-2 text-sm text-[var(--student-muted)]">Hoàn thành bài kiểm tra hoặc luyện tập để bắt đầu theo dõi tiến bộ.</p></div> : <>
      <div className="mt-5 flex flex-wrap gap-2" role="group" aria-label="Chọn nội dung biểu đồ">
        <button type="button" onClick={() => { setHidden([]); setShowAverage(false); }} className="rounded-full border border-[var(--student-border)] px-3 py-2 text-xs font-semibold text-[var(--student-muted)]">Hiện tất cả</button>
        {topics.map((topic, index) => {
          const active = !hidden.includes(topic.id);
          return <button key={topic.id} type="button" aria-pressed={active} onClick={() => toggleTopic(topic.id)} className={`inline-flex items-center gap-2 rounded-full border px-3.5 py-2 text-xs font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--student-brand)] ${active ? "border-[var(--student-border)] bg-[var(--student-surface-subtle)] text-[var(--student-ink)]" : "border-transparent text-[var(--student-muted)] opacity-50"}`}><span className="h-2.5 w-2.5 rounded-full" style={{ background: active ? topicColor(index) : "var(--student-muted)" }} />{topic.name}<span className="tabular-nums text-[var(--student-muted)]">{percent(topic.current)}%</span></button>;
        })}
        <button type="button" aria-pressed={showAverage} onClick={() => { if (showAverage && visible.length === 0) setHidden([]); setShowAverage(!showAverage); }} className={`rounded-full border px-3 py-2 text-xs font-semibold ${showAverage ? "border-[var(--student-ink)] text-[var(--student-ink)]" : "border-transparent text-[var(--student-muted)]"}`}>┄ Trung bình</button>
      </div>
      <div className="mt-6 overflow-hidden rounded-2xl border border-[var(--student-border)] bg-[linear-gradient(155deg,var(--student-brand-soft),var(--student-surface)_45%)]">
        <div className="flex flex-wrap items-end justify-between gap-4 px-5 pt-5 sm:px-6">
          <div><p className="text-xs font-semibold text-[var(--student-ink-secondary)]">{focused?.name ?? `Trung bình ${topics.length} chuyên đề có dữ liệu`}</p><div className="mt-2 flex flex-wrap items-center gap-3"><p className="text-4xl font-extrabold tracking-tight tabular-nums sm:text-5xl">{percent(current)}<span className="ml-1 text-lg font-semibold text-[var(--student-muted)]">%</span></p><span className={`rounded-full bg-[var(--student-surface)] px-2.5 py-1 text-xs font-bold ${delta === 0 ? "text-[var(--student-muted)]" : tone}`}>{delta > 0 ? "↗ +" : delta < 0 ? "↘ " : "→ "}{percent(delta)} điểm %</span></div></div>
          <p className="max-w-48 text-xs leading-5 text-[var(--student-muted)]">{isFlat ? "Năng lực chưa thay đổi trong các lần ghi nhận này." : delta > 0 ? "Đã tiến bộ so với trước lần ghi nhận đầu tiên." : delta < 0 ? "Đang thấp hơn mức trước lần ghi nhận đầu tiên." : "Đã trở về mức trước lần ghi nhận đầu tiên."}</p>
        </div>
        <div className="mt-5 h-56 min-w-0 w-full pr-3 sm:h-64 sm:pr-5" role="img" aria-label={`Diễn biến năng lực của ${visible.map(topic => topic.name).join(", ")}, qua ${lastIndex} lần ghi nhận.`}>
          <ResponsiveContainer width="100%" height="100%" minWidth={0}>
            <LineChart data={rows} margin={{ top: 16, right: 18, left: 0, bottom: 12 }} accessibilityLayer>
              <CartesianGrid vertical={false} stroke="var(--student-border)" strokeDasharray="3 6" />
              <XAxis dataKey="index" type="number" domain={[0, lastIndex]} ticks={axisTicks} tickFormatter={(index: number) => index === 0 ? "Trước đó" : `Lượt ${index}`} tickLine={false} axisLine={false} tick={{ fill: "var(--student-muted)", fontSize: 11 }} dy={10} minTickGap={30} />
              <YAxis domain={[0, upper]} ticks={ticks} tickFormatter={(value: number) => `${value}%`} tickLine={false} axisLine={false} tick={{ fill: "var(--student-muted)", fontSize: 11 }} width={52} />
              <Tooltip content={<TrendTooltip />} cursor={{ stroke: "var(--student-brand)", strokeOpacity: 0.3, strokeDasharray: "4 4" }} />
              {topics.map((topic, index) => hidden.includes(topic.id) ? null : <Line key={topic.id} type="linear" dataKey={(row: TrendRow) => row.values[topic.id]} name={topic.name} stroke={topicColor(index)} strokeWidth={2.5} strokeDasharray={index % 3 === 1 ? "9 3" : index % 3 === 2 ? "3 3" : undefined} strokeLinejoin="round" strokeLinecap="round" isAnimationActive={false} dot={({ cx, cy, index: pointIndex, payload }) => <circle key={pointIndex} cx={cx} cy={cy} r={pointIndex === lastIndex ? 4 : payload?.event?.topicNodeId === topic.id ? 3 : 0} fill="var(--student-surface)" stroke={topicColor(index)} strokeWidth={2} />} activeDot={{ r: 5, fill: topicColor(index), stroke: "var(--student-surface)", strokeWidth: 2 }} />)}
              {showAverage && <Line type="linear" dataKey="value" name="Trung bình" stroke="var(--student-ink-secondary)" strokeWidth={2} strokeDasharray="6 5" dot={false} isAnimationActive={false} />}
            </LineChart>
          </ResponsiveContainer>
        </div>
        <div className="flex flex-wrap items-center justify-between gap-2 border-t border-[var(--student-border)] px-5 py-3 text-xs text-[var(--student-muted)] sm:px-6"><span>● <span className="ml-1">Sau mỗi lượt ghi nhận</span></span><span>Trục tung: 0–{upper}% · Thang năng lực: 0–100%</span></div>
      </div>
      <p className="mt-3 text-xs leading-5 text-[var(--student-muted)]">Mỗi màu là một chuyên đề. Nhấn tên để bật/tắt đường; các đường giữ mức gần nhất khi chuyên đề khác được cập nhật. Các lượt được giãn đều để dễ so sánh. Rê chuột hoặc chạm vào điểm để xem thời gian và kết quả.</p>
    </>}
  </div>;
};
export default StudentMasteryTrendChart;
