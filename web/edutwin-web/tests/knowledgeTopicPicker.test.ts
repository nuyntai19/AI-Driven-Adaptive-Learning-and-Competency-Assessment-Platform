import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { knowledgeTopicGroups, normalizeTopicSearch, type TopicOptionNode } from "../src/utils/knowledgeTopicOptions.ts";
import { goalGapLabel, questionTypeLabel } from "../src/utils/academicDisplay.ts";
const node = (id: string, name: string, parent: string | null = null, type: TopicOptionNode["nodeType"] = "Topic", active = true): TopicOptionNode =>
  ({ nodeId: id, nodeName: name, nodeCode: `CODE-${id}`, parentNodeId: parent, nodeType: type, isActive: active, orderIndex: 1 });
test("picker groups real chapters, excludes organizational and inactive nodes, preserves UInt64 IDs", () => {
  const nodes = [node("10", "Toán 10", null, "Chapter"), node("9007199254740993", "Hệ thức lượng", "10"), node("3", "Đã tắt", "10", "Topic", false), node("4", "Kỹ năng", "10", "Skill")];
  const groups = knowledgeTopicGroups(nodes);
  assert.deepEqual(groups.map(g => [g.name, g.topics.map(t => t.nodeId)]), [["Toán 10", ["9007199254740993"]]]);
});
test("search is Vietnamese accent-insensitive and supports codes and group filter without invented grade metadata", () => {
  const nodes = [node("c", "Hình học", null, "Chapter"), node("a", "Đường tròn", "c"), node("b", "Vectơ")];
  assert.equal(normalizeTopicSearch("ĐƯỜNG"), "duong");
  assert.equal(knowledgeTopicGroups(nodes, "duong tron")[0].topics[0].nodeId, "a");
  assert.equal(knowledgeTopicGroups(nodes, "CODE-b")[0].topics[0].nodeId, "b");
  assert.equal(knowledgeTopicGroups(nodes, "", "c").flatMap(g => g.topics).length, 1);
  assert.equal(knowledgeTopicGroups(nodes, "Khối 12").length, 0);
});
test("bad/cyclic parent metadata terminates and keeps ungrouped topics visible", () => {
  const nodes = [node("a", "A", "b"), node("b", "B", "a"), node("c", "C", "missing")];
  assert.equal(knowledgeTopicGroups(nodes)[0].topics.length, 3);
});
test("both question views use searchable picker with keyboard close and real visible groups", () => {
  for (const page of ["TeacherQuestionBankView", "TeacherQuestionEditorView"]) {
    const s = readFileSync(new URL(`../src/pages/teacher/${page}.tsx`, import.meta.url), "utf8");
    assert.match(s, /<KnowledgeTopicPicker/);
  }
  const s = readFileSync(new URL("../src/components/teacher/KnowledgeTopicPicker.tsx", import.meta.url), "utf8");
  assert.match(s, /aria-expanded/); assert.match(s, /Escape/); assert.match(s, /trigger.current\?\.focus/); assert.match(s, /max-h-72/);
});
test("target gap labels never double sign and distinguish reaching/exceeding goal", () => {
  assert.equal(goalGapLabel(8, 9.8), "Đã vượt 1.8 điểm");
  assert.equal(goalGapLabel(8, 6), "Cần thêm 2.0 điểm"); assert.equal(goalGapLabel(8, 8), "Đã đạt mục tiêu");
  assert.equal(questionTypeLabel("ShortAnswer"), "Đáp án ngắn");
});
test("questionnaire query and saved topic selection use the selected class context", () => {
  const s = readFileSync(new URL("../src/pages/StudentLearningPathPage.tsx", import.meta.url), "utf8");
  assert.match(s, /getLearningPathTopics\(effectiveSubjectId, access.classId, access.history\)/);
  assert.match(s, /filter\(id => allowed.has\(id\)\)/);
});
