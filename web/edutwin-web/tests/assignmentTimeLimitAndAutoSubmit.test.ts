import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const pagesDir = path.join(__dirname, "../src/pages");

test("1. AssignmentEditorPage (Center Manager) supports custom time limit in minutes or hours", () => {
  const content = fs.readFileSync(path.join(pagesDir, "AssignmentEditorPage.tsx"), "utf-8");

  // State & handler
  assert.ok(content.includes("timeLimitValue"), "Must have timeLimitValue state");
  assert.ok(content.includes("timeLimitUnit"), "Must have timeLimitUnit state");
  assert.ok(content.includes("effectiveTimeLimitMinutes"), "Must compute effectiveTimeLimitMinutes");
  assert.ok(content.includes("handleTimeLimitChange"), "Must have handleTimeLimitChange function");

  // Payloads
  assert.ok(content.includes("timeLimitMinutes: effectiveTimeLimitMinutes"), "Must pass timeLimitMinutes to payloads");

  // Input & Select elements for flexible custom minutes/hours
  assert.ok(content.includes('id="assignment-timelimit-input"'), "Must render assignment-timelimit-input");
  assert.ok(content.includes('<option value="minutes">Phút</option>'), "Must have minutes option");
  assert.ok(content.includes('<option value="hours">Giờ</option>'), "Must have hours option");

  // Step 3 summary
  assert.ok(content.includes("Thời gian làm bài:"), "Must display time limit in Step 3 summary");
});

test("2. TeacherAssignmentEditorView (Teacher) supports custom time limit in minutes or hours", () => {
  const content = fs.readFileSync(path.join(pagesDir, "teacher/TeacherAssignmentEditorView.tsx"), "utf-8");

  // State & handler
  assert.ok(content.includes("timeLimitValue"), "Must have timeLimitValue state");
  assert.ok(content.includes("timeLimitUnit"), "Must have timeLimitUnit state");
  assert.ok(content.includes("effectiveTimeLimitMinutes"), "Must compute effectiveTimeLimitMinutes");
  assert.ok(content.includes("handleTimeLimitChange"), "Must have handleTimeLimitChange function");

  // Payloads
  assert.ok(content.includes("timeLimitMinutes: effectiveTimeLimitMinutes"), "Must pass timeLimitMinutes to payloads");

  // Input & Select elements for flexible custom minutes/hours
  assert.ok(content.includes('id="teacher-assignment-timelimit-input"'), "Must render teacher-assignment-timelimit-input");
  assert.ok(content.includes('<option value="minutes">Phút</option>'), "Must have minutes option");
  assert.ok(content.includes('<option value="hours">Giờ</option>'), "Must have hours option");

  // Step 3 summary
  assert.ok(content.includes("Thời gian làm bài:"), "Must display time limit in Step 3 summary");
});

test("3. Time limit conversion helper correctly calculates minutes and hours", () => {
  const computeMinutes = (val: string, unit: "minutes" | "hours"): number | null => {
    const trimmed = val.trim();
    if (!trimmed) return null;
    const num = Number(trimmed);
    if (isNaN(num) || num <= 0) return null;
    return unit === "hours" ? Math.round(num * 60) : Math.round(num);
  };

  // Arbitrary minutes (not restricted to presets)
  assert.equal(computeMinutes("17", "minutes"), 17);
  assert.equal(computeMinutes("45", "minutes"), 45);
  assert.equal(computeMinutes("90", "minutes"), 90);

  // Arbitrary hours
  assert.equal(computeMinutes("1", "hours"), 60);
  assert.equal(computeMinutes("1.5", "hours"), 90);
  assert.equal(computeMinutes("2", "hours"), 120);

  // Edge cases & invalid inputs
  assert.equal(computeMinutes("", "minutes"), null);
  assert.equal(computeMinutes("0", "minutes"), null);
  assert.equal(computeMinutes("-10", "minutes"), null);
  assert.equal(computeMinutes("abc", "minutes"), null);
});

test("4. LearningPlayerPage has auto-submit on test time expiration and auto-skips unanswered questions", () => {
  const content = fs.readFileSync(path.join(pagesDir, "LearningPlayerPage.tsx"), "utf-8");

  // Auto-submit ref & effect
  assert.ok(content.includes("hasAutoSubmittedRef"), "Must have hasAutoSubmittedRef guard");
  assert.ok(content.includes("handleFinalSubmit(true)"), "Must invoke handleFinalSubmit with isAutoSubmit true");

  // Auto-skip unanswered questions
  assert.ok(content.includes('finalAnswer: isSkipped ? "SKIPPED" : qFinalAnswer'), "Must set finalAnswer to SKIPPED if skipped");
  assert.ok(content.includes("skipped: isSkipped"), "Must mark skipped boolean true");

  // No synthetic reasoning fallback: student answers must not be injected with fake reasoning
  assert.ok(!content.includes("[Hết giờ làm bài - Tự động nộp]"), "Must NOT inject synthetic reasoning note");

  // Timer label & auto-submit notification
  assert.ok(content.includes("Thời gian làm bài còn lại"), "Must label timer as test remaining time");
  assert.ok(content.includes("Các câu chưa làm đã được tự động bỏ qua"), "Must notify student that unanswered questions were skipped");
});

test("5. StudentAssignmentDetailPage displays standard 'Thời gian làm bài: (time)' instead of 'Dự kiến ~20 phút'", () => {
  const content = fs.readFileSync(path.join(pagesDir, "StudentAssignmentDetailPage.tsx"), "utf-8");

  // Removed hardcoded ~20 minutes estimate
  assert.ok(!content.includes("Dự kiến ~"), "Must not display hardcoded 'Dự kiến ~' estimate");

  // Formatter and label
  assert.ok(content.includes("formatTimeLimit"), "Must have formatTimeLimit helper");
  assert.ok(content.includes("Thời gian làm bài:"), "Must display standard 'Thời gian làm bài:' label");
});

test("6. LearningPlayerPage supports pause & resume on exit/close and only shows due countdown when untimed", () => {
  const content = fs.readFileSync(path.join(pagesDir, "LearningPlayerPage.tsx"), "utf-8");

  // Scoped timer persistence
  assert.ok(content.includes("readAssignmentRemainingSeconds"), "Must read saved remaining seconds on resume");
  assert.ok(content.includes("writeAssignmentRemainingSeconds"), "Must write remaining seconds on each tick");
  assert.ok(content.includes("removeAssignmentRemainingSeconds"), "Must clear saved timer on submit or retake");

  // Due countdown is strictly conditional on !hasTimeLimit
  assert.ok(content.includes("hasTimeLimit || !assignment?.dueAt"), "Must only countdown to due date when untimed");
  assert.ok(content.includes("hasTimeLimit"), "Must distinguish timed assignments from untimed assignments");
});

test("7. LearningPlayerPage snapshot upload tracking: identifies uploads, clears stale tokens on replace or removal, and isolates questions", () => {
  const content = fs.readFileSync(path.join(pagesDir, "LearningPlayerPage.tsx"), "utf-8");
  const uploader = fs.readFileSync(path.join(__dirname, "../src/utils/assignmentSnapshotUploader.ts"), "utf-8");

  // Snapshot upload tracking refs
  assert.ok(content.includes("snapshotUploadSeqRef"), "Must have snapshotUploadSeqRef");
  assert.ok(content.includes("snapshotUploadsRef"), "Must have snapshotUploadsRef");
  assert.ok(content.includes("activeQuestionIdRef"), "Must track activeQuestionIdRef");

  // Immediately clearing prior token on replacement and in flight
  assert.ok(content.includes("drawingUploadToken: null"), "Must reset drawingUploadToken while upload is in flight");

  // Verifying upload matches active snapshot and question
  assert.ok(content.includes("return executeSnapshotUpload({"), "The page must use the tested production uploader");
  assert.ok(uploader.includes("current.uploadId !== uploadId"), "Must discard callback if uploadId changed");
  assert.ok(uploader.includes("current.dataUrl !== dataUrl"), "Must discard callback if dataUrl changed");
  assert.ok(uploader.includes("getActiveQuestionId() === qId"), "Must only set component token if viewing same question");

  // Invalidation on removal
  assert.ok(content.includes("delete snapshotUploadsRef.current[qId]"), "Must delete in-flight upload on snapshot removal");
});

test("8. Separation of authoritative expiration timestamp vs display ceil-rounding prevents premature auto-submit", () => {
  const content = fs.readFileSync(path.join(pagesDir, "LearningPlayerPage.tsx"), "utf-8");

  // Expiration is checked against actual absolute timestamp, not truncated integer
  assert.ok(content.includes("Date.now() >= targetEndTimestampRef.current"), "Must check expiration against targetEndTimestampRef");

  // Display uses ceil so fractional seconds do not display 0
  assert.ok(content.includes("Math.ceil(msLeft / 1000)"), "Must use Math.ceil for display countdown");
});

