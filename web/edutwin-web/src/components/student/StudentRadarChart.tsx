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

export const StudentRadarChart: React.FC<StudentRadarChartProps> = ({ data, themeColor }) => {
  const groups = groupCompetencies(data);
  const canUseRadar = groups.length >= 3 && groups.length <= 8 && groups.every(g => g.score !== null);
  if (!canUseRadar) return <div className="mt-4 space-y-4" data-testid="student-competency-summary">
    <p className="text-sm text-stone-500 dark:text-stone-400">Tổng hợp theo chương/nhóm. Radar chỉ dùng khi có 3–8 nhóm đã được đánh giá; các trường hợp khác dùng thanh tổng hợp để tránh chồng nhãn.</p>
    {groups.map(g => <div key={g.id} className="space-y-2">
      <div className="flex flex-wrap justify-between gap-2 text-sm"><span className="font-semibold">{g.name}</span><span>{g.score === null ? "Chưa đánh giá" : `${g.score.toFixed(1)}%`}</span></div>
      <div className="h-2 rounded-full bg-stone-100 dark:bg-slate-800"><div className="h-full rounded-full" style={{width:`${g.score ?? 0}%`,backgroundColor:themeColor}} /></div>
      <p className="text-xs text-stone-500 dark:text-stone-400">{g.assessedCount}/{g.topicCount} chủ đề có bằng chứng; chưa đánh giá không bị coi là làm sai.</p>
    </div>)}
  </div>;
  return (
    <div className="h-72 w-full min-w-0 mt-4 relative">
      <ResponsiveContainer width="100%" height="100%" minWidth={0}>
        <RadarChart data={groups.map(g => ({...g, name: g.name.length > 22 ? `${g.name.slice(0, 21)}…` : g.name, fullName:g.name}))} outerRadius="60%">
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
  );
};

export default StudentRadarChart;
