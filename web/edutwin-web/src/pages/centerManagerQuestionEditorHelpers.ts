/**
 * Pure helper utilities for CenterManager QuestionEditorPage.
 * Zero DOM dependencies — fully testable via Node test runner.
 */

import {
  type AnswerEditorValue,
  type QuestionType,
  type QuestionAnswerEvaluationMode,
  buildTextExactAnswer,
  buildProseAnswer,
  buildNumericRationalAnswer,
  deserializeCoordinate,
  serializeCoordinateParts,
} from "../components/math/answer-editor/answerEditorHelpers.ts";
import type { CreateQuestionRequest } from "../types/questions.ts";

/**
 * Authoritative Draft Store Key:
 * strictly pairs questionType with evaluationMode to prevent cross-contamination
 * between ShortAnswer (concise answer) and Essay (detailed scoring rubric).
 */
export type AnswerDraftKey =
  | "ShortAnswer:TextExact"
  | "ShortAnswer:NumericRational"
  | "ShortAnswer:Coordinate2D"
  | "ShortAnswer:Manual"
  | "Essay:Manual";

export type DraftStore = Partial<Record<AnswerDraftKey, AnswerEditorValue>>;

/**
 * Resolves the AnswerDraftKey for a given questionType and evaluationMode.
 * Returns null if the combination does not use the modeDrafts store (e.g. MultipleChoice).
 */
export function getAnswerDraftKey(
  questionType: QuestionType | string,
  evaluationMode: QuestionAnswerEvaluationMode | string
): AnswerDraftKey | null {
  if (questionType === "ShortAnswer") {
    switch (evaluationMode) {
      case "TextExact":
        return "ShortAnswer:TextExact";
      case "NumericRational":
        return "ShortAnswer:NumericRational";
      case "Coordinate2D":
        return "ShortAnswer:Coordinate2D";
      case "Manual":
        return "ShortAnswer:Manual";
      default:
        return null;
    }
  }

  if (questionType === "Essay") {
    if (evaluationMode === "Manual") {
      return "Essay:Manual";
    }
    return null;
  }

  return null;
}

/**
 * Automatically synchronizes correctAnswer for MultipleChoice questions
 * from the option marked with isCorrect = true.
 */
export function syncMcqCorrectAnswer(
  options?: Array<{ optionLabel?: string; label?: string; isCorrect: boolean }> | null
): string {
  if (!options || !Array.isArray(options)) return "";
  const correctOpt = options.find((o) => o.isCorrect);
  if (!correctOpt) return "";
  return (correctOpt.optionLabel || correctOpt.label || "").trim();
}

/**
 * Strips outer single or double math delimiters ($...$ or $$...$$) and surrounding whitespace
 * so that inline formula insertion does not create nested/broken delimiters.
 */
export function cleanFormulaForInsertion(latex: string | null | undefined): string {
  if (!latex) return "";
  let clean = latex.trim();

  // Strip outer double dollars $$...$$
  if (clean.startsWith("$$") && clean.endsWith("$$") && clean.length >= 4) {
    clean = clean.slice(2, -2).trim();
  }
  // Strip outer single dollar $...$
  else if (clean.startsWith("$") && clean.endsWith("$") && clean.length >= 2) {
    clean = clean.slice(1, -1).trim();
  }

  return clean;
}

/**
 * Inserts a clean LaTeX formula enclosed in $...$ into the target string at [selectionStart, selectionEnd].
 * Returns the updated string and the new caret position immediately after the inserted formula.
 *
 * Rules:
 * - If formula is empty/whitespace: returns original text and unchanged caret.
 * - Prevents nested $ delimiters.
 * - Clamps selection bounds safely within string boundaries.
 */
export function insertFormulaAtCursor(
  text: string,
  selectionStart: number,
  selectionEnd: number,
  formula: string | null | undefined
): { newText: string; newCursorPos: number } {
  const cleanLatex = cleanFormulaForInsertion(formula);
  if (!cleanLatex) {
    const safeCaret = Math.min(Math.max(0, selectionStart), text.length);
    return { newText: text, newCursorPos: safeCaret };
  }

  const safeStart = Math.min(Math.max(0, Math.min(selectionStart, selectionEnd)), text.length);
  const safeEnd = Math.min(Math.max(0, Math.max(selectionStart, selectionEnd)), text.length);

  const formulaToken = `$${cleanLatex}$`;
  const before = text.slice(0, safeStart);
  const after = text.slice(safeEnd);

  const newText = `${before}${formulaToken}${after}`;
  const newCursorPos = safeStart + formulaToken.length;

  return { newText, newCursorPos };
}

/**
 * Hydrates an AnswerEditorValue from a raw backend string based on evaluation mode.
 */
export function hydrateAnswerEditorValue(
  correctAnswer: string | null | undefined,
  evaluationMode: QuestionAnswerEvaluationMode | string
): AnswerEditorValue {
  const raw = correctAnswer ?? "";

  switch (evaluationMode) {
    case "NumericRational":
      return buildNumericRationalAnswer(raw, raw);
    case "Coordinate2D": {
      const { x, y } = deserializeCoordinate(raw);
      return serializeCoordinateParts(
        { plainText: x, latex: x },
        { plainText: y, latex: y }
      );
    }
    case "TextExact":
      return buildTextExactAnswer(raw);
    case "Manual":
      return buildProseAnswer(raw);
    default:
      return buildTextExactAnswer(raw);
  }
}

/**
 * Serializes an AnswerEditorValue into the authoritative raw string for API submission.
 */
export function serializeAnswerEditorValue(
  val: AnswerEditorValue | null | undefined,
  evaluationMode: QuestionAnswerEvaluationMode | string
): string {
  if (!val) return "";
  const raw = (val.rawText ?? "").trim();

  if (evaluationMode === "Coordinate2D") {
    // rawText is already serialized as (xPlain; yPlain)
    return raw;
  }

  return raw;
}

/**
 * Lifecycle Management: Resets and re-hydrates the draft store when switching questions
 * or toggling between create and edit mode.
 *
 * Guarantees that draft answers from Question A NEVER leak into Question B.
 */
export function resetAndHydrateDraftStore(
  questionData?: {
    questionType: QuestionType | string;
    answerEvaluationMode?: QuestionAnswerEvaluationMode | string;
    correctAnswer?: string;
  } | null,
  isEditMode: boolean = false
): DraftStore {
  const store: DraftStore = {};

  if (isEditMode && questionData) {
    const key = getAnswerDraftKey(
      questionData.questionType,
      questionData.answerEvaluationMode ||
        (questionData.questionType === "Essay" ? "Manual" : "TextExact")
    );
    if (key) {
      store[key] = hydrateAnswerEditorValue(
        questionData.correctAnswer,
        questionData.answerEvaluationMode || (questionData.questionType === "Essay" ? "Manual" : "TextExact")
      );
    }
  }

  return store;
}

export interface BuildPayloadOptions {
  formData: CreateQuestionRequest;
  activeDraftValue?: AnswerEditorValue | null;
  modeDrafts: DraftStore;
}

/**
 * Builds the authoritative API payload for Question Create or Update.
 *
 * Enforces Single Source of Truth:
 * - For MultipleChoice: correctAnswer is derived strictly from syncMcqCorrectAnswer(options).
 * - For ShortAnswer & Essay: correctAnswer is derived from activeDraftValue or the resolved key in modeDrafts.
 * - For Essay:Manual: validates that correctAnswer is non-empty (satisfying QuestionActivationPolicy).
 */
export function buildAuthoritativeQuestionPayload({
  formData,
  activeDraftValue,
  modeDrafts,
}: BuildPayloadOptions): {
  payload: CreateQuestionRequest;
  error?: string;
} {
  const qType = formData.questionType;
  const evalMode = formData.answerEvaluationMode || (qType === "Essay" ? "Manual" : "TextExact");

  let authoritativeCorrectAnswer = "";

  if (qType === "MultipleChoice") {
    authoritativeCorrectAnswer = syncMcqCorrectAnswer(formData.options);
    if (!authoritativeCorrectAnswer) {
      return {
        payload: formData,
        error: "Vui lòng chọn một phương án đúng cho câu hỏi trắc nghiệm.",
      };
    }
  } else {
    // ShortAnswer or Essay
    const key = getAnswerDraftKey(qType, evalMode);
    const draft = activeDraftValue ?? (key ? modeDrafts[key] : undefined);
    authoritativeCorrectAnswer = serializeAnswerEditorValue(draft, evalMode);

    if (qType === "Essay") {
      if (!authoritativeCorrectAnswer) {
        return {
          payload: formData,
          error: "Vui lòng nhập đáp án mẫu hoặc rubric chấm điểm chi tiết cho câu hỏi tự luận.",
        };
      }
    } else if (qType === "ShortAnswer") {
      if (!authoritativeCorrectAnswer) {
        return {
          payload: formData,
          error: "Vui lòng nhập đáp án chuẩn cho câu hỏi trả lời ngắn.",
        };
      }
    }
  }

  const payload: CreateQuestionRequest = {
    ...formData,
    answerEvaluationMode: evalMode,
    correctAnswer: authoritativeCorrectAnswer,
  };

  return { payload };
}
