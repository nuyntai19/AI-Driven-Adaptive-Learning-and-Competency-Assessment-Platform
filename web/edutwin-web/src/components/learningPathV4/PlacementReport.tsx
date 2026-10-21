import type { PlacementReportDto } from "../../types/learningPath";

const statusLabels: Record<string, string> = {
  Assessed: "Đã kiểm tra", Confirmed: "Đã có bằng chứng", InProgress: "Đang làm / chờ chấm",
  NotStarted: "Chưa kiểm tra", Unavailable: "Thiếu câu kiểm tra mới"
};

export function PlacementReport({ report, pending, onBuild, onContinue }: {
  report: PlacementReportDto; pending: boolean; onBuild: () => void; onContinue?: () => void;
}) {
  const topics = [...new Set(report.skills.map(s => s.topicName))];
  return <section className="space-y-5 rounded-2xl border border-indigo-200 bg-white p-5 dark:bg-slate-900" aria-labelledby="placement-report-title">
    <div className="flex flex-wrap items-start justify-between gap-4"><div>
      <p className="text-sm text-indigo-600">Đánh giá đầu vào · {report.assessedSkills}/{report.totalSkills} kỹ năng có kết quả</p>
      <h2 id="placement-report-title" className="mt-2 text-2xl font-semibold">{report.isComplete ? "Điểm xuất phát của em" : "Kết quả đang được bổ sung"}</h2>
      <p className="mt-2 max-w-2xl text-sm text-slate-500">Điểm dưới đây chỉ tính các phần đã kiểm tra xong trong lượt đầu vào này, gồm điểm thành phần và điều chỉnh của giáo viên. Phần đã có bằng chứng từ trước được ghi riêng; chưa làm không tính là sai.</p>
    </div>{report.scoreOutOfTen != null && <div className="rounded-xl bg-indigo-50 p-4 text-center text-indigo-950"><p className="text-2xl font-bold">{report.scoreOutOfTen}/10</p><p className="mt-1 text-xs">{report.isComplete ? "Các phần vừa kiểm tra" : "Điểm tạm · phần đã xong"}</p></div>}</div>
    {topics.map(topic => {
      const completed = report.skills.filter(s => s.topicName === topic && s.scoreOutOfTen != null);
      const possible = completed.reduce((sum, s) => sum + s.possibleScore, 0);
      const score = possible > 0 ? Math.round(100 * completed.reduce((sum, s) => sum + s.earnedScore, 0) / possible) / 10 : null;
      return <div key={topic}><div className="flex items-center justify-between gap-3"><h3 className="font-semibold">{topic}</h3>
        {score != null && <p className="text-sm font-medium">{score}/10 <span className="text-xs font-normal text-slate-500">· phần đã kiểm tra</span></p>}</div>
      <div className="mt-3 grid gap-3 sm:grid-cols-2">{report.skills.filter(s => s.topicName === topic).map(skill => <article key={skill.skillId} className="rounded-xl border border-slate-200 p-4 dark:border-slate-700">
        <div className="flex items-start justify-between gap-3"><h4 className="font-medium">{skill.skillName}</h4><span className="text-xs text-slate-500">{statusLabels[skill.status] ?? skill.status}</span></div>
        {skill.scoreOutOfTen != null && <p className="mt-2 font-semibold">{skill.scoreOutOfTen}/10 · đúng {skill.correctAnswers}/{skill.answeredQuestions} câu</p>}
        <p className="mt-2 text-sm leading-relaxed text-slate-600 dark:text-slate-300">{skill.recommendation}</p>
      </article>)}</div>
    </div>; })}
    <p className="text-xs text-slate-500">Số bộ luyện là ước lượng. Kết quả từng bộ sẽ điều chỉnh lượng luyện và độ khó; kiểm tra độc lập dùng để xác nhận đã nắm vững.</p>
    {onContinue && !report.isComplete && <button onClick={onContinue} className="mr-3 rounded-xl border border-indigo-300 px-4 py-3 text-sm font-semibold text-indigo-700">Tiếp tục kiểm tra</button>}
    <button disabled={pending} onClick={onBuild} className="rounded-xl bg-indigo-600 px-4 py-3 text-sm font-semibold text-white disabled:opacity-40">{report.isComplete ? "Xem lộ trình đề xuất từ kết quả này" : "Xem tuyến tạm từ dữ liệu hiện có"}</button>
    {pending && <p className="text-xs text-amber-700">Hoàn thành phần đang làm hoặc chờ chấm trước khi chuyển sang tuyến luyện.</p>}
  </section>;
}
