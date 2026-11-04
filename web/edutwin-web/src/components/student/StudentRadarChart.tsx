import React from "react";
import { groupCompetencies, type CompetencyInput } from "../../utils/competencyGroups";
import {
  ResponsiveContainer,
  RadarChart,
  PolarGrid,
  PolarAngleAxis,
  PolarRadiusAxis,
  Radar,
  Tooltip,
} from "recharts";

export interface StudentRadarChartProps {
  data: CompetencyInput[];
  themeColor: string;
}

const shorten = (name: string) => (name.length > 22 ? `${name.slice(0, 21)}…` : name);

export const StudentRadarChart: React.FC<StudentRadarChartProps> = ({ data, themeColor }) => {
  const groups = groupCompetencies(data);
  const assessedGroups = groups.filter(g => g.score !== null);
  // Prefer group axes when there are 3-8 assessed groups; otherwise fall back to assessed topics
  // so filtering to a single chapter still draws a radar instead of silently dropping to bars.
  let axes: { name: string; fullName: string; score: number; target: number }[] | null = null;
  let note: string | null = null;
  if (assessedGroups.length >= 3 && assessedGroups.length <= 8) {
    axes = assessedGroups.map(g => ({ name: shorten(g.name), fullName: g.name, score: g.score!, target: 75 }));
    const skipped = groups.length - assessedGroups.length;
    if (skipped > 0) note = `Đã bỏ qua ${skipped} nhóm chưa đánh giá — không coi là 0 điểm.`;
  } else {
    const topics = data.filter(d => (d.evidenceCount ?? 0) > 0);
    if (topics.length >= 3) {
      const picked = topics.length > 8 ? [...topics].sort((a, b) => a.score - b.score).slice(0, 8) : topics;
      axes = picked.map(t => ({ name: shorten(t.name), fullName: t.name, score: t.score, target: 75 }));
      if (topics.length > 8) note = "Radar hiện 8 chủ đề cần ưu tiên nhất (điểm thấp nhất).";
      const skipped = data.length - topics.length;
      if (skipped > 0) note = `${note ? `${note} ` : ""}Đã bỏ qua ${skipped} chủ đề chưa đánh giá — không coi là 0 điểm.`;
    }
  }
  if (!axes) return <div className="mt-4 space-y-4" data-testid="student-competency-summary">
    <p className="text-sm text-stone-500 dark:text-stone-400">Tổng hợp theo chương/nhóm. Radar chỉ dùng khi có ít nhất 3 nhóm hoặc chủ đề đã được đánh giá; các trường hợp khác dùng thanh tổng hợp để tránh chồng nhãn.</p>
    {groups.map(g => <div key={g.id} className="space-y-2">
      <div className="flex flex-wrap justify-between gap-2 text-sm"><span className="font-semibold">{g.name}</span><span>{g.score === null ? "Chưa đánh giá" : `${g.score.toFixed(1)}%`}</span></div>
      <div className="h-2 rounded-full bg-stone-100 dark:bg-slate-800"><div className="h-full rounded-full" style={{width:`${g.score ?? 0}%`,backgroundColor:themeColor}} /></div>
      <p className="text-xs text-stone-500 dark:text-stone-400">{g.assessedCount}/{g.topicCount} chủ đề có bằng chứng; chưa đánh giá không bị coi là làm sai.</p>
    </div>)}
  </div>;
  return (
    <div className="mt-4">
      <div className="h-72 w-full min-w-0 relative">
        <ResponsiveContainer width="100%" height="100%" minWidth={0}>
          <RadarChart data={axes} outerRadius="60%">
            <PolarGrid stroke="#e5e7eb" className="dark:opacity-20" />
            <PolarAngleAxis
              dataKey="name"
              tick={{ fill: "#6b7280", fontSize: 11, fontWeight: 600 }}
            />
            <PolarRadiusAxis angle={30} domain={[0, 100]} tick={false} stroke="#9ca3af" />
            <Radar
              name="Năng lực"
              dataKey="score"
              stroke={themeColor}
              fill={themeColor}
              fillOpacity={0.25}
            />
            <Radar
              name="Mục tiêu"
              dataKey="target"
              stroke="#9ca3af"
              strokeDasharray="4 4"
              fill="none"
            />
            <Tooltip
              contentStyle={{
                borderRadius: "12px",
                fontSize: "12px",
                border: "1px solid #e5e7eb",
                backgroundColor: "rgba(255, 255, 255, 0.95)",
              }}
            />
          </RadarChart>
        </ResponsiveContainer>
      </div>
      <p className="mt-2 text-xs text-stone-500 dark:text-stone-400">Đường nét đứt xám là vòng mục tiêu 75% (mức thành thạo).{note ? ` ${note}` : ""}</p>
    </div>
  );
};

export default StudentRadarChart;
