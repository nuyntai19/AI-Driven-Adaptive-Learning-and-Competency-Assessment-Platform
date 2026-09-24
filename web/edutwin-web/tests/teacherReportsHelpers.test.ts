import assert from "node:assert/strict";
import test from "node:test";
import * as XLSX from "xlsx";
import {
  buildClassReportWorkbook,
  buildStudentReportWorkbook,
  buildClassReportCsv,
  type StudentAcademicSummary,
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

test("buildClassReportWorkbook creates structured Excel workbook with dedicated columns and sheet", () => {
  const goalsMap = new Map<string, StudentSubjectGoalDto[]>([["hs-01", mockGoals]]);
  const wb = buildClassReportWorkbook(mockClass, mockStudents, mockStudentSummaries, goalsMap);

  assert.ok(wb.SheetNames.includes("Bang_Diem_Lop"), "Workbook should contain Bang_Diem_Lop sheet");

  const ws = wb.Sheets["Bang_Diem_Lop"];
  assert.ok(ws, "Bang_Diem_Lop sheet should be present");

  // Verify column widths exist
  assert.ok(ws["!cols"], "Column widths (!cols) should be defined");
  assert.equal(ws["!cols"].length, 21, "There should be 21 formatted columns");

  // Convert to 2D array and inspect headers
  const data = XLSX.utils.sheet_to_json<string[]>(ws, { header: 1 });
  assert.ok(data.length >= 2, "Should contain headers and student data rows");

  // Row index 0 (1st line) has table headers
  const headerRow = data[0];
  assert.equal(headerRow[0], "STT");
  assert.equal(headerRow[1], "Mã Học Sinh");
  assert.equal(headerRow[2], "Họ Và Tên");
  assert.equal(headerRow[3], "Tên Đăng Nhập");
  assert.equal(headerRow[11], "Tỷ Lệ Hoàn Thành (%)");
  assert.equal(headerRow[12], "Điểm Trung Bình");
  assert.equal(headerRow[20], "Trạng Thái Hoạt Động");

  // Row index 1 has student 1
  const student1Row = data[1];
  assert.equal(student1Row[0], 1);
  assert.equal(student1Row[1], "hs-01");
  assert.equal(student1Row[2], "Trần Minh Quân");
  assert.equal(student1Row[3], "@quan.tm");
  assert.equal(student1Row[11], 80);
  assert.equal(student1Row[12], 8.5);
  assert.equal(student1Row[15], "Giỏi");
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
