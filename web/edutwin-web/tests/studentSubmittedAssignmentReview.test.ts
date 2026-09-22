import assert from "node:assert/strict";
import test from "node:test";
import type { StudentAssignmentQuestionDto } from "../src/types/assignments.ts";
import {
  isFeedbackForQuestion,
  resolveQuestionReviewAttemptId,
} from "../src/utils/questionReview.ts";

interface StoredAnswer {
  finalAnswer: string;
  reasoningText: string;
  confidence: number;
  timeSpentSeconds: number;
  answerChanges: number;
  snapshotDataUrl?: string | null;
  snapshotTime?: string | null;
  drawingUploadToken?: string | null;
}

/**
 * Pure helper function mirroring the form sync resolution logic in LearningPlayerPage.tsx
 */
function resolveQuestionFormInputs({
  saved,
  assignmentQuestion,
  isAssignmentSubmitted,
}: {
  saved?: StoredAnswer | null;
  assignmentQuestion?: StudentAssignmentQuestionDto | null;
  isAssignmentSubmitted: boolean;
}) {
  const isQuestionSubmitted =
    assignmentQuestion?.attemptStatus === "Completed" ||
    assignmentQuestion?.attemptStatus === "NeedsTeacherReview" ||
    isAssignmentSubmitted;

  const hasSubmittedData =
    (assignmentQuestion?.submittedAnswer !== undefined && assignmentQuestion?.submittedAnswer !== null) ||
    (assignmentQuestion?.submittedReasoning !== undefined && assignmentQuestion?.submittedReasoning !== null);

  if (isQuestionSubmitted && hasSubmittedData) {
    return {
      finalAnswer: assignmentQuestion?.submittedAnswer || "",
      reasoningText: assignmentQuestion?.submittedReasoning || "",
      confidence: saved?.confidence ?? 80,
      timeSpentSeconds: saved?.timeSpentSeconds ?? 0,
      answerChanges: saved?.answerChanges ?? 0,
      snapshotDataUrl: saved?.snapshotDataUrl || null,
      snapshotTime: saved?.snapshotTime || null,
      drawingUploadToken: saved?.drawingUploadToken || null,
      isReadOnly: true,
    };
  }

  if (saved) {
    return {
      finalAnswer: saved.finalAnswer || "",
      reasoningText: saved.reasoningText || "",
      confidence: saved.confidence ?? 80,
      timeSpentSeconds: saved.timeSpentSeconds ?? 0,
      answerChanges: saved.answerChanges ?? 0,
      snapshotDataUrl: saved.snapshotDataUrl || null,
      snapshotTime: saved.snapshotTime || null,
      drawingUploadToken: saved.drawingUploadToken || null,
      isReadOnly: isQuestionSubmitted,
    };
  }

  if (hasSubmittedData) {
    return {
      finalAnswer: assignmentQuestion?.submittedAnswer || "",
      reasoningText: assignmentQuestion?.submittedReasoning || "",
      confidence: 80,
      timeSpentSeconds: 0,
      answerChanges: 0,
      snapshotDataUrl: null,
      snapshotTime: null,
      drawingUploadToken: null,
      isReadOnly: isQuestionSubmitted,
    };
  }

  return {
    finalAnswer: "",
    reasoningText: "",
    confidence: 80,
    timeSpentSeconds: 0,
    answerChanges: 0,
    snapshotDataUrl: null,
    snapshotTime: null,
    drawingUploadToken: null,
    isReadOnly: isQuestionSubmitted,
  };
}

test("Student submitted assignment review - empty localStorage resolves backend submitted answer and reasoning", () => {
  const submittedQuestion: StudentAssignmentQuestionDto = {
    questionId: "10001",
    questionType: "NumericRational",
    difficulty: 2,
    questionText: "Tìm tập xác định của hàm số y = 1 / (x - 2).",
    estimatedTimeSeconds: 120,
    reasoningRequired: true,
    languageCode: "vi",
    attemptStatus: "NeedsTeacherReview",
    submittedAnswer: "x \\ne 2",
    submittedReasoning: "Mẫu khác 0 cho nên (x-2) khác 0 cho nên x khác 2",
    submittedAttemptId: 10,
    hasAttachment: true,
  };

  // Scenario 1: User cleared localStorage, opened in incognito, or restarted browser
  const result = resolveQuestionFormInputs({
    saved: null,
    assignmentQuestion: submittedQuestion,
    isAssignmentSubmitted: true,
  });

  assert.equal(result.finalAnswer, "x \\ne 2", "Should display submitted answer from backend");
  assert.equal(
    result.reasoningText,
    "Mẫu khác 0 cho nên (x-2) khác 0 cho nên x khác 2",
    "Should display submitted reasoning from backend"
  );
  assert.equal(result.isReadOnly, true, "Submitted question should be read-only");
});

test("Student submitted assignment review - unsubmitted question preserves localStorage draft", () => {
  const unsubmittedQuestion: StudentAssignmentQuestionDto = {
    questionId: "10002",
    questionType: "MultipleChoice",
    difficulty: 1,
    questionText: "1 + 1 = ?",
    estimatedTimeSeconds: 60,
    reasoningRequired: false,
    languageCode: "vi",
    attemptStatus: null,
    submittedAnswer: null,
    submittedReasoning: null,
    submittedAttemptId: null,
    hasAttachment: false,
  };

  const draft: StoredAnswer = {
    finalAnswer: "B",
    reasoningText: "Draft explanation",
    confidence: 90,
    timeSpentSeconds: 45,
    answerChanges: 1,
  };

  const result = resolveQuestionFormInputs({
    saved: draft,
    assignmentQuestion: unsubmittedQuestion,
    isAssignmentSubmitted: false,
  });

  assert.equal(result.finalAnswer, "B", "Draft answer should be preserved");
  assert.equal(result.reasoningText, "Draft explanation", "Draft reasoning should be preserved");
  assert.equal(result.isReadOnly, false, "Unsubmitted question should not be read-only");
});

test("Student submitted assignment review - submitted question overrides stale draft with authoritative submitted answer", () => {
  const submittedQuestion: StudentAssignmentQuestionDto = {
    questionId: "10001",
    questionType: "NumericRational",
    difficulty: 2,
    questionText: "Tìm tập xác định của hàm số y = 1 / (x - 2).",
    estimatedTimeSeconds: 120,
    reasoningRequired: true,
    languageCode: "vi",
    attemptStatus: "Completed",
    submittedAnswer: "\\mathbb{R} \\setminus \\{2\\}",
    submittedReasoning: "x != 2",
    submittedAttemptId: 14,
    hasAttachment: false,
  };

  const staleDraft: StoredAnswer = {
    finalAnswer: "stale draft before submit",
    reasoningText: "stale reasoning",
    confidence: 70,
    timeSpentSeconds: 30,
    answerChanges: 2,
  };

  const result = resolveQuestionFormInputs({
    saved: staleDraft,
    assignmentQuestion: submittedQuestion,
    isAssignmentSubmitted: true,
  });

  assert.equal(
    result.finalAnswer,
    "\\mathbb{R} \\setminus \\{2\\}",
    "Submitted question should use authoritative backend answer instead of stale draft"
  );
  assert.equal(
    result.reasoningText,
    "x != 2",
    "Submitted question should use authoritative backend reasoning instead of stale draft"
  );
  assert.equal(result.isReadOnly, true);
});

test("question review resolves the persisted attempt for each question independently", () => {
  const questions: StudentAssignmentQuestionDto[] = [
    {
      questionId: "10001",
      questionType: "MultipleChoice",
      difficulty: 1,
      questionText: "Question one",
      estimatedTimeSeconds: 60,
      reasoningRequired: false,
      languageCode: "vi",
      submittedAttemptId: "501",
      submittedAnswer: "A",
      attemptStatus: "Completed",
    },
    {
      questionId: "10002",
      questionType: "Essay",
      difficulty: 2,
      questionText: "Question two",
      estimatedTimeSeconds: 120,
      reasoningRequired: true,
      languageCode: "vi",
      submittedAttemptId: "502",
      submittedAnswer: "Second answer",
      attemptStatus: "NeedsTeacherReview",
    },
  ];

  assert.equal(resolveQuestionReviewAttemptId(questions[0]), "501");
  assert.equal(resolveQuestionReviewAttemptId(questions[1]), "502");
  assert.equal(isFeedbackForQuestion(questions[0].questionId, "10001"), true);
  assert.equal(isFeedbackForQuestion(questions[0].questionId, "10002"), false);
});

test("question review falls back to latestAttempt after refresh without frontend draft state", () => {
  const question: StudentAssignmentQuestionDto = {
    questionId: "10003",
    questionType: "Essay",
    difficulty: 3,
    questionText: "Persisted question",
    estimatedTimeSeconds: 180,
    reasoningRequired: true,
    languageCode: "vi",
    attemptStatus: "Completed",
    submittedAttemptId: null,
    latestAttempt: {
      attemptId: "7003",
      status: "Completed",
      finalAnswer: "Persisted answer",
      reasoningText: "Persisted reasoning",
      confidence: 85,
      timeSpentSeconds: 90,
      answerChanges: 1,
      skipped: false,
      submittedAt: "2026-09-20T00:00:00Z",
    },
  };

  assert.equal(resolveQuestionReviewAttemptId(question), "7003");
});
