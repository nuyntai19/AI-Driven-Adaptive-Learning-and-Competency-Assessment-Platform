import assert from "node:assert/strict";
import fs from "node:fs";
import test from "node:test";
import { isAssignmentPastDue } from "../src/utils/assignmentReviewTiming.ts";

const read = (file: string) => fs.readFileSync(new URL(`../src/${file}`, import.meta.url), "utf8");

test("an assignment is past due only after a valid due date", () => {
  const now = Date.parse("2026-11-13T12:00:00Z");
  assert.equal(isAssignmentPastDue("2026-11-13T11:59:59Z", now), true);
  assert.equal(isAssignmentPastDue("2026-11-13T12:00:01Z", now), false);
  assert.equal(isAssignmentPastDue(null, now), false);
  assert.equal(isAssignmentPastDue("not a date", now), false);
});

test("overdue work is never offered as start/continue", () => {
  assert.match(read("pages/StudentAssignmentDetailPage.tsx"), /Đã quá hạn nộp · không thể làm tiếp/);
  assert.match(read("pages/StudentAssignmentsPage.tsx"), /isPastDue \? "Xem \(quá hạn\)"/);
  assert.match(read("pages/LearningPlayerPage.tsx"), /isAssignmentPastDue\(assignment\.dueAt\)/);
  assert.match(read("pages/StudentDashboardPage.tsx"), /!isAssignmentPastDue\(a\.dueAt\)/);
});

test("pedagogy advice never praises a student without evidence", () => {
  const view = read("pages/teacher/TeacherStudentTwinView.tsx");
  assert.match(view, /measuredTopics\.length === 0\s*\?/);
  assert.match(view, /Chưa đủ dữ liệu để nhận xét/);
  assert.doesNotMatch(view, /AI Advisor/);
});

test("multi-part checks show a waiting screen between parts", () => {
  const page = read("pages/StudentLearningPathPage.tsx");
  assert.match(page, /transitionNote && !mission/);
  assert.match(page, /Đang chuẩn bị phần kiểm tra tiếp theo/);
});
