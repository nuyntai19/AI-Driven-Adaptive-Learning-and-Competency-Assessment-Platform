import type { RubricGrade } from "../../types/reviews";

export function RubricGradeView({ grade }: { grade: RubricGrade }) {
  const display = (score: number) => Number((score / grade.maxScore * 10).toFixed(2));
  return <section className="rounded-xl border border-slate-200 dark:border-slate-700 p-4 space-y-3">
    <h3 className="font-bold text-sm">Điểm theo tiêu chí của giáo viên (quy đổi thang 10)</h3>
    <ul className="space-y-3">{grade.criteria.map(c => <li key={c.criterionId} className="text-sm">
      <div className="flex justify-between gap-4"><span className="font-semibold">{c.title}</span><strong>{display(c.awardedScore)} / {display(c.maxScore)}</strong></div>
      {c.comment && <p className="text-slate-600 dark:text-slate-300 whitespace-pre-wrap mt-1">{c.comment}</p>}
    </li>)}</ul>
    <p className="border-t border-slate-200 dark:border-slate-700 pt-2 font-bold text-sm">Tổng: {display(grade.awardedScore)} / 10</p>
  </section>;
}
