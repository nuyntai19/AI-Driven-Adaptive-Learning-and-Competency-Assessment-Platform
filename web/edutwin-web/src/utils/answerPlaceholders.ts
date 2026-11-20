const MATH_INPUT_HINT = "(Gõ $ hoặc Ctrl+M để chèn công thức toán)";

/** Input policy uses the catalog code, never the editable display name or question language. */
export function usesMathInput(subjectCode?: string | null): boolean {
  return !new Set(["ENG", "EN", "ENGLISH", "LIT", "VAN", "LITERATURE"]).has(subjectCode?.trim().toUpperCase() ?? "");
}

/** Placeholder of the single answer box for free-response questions. */
export function answerWorkPlaceholder(mathTools: boolean): string {
  return mathTools ? `Trình bày bài làm của em... ${MATH_INPUT_HINT}` : "Viết câu trả lời của em...";
}

/** Placeholder of the explanation box for multiple-choice questions. */
export function answerReasoningPlaceholder(mathTools: boolean): string {
  const text = "Giải thích vì sao em chọn đáp án này...";
  return mathTools ? `${text} ${MATH_INPUT_HINT}` : text;
}
