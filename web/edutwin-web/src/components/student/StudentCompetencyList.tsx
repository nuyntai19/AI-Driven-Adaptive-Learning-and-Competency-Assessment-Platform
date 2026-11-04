import React from "react";
import { groupCompetencies, type CompetencyInput } from "../../utils/competencyGroups";

export interface StudentCompetencyListProps {
  data: CompetencyInput[];
  themeColor: string;
}

type Status = { label: string; chip: string };

function statusOf(item: CompetencyInput): Status {
  if ((item.evidenceCount ?? 0) === 0) return { label: "Chưa đánh giá", chip: "bg-stone-100 text-stone-500 dark:bg-slate-800 dark:text-stone-400" };
  if (item.score >= 75) return { label: "Thành thạo", chip: "bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300" };
  if (item.score >= 50) return { label: "Đang phát triển", chip: "bg-indigo-50 text-indigo-700 dark:bg-indigo-950 dark:text-indigo-300" };
  return { label: "Cần rèn luyện", chip: "bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-300" };
}

/** Khan-style per-topic mastery bars grouped by chapter: readable at any topic count, weakest first-class. */
export const StudentCompetencyList: React.FC<StudentCompetencyListProps> = ({ data, themeColor }) => {
  const groups = groupCompetencies(data);
  const groupById = new Map(groups.map(g => [g.id, g]));
  const topicsByGroup = new Map<string, { name: string; items: CompetencyInput[] }>();
  for (const item of data) {
    const id = item.groupId || item.name;
    const group = topicsByGroup.get(id) ?? { name: item.groupName || item.name, items: [] };
    group.items.push(item);
    topicsByGroup.set(id, group);
  }
  const assessed = data.filter(d => (d.evidenceCount ?? 0) > 0);
  const mastered = assessed.filter(d => d.score >= 75);
  const weakest = assessed.length ? assessed.reduce((min, d) => (d.score < min.score ? d : min)) : null;

  return (
    <div className="mt-4 space-y-6" data-testid="student-competency-list">
      <div className="flex flex-wrap gap-x-5 gap-y-1 text-sm text-stone-600 dark:text-stone-300">
        <span><strong className="text-stone-900 dark:text-stone-100">{data.length}</strong> chủ đề</span>
        <span><strong className="text-stone-900 dark:text-stone-100">{assessed.length}</strong> đã có bằng chứng</span>
        <span><strong className="text-stone-900 dark:text-stone-100">{mastered.length}</strong> đã thành thạo (≥75%)</span>
        {weakest && <span className="text-amber-700 dark:text-amber-300">Cần ưu tiên: <strong>{weakest.name}</strong> ({weakest.score.toFixed(0)}%)</span>}
        {!assessed.length && <span>Chưa có chủ đề nào có bằng chứng — hãy làm bài kiểm tra đầu vào hoặc luyện tập để có đánh giá.</span>}
      </div>

      {[...topicsByGroup.entries()].map(([id, group]) => {
        const summary = groupById.get(id);
        return (
          <section key={id} className="space-y-3">
            <div>
              <div className="flex flex-wrap justify-between gap-2 text-sm">
                <span className="font-bold text-stone-900 dark:text-stone-100">{group.name}</span>
                <span className="text-stone-500 dark:text-stone-400">{summary ? `${summary.assessedCount}/${summary.topicCount} chủ đề có bằng chứng` : ""}{summary?.score != null ? ` · trung bình ${summary.score.toFixed(0)}%` : ""}</span>
              </div>
              <div className="mt-1.5 h-2 rounded-full bg-stone-100 dark:bg-slate-800">
                <div className="h-full rounded-full" style={{ width: `${summary?.score ?? 0}%`, backgroundColor: themeColor }} />
              </div>
            </div>
            <div className="space-y-2">
              {group.items.map(item => {
                const status = statusOf(item);
                const hasEvidence = (item.evidenceCount ?? 0) > 0;
                const isWeakest = weakest != null && item === weakest;
                return (
                  <div key={item.name} className={`rounded-lg border border-stone-100 dark:border-stone-800 px-3 py-2 ${isWeakest ? "ring-1 ring-amber-300 dark:ring-amber-700" : ""}`}>
                    <div className="flex flex-wrap items-center justify-between gap-2 text-sm">
                      <span className="font-medium text-stone-800 dark:text-stone-200">
                        {item.name}
                        {isWeakest && <span className="ml-2 rounded-full bg-amber-100 px-2 py-0.5 text-[11px] font-semibold text-amber-800 dark:bg-amber-950 dark:text-amber-300">Yếu nhất</span>}
                      </span>
                      <span className="flex items-center gap-2">
                        <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${status.chip}`}>{status.label}</span>
                        <span className="font-semibold text-stone-900 dark:text-stone-100">{hasEvidence ? `${item.score.toFixed(0)}%` : "—"}</span>
                      </span>
                    </div>
                    <div className="mt-1.5 h-1.5 rounded-full bg-stone-100 dark:bg-slate-800">
                      <div className="h-full rounded-full" style={{ width: `${hasEvidence ? item.score : 0}%`, backgroundColor: themeColor }} />
                    </div>
                    {item.evidenceCount != null && item.evidenceCount > 0 && <p className="mt-1 text-xs text-stone-500 dark:text-stone-400">{item.evidenceCount} bằng chứng</p>}
                  </div>
                );
              })}
            </div>
          </section>
        );
      })}
    </div>
  );
};

export default StudentCompetencyList;
