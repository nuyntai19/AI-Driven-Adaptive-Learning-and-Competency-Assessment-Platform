import assert from "node:assert/strict";
import test from "node:test";
import { readFileSync } from "node:fs";
import { assessmentLabel, isAcademicHighRisk, reportScoreText, buildClassReportCsv, type StudentAcademicSummary } from "../src/pages/teacher/teacherReportsHelpers.ts";
import { buildStudentReportWorkbook } from "../src/pages/teacher/teacherExcelReports.ts";
import * as XLSX from "xlsx";
const blank: StudentAcademicSummary = { assessmentStatus:"Unknown", totalAssigned:0,completedCount:0,inProgressCount:0,notStartedCount:0,overdueCount:0,completionRate:0,averageScore:null,minScore:null,maxScore:null,records:[] };
test("no assignment and pending grading are not mislabeled high risk",()=>{
  assert.equal(isAcademicHighRisk(blank),false);
  assert.match(assessmentLabel(blank),/Chưa đủ/);
  assert.match(assessmentLabel({...blank,assessmentStatus:"PendingGrading"}),/chờ chốt/);
  assert.equal(isAcademicHighRisk({...blank,assessmentStatus:"HighRisk",averageScore:0}),true);
});
test("report score clearly separates provisional and final and preserves zero",()=>{
  const record = {assignmentId:"a",title:"A",dueAt:null,status:"Completed" as const,completedQuestionCount:10,totalQuestionCount:10,score:0};
  assert.equal(reportScoreText({...record,resultStatus:"Provisional"}),"0.0 / 10 (tạm thời)");
  assert.equal(reportScoreText({...record,resultStatus:"Final"}),"0.0 / 10 (đã chốt)");
  assert.equal(reportScoreText({...record,score:null}),"Chưa có điểm");
  const student={studentId:"s",username:"student",fullName:"Student",gradeLevel:10,status:"Active" as const,activeClassCount:1,rowVersion:"1"};
  const wb=buildStudentReportWorkbook(student,undefined,[],new Map(),{...blank,records:[{...record,score:9,resultStatus:"Provisional"}]});
  const rows=XLSX.utils.sheet_to_json(wb.Sheets["Chi_Tiet_Bai_Tap"],{header:1}) as unknown[][];
  assert.ok(rows.some(row=>row.includes(9)&&row.includes("Chưa chốt điểm")));
});
test("CSV class export retains all rows beyond the old page cap",()=>{
  const students=Array.from({length:125},(_,i)=>({studentId:`s${i}`,username:`u${i}`,fullName:`Student ${i}`,gradeLevel:10,status:"Active" as const,activeClassCount:1,rowVersion:"1"}));
  const cls={classId:"c",className:"Class",academicYear:"2026",status:"Active" as const,subject:{subjectId:"m",subjectName:"Math"},teacher:{teacherId:"t",displayName:"Teacher"},studentCount:125,rowVersion:"1"};
  const csv=buildClassReportCsv(cls,students,new Map(students.map(s=>[s.studentId,blank])),new Map());
  assert.ok(csv.some(row=>row.includes("Student 124")&&row.includes("Chưa đủ dữ liệu đánh giá")));
});
test("actual teacher view consumes one authoritative snapshot, keeps export error gates and paginates rendering",()=>{
  const source=readFileSync(new URL("../src/pages/teacher/TeacherStudentManagementView.tsx",import.meta.url),"utf8");
  assert.match(source,/academic-report/);assert.match(source,/!reportReady/);
  assert.match(source,/slice\(\(studentPage-1\)\*25/);
  assert.doesNotMatch(source,/getAssignmentProgress|calculatedScore|completedQuestions \/ totalQuestions/);
  const manager=readFileSync(new URL("../src/pages/ClassListPage.tsx",import.meta.url),"utf8");
  assert.match(manager,/lifecycleReason: lifecycleReason.trim/);
  assert.match(manager,/editStatus !== editingClass.status && !lifecycleReason.trim/);
  assert.match(manager,/ClassHistoryPanel/);
});
