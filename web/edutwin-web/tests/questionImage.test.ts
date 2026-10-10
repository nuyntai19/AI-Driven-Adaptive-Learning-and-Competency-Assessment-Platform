import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { questionImageFileError, MAX_QUESTION_IMAGE_BYTES, IMAGE_ONLY_QUESTION_TEXT } from "../src/utils/questionImage.ts";

test("problem images accept PNG/JPEG/WebP and reject unsafe formats and unbounded input", () => {
  for (const type of ["image/png", "image/jpeg", "image/webp"]) assert.equal(questionImageFileError({ type, size: 100 }), null);
  for (const type of ["image/svg+xml", "image/gif", "text/html", ""]) assert.ok(questionImageFileError({ type, size: 100 }));
  for (const size of [0, 8 * 1024 * 1024 + 1]) assert.ok(questionImageFileError({ type: "image/png", size }));
  assert.equal(MAX_QUESTION_IMAGE_BYTES, 2 * 1024 * 1024);
  assert.equal(IMAGE_ONLY_QUESTION_TEXT, "Đọc đề bài trong ảnh đính kèm.");
});

test("image loading uses authenticated scoped API, and list does not download all image bytes", () => {
  const viewer = readFileSync(new URL("../src/components/QuestionImage.tsx", import.meta.url), "utf8");
  assert.match(viewer, /httpClient\.get/); assert.match(viewer, /responseType: "blob"/);
  assert.match(viewer, /URL\.revokeObjectURL/); assert.doesNotMatch(viewer, /localStorage|\?token=|data:image\/png;base64/);
  const bank = readFileSync(new URL("../src/pages/teacher/TeacherQuestionBankView.tsx", import.meta.url), "utf8");
  assert.match(bank, /q\.hasImage/); assert.doesNotMatch(bank, /<QuestionImage /);
});
