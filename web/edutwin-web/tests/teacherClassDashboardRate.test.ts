import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { normalizeCompletionRate } from "../src/pages/teacher/teacherDashboardHelpers.ts";

describe("TeacherClassDashboardView normalizeCompletionRate", () => {
  it("correctly preserves rates at key benchmarks without multiplying small percentages", () => {
    assert.equal(normalizeCompletionRate(0), 0);
    assert.equal(normalizeCompletionRate(0.5), 0.5);
    assert.equal(normalizeCompletionRate(1), 1);
    assert.equal(normalizeCompletionRate(50), 50);
    assert.equal(normalizeCompletionRate(100), 100);
  });

  it("clamps values outside [0, 100]", () => {
    assert.equal(normalizeCompletionRate(-10), 0);
    assert.equal(normalizeCompletionRate(150), 100);
  });

  it("handles null, undefined, and non-numeric inputs safely", () => {
    assert.equal(normalizeCompletionRate(null), 0);
    assert.equal(normalizeCompletionRate(undefined), 0);
    assert.equal(normalizeCompletionRate(Number.NaN), 0);
  });
});
