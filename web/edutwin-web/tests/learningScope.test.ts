import test from "node:test";
import assert from "node:assert/strict";
import { groupLearningSkills, missingLearningPrerequisites } from "../src/utils/learningScope.ts";
import type { SkillCapabilityStatusDto } from "../src/types/learningPath.ts";

const skills = [
  { skillId: "1", skillName: "Hiện tại đơn", topicId: "tenses", topicName: "Thì", state: "Unknown", prerequisiteSkillIds: [] },
  { skillId: "2", skillName: "Quá khứ hoàn thành", topicId: "tenses", topicName: "Thì", state: "Unknown", prerequisiteSkillIds: ["1"] },
  { skillId: "3", skillName: "Mệnh đề", topicId: "clauses", topicName: "Mệnh đề", state: "Unknown", prerequisiteSkillIds: ["2"] },
  { skillId: "4", skillName: "Từ vựng", topicId: "words", topicName: "Từ vựng", state: "Unknown", prerequisiteSkillIds: [] }
] as SkillCapabilityStatusDto[];

test("scope groups leaves without losing individual skills", () => {
  const groups = groupLearningSkills(skills);
  assert.deepEqual(groups.map(g => g.name), ["Thì", "Mệnh đề", "Từ vựng"]);
  assert.deepEqual(groups[0].skills.map(s => s.skillId), ["1", "2"]);
});
test("warnings follow transitive graph dependencies, never checklist order", () => {
  assert.deepEqual(missingLearningPrerequisites(skills, ["3"]).map(s => s.skillId), ["2", "1"]);
  assert.deepEqual(missingLearningPrerequisites(skills, ["4"]), []);
  assert.deepEqual(missingLearningPrerequisites(skills, ["1", "2", "3"]), []);
});
test("confirmed prerequisites need no warning and cycles terminate", () => {
  assert.deepEqual(missingLearningPrerequisites(skills.map(s => ({ ...s, state: "Confirmed" })), ["3"]), []);
  const cycle = skills.map(s => ({ ...s, prerequisiteSkillIds: s.skillId === "1" ? ["3"] : s.prerequisiteSkillIds }));
  assert.equal(missingLearningPrerequisites(cycle, ["3"]).length, 2);
});
