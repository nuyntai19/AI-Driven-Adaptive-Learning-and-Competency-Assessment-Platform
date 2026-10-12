import assert from "node:assert/strict";
import test from "node:test";
import { readFileSync } from "node:fs";
import { serializeCsvCell, buildStudentReportCsv, type StudentAcademicSummary } from "../src/pages/teacher/teacherReportsHelpers.ts";
import { teacherNotesKey, readTeacherNotes } from "../src/utils/teacherNotesStorage.ts";
import { studentScopeChangeUrl, studentLearningPathReturnUrl } from "../src/utils/studentAcademicNavigation.ts";

test("return from a completed session preserves its original plan version, but scope changes drop it", () => {
  const id = "12345678-1234-1234-1234-123456789012";
  const params = new URLSearchParams("subjectId=s&classId=c&analysisJobId=old");
  assert.equal(studentLearningPathReturnUrl(params, `${id}-w01-s01`), `/hoc-tap/lo-trinh-hoc-tap?subjectId=s&classId=c&pathId=${id}`);
  assert.equal(studentLearningPathReturnUrl(params, "legacy"), "/hoc-tap/lo-trinh-hoc-tap?subjectId=s&classId=c");
  assert.equal(studentScopeChangeUrl("/hoc-tap/lo-trinh-hoc-tap", new URLSearchParams(`subjectId=s&classId=new&pathId=${id}`)),
    "/hoc-tap/lo-trinh-hoc-tap?subjectId=s&classId=new");
});

test("changing class during a path session removes the old session/job context", () => {
  assert.equal(studentScopeChangeUrl("/hoc-tap/luyen-tap", new URLSearchParams("subjectId=s&classId=b&sessionId=old&analysisJobId=1")),
    "/hoc-tap/luyen-tap?subjectId=s&classId=b");
});

test("CSV escapes text and neutralizes formulas without changing numeric negative scores", () => {
  for (const text of ["=1+1", "+cmd", "@SUM(1,1)", "-1+2", "\t=1+1", " \r@SUM(1,1)"])
    assert.equal(serializeCsvCell(text), `"'${text}"`);
  assert.equal(serializeCsvCell(-2), '"-2"');
  assert.equal(serializeCsvCell('Nguyễn "An"'), '"Nguyễn ""An"""');
  assert.equal(serializeCsvCell(null), '""');
});

test("CSV keeps provisional/final result status visible", () => {
  const summary: StudentAcademicSummary = { totalAssigned: 1, completedCount: 1, inProgressCount: 0, notStartedCount: 0, overdueCount: 0,
    completionRate: 100, averageScore: null, minScore: null, maxScore: null,
    records: [{ assignmentId: "a", title: "Audit assignment", status: "Completed", resultStatus: "Provisional", score: 9, dueAt: null,
      completedQuestionCount: 1, totalQuestionCount: 1 }] };
  const student = { studentId: "s", fullName: "Synthetic", username: "audit", gradeLevel: 10, status: "Active" as const, activeClassCount: 1, rowVersion: "1" };
  const row = buildStudentReportCsv(student, undefined, [], new Map(), summary).find(r => r.includes("Audit assignment"));
  assert.ok(row?.some(cell => String(cell).includes("tạm thời")));
  summary.records[0].resultStatus = "Final";
  assert.ok(buildStudentReportCsv(student, undefined, [], new Map(), summary).some(r => r.some(cell => String(cell).includes("đã chốt"))));
});

test("teacher notes are isolated by tenant and author; malformed values and legacy shared notes are excluded", () => {
  const values = new Map([["teacher_pedagogical_notes", '{"s":"legacy unknown author"}']]);
  const storage = { getItem: (key: string) => values.get(key) ?? null };
  const a = teacherNotesKey("c", "a")!;
  const b = teacherNotesKey("c", "b")!;
  const other = teacherNotesKey("other", "a")!;
  values.set(a, '{"s":"teacher a","bad":12}');
  assert.deepEqual(readTeacherNotes(a, storage), { s: "teacher a" });
  assert.deepEqual(readTeacherNotes(b, storage), {});
  assert.deepEqual(readTeacherNotes(other, storage), {});
  assert.deepEqual(readTeacherNotes(null, storage), {});
  values.set(b, "[]"); assert.deepEqual(readTeacherNotes(b, storage), {});
  values.set(b, "invalid"); assert.deepEqual(readTeacherNotes(b, storage), {});
});

test("teacher pedagogical notes view has migrated away from localStorage to backend MySQL API", () => {
  const code = readFileSync(new URL("../src/pages/teacher/TeacherStudentManagementView.tsx", import.meta.url), "utf8");
  assert.doesNotMatch(code, /teacherNotesStorage/, "TeacherStudentManagementView must not import teacherNotesStorage");
  assert.doesNotMatch(code, /localStorage\.setItem\(/, "TeacherStudentManagementView must not set items in localStorage");
});

