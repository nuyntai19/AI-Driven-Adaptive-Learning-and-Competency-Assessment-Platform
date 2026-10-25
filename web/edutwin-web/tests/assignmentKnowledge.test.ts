import test from "node:test";
import assert from "node:assert/strict";
import { assignmentKnowledgeSearch, assignmentResultLabel } from "../src/utils/assignmentKnowledge.ts";
import type { AssignmentResultSummaryDto } from "../src/types/assignments.ts";

test("result labels distinguish answered progress from grading and teacher approval", () => {
  const partial = { evaluatedQuestionCount: 2, correctQuestionCount: 1, pendingQuestionCount: 1, teacherFinalReviewStatus: "Pending", resultStatus: "Processing" } as AssignmentResultSummaryDto;
  assert.match(assignmentResultLabel(partial), /Kết quả tạm: đúng 1\/2 câu đã chấm/);
  assert.match(assignmentResultLabel({ ...partial, evaluatedQuestionCount: 0 }), /đang chờ chấm/);
  assert.match(assignmentResultLabel({ ...partial, teacherFinalReviewStatus: "Approved", resultStatus: "Final", pendingQuestionCount: 0 }), /Kết quả đã duyệt/);
  assert.match(assignmentResultLabel({ ...partial, evaluatedQuestionCount: 3, voidedQuestionCount: 1 }), /đúng 1\/2/);
});
test("knowledge search uses actual skill names even for uninformative assignment titles", () => {
  const summary = { knowledgeAreas: [{ topicName: "Thì", skillName: "Hiện tại đơn" }] } as AssignmentResultSummaryDto;
  assert.match(assignmentKnowledgeSearch(summary), /Thì Hiện tại đơn/);
  assert.equal(assignmentKnowledgeSearch(null), "");
});
