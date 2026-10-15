import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import {
  studentClassesInView,
  reconcileStudentClassParam,
  formatStudentClassBadge,
  formatStudentProfileSubtitle,
  resolveStudentActiveDisplayClass,
} from "../src/utils/studentAcademicScope.ts";
import type { StudentAcademicContextDto } from "../src/types/dashboards.ts";

const classes: StudentAcademicContextDto["classes"] = [
  {classId:"current",subjectId:"math",className:"Toán 12",gradeLevel:12,isHistorical:false},
  {classId:"archived",subjectId:"math",className:"Lớp đã kết thúc",gradeLevel:11,isHistorical:true},
  {classId:"left",subjectId:"math",className:"Lớp vẫn hoạt động nhưng học sinh đã rời",gradeLevel:10,isHistorical:true},
];

test("current and class-history selections are disjoint, including an active class the student left",()=>{
  assert.deepEqual(studentClassesInView(classes,false).map(c=>c.classId),["current"]);
  assert.deepEqual(studentClassesInView(classes,true).map(c=>c.classId),["archived","left"]);
  assert.equal(classes.length,3);
});
test("history never falls back to a current class when there are no former classes",()=>{
  assert.deepEqual(studentClassesInView([classes[0]],true),[]);
  const params=new URLSearchParams("subjectId=math&classId=current&history=true&tab=radar");
  const next=reconcileStudentClassParam(params,null)!;
  assert.equal(next.has("classId"),false);assert.equal(next.get("history"),"true");assert.equal(next.get("tab"),"radar");
  assert.equal(params.get("classId"),"current");
});
test("stale bookmarks normalize to a permitted former class, not the stale current selection",()=>{
  const next=reconcileStudentClassParam(new URLSearchParams("subjectId=math&classId=current&history=true"),"archived")!;
  assert.equal(next.get("classId"),"archived");assert.equal(next.get("history"),"true");
  assert.equal(reconcileStudentClassParam(next,"archived"),null);
});
test("a subjectless view cannot retain an orphan class id",()=>{
  const next=reconcileStudentClassParam(new URLSearchParams("classId=current&history=true"),"archived")!;
  assert.equal(next.has("classId"),false);assert.equal(next.get("history"),"true");
});
test("context shows an honest empty history and only retries rejected class selections",()=>{
  const context=fs.readFileSync(new URL("../src/components/student/StudentAcademicContext.tsx",import.meta.url),"utf8");
  assert.match(context,/studentClassesInView\(query.data\?\.classes \?\? \[\], history\)/);
  assert.doesNotMatch(context,/history \|\| !c.isHistorical/);
  const api=fs.readFileSync(new URL("../src/api/dashboardsApi.ts",import.meta.url),"utf8");
  assert.match(api,/classId && isAxiosError\(error\) && error.response\?\.status === 404/);
  assert.match(context,/getReconciledStudentAcademicContext\(subject, classId, history\)/);
  assert.match(context,/reconcileStudentClassParam/);assert.match(context,/Chưa có lịch sử lớp học/);
  assert.match(context,/Giáo trình từng áp dụng/);assert.match(context,/history && classes.length > 0/);
  const list=fs.readFileSync(new URL("../src/pages/StudentAssignmentsPage.tsx",import.meta.url),"utf8");
  assert.match(list,/Chưa có bài tập trong lịch sử lớp học/);assert.doesNotMatch(list,/Tất cả bài tập đã hoàn thành/);
});

test("formatStudentClassBadge and formatStudentProfileSubtitle format Vietnamese school class labels naturally", () => {
  // Case 1: Already begins with 'Lớp'
  assert.equal(formatStudentClassBadge({ className: "Lớp Toán 11", gradeLevel: 11 }), "Lớp Toán 11");
  assert.equal(formatStudentProfileSubtitle({ className: "Lớp Toán 11", gradeLevel: 11 }), "Lớp Toán 11 · Học sinh");

  // Case 2: Begins with 'Khối'
  assert.equal(formatStudentClassBadge({ className: "Khối 11", gradeLevel: 11 }), "Khối 11");
  assert.equal(formatStudentProfileSubtitle({ className: "Khối 11", gradeLevel: 11 }), "Khối 11 · Học sinh");

  // Case 3: Does not begin with 'Lớp' (e.g. 11A1, 12B, Toán nâng cao)
  assert.equal(formatStudentClassBadge({ className: "11A1", gradeLevel: 11 }), "Lớp 11A1");
  assert.equal(formatStudentProfileSubtitle({ className: "11A1", gradeLevel: 11 }), "Lớp 11A1 · Học sinh");

  // Case 4: Missing class name but known gradeLevel
  assert.equal(formatStudentClassBadge({ className: "", gradeLevel: 11 }), "Khối 11");
  assert.equal(formatStudentProfileSubtitle({ className: "", gradeLevel: 11 }), "Khối 11 · Học sinh");

  // Case 5: Null / undefined
  assert.equal(formatStudentClassBadge(null), "");
  assert.equal(formatStudentProfileSubtitle(null), "Học sinh");
});

test("resolveStudentActiveDisplayClass prioritizes selected class, then active non-historical class", () => {
  const sampleClasses = [
    { classId: "c1", subjectId: "sub1", className: "Lớp Toán 11", gradeLevel: 11, isHistorical: false },
    { classId: "c2", subjectId: "sub2", className: "Lớp Tiếng Anh 11", gradeLevel: 11, isHistorical: false },
    { classId: "c3", subjectId: "sub1", className: "Lớp Toán 10 cũ", gradeLevel: 10, isHistorical: true },
  ];

  // Specific selected class ID takes priority
  assert.equal(resolveStudentActiveDisplayClass(sampleClasses, "c2", "")?.className, "Lớp Tiếng Anh 11");
  assert.equal(resolveStudentActiveDisplayClass(sampleClasses, null, "c2")?.className, "Lớp Tiếng Anh 11");

  // Default selection picks first active non-historical class
  assert.equal(resolveStudentActiveDisplayClass(sampleClasses, null, null)?.className, "Lớp Toán 11");

  // Empty list returns null
  assert.equal(resolveStudentActiveDisplayClass([], null, null), null);
});

test("StudentLayout header renders dynamic student class and does not hardcode Lớp 12A1 or mock name", () => {
  const layout = fs.readFileSync(new URL("../src/layouts/StudentLayout.tsx", import.meta.url), "utf8");
  assert.doesNotMatch(layout, /Lớp 12A1 · Học sinh/);
  assert.doesNotMatch(layout, /Nguyễn Văn An/);
  assert.doesNotMatch(layout, /an\.nguyen@edutwin\.edu\.vn/);
  assert.match(layout, /resolveStudentActiveDisplayClass/);
  assert.match(layout, /formatStudentProfileSubtitle/);
  assert.match(layout, /profileSubtitle/);
});

