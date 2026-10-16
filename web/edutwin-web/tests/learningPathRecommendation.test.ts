import test from "node:test";
import assert from "node:assert/strict";
import { recommendLearningGoal } from "../src/utils/learningPathRecommendation.ts";
import type { LearningInsightsDto, SkillCapabilityStatusDto } from "../src/types/learningPath.ts";
const skills = (state = "Unknown") => [{ skillId: "101", skillName: "Thì", state }, { skillId: "102", skillName: "Đọc hiểu", state: "Unknown" }] as SkillCapabilityStatusDto[];
const insights = (count: number, wrong: number) => ({ gradedQuestionCount: count, skills: [{ skillId: "101", skillName: "Thì", questionCount: count, correctCount: count - wrong, wrongCount: wrong }] }) as LearningInsightsDto;
test("3/3 correct preselects an honest diagnostic instead of an empty foundation form or advanced claim", () => {
  const r = recommendLearningGoal("Tiếng Anh", skills(), insights(3, 0));
  assert.equal(r.goalType, "Diagnostic"); assert.ok(r.targetSkillIds.includes("101")); assert.ok(r.title.includes("Xác định")); assert.match(r.observation, /3 câu/);
});
test("observed mistakes select repair with concrete counts and focus already filled", () => {
  const r = recommendLearningGoal("Tiếng Anh", skills(), insights(6, 4));
  assert.equal(r.goalType, "Foundation"); assert.deepEqual(r.targetSkillIds, ["101"]); assert.match(r.observation, /sai 4\/6/); assert.equal(r.focus, "Thì");
});
test("advanced recommendation requires confirmed evidence; guided perfect scores alone do not qualify", () => {
  assert.equal(recommendLearningGoal("Tiếng Anh", skills(), insights(8, 0)).goalType, "Diagnostic");
  const advanced = recommendLearningGoal("Tiếng Anh", skills("Confirmed"), insights(8, 0));
  assert.equal(advanced.goalType, "Advanced"); assert.deepEqual(advanced.targetSkillIds, ["101"]); assert.match(advanced.improvement, /4–5/);
});
