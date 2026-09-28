import * as XLSX from "xlsx";
import type { ClassDto, StudentDto, StudentSubjectGoalDto } from "../../types/organization";
import type { StudentAcademicSummary, StudentAssignmentRecord } from "./teacherReportsHelpers";

export function downloadXlsx(wb: XLSX.WorkBook, filename: string) {
  const wbout = XLSX.write(wb, { bookType: "xlsx", type: "array" });
  const blob = new Blob([wbout], {
    type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.setAttribute("download", filename);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

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
 * Builds a structured, multi-sheet workbook for an individual student report.
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
    summary.records.forEach((rec: StudentAssignmentRecord, idx: number) => {
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
