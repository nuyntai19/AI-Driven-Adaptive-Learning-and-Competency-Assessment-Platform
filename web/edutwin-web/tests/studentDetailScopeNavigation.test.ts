import test from "node:test";
import assert from "node:assert/strict";
import { studentScopeChangeUrl } from "../src/utils/studentAcademicNavigation.ts";

test("switching current/history from a detail removes the old assignment ID", () => {
  const params = new URLSearchParams("subjectId=math&history=true&assignmentId=test4&curriculumId=ignored");
  assert.equal(studentScopeChangeUrl("/hoc-tap/bai-tap/test4", params), "/hoc-tap/bai-tap?subjectId=math&history=true");
  assert.equal(params.get("assignmentId"), "test4", "navigation must not mutate caller parameters");
});

test("switching class or subject from detail returns to that scope's assignment list", () => {
  assert.equal(studentScopeChangeUrl("/hoc-tap/bai-tap/test4/", new URLSearchParams("subjectId=math&classId=other")),
    "/hoc-tap/bai-tap?subjectId=math&classId=other");
  assert.equal(studentScopeChangeUrl("/hoc-tap/bai-tap/test4", new URLSearchParams("subjectId=english")),
    "/hoc-tap/bai-tap?subjectId=english");
  assert.equal(studentScopeChangeUrl("/hoc-tap/bai-tap/test4", new URLSearchParams()), "/hoc-tap/bai-tap");
});

test("list and dashboard navigation retain their own non-detail view options", () => {
  assert.equal(studentScopeChangeUrl("/hoc-tap/bai-tap", new URLSearchParams("subjectId=math&history=true")),
    "/hoc-tap/bai-tap?subjectId=math&history=true");
  assert.equal(studentScopeChangeUrl("/hoc-tap/tong-quan", new URLSearchParams("subjectId=math&tab=radar")),
    "/hoc-tap/tong-quan?subjectId=math&tab=radar");
});
