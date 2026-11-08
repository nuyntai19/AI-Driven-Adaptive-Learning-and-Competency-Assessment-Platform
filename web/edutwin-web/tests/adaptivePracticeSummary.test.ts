import assert from "node:assert/strict";
import test from "node:test";
import { answeredQuestionCount, formatPracticeDuration, practiceEndLabel } from "../src/utils/adaptivePracticeSummary.ts";

test("an unanswered question does not count as answered", () => {
  assert.equal(answeredQuestionCount({ correctCount: 0, incorrectCount: 0, partialCount: 0, pendingCount: 0 }), 0);
  assert.equal(answeredQuestionCount({ correctCount: 2, incorrectCount: 1, partialCount: 0, pendingCount: 1 }), 4);
});

test("durations read naturally", () => {
  assert.equal(formatPracticeDuration(8), "8 giây");
  assert.equal(formatPracticeDuration(60), "1 phút");
  assert.equal(formatPracticeDuration(125), "2 phút 5 giây");
});

test("each ending is named for what happened", () => {
  assert.equal(practiceEndLabel("StudentStopped"), "Bạn đã dừng phiên");
  assert.equal(practiceEndLabel("OutOfQuestions"), "Hết câu hỏi");
  assert.equal(practiceEndLabel("TimeBudgetReached"), "Hết thời gian");
  assert.equal(practiceEndLabel(null), "Đã kết thúc");
});
