import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { permissions } from "../src/auth/permissions.ts";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const webSrcDir = path.resolve(__dirname, "../src");

test("Knowledge Graph Filter: Canonical permission nodesRead exists", () => {
  assert.equal(
    permissions.nodesRead,
    "knowledge.nodes.read",
    "nodesRead permission must match backend knowledge node policy"
  );
});

test("Knowledge Graph Filter: QuestionBankPage (Center Manager) supports topicId filter", () => {
  const code = fs.readFileSync(path.join(webSrcDir, "pages/QuestionBankPage.tsx"), "utf-8");

  assert.ok(code.includes("knowledgeGraphApi"), "QuestionBankPage must import knowledgeGraphApi");
  assert.ok(code.includes("selectedTopicId"), "QuestionBankPage must have selectedTopicId state");
  assert.ok(code.includes("canReadNodes"), "QuestionBankPage must check canReadNodes permission");
  assert.ok(code.includes("knowledgeNodesData"), "QuestionBankPage must query knowledge nodes");
  assert.ok(code.includes("topicId: selectedTopicId"), "QuestionBankPage must include topicId in questionFilter");
  assert.ok(code.includes('id="filter-topic"'), "QuestionBankPage must render knowledge graph select dropdown");
  assert.ok(code.includes("xl:grid-cols-6"), "QuestionBankPage filter grid must adapt cleanly for 6 filter items");
  assert.ok(code.includes("setSelectedTopicId(\"\")"), "QuestionBankPage must reset topicId when subject changes");
});

test("Knowledge Graph Filter: TeacherQuestionBankView supports topicId filter", () => {
  const code = fs.readFileSync(path.join(webSrcDir, "pages/teacher/TeacherQuestionBankView.tsx"), "utf-8");

  assert.ok(code.includes("knowledgeGraphApi"), "TeacherQuestionBankView must import knowledgeGraphApi");
  assert.ok(code.includes("selectedTopicId"), "TeacherQuestionBankView must have selectedTopicId state");
  assert.ok(code.includes("canReadNodes"), "TeacherQuestionBankView must check canReadNodes permission");
  assert.ok(code.includes("knowledgeNodesData"), "TeacherQuestionBankView must query knowledge nodes");
  assert.ok(code.includes("topicId: selectedTopicId"), "TeacherQuestionBankView must include topicId in questionFilter");
  assert.ok(code.includes('id="filter-teacher-topic"'), "TeacherQuestionBankView must render knowledge graph select dropdown");
  assert.ok(code.includes("xl:grid-cols-6"), "TeacherQuestionBankView filter grid must adapt cleanly for 6 filter items");
  assert.ok(code.includes("setSelectedTopicId(\"\")"), "TeacherQuestionBankView must reset topicId when subject changes");
});

test("Knowledge Graph Filter: AssignmentEditorPage (Center Manager) supports topicId filter in question selector", () => {
  const code = fs.readFileSync(path.join(webSrcDir, "pages/AssignmentEditorPage.tsx"), "utf-8");

  assert.ok(code.includes("knowledgeGraphApi"), "AssignmentEditorPage must import knowledgeGraphApi");
  assert.ok(code.includes("questionTopicId"), "AssignmentEditorPage must have questionTopicId state");
  assert.ok(code.includes("canReadNodes"), "AssignmentEditorPage must check canReadNodes permission");
  assert.ok(code.includes("knowledgeNodesQuery"), "AssignmentEditorPage must query knowledge nodes for subject");
  assert.ok(code.includes("topicId: questionTopicId"), "AssignmentEditorPage must pass topicId to useAssignableQuestions");
  assert.ok(code.includes('id="q-topic-filter"'), "AssignmentEditorPage must render topic filter select dropdown in Step 1");
  assert.ok(code.includes("setQuestionTopicId(\"\")"), "AssignmentEditorPage must reset questionTopicId when class changes");
});

test("Knowledge Graph Filter: TeacherAssignmentEditorView supports topicId filter in question selector", () => {
  const code = fs.readFileSync(path.join(webSrcDir, "pages/teacher/TeacherAssignmentEditorView.tsx"), "utf-8");

  assert.ok(code.includes("knowledgeGraphApi"), "TeacherAssignmentEditorView must import knowledgeGraphApi");
  assert.ok(code.includes("questionTopicId"), "TeacherAssignmentEditorView must have questionTopicId state");
  assert.ok(code.includes("canReadNodes"), "TeacherAssignmentEditorView must check canReadNodes permission");
  assert.ok(code.includes("knowledgeNodesQuery"), "TeacherAssignmentEditorView must query knowledge nodes for subject");
  assert.ok(code.includes("topicId: questionTopicId"), "TeacherAssignmentEditorView must pass topicId to useAssignableQuestions");
  assert.ok(code.includes('id="teacher-q-topic-filter"'), "TeacherAssignmentEditorView must render topic filter select dropdown in Step 1");
  assert.ok(code.includes("setQuestionTopicId(\"\")"), "TeacherAssignmentEditorView must reset questionTopicId when class changes");
});
