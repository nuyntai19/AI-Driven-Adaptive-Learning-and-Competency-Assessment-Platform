// Students write one free-form work box; these helpers extract the answer the
// server grades from it. The graders parse the answer strictly, so "Vậy x = 5"
// or "$\frac{1}{2}$" must be reduced to "5" and "\frac{1}{2}" before submitting.

const MATH_MODES = new Set(["NumericRational", "MathEquivalent", "Coordinate2D"]);
const LABEL_WITH_COLON = /^(?:đáp\s*số|đáp\s*án|kết\s*quả|kết\s*luận|answer|result)\s*[:：]\s*/iu;
const MATH_LEAD_IN = /^(?:đáp\s*số|đáp\s*án|kết\s*quả|kết\s*luận|vậy|do\s*đó|suy\s*ra|nên|=>|⇒|→|∴)\s*[:：,]?\s*/iu;
const WHOLE_LINE_MATH = /^(?:\$\$([\s\S]+)\$\$|\$([^$]+)\$|\\\(([\s\S]+)\\\))$/u;

const FORMULA = /\$\$([\s\S]+?)\$\$|\$([^$]+)\$|\\\(([\s\S]+?)\\\)/gu;
// Formulas joined only by these are one answer: "$x=1$; $y=2$", "$x=2$ hoặc $x=3$".
const LIST_JOINER = /^\s*(?:[;,]|và|hoặc)\s*$/iu;

/**
 * The answer inside a written sentence: "Thay $x=2$ được $12$" concludes with its
 * last formula. Undefined when the line has none, or its value sits outside the
 * formula ("$y'(2)$ = 12"), so the whole line has to be read instead.
 */
function concludingFormula(text: string): string | undefined {
  const formulas = [...text.matchAll(FORMULA)];
  if (formulas.length === 0) return undefined;
  const last = formulas[formulas.length - 1];
  if (/[\d=]/u.test(text.slice(last.index + last[0].length))) return undefined;

  let first = formulas.length - 1;
  while (first > 0) {
    const previous = formulas[first - 1];
    if (!LIST_JOINER.test(text.slice(previous.index + previous[0].length, formulas[first].index))) break;
    first -= 1;
  }
  return formulas.slice(first).map((match) => (match[1] ?? match[2] ?? match[3]).trim()).join("; ");
}

function stripRepeated(text: string, pattern: RegExp): string {
  let current = text;
  for (;;) {
    const next = current.replace(pattern, "").trim();
    if (next === current) return current;
    current = next;
  }
}

/** Reduces one written line to the form the server's grader for `evaluationMode` accepts. */
export function normalizeFinalAnswer(line: string, evaluationMode?: string | null): string {
  const text = line.trim().replace(LABEL_WITH_COLON, "").trim();
  if (!MATH_MODES.has(evaluationMode ?? "")) {
    const wrapped = text.match(WHOLE_LINE_MATH);
    return wrapped ? (wrapped[1] ?? wrapped[2] ?? wrapped[3]).trim() : text;
  }

  let math = stripRepeated(concludingFormula(text) ?? text, MATH_LEAD_IN)
    .replace(/\$/g, "")
    .replace(/\\\(|\\\)/g, "")
    .replace(/\\[dt]frac/g, "\\frac")
    .trim();
  // "x = 10/2 = 5" concludes with 5, but "x = 1; y = 2" is a pair of values.
  const assignments = math.split("=").length - 1;
  if (assignments === 1 || (assignments > 1 && !/[;,]/.test(math))) {
    math = math.slice(math.lastIndexOf("=") + 1).trim();
  }
  return math.replace(/[.。]+$/u, "").trim();
}

/**
 * The answer submitted for a work box: the full text for essays, otherwise its last non-empty line.
 * With `wholeWork` (language subjects) the answer is a sentence that may wrap over lines, so every
 * line counts, unless the last one is explicitly marked "Answer: ...".
 */
export function deriveFinalAnswer(work: string, questionType?: string | null, evaluationMode?: string | null, wholeWork = false): string {
  if (questionType === "Essay") return work.trim();
  const lines = work.split("\n").map((line) => line.trim()).filter(Boolean);
  if (lines.length === 0) return "";
  const last = lines[lines.length - 1];
  return normalizeFinalAnswer(wholeWork && !LABEL_WITH_COLON.test(last) ? lines.join(" ") : last, evaluationMode);
}

const MATH_FUNCTION_NAMES = /^(?:sin|cos|tan|cot|ln|log|lim|max|min|arcsin|arccos|arctan)$/iu;

/** False when the grader would not be able to read this answer, so the student can fix it before submitting. */
export function isAnswerReadable(answer: string, evaluationMode?: string | null): boolean {
  if (!answer.trim()) return false;
  if (!MATH_MODES.has(evaluationMode ?? "")) return true;
  const withoutCommands = answer.replace(/\\[a-zA-Z]+/g, "");
  if (evaluationMode === "NumericRational") return !/\p{L}/u.test(withoutCommands);
  if (/[^\p{ASCII}]/u.test(withoutCommands.replace(/[²³√∞≠≤≥±·×÷−]/gu, ""))) return false;
  return (withoutCommands.match(/[a-zA-Z]{2,}/g) ?? []).every((word) => MATH_FUNCTION_NAMES.test(word));
}
