import assert from "node:assert/strict";
import test from "node:test";
import { resolveQuestionDefaultFormValues, questionGradingActions, questionAnswerVerdictBadge } from "../src/utils/gradingWorkspaceHelpers.ts";
import type { TeacherReviewQueueItemDto } from "../src/types/reviews.ts";

test("answer verdict badge preserves authoritative true/false over conflicting AI opinions", () => {
  const q = { isFallback: false, reasoningQuality: 100, suggestedScore: 10 } as TeacherReviewQueueItemDto;
  assert.deepEqual(questionAnswerVerdictBadge({ ...q, isCorrect: true, answerAssessment: "Incorrect" }),
    { label: "✓ Học sinh làm đúng", tone: "correct" });
  assert.deepEqual(questionAnswerVerdictBadge({ ...q, isCorrect: false, answerAssessment: "Correct" }),
    { label: "✗ Học sinh làm sai", tone: "incorrect" });
});

test("legacy ungraded essay displays the AI opinion as pending without inventing a score", () => {
  const q = { isCorrect: null, awardedScore: null, suggestedScore: null, isFallback: false,
    answerAssessment: "Correct", attemptStatus: "NeedsTeacherReview", reasoningQuality: 100 } as TeacherReviewQueueItemDto;
  const before = structuredClone(q);
  assert.deepEqual(questionAnswerVerdictBadge(q),
    { label: "AI đánh giá đúng · Chờ giáo viên duyệt", tone: "pending" });
  assert.deepEqual(q, before);
  assert.deepEqual(questionAnswerVerdictBadge({ ...q, suggestedScore: 0, answerAssessment: "Incorrect" }),
    { label: "AI đánh giá chưa đúng · Chờ giáo viên duyệt", tone: "pending" });
});

test("null, missing, uncertain and fallback verdicts never display student wrong", () => {
  const cases = [null, undefined,
    { isCorrect: null, isFallback: false },
    { isFallback: false, answerAssessment: "Uncertain", suggestedScore: 0 },
    { isCorrect: null, isFallback: true, answerAssessment: "Incorrect", reasoningQuality: 100 },
    { isCorrect: null, isFallback: true, answerAssessment: "Correct", suggestedScore: 10 }];
  for (const q of cases) {
    assert.deepEqual(questionAnswerVerdictBadge(q as TeacherReviewQueueItemDto | null | undefined),
      { label: "Chưa có kết luận · Chờ giáo viên duyệt", tone: "pending" });
  }
});

test("processing answer stays pending even if a stale AI opinion exists", () => {
  for (const attemptStatus of ["PendingAnalysis", "Processing"]) {
    assert.deepEqual(questionAnswerVerdictBadge({ isCorrect: null, isFallback: false,
      answerAssessment: "Correct", attemptStatus } as TeacherReviewQueueItemDto),
      { label: "Đang chờ chấm", tone: "pending" });
  }
});

test("resolveQuestionDefaultFormValues preserves reasoningQuality = 0 without converting to 80", () => {
  const mockQuestion = {
    reasoningQuality: 0,
    isCorrect: false,
    errorType: "Reasoning",
  } as unknown as TeacherReviewQueueItemDto;

  const values = resolveQuestionDefaultFormValues(mockQuestion);
  assert.equal(values.reasoningQuality, 0, "0 reasoning quality must remain 0");
});

test("manual AI proposal is normalized to /10 and can be approved without becoming a final grade", () => {
  const q = { isCorrect: null, awardedScore: null, maxScore: 20, suggestedScore: 16,
    answerAssessment: "Correct", isFallback: false, attemptStatus: "NeedsTeacherReview", reasoningQuality: 95,
    gradingCriteria: { criteria: [{ criterionId: "method" }] }, suggestedRubricGrade: { awardedScore: 16 } } as unknown as TeacherReviewQueueItemDto;
  assert.equal(resolveQuestionDefaultFormValues(q).awardedScore, 8);
  assert.equal(resolveQuestionDefaultFormValues(q).reasoningQuality, 95);
  assert.equal(questionGradingActions(q).canConfirm, true);
  assert.equal(questionGradingActions(q).needsManualGrade, false);
  assert.equal(q.awardedScore, null);
  assert.equal(questionGradingActions({ ...q, suggestedRubricGrade: null }).canConfirm, false);
  assert.equal(questionGradingActions({ ...q, answerAssessment: "Uncertain" }).canConfirm, false);
  assert.equal(questionGradingActions({ ...q, isFallback: true }).canConfirm, false);
  assert.equal(resolveQuestionDefaultFormValues({ ...q, isFallback: true }).isCorrectVal, null);
});

test("deterministic answer grade takes priority over an AI proposal", () => {
  const q = { isCorrect: true, awardedScore: 2, maxScore: 2, suggestedScore: 0,
    answerAssessment: "Incorrect", isFallback: false } as unknown as TeacherReviewQueueItemDto;
  assert.equal(resolveQuestionDefaultFormValues(q).awardedScore, 10);
  assert.equal(resolveQuestionDefaultFormValues(q).isCorrectVal, true);
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
