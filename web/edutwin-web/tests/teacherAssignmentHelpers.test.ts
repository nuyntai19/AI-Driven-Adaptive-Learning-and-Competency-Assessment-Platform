import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { getAssignmentQuestionNumber } from "../src/utils/teacherAssignmentHelpers.ts";

describe("assignment question numbering", () => {
  it("uses one-based API numbering without adding another one", () => {
    assert.equal(getAssignmentQuestionNumber(1, 0), 1);
    assert.equal(getAssignmentQuestionNumber(2, 1), 2);
  });
  it("preserves original numbering after filtering questions", () => {
    assert.equal(getAssignmentQuestionNumber(2, 0), 2);
    assert.equal(getAssignmentQuestionNumber(5, 0), 5);
  });
  it("uses the display position only when the API index is missing or invalid", () => {
    for (const value of [undefined, 0, -1, Number.NaN, 1.5]) {
      assert.equal(getAssignmentQuestionNumber(value, 0), 1);
      assert.equal(getAssignmentQuestionNumber(value, 1), 2);
    }
  });
});
