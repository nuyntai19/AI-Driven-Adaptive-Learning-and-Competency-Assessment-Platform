import * as XLSX from "xlsx";
import type { StudentDto, StudentSubjectGoalDto, ClassDto } from "../../types/organization";
import type { ProgressStatus } from "../../types/assignments";

export interface StudentAssignmentRecord {
  assignmentId: string;
  title: string;
  subjectId?: string | null;
  subjectName?: string;
  dueAt: string | null;
  status: ProgressStatus;
  completedQuestionCount: number;
  totalQuestionCount: number;
  score?: number | null;
  maxScore?: number;
  feedbackNote?: string | null;
  submittedAt?: string | null;
}

export interface StudentAcademicSummary {
  totalAssigned: number;
  completedCount: number;
  inProgressCount: number;
  notStartedCount: number;
  overdueCount: number;
  completionRate: number; // 0 - 100
  averageScore: number | null; // 0 - 10 scale
  minScore: number | null;
  maxScore: number | null;
  records: StudentAssignmentRecord[];
}

export interface ClassAcademicSummary {
  totalStudents: number;
  totalAssignments: number;
  averageCompletionRate: number;
  classAverageScore: number | null;
  highRiskCount: number;
  safeCount: number;
}

/**
 * Downloads an Excel file (.xls) with native HTML table structure, custom column widths,
 * borders, and colors so Microsoft Excel opens it with dedicated, clear rows and columns.
 */
export function downloadExcel(filename: string, htmlContent: string) {
  const fullHtml = `
<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns="http://www.w3.org/TR/REC-html40">
<head>
<meta http-equiv="Content-Type" content="text/html; charset=utf-8">
<!--[if gte mso 9]>
<xml>
 <x:ExcelWorkbook>
  <x:ExcelWorksheets>
   <x:ExcelWorksheet>
    <x:Name>Báo Cáo</x:Name>
    <x:WorksheetOptions>
     <x:DisplayGridlines/>
    </x:WorksheetOptions>
   </x:ExcelWorksheet>
  </x:ExcelWorksheets>
 </x:ExcelWorkbook>
</xml>
<![endif]-->
<style>
  body { font-family: "Segoe UI", Arial, Helvetica, sans-serif; font-size: 11pt; color: #1e293b; margin: 20px; }
  table { border-collapse: collapse; margin-bottom: 24px; width: 100%; }
  th { background-color: #0284c7; color: #ffffff; font-weight: bold; border: 1px solid #94a3b8; padding: 9px 12px; text-align: left; font-size: 10.5pt; }
  td { border: 1px solid #cbd5e1; padding: 7px 10px; font-size: 10pt; vertical-align: middle; }
  .title-header { font-size: 16pt; font-weight: bold; color: #0f172a; text-align: left; padding: 10px 0; }
  .meta-sub { font-size: 9.5pt; color: #64748b; margin-bottom: 16px; }
  .section-title { font-size: 11.5pt; font-weight: bold; color: #0369a1; background-color: #e0f2fe; border: 1px solid #bae6fd; padding: 8px 12px; }
  .label-cell { background-color: #f8fafc; font-weight: bold; color: #334155; width: 220px; }
  .value-cell { font-weight: 600; color: #0f172a; }
  .text-center { text-align: center; }
  .text-right { text-align: right; }
  .font-bold { font-weight: bold; }
  .badge-success { color: #15803d; font-weight: bold; }
  .badge-warning { color: #a16207; font-weight: bold; }
  .badge-danger { color: #b91c1c; font-weight: bold; }
  .badge-info { color: #0369a1; font-weight: bold; }
  .zebra-even { background-color: #f8fafc; }
</style>
</head>
<body>
${htmlContent}
</body>
</html>`;

  if (typeof document === "undefined") return;
  const blob = new Blob(["\uFEFF" + fullHtml], { type: "application/vnd.ms-excel;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.setAttribute("href", url);
  link.setAttribute("download", filename.endsWith(".xls") ? filename : `${filename}.xls`);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Downloads a CSV string as a file in browser with UTF-8 BOM for proper Vietnamese character display in Excel.
 */
export function downloadCsv(filename: string, rows: (string | number | boolean | null | undefined)[][]) {
  const csvContent = rows
    .map((row) =>
      row
        .map((cell) => {
          if (cell === null || cell === undefined) return '""';
          const stringValue = String(cell).replace(/"/g, '""');
          return `"${stringValue}"`;
        })
        .join(",")
    )
    .join("\r\n");

  if (typeof document === "undefined") return;
  // \uFEFFsep=,\r\n forces Microsoft Excel on Windows to parse commas into separate columns
  const blob = new Blob(["\uFEFFsep=,\r\n" + csvContent], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.setAttribute("href", url);
  link.setAttribute("download", filename.endsWith(".csv") ? filename : `${filename}.csv`);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Generates an Excel HTML string for an individual student report with formatted columns and rows.
 */
export function buildStudentReportExcelHtml(
  student: StudentDto,
  classInfo: ClassDto | undefined,
  goals: StudentSubjectGoalDto[],
  subjectsMap: Map<string, string>,
  summary: StudentAcademicSummary,
  teacherNote?: string
): string {
  const exportTime = new Date().toLocaleString("vi-VN");
  const avgScoreStr = summary.averageScore !== null ? summary.averageScore.toFixed(1) : "Chưa có điểm";
  const maxScoreStr = summary.maxScore !== null ? summary.maxScore.toFixed(1) : "-";
  const minScoreStr = summary.minScore !== null ? summary.minScore.toFixed(1) : "-";

  let html = `
  <div class="title-header">BÁO CÁO KẾT QUẢ HỌC TẬP VÀ ĐÁNH GIÁ NĂNG LỰC HỌC VIÊN</div>
  <div class="meta-sub">Thời gian xuất báo cáo: ${exportTime} | Hệ Thống Đào Tạo & Khảo Thí Thích Ứng EduTwin</div>

  <!-- Section 1: Thông tin học sinh -->
  <table>
    <colgroup>
      <col width="220">
      <col width="280">
      <col width="200">
      <col width="240">
    </colgroup>
    <thead>
      <tr>
        <th colspan="4" class="section-title">1. THÔNG TIN HỌC VIÊN & LỚP PHỤ TRÁCH</th>
      </tr>
      <tr>
        <th>Tiêu chí</th>
        <th>Thông tin</th>
        <th>Phân loại</th>
        <th>Ghi chú</th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td class="label-cell">Họ và tên học sinh</td>
        <td class="value-cell">${student.fullName}</td>
        <td>Học viên chính thức</td>
        <td>-</td>
      </tr>
      <tr class="zebra-even">
        <td class="label-cell">Tên đăng nhập (Username)</td>
        <td class="value-cell">@${student.username}</td>
        <td>Tài khoản học vụ</td>
        <td>-</td>
      </tr>
      <tr>
        <td class="label-cell">Khối lớp</td>
        <td class="value-cell">Khối ${student.gradeLevel}</td>
        <td>Bậc THPT</td>
        <td>-</td>
      </tr>
      <tr class="zebra-even">
        <td class="label-cell">Lớp học phụ trách</td>
        <td class="value-cell">${classInfo ? `${classInfo.className} (${classInfo.academicYear})` : "Tất cả lớp"}</td>
        <td>${classInfo?.subject?.subjectName || "Toàn bộ môn"}</td>
        <td>Giáo viên phụ trách</td>
      </tr>
    </tbody>
  </table>

  <!-- Section 2: Tổng hợp kết quả bài tập -->
  <table>
    <colgroup>
      <col width="220">
      <col width="280">
      <col width="200">
      <col width="240">
    </colgroup>
    <thead>
      <tr>
        <th colspan="4" class="section-title">2. TỔNG HỢP TIẾN ĐỘ & KẾT QUẢ BÀI TẬP</th>
      </tr>
      <tr>
        <th>Chỉ số học tập</th>
        <th>Giá trị thống kê</th>
        <th>Đơn vị tính</th>
        <th>Đánh giá tiến độ</th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td class="label-cell">Tổng số bài tập đã giao</td>
        <td class="value-cell text-center">${summary.totalAssigned}</td>
        <td class="text-center">Bài tập</td>
        <td>Toàn bộ niên khóa</td>
      </tr>
      <tr class="zebra-even">
        <td class="label-cell">Số bài đã nộp / hoàn thành</td>
        <td class="value-cell text-center">${summary.completedCount}</td>
        <td class="text-center">Bài tập</td>
        <td class="badge-success">Đã hoàn thành</td>
      </tr>
      <tr>
        <td class="label-cell">Số bài đang làm dở dang</td>
        <td class="value-cell text-center">${summary.inProgressCount}</td>
        <td class="text-center">Bài tập</td>
        <td class="badge-info">Đang thực hiện</td>
      </tr>
      <tr class="zebra-even">
        <td class="label-cell">Số bài chưa làm / quá hạn</td>
        <td class="value-cell text-center">${summary.notStartedCount + summary.overdueCount}</td>
        <td class="text-center">Bài tập</td>
        <td class="${summary.overdueCount > 0 ? "badge-danger" : ""}">${summary.overdueCount > 0 ? `Có ${summary.overdueCount} bài quá hạn` : "Chưa làm"}</td>
      </tr>
      <tr>
        <td class="label-cell">Tỷ lệ hoàn thành bài tập</td>
        <td class="value-cell text-center font-bold">${summary.completionRate.toFixed(1)}%</td>
        <td class="text-center">Phần trăm (%)</td>
        <td class="${summary.completionRate >= 80 ? "badge-success" : summary.completionRate >= 50 ? "badge-warning" : "badge-danger"}">
          ${summary.completionRate >= 80 ? "Đạt chỉ tiêu xuất sắc" : summary.completionRate >= 50 ? "Mức độ trung bình" : "Cần đôn đốc nhắc nhở"}
        </td>
      </tr>
      <tr class="zebra-even">
        <td class="label-cell">Điểm trung bình các bài tập</td>
        <td class="value-cell text-center font-bold">${avgScoreStr}</td>
        <td class="text-center">Thang điểm 10.0</td>
        <td>${summary.averageScore !== null && summary.averageScore >= 8.0 ? "Học lực Giỏi" : summary.averageScore !== null && summary.averageScore >= 6.5 ? "Học lực Khá" : "Cần củng cố"}</td>
      </tr>
      <tr>
        <td class="label-cell">Điểm cao nhất đạt được</td>
        <td class="value-cell text-center">${maxScoreStr}</td>
        <td class="text-center">Thang điểm 10.0</td>
        <td>-</td>
      </tr>
      <tr class="zebra-even">
        <td class="label-cell">Điểm thấp nhất</td>
        <td class="value-cell text-center">${minScoreStr}</td>
        <td class="text-center">Thang điểm 10.0</td>
        <td>-</td>
      </tr>
    </tbody>
  </table>

  <!-- Section 3: Mục tiêu môn học & Điểm dự báo AI -->
  <table>
    <colgroup>
      <col width="50">
      <col width="220">
      <col width="130">
      <col width="150">
      <col width="140">
      <col width="120">
      <col width="130">
    </colgroup>
    <thead>
      <tr>
        <th colspan="7" class="section-title">3. MỤC TIÊU ĐIỂM THI VÀ DỰ BÁO NĂNG LỰC (AI DIGITAL TWIN)</th>
      </tr>
      <tr>
        <th class="text-center">STT</th>
        <th>Môn học</th>
        <th class="text-center">Điểm mục tiêu</th>
        <th class="text-center">Dự báo hiện tại (AI)</th>
        <th class="text-center">Khoảng cách (Gap)</th>
        <th class="text-center">Thời gian còn lại</th>
        <th class="text-center">Đánh giá mức độ rủi ro</th>
      </tr>
    </thead>
    <tbody>`;

  if (goals.length === 0) {
    html += `
      <tr>
        <td colspan="7" class="text-center" style="color: #94a3b8; font-style: italic; padding: 14px;">Chưa thiết lập mục tiêu môn học nào</td>
      </tr>`;
  } else {
    goals.forEach((g, idx) => {
      const subjectName = subjectsMap.get(g.subjectId) || g.subjectId;
      const gap = (g.targetScore - g.currentPredictedScore).toFixed(1);
      const isHighRisk = g.riskScore > 0.6;
      const riskClass = isHighRisk ? "badge-danger" : g.riskScore > 0.3 ? "badge-warning" : "badge-success";
      const riskLabel = isHighRisk ? "Nguy cơ cao" : g.riskScore > 0.3 ? "Cần theo dõi" : "Tiến độ an toàn";

      html += `
      <tr class="${idx % 2 === 1 ? "zebra-even" : ""}">
        <td class="text-center">${idx + 1}</td>
        <td class="font-bold">${subjectName}</td>
        <td class="text-center font-bold">${g.targetScore.toFixed(1)}</td>
        <td class="text-center font-bold" style="color: #0284c7;">${g.currentPredictedScore.toFixed(1)}</td>
        <td class="text-center font-bold" style="color: ${Number(gap) > 0 ? "#dc2626" : "#16a34a"};">${Number(gap) > 0 ? `-${gap}` : `+${Math.abs(Number(gap)).toFixed(1)}`}</td>
        <td class="text-center">${g.remainingDays} ngày</td>
        <td class="text-center ${riskClass}">${riskLabel}</td>
      </tr>`;
    });
  }

  html += `
    </tbody>
  </table>

  <!-- Section 4: Danh sách chi tiết từng bài tập -->
  <table>
    <colgroup>
      <col width="45">
      <col width="260">
      <col width="140">
      <col width="110">
      <col width="120">
      <col width="110">
      <col width="100">
      <col width="180">
    </colgroup>
    <thead>
      <tr>
        <th colspan="8" class="section-title">4. DANH SÁCH CHI TIẾT TỪNG BÀI TẬP & KẾT QUẢ ĐẠT ĐƯỢC</th>
      </tr>
      <tr>
        <th class="text-center">STT</th>
        <th>Tên bài tập</th>
        <th>Môn học</th>
        <th class="text-center">Hạn nộp</th>
        <th class="text-center">Trạng thái</th>
        <th class="text-center">Tiến độ câu</th>
        <th class="text-center">Điểm số</th>
        <th>Ghi chú & Nhận xét</th>
      </tr>
    </thead>
    <tbody>`;

  if (summary.records.length === 0) {
    html += `
      <tr>
        <td colspan="8" class="text-center" style="color: #94a3b8; font-style: italic; padding: 14px;">Chưa có bài tập nào được giao</td>
      </tr>`;
  } else {
    summary.records.forEach((rec, idx) => {
      const statusLabel =
        rec.status === "Completed"
          ? "Đã nộp bài"
          : rec.status === "InProgress"
          ? "Đang làm"
          : rec.status === "Overdue"
          ? "Quá hạn"
          : "Chưa làm";

      const statusClass =
        rec.status === "Completed"
          ? "badge-success"
          : rec.status === "InProgress"
          ? "badge-info"
          : rec.status === "Overdue"
          ? "badge-danger"
          : "";

      const scoreText =
        rec.score !== null && rec.score !== undefined
          ? `${rec.score.toFixed(1)} / ${rec.maxScore || 10}`
          : "-";

      const dueDateText = rec.dueAt ? new Date(rec.dueAt).toLocaleDateString("vi-VN") : "Không giới hạn";

      html += `
      <tr class="${idx % 2 === 1 ? "zebra-even" : ""}">
        <td class="text-center">${idx + 1}</td>
        <td class="font-bold">${rec.title}</td>
        <td>${rec.subjectName || classInfo?.subject?.subjectName || "-"}</td>
        <td class="text-center">${dueDateText}</td>
        <td class="text-center ${statusClass}">${statusLabel}</td>
        <td class="text-center">${rec.completedQuestionCount}/${rec.totalQuestionCount} câu</td>
        <td class="text-center font-bold" style="color: #0f766e;">${scoreText}</td>
        <td>${rec.feedbackNote || ""}</td>
      </tr>`;
    });
  }

  html += `
    </tbody>
  </table>`;

  if (teacherNote) {
    html += `
  <!-- Section 5: Nhận xét sư phạm của giáo viên -->
  <table>
    <colgroup>
      <col width="220">
      <col width="720">
    </colgroup>
    <thead>
      <tr>
        <th colspan="2" class="section-title">5. NHẬN XÉT SƯ PHẠM CỦA GIÁO VIÊN PHỤ TRÁCH</th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td class="label-cell">Nội dung đánh giá & Phương hướng bồi dưỡng</td>
        <td class="value-cell" style="line-height: 1.5; color: #1e293b;">${teacherNote}</td>
      </tr>
    </tbody>
  </table>`;
  }

  return html;
}

/**
 * Generates an Excel HTML string for the entire class report with formatted columns and rows.
 */
export function buildClassReportExcelHtml(
  classInfo: ClassDto,
  students: StudentDto[],
  studentSummaries: Map<string, StudentAcademicSummary>,
  studentGoalsMap: Map<string, StudentSubjectGoalDto[]>
): string {
  const exportTime = new Date().toLocaleString("vi-VN");

  let totalCompRate = 0;
  let totalScoreSum = 0;
  let scoredStudentCount = 0;
  let highRiskCount = 0;

  students.forEach((s) => {
    const sum = studentSummaries.get(s.studentId);
    if (sum) {
      totalCompRate += sum.completionRate;
      if (sum.averageScore !== null) {
        totalScoreSum += sum.averageScore;
        scoredStudentCount++;
      }
      if (sum.completionRate < 50 || (sum.averageScore !== null && sum.averageScore < 5.0)) {
        highRiskCount++;
      }
    }
  });

  const avgClassCompRate = students.length > 0 ? (totalCompRate / students.length).toFixed(1) : "0";
  const avgClassScore = scoredStudentCount > 0 ? (totalScoreSum / scoredStudentCount).toFixed(1) : "-";

  return `
  <div class="title-header">BÁO CÁO TỔNG KẾT TIẾN ĐỘ VÀ BẢNG ĐIỂM LỚP HỌC</div>
  <div class="meta-sub">Thời gian xuất: ${exportTime} | Hệ Thống Đào Tạo & Khảo Thí Thích Ứng EduTwin</div>

  <!-- Class Metadata Summary Grid -->
  <table>
    <colgroup>
      <col width="220">
      <col width="280">
      <col width="220">
      <col width="280">
    </colgroup>
    <thead>
      <tr>
        <th colspan="4" class="section-title">THÔNG TIN TỔNG QUAN LỚP HỌC</th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td class="label-cell">Lớp học</td>
        <td class="value-cell">${classInfo.className}</td>
        <td class="label-cell">Niên khóa</td>
        <td class="value-cell">${classInfo.academicYear}</td>
      </tr>
      <tr class="zebra-even">
        <td class="label-cell">Môn học phụ trách</td>
        <td class="value-cell">${classInfo.subject?.subjectName || "Đa môn"}</td>
        <td class="label-cell">Sĩ số học sinh</td>
        <td class="value-cell">${students.length} học sinh</td>
      </tr>
      <tr>
        <td class="label-cell">Tỷ lệ nộp bài tập chung</td>
        <td class="value-cell font-bold" style="color: #0284c7;">${avgClassCompRate}%</td>
        <td class="label-cell">Điểm trung bình cả lớp</td>
        <td class="value-cell font-bold" style="color: #0f766e;">${avgClassScore} / 10.0</td>
      </tr>
      <tr class="zebra-even">
        <td class="label-cell">Học sinh cần kèm cặp bổ trợ</td>
        <td class="value-cell font-bold" style="color: #b91c1c;">${highRiskCount} học sinh</td>
        <td class="label-cell">Trạng thái lớp</td>
        <td class="value-cell">${classInfo.status === "Active" ? "Đang hoạt động" : "Lưu trữ"}</td>
      </tr>
    </tbody>
  </table>

  <!-- Students Roster Grid -->
  <table>
    <colgroup>
      <col width="45">
      <col width="220">
      <col width="140">
      <col width="75">
      <col width="90">
      <col width="90">
      <col width="110">
      <col width="90">
      <col width="110">
      <col width="100">
      <col width="110">
      <col width="120">
    </colgroup>
    <thead>
      <tr>
        <th colspan="12" class="section-title">BẢNG ĐIỂM VÀ TIẾN ĐỘ HỌC TẬP TỪNG HỌC SINH</th>
      </tr>
      <tr>
        <th class="text-center">STT</th>
        <th>Họ và tên học sinh</th>
        <th>Tên đăng nhập</th>
        <th class="text-center">Khối</th>
        <th class="text-center">Bài đã nộp</th>
        <th class="text-center">Chưa nộp</th>
        <th class="text-center">% Hoàn thành</th>
        <th class="text-center">Điểm TB</th>
        <th class="text-center">Xếp loại</th>
        <th class="text-center">Mục tiêu</th>
        <th class="text-center">Dự báo (AI)</th>
        <th class="text-center">Đánh giá rủi ro</th>
      </tr>
    </thead>
    <tbody>
      ${students
        .map((student, idx) => {
          const summary = studentSummaries.get(student.studentId);
          const goals = studentGoalsMap.get(student.studentId) || [];
          const mainGoal = goals.length > 0 ? goals[0] : null;

          const comp = summary ? summary.completionRate : 0;
          const avg = summary?.averageScore;
          const targetStr = mainGoal ? mainGoal.targetScore.toFixed(1) : "-";
          const predStr = mainGoal ? mainGoal.currentPredictedScore.toFixed(1) : "-";

          const classification =
            avg !== null && avg !== undefined
              ? avg >= 8.0
                ? "Giỏi"
                : avg >= 6.5
                ? "Khá"
                : avg >= 5.0
                ? "Trung bình"
                : "Cần củng cố"
              : "Chưa có điểm";

          const isHighRisk = comp < 50 || (avg !== null && avg !== undefined && avg < 5.0);
          const riskLabel = isHighRisk ? "Nguy cơ cao" : comp >= 80 ? "Tiến độ tốt" : "Bình thường";
          const riskClass = isHighRisk ? "badge-danger" : comp >= 80 ? "badge-success" : "badge-info";

          return `
          <tr class="${idx % 2 === 1 ? "zebra-even" : ""}">
            <td class="text-center">${idx + 1}</td>
            <td class="font-bold">${student.fullName}</td>
            <td style="font-family: monospace;">@${student.username}</td>
            <td class="text-center">Khối ${student.gradeLevel}</td>
            <td class="text-center">${summary?.completedCount || 0}</td>
            <td class="text-center">${(summary?.notStartedCount || 0) + (summary?.overdueCount || 0)}</td>
            <td class="text-center font-bold" style="color: #0284c7;">${comp.toFixed(0)}%</td>
            <td class="text-center font-bold" style="color: #0f766e;">${avg !== null && avg !== undefined ? avg.toFixed(1) : "-"}</td>
            <td class="text-center">${classification}</td>
            <td class="text-center">${targetStr}</td>
            <td class="text-center" style="color: #0284c7;">${predStr}</td>
            <td class="text-center ${riskClass}">${riskLabel}</td>
          </tr>`;
        })
        .join("")}
    </tbody>
  </table>`;
}

/**
 * Generates CSV rows for an individual student report formatted strictly in 8 uniform columns.
 */
export function buildStudentReportCsv(
  student: StudentDto,
  classInfo: ClassDto | undefined,
  goals: StudentSubjectGoalDto[],
  subjectsMap: Map<string, string>,
  summary: StudentAcademicSummary
): (string | number | boolean | null | undefined)[][] {
  const rows: (string | number | boolean | null | undefined)[][] = [];

  // Title Row
  rows.push(["BÁO CÁO KẾT QUẢ HỌC TẬP VÀ ĐÁNH GIÁ NĂNG LỰC HỌC VIÊN", "", "", "", "", "", "", ""]);
  rows.push(["Thời gian xuất:", new Date().toLocaleString("vi-VN"), "", "", "", "", "", ""]);
  rows.push(["", "", "", "", "", "", "", ""]);

  // 1. Student Info Table (4 pairs of columns)
  rows.push(["1. THÔNG TIN HỌC VIÊN", "", "", "", "", "", "", ""]);
  rows.push(["Họ và tên:", student.fullName, "Tên đăng nhập:", `@${student.username}`, "Khối lớp:", `Khối ${student.gradeLevel}`, "Trạng thái:", "Hoạt động"]);
  rows.push(["Lớp học:", classInfo ? classInfo.className : "Tất cả lớp", "Niên khóa:", classInfo ? classInfo.academicYear : "-", "Môn học:", classInfo?.subject?.subjectName || "Đa môn", "Giáo viên:", classInfo?.teacher?.displayName || "-"]);
  rows.push(["", "", "", "", "", "", "", ""]);

  // 2. Academic Summary Table
  rows.push(["2. TỔNG HỢP KẾT QUẢ BÀI TẬP", "", "", "", "", "", "", ""]);
  rows.push(["Tổng số bài tập đã giao:", summary.totalAssigned, "Số bài đã hoàn thành:", summary.completedCount, "Số bài chưa làm / quá hạn:", summary.notStartedCount + summary.overdueCount, "Tỷ lệ hoàn thành (%):", `${summary.completionRate.toFixed(1)}%`]);
  rows.push(["Điểm trung bình các bài:", summary.averageScore !== null ? summary.averageScore.toFixed(1) : "Chưa có", "Điểm cao nhất:", summary.maxScore !== null ? summary.maxScore.toFixed(1) : "-", "Điểm thấp nhất:", summary.minScore !== null ? summary.minScore.toFixed(1) : "-", "Thang điểm:", "10.0"]);
  rows.push(["", "", "", "", "", "", "", ""]);

  // 3. Subject Goals Table
  rows.push(["3. MỤC TIÊU ĐIỂM THI VÀ DỰ BÁO NĂNG LỰC (AI DIGITAL TWIN)", "", "", "", "", "", "", ""]);
  rows.push(["STT", "Môn học", "Điểm mục tiêu", "Điểm dự báo (AI)", "Khoảng cách (Gap)", "Số ngày còn lại", "Đánh giá mức độ rủi ro", "Ghi chú"]);
  if (goals.length === 0) {
    rows.push(["-", "Chưa thiết lập mục tiêu môn học nào", "-", "-", "-", "-", "-", "-"]);
  } else {
    goals.forEach((g, idx) => {
      const subjectName = subjectsMap.get(g.subjectId) || g.subjectId;
      const gap = (g.targetScore - g.currentPredictedScore).toFixed(1);
      const riskLabel = g.riskScore > 0.6 ? "Nguy cơ cao" : g.riskScore > 0.3 ? "Cần theo dõi" : "An toàn";
      rows.push([
        idx + 1,
        subjectName,
        g.targetScore.toFixed(1),
        g.currentPredictedScore.toFixed(1),
        Number(gap) > 0 ? `-${gap}` : `+${Math.abs(Number(gap)).toFixed(1)}`,
        `${g.remainingDays} ngày`,
        riskLabel,
        "",
      ]);
    });
  }
  rows.push(["", "", "", "", "", "", "", ""]);

  // 4. Assignments Detail Table
  rows.push(["4. DANH SÁCH CHI TIẾT TỪNG BÀI TẬP VÀ ĐIỂM SỐ", "", "", "", "", "", "", ""]);
  rows.push(["STT", "Tên bài tập", "Môn học", "Hạn nộp", "Trạng thái", "Tiến độ câu", "Điểm số", "Nhận xét"]);

  if (summary.records.length === 0) {
    rows.push(["-", "Chưa có bài tập nào", "-", "-", "-", "-", "-", "-"]);
  } else {
    summary.records.forEach((rec, idx) => {
      const statusText =
        rec.status === "Completed"
          ? "Đã nộp bài"
          : rec.status === "InProgress"
          ? "Đang làm"
          : rec.status === "Overdue"
          ? "Quá hạn"
          : "Chưa làm";

      const scoreText =
        rec.score !== null && rec.score !== undefined
          ? `${rec.score.toFixed(1)} / ${rec.maxScore || 10}`
          : "-";

      const dueDateText = rec.dueAt ? new Date(rec.dueAt).toLocaleDateString("vi-VN") : "Không giới hạn";

      rows.push([
        idx + 1,
        rec.title,
        rec.subjectName || classInfo?.subject?.subjectName || "-",
        dueDateText,
        statusText,
        `${rec.completedQuestionCount}/${rec.totalQuestionCount} câu`,
        scoreText,
        rec.feedbackNote || "",
      ]);
    });
  }

  return rows;
}

/**
 * Generates CSV rows for the whole class report formatted strictly in 12 uniform columns.
 */
export function buildClassReportCsv(
  classInfo: ClassDto,
  students: StudentDto[],
  studentSummaries: Map<string, StudentAcademicSummary>,
  studentGoalsMap: Map<string, StudentSubjectGoalDto[]>
): (string | number | boolean | null | undefined)[][] {
  const rows: (string | number | boolean | null | undefined)[][] = [];

  // Title
  rows.push(["BÁO CÁO THỐNG KÊ TIẾN ĐỘ VÀ KẾT QUẢ HỌC TẬP CẢ LỚP", "", "", "", "", "", "", "", "", "", "", ""]);
  rows.push(["Lớp học:", classInfo.className, "Niên khóa:", classInfo.academicYear, "Sĩ số:", `${students.length} học sinh`, "Thời gian xuất:", new Date().toLocaleString("vi-VN"), "", "", "", ""]);
  rows.push(["", "", "", "", "", "", "", "", "", "", "", ""]);

  // Summary Metrics
  let totalCompRate = 0;
  let totalScoreSum = 0;
  let scoredStudentCount = 0;

  students.forEach((s) => {
    const sum = studentSummaries.get(s.studentId);
    if (sum) {
      totalCompRate += sum.completionRate;
      if (sum.averageScore !== null) {
        totalScoreSum += sum.averageScore;
        scoredStudentCount++;
      }
    }
  });

  const avgClassCompRate = students.length > 0 ? (totalCompRate / students.length).toFixed(1) : "0";
  const avgClassScore = scoredStudentCount > 0 ? (totalScoreSum / scoredStudentCount).toFixed(1) : "-";

  rows.push(["TỔNG QUAN CHỈ SỐ LỚP HỌC", "", "", "", "", "", "", "", "", "", "", ""]);
  rows.push(["Tỷ lệ hoàn thành bài tập chung:", `${avgClassCompRate}%`, "Điểm trung bình toàn lớp:", `${avgClassScore} / 10.0`, "", "", "", "", "", "", "", ""]);
  rows.push(["", "", "", "", "", "", "", "", "", "", "", ""]);

  // Students Roster Header
  rows.push(["DANH SÁCH BẢNG ĐIỂM VÀ TIẾN ĐỘ HỌC SINH", "", "", "", "", "", "", "", "", "", "", ""]);
  rows.push([
    "STT",
    "Họ và tên",
    "Tên đăng nhập",
    "Khối lớp",
    "Bài đã nộp",
    "Chưa nộp / Quá hạn",
    "Tỷ lệ hoàn thành (%)",
    "Điểm trung bình",
    "Xếp loại học lực",
    "Mục tiêu điểm",
    "Dự báo hiện tại (AI)",
    "Đánh giá rủi ro",
  ]);

  students.forEach((student, idx) => {
    const summary = studentSummaries.get(student.studentId);
    const goals = studentGoalsMap.get(student.studentId) || [];
    const mainGoal = goals.length > 0 ? goals[0] : null;

    const completed = summary ? summary.completedCount : 0;
    const pending = summary ? summary.notStartedCount + summary.overdueCount : 0;
    const compRate = summary ? `${summary.completionRate.toFixed(1)}%` : "0%";
    const avgScore = summary && summary.averageScore !== null ? summary.averageScore.toFixed(1) : "-";

    const classification =
      summary && summary.averageScore !== null
        ? summary.averageScore >= 8.0
          ? "Giỏi"
          : summary.averageScore >= 6.5
          ? "Khá"
          : summary.averageScore >= 5.0
          ? "Trung bình"
          : "Cần củng cố"
        : "Chưa có điểm";

    const targetText = mainGoal ? mainGoal.targetScore.toFixed(1) : "-";
    const predictedText = mainGoal ? mainGoal.currentPredictedScore.toFixed(1) : "-";
    const isHighRisk = summary && (summary.completionRate < 50 || (summary.averageScore !== null && summary.averageScore < 5.0));
    const riskLabel = isHighRisk ? "Nguy cơ cao" : summary && summary.completionRate >= 80 ? "Tiến độ tốt" : "Bình thường";

    rows.push([
      idx + 1,
      student.fullName,
      `@${student.username}`,
      `Khối ${student.gradeLevel}`,
      completed,
      pending,
      compRate,
      avgScore,
      classification,
      targetText,
      predictedText,
      riskLabel,
    ]);
  });

  return rows;
}

/**
 * Downloads a genuine binary XLSX workbook (.xlsx) to the browser.
 */
export function downloadXlsx(workbook: XLSX.WorkBook, filename: string) {
  if (typeof document === "undefined") return;
  const wbout = XLSX.write(workbook, { bookType: "xlsx", type: "array" });
  const blob = new Blob([wbout], {
    type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.setAttribute("href", url);
  link.setAttribute("download", filename.endsWith(".xlsx") ? filename : `${filename}.xlsx`);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Builds the complete class report workbook (.xlsx) with discrete columns and custom widths.
 */
export function buildClassReportWorkbook(
  classInfo: ClassDto,
  students: StudentDto[],
  studentSummaries: Map<string, StudentAcademicSummary>,
  studentGoalsMap: Map<string, StudentSubjectGoalDto[]>
): XLSX.WorkBook {
  const wb = XLSX.utils.book_new();
  const exportTime = new Date().toLocaleString("vi-VN");

  let totalCompRate = 0;
  let totalScoreSum = 0;
  let scoredStudentCount = 0;
  let highRiskCount = 0;

  students.forEach((s) => {
    const sum = studentSummaries.get(s.studentId);
    if (sum) {
      totalCompRate += sum.completionRate;
      if (sum.averageScore !== null) {
        totalScoreSum += sum.averageScore;
        scoredStudentCount++;
      }
      if (sum.completionRate < 50 || (sum.averageScore !== null && sum.averageScore < 5.0)) {
        highRiskCount++;
      }
    }
  });

  const avgClassCompRate = students.length > 0 ? (totalCompRate / students.length).toFixed(1) : "0";
  const avgClassScore = scoredStudentCount > 0 ? (totalScoreSum / scoredStudentCount).toFixed(1) : "-";

  const rows: (string | number)[][] = [
    [
      "STT",
      "Mã Học Sinh",
      "Họ Và Tên",
      "Tên Đăng Nhập",
      "Khối Lớp",
      "Lớp Học",
      "Môn Phụ Trách",
      "Tổng Bài Giao",
      "Bài Đã Nộp",
      "Bài Đang Làm",
      "Chưa Làm / Quá Hạn",
      "Tỷ Lệ Hoàn Thành (%)",
      "Điểm Trung Bình",
      "Điểm Cao Nhất",
      "Điểm Thấp Nhất",
      "Xếp Loại Học Lực",
      "Điểm Mục Tiêu",
      "Dự Báo AI",
      "Đánh Giá Rủi Ro",
      "Đánh Giá Sư Phạm",
      "Trạng Thái Hoạt Động",
    ],
  ];

  students.forEach((student, idx) => {
    const summary = studentSummaries.get(student.studentId);
    const goals = studentGoalsMap.get(student.studentId) || [];
    const mainGoal = goals.length > 0 ? goals[0] : null;

    const completed = summary ? summary.completedCount : 0;
    const inProgress = summary ? summary.inProgressCount : 0;
    const pending = summary ? summary.notStartedCount + summary.overdueCount : 0;
    const totalAssigned = summary ? summary.totalAssigned : 0;
    const compRate = summary ? Number(summary.completionRate.toFixed(1)) : 0;
    const avgScore = summary && summary.averageScore !== null ? Number(summary.averageScore.toFixed(1)) : "";
    const maxScore = summary && summary.maxScore !== null ? Number(summary.maxScore.toFixed(1)) : "";
    const minScore = summary && summary.minScore !== null ? Number(summary.minScore.toFixed(1)) : "";

    const classification =
      summary && summary.averageScore !== null
        ? summary.averageScore >= 8.0
          ? "Giỏi"
          : summary.averageScore >= 6.5
          ? "Khá"
          : summary.averageScore >= 5.0
          ? "Trung bình"
          : "Cần củng cố"
        : "Chưa có điểm";

    const targetVal = mainGoal ? mainGoal.targetScore : "";
    const predVal = mainGoal ? mainGoal.currentPredictedScore : "";
    const isHighRisk = summary && (summary.completionRate < 50 || (summary.averageScore !== null && summary.averageScore < 5.0));
    const riskLabel = isHighRisk ? "Nguy cơ cao" : summary && summary.completionRate >= 80 ? "Tiến độ tốt" : "Bình thường";
    const pedagogicalAssessment = isHighRisk
      ? "Cần kèm cặp bổ trợ kiến thức hổng"
      : compRate >= 80
      ? "Chủ động hoàn thành bài tập tốt"
      : "Cần duy trì tiến độ làm bài";

    rows.push([
      idx + 1,
      student.studentId,
      student.fullName,
      `@${student.username}`,
      `Khối ${student.gradeLevel}`,
      classInfo.className,
      classInfo.subject?.subjectName || "Đa môn",
      totalAssigned,
      completed,
      inProgress,
      pending,
      compRate,
      avgScore,
      maxScore,
      minScore,
      classification,
      targetVal,
      predVal,
      riskLabel,
      pedagogicalAssessment,
      student.status === "Active" ? "Đang hoạt động" : "Tạm ngưng",
    ]);
  });

  const ws = XLSX.utils.aoa_to_sheet(rows);

  ws["!cols"] = [
    { wch: 6 },   // STT
    { wch: 14 },  // Mã Học Sinh
    { wch: 25 },  // Họ Và Tên
    { wch: 18 },  // Tên Đăng Nhập
    { wch: 10 },  // Khối Lớp
    { wch: 14 },  // Lớp Học
    { wch: 18 },  // Môn Phụ Trách
    { wch: 14 },  // Tổng Bài Giao
    { wch: 12 },  // Bài Đã Nộp
    { wch: 14 },  // Bài Đang Làm
    { wch: 18 },  // Chưa Làm / Quá Hạn
    { wch: 20 },  // Tỷ Lệ Hoàn Thành (%)
    { wch: 16 },  // Điểm Trung Bình
    { wch: 14 },  // Điểm Cao Nhất
    { wch: 14 },  // Điểm Thấp Nhất
    { wch: 16 },  // Xếp Loại Học Lực
    { wch: 14 },  // Điểm Mục Tiêu
    { wch: 14 },  // Dự Báo AI
    { wch: 18 },  // Đánh Giá Rủi Ro
    { wch: 32 },  // Đánh Giá Sư Phạm
    { wch: 18 },  // Trạng Thái Hoạt Động
  ];

  XLSX.utils.book_append_sheet(wb, ws, "Bang_Diem_Lop");

  // Sheet 2: Tổng quan & Chỉ số lớp học
  const summaryRows: (string | number)[][] = [
    ["BÁO CÁO TỔNG KẾT TIẾN ĐỘ VÀ KẾT QUẢ HỌC TẬP LỚP HỌC"],
    [`Lớp học: ${classInfo.className}`, `Niên khóa: ${classInfo.academicYear}`, `Môn học: ${classInfo.subject?.subjectName || "Đa môn"}`],
    [`Sĩ số: ${students.length} học sinh`, `Thời gian xuất: ${exportTime}`, `Trạng thái: ${classInfo.status === "Active" ? "Đang hoạt động" : "Lưu trữ"}`],
    [],
    ["CHỈ SỐ TIẾN ĐỘ VÀ KẾT QUẢ HỌC TẬP TOÀN LỚP"],
    ["Chỉ Số", "Giá Trị", "Đơn Vị", "Đánh Giá"],
    ["Tỷ lệ hoàn thành bài tập trung bình", Number(avgClassCompRate), "%", Number(avgClassCompRate) >= 80 ? "Đạt xuất sắc" : Number(avgClassCompRate) >= 50 ? "Mức độ trung bình" : "Cần đôn đốc"],
    ["Điểm trung bình toàn lớp", avgClassScore !== "-" ? Number(avgClassScore) : "-", "Thang 10", ""],
    ["Số học sinh cần hỗ trợ bổ trợ", highRiskCount, "Học sinh", highRiskCount > 0 ? "Cần lên kế hoạch phụ đạo" : "Tiến độ an toàn"],
  ];
  const wsSummary = XLSX.utils.aoa_to_sheet(summaryRows);
  wsSummary["!cols"] = [
    { wch: 35 },
    { wch: 15 },
    { wch: 15 },
    { wch: 30 },
  ];
  XLSX.utils.book_append_sheet(wb, wsSummary, "Tong_Quan_Lop");

  return wb;
}

/**
 * Exports the entire class report as a native multi-column Microsoft Excel (.xlsx) file with custom column widths.
 */
export function exportClassReportXlsx(
  classInfo: ClassDto,
  students: StudentDto[],
  studentSummaries: Map<string, StudentAcademicSummary>,
  studentGoalsMap: Map<string, StudentSubjectGoalDto[]>
) {
  const wb = buildClassReportWorkbook(classInfo, students, studentSummaries, studentGoalsMap);
  const filename = `Bang_diem_lop_${classInfo.className}_${new Date().toISOString().slice(0, 10)}.xlsx`;
  downloadXlsx(wb, filename);
}

/**
 * Builds an individual student report workbook (.xlsx) with discrete columns and custom widths across multiple sheets.
 */
export function buildStudentReportWorkbook(
  student: StudentDto,
  classInfo: ClassDto | undefined,
  goals: StudentSubjectGoalDto[],
  subjectsMap: Map<string, string>,
  summary: StudentAcademicSummary,
  teacherNote?: string
): XLSX.WorkBook {
  const wb = XLSX.utils.book_new();
  const exportTime = new Date().toLocaleString("vi-VN");
  const avgScoreStr = summary.averageScore !== null ? summary.averageScore.toFixed(1) : "Chưa có";

  // --- SHEET 1: Chi Tiết Từng Bài Tập (Row 1 is Table Header) ---
  const assignmentRows: (string | number)[][] = [
    [
      "STT",
      "Tên Bài Tập",
      "Môn Học",
      "Hạn Nộp",
      "Trạng Thái",
      "Số Câu Đã Làm",
      "Tổng Số Câu",
      "Tỷ Lệ Hoàn Thành (%)",
      "Điểm Số",
      "Thang Điểm",
      "Xếp Loại",
      "Ngày Nộp Bài",
      "Lời Phê & Nhận Xét",
    ],
  ];

  if (summary.records.length === 0) {
    assignmentRows.push(["-", "Chưa có bài tập nào được giao", "-", "-", "-", "-", "-", "-", "-", "-", "-", "-", "-"]);
  } else {
    summary.records.forEach((rec, idx) => {
      const statusText =
        rec.status === "Completed"
          ? "Đã nộp bài"
          : rec.status === "InProgress"
          ? "Đang làm"
          : rec.status === "Overdue"
          ? "Quá hạn"
          : "Chưa làm";

      const dueDateText = rec.dueAt ? new Date(rec.dueAt).toLocaleDateString("vi-VN") : "Không giới hạn";
      const submittedDateText = rec.submittedAt ? new Date(rec.submittedAt).toLocaleDateString("vi-VN") : "-";
      const completionPct = rec.totalQuestionCount > 0 ? Math.round((rec.completedQuestionCount / rec.totalQuestionCount) * 100) : 0;
      const scoreVal = rec.score !== null && rec.score !== undefined ? Number(rec.score.toFixed(1)) : "";

      const gradeRank =
        scoreVal !== ""
          ? Number(scoreVal) >= 8.0
            ? "Giỏi"
            : Number(scoreVal) >= 6.5
            ? "Khá"
            : Number(scoreVal) >= 5.0
            ? "Đạt"
            : "Chưa đạt"
          : "-";

      assignmentRows.push([
        idx + 1,
        rec.title,
        rec.subjectName || classInfo?.subject?.subjectName || "Chung",
        dueDateText,
        statusText,
        rec.completedQuestionCount,
        rec.totalQuestionCount,
        completionPct,
        scoreVal,
        rec.maxScore || 10,
        gradeRank,
        submittedDateText,
        rec.feedbackNote || "",
      ]);
    });
  }

  const wsAssignments = XLSX.utils.aoa_to_sheet(assignmentRows);
  wsAssignments["!cols"] = [
    { wch: 6 },   // STT
    { wch: 34 },  // Tên Bài Tập
    { wch: 18 },  // Môn Học
    { wch: 14 },  // Hạn Nộp
    { wch: 16 },  // Trạng Thái
    { wch: 16 },  // Số Câu Đã Làm
    { wch: 14 },  // Tổng Số Câu
    { wch: 20 },  // Tỷ Lệ Hoàn Thành (%)
    { wch: 12 },  // Điểm Số
    { wch: 12 },  // Thang Điểm
    { wch: 14 },  // Xếp Loại
    { wch: 16 },  // Ngày Nộp Bài
    { wch: 36 },  // Lời Phê & Nhận Xét
  ];
  XLSX.utils.book_append_sheet(wb, wsAssignments, "Chi_Tiet_Bai_Tap");

  // --- SHEET 2: Tổng Quan Học Lực & Mục Tiêu AI ---
  const overviewRows: (string | number)[][] = [
    ["TỔNG QUAN NĂNG LỰC & MỤC TIÊU HỌC TẬP HỌC VIÊN"],
    [`Thời gian xuất: ${exportTime}`, `Điểm TB: ${avgScoreStr}`, `Tỷ lệ hoàn thành: ${summary.completionRate.toFixed(1)}%`],
    [],
    ["1. HỒ SƠ HỌC VIÊN"],
    ["Thuộc Tính", "Giá Trị", "Phân Loại", "Ghi Chú"],
    ["Họ và tên học sinh", student.fullName, "Học viên", ""],
    ["Tên đăng nhập (Username)", `@${student.username}`, "Tài khoản học vụ", ""],
    ["Mã học viên", student.studentId, "Hệ thống", ""],
    ["Khối lớp", `Khối ${student.gradeLevel}`, "Bậc THPT", ""],
    ["Lớp học phụ trách", classInfo ? classInfo.className : "Tất cả lớp", classInfo?.academicYear || "-", classInfo?.subject?.subjectName || "Đa môn"],
    [],
    ["2. CHỈ SỐ HỌC TẬP & TIẾN ĐỘ"],
    ["Chỉ Số", "Giá Trị", "Đơn Vị", "Đánh Giá Sư Phạm"],
    ["Tổng số bài tập đã giao", summary.totalAssigned, "Bài tập", "Toàn bộ bài tập được giao"],
    ["Số bài đã hoàn thành", summary.completedCount, "Bài tập", "Đã nộp bài đầy đủ"],
    ["Số bài đang làm dở dang", summary.inProgressCount, "Bài tập", "Đang trong quá trình làm"],
    ["Số bài chưa làm / quá hạn", summary.notStartedCount + summary.overdueCount, "Bài tập", summary.overdueCount > 0 ? `Có ${summary.overdueCount} bài quá hạn` : "Chưa làm"],
    ["Tỷ lệ hoàn thành bài tập", `${summary.completionRate.toFixed(1)}%`, "%", summary.completionRate >= 80 ? "Đạt xuất sắc" : summary.completionRate >= 50 ? "Mức độ trung bình" : "Cần đôn đốc"],
    ["Điểm trung bình các bài", summary.averageScore !== null ? Number(summary.averageScore.toFixed(1)) : "Chưa có", "Thang 10", summary.averageScore !== null && summary.averageScore >= 8.0 ? "Học lực Giỏi" : summary.averageScore !== null && summary.averageScore >= 6.5 ? "Học lực Khá" : "Cần bồi dưỡng"],
    ["Điểm cao nhất", summary.maxScore !== null ? Number(summary.maxScore.toFixed(1)) : "-", "Thang 10", ""],
    ["Điểm thấp nhất", summary.minScore !== null ? Number(summary.minScore.toFixed(1)) : "-", "Thang 10", ""],
    [],
    ["3. MỤC TIÊU ĐIỂM THI & DỰ BÁO AI DIGITAL TWIN"],
    ["STT", "Môn Học", "Điểm Mục Tiêu", "Điểm Dự Báo AI", "Chênh Lệch (Gap)", "Số Ngày Còn Lại", "Mức Độ Rủi Ro"],
  ];

  if (goals.length === 0) {
    overviewRows.push(["-", "Chưa thiết lập mục tiêu môn học nào", "-", "-", "-", "-", "-"]);
  } else {
    goals.forEach((g, idx) => {
      const subjectName = subjectsMap.get(g.subjectId) || g.subjectId;
      const gap = (g.targetScore - g.currentPredictedScore).toFixed(1);
      const isHighRisk = g.riskScore > 0.6;
      const riskLabel = isHighRisk ? "Nguy cơ cao" : g.riskScore > 0.3 ? "Cần theo dõi" : "Tiến độ an toàn";

      overviewRows.push([
        idx + 1,
        subjectName,
        g.targetScore,
        g.currentPredictedScore,
        Number(gap) > 0 ? `-${gap}` : `+${Math.abs(Number(gap)).toFixed(1)}`,
        `${g.remainingDays} ngày`,
        riskLabel,
      ]);
    });
  }

  if (teacherNote) {
    overviewRows.push([]);
    overviewRows.push(["4. NHẬN XÉT SƯ PHẠM CỦA GIÁO VIÊN"]);
    overviewRows.push(["Nội dung nhận xét & Hướng dẫn:", teacherNote]);
  }

  const wsOverview = XLSX.utils.aoa_to_sheet(overviewRows);
  wsOverview["!cols"] = [
    { wch: 28 }, // Cột 1
    { wch: 32 }, // Cột 2
    { wch: 22 }, // Cột 3
    { wch: 35 }, // Cột 4
    { wch: 18 }, // Cột 5
    { wch: 18 }, // Cột 6
    { wch: 20 }, // Cột 7
  ];
  XLSX.utils.book_append_sheet(wb, wsOverview, "Tong_Quan_Hoc_Vien");
  return wb;
}

/**
 * Exports an individual student report as a native multi-column Microsoft Excel (.xlsx) file with custom column widths.
 */
export function exportStudentReportXlsx(
  student: StudentDto,
  classInfo: ClassDto | undefined,
  goals: StudentSubjectGoalDto[],
  subjectsMap: Map<string, string>,
  summary: StudentAcademicSummary,
  teacherNote?: string
) {
  const wb = buildStudentReportWorkbook(student, classInfo, goals, subjectsMap, summary, teacherNote);
  const filename = `Bao_cao_hoc_vien_${student.username}_${new Date().toISOString().slice(0, 10)}.xlsx`;
  downloadXlsx(wb, filename);
}

