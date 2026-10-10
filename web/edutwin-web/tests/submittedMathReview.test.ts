import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import vm from "node:vm";
import ts from "typescript";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import katex from "katex";
import { normalizeMathExpression } from "../src/utils/mathExpression.ts";
import { normalizeAITextLineBreaks } from "../src/utils/aiTextFormatting.ts";
import { getAttemptFeedbackPresentation } from "../src/utils/attemptFeedbackPresentation.ts";
import { isQuestionSubmissionLocked, canSubmitLearningWork } from "../src/utils/questionReview.ts";
import { getAttemptAttachmentPath } from "../src/utils/attemptAttachment.ts";

const read = (path: string) => fs.readFileSync(new URL(path, import.meta.url), "utf8");

// Exercise the actual TSX renderer, not a reimplementation of its regexes.
const rendererSource = read("../src/components/math/RichMathText.tsx");
const compiled = ts.transpileModule(rendererSource, {
  compilerOptions: { module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX, target: ts.ScriptTarget.ES2020 },
}).outputText.replace(/from\s+["']([^"']+)["']/g, (_match, specifier: string) => {
  const target = specifier === "../../utils/mathExpression"
    ? new URL("../src/utils/mathExpression.ts", import.meta.url).href
    : import.meta.resolve(specifier);
  return `from ${JSON.stringify(target)}`;
});
const { RichMathText } = await import(`data:text/javascript;base64,${Buffer.from(compiled).toString("base64")}`);

const answer = String.raw`(\frac{x^2}{2})\cdot\ln(x)-\frac{x^2}{4}+C`;

test("canonical antiderivative LaTeX is preserved, never rewritten into a newline command", () => {
  assert.equal(normalizeMathExpression(answer).latex, answer);
  for (const command of ["ln", "sin", "cos", "tan", "cot"]) {
    const input = `\\${command}(x)`;
    assert.equal(normalizeMathExpression(input).latex, input);
  }
  assert.equal(normalizeMathExpression("ln(x)").latex, String.raw`\ln\left(x\right)`);
  assert.equal(normalizeMathExpression("x * ln(x)").latex, String.raw`x \cdot \ln\left(x\right)`);
});

test("actual RichMathText renders the stored answer as one intact inline KaTeX formula", () => {
  const output = renderToStaticMarkup(React.createElement(RichMathText, { content: `$${answer}$` }));
  const expected = katex.renderToString(answer, { throwOnError: false, displayMode: false, trust: false, output: "htmlAndMathml" });
  assert.ok(output.includes(expected));
  assert.doesNotMatch(output, /katex-display|mspace newline/);
});

test("bare canonical formulas respect inline mode; explicit display delimiters remain block math", () => {
  const formula = String.raw`\frac{x^2}{2}\ln(x)`;
  assert.doesNotMatch(renderToStaticMarkup(React.createElement(RichMathText, { content: formula })), /katex-display/);
  assert.match(renderToStaticMarkup(React.createElement(RichMathText, { content: `$$${formula}$$` })), /katex-display/);
});

test("AI solution literal line breaks are repaired without damaging delimited or bare LaTeX", () => {
  const input = String.raw`Đặt $u=\ln(x)$ và $dv=x dx$.\nKhi đó:\n$du=\frac{1}{x}dx$\n\nÁp dụng nguyên hàm từng phần.`;
  const result = normalizeAITextLineBreaks(input);
  assert.equal(result, "Đặt $u=\\ln(x)$ và $dv=x dx$.\nKhi đó:\n$du=\\frac{1}{x}dx$\n\nÁp dụng nguyên hàm từng phần.");
  const commands = String.raw`$\nabla f \neq 0,\quad x\notin A$; \nabla, \neq, \nu, \notin, \nRightarrow, \right.`;
  assert.equal(normalizeAITextLineBreaks(commands), commands);
  assert.equal(normalizeAITextLineBreaks(result), result);
  assert.equal(normalizeAITextLineBreaks(String.raw`Bước 1.\r\nBước 2.`), "Bước 1.\nBước 2.");
});

test("confident valid manual essay explains teacher approval, never invents AI uncertainty or a grade", () => {
  const grading = { maxScore: 10, source: "PendingTeacher", reasonCode: "MANUAL_MODE", awardedScore: null, isCorrect: null };
  const view = getAttemptFeedbackPresentation(grading, {
    analysisId: "4", schemaVersion: "1.0", feedback: "Hợp lệ", missingSteps: [], rootCauseNodes: [],
    isFallback: false, hasTeacherOverride: false, needsTeacherReview: true,
    answerAssessment: "Correct", reasoningVerdict: "Valid", reasoningQuality: 100, confidence: 100, errorType: "None",
  }, "NeedsTeacherReview");
  assert.equal(view.manualReview, true);
  assert.equal(view.hasReasoningConcerns, false);
  assert.match(view.reviewExplanation, /AI đánh giá lập luận hợp lệ/);
  assert.match(view.reviewExplanation, /không phải cảnh báo AI thiếu tự tin/);
  assert.equal(grading.awardedScore, null);
  assert.equal(grading.isCorrect, null);
});

test("actual low-confidence or invalid reasoning still exposes review concerns", () => {
  const analysis = { analysisId: "4", schemaVersion: "1.0", feedback: "Kiểm tra", missingSteps: [], rootCauseNodes: [],
    isFallback: false, hasTeacherOverride: false, needsTeacherReview: true, confidence: 60 };
  const view = getAttemptFeedbackPresentation({ maxScore: 10, source: "PendingTeacher", reasonCode: "MANUAL_MODE" }, analysis, "NeedsTeacherReview");
  assert.equal(view.hasReasoningConcerns, true);
  assert.match(view.reviewExplanation, /Có điểm cần kiểm tra/);
});

test("single-question submissions, skipped answers and pending analysis all lock editing", () => {
  assert.equal(isQuestionSubmissionLocked(null), false);
  assert.equal(isQuestionSubmissionLocked({ attemptStatus: null }), false);
  for (const attemptStatus of ["Completed", "NeedsTeacherReview", "PendingAnalysis", "Processing"] as const) {
    assert.equal(isQuestionSubmissionLocked({ attemptStatus }), true);
  }
  assert.equal(isQuestionSubmissionLocked({ submittedAttemptId: "7" }), true);
  assert.equal(isQuestionSubmissionLocked({ submittedAnswer: "" }), true);
  assert.equal(isQuestionSubmissionLocked({ isVoided: true }), true);
});

test("submission guard rejects review/double-submit/polling before any upload but allows deadline auto-submit", () => {
  const state = { submitted: false, submitting: false, pendingAnalysis: false, expired: false, autoSubmit: false };
  assert.equal(canSubmitLearningWork(state), true);
  for (const flag of ["submitted", "submitting", "pendingAnalysis", "expired"] as const) {
    assert.equal(canSubmitLearningWork({ ...state, [flag]: true }), false);
  }
  assert.equal(canSubmitLearningWork({ ...state, expired: true, autoSubmit: true }), true);
  assert.equal(canSubmitLearningWork({ ...state, expired: true, autoSubmit: true, submitted: true }), false);
});

test("actual page submit handler exits before any draft persistence/upload when work is locked", async () => {
  const page = read("../src/pages/LearningPlayerPage.tsx");
  const source = page.slice(page.indexOf("  const handleFinalSubmit"), page.indexOf("  const handleRetrySubmission"));
  const script = ts.transpileModule(`${source}\nglobalThis.submit = handleFinalSubmit;`, {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.None },
  }).outputText;
  for (const overrides of [
    { isAssignmentSubmitted: true },
    { feedbackData: { attemptId: "7" } },
    { isSubmitting: true },
    { pollingJobId: "existing-job" },
    { isAssignmentExpired: true },
    { submissionInFlightRef: { current: true } },
    { assignmentQuestions: [{ submittedAttemptId: "7", hasAttachment: true }] },
    { classReadOnly: true },
    { assignmentId: null, learningAccess: { writable: false } },
  ]) {
    const effects: string[] = [];
    const context = vm.createContext({
      question: { questionId: "10014" }, submissionInFlightRef: { current: false },
      canSubmitLearningWork, isQuestionSubmissionLocked,
      isAssignmentSubmitted: false, feedbackData: null, isSubmitting: false,
      pollingJobId: null, isAssignmentExpired: false,
      classReadOnly: false, learningAccess: { writable: true },
      assignmentId: "assignment", assignmentQuestions: [{ questionId: "10014" }],
      persistCurrentAnswer: () => effects.push("persist"),
      uploadScratchpadAttachmentIfAny: () => effects.push("upload"),
      ...overrides,
    });
    vm.runInContext(script, context);
    await context.submit();
    assert.deepEqual(effects, [], JSON.stringify(overrides));
  }
});

test("attachment routes preserve large IDs and reject arbitrary external/path URLs", () => {
  assert.equal(getAttemptAttachmentPath("7"), "/learning/attempts/7/attachment");
  assert.equal(getAttemptAttachmentPath("18446744073709551615"), "/learning/attempts/18446744073709551615/attachment");
  for (const value of ["", "0", "../7", "https://example.test/img", "7?token=secret", -1, NaN, 1.5, Number.MAX_SAFE_INTEGER + 1]) {
    assert.throws(() => getAttemptAttachmentPath(value));
  }
});

test("page wiring uses one assignment review and never downloads submitted images into draft upload state", () => {
  const page = read("../src/pages/LearningPlayerPage.tsx");
  assert.match(page, /if \(feedbackData && !assignmentId\)/);
  assert.match(page, /storeAttemptFeedback = useCallback[\s\S]*?queryClient.setQueryData/);
  assert.match(page, /refetchInterval:[\s\S]*?PendingAnalysis[\s\S]*?Processing/);
  assert.match(page, /<AttemptScratchpadAttachment attemptId=\{reviewAttemptId\}/);
  assert.doesNotMatch(page, /setAttachedSnapshotDataUrl\(objectUrl\)/);
  const submit = page.slice(page.indexOf("const handleFinalSubmit"), page.indexOf("const handleRetrySubmission"));
  assert.ok(submit.indexOf("canSubmitLearningWork") < submit.indexOf("uploadScratchpadAttachmentIfAny"));
  assert.match(submit, /for \(const q of pendingQuestions\)/);
  assert.match(submit, /finally \{\s*submissionInFlightRef.current = false/);
  assert.match(page, /\) : isReadOnly \? \([\s\S]*?Bài làm đã nộp — chỉ có thể xem lại/);
});

test("RichMathEditor locks native formula clicks, commit/delete, and closes popovers when disabled", () => {
  const source = read("../src/components/math/RichMathEditor.tsx");
  assert.match(source, /handleOpenMathNode = useCallback\([^]*?if \(disabledRef.current\) return/);
  assert.match(source, /handleConfirmMath = \(\) => \{\s*if \(disabledRef.current/);
  assert.match(source, /handleDeleteMath = \(\) => \{\s*if \(disabledRef.current\) return/);
  assert.match(source, /\{!disabled && activeMathNode && popoverPos &&/);
  assert.match(source, /hydrateEditorDom\(editorRef.current, value, disabled \? undefined/);
});

test("both result views use authenticated attachment component with cleanup, error and retry UI", () => {
  const hierarchy = read("../src/components/student/AttemptFeedbackHierarchy.tsx");
  const component = read("../src/components/student/AttemptScratchpadAttachment.tsx");
  const api = read("../src/api/learningFeedbackApi.ts");
  assert.match(hierarchy, /<AttemptScratchpadAttachment attemptId=\{attemptId\}/);
  assert.doesNotMatch(hierarchy, /src=\{studentSubmission.attachmentUrl\}/);
  assert.match(component, /URL.revokeObjectURL\(url\)/);
  assert.match(component, /user\?\.centerId, user\?\.userId/);
  assert.match(component, /Tải lại ảnh/);
  assert.match(api, /httpClient.get<Blob>\(getAttemptAttachmentPath\(attemptId\)/);
});
