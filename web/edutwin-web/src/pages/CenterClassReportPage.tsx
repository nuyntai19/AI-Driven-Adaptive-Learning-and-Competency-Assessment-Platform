import { useQuery } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { httpClient } from "../api/httpClient";
import { useAuthStore } from "../stores/authStore";
import { SafeErrorPanel, Skeleton } from "../components/centerManager";
import { ClassHistoryPanel } from "../components/ClassHistoryPanel";
import type { ClassDto, StudentDto } from "../types/organization";
import { assessmentLabel, reportScoreText, type StudentAcademicSummary } from "./teacher/teacherReportsHelpers";

interface ClassReport {
  class: ClassDto;
  generatedAt: string;
  totalAssignments: number;
  students: { student: StudentDto; summary: StudentAcademicSummary }[];
}

/** Governance report only: never mounts teacher authoring/grading controls. */
export function CenterClassReportPage() {
  const { classId } = useParams<{ classId: string }>();
  const actor = useAuthStore(state => state.user);
  const query = useQuery({
    queryKey: ["class-academic-report", actor?.centerId, actor?.userId, classId],
    queryFn: async () => (await httpClient.get<{ data: ClassReport }>(`/classes/${classId}/academic-report`)).data.data,
    enabled: Boolean(classId && actor?.accountType === "CenterManager"),
  });
  if (query.isPending) return <div className="p-4 sm:p-6 lg:p-8"><Skeleton className="h-80 w-full" /></div>;
  if (query.isError) return <div className="p-4 sm:p-6 lg:p-8"><SafeErrorPanel error={query.error} onRetry={() => void query.refetch()} /></div>;
  const report = query.data;
  if (!report) return null;
  return <div className="space-y-6 min-w-0 w-full max-w-[96rem] mx-auto p-4 sm:p-6 lg:p-8" data-testid="center-class-report-readonly">
    <Link to="/quan-ly/lop-hoc" className="cm-button">← Danh sách lớp</Link>
    <header>
      <h1 className="text-2xl font-bold">Báo cáo lớp: {report.class.className}</h1>
      <p>{report.class.subject.subjectName} · {report.class.academicYear} · {report.class.status === "Archived" ? "Đã lưu trữ — chỉ xem" : "Đang hoạt động"}</p>
      <p className="text-sm text-slate-500">{report.students.length} học sinh trong phạm vi báo cáo · {report.totalAssignments} bài tập · Điểm thang 10. Điểm trung bình chỉ tính kết quả đã chốt.</p>
    </header>
    <div className="overflow-x-auto rounded-xl border border-slate-300 dark:border-slate-700">
      <table className="w-full text-left text-sm">
        <thead><tr>{["Học sinh", "Bài đã giao", "Đã nộp", "Quá hạn", "Điểm TB đã chốt", "Đánh giá"].map(label => <th key={label} className="p-3 whitespace-nowrap">{label}</th>)}</tr></thead>
        <tbody>{report.students.map(({ student, summary }) => <tr key={student.studentId} className="border-t border-slate-300 dark:border-slate-700">
          <td className="p-3">{student.fullName}</td><td className="p-3">{summary.totalAssigned}</td>
          <td className="p-3">{summary.completedCount}</td><td className="p-3">{summary.overdueCount}</td>
          <td className="p-3">{summary.averageScore == null ? "Chưa có" : `${summary.averageScore.toFixed(1)} / 10`}</td>
          <td className="p-3">{assessmentLabel(summary)}</td>
        </tr>)}</tbody>
      </table>
      {report.students.length === 0 && <p className="p-4">Chưa có học sinh hoặc bài làm trong phạm vi báo cáo.</p>}
    </div>
    {report.students.map(({ student, summary }) => <details key={student.studentId} className="rounded-xl border border-slate-300 dark:border-slate-700 p-4">
      <summary className="cursor-pointer font-semibold">Bài làm của {student.fullName}</summary>
      <ul className="mt-3 space-y-2">{summary.records.map(record => <li key={record.assignmentId}>
        {record.title}: {record.completedQuestionCount}/{record.totalQuestionCount} câu · {reportScoreText(record)}
      </li>)}</ul>
      {summary.records.length === 0 && <p className="mt-3">Chưa được giao bài tập.</p>}
    </details>)}
    <ClassHistoryPanel classId={classId!} />
  </div>;
}
