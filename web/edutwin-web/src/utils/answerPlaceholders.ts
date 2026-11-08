const MATH_INPUT_HINT = "(Gõ $ hoặc Ctrl+M để chèn công thức toán)";

/** Whether answers in this subject and question language are written with math tools. */
export function usesMathInput(subjectName?: string | null, languageCode?: string | null): boolean {
  return !/tiếng anh|english|ngoại ngữ|ngữ văn|literature/i.test(subjectName ?? "") && languageCode?.toLowerCase() !== "en";
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
