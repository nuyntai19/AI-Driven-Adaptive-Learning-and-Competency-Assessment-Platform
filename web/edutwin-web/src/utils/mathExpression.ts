/**
 * Checks whether a token enclosed in $...$ is actually natural prose
 * (e.g. English or Vietnamese sentences mistakenly wrapped in dollars by LLMs)
 * rather than an actual mathematical expression.
 */
export function isLikelyProse(token: string): boolean {
  const trimmed = token.trim();
  if (!trimmed) return true;

  // If it has standard LaTeX commands like \frac, \sqrt, \alpha, etc., it's definitely math
  if (/\\(frac|sqrt|sum|int|prod|pm|times|div|vec|mathbf|alpha|beta|gamma|theta|pi|le|ge|ne|neq|approx|left|right|cdot)\b/.test(trimmed)) {
    return false;
  }

  // If it has math operator symbols: =, +, *, ^, _, <, >, \
  if (/[=+*^_<>\\]/.test(trimmed)) {
    return false;
  }

  // If it contains minus or slash, check if it's math like "x - y" or "a/b"
  if (/[-/]/.test(trimmed) && /\d|[xyzabcn]/.test(trimmed)) {
    return false;
  }

  // Count alphabetical words (Vietnamese or Latin letters)
  const words = trimmed.split(/\s+/).filter((w) =>
    /^[a-zA-Z\u00C0-\u024F\u1EA0-\u1EF9]+[.,!?:;]?$/.test(w)
  );

  // If it has 2 or more ordinary words and no math operators, it's prose!
  return words.length >= 2;
}

/**
 * Normalizes raw mathematical shorthand, plain-text math, or LaTeX fragments into KaTeX-compatible LaTeX.
 */
export function normalizeMathExpression(raw: string): { latex: string; trailingPunct: string } {
  let s = (raw ?? "").trim();
  if (!s) return { latex: "", trailingPunct: "" };

  // Separate trailing punctuation (. , : ? !) if not part of formula
  let trailingPunct = "";
  const punctMatch = s.match(/([.,:;!?]+)$/);
  if (punctMatch && !s.endsWith("...") && !s.endsWith("}") && !s.endsWith(")") && !s.endsWith("]")) {
    if (!/\d\.\d$/.test(s)) {
      trailingPunct = punctMatch[1];
      s = s.slice(0, -trailingPunct.length).trim();
    }
  }

  // Handle common set notations: R \ {2} -> \mathbb{R} \setminus \{2\}
  s = s.replace(/\bR\s*\\\s*\{([^}]+)\}/g, (_, inner) => `\\mathbb{R} \\setminus \\{${inner.trim()}\\}`);
  s = s.replace(/\bD\s*=\s*R\s*\\\s*\{([^}]+)\}/g, (_, inner) => `D = \\mathbb{R} \\setminus \\{${inner.trim()}\\}`);
  s = s.replace(/\bD\s*=\s*R\b/g, "D = \\mathbb{R}");

  // Handle infinities and intervals: +vô cùng -> +\infty, -vô cùng -> -\infty, vô cùng -> \infty
  s = s.replace(/([+-]?)\s*vô cùng/gi, (_, sign) => (sign ? `${sign}\\infty` : "\\infty"));
  s = s.replace(/([+-]?)\s*vc\b/gi, (_, sign) => (sign ? `${sign}\\infty` : "\\infty"));

  // Logic arrows and inequalities
  s = s.replace(/<=>|<->/g, " \\Leftrightarrow ");
  s = s.replace(/=>|-->/g, " \\Rightarrow ");
  s = s.replace(/>=/g, " \\ge ");
  s = s.replace(/<=/g, " \\le ");
  s = s.replace(/!=|<>/g, " \\neq ");

  // Multiplication asterisk: 2 * 1 -> 2 \cdot 1, x * ln(x) -> x \cdot \ln(x)
  s = s.replace(/(\d|[a-zA-Z]|\))\s*\*\s*(\d|[a-zA-Z]|\()/g, "$1 \\cdot $2");

  // Logarithms and Trig
  s = s.replace(/\blog_([a-zA-Z0-9]+)\(([^)]+)\)/g, "\\log_{$1}\\left($2\\right)");
  s = s.replace(/\blog_([a-zA-Z0-9]+)/g, "\\log_{$1}");
  s = s.replace(/\bln\(([^)]+)\)/g, "\\ln\\left($1\\right)");
  s = s.replace(/\bcos\(([^)]+)\)/g, "\\cos\\left($1\\right)");
  s = s.replace(/\bsin\(([^)]+)\)/g, "\\sin\\left($1\\right)");
  s = s.replace(/\btan\(([^)]+)\)/g, "\\tan\\left($1\\right)");
  s = s.replace(/\bcot\(([^)]+)\)/g, "\\cot\\left($1\\right)");

  // Handle fractions like (x^2/2) or 1 / (x - 2) or x^2/4 or -b / (2a)
  s = s.replace(/\(([^()]+)\s*\/\s*([^()]+)\)/g, "\\left(\\frac{$1}{$2}\\right)");
  s = s.replace(/([a-zA-Z0-9\-+*]+)\s*\/\s*\(([^()]+)\)/g, "\\frac{$1}{$2}");
  s = s.replace(/\(([^()]+)\)\s*\/\s*([a-zA-Z0-9]+)/g, "\\frac{$1}{$2}");
  s = s.replace(/([a-zA-Z0-9]+(?:\^[a-zA-Z0-9()\-+*]+)?)\s*\/\s*([a-zA-Z0-9]+)/g, "\\frac{$1}{$2}");

  // Handle powers: 3^(x-1) -> 3^{x-1}, e^(x^2) -> e^{x^2}, (1/2)^(x^2 - x) -> \left(\frac{1}{2}\right)^{x^2 - x}
  s = s.replace(/\\left\(([^()]+)\\right\)\^\(([^()]+)\)/g, (_, base, exp) => `\\left(${base}\\right)^{${exp}}`);
  s = s.replace(/\\left\(([^()]+)\\right\)\^([a-zA-Z0-9]+)/g, (_, base, exp) => `\\left(${base}\\right)^{${exp}}`);
  s = s.replace(/\(([^()]+)\)\^\(([^()]+)\)/g, (_, base, exp) => `\\left(${base}\\right)^{${exp}}`);
  s = s.replace(/([a-zA-Z0-9]+)\^\(([^()]+)\)/g, (_, base, exp) => `${base}^{${exp}}`);
  s = s.replace(/([a-zA-Z0-9]+)\^([a-zA-Z0-9]+)/g, (_, base, exp) => `${base}^{${exp}}`);
  s = s.replace(/\(([^()]+)\)\^([a-zA-Z0-9]+)/g, (_, base, exp) => `\\left(${base}\\right)^{${exp}}`);

  // Clean up any duplicated \left or \right
  s = s.replace(/\\left\s*\\left/g, "\\left");
  s = s.replace(/\\right\s*\\right/g, "\\right");

  // Multi-equations like x = -1, x = -3
  s = s.replace(/,\s*([a-zA-Z]\s*=)/g, ",\\ $1");

  // Semicolon in intervals [1; +\infty) or (-1; 1)
  s = s.replace(/;\s*/g, ";\\ ");

  // Clean up any double spaces
  s = s.replace(/  +/g, " ");

  return { latex: s, trailingPunct };
}

/**
 * Checks if a standalone string or option is pure mathematical notation.
 */
export function isPureMathString(str: string): boolean {
  const s = str.trim();
  if (!s) return false;
  if (/_{2,}/.test(s)) return false;

  // Natural language prose containing Vietnamese diacritics is never pure math
  if (/[àáảãạăắằẳẵặâấầẩẫậèéẻẽẹêếềểễệìíỉĩịòóỏõọôốồổỗộơớờởỡợùúủũụưứừửữựỳýỷỹỵđĐ]/i.test(s)) {
    return false;
  }

  if (/^\\(frac|sqrt|int|sum|prod|lim|vec|alpha|beta|gamma|theta|Delta|begin|left|mathbf|text|displaystyle)\b|^\$|\\\[|\\\(|^(?:[a-zA-Z]\s*=\s*)?R\s*\\\s*\{/.test(s)) return true;
  if (/^[-+]?\d+(\.\d+)?$/.test(s)) return true;
  if (/^[[()]\s*[-+]?(?:\d+|vô cùng|\+vô cùng|-vô cùng|\\infty|\+\\infty|-\\infty)\s*[;,]\s*[-+]?(?:\d+|vô cùng|\+vô cùng|-vô cùng|\\infty|\+\\infty|-\\infty)\s*[\])]$/.test(s)) return true;
  if (/^(?:[A-Z]\s*)?\(\s*[-+]?\d+(?:\.\d+)?\s*,\s*[-+]?\d+(?:\.\d+)?\s*\)$/.test(s)) return true;

  if (/^[a-zA-Z0-9\s+\-*/^'(),;\\{}<>=!]+$/.test(s) && /[0-9=+\-*/^'<>]/.test(s)) {
    const words = s.split(/[\s,;]+/).filter((w) => /^[a-zA-Z]{2,}$/.test(w) && !/^(?:ln|log|sin|cos|tan|cot|lim|exp|min|max|dx|du|dv|dt)$/i.test(w));
    if (words.length === 0) return true;
  }
  return false;
}

const MATH_FUNCS = "(?:ln|log|sin|cos|tan|cot|lim|exp|min|max|det|dim|gcd|lcm|deg|mod|dx|du|dv|dt|dz|dy|da|db|dc|sqrt|frac)(?:_[a-zA-Z0-9]+|[({]|\\b)";
const VIETNAMESE_OR_PROSE_WORD = `(?:[A-ZÀ-Ỹa-zà-ỹ]*[àáảãạăắằẳẵặâấầẩẫậèéẻẽẹêếềểễệìíỉĩịòóỏõọôốồổỗộơớờởỡợùúủũụưứừửữựỳýỷỹỵđĐ][A-ZÀ-Ỹa-zà-ỹ]*|(?!(?:${MATH_FUNCS}))[a-zA-Z]{2,})`;
const WORD_STOPPER = `(?=[.,:;!?]?(?:\\s+${VIETNAMESE_OR_PROSE_WORD}|\\s*$|[.,:;!?]\\s|[.,:;!?]$|$))`;

const MATH_LHS_SINGLE_TOKEN = "(?:[-+]?[0-9]+(?:/[0-9]+)?|[a-zA-Z]'(?:\\(x\\))?|[a-zA-Z](?:\\([a-zA-Z0-9, ]+\\))?|[a-zA-Z0-9]+(?:\\^[0-9a-zA-Z()\\-+*/^ ]+)?|\\([0-9a-zA-Z/+\\- ]+\\)(?:\\^[0-9a-zA-Z()\\-+*/^ ]+)?|(?:log_[a-zA-Z0-9]+|ln|sin|cos|tan|cot)\\([^)]+\\)|e\\^[0-9a-zA-Z()\\-+*/^ ]+)";

export const MATH_PATTERNS = [
  /(?:D\s*=\s*)?R\s*\\\s*\{[^}]+\}/,
  /[[()]\s*[-+]?(?:\d+|vô cùng|\+vô cùng|-vô cùng|\\infty|\+\\infty|-\\infty)\s*[;,]\s*[-+]?(?:\d+|vô cùng|\+vô cùng|-vô cùng|\\infty|\+\\infty|-\\infty)\s*[\])]/,
  /(?:[A-Z]\s*)?\(\s*[-+]?\d+(?:\.\d+)?\s*,\s*[-+]?\d+(?:\.\d+)?\s*\)/,
  new RegExp(
    "(?:" +
      "(?<=\\s|^)" + MATH_LHS_SINGLE_TOKEN + "(?:\\s*[+\\-*/]\\s*" + MATH_LHS_SINGLE_TOKEN + ")*" +
      "\\s*(?:=|<|>|<=|>=|!=|<=>|=>)\\s*" +
      "[-0-9a-zA-Z+\\-*/^_(),;\\\\{}<=>| \t]+?" +
    ")" +
    WORD_STOPPER
  ),
  new RegExp(
    "(?<=\\s|^)[-+]?(?:[a-zA-Z0-9]+\\^|\\([a-zA-Z0-9/+\\- ]+\\)\\^|log_|ln\\(|sin\\(|cos\\(|tan\\()[a-zA-Z0-9+\\-*/^_(),;\\\\{} \t]+?" +
    WORD_STOPPER
  )
];

export const COMBINED_MATH_REGEX = new RegExp(
  MATH_PATTERNS.map((p) => `(?:${p.source})`).join("|"),
  "g"
);

export interface TokenPart {
  type: "text" | "math";
  value?: string;
  raw?: string;
  latex?: string;
}

/**
 * Tokenizes plain natural text and identifies math expressions embedded within it.
 */
export function tokenizePlainText(text: string): TokenPart[] {
  if (!text) return [];

  if (isPureMathString(text)) {
    const { latex } = normalizeMathExpression(text);
    return [{ type: "math", raw: text, latex }];
  }

  const parts: TokenPart[] = [];
  let lastIndex = 0;
  let match: RegExpExecArray | null;

  const regex = new RegExp(COMBINED_MATH_REGEX.source, "g");
  while ((match = regex.exec(text)) !== null) {
    const start = match.index;
    const matchStr = match[0];
    const end = start + matchStr.length;

    if (start > lastIndex) {
      parts.push({ type: "text", value: text.slice(lastIndex, start) });
    }

    const { latex, trailingPunct } = normalizeMathExpression(matchStr);
    parts.push({ type: "math", raw: matchStr, latex });
    if (trailingPunct) {
      parts.push({ type: "text", value: trailingPunct });
    }

    lastIndex = end;
  }

  if (lastIndex < text.length) {
    parts.push({ type: "text", value: text.slice(lastIndex) });
  }

  return parts;
}
