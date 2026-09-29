/**
 * Pure helper utilities for ModeAwareAnswerEditor.
 * Zero DOM dependencies — fully testable via Node test runner.
 */

import { shouldSyncExternalValue } from "../../../utils/visualMathFieldLifecycle.ts";
export { shouldSyncExternalValue };

export type AnswerEditorProfile = "authoring" | "answering" | "readonly";

export type QuestionAnswerEvaluationMode =
  | "TextExact"
  | "NumericRational"
  | "Coordinate2D"
  | "Manual";

export type QuestionType = "MultipleChoice" | "ShortAnswer" | "Essay";

export interface AnswerEditorValue {
  /** Plain text or serialized backend API value */
  rawText: string;
  /** LaTeX formatted value for visual MathLive display and KaTeX preview (strictly "" for TextExact/Manual/Essay) */
  displayLatex: string;
}

export type ResolvedInputType =
  | "option-selector"
  | "plain-text"
  | "numeric-rational"
  | "coordinate-2d"
  | "manual-short-answer"
  | "essay-prose"
  | "unsupported";

export interface InputTypeResolution {
  type: ResolvedInputType;
  isValid: boolean;
  errorMessage?: string;
}

export interface AnswerEditorRef {
  insertLatex: (latex: string) => void;
  insertAtCursor?: (latex: string) => void;
  focus: () => void;
  clear: () => void;
  getValue: () => AnswerEditorValue;
}

/**
 * Builds AnswerEditorValue for TextExact questions.
 * Invariant: displayLatex is always empty "".
 */
export function buildTextExactAnswer(text: string | null | undefined): AnswerEditorValue {
  return {
    rawText: text ?? "",
    displayLatex: "",
  };
}

/**
 * Builds AnswerEditorValue for Prose (Essay / ShortAnswer Manual) questions.
 * Invariant: displayLatex is always empty "".
 */
export function buildProseAnswer(text: string | null | undefined): AnswerEditorValue {
  return {
    rawText: text ?? "",
    displayLatex: "",
  };
}

/**
 * Builds AnswerEditorValue for NumericRational questions.
 * Invariant: rawText holds the plain/fraction string, displayLatex holds LaTeX for visual display.
 */
export function buildNumericRationalAnswer(
  rawText: string | null | undefined,
  displayLatex: string | null | undefined
): AnswerEditorValue {
  return {
    rawText: rawText ?? "",
    displayLatex: displayLatex ?? "",
  };
}

/**
 * Serializes 2D coordinate components into AnswerEditorValue with distinct plainText and latex.
 * Strictly enforces semicolon ';' delimiter to prevent ambiguity with Vietnamese decimal commas.
 * Output:
 *   rawText: (xPlain; yPlain)
 *   displayLatex: \left(xLatex;\,yLatex\right)
 */
export function serializeCoordinateParts(
  x: { plainText: string; latex: string },
  y: { plainText: string; latex: string }
): AnswerEditorValue {
  const xPlain = (x?.plainText ?? "").trim();
  const yPlain = (y?.plainText ?? "").trim();
  const xLatex = (x?.latex ?? "").trim();
  const yLatex = (y?.latex ?? "").trim();

  if (!xPlain && !yPlain && !xLatex && !yLatex) {
    return { rawText: "", displayLatex: "" };
  }

  const rawText = `(${xPlain}; ${yPlain})`;
  const finalXLatex = xLatex || xPlain;
  const finalYLatex = yLatex || yPlain;
  const displayLatex = `\\left(${finalXLatex};\\,${finalYLatex}\\right)`;

  return { rawText, displayLatex };
}

/**
 * Legacy serialization wrapper for single-string coordinates.
 * Calls serializeCoordinateParts under the hood.
 */
export function serializeCoordinate(x: string, y: string): AnswerEditorValue {
  return serializeCoordinateParts(
    { plainText: x, latex: x },
    { plainText: y, latex: y }
  );
}

/**
 * Deserializes a coordinate string into its X and Y plain components.
 * Supports both standard semicolon ';' and legacy comma ',' delimiters for backward compatibility.
 */
export function deserializeCoordinate(raw: string | null | undefined): { x: string; y: string } {
  if (!raw) return { x: "", y: "" };

  let text = raw.trim();

  // Strip optional outer wrappers e.g. ( ... ) or [ ... ] or \left( ... \right)
  text = text
    .replace(/^\\left\s*\(/i, "(")
    .replace(/\\right\s*\)$/i, ")")
    .replace(/^\\left\s*\[/i, "[")
    .replace(/\\right\s*\]$/i, "]");

  if (text.startsWith("(") && text.endsWith(")")) {
    text = text.slice(1, -1).trim();
  } else if (text.startsWith("[") && text.endsWith("]")) {
    text = text.slice(1, -1).trim();
  }

  const cleanPart = (val: string) => val.trim().replace(/^\\([,;: ]|quad|qquad)\s*/, "").trim();

  // Split preferentially by semicolon ';'
  if (text.includes(";")) {
    const parts = text.split(";");
    return {
      x: cleanPart(parts[0] ?? ""),
      y: cleanPart(parts.slice(1).join(";")),
    };
  }

  // Fallback to split by comma ','
  if (text.includes(",")) {
    const parts = text.split(",");
    return {
      x: cleanPart(parts[0] ?? ""),
      y: cleanPart(parts.slice(1).join(",")),
    };
  }

  return { x: cleanPart(text), y: "" };
}

export interface CoordinateAxisValue {
  plainText: string;
  latex: string;
}

export interface Coordinate2DValue {
  x: CoordinateAxisValue;
  y: CoordinateAxisValue;
}

/**
 * Pure helper to update a single coordinate axis and compute the resulting Coordinate2DValue and serialized AnswerEditorValue.
 * Guarantees no side effects and deterministic one-shot state calculation.
 */
export function updateCoordinateAxis(
  current: Coordinate2DValue,
  axis: "x" | "y",
  nextPart: CoordinateAxisValue
): { nextCoords: Coordinate2DValue; nextValue: AnswerEditorValue } {
  const nextCoords: Coordinate2DValue = {
    x: axis === "x" ? nextPart : current.x,
    y: axis === "y" ? nextPart : current.y,
  };
  const nextValue = serializeCoordinateParts(nextCoords.x, nextCoords.y);
  return { nextCoords, nextValue };
}

/**
 * Pure lifecycle helper determining if Coordinate2DInput should resynchronize its internal state from external props.
 * Compares BOTH rawText and displayLatex to ensure complete hydration.
 */
export function shouldSyncCoordinateExternalValue(
  incoming?: AnswerEditorValue | null,
  current?: AnswerEditorValue | null
): boolean {
  const incomingRaw = incoming?.rawText ?? "";
  const incomingLatex = incoming?.displayLatex ?? "";
  const currentRaw = current?.rawText ?? "";
  const currentLatex = current?.displayLatex ?? "";
  return incomingRaw !== currentRaw || incomingLatex !== currentLatex;
}

/**
 * Deserializes a coordinate string with separate plainText and LaTeX support for both axes.
 */
export function deserializeCoordinateWithLatex(
  rawText: string | null | undefined,
  displayLatex?: string | null | undefined
): Coordinate2DValue {
  const plain = deserializeCoordinate(rawText);
  const latex = displayLatex ? deserializeCoordinate(displayLatex) : plain;

  return {
    x: {
      plainText: plain.x,
      latex: latex.x || plain.x,
    },
    y: {
      plainText: plain.y,
      latex: latex.y || plain.y,
    },
  };
}

/**
 * Builds the authoritative backend submission fields based on QuestionType, EvaluationMode, and AnswerEditorValue.
 * Strictly adheres to backend contracts:
 * - MultipleChoice: finalAnswer = optionId, answerDisplayLatex = ""
 * - TextExact: finalAnswer = rawText, answerDisplayLatex = ""
 * - NumericRational: finalAnswer = rawText, answerDisplayLatex = displayLatex
 * - Coordinate2D: finalAnswer = rawText, answerDisplayLatex = displayLatex
 * - Manual / Essay: finalAnswer = rawText, answerDisplayLatex = ""
 */
export function buildSubmissionAnswerFields(
  questionType: QuestionType | string,
  evaluationMode: QuestionAnswerEvaluationMode | string,
  value: AnswerEditorValue | { optionId: string }
): { finalAnswer: string; answerDisplayLatex: string } {
  if (questionType === "MultipleChoice") {
    const optId = "optionId" in value ? value.optionId : (value as AnswerEditorValue).rawText;
    return {
      finalAnswer: optId ?? "",
      answerDisplayLatex: "",
    };
  }

  const editorVal = value as AnswerEditorValue;
  const rawText = editorVal?.rawText ?? "";
  const displayLatex = editorVal?.displayLatex ?? "";

  if (evaluationMode === "NumericRational" || evaluationMode === "Coordinate2D") {
    return {
      finalAnswer: rawText,
      answerDisplayLatex: displayLatex,
    };
  }

  // TextExact and Manual (ShortAnswer or Essay): answerDisplayLatex is strictly empty ""
  return {
    finalAnswer: rawText,
    answerDisplayLatex: "",
  };
}

/**
 * Strict Compatibility Matrix enforcement.
 * Mirrored 100% with EduTwin.BLL.CurriculumAndQuestions.QuestionActivationPolicy.
 * Fails closed on any invalid combination — never silently falls back to plain text.
 */
export function resolveAnswerInputType(
  questionType: QuestionType | string,
  evaluationMode: QuestionAnswerEvaluationMode | string
): InputTypeResolution {
  if (questionType === "MultipleChoice") {
    if (evaluationMode === "TextExact") {
      return { type: "option-selector", isValid: true };
    }
    return {
      type: "unsupported",
      isValid: false,
      errorMessage: `Tổ hợp câu hỏi trắc nghiệm (MultipleChoice) với chế độ chấm '${evaluationMode}' không hợp lệ. Câu trắc nghiệm chỉ chấp nhận chế độ 'TextExact'.`,
    };
  }

  if (questionType === "ShortAnswer") {
    switch (evaluationMode) {
      case "TextExact":
        return { type: "plain-text", isValid: true };
      case "NumericRational":
        return { type: "numeric-rational", isValid: true };
      case "Coordinate2D":
        return { type: "coordinate-2d", isValid: true };
      case "Manual":
        return { type: "manual-short-answer", isValid: true };
      default:
        return {
          type: "unsupported",
          isValid: false,
          errorMessage: `Chế độ chấm '${evaluationMode}' không được hỗ trợ cho câu hỏi trả lời ngắn (ShortAnswer).`,
        };
    }
  }

  if (questionType === "Essay") {
    if (evaluationMode === "Manual") {
      return { type: "essay-prose", isValid: true };
    }
    return {
      type: "unsupported",
      isValid: false,
      errorMessage: `Tổ hợp câu hỏi tự luận (Essay) với chế độ chấm '${evaluationMode}' không hợp lệ. Câu tự luận chỉ hỗ trợ chế độ 'Manual'.`,
    };
  }

  return {
    type: "unsupported",
    isValid: false,
    errorMessage: `Loại câu hỏi '${questionType}' không được hỗ trợ.`,
  };
}

/**
 * Resolves the appropriate string and rendering mode for readonly answer display.
 * Contract (b): If questionType is MultipleChoice, returns empty content to prevent exposing internal optionId.
 */
export function resolveReadonlyDisplay(
  evaluationMode: QuestionAnswerEvaluationMode | string,
  value: AnswerEditorValue,
  questionType?: QuestionType | string
): { content: string; mode: "formula" | "rich" } {
  if (questionType === "MultipleChoice") {
    return { content: "", mode: "rich" };
  }

  const raw = value?.rawText?.trim() ?? "";
  const latex = value?.displayLatex?.trim() ?? "";

  if (evaluationMode === "NumericRational" || evaluationMode === "Coordinate2D") {
    if (latex) {
      return { content: latex, mode: "formula" };
    }
    return { content: raw, mode: "formula" };
  }

  // TextExact and Manual/Essay default to rich text rendering using rawText
  return { content: raw, mode: "rich" };
}

/**
 * Generates non-authoritative client syntax hints.
 * Does not replicate the backend normalizer.
 */
export function getAnswerSyntaxHint(
  evaluationMode: QuestionAnswerEvaluationMode | string,
  value: AnswerEditorValue
): string | null {
  const raw = (value?.rawText ?? "").trim();
  if (!raw) return null;

  if (evaluationMode === "NumericRational") {
    // Basic non-authoritative check for alphabetical letters (excluding valid fraction tokens or LaTeX commands)
    if (/[a-zA-Z]/.test(raw) && !/\\(frac|cdot|times)/.test(raw)) {
      return "Gợi ý cú pháp: Chế độ Số hữu tỉ chỉ chấp nhận số nguyên, số thập phân hoặc phân số (ví dụ: -3, 0.75, 3/4).";
    }
  }

  if (evaluationMode === "Coordinate2D") {
    const { x, y } = deserializeCoordinate(raw);
    if (!x || !y) {
      return "Gợi ý cú pháp: Tọa độ 2D yêu cầu đủ 2 thành phần (x; y).";
    }
  }

  return null;
}
