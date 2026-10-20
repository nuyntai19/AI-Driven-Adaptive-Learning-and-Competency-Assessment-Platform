import * as XLSX from "xlsx";
import type { ClassDto, StudentDto, StudentSubjectGoalDto } from "../../types/organization";
import { assessmentLabel, isAcademicHighRisk } from "./teacherReportsHelpers.ts";
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

function visualProgressBar(percentage: number): string {
  if (percentage <= 0) return "░░░░░░░░░░ 0.0%";
  const filled = Math.min(10, Math.max(1, Math.round(percentage / 10)));
  const empty = 10 - filled;
  return `${"█".repeat(filled)}${"░".repeat(empty)} ${percentage.toFixed(1)}%`;
}

export function buildClassReportWorkbook(
  classInfo: ClassDto,
  students: StudentDto[],
  studentSummaries: Map<string, StudentAcademicSummary>,
  studentGoalsMap: Map<string, StudentSubjectGoalDto[]>
): XLSX.WorkBook {
  const wb = XLSX.utils.book_new();
  const exportTime = new Date().toLocaleString("vi-VN");

  // Statistical aggregations
  let totalCompRate = 0;
  let totalScoreSum = 0;
  let scoredStudentCount = 0;
  let highRiskCount = 0;
  const validScores: number[] = [];
  let summaryTotalAssigned = 0;
  let summaryCompleted = 0;
  let summaryInProgress = 0;
  let summaryPending = 0;
  let passedCount = 0;

  // Academic distribution counts
  let countXuatSac = 0;
  let countGioi = 0;
  let countKha = 0;
  let countTB = 0;
  let countYeu = 0;
  let countNoScore = 0;

  students.forEach((s) => {
    const sum = studentSummaries.get(s.studentId);
    if (sum) {
      totalCompRate += sum.completionRate;
      summaryTotalAssigned += sum.totalAssigned;
      summaryCompleted += sum.completedCount;
      summaryInProgress += sum.inProgressCount;
      summaryPending += sum.notStartedCount + sum.overdueCount;

      if (sum.averageScore !== null) {
        totalScoreSum += sum.averageScore;
        scoredStudentCount++;
        validScores.push(sum.averageScore);
        if (sum.averageScore >= 5.0) passedCount++;

        if (sum.averageScore >= 9.0) countXuatSac++;
        else if (sum.averageScore >= 8.0) countGioi++;
        else if (sum.averageScore >= 6.5) countKha++;
        else if (sum.averageScore >= 5.0) countTB++;
        else countYeu++;
      } else {
        countNoScore++;
      }

      if (isAcademicHighRisk(sum)) {
        highRiskCount++;
      }
    } else {
      countNoScore++;
    }
  });

  const classMaxScore = validScores.length > 0 ? Math.max(...validScores) : null;
  const classMinScore = validScores.length > 0 ? Math.min(...validScores) : null;

  const totalStudents = Math.max(1, students.length);
  const avgClassCompRate = students.length > 0 ? (totalCompRate / students.length).toFixed(1) : "0";
  const avgClassScore = scoredStudentCount > 0 ? (totalScoreSum / scoredStudentCount).toFixed(1) : "-";
  const passRate = scoredStudentCount > 0 ? Number(((passedCount / scoredStudentCount) * 100).toFixed(1)) : 0;

  const pctXuatSac = Number(((countXuatSac / totalStudents) * 100).toFixed(1));
  const pctGioi = Number(((countGioi / totalStudents) * 100).toFixed(1));
  const pctKha = Number(((countKha / totalStudents) * 100).toFixed(1));
  const pctTB = Number(((countTB / totalStudents) * 100).toFixed(1));
  const pctYeu = Number(((countYeu / totalStudents) * 100).toFixed(1));
  const pctNoScore = Number(((countNoScore / totalStudents) * 100).toFixed(1));

  // --- SHEET 1: Bảng Điểm & Tiến Độ Học Sinh Lớp ---
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

  const studentsNeedingSupport: {
    student: StudentDto;
    summary: StudentAcademicSummary;
    reason: string;
    action: string;
  }[] = [];

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
        ? summary.averageScore >= 9.0
          ? "Xuất sắc"
          : summary.averageScore >= 8.0
          ? "Giỏi"
          : summary.averageScore >= 6.5
          ? "Khá"
          : summary.averageScore >= 5.0
          ? "Trung bình"
          : "Cần củng cố"
        : "Chưa có điểm";

    const targetVal = mainGoal ? mainGoal.targetScore : "";
    const predVal = mainGoal ? mainGoal.currentPredictedScore : "";
    const isHighRisk = isAcademicHighRisk(summary);
    const riskLabel = assessmentLabel(summary);
    const pedagogicalAssessment = isHighRisk
      ? "Cần kèm cặp bổ trợ kiến thức hổng"
      : compRate >= 80
      ? "Chủ động hoàn thành bài tập tốt"
      : "Cần duy trì tiến độ làm bài";

    if (summary && (isHighRisk || (summary.averageScore !== null && summary.averageScore < 6.5) || compRate < 60 || summary.overdueCount > 0)) {
      const reason = isHighRisk
        ? "Cảnh báo nguy cơ cao từ AI Digital Twin"
        : summary.averageScore !== null && summary.averageScore < 5.0
        ? "Điểm trung bình dưới 5.0 (Cần củng cố)"
        : summary.overdueCount > 0
        ? `Có ${summary.overdueCount} bài tập nộp muộn / quá hạn`
        : compRate < 60
        ? `Tỷ lệ hoàn thành thấp (${compRate}%)`
        : "Điểm trung bình mức trung bình, cần bứt phá";
      const action = summary.averageScore !== null && summary.averageScore < 5.0
        ? "Lập kế hoạch phụ đạo 1-1 và phân tích lỗi sai bài tập"
        : summary.overdueCount > 0
        ? "Đôn đốc nộp bài và nhắc nhở gia đình phối hợp"
        : "Giao thêm bài tập bổ trợ chuyên đề kiến thức hổng";
      studentsNeedingSupport.push({ student, summary, reason, action });
    }

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
    { wch: 18 },  // Xếp Loại Học Lực
    { wch: 14 },  // Điểm Mục Tiêu
    { wch: 14 },  // Dự Báo AI
    { wch: 20 },  // Đánh Giá Rủi Ro
    { wch: 34 },  // Đánh Giá Sư Phạm
    { wch: 20 },  // Trạng Thái Hoạt Động
  ];

  // AutoFilter for interactive sorting and filtering in Excel
  ws["!autofilter"] = {
    ref: XLSX.utils.encode_range({ s: { r: 0, c: 0 }, e: { r: rows.length - 1, c: 20 } }),
  };

  // Freeze top header row and left 3 identifier columns (STT, Mã, Họ Tên)
  ws["!views"] = [{ state: "frozen", xSplit: 3, ySplit: 1, topLeftCell: "D2", activeCell: "D2" }];
  ws["!rows"] = [{ hpt: 26 }, ...students.map(() => ({ hpt: 20 }))];

  XLSX.utils.book_append_sheet(wb, ws, "Bang_Diem_Lop");

  // --- SHEET 2: Báo Cáo Tổng Quan & Dashboard Lớp Học ---
  const summaryRows: (string | number)[][] = [
    ["BÁO CÁO TỔNG QUAN TIẾN ĐỘ & PHÂN TÍCH HỌC LỰC LỚP HỌC - EDUTWIN PLATFORM"],
    [
      `Lớp học: ${classInfo.className}`,
      `Niên khóa: ${classInfo.academicYear || "2025-2026"}`,
      `Môn học: ${classInfo.subject?.subjectName || "Đa môn"}`,
      `Giáo viên: ${classInfo.teacher?.displayName || "Giáo viên phụ trách"}`,
    ],
    [
      `Sĩ số: ${students.length} học sinh`,
      `Trạng thái: ${classInfo.status === "Active" ? "Đang hoạt động" : "Lưu trữ"}`,
      `Thời gian xuất: ${exportTime}`,
      "Nền tảng: Hệ thống Học tập Thích ứng EduTwin",
    ],
    [],
    ["1. CHỈ SỐ HỌC VỤ & TIẾN ĐỘ CỐT LÕI (KEY METRICS DASHBOARD)"],
    ["Chỉ Số Học Vụ", "Giá Trị", "Đơn Vị", "Mức Đánh Giá", "Khuyến Nghị Sư Phạm"],
    [
      "Tỷ lệ hoàn thành bài tập trung bình toàn lớp",
      Number(avgClassCompRate),
      "%",
      Number(avgClassCompRate) >= 80 ? "Xuất sắc (>= 80%)" : Number(avgClassCompRate) >= 50 ? "Mức độ trung bình (50-79%)" : "Cần đôn đốc (< 50%)",
      Number(avgClassCompRate) >= 80 ? "Duy trì tần suất và độ khó bài tập phù hợp" : "Cần tăng cường nhắc nhở và giao bài tập vừa sức",
    ],
    [
      "Điểm trung bình toàn lớp",
      avgClassScore !== "-" ? Number(avgClassScore) : "-",
      "Thang 10",
      avgClassScore !== "-" ? (Number(avgClassScore) >= 8.0 ? "Học lực Giỏi" : Number(avgClassScore) >= 6.5 ? "Học lực Khá" : Number(avgClassScore) >= 5.0 ? "Học lực Trung bình" : "Học lực Cần củng cố") : "Chưa có điểm",
      avgClassScore !== "-" ? (Number(avgClassScore) >= 8.0 ? "Bồi dưỡng học sinh mũi nhọn và nâng cao tư duy" : Number(avgClassScore) >= 6.5 ? "Rèn luyện bài tập phân hóa mức độ 3 và 4" : "Tập trung củng cố kiến thức trọng tâm cơ bản") : "Đôn đốc nộp bài để lấy điểm đánh giá",
    ],
    [
      "Điểm cao nhất trong lớp",
      classMaxScore !== null ? Number(classMaxScore.toFixed(1)) : "-",
      "Thang 10",
      "Thành tích dẫn đầu",
      "Khen thưởng và giao thêm nhiệm vụ thử thách",
    ],
    [
      "Điểm thấp nhất trong lớp",
      classMinScore !== null ? Number(classMinScore.toFixed(1)) : "-",
      "Thang 10",
      classMinScore !== null && classMinScore < 5.0 ? "Cần phụ đạo gấp" : "Đạt mức an toàn",
      classMinScore !== null && classMinScore < 5.0 ? "Kèm cặp 1-1 và phân tích lỗi sai điển hình" : "Khích lệ cải thiện điểm số",
    ],
    [
      "Tỷ lệ học sinh đạt chuẩn (Điểm TB >= 5.0)",
      passRate,
      "%",
      passRate >= 80 ? "Tỷ lệ đạt rất cao" : passRate >= 60 ? "Mức trung bình" : "Cần lưu ý, tỷ lệ đạt thấp",
      passRate >= 80 ? "Tiến độ học tập của lớp ổn định" : "Lên kế hoạch ôn tập lại các chuyên đề yếu",
    ],
    [
      "Số học sinh có nguy cơ học tập cao (High Risk)",
      highRiskCount,
      "Học sinh",
      highRiskCount > 0 ? `Chiếm ${((highRiskCount / totalStudents) * 100).toFixed(1)}% sĩ số` : "0% (Lớp học an toàn)",
      highRiskCount > 0 ? "Ưu tiên theo dõi đặc biệt và liên hệ phụ huynh" : "Lớp học duy trì tiến độ hoàn hảo",
    ],
    [
      "Tổng số lượt bài tập đã giao toàn lớp",
      summaryTotalAssigned,
      "Lượt bài",
      `${students.length > 0 ? (summaryTotalAssigned / students.length).toFixed(1) : 0} bài/học sinh`,
      "Khối lượng giao bài phù hợp phân phối chương trình",
    ],
    [
      "Tổng số bài tập đã nộp hoàn thành",
      summaryCompleted,
      "Lượt bài",
      `${summaryTotalAssigned > 0 ? ((summaryCompleted / summaryTotalAssigned) * 100).toFixed(1) : 0}% tổng lượt giao`,
      "Đã thu và chấm điểm đầy đủ",
    ],
    [
      "Số bài đang làm dở dang hoặc chưa nộp",
      summaryInProgress + summaryPending,
      "Lượt bài",
      summaryPending > 0 ? `Có ${summaryPending} bài chưa làm/quá hạn` : "Không có bài nộp muộn",
      summaryPending > 0 ? "Đôn đốc nộp trước thời hạn đóng bài" : "Học sinh nộp bài đúng tiến độ",
    ],
    [],
    ["2. PHÂN BỐ XẾP LOẠI HỌC LỰC TOÀN LỚP (GRADE DISTRIBUTION)"],
    ["Xếp Loại Học Lực", "Khung Điểm Quy Chuẩn", "Số Lượng (HS)", "Tỷ Lệ (%)", "Thanh Trực Quan (Tỷ Trọng)", "Đánh Giá Sư Phạm"],
    ["Xuất sắc", "Từ 9.0 đến 10.0 điểm", countXuatSac, pctXuatSac, visualProgressBar(pctXuatSac), "Năng lực vượt trội, tư duy sắc bén"],
    ["Giỏi", "Từ 8.0 đến < 9.0 điểm", countGioi, pctGioi, visualProgressBar(pctGioi), "Nắm vững toàn diện kiến thức môn học"],
    ["Khá", "Từ 6.5 đến < 8.0 điểm", countKha, pctKha, visualProgressBar(pctKha), "Đạt chuẩn yêu cầu cần đạt, cần phát huy"],
    ["Trung bình", "Từ 5.0 đến < 6.5 điểm", countTB, pctTB, visualProgressBar(pctTB), "Cần củng cố phương pháp giải và kỹ năng làm bài"],
    ["Cần củng cố", "Dưới 5.0 điểm", countYeu, pctYeu, visualProgressBar(pctYeu), "Hổng kiến thức cơ bản, cần hỗ trợ bổ trợ"],
    ["Chưa có điểm", "Chưa nộp bài nào", countNoScore, pctNoScore, visualProgressBar(pctNoScore), "Cần đôn đốc tham gia làm bài tập đầy đủ"],
    ["TỔNG CỘNG", "Toàn bộ học sinh", students.length, 100, "██████████ 100.0%", "Sĩ số học sinh lớp"],
    [],
    ["3. TOP HỌC SINH XUẤT SẮC DẪN ĐẦU LỚP (TOP PERFORMERS)"],
    ["Hạng", "Mã Học Sinh", "Họ Và Tên", "Tên Đăng Nhập", "Điểm TB", "Tỷ Lệ Hoàn Thành (%)", "Bài Đã Nộp", "Xếp Loại", "Ghi Nhận"],
  ];

  // Top performers list
  const topStudents = [...students]
    .map((s) => ({ student: s, summary: studentSummaries.get(s.studentId) }))
    .filter((item) => item.summary && item.summary.averageScore !== null)
    .sort((a, b) => {
      const diff = (b.summary?.averageScore ?? 0) - (a.summary?.averageScore ?? 0);
      if (diff !== 0) return diff;
      return (b.summary?.completionRate ?? 0) - (a.summary?.completionRate ?? 0);
    })
    .slice(0, 5);

  if (topStudents.length === 0) {
    summaryRows.push(["-", "-", "Chưa có dữ liệu bài nộp đủ điều kiện xếp hạng", "-", "-", "-", "-", "-", "-"]);
  } else {
    topStudents.forEach((item, idx) => {
      const sum = item.summary!;
      const rankBadge = idx === 0 ? "🥇 Thủ khoa lớp" : idx === 1 ? "🥈 Á khoa 1" : idx === 2 ? "🥉 Á khoa 2" : `Top ${idx + 1}`;
      summaryRows.push([
        idx + 1,
        item.student.studentId,
        item.student.fullName,
        `@${item.student.username}`,
        sum.averageScore !== null ? Number(sum.averageScore.toFixed(1)) : "-",
        Number(sum.completionRate.toFixed(1)),
        sum.completedCount,
        sum.averageScore !== null && sum.averageScore >= 9.0 ? "Xuất sắc" : "Giỏi",
        rankBadge,
      ]);
    });
  }

  summaryRows.push([]);
  summaryRows.push(["4. QUY ƯỚC ĐÁNH GIÁ SƯ PHẠM & HỆ THỐNG EDUTWIN"]);
  summaryRows.push(["Thuật Ngữ / Chỉ Số", "Quy Chuẩn Hệ Thống", "Ý Nghĩa Áp Dụng"]);
  summaryRows.push(["Điểm Trung Bình", "Trung bình cộng các bài tập có kết quả chấm chính thức", "Đánh giá chuẩn năng lực hiện tại của học viên"]);
  summaryRows.push(["Tỷ Lệ Hoàn Thành", "Số bài đã hoàn thành chia cho tổng số bài tập được giao", "Đánh giá mức độ chuyên cần và tự giác học tập"]);
  summaryRows.push(["Xếp Loại Giỏi / Xuất Sắc", "Điểm trung bình >= 8.0 (>= 9.0 là Xuất sắc)", "Học sinh nắm vững kiến thức, tư duy tốt"]);
  summaryRows.push(["Đánh Giá Rủi Ro", "Mức độ nguy cơ không đạt mục tiêu điểm số hoặc trễ hạn", "Cảnh báo sớm để giáo viên can thiệp sư phạm kịp thời"]);
  summaryRows.push(["Dự Báo AI Digital Twin", "Điểm dự báo dựa trên mô hình năng lực thích ứng", "Giúp học sinh và giáo viên điều chỉnh lộ trình học"]);

  const wsSummary = XLSX.utils.aoa_to_sheet(summaryRows);
  wsSummary["!cols"] = [
    { wch: 38 }, // Cột 1
    { wch: 22 }, // Cột 2
    { wch: 18 }, // Cột 3
    { wch: 30 }, // Cột 4
    { wch: 32 }, // Cột 5
    { wch: 35 }, // Cột 6
    { wch: 20 }, // Cột 7
    { wch: 20 }, // Cột 8
    { wch: 32 }, // Cột 9
  ];
  wsSummary["!merges"] = [
    { s: { r: 0, c: 0 }, e: { r: 0, c: 4 } },
  ];
  wsSummary["!rows"] = [
    { hpt: 28 }, // Tiêu đề lớn
    { hpt: 20 },
    { hpt: 20 },
    { hpt: 12 },
    { hpt: 24 }, // Header section 1
    { hpt: 22 },
  ];

  XLSX.utils.book_append_sheet(wb, wsSummary, "Tong_Quan_Lop");

  // --- SHEET 3: Danh Sách Học Sinh Cần Phụ Đạo & Bổ Trợ (Tutoring & Intervention) ---
  const supportRows: (string | number)[][] = [
    ["DANH SÁCH HỌC SINH CẦN PHỤ ĐẠO & THEO DÕI BỔ TRỢ KIẾN THỨC"],
    [`Lớp học: ${classInfo.className}`, `Môn học: ${classInfo.subject?.subjectName || "Đa môn"}`, `Thời gian: ${exportTime}`, `Số lượng cần hỗ trợ: ${studentsNeedingSupport.length} học sinh`],
    [],
    [
      "STT",
      "Mã Học Sinh",
      "Họ Và Tên",
      "Tên Đăng Nhập",
      "Khối Lớp",
      "Điểm TB",
      "Hoàn Thành (%)",
      "Bài Đang Làm",
      "Chưa Làm / Quá Hạn",
      "Mức Độ Rủi Ro",
      "Chẩn Đoán Khó Khăn Sư Phạm",
      "Kế Hoạch Bổ Trợ Đề Xuất",
      "Ghi Chú Tiến Độ / Ký Nhận",
    ],
  ];

  if (studentsNeedingSupport.length === 0) {
    supportRows.push([
      "-",
      "-",
      "Toàn bộ học sinh trong lớp đều đạt tiến độ học tập an toàn",
      "-",
      "-",
      "-",
      "-",
      "-",
      "-",
      "Tiến độ an toàn",
      "Không có học sinh trong diện nguy cơ học tập",
      "Duy trì phong độ học tập hiện tại",
      "Đã xác nhận",
    ]);
  } else {
    studentsNeedingSupport.forEach((item, idx) => {
      const sum = item.summary;
      supportRows.push([
        idx + 1,
        item.student.studentId,
        item.student.fullName,
        `@${item.student.username}`,
        `Khối ${item.student.gradeLevel}`,
        sum.averageScore !== null ? Number(sum.averageScore.toFixed(1)) : "-",
        Number(sum.completionRate.toFixed(1)),
        sum.inProgressCount,
        sum.notStartedCount + sum.overdueCount,
        assessmentLabel(sum),
        item.reason,
        item.action,
        "",
      ]);
    });
  }

  const wsSupport = XLSX.utils.aoa_to_sheet(supportRows);
  wsSupport["!cols"] = [
    { wch: 6 },   // STT
    { wch: 14 },  // Mã Học Sinh
    { wch: 25 },  // Họ Và Tên
    { wch: 18 },  // Tên Đăng Nhập
    { wch: 10 },  // Khối Lớp
    { wch: 12 },  // Điểm TB
    { wch: 16 },  // Hoàn Thành (%)
    { wch: 14 },  // Bài Đang Làm
    { wch: 18 },  // Chưa Làm / Quá Hạn
    { wch: 20 },  // Mức Độ Rủi Ro
    { wch: 38 },  // Chẩn Đoán Khó Khăn Sư Phạm
    { wch: 42 },  // Kế Hoạch Bổ Trợ Đề Xuất
    { wch: 24 },  // Ghi Chú Tiến Độ / Ký Nhận
  ];
  wsSupport["!autofilter"] = {
    ref: XLSX.utils.encode_range({ s: { r: 3, c: 0 }, e: { r: supportRows.length - 1, c: 12 } }),
  };
  wsSupport["!views"] = [{ state: "frozen", xSplit: 3, ySplit: 4, topLeftCell: "D5", activeCell: "D5" }];
  wsSupport["!merges"] = [
    { s: { r: 0, c: 0 }, e: { r: 0, c: 6 } },
  ];

  XLSX.utils.book_append_sheet(wb, wsSupport, "Hoc_Sinh_Can_Bo_Tro");

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
      const scoreVal = rec.score == null ? "" : Number(rec.score.toFixed(1));

      const gradeRank =
        rec.resultStatus === "Final" && rec.score != null
          ? rec.score >= 8.0
            ? "Giỏi"
            : rec.score >= 6.5
            ? "Khá"
            : rec.score >= 5.0
            ? "Đạt"
            : "Chưa đạt"
          : "Chưa chốt điểm";

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
    ["Tỷ lệ hoàn thành bài tập", `${summary.completionRate.toFixed(1)}%`, "%", assessmentLabel(summary)],
    ["Điểm trung bình các bài", summary.averageScore !== null ? Number(summary.averageScore.toFixed(1)) : "Chưa có", "Thang 10", summary.averageScore === null ? assessmentLabel(summary) : summary.averageScore >= 8.0 ? "Học lực Giỏi" : summary.averageScore >= 6.5 ? "Học lực Khá" : "Cần bồi dưỡng"],
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
