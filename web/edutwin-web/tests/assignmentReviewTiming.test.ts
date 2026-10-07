import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import vm from "node:vm";
import ts from "typescript";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import type { StudentAssignmentDetailDto, StudentAssignmentQuestionDto } from "../src/types/assignments.ts";
import {
  isAssignmentWorkSubmitted, shouldStartAssignment, getAssignmentReviewTiming,
  isActiveAssignmentExpired, formatReviewDuration,
} from "../src/utils/assignmentReviewTiming.ts";

const startedAt = "2026-10-06T08:00:00Z";
const submittedAt = "2026-10-06T08:10:00Z";
const expiresAt = Date.parse("2026-10-06T09:00:00Z");
const question = (extra: Partial<StudentAssignmentQuestionDto> = {}): StudentAssignmentQuestionDto => ({
  questionId: "1", questionType: "Essay", difficulty: 1, questionText: "Q",
  estimatedTimeSeconds: 60, reasoningRequired: true, languageCode: "vi", attemptStatus: null, ...extra,
});
const assignment = (extra: Partial<StudentAssignmentDetailDto> = {}): StudentAssignmentDetailDto => ({
  assignmentId: "assignment-1", title: "Test", instructions: null, dueAt: "2026-10-06T09:00:00Z",
  timeLimitMinutes: 60, startedAt, effectiveExpiresAt: "2026-10-06T09:00:00Z", remainingSeconds: 3000,
  progress: { status: "InProgress", completedQuestionCount: 0, totalQuestionCount: 1 },
  questions: [question()], ...extra,
});

test("reopened submitted work is review-only even long after both deadlines", () => {
  for (const timeLimitMinutes of [60, null]) {
    const detail = assignment({ timeLimitMinutes, isSubmitted: true, submittedAt, elapsedSeconds: 600,
      progress: { status: "Completed", completedQuestionCount: 1, totalQuestionCount: 1 } });
    assert.equal(shouldStartAssignment(detail), false);
    for (const now of [expiresAt - 1, expiresAt, expiresAt + 86400000]) {
      assert.equal(isActiveAssignmentExpired(isAssignmentWorkSubmitted(detail), expiresAt, now), false);
      assert.deepEqual(getAssignmentReviewTiming(detail), { submittedAt, elapsedSeconds: 600 });
    }
  }
});

test("completed progress and local server acceptance also suppress restart before detail refresh", () => {
  const stale = assignment({ startedAt: null });
  assert.equal(shouldStartAssignment(stale), true);
  assert.equal(isAssignmentWorkSubmitted(stale, true), true);
  assert.equal(shouldStartAssignment(stale, true), false);
  assert.equal(isAssignmentWorkSubmitted(assignment({
    progress: { status: "Completed", completedQuestionCount: 1, totalQuestionCount: 1 },
  })), true);
});

test("all persisted answers remain submitted while AI is processing, failed or waiting for teacher", () => {
  for (const status of ["PendingAnalysis", "Processing", "AnalysisFailed", "NeedsTeacherReview", "Completed"] as const) {
    const detail = assignment({ questions: [question({ attemptStatus: status, submittedAttemptId: "7" })] });
    assert.equal(isAssignmentWorkSubmitted(detail), true);
    assert.equal(shouldStartAssignment(detail), false);
    assert.equal(isActiveAssignmentExpired(true, expiresAt, expiresAt + 1), false);
  }
  assert.equal(isAssignmentWorkSubmitted(assignment({ questions: [question({ isVoided: true })] })), true);
});

test("partially submitted and empty assignments are not mistaken for submitted work", () => {
  const partial = assignment({ questions: [question({ submittedAttemptId: "7" }), question({ questionId: "2" })] });
  assert.equal(isAssignmentWorkSubmitted(partial), false);
  assert.equal(isAssignmentWorkSubmitted(assignment({ questions: [] })), false);
  assert.equal(isActiveAssignmentExpired(false, expiresAt, expiresAt - 1), false);
  assert.equal(isActiveAssignmentExpired(false, expiresAt, expiresAt), true);
  assert.equal(isActiveAssignmentExpired(false, null, expiresAt), false);
  assert.equal(isActiveAssignmentExpired(false, NaN, expiresAt), false);
});

test("already-started active work uses existing server timing instead of another start request", () => {
  assert.equal(shouldStartAssignment(assignment()), false);
  assert.equal(shouldStartAssignment(undefined), false);
  assert.equal(shouldStartAssignment(assignment({ startedAt: null })), true);
});

test("legacy review timing uses latest persisted submission, never the current wall clock", () => {
  const attempt = { attemptId: "7", status: "Completed" as const, finalAnswer: "x", reasoningText: "ok",
    confidence: 80, timeSpentSeconds: 600, answerChanges: 1, skipped: false, submittedAt };
  const detail = assignment({ questions: [question({ latestAttempt: attempt }), question({ questionId: "2",
    latestAttempt: { ...attempt, attemptId: "8", submittedAt: "2026-10-06T08:12:00Z" } })] });
  assert.deepEqual(getAssignmentReviewTiming(detail), { submittedAt: "2026-10-06T08:12:00Z", elapsedSeconds: 720 });
  assert.deepEqual(getAssignmentReviewTiming(assignment({ isSubmitted: true, submittedAt: "invalid", startedAt: null })),
    { submittedAt: null, elapsedSeconds: null });
  assert.deepEqual(getAssignmentReviewTiming(assignment()), { submittedAt: null, elapsedSeconds: null });
});

test("receipt shows recorded duration and submission, without a running/expired clock", async () => {
  const source = fs.readFileSync(new URL("../src/components/student/AssignmentReviewReceipt.tsx", import.meta.url), "utf8");
  const compiled = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX, target: ts.ScriptTarget.ES2020 },
  }).outputText.replace(/from\s+["']([^"']+)["']/g, (_match, specifier: string) => `from ${JSON.stringify(
    specifier === "../../utils/assignmentReviewTiming"
      ? new URL("../src/utils/assignmentReviewTiming.ts", import.meta.url).href : import.meta.resolve(specifier))}`);
  const { AssignmentReviewReceipt } = await import(`data:text/javascript;base64,${Buffer.from(compiled).toString("base64")}`);
  const rendered = renderToStaticMarkup(React.createElement(AssignmentReviewReceipt, { submittedAt, elapsedSeconds: 600 }));
  assert.match(rendered, /Đã nộp bài/);
  assert.match(rendered, /00:10:00/);
  assert.doesNotMatch(rendered, /HẾT GIỜ/);
  const missing = renderToStaticMarkup(React.createElement(AssignmentReviewReceipt, { submittedAt: null, elapsedSeconds: null }));
  assert.doesNotMatch(missing, /00:00:00|Thời gian làm bài|Lúc/);
  assert.equal(formatReviewDuration(3661), "01:01:01");
});

const page = fs.readFileSync(new URL("../src/pages/LearningPlayerPage.tsx", import.meta.url), "utf8");
const startEffect = page.slice(page.indexOf("  // Start only unsubmitted work"), page.indexOf("  // Initialize or update activeQuestionId"));
function runStartEffect(detail: StudentAssignmentDetailDto, loading = false) {
  let resolve!: (value: unknown) => void;
  let reject!: (error: unknown) => void;
  const request = new Promise((res, rej) => { resolve = res; reject = rej; });
  const errors: unknown[] = [], cache: unknown[] = [];
  let calls = 0, refetches = 0;
  const state = {
    assignmentId: detail.assignmentId, assignmentDraftKey: "scope", assignmentLoading: loading,
    assignment: detail, canStartAssignment: shouldStartAssignment(detail),
    isAssignmentSubmitted: isAssignmentWorkSubmitted(detail),
    assignmentReviewRef: { current: isAssignmentWorkSubmitted(detail) },
    currentUser: { centerId: "center", userId: "student" },
    queryClient: { setQueryData: (_key: unknown, value: unknown) => cache.push(value) },
    startStudentAssignment: () => { calls++; return request; },
    refetchAssignment: async () => { refetches++; return { data: { data: state.assignment } }; },
    setStartAssignmentError: (value: unknown) => errors.push(value), isAssignmentWorkSubmitted,
    cleanup: undefined as (() => void) | undefined,
    useEffect: (effect: () => (() => void) | undefined) => { state.cleanup = effect(); },
  };
  vm.runInNewContext(ts.transpileModule(startEffect, { compilerOptions: { target: ts.ScriptTarget.ES2020 } }).outputText, state);
  return { state, errors, cache, resolve, reject, calls: () => calls, refetches: () => refetches,
    flush: async () => { await new Promise<void>(done => setImmediate(done)); } };
}

test("actual start effect waits for detail and never restarts completed assignments", () => {
  assert.equal(runStartEffect(assignment({ startedAt: null }), true).calls(), 0);
  assert.equal(runStartEffect(assignment({ startedAt: null, isSubmitted: true })).calls(), 0);
  assert.equal(runStartEffect(assignment()).calls(), 0);
});

test("late start failure cannot turn accepted submission back into a start-error page", async () => {
  const run = runStartEffect(assignment({ startedAt: null }));
  assert.equal(run.calls(), 1);
  run.state.assignmentReviewRef.current = true;
  run.reject(new Error("expired"));
  await run.flush();
  assert.equal(run.errors.length, 0);
  assert.equal(run.refetches(), 0);
});

test("start rejection refreshes authoritative state before showing an error", async () => {
  const run = runStartEffect(assignment({ startedAt: null }));
  run.state.assignment = assignment({ isSubmitted: true });
  run.reject(new Error("already completed"));
  await run.flush();
  assert.equal(run.refetches(), 1);
  assert.equal(run.errors.length, 0);
});

test("late start success after unmount cannot overwrite the assignment cache", async () => {
  const run = runStartEffect(assignment({ startedAt: null }));
  run.state.cleanup?.();
  run.resolve({ data: assignment() });
  await run.flush();
  assert.equal(run.cache.length, 0);
  assert.equal(run.errors.length, 0);
});

test("active start still updates the cache and displays genuine start errors", async () => {
  const success = runStartEffect(assignment({ startedAt: null }));
  success.resolve({ data: assignment() });
  await success.flush();
  assert.equal(success.cache.length, 1);
  assert.deepEqual(success.errors, [null]);
  const failure = runStartEffect(assignment({ startedAt: null }));
  failure.reject({ response: { data: { error: { message: "Hết hạn" } } } });
  await failure.flush();
  assert.deepEqual(failure.errors, ["Hết hạn"]);
});

test("page clears countdown before initialization and guards both expired/start-error banners", () => {
  const timerEffect = page.slice(page.indexOf("  // Initialize timer for timed"), page.indexOf("  const isTimerTicking"));
  assert.ok(timerEffect.indexOf("if (isAssignmentSubmitted)") < timerEffect.indexOf("if (hasTimeLimit)"));
  assert.match(timerEffect, /targetEndTimestampRef.current = null/);
  assert.match(page, /!isAssignmentSubmitted && isAssignmentExpired &&/);
  assert.match(page, /!isAssignmentSubmitted && startAssignmentError &&/);
  const submit = page.slice(page.indexOf("const submitRes = await submitStudentAssignment"));
  assert.ok(submit.indexOf("assignmentReviewRef.current = true") < submit.indexOf("await refreshSubmittedAssignmentData()"));
});
