import assert from "node:assert/strict";
import test from "node:test";
import {
  formatAwardedScore,
  formatPreliminaryResult,
} from "../src/utils/gradingDisplay.ts";

test("unresolved grading is shown as pending without a zero score", () => {
  assert.equal(formatPreliminaryResult(null), "Kết quả: Chờ giáo viên chấm");
  assert.equal(formatAwardedScore(null, 50), "Điểm: Chưa chấm");
});

test("a determined zero remains distinguishable from an unresolved score", () => {
  assert.equal(formatPreliminaryResult(false), "Kết quả: Chưa đúng");
  assert.equal(formatAwardedScore(0, 50), "Điểm: 0 / 50");
});
