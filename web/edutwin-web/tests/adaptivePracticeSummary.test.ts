import assert from "node:assert/strict";
import test from "node:test";
import { answeredQuestionCount, formatPracticeDuration, practiceEndLabel, practiceGoalLabel, teacherPracticeEndLabel } from "../src/utils/adaptivePracticeSummary.ts";

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

test("teacher goal labels distinguish progress from simply ending a session", () => {
  assert.equal(practiceGoalLabel("Achieved"), "Đạt mục tiêu");
  assert.equal(practiceGoalLabel("PartiallyAchieved"), "Đạt một phần");
  assert.equal(practiceGoalLabel("NotAchieved"), "Chưa đạt");
  assert.equal(practiceGoalLabel("RemediationRecommended"), "Cần bổ trợ");
  assert.equal(practiceGoalLabel("PartiallyAchieved", true), "Cần bổ trợ");
  assert.equal(practiceGoalLabel("unknown"), "Chưa xác định");
});

test("teacher ending reasons do not call every limit or failure a student stop", () => {
  for (const reason of ["StudentStopped", "TimeBudgetReached", "OutOfQuestions", "QuestionLimitReached", "Expired", "EvaluationFailed", "GoalAchievedWithEvidence"]) {
    assert.equal(teacherPracticeEndLabel(reason) === "Học sinh dừng", reason === "StudentStopped");
  }
  assert.equal(teacherPracticeEndLabel(null), "Đã kết thúc");
});
