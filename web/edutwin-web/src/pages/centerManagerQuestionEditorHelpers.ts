/**
 * Pure helper utilities for CenterManager QuestionEditorPage.
 * Zero DOM dependencies — fully testable via Node test runner.
 */

import katex from "katex";
import {
  type AnswerEditorValue,
  type QuestionType,
  type QuestionAnswerEvaluationMode,
  buildTextExactAnswer,
  buildProseAnswer,
  buildNumericRationalAnswer,
  buildMathEquivalentAnswer,
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
  | "ShortAnswer:MathEquivalent"
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
      case "MathEquivalent":
        return "ShortAnswer:MathEquivalent";
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

  // Real KaTeX syntax validation with throwOnError: true
  try {
    katex.renderToString(cleanLatex, { throwOnError: true });
  } catch (err: unknown) {
    const rawMsg = err instanceof Error ? err.message : String(err);
    const cleanMsg = rawMsg.replace(/^KaTeX parse error:\s*/i, "").trim();
    return {
      cleanLatex,
      isComplete: false,
      hasPlaceholder: false,
      error: `Công thức sai cú pháp LaTeX (${cleanMsg}).`,
    };
  }

  return {
    cleanLatex,
    isComplete: true,
    hasPlaceholder: false,
  };
}

export type MathFormulaDiagnosticType =
  | "unclosed-delimiter"
  | "empty-formula"
  | "placeholder"
  | "invalid-syntax"
  | "unwrapped-latex";

export interface MathFormulaDiagnostic {
  type: MathFormulaDiagnosticType;
  raw: string;
  message: string;
  position?: number;
}

/**
 * Scans a text string for mathematical formula issues:
 * - Unclosed $ or $$ delimiters (properly handling escaped \$)
 * - Empty formulas ($$ or $$$$ or $   $)
 * - Unfilled \placeholder{}
 * - Invalid KaTeX syntax (throwOnError: true)
 * - Unwrapped raw LaTeX outside delimiters (e.g. \frac{1}{2}, \sqrt{x}, \alpha)
 *
 * Returns structured diagnostics categorized by error cause.
 */
export function validateTextMathFormulas(text: string | null | undefined): MathFormulaDiagnostic[] {
  if (!text) return [];

  const diagnostics: MathFormulaDiagnostic[] = [];
  const len = text.length;
  let i = 0;
  let lastEnd = 0;

  // Helper: check if character at idx is preceded by an odd number of backslashes
  function isEscaped(idx: number): boolean {
    let backslashCount = 0;
    let j = idx - 1;
    while (j >= 0 && text![j] === "\\") {
      backslashCount++;
      j--;
    }
    return backslashCount % 2 === 1;
  }

  // Helper: extracts candidate LaTeX command and scans arguments
  function extractLatexCandidate(str: string, startIdx: number) {
    const cmdMatch = /^\\([a-zA-Z]+)/.exec(str.slice(startIdx));
    if (!cmdMatch) return null;

    let pos = startIdx + cmdMatch[0].length;
    while (pos < str.length) {
      const ch = str[pos];
      if (ch === " " || ch === "\t" || ch === "\n" || ch === "$") {
        break;
      }
      if (ch === "{") {
        let depth = 1;
        let j = pos + 1;
        while (j < str.length && depth > 0) {
          if (str[j] === "{" && str[j - 1] !== "\\") depth++;
          else if (str[j] === "}" && str[j - 1] !== "\\") depth--;
          j++;
        }
        if (depth > 0) {
          return { raw: str.slice(startIdx).trim(), isUnclosedBrace: true, pos: startIdx };
        }
        pos = j;
      } else if (ch === "[") {
        let depth = 1;
        let j = pos + 1;
        while (j < str.length && depth > 0) {
          if (str[j] === "[" && str[j - 1] !== "\\") depth++;
          else if (str[j] === "]" && str[j - 1] !== "\\") depth--;
          j++;
        }
        if (depth > 0) {
          return { raw: str.slice(startIdx).trim(), isUnclosedBrace: true, pos: startIdx };
        }
        pos = j;
      } else if (ch === "_" || ch === "^") {
        pos++;
        if (pos < str.length && (str[pos] === "{" || str[pos] === "[")) {
          continue;
        } else if (pos < str.length && /[a-zA-Z0-9]/.test(str[pos])) {
          pos++;
        }
      } else if (/[a-zA-Z0-9.,=+/*-]/.test(ch)) {
        pos++;
      } else {
        break;
      }
    }

    const raw = str.slice(startIdx, pos).trim();
    return { raw, isUnclosedBrace: false, pos: startIdx };
  }

  // Validate prose segments outside math formula delimiters ($...$, $$...$$)
  function validateProseSegment(segment: string, offset: number) {
    if (!segment) return;

    // 1. Check for unfilled placeholder in prose outside math delimiters
    if (hasUnfilledPlaceholder(segment)) {
      diagnostics.push({
        type: "placeholder",
        raw: segment.trim(),
        message: "Nội dung còn chứa ô trống chưa điền (\\placeholder). Vui lòng hoàn thành mọi vị trí.",
        position: offset,
      });
      return;
    }

    // 2. Check for raw LaTeX command outside delimiters (e.g. \frac{1}{2}, \sqrt{x}, \alpha, \frac{1}{)
    let idx = 0;
    while (idx < segment.length) {
      if (segment[idx] === "\\" && !isEscaped(offset + idx)) {
        const candidate = extractLatexCandidate(segment, idx);
        if (candidate) {
          if (candidate.isUnclosedBrace) {
            diagnostics.push({
              type: "invalid-syntax",
              raw: candidate.raw,
              message: "Công thức sai cú pháp LaTeX (dấu ngoặc nhọn chưa đóng).",
              position: offset + candidate.pos,
            });
            return;
          }

          try {
            katex.renderToString(candidate.raw, { throwOnError: true });
            diagnostics.push({
              type: "unwrapped-latex",
              raw: candidate.raw,
              message: "Công thức LaTeX phải được chèn bằng trình soạn công thức hoặc đặt trong $...$.",
              position: offset + candidate.pos,
            });
            return;
          } catch (err: unknown) {
            const rawMsg = err instanceof Error ? err.message : String(err);
            const cleanMsg = rawMsg.replace(/^KaTeX parse error:\s*/i, "").trim();
            diagnostics.push({
              type: "invalid-syntax",
              raw: candidate.raw,
              message: `Công thức sai cú pháp LaTeX (${cleanMsg}).`,
              position: offset + candidate.pos,
            });
            return;
          }
        }
      }
      idx++;
    }
  }

  while (i < len) {
    if (text[i] === "$" && !isEscaped(i)) {
      // Validate prose segment preceding this math formula
      const prose = text.slice(lastEnd, i);
      validateProseSegment(prose, lastEnd);

      const isDisplay = i + 1 < len && text[i + 1] === "$";
      const delim = isDisplay ? "$$" : "$";
      const delimLen = delim.length;
      const startIndex = i;
      const contentStart = i + delimLen;

      i = contentStart;
      let closed = false;
      let contentEnd = -1;

      while (i < len) {
        if (text[i] === "$" && !isEscaped(i)) {
          if (isDisplay) {
            if (i + 1 < len && text[i + 1] === "$") {
              closed = true;
              contentEnd = i;
              i += 2;
              break;
            }
          } else {
            closed = true;
            contentEnd = i;
            i += 1;
            break;
          }
        }
        i++;
      }

      if (!closed) {
        const raw = text.slice(startIndex);
        diagnostics.push({
          type: "unclosed-delimiter",
          raw,
          message: `Công thức toán chưa được đóng dấu "${delim}". Vui lòng thêm "${delim}" đóng ở cuối công thức.`,
          position: startIndex,
        });
        lastEnd = len;
        break;
      }

      lastEnd = i;
      const rawFormula = text.slice(startIndex, i);
      const mathContent = text.slice(contentStart, contentEnd);
      const trimmedMath = mathContent.trim();

      // Check empty formula
      if (!trimmedMath) {
        diagnostics.push({
          type: "empty-formula",
          raw: rawFormula,
          message: `Công thức toán "${rawFormula}" không được để trống.`,
          position: startIndex,
        });
        continue;
      }

      // Check unfilled placeholder inside formula
      if (hasUnfilledPlaceholder(trimmedMath)) {
        diagnostics.push({
          type: "placeholder",
          raw: rawFormula,
          message: `Công thức "${rawFormula}" còn ô trống chưa điền (\\placeholder). Vui lòng hoàn tất trước khi lưu.`,
          position: startIndex,
        });
        continue;
      }

      // Check KaTeX syntax
      try {
        katex.renderToString(trimmedMath, { throwOnError: true });
      } catch (err: unknown) {
        const rawMsg = err instanceof Error ? err.message : String(err);
        const cleanMsg = rawMsg.replace(/^KaTeX parse error:\s*/i, "").trim();
        diagnostics.push({
          type: "invalid-syntax",
          raw: rawFormula,
          message: `Công thức "${rawFormula}" sai cú pháp LaTeX (${cleanMsg}).`,
          position: startIndex,
        });
      }
      continue;
    }

    i++;
  }

  // Validate trailing prose segment after the last formula (or the entire text if no formulas)
  if (lastEnd < len) {
    const trailingProse = text.slice(lastEnd);
    validateProseSegment(trailingProse, lastEnd);
  }

  return diagnostics;
}

/**
 * Formats a user-friendly error message based on the exact diagnostic category.
 */
export function formatFormulaDiagnosticMessage(
  fieldLabel: string,
  diag: MathFormulaDiagnostic
): string {
  switch (diag.type) {
    case "unclosed-delimiter":
      return `${fieldLabel}: ${diag.message}`;
    case "empty-formula":
      return `${fieldLabel}: ${diag.message}`;
    case "placeholder":
      return `${fieldLabel} chứa công thức chưa hoàn thành (còn ô trống \\placeholder). Vui lòng hoàn tất trước khi lưu.`;
    case "invalid-syntax":
      return `${fieldLabel}: ${diag.message}`;
    case "unwrapped-latex":
      return `${fieldLabel}: ${diag.message}`;
  }
}

/**
 * Scans a text string for any inline $...$ or $$...$$ formulas or raw LaTeX that still contain \placeholder{}
 * or have invalid LaTeX syntax or unclosed delimiters.
 * Kept for backwards compatibility; maps directly from validateTextMathFormulas.
 */
export function findIncompleteFormulasInText(text: string | null | undefined): string[] {
  return validateTextMathFormulas(text).map((d) => d.raw);
}

/** MathEquivalent answers are a single formula, not prose requiring dollar delimiters. */
export function validateAnswerMathFormulas(answer: string, evaluationMode: string): MathFormulaDiagnostic[] {
  return validateTextMathFormulas(
    evaluationMode === "MathEquivalent" && !answer.includes("$") ? `$${answer}$` : answer,
  );
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
    case "MathEquivalent":
      return buildMathEquivalentAnswer(raw, raw);
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
  val: AnswerEditorValue | string | null | undefined,
  evaluationMode: QuestionAnswerEvaluationMode | string
): string {
  if (!val) return "";
  if (typeof val === "string") return val.trim();
  const raw = (val.rawText ?? "").trim();

  if (evaluationMode === "MathEquivalent") return (val.displayLatex || raw).trim();

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

  // Defensive validation against unclosed delimiter, empty, placeholder, or invalid syntax across all fields
  const qTextDiag = validateTextMathFormulas(formData.questionText)[0];
  if (qTextDiag) {
    return {
      payload: formData,
      error: formatFormulaDiagnosticMessage("Nội dung câu hỏi", qTextDiag),
    };
  }

  if (formData.solution) {
    const solDiag = validateTextMathFormulas(formData.solution)[0];
    if (solDiag) {
      return {
        payload: formData,
        error: formatFormulaDiagnosticMessage("Lời giải", solDiag),
      };
    }
  }

  if (formData.options && Array.isArray(formData.options)) {
    for (let index = 0; index < formData.options.length; index++) {
      const opt = formData.options[index];
      const optText = opt.optionText ?? (opt as any).text;
      const optLabel = opt.optionLabel ?? (opt as any).label ?? String.fromCharCode(65 + index);
      const optDiag = validateTextMathFormulas(optText)[0];
      if (optDiag) {
        return {
          payload: formData,
          error: formatFormulaDiagnosticMessage(
            `Phương án ${optLabel}`,
            optDiag
          ),
        };
      }
    }
  }

  if (authoritativeCorrectAnswer) {
    const ansDiag = validateAnswerMathFormulas(authoritativeCorrectAnswer, evalMode)[0];
    if (ansDiag) {
      return {
        payload: formData,
        error: formatFormulaDiagnosticMessage("Đáp án chuẩn", ansDiag),
      };
    }
  }

  const payload: CreateQuestionRequest = {
    ...formData,
    answerEvaluationMode: evalMode,
    correctAnswer: authoritativeCorrectAnswer,
  };

  return { payload };
}
