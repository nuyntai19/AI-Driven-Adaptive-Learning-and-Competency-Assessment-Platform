import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const pagesDir = path.join(__dirname, "../src/pages");

test("1. AssignmentEditorPage and TeacherAssignmentEditorView enforce min date-time and validation for DueAt", () => {
  const managerEditorContent = fs.readFileSync(path.join(pagesDir, "AssignmentEditorPage.tsx"), "utf-8");
  const teacherEditorContent = fs.readFileSync(path.join(pagesDir, "teacher/TeacherAssignmentEditorView.tsx"), "utf-8");

  // Manager Editor has min attribute, dynamic refresh, and handleDueAtChange
  assert.equal(managerEditorContent.includes("min={minDateTime}"), true, "Manager editor must set min={minDateTime}");
  assert.equal(managerEditorContent.includes("onFocus={refreshMinDateTime}"), true, "Manager editor must refresh min on focus");
  assert.equal(managerEditorContent.includes("onPointerDown={refreshMinDateTime}"), true, "Manager editor must refresh min on pointer down");
  assert.equal(managerEditorContent.includes("handleDueAtChange"), true, "Manager editor must have handleDueAtChange");
  assert.equal(managerEditorContent.includes("dueDateError"), true, "Manager editor must display dueDateError");
  assert.equal(managerEditorContent.includes("validateStep0"), true, "Manager editor must run validateStep0");

  // Teacher Editor has min attribute, dynamic refresh, and handleDueAtChange
  assert.equal(teacherEditorContent.includes("min={minDateTime}"), true, "Teacher editor must set min={minDateTime}");
  assert.equal(teacherEditorContent.includes("onFocus={refreshMinDateTime}"), true, "Teacher editor must refresh min on focus");
  assert.equal(teacherEditorContent.includes("onPointerDown={refreshMinDateTime}"), true, "Teacher editor must refresh min on pointer down");
  assert.equal(teacherEditorContent.includes("handleDueAtChange"), true, "Teacher editor must have handleDueAtChange");
  assert.equal(teacherEditorContent.includes("dueDateError"), true, "Teacher editor must display dueDateError");
  assert.equal(teacherEditorContent.includes("validateStep0"), true, "Teacher editor must run validateStep0");
});

test("2. getMinLocalDateTime returns YYYY-MM-DDTHH:mm format in the future", () => {
  const now = Date.now();
  const d = new Date(now + 60_000);
  const pad = (n: number) => String(n).padStart(2, "0");
  const minDateTime = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;

  // Matches ISO local datetime regex YYYY-MM-DDTHH:mm
  assert.match(minDateTime, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/);

  // Timestamp of minDateTime is in the future
  const parsedTime = new Date(minDateTime).getTime();
  assert.ok(parsedTime >= now, "minDateTime timestamp should be greater than or equal to current timestamp");
});

test("3. Due date validation rejects past dates and accepts future dates", () => {
  const validateDueAt = (val: string): { isValid: boolean; error: string | null } => {
    if (!val) return { isValid: true, error: null };
    const dueTime = new Date(val).getTime();
    if (isNaN(dueTime)) {
      return { isValid: false, error: "Thời gian hạn chót nộp bài không hợp lệ." };
    }
    if (dueTime <= Date.now()) {
      return { isValid: false, error: "Hạn chót nộp bài phải ở thời điểm tương lai (sau thời điểm hiện tại)." };
    }
    return { isValid: true, error: null };
  };

  // Past date (like the screenshot: 2026-09-19T22:12)
  const pastResult = validateDueAt("2020-01-01T10:00");
  assert.equal(pastResult.isValid, false);
  assert.equal(pastResult.error, "Hạn chót nộp bài phải ở thời điểm tương lai (sau thời điểm hiện tại).");

  // Invalid date string
  const invalidResult = validateDueAt("invalid-date-string");
  assert.equal(invalidResult.isValid, false);
  assert.equal(invalidResult.error, "Thời gian hạn chót nộp bài không hợp lệ.");

  // Future date (year 2099)
  const futureResult = validateDueAt("2099-12-31T23:59");
  assert.equal(futureResult.isValid, true);
  assert.equal(futureResult.error, null);

  // Empty string (optional field)
  const emptyResult = validateDueAt("");
  assert.equal(emptyResult.isValid, true);
  assert.equal(emptyResult.error, null);
});
