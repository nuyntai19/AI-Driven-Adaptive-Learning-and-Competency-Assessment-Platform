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

export interface FormulaCleanResult {
  cleanLatex: string;
  isComplete: boolean;
  hasPlaceholder: boolean;
  error?: string;
}

/**
 * Checks if a formula string contains any unfilled MathLive \placeholder{} elements.
 */
export function hasUnfilledPlaceholder(latex: string | null | undefined): boolean {
  if (!latex) return false;
  return /\\placeholder\b/.test(latex);
}

/**
 * Validates whether a formula is complete (not empty and contains no \placeholder{}).
 */
export function validateAndCleanFormula(latex: string | null | undefined): FormulaCleanResult {
  const cleanLatex = cleanFormulaForInsertion(latex);
  if (!cleanLatex) {
    return {
      cleanLatex: "",
      isComplete: false,
      hasPlaceholder: false,
      error: "Công thức không được để trống.",
    };
  }

  const hasPlaceholder = hasUnfilledPlaceholder(cleanLatex);
  if (hasPlaceholder) {
    return {
      cleanLatex,
      isComplete: false,
      hasPlaceholder: true,
      error: "Công thức còn ô trống chưa điền (\\placeholder). Vui lòng hoàn thành mọi vị trí trước khi xác nhận.",
    };
  }

  return {
    cleanLatex,
    isComplete: true,
    hasPlaceholder: false,
  };
}

/**
 * Scans a text string for any inline $...$ or $$...$$ formulas that still contain \placeholder{}.
 * Used for defensive validation on save, activation, or legacy data hydration.
 */
export function findIncompleteFormulasInText(text: string | null | undefined): string[] {
  if (!text) return [];
  const mathMatches = text.match(/(\$\$[\s\S]*?\$\$|\$[^$\n]+?\$)/g) || [];
  const incomplete: string[] = [];
  for (const m of mathMatches) {
    if (hasUnfilledPlaceholder(m)) {
      incomplete.push(m);
    }
  }
  return incomplete;
}

export interface RichSegment {
  id: string;
  type: "text" | "math";
  content: string; // text: plain string; math: LaTeX without outer $
}

/**
 * Parses a mixed-content string (prose + $...$ / $$...$$) into structured RichSegments.
 */
export function parseRichSegments(raw: string | null | undefined): RichSegment[] {
  if (!raw) {
    return [{ id: "seg-1", type: "text", content: "" }];
  }
  const regex = /(\$\$[\s\S]*?\$\$|\$[^$\n]+?\$)/g;
  const parts = raw.split(regex);
  const segments: RichSegment[] = [];
  let idCounter = 1;

  for (const part of parts) {
    if (!part) continue;
    if (part.startsWith("$$") && part.endsWith("$$") && part.length >= 4) {
      segments.push({
        id: `seg-${idCounter++}`,
        type: "math",
        content: part.slice(2, -2).trim(),
      });
    } else if (part.startsWith("$") && part.endsWith("$") && part.length >= 2) {
      segments.push({
        id: `seg-${idCounter++}`,
        type: "math",
        content: part.slice(1, -1).trim(),
      });
    } else {
      segments.push({
        id: `seg-${idCounter++}`,
        type: "text",
        content: part,
      });
    }
  }

  if (segments.length === 0) {
    return [{ id: "seg-1", type: "text", content: "" }];
  }
  return segments;
}

/**
 * Serializes an array of RichSegments back into a canonical mixed-content string for API/storage.
 */
export function serializeRichSegments(segments: RichSegment[]): string {
  if (!segments || segments.length === 0) return "";
  return segments
    .map((seg) => {
      if (seg.type === "math") {
        const clean = cleanFormulaForInsertion(seg.content);
        return clean ? `$${clean}$` : "";
      }
      return seg.content;
    })
    .join("");
}

/**
 * Inserts a clean LaTeX formula enclosed in $...$ into the target string at [selectionStart, selectionEnd].
 * Returns the updated string and the new caret position immediately after the inserted formula.
 *
 * Rules:
 * - If formula is empty/whitespace or has incomplete \placeholder{}: returns original text and unchanged caret.
 * - Prevents nested $ delimiters.
 * - Clamps selection bounds safely within string boundaries.
 */
export function insertFormulaAtCursor(
  text: string,
  selectionStart: number,
  selectionEnd: number,
  formula: string | null | undefined
): { newText: string; newCursorPos: number; error?: string } {
  const validation = validateAndCleanFormula(formula);
  if (!validation.isComplete || !validation.cleanLatex) {
    const safeCaret = Math.min(Math.max(0, selectionStart), text.length);
    return { newText: text, newCursorPos: safeCaret, error: validation.error };
  }

  const cleanLatex = validation.cleanLatex;
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

  // Defensive validation against incomplete MathLive \placeholder{} across all fields
  if (findIncompleteFormulasInText(formData.questionText).length > 0) {
    return {
      payload: formData,
      error: "Nội dung câu hỏi chứa công thức chưa hoàn thành (còn ô trống \\placeholder). Vui lòng hoàn tất trước khi lưu.",
    };
  }

  if (formData.solution && findIncompleteFormulasInText(formData.solution).length > 0) {
    return {
      payload: formData,
      error: "Lời giải chứa công thức chưa hoàn thành (còn ô trống \\placeholder). Vui lòng hoàn tất trước khi lưu.",
    };
  }

  if (formData.options && Array.isArray(formData.options)) {
    for (const opt of formData.options) {
      if (findIncompleteFormulasInText(opt.optionText).length > 0) {
        return {
          payload: formData,
          error: `Phương án ${opt.optionLabel || ""} chứa công thức chưa hoàn thành (còn ô trống \\placeholder).`,
        };
      }
    }
  }

  if (authoritativeCorrectAnswer && findIncompleteFormulasInText(authoritativeCorrectAnswer).length > 0) {
    return {
      payload: formData,
      error: "Đáp án chuẩn chứa công thức chưa hoàn thành (còn ô trống \\placeholder).",
    };
  }

  const payload: CreateQuestionRequest = {
    ...formData,
    answerEvaluationMode: evalMode,
    correctAnswer: authoritativeCorrectAnswer,
  };

  return { payload };
}
