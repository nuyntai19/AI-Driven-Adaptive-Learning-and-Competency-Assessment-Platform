import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import vm from "node:vm";
import { createRequire } from "node:module";
import ts from "typescript";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import * as questionDisplay from "../src/utils/questionDisplay.ts";

const require = createRequire(import.meta.url);
const source = fs.readFileSync(new URL("../src/pages/StudentAssignmentDetailPage.tsx", import.meta.url), "utf8");
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX, target: ts.ScriptTarget.ES2020, esModuleInterop: true } }).outputText;
function renderOverview(submitted = false, started = false) {
  const exports = {};
  const assignment = { assignmentId: "assignment", title: "English", subjectName: "Tiếng Anh", timeLimitMinutes: 30,
    isSubmitted: submitted, startedAt: started ? "2026-10-18T00:00:00Z" : null,
    progress: { status: submitted ? "Completed" : "InProgress", completedQuestionCount: submitted ? 3 : 0, totalQuestionCount: 3 },
    draftAnswers: [{ questionId: 1, finalAnswer: "one" }],
    questions: [1, 2, 3].map(id => ({ questionId: String(id), questionType: "SHORTANSWER", difficulty: 4,
      questionText: `SECRET QUESTION ${id}`, options: [{ optionId: "a", label: "A", text: "SECRET OPTION" }],
      latestAttempt: submitted ? { status: "Completed", isCorrect: true, awardedScore: 10 } : null })) };
  vm.runInNewContext(compiled, { exports, require: (name: string) => {
    if (name === "react" || name === "react/jsx-runtime") return require(name);
    if (name === "react-router-dom") return { useParams: () => ({ id: "assignment" }), useSearchParams: () => [new URLSearchParams()], Link: ({ children }: any) => React.createElement("a", null, children) };
    if (name === "axios") return { isAxiosError: () => false };
    if (name.endsWith("authStore")) return { useAuthStore: (selector: any) => selector({ user: { centerId: "center", userId: "student" } }) };
    if (name.endsWith("useStudentAssignment")) return { useStudentAssignment: () => ({ data: { data: assignment } }) };
    if (name.endsWith("assignmentDraftStorage")) return { readAssignmentDraft: () => JSON.stringify({ "1": { finalAnswer: "one" }, "2": { finalAnswer: "two" }, "3": { reasoningText: "three" } }) };
    if (name.endsWith("questionDisplay")) return questionDisplay;
    if (name.endsWith("studentAcademicNavigation")) return { studentScopeUrl: (path: string) => path };
    if (name.endsWith("subjectTheme")) return { getSubjectTheme: () => ({}) };
    if (name.endsWith("StudentSubjectPattern")) return { StudentSubjectPattern: () => null };
    if (name.endsWith("StudentBadge")) return { StudentBadge: ({ children }: any) => React.createElement("span", null, children) };
    if (name.endsWith("RichMathText")) return { RichMathText: ({ text, content }: any) => React.createElement("span", null, text ?? content) };
    throw new Error(`Missing test dependency: ${name}`);
  }});
  return renderToStaticMarkup((exports as any).StudentAssignmentDetailPage());
}

test("overview hides question text and options until the submitted review", () => {
  for (const started of [false, true]) assert.doesNotMatch(renderOverview(false, started), /SECRET QUESTION|SECRET OPTION/);
  assert.match(renderOverview(true, true), /SECRET QUESTION 1/);
});

test("overview combines scoped local and server drafts without counting duplicates or calling them unattempted", () => {
  const html = renderOverview();
  assert.match(html, /3 câu nháp chưa nộp/);
  assert.equal((html.match(/Đã lưu nháp · Chưa nộp/g) ?? []).length, 3);
  assert.doesNotMatch(html, />Chưa làm</);
  assert.match(html, /Trả lời ngắn/);
  assert.match(html, /Vận dụng cao/);
});
