import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import { averageMeasuredMastery, formatMasteryDelta, isWeakTopic, twinPercent } from "../src/utils/topicMastery.ts";
import { isCloseLabel } from "../src/utils/notifyToast.ts";

const read = (path: string) => fs.readFileSync(new URL(`../src/${path}`, import.meta.url), "utf8");

test("twin values are percentages 0–100: shown as is, clamped, never multiplied by 100", () => {
  assert.equal(twinPercent(5), 5, "the 500% case: stored 5 means 5%");
  assert.equal(twinPercent(1), 1, "the 100% 'Thành thạo' case: stored 1 means 1%");
  assert.equal(twinPercent(100), 100, "the 10000% change-answer case: stored 100 means 100%");
  assert.equal(twinPercent(87.6), 88);
  assert.equal(twinPercent(130), 100);
  assert.equal(twinPercent(-4), 0);
  assert.equal(twinPercent(Number.NaN), 0);
  assert.equal(twinPercent(undefined), 0);
});

test("weak topics need evidence; average ignores unmeasured topics; deltas are percentage points", () => {
  assert.equal(isWeakTopic({ masteryPercentage: 5, evidenceCount: 3 }), true);
  assert.equal(isWeakTopic({ masteryPercentage: 0, evidenceCount: 0 }), false, "no evidence is not a knowledge gap");
  assert.equal(isWeakTopic({ masteryPercentage: 60, evidenceCount: 2 }), false);
  assert.equal(isWeakTopic({ masteryPercentage: 59.6, evidenceCount: 2 }), false, "rounded like the displayed value (60%)");
  assert.equal(averageMeasuredMastery([{ masteryPercentage: 80, evidenceCount: 2 }, { masteryPercentage: 40, evidenceCount: 1 }, { masteryPercentage: 0, evidenceCount: 0 }]), 60);
  assert.equal(averageMeasuredMastery([{ masteryPercentage: 0, evidenceCount: 0 }]), null);
  assert.equal(formatMasteryDelta(2.5), "2,5");
  assert.equal(formatMasteryDelta(-10), "-10");
  assert.equal(formatMasteryDelta(0.04), "0");
});

test("teacher twin pages read the 0–100 scale (no *100, thresholds in percent, unmeasured shown as no data)", () => {
  for (const path of ["pages/teacher/TeacherStudentTwinView.tsx", "pages/teacher/TeacherStudentManagementView.tsx"]) {
    const source = read(path);
    assert.doesNotMatch(source, /(masteryPercentage|skipRate|changeAnswerRate|delta) \* 100/, path);
    assert.doesNotMatch(source, /masteryPercentage [<>]=? 0\.\d/, path);
    assert.match(source, /twinPercent\(/, path);
  }
  const twin = read("pages/teacher/TeacherStudentTwinView.tsx");
  assert.match(twin, /statusLabel = "Chưa có dữ liệu";/);
  assert.match(twin, /twinPercent\(behavior\.skipRate\) > 30/);
  const management = read("pages/teacher/TeacherStudentManagementView.tsx");
  assert.match(management, /topic\.evidenceCount > 0 \? `\$\{pct\}%` : "Chưa có dữ liệu"/);
});

test("close buttons are recognised so closing a toast also clears the page message", () => {
  for (const label of ["✕", "×", " Đóng ", "Đóng thông báo", "close"]) assert.ok(isCloseLabel(label), label);
  for (const label of ["Thử lại", "Sao chép", "Tải lại"]) assert.ok(!isCloseLabel(label), label);
});

test("page notifications are toast-only across the app; load failures and field/dialog errors stay inline", () => {
  const css = fs.readFileSync(new URL("../src/index.css", import.meta.url), "utf8");
  assert.match(css, /\[data-notify="toast"\] \{\s*display: none !important;/);
  const host = read("components/AlertToastHost.tsx");
  assert.match(host, /if \(toast\.tone === "error"\) continue; \/\/ errors stay until closed or cleared by the page/);
  assert.match(host, /role=\{toast\.source \? \(toast\.tone === "error" \? "alert" : "status"\) : undefined\}/);
  assert.match(host, /className="pointer-events-none fixed bottom-4 right-4/);
  assert.match(read("components/teacher/TeacherPrimitives.tsx"), /data-notify=\{notify \? "toast" : undefined\}/);

  // Action errors (save/delete/submit) → toast.
  const actionPanels: [string, string][] = [
    ["pages/teacher/TeacherAssignmentEditorView.tsx", "formError"], ["pages/teacher/TeacherAssignmentListView.tsx", "actionError"],
    ["pages/teacher/TeacherAssignmentProgressView.tsx", "actionError"], ["pages/teacher/TeacherQuestionBankView.tsx", "actionError"],
    ["pages/teacher/TeacherQuestionEditorView.tsx", "formError"],
  ];
  for (const [path, state] of actionPanels)
    assert.match(read(path), new RegExp(`<TeacherSafeErrorPanel\\s+notify\\s+error=\\{${state}\\.message\\}`), path);
  for (const path of ["pages/ClassListPage.tsx", "pages/SubjectListPage.tsx", "pages/TeacherListPage.tsx", "pages/StudentListPage.tsx",
    "pages/AuthorizationManagementPage.tsx", "pages/teacher/TeacherCurriculumEditorView.tsx", "pages/teacher/TeacherCurriculumListView.tsx",
    "pages/teacher/TeacherKnowledgeGraphView.tsx", "pages/teacher/TeacherAssignmentListView.tsx", "pages/teacher/TeacherQuestionBankView.tsx"])
    assert.match(read(path), /data-notify="toast"/, path);

  // Load failures with a retry replace the page content and stay where they are.
  assert.match(read("pages/teacher/TeacherKnowledgeGraphView.tsx"), /<TeacherSafeErrorPanel\s+error=\{graphError\}/);
  assert.doesNotMatch(read("components/centerManager/CenterManagerPrimitives.tsx"), /data-notify/);
});
