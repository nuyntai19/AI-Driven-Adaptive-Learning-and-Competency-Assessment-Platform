import type { AssignmentResultSummaryDto } from "../../types/assignments";

export function AssignmentKnowledgeScope({ summary, completed }: { summary?: AssignmentResultSummaryDto | null; completed: boolean }) {
  const areas = summary?.knowledgeAreas ?? [];
  return <div className="space-y-2 pt-1 text-xs">
    <div className="flex flex-wrap items-center gap-1.5" aria-label="Phạm vi kiến thức">
      <span className="font-medium text-stone-500 dark:text-stone-400">Nội dung luyện:</span>
      {areas.length ? areas.slice(0, 3).map(area => <span key={area.skillId} className="rounded-md border border-indigo-100 bg-indigo-50 px-2 py-1 text-indigo-800 dark:border-indigo-900 dark:bg-indigo-950/40 dark:text-indigo-200">
        {area.topicName !== area.skillName ? `${area.topicName} → ` : ""}{area.skillName}
      </span>) : <span className="text-stone-500 dark:text-stone-400">Chưa xác định phạm vi kiến thức</span>}
      {areas.length > 3 && <span className="text-stone-500">+{areas.length - 3} kỹ năng</span>}
      {summary?.estimatedMinutes != null && <span className="text-stone-500">· khoảng {summary.estimatedMinutes} phút</span>}
    </div>
    {areas.length > 0 && <details className="text-stone-600 dark:text-stone-300">
      <summary className="cursor-pointer font-medium text-indigo-700 dark:text-indigo-300">{completed ? "Xem kết quả theo kỹ năng" : "Xem phân bố câu hỏi"}</summary>
      <ul className="mt-2 space-y-2 rounded-lg border border-stone-200 bg-white p-3 dark:border-stone-700 dark:bg-slate-900">{areas.map(area => <li key={area.skillId} className="flex flex-wrap justify-between gap-x-4 gap-y-1">
        <span>{area.topicName !== area.skillName ? `${area.topicName} → ` : ""}{area.skillName}</span>
        <span>{area.questionCount} câu{completed && (area.evaluatedQuestionCount > 0
          ? ` · đúng ${area.correctQuestionCount}/${area.evaluatedQuestionCount} câu đã chấm${area.evaluatedQuestionCount < area.questionCount ? " · còn chờ chấm" : ""}`
          : " · chưa có kết quả")}</span>
      </li>)}</ul>
      {completed && summary?.teacherFinalReviewStatus !== "Approved" && <p className="mt-2 text-stone-500">Kết quả theo kỹ năng là tạm thời cho tới khi giáo viên duyệt.</p>}
    </details>}
    {!!summary?.unclassifiedQuestionCount && <p className="text-amber-700 dark:text-amber-300">{summary.unclassifiedQuestionCount} câu chưa xác định kỹ năng; cần giáo viên bổ sung thông tin.</p>}
    {!!summary?.voidedQuestionCount && <p className="text-stone-500">{summary.voidedQuestionCount} câu đã hủy không dùng để đánh giá kỹ năng.</p>}
  </div>;
}
