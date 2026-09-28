import React from "react";
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
  data: Array<{ name: string; score: number; fullMark: number }>;
  themeColor: string;
}

export const StudentRadarChart: React.FC<StudentRadarChartProps> = ({ data, themeColor }) => {
  return (
    <div className="h-72 w-full min-w-0 mt-4 relative">
      <ResponsiveContainer width="100%" height="100%" minWidth={0}>
        <RadarChart data={data} outerRadius="68%">
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
