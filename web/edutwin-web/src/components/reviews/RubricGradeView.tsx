import type { RubricGrade } from "../../types/reviews";
import { RichMathText } from "../math/RichMathText";

export function RubricGradeView({ grade }: { grade: RubricGrade }) {
  const display = (score: number) => Number((score / grade.maxScore * 10).toFixed(2));
  return <section className="rounded-xl border border-slate-200 dark:border-slate-700 p-4 space-y-3">
    <h3 className="font-bold text-sm">Điểm theo tiêu chí của giáo viên (quy đổi thang 10)</h3>
    <ul className="space-y-3">{grade.criteria.map(c => <li key={c.criterionId} className="text-sm">
      <div className="flex justify-between gap-4"><span className="font-semibold"><RichMathText text={c.title} /></span><strong>{display(c.awardedScore)} / {display(c.maxScore)}</strong></div>
      {c.comment && <p className="text-slate-600 dark:text-slate-300 whitespace-pre-wrap mt-1"><RichMathText text={c.comment} /></p>}
      {(grade.visualEvidence || []).filter(e => e.criterionId === c.criterionId).map(e => <p key={e.requirementIndex}
        className={`mt-1 text-xs ${e.status === 'Present' ? 'text-emerald-700 dark:text-emerald-300' : 'text-amber-800 dark:text-amber-300'}`}>
        <span className="font-semibold">{e.status === 'Present' ? 'Đã thấy' : e.status === 'Missing' ? 'Còn thiếu' : 'Chưa rõ'}{e.studentImageIndex ? ` · Ảnh nháp ${e.studentImageIndex}` : ''}: </span>
        <RichMathText text={e.observation} />
      </p>)}
    </li>)}</ul>
    <p className="border-t border-slate-200 dark:border-slate-700 pt-2 font-bold text-sm">Tổng: {display(grade.awardedScore)} / 10</p>
  </section>;
}
