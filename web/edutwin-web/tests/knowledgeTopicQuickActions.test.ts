import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import {
  isKnowledgeNodeId,
  isSubjectId,
  parseKnowledgeTopicContext,
  sameSubject,
  topicClassDisposition,
  topicQuickActionSearch,
  validateKnowledgeTopicContext,
  withoutTopicContext,
} from "../src/utils/knowledgeTopicContext.ts";

const subjectId = "12345678-1234-5678-9abc-123456789abc";
const otherSubjectId = "87654321-4321-8765-abcd-987654321abc";
const topicId = "18446744073709551615";
const subject = { subjectId, subjectName: "Toán", isActive: true };
const topic = { nodeId: topicId, subjectId, nodeName: "Hàm số", nodeType: "Topic", isActive: true };
const context = { kind: "topic" as const, subjectId, topicId };
const validData = { canRead: true, subjects: [subject], nodes: [topic] };
const readSource = (relative: string) => fs.readFileSync(new URL(`../src/${relative}`, import.meta.url), "utf8");
const bank = readSource("pages/teacher/TeacherQuestionBankView.tsx");
const editor = readSource("pages/teacher/TeacherAssignmentEditorView.tsx");
const graph = readSource("pages/teacher/TeacherKnowledgeGraphView.tsx");

test("IDs: accept exact UInt64 strings above JS safe-integer range without rounding", () => {
  for (const value of ["1", "9007199254740993", "9223372036854775808", topicId]) assert.equal(isKnowledgeNodeId(value), true);
  for (const value of ["", "0", "-1", "01", "+1", "1.5", "1e3", " 1", "1 ", "18446744073709551616", "9".repeat(500)]) {
    assert.equal(isKnowledgeNodeId(value), false, value);
  }
  assert.equal(isSubjectId(subjectId.toUpperCase()), true);
  for (const value of ["1", "not-a-guid", "00000000-0000-0000-0000-000000000000", ` ${subjectId}`]) assert.equal(isSubjectId(value), false);
});

test("links round-trip topic and subject, never infer recipients or assignment creation", () => {
  const query = topicQuickActionSearch(subjectId.toUpperCase(), topicId)!;
  const params = new URLSearchParams(query);
  assert.equal(params.get("topicNodeId"), topicId);
  assert.equal(params.get("subjectId"), subjectId.toUpperCase());
  assert.equal(params.has("studentIds"), false);
  assert.deepEqual(parseKnowledgeTopicContext(params), context);
  assert.equal(topicQuickActionSearch("bad", topicId), null);
  assert.equal(topicQuickActionSearch(subjectId, "0"), null);
});

test("URL parsing rejects missing, malformed and ambiguous IDs; no-context links remain unchanged", () => {
  assert.deepEqual(parseKnowledgeTopicContext(new URLSearchParams()), { kind: "none" });
  assert.deepEqual(parseKnowledgeTopicContext(new URLSearchParams({ subjectId, studentIds: "existing-flow" })), { kind: "none" });
  for (const query of [
    `topicNodeId=${topicId}`, `subjectId=${subjectId}&topicNodeId=`,
    `subjectId=bad&topicNodeId=1`, `subjectId=${subjectId}&topicNodeId=NaN`,
    `subjectId=${subjectId}&topicNodeId=1&topicNodeId=2`,
    `subjectId=${subjectId}&subjectId=${otherSubjectId}&topicNodeId=1`,
  ]) assert.equal(parseKnowledgeTopicContext(new URLSearchParams(query)).kind, "invalid", query);
  assert.deepEqual(parseKnowledgeTopicContext(new URLSearchParams({ topicNodeId: topicId }), subjectId), context);
  // Explicit bad subject must not be replaced with a fallback class subject.
  assert.equal(parseKnowledgeTopicContext(new URLSearchParams({ subjectId: "bad", topicNodeId: topicId }), subjectId).kind, "invalid");
});

test("membership requires an API-visible active subject and active Topic in that subject", () => {
  const result = validateKnowledgeTopicContext(context, validData);
  assert.equal(result.status, "ready");
  assert.match(result.message, /Hàm số.*Toán/);
  assert.equal(validateKnowledgeTopicContext(context, { ...validData, subjects: [{ ...subject, subjectId: subjectId.toUpperCase() }] }).status, "ready");
  for (const subjects of [[], [{ ...subject, subjectId: otherSubjectId }], [{ ...subject, isActive: false }]]) {
    assert.equal(validateKnowledgeTopicContext(context, { ...validData, subjects }).status, "invalid");
  }
  for (const nodes of [[], [{ ...topic, subjectId: otherSubjectId }], [{ ...topic, isActive: false }],
    ...["Subject", "Chapter", "Skill", "Concept"].map(nodeType => [{ ...topic, nodeType }])]) {
    assert.equal(validateKnowledgeTopicContext(context, { ...validData, nodes }).status, "invalid");
  }
});

test("pending, failed and forbidden validation never produce a ready/unfiltered context", () => {
  for (const data of [{ canRead: true }, { canRead: true, subjects: [subject] }, { canRead: true, nodes: [topic] }]) {
    assert.equal(validateKnowledgeTopicContext(context, data).status, "pending");
  }
  assert.equal(validateKnowledgeTopicContext(context, { ...validData, isError: true }).status, "error", "even with cached data, a failed verification is not ready");
  assert.equal(validateKnowledgeTopicContext(context, { ...validData, canRead: false }).status, "error");
  assert.equal(validateKnowledgeTopicContext({ kind: "invalid", message: "bad" }, validData).status, "invalid");
  assert.equal(validateKnowledgeTopicContext({ kind: "none" }, { canRead: false }).status, "none");
});

test("class transition waits for API data, preserves same subject and detects mismatch", () => {
  assert.equal(topicClassDisposition(subjectId), "waiting");
  assert.equal(topicClassDisposition(subjectId, subjectId), "same");
  assert.equal(topicClassDisposition(subjectId, subjectId.toUpperCase()), "same");
  assert.equal(topicClassDisposition(subjectId, otherSubjectId), "mismatch");
  assert.equal(sameSubject(undefined, undefined), false);
  const url = new URLSearchParams({ subjectId, topicNodeId: topicId, classId: "existing-class", studentIds: "existing-students", keep: "yes" });
  const cleared = withoutTopicContext(url);
  assert.equal(cleared.has("topicNodeId"), false);
  assert.equal(cleared.has("subjectId"), false);
  assert.equal(cleared.get("classId"), "existing-class");
  assert.equal(cleared.get("studentIds"), "existing-students");
  assert.equal(cleared.get("keep"), "yes");
  assert.equal(url.get("topicNodeId"), topicId, "do not mutate Router's searchParams in place");
});

test("graph wires subject/topic to both actions only for active Topic and correct permissions", () => {
  assert.match(graph, /selectedNode\?\.nodeType === "Topic" && selectedNode\.isActive/);
  assert.match(graph, /sameSubject\(graphData\?\.subjectId, selectedSubjectId\)/);
  assert.match(graph, /topicQuickActionSearch\(selectedSubjectId, selectedNode\.nodeId\)/);
  assert.match(graph, /canReadQuestions && \([\s\S]*?to=\{`\/giao-vien\/cau-hoi\?\$\{quickActionSearch\}`\}/);
  assert.match(graph, /canCreateAssignments && \([\s\S]*?to=\{`\/giao-vien\/bai-tap\/tao-moi\?\$\{quickActionSearch\}`\}/);
  assert.match(graph, /permissions\.questionsRead/);
  assert.match(graph, /permissions\.assignmentsCreate/);
  assert.match(graph, /Chọn một Chủ đề \(Topic\) đang hoạt động/);
});

test("bank synchronizes URL context, gates requests/results and preserves manual/image UI", () => {
  assert.match(bank, /parseKnowledgeTopicContext\(searchParams\)/);
  assert.match(bank, /topicContext\.kind === "topic" \? topicContext\.subjectId : manualSubjectId/);
  assert.match(bank, /topicContext\.kind === "topic" \? topicContext\.topicId : manualTopicId/);
  assert.match(bank, /useQuestions\(questionFilter, \{ enabled: questionsReady \}\)/);
  assert.match(bank, /topicValidation\.status === "none" \|\| topicValidation\.status === "ready"/);
  assert.match(bank, /if \(!questionsReady \|\| !response\?\.data\) return \[\]/);
  assert.match(bank, /questionsReady && !isLoading && !isError && displayedQuestions\.length === 0/);
  assert.match(bank, /questionsReady && !isLoading && !isError && totalItems > 0/);
  assert.match(bank, /setSearchParams\(withoutTopicContext\(searchParams\), \{ replace: true \}\)/);
  assert.match(bank, /role="status"/);
  assert.match(bank, /topicValidation\.message/);
  assert.match(bank, /q\.hasImage && <span[^\n]*Có ảnh đề bài/);
});

test("assignment retains topic across same-subject classes, clears confirmed mismatch and ignores edit context", () => {
  assert.match(editor, /const topicContext = isEditing \? \{ kind: "none" as const \}/);
  assert.match(editor, /topicClassDisposition\(contextSubjectId, selectedSubjectId\)/);
  assert.match(editor, /if \(!subjectMismatch \|\| classDetailQuery\.isError\) return/);
  assert.match(editor, /Đã bỏ bộ lọc chủ đề từ đồ thị vì lớp vừa chọn thuộc môn học khác/);
  const changeClass = editor.slice(editor.indexOf("const handleClassChange"), editor.indexOf("const handleDueAtChange"));
  assert.match(changeClass, /topicContext\.kind === "none" && !sameSubject\(selectedSubjectId, nextSubjectId\)/);
  assert.doesNotMatch(changeClass, /setSearchParams/);
  assert.match(editor, /topicId: questionTopicId \|\| undefined/);
  assert.match(editor, /enabled: questionsReady/);
  assert.match(editor, /!classDetailQuery\.isError && !subjectMismatch/);
  assert.match(editor, /\) : !questionsReady \? \(/);
  assert.match(editor, /questionsReady && !questionsQuery\.isError && questionsQuery\.data\?\.meta/);
  assert.match(editor, /const isReadOnly = isEditing \?[^\n]*: !canCreate/);
  assert.match(editor, /canPublish && \(isEditing \|\| canCreate\)/);
  assert.match(editor, /Bạn vẫn cần tự chọn câu hỏi và đối tượng giao bài/);
  assert.match(editor, /const hasQuickActionContext = searchParams\.has\("topicNodeId"\) && searchParams\.has\("subjectId"\)/);
  assert.match(editor, /const paramStudentIds = !isEditing && !hasQuickActionContext && searchParams\.get\("studentIds"\)/);
  const contextEffects = editor.slice(editor.indexOf("const topicContext ="), editor.indexOf("// Questions query for the subject"));
  assert.doesNotMatch(contextEffects, /setStudentIds|setTargetMode|setQuestionIds|\.mutate\(/);
});

test("touched query caches include center and user, while ordinary question calls remain compatible", () => {
  for (const source of [bank, editor, graph]) {
    const keys = source.matchAll(/queryKey: \[([^\n]+)\]/g);
    for (const [, key] of keys) {
      // All queries in these views are subject/class/question/context queries.
      assert.match(key, /user\?\.centerId|user\.centerId/, key);
      assert.match(key, /user\?\.userId|user\.userId/, key);
    }
  }
  const hook = readSource("features/questions/useQuestions.ts");
  assert.match(hook, /useQuestions\(filter\?: QuestionFilter, options\?: \{ enabled\?: boolean \}\)/);
  assert.match(hook, /queryKey: \["questions", user\?\.centerId, user\?\.userId, filter\]/);
  assert.match(hook, /options\?\.enabled \?\? true/);
  assert.match(hook, /invalidateQueries\(\{ queryKey: \["questions"\] \}\)/);
});
