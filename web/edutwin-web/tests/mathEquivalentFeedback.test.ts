import assert from "node:assert/strict";
import test from "node:test";
import { buildMathEquivalentAnswer, buildSubmissionAnswerFields, resolveAnswerInputType,
  resolveReadonlyDisplay, buildTextExactAnswer } from "../src/components/math/answer-editor/answerEditorHelpers.ts";
import { buildAuthoritativeQuestionPayload, getAnswerDraftKey, resetAndHydrateDraftStore,
  serializeAnswerEditorValue, validateAnswerMathFormulas } from "../src/pages/centerManagerQuestionEditorHelpers.ts";
import { getAttemptFeedbackPresentation, normalizeQuestionScore, questionAssignmentContribution, toInternalQuestionScore, fallbackAssignmentGrade } from "../src/utils/attemptFeedbackPresentation.ts";
import { MATH_EQUIVALENT_HELP, validateQuestionImportModes } from "../src/utils/questionEvaluationModes.ts";
import type { CreateQuestionRequest } from "../src/types/questions.ts";
import type { AttemptFeedbackAnalysisDto } from "../src/types/learning.ts";

const domain = "D=\\mathbb{R}\\setminus\\{2\\}";

test("assignment fallback uses equal normalized weights, fixed total of ten, and includes unanswered questions", () => {
  assert.deepEqual(fallbackAssignmentGrade([{ score: 50, maxScore: 100 }, { score: 20, maxScore: 20 }]), { awardedScore: 7.5, maxScore: 10 });
  assert.deepEqual(fallbackAssignmentGrade([{ score: 100, maxScore: 100 }, { score: null, maxScore: 20 }]), { awardedScore: 5, maxScore: 10 });
  assert.deepEqual(fallbackAssignmentGrade([{ isVoided: true, maxScore: 100 }, { score: 0, maxScore: 20 }]), { awardedScore: 5, maxScore: 10 });
  assert.deepEqual(fallbackAssignmentGrade([]), { awardedScore: 0, maxScore: 10 });
});
const analysis = (overrides: Partial<AttemptFeedbackAnalysisDto> = {}): AttemptFeedbackAnalysisDto => ({
  analysisId: "analysis", schemaVersion: "1.0", missingSteps: [], rootCauseNodes: [],
  feedback: "Cách giải hợp lệ.", isFallback: false, needsTeacherReview: false,
  hasTeacherOverride: false, ...overrides,
});

test("Groq analysis is labeled with its actual provider, never Gemini or system fallback", () => {
  const presentation = getAttemptFeedbackPresentation({ source: "Deterministic", isCorrect: true, maxScore: 10 },
    analysis({ feedbackOrigin: "Groq", isRawAI: true }), "Completed");
  assert.equal(presentation.feedbackLabel, "Nhận xét từ AI (Groq)");
  assert.equal(presentation.isAI, true);
  assert.equal(presentation.isGemini, false);
});

test("MathEquivalent uses visual math input only for ShortAnswer; MCQ and Essay fail closed", () => {
  assert.deepEqual(resolveAnswerInputType("ShortAnswer", "MathEquivalent"), { type: "math-equivalent", isValid: true });
  assert.equal(resolveAnswerInputType("Essay", "MathEquivalent").isValid, false);
  assert.equal(resolveAnswerInputType("MultipleChoice", "MathEquivalent").isValid, false);
  assert.deepEqual(resolveAnswerInputType("ShortAnswer", "TextExact"), { type: "plain-text", isValid: true });
});

test("MathEquivalent authoring and student submission retain LaTeX instead of ASCII approximation", () => {
  const value = buildMathEquivalentAnswer("D=R{2}", domain);
  assert.equal(value.rawText, domain);
  assert.deepEqual(buildSubmissionAnswerFields("ShortAnswer", "MathEquivalent", value), {
    finalAnswer: domain, answerDisplayLatex: domain,
  });
  assert.equal(serializeAnswerEditorValue({ rawText: "D=R{2}", displayLatex: domain }, "MathEquivalent"), domain);
  assert.equal(buildMathEquivalentAnswer("1/2", null).rawText, "1/2");
  assert.deepEqual(resolveReadonlyDisplay("MathEquivalent", value), { mode: "formula", content: domain });
});

test("MathEquivalent drafts hydrate independently from literal text drafts", () => {
  const store = resetAndHydrateDraftStore({ questionType: "ShortAnswer", answerEvaluationMode: "MathEquivalent", correctAnswer: domain }, true);
  assert.equal(getAnswerDraftKey("ShortAnswer", "MathEquivalent"), "ShortAnswer:MathEquivalent");
  assert.equal(store["ShortAnswer:MathEquivalent"]?.rawText, domain);
  assert.equal(store["ShortAnswer:TextExact"], undefined);
  assert.deepEqual(resetAndHydrateDraftStore(null, false), {});
  const literal = buildTextExactAnswer("D=R\\{2}");
  assert.deepEqual(buildSubmissionAnswerFields("ShortAnswer", "TextExact", literal), { finalAnswer: "D=R\\{2}", answerDisplayLatex: "" });
});

test("authoring accepts complete MathEquivalent formulas without prose delimiters, rejects unfinished slots", () => {
  assert.deepEqual(validateAnswerMathFormulas(domain, "MathEquivalent"), []);
  assert.equal(validateAnswerMathFormulas("\\frac{1}{\\placeholder{}}", "MathEquivalent")[0]?.type, "placeholder");
  const formData: CreateQuestionRequest = {
    subjectId: "subject", primaryTopicNodeId: "topic", questionType: "ShortAnswer", answerEvaluationMode: "MathEquivalent",
    difficulty: 1, questionText: "Tìm tập xác định.", correctAnswer: domain,
    solution: "Mẫu số khác 0.", maxScore: 20, estimatedTimeSeconds: 120, reasoningRequired: true, languageCode: "vi",
  };
  const result = buildAuthoritativeQuestionPayload({ formData, modeDrafts: {
    "ShortAnswer:MathEquivalent": buildMathEquivalentAnswer(domain, domain),
  } });
  assert.equal(result.error, undefined);
  assert.equal(result.payload.correctAnswer, domain);
  assert.equal(result.payload.answerEvaluationMode, "MathEquivalent");
});

test("import mode compatibility accepts bounded MathEquivalent, rejects unknown/invalid combinations", () => {
  assert.deepEqual(validateQuestionImportModes([
    { rowIndex: 1, questionType: "ShortAnswer", answerEvaluationMode: "MathEquivalent" },
    { rowIndex: 2, questionType: "Essay" },
    { rowIndex: 3, questionType: "MultipleChoice" },
  ]), []);
  const errors = validateQuestionImportModes([
    { rowIndex: 4, questionType: "MultipleChoice", answerEvaluationMode: "MathEquivalent" },
    { rowIndex: 5, questionType: "ShortAnswer", answerEvaluationMode: "CAS" },
  ]);
  assert.equal(errors.length, 2);
  assert.match(errors[0], /Dòng 4/);
  assert.match(errors[1], /Dòng 5/);
  assert.match(MATH_EQUIVALENT_HELP, /Không phải CAS/);
  assert.match(MATH_EQUIVALENT_HELP, /giáo viên xem xét/);
});

test("legacy/unknown/canned feedback is never presented as Gemini feedback", () => {
  const grade = { maxScore: 20, isCorrect: false, awardedScore: 0, source: "Deterministic" };
  assert.equal(getAttemptFeedbackPresentation(grade, analysis({ feedbackOrigin: "LegacySystem" }), "Completed").isGemini, false);
  assert.match(getAttemptFeedbackPresentation(grade, analysis({ feedbackOrigin: "LegacySystem" }), "Completed").feedbackLabel, /dữ liệu cũ/);
  assert.equal(getAttemptFeedbackPresentation(grade, analysis(), "Completed").isGemini, false);
  assert.equal(getAttemptFeedbackPresentation(grade, analysis({ isRawAI: true }), "Completed").isGemini, false);
  assert.equal(getAttemptFeedbackPresentation(grade, analysis({ feedbackOrigin: "Gemini", isRawAI: false }), "Completed").isGemini, false);
  assert.match(getAttemptFeedbackPresentation(grade, analysis({ feedbackOrigin: "RuleBased" }), "Completed").feedbackLabel, /quy tắc/);
  assert.equal(getAttemptFeedbackPresentation(grade, analysis({ feedbackOrigin: "Gemini", isRawAI: true }), "Completed").isGemini, true);
  assert.equal(getAttemptFeedbackPresentation(grade, analysis({ feedbackOrigin: "Gemini", isRawAI: true, isFallback: true }), "Completed").isGemini, false);
});

test("answer disagreement is a review notice, not an AI-based score override", () => {
  const grade = { maxScore: 20, isCorrect: false, awardedScore: 0, source: "Deterministic" };
  const view = getAttemptFeedbackPresentation(grade, analysis({ answerAssessment: "Correct", reasoningQuality: 95 }), "Completed");
  assert.equal(view.answerDisagreement, true);
  assert.equal(view.needsReview, true);
  assert.equal(view.pendingTeacher, false); // UI must not invent a queued review.
  assert.match(view.scoreSourceLabel, /quy tắc/);
  assert.equal(grade.isCorrect, false);
  assert.equal(grade.awardedScore, 0);
  assert.equal(getAttemptFeedbackPresentation(grade, analysis({ reasoningQuality: 100 }), "Completed").answerDisagreement, false);
});

test("correct final answer with fallacious reasoning remains correct but exposes reasoning concerns", () => {
  const grade = { maxScore: 10, isCorrect: true, awardedScore: 10, source: "Deterministic" };
  const view = getAttemptFeedbackPresentation(grade, analysis({ reasoningVerdict: "Invalid", misconception: "Gạch bỏ chữ số 6 trong 16/64.", reasoningQuality: 99 }), "Completed");
  assert.equal(view.hasReasoningConcerns, true);
  assert.equal(view.needsReview, true);
  assert.equal(grade.awardedScore, 10);
  assert.equal(grade.isCorrect, true);
});

test("uncertainty and actual PendingTeacher states are distinct from teacher-resolved disagreements", () => {
  const grade = { maxScore: 20, source: "Deterministic" };
  assert.equal(getAttemptFeedbackPresentation(grade, analysis({ reasoningVerdict: "Uncertain" }), "Completed").needsReview, true);
  const pending = getAttemptFeedbackPresentation({ ...grade, source: "PendingTeacher" }, analysis(), "NeedsTeacherReview");
  assert.equal(pending.pendingTeacher, true);
  assert.equal(pending.needsReview, true);
  const reviewed = getAttemptFeedbackPresentation({ ...grade, source: "Teacher", isCorrect: true }, analysis({ answerAssessment: "Incorrect" }), "Completed");
  assert.equal(reviewed.needsReview, false);
  assert.equal(reviewed.scoreSourceLabel, "Giáo viên xác nhận");
});

test("question grade is out of ten in both roles, regardless of internal maximum or question count", () => {
  assert.deepEqual(normalizeQuestionScore(10, 10, 2), { awardedScore: 10, maxScore: 10 });
  assert.deepEqual(normalizeQuestionScore(20, 20, 2), { awardedScore: 10, maxScore: 10 });
  assert.deepEqual(normalizeQuestionScore(10, 20, 2), { awardedScore: 5, maxScore: 10 });
  assert.deepEqual(normalizeQuestionScore(null, 20, 2), { awardedScore: null, maxScore: 10 });
  assert.deepEqual(normalizeQuestionScore(10, 20), { awardedScore: 5, maxScore: 10 });
  assert.deepEqual(normalizeQuestionScore(50, 100, 0), { awardedScore: 5, maxScore: 10 });
});

test("assignment contribution is explicitly separate; normalization round-trip does not change stored grade", () => {
  assert.deepEqual(questionAssignmentContribution(10, 20, 2), { awardedScore: 2.5, maxScore: 5 });
  assert.deepEqual(questionAssignmentContribution(null, 20, 2), { awardedScore: null, maxScore: 5 });
  assert.equal(questionAssignmentContribution(10, 20, 0), null);
  for (const max of [10, 20, 100]) {
    assert.equal(toInternalQuestionScore(7.5, max), max * 0.75);
    assert.equal(normalizeQuestionScore(max * 0.75, max).awardedScore, 7.5);
  }
});
