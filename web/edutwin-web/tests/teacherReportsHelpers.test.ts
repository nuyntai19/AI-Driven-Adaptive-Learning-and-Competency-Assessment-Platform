import assert from "node:assert/strict";
import test from "node:test";
import * as XLSX from "xlsx";
import {
  buildClassReportWorkbook,
  buildStudentReportWorkbook,
} from "../src/pages/teacher/teacherExcelReports.ts";
import {
  buildClassReportCsv,
  buildClassReportExcelHtml,
  classSubmissionProgress,
  filterReportPeriod,
  simulateSubjectGrade,
  type StudentAcademicSummary,
  type StudentAssignmentRecord,
} from "../src/pages/teacher/teacherReportsHelpers.ts";
import type { StudentDto, ClassDto, StudentSubjectGoalDto } from "../src/types/organization.ts";

const mockClass: ClassDto = {
  classId: "class-12a1",
  className: "12A1",
  academicYear: "2025-2026",
  status: "Active",
  subject: {
    subjectId: "toan-12",
    subjectName: "Toán Học",
  },
  teacher: {
    teacherId: "teacher-01",
    displayName: "Thầy Nguyễn Văn A",
  },
  studentCount: 2,
  rowVersion: "1",
};

const mockStudents: StudentDto[] = [
  {
    studentId: "hs-01",
    fullName: "Trần Minh Quân",
    username: "quan.tm",
    gradeLevel: 12,
    status: "Active",
    activeClassCount: 1,
    rowVersion: "1",
  },
  {
    studentId: "hs-02",
    fullName: "Lê Thị Thảo",
    username: "thao.lt",
    gradeLevel: 12,
    status: "Active",
    activeClassCount: 1,
    rowVersion: "1",
  },
];

const mockStudentSummaries = new Map<string, StudentAcademicSummary>([
  [
    "hs-01",
    {
      totalAssigned: 5,
      completedCount: 4,
      inProgressCount: 1,
      notStartedCount: 0,
      overdueCount: 0,
      completionRate: 80,
      averageScore: 8.5,
      minScore: 8.0,
      maxScore: 9.0,
      records: [
        {
          assignmentId: "bt-01",
          title: "Khảo sát hàm số nâng cao",
          subjectName: "Toán Học",
          dueAt: "2026-10-15T00:00:00.000Z",
          status: "Completed",
          completedQuestionCount: 10,
          totalQuestionCount: 10,
          score: 9.0,
          maxScore: 10,
          submittedAt: "2026-10-14T08:00:00.000Z",
          feedbackNote: "Làm bài rất tốt!",
        },
      ],
    },
  ],
  [
    "hs-02",
    {
      totalAssigned: 5,
      completedCount: 2,
      inProgressCount: 1,
      notStartedCount: 2,
      overdueCount: 1,
      completionRate: 40,
      averageScore: 4.5,
      minScore: 4.0,
      maxScore: 5.0,
      records: [],
    },
  ],
]);

const mockGoals: StudentSubjectGoalDto[] = [
  {
    goalId: "goal-01",
    studentId: "hs-01",
    subjectId: "toan-12",
    targetScore: 9.0,
    currentPredictedScore: 8.2,
    riskScore: 0.25,
    remainingDays: 45,
    rowVersion: "1",
  },
];

const mockSubjectsMap = new Map<string, string>([["toan-12", "Toán Học"]]);

const unassigned: StudentAcademicSummary = {
  totalAssigned: 0, completedCount: 0, inProgressCount: 0, notStartedCount: 0, overdueCount: 0,
  completionRate: 0, averageScore: null, minScore: null, maxScore: null, records: [],
};

test("one targeted student submits: class submission rate is 1/1, not 1/2 of the roster", () => {
  const summaries = new Map([
    ["hs-01", { ...unassigned, totalAssigned: 1, completedCount: 1, completionRate: 100 }],
    ["hs-02", unassigned],
  ]);
  assert.deepEqual(classSubmissionProgress(mockStudents, summaries), { totalAssigned: 1, submittedCount: 1, rate: 100 });
  summaries.delete("hs-02");
  assert.equal(classSubmissionProgress(mockStudents, summaries).rate, 100);
});

test("submission rate uses actual target counts when students receive different numbers of assignments", () => {
  const summaries = new Map([
    ["hs-01", { ...unassigned, totalAssigned: 1, completedCount: 1, completionRate: 100 }],
    ["hs-02", { ...unassigned, totalAssigned: 3, completedCount: 0, completionRate: 0 }],
    ["outside-roster", { ...unassigned, totalAssigned: 20, completedCount: 20, completionRate: 100 }],
  ]);
  assert.deepEqual(classSubmissionProgress(mockStudents, summaries), { totalAssigned: 4, submittedCount: 1, rate: 25 });
  assert.equal(classSubmissionProgress([mockStudents[0]], summaries).rate, 100);
});

test("unsubmitted targets stay in the denominator and empty assignments never divide by zero", () => {
  const summaries = new Map([
    ["hs-01", { ...unassigned, totalAssigned: 1, completedCount: 1, completionRate: 100 }],
    ["hs-02", { ...unassigned, totalAssigned: 1, completedCount: 0, inProgressCount: 1 }],
  ]);
  assert.equal(classSubmissionProgress(mockStudents, summaries).rate, 50);
  assert.equal(classSubmissionProgress([mockStudents[1]], summaries).rate, 0);
  assert.equal(classSubmissionProgress(mockStudents, new Map([["hs-01", unassigned], ["hs-02", unassigned]])).rate, 0);
  assert.equal(classSubmissionProgress([], summaries).rate, 0);
});

test("class CSV, HTML and XLSX overview share the targeted submission rate", () => {
  const summaries = new Map([
    ["hs-01", { ...unassigned, totalAssigned: 1, completedCount: 1, completionRate: 100 }],
    ["hs-02", unassigned],
  ]);
  const csv = buildClassReportCsv(mockClass, mockStudents, summaries, new Map());
  assert.equal(csv.find(row => row[0] === "Tỷ lệ hoàn thành bài tập chung:")?.[1], "100.0%");
  const html = buildClassReportExcelHtml(mockClass, mockStudents, summaries, new Map());
  assert.match(html, /Tỷ lệ nộp bài tập chung<\/td>\s*<td[^>]*>100\.0%<\/td>/);
  const workbook = buildClassReportWorkbook(mockClass, mockStudents, summaries);
  const rows = XLSX.utils.sheet_to_json<unknown[]>(workbook.Sheets.Tong_Quan_Lop, { header: 1 });
  assert.equal(rows.find(row => row[0] === "Tỷ lệ nộp bài tập chung")?.[1], 100);
});

test("buildClassReportWorkbook creates structured Excel workbook with dedicated columns and sheet", () => {
  const wb = buildClassReportWorkbook(mockClass, mockStudents, mockStudentSummaries);

  assert.ok(wb.SheetNames.includes("Bang_Diem_Lop"), "Workbook should contain Bang_Diem_Lop sheet");

  const ws = wb.Sheets["Bang_Diem_Lop"];
  assert.ok(ws, "Bang_Diem_Lop sheet should be present");

  assert.equal(ws["!cols"]?.length, 8);
  const data = XLSX.utils.sheet_to_json<(string | number)[]>(ws, { header: 1 });
  assert.deepEqual(data, [
    ["STT", "Mã học sinh", "Họ và tên", "Lớp", "Điểm thường xuyên", "Điểm giữa kỳ", "Điểm cuối kỳ", "Điểm TB học kỳ (mô phỏng)"],
    [1, mockStudents[0].studentId, mockStudents[0].fullName, mockClass.className, "", "", "", ""],
    [2, mockStudents[1].studentId, mockStudents[1].fullName, mockClass.className, "", "", "", ""],
  ]);
  assert.equal(ws["!autofilter"]?.ref, "A1:H3");
  assert.equal(ws["!views"]?.[0].state, "frozen");

  // Verify Sheet 2: Tong_Quan_Lop (Executive Dashboard)
  assert.ok(wb.SheetNames.includes("Tong_Quan_Lop"), "Workbook should contain Tong_Quan_Lop dashboard sheet");
  const wsSummary = wb.Sheets["Tong_Quan_Lop"];
  assert.ok(wsSummary["!cols"], "Tong_Quan_Lop should have formatted column widths");
  assert.ok(wsSummary["!merges"], "Tong_Quan_Lop should have header cell merges");
  const summaryData = XLSX.utils.sheet_to_json<string[]>(wsSummary, { header: 1 });
  assert.ok(summaryData.some((row) => row.includes("BÁO CÁO TỔNG QUAN TIẾN ĐỘ & PHÂN TÍCH HỌC LỰC LỚP HỌC - EDUTWIN PLATFORM")));
  assert.ok(summaryData.some((row) => row.includes("1. CHỈ SỐ HỌC VỤ & TIẾN ĐỘ CỐT LÕI (KEY METRICS DASHBOARD)")));
  assert.ok(summaryData.some((row) => row.includes("2. PHÂN BỐ XẾP LOẠI HỌC LỰC TOÀN LỚP (GRADE DISTRIBUTION)")));
  assert.ok(summaryData.some((row) => row.includes("3. TOP HỌC SINH XUẤT SẮC DẪN ĐẦU LỚP (TOP PERFORMERS)")));

  // Verify Sheet 3: Hoc_Sinh_Can_Bo_Tro (Intervention / Tutoring Sheet)
  assert.ok(wb.SheetNames.includes("Hoc_Sinh_Can_Bo_Tro"), "Workbook should contain Hoc_Sinh_Can_Bo_Tro tutoring sheet");
  const wsSupport = wb.Sheets["Hoc_Sinh_Can_Bo_Tro"];
  assert.ok(wsSupport["!cols"], "Hoc_Sinh_Can_Bo_Tro should have formatted column widths");
  assert.ok(wsSupport["!autofilter"], "Hoc_Sinh_Can_Bo_Tro should have autofilter configured");
  assert.ok(wsSupport["!views"], "Hoc_Sinh_Can_Bo_Tro should have freeze panes configured");
  const supportData = XLSX.utils.sheet_to_json<string[]>(wsSupport, { header: 1 });
  // hs-02 has averageScore 4.5 (< 5.0) and completion rate 40% (< 60%), so she must be in the tutoring list
  assert.ok(supportData.some((row) => row.includes("hs-02") && row.includes("Lê Thị Thảo")));
});

test("buildStudentReportWorkbook creates multi-sheet Excel workbook with clean columns", () => {
  const summary = mockStudentSummaries.get("hs-01")!;
  const wb = buildStudentReportWorkbook(
    mockStudents[0],
    mockClass,
    mockGoals,
    mockSubjectsMap,
    summary,
    "Tiếp thu bài nhanh, cần rèn luyện thêm bài toán cực trị."
  );

  assert.ok(wb.SheetNames.includes("Chi_Tiet_Bai_Tap"), "Should have Chi_Tiet_Bai_Tap sheet");
  assert.ok(wb.SheetNames.includes("Tong_Quan_Hoc_Vien"), "Should have Tong_Quan_Hoc_Vien sheet");

  const wsAssignments = wb.Sheets["Chi_Tiet_Bai_Tap"];
  assert.ok(wsAssignments["!cols"], "Column widths should be defined for assignments sheet");
  assert.equal(wsAssignments["!cols"].length, 13, "There should be 13 columns for assignments table");

  const assignmentsData = XLSX.utils.sheet_to_json<string[]>(wsAssignments, { header: 1 });
  const assignmentHeaders = assignmentsData[0];
  assert.equal(assignmentHeaders[0], "STT");
  assert.equal(assignmentHeaders[1], "Tên Bài Tập");
  assert.equal(assignmentHeaders[8], "Điểm Số");
  assert.equal(assignmentHeaders[12], "Lời Phê & Nhận Xét");

  const firstRecRow = assignmentsData[1];
  assert.equal(firstRecRow[0], 1);
  assert.equal(firstRecRow[1], "Khảo sát hàm số nâng cao");
  assert.equal(firstRecRow[8], 9);
  assert.equal(firstRecRow[12], "Làm bài rất tốt!");
});

test("buildClassReportCsv outputs uniform 12-column CSV structure", () => {
  const goalsMap = new Map<string, StudentSubjectGoalDto[]>();
  const rows = buildClassReportCsv(mockClass, mockStudents, mockStudentSummaries, goalsMap);

  assert.ok(rows.length > 5);
  // Find the header row starting with "STT"
  const headerRowIdx = rows.findIndex((r) => r[0] === "STT");
  assert.ok(headerRowIdx !== -1, "Header row with STT should be found");

  const headerRow = rows[headerRowIdx];
  assert.equal(headerRow.length, 12);
  assert.equal(headerRow[0], "STT");
  assert.equal(headerRow[1], "Họ và tên");

  const studentRow = rows[headerRowIdx + 1];
  assert.equal(studentRow.length, 12);
  assert.equal(studentRow[1], "Trần Minh Quân");
});

test("mô phỏng tính hệ số theo từng bài được chọn và bỏ thi thử khỏi điểm kỳ", () => {
  const entry = (assignmentId: string, kind: string, score: number): StudentAssignmentRecord => ({
    ...mockStudentSummaries.get("hs-01")!.records[0], assignmentId, kind, score,
    resultStatus: "Final", gradingPeriodDate: "2026-11-18T18:00:00Z",
  });
  const records = [
    entry("quiz1", "Regular", 10), entry("quiz2", "Regular", 8), entry("practice", "Regular", 2),
    entry("midterm", "Midterm", 7), entry("final", "Final", 8), entry("mock", "MockExam", 0),
  ];
  const selected = new Set(["quiz1", "quiz2"]);
  const result = simulateSubjectGrade(filterReportPeriod(records, "2026-11-19", "2026-11-19"), selected);
  assert.equal(result.score, 8);
  assert.equal(result.label, "Giỏi");
  assert.equal(result.byGroup[0].total, 2);
  assert.equal(simulateSubjectGrade(records.filter(record => record.kind !== "Final"), selected).score, null);
  assert.equal(simulateSubjectGrade([...records, entry("other-final", "Final", 9)], selected).score, null);
  records[0].resultStatus = "Provisional";
  assert.equal(simulateSubjectGrade(records, selected).score, null);
});

test("Excel exports selected term grades as numeric cells and keeps missing grades blank", () => {
  const summary = mockStudentSummaries.get("hs-01")!;
  const entry = (assignmentId: string, kind: string, score: number): StudentAssignmentRecord => ({
    ...summary.records[0], assignmentId, kind, score, resultStatus: "Final",
    gradingPeriodDate: "2026-11-18T18:00:00Z",
  });
  const final = entry("final", "Final", 0);
  const records = [
    entry("quiz", "Regular", 9),
    entry("unused", "Regular", 1),
    entry("midterm", "Midterm", 6),
    { ...entry("old-midterm", "Midterm", 8), gradingPeriodDate: "2026-11-12T09:00:00Z" },
    final,
  ];
  const summaries = new Map([
    ["hs-01", { ...summary, records }],
    ["hs-02", { ...summary, records: [{ ...final, resultStatus: "Provisional" as const }] }],
  ]);

  const workbook = buildClassReportWorkbook(mockClass, mockStudents, summaries, {
    from: "2026-11-19", to: "2026-11-19", selectedRegularIds: new Set(["quiz"]),
  });
  const exported = XLSX.read(XLSX.write(workbook, { bookType: "xlsx", type: "buffer" }), { type: "buffer" });
  const rows = XLSX.utils.sheet_to_json<(string | number)[]>(exported.Sheets.Bang_Diem_Lop, { header: 1 });
  assert.deepEqual(rows[1].slice(4, 8), [9, 6, 0, 3.5]);
  assert.deepEqual(rows[2].slice(4, 8), ["", "", "", ""]);
});

test("selected students export together on one sheet in class order", () => {
  const third = { ...mockStudents[0], studentId: "hs-03", fullName: "Nguyễn Minh An", username: "an.nm" };
  const summary = mockStudentSummaries.get("hs-01")!;
  const examRecords: StudentAssignmentRecord[] = [
    { ...summary.records[0], assignmentId: "regular", kind: "Regular", score: 4, resultStatus: "Final" },
    { ...summary.records[0], assignmentId: "midterm", kind: "Midterm", score: 8, resultStatus: "Final" },
    { ...summary.records[0], assignmentId: "final", kind: "Final", score: 9, resultStatus: "Final" },
  ];
  const summaries = new Map(mockStudentSummaries);
  summaries.set("hs-01", { ...summary, records: examRecords });
  const workbook = buildClassReportWorkbook(mockClass, [...mockStudents, third], summaries, {
    from: "", to: "", selectedRegularIds: new Set(["regular"]),
    selectedStudentIds: new Set(["hs-outside-class", "hs-03", "hs-01"]),
  });
  const exported = XLSX.read(XLSX.write(workbook, { bookType: "xlsx", type: "buffer" }), { type: "buffer" });

  assert.deepEqual(exported.SheetNames, ["Bang_Diem_Lop"]);
  const rows = XLSX.utils.sheet_to_json<(string | number)[]>(exported.Sheets.Bang_Diem_Lop, { header: 1 });
  assert.deepEqual(rows, [
    ["STT", "Mã học sinh", "Họ và tên", "Lớp", "Điểm thường xuyên", "Điểm giữa kỳ", "Điểm cuối kỳ", "Điểm TB học kỳ (mô phỏng)"],
    [1, mockStudents[0].studentId, mockStudents[0].fullName, mockClass.className, 4, 8, 9, 7.8],
    [2, third.studentId, third.fullName, mockClass.className, "", "", "", ""],
  ]);
});
