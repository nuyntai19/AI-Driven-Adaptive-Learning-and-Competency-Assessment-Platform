import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";

const source = (path: string) => fs.readFileSync(new URL(`../src/${path}`, import.meta.url), "utf8");

test("tenant security uses self endpoints, isolates profile cache and explicitly signs out after revocation", () => {
  const modal = source("components/PlatformSecurityModal.tsx");
  assert.match(modal, /user\?\.accountType === "PlatformAdmin" \? platformApi : accountSecurityApi/);
  assert.match(modal, /"account-security-profile", user\?\.centerId, user\?\.userId/);
  assert.match(modal, /useAuthStore\.getState\(\)\.clearSession\(\)/);
  assert.match(modal, /profileError/);
  assert.match(source("api/accountSecurityApi.ts"), /\/account\/me\/security/);
});

test("both review dialogs fit viewport height and expose dialog semantics", () => {
  const feedback = source("components/student/AttemptFeedbackHierarchy.tsx");
  for (const name of ["Yêu cầu xem xét kết quả", "Báo cáo đề bài bị sai sót"])
    assert.ok(feedback.includes(`aria-label="${name}" className="w-full max-w-lg max-h-[calc(100dvh-2rem)] overflow-y-auto`));
});

test("skipped and voided work are distinguished from a correct submitted answer and an AI failure", () => {
  const feedback = source("components/student/AttemptFeedbackHierarchy.tsx");
  assert.match(feedback, /studentSubmission\?\.skipped \? null : getAIProcessingNotice/);
  assert.ok(feedback.includes('isVoided ? "Được miễn · Tính trọn điểm"'));
  assert.ok(feedback.includes("Hệ thống không gọi AI và không dùng câu này để cập nhật mức độ thành thạo"));
  assert.ok(feedback.includes("Câu này không có phân tích AI và không cập nhật mức độ thành thạo"));
});

test("void credit is rounded on both detail and player surfaces", () => {
  assert.ok(source("pages/StudentAssignmentDetailPage.tsx").includes("question.voidedScore.toFixed(2)"));
  assert.ok(source("pages/LearningPlayerPage.tsx").includes("assignmentQuestion.voidedScore.toFixed(2)"));
});

test("historical route numbers and equal question contributions are labelled explicitly", () => {
  assert.ok(source("pages/StudentLearningPathPage.tsx").includes("số liệu tại thời điểm cập nhật"));
  const editor = source("pages/teacher/TeacherAssignmentEditorView.tsx");
  assert.ok(editor.includes("mỗi câu được chuẩn hóa về thang 10 và đóng góp bằng nhau"));
  assert.ok(!editor.includes("giữ nguyên để tính trọng số bài tập"));
});
