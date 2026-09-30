import assert from "node:assert/strict";
import test from "node:test";
import { resolveQuestionDefaultFormValues } from "../src/utils/gradingWorkspaceHelpers.ts";
import type { TeacherReviewQueueItemDto } from "../src/types/reviews.ts";

test("resolveQuestionDefaultFormValues preserves reasoningQuality = 0 without converting to 80", () => {
  const mockQuestion = {
    reasoningQuality: 0,
    isCorrect: false,
    errorType: "Reasoning",
  } as unknown as TeacherReviewQueueItemDto;

  const values = resolveQuestionDefaultFormValues(mockQuestion);
  assert.equal(values.reasoningQuality, 0, "0 reasoning quality must remain 0");
});

test("resolveQuestionDefaultFormValues keeps unevaluated question as isCorrectVal = null", () => {
  const mockQuestion = {
    isCorrect: null,
    evidence: { decisionMode: "Deterministic", trustLevel: "Trusted" },
  } as unknown as TeacherReviewQueueItemDto;

  const values = resolveQuestionDefaultFormValues(mockQuestion);
  assert.equal(values.isCorrectVal, null, "Unevaluated question must remain null to prevent false positive/negative bias");
});

test("resolveQuestionDefaultFormValues preserves evaluated isCorrect true/false states", () => {
  const trueQuestion = { isCorrect: true } as unknown as TeacherReviewQueueItemDto;
  const falseQuestion = { isCorrect: false } as unknown as TeacherReviewQueueItemDto;

  assert.equal(resolveQuestionDefaultFormValues(trueQuestion).isCorrectVal, true);
  assert.equal(resolveQuestionDefaultFormValues(falseQuestion).isCorrectVal, false);
});

test("resolveQuestionDefaultFormValues preserves question errorType rather than forcing None", () => {
  const mockQuestion = {
    errorType: "Knowledge",
  } as unknown as TeacherReviewQueueItemDto;

  const values = resolveQuestionDefaultFormValues(mockQuestion);
  assert.equal(values.errorTypeVal, "Knowledge", "Existing errorType must be preserved");
});

test("resolveQuestionDefaultFormValues falls back to safe defaults when currentQuestion is null/undefined", () => {
  const nullValues = resolveQuestionDefaultFormValues(null);
  assert.equal(nullValues.reasoningQuality, 80);
  assert.equal(nullValues.errorTypeVal, "None");
  assert.equal(nullValues.isCorrectVal, null);
  assert.equal(nullValues.feedbackVal, "");
  assert.equal(nullValues.overrideReasonVal, "");
});
