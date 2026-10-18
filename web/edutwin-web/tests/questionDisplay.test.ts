import assert from "node:assert/strict";
import test from "node:test";
import { getDifficultyLabel, getQuestionTypeLabel, getErrorTypeLabel, hasDeterminedGrade } from "../src/utils/questionDisplay.ts";

test("a determined grade remains visible while awaiting AI or teacher review", () => {
  assert.equal(hasDeterminedGrade({ latestAttempt: { awardedScore: 10, isCorrect: true } }), true);
  assert.equal(hasDeterminedGrade({ latestAttempt: { awardedScore: 0, isCorrect: false } }), true);
  assert.equal(hasDeterminedGrade({ effectiveIsCorrect: false }), true);
  assert.equal(hasDeterminedGrade({ isVoided: true }), true);
  assert.equal(hasDeterminedGrade({ latestAttempt: { awardedScore: null, isCorrect: null } }), false);
  assert.equal(hasDeterminedGrade({}), false);
});

test("labels cover difficulty levels and normalize imported type codes", () => {
  assert.deepEqual([1, 2, 3, 4, 5].map(getDifficultyLabel), ["Nhận biết", "Thông hiểu", "Vận dụng", "Vận dụng cao", "Vận dụng cao"]);
  assert.equal(getQuestionTypeLabel("SHORTANSWER"), "Trả lời ngắn");
  assert.equal(getQuestionTypeLabel("Essay"), "Tự luận");
  assert.equal(getQuestionTypeLabel("new-type"), "Câu hỏi");
  assert.equal(getErrorTypeLabel("Unknown"), "Chưa xác định");
});
