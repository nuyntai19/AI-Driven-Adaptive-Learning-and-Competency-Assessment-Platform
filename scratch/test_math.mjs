import katex from 'katex';

export function normalizeMathExpression(raw) {
  let s = (raw ?? '').trim();
  if (!s) return { latex: '', trailingPunct: '' };

  let trailingPunct = '';
  const punctMatch = s.match(/([.,:;!?]+)$/);
  if (punctMatch && !s.endsWith('...') && !s.endsWith('}') && !s.endsWith(')') && !s.endsWith(']')) {
    if (!/\d\.\d$/.test(s)) {
      trailingPunct = punctMatch[1];
      s = s.slice(0, -trailingPunct.length).trim();
    }
  }

  // Handle common set notations: R \ {2} -> \mathbb{R} \setminus \{2\}
  s = s.replace(/\bR\s*\\\s*\{([^}]+)\}/g, (_, inner) => `\\mathbb{R} \\setminus \\{${inner.trim()}\\}`);
  s = s.replace(/\bD\s*=\s*R\s*\\\s*\{([^}]+)\}/g, (_, inner) => `D = \\mathbb{R} \\setminus \\{${inner.trim()}\\}`);
  s = s.replace(/\bD\s*=\s*R\b/g, 'D = \\mathbb{R}');

  // Handle infinities and intervals: +vô cùng -> +\infty, -vô cùng -> -\infty, vô cùng -> \infty
  s = s.replace(/([+-]?)\s*vô cùng/gi, (_, sign) => (sign ? `${sign}\\infty` : '\\infty'));
  s = s.replace(/([+-]?)\s*vc\b/gi, (_, sign) => (sign ? `${sign}\\infty` : '\\infty'));

  // Logic arrows and inequalities
  s = s.replace(/<=>|<->/g, ' \\Leftrightarrow ');
  s = s.replace(/=>|-->/g, ' \\Rightarrow ');
  s = s.replace(/>=/g, ' \\ge ');
  s = s.replace(/<=/g, ' \\le ');
  s = s.replace(/!=|<>/g, ' \\neq ');

  // Multiplication asterisk: 2 * 1 -> 2 \cdot 1, x * ln(x) -> x \cdot \ln(x)
  s = s.replace(/(\d|[a-zA-Z]|\))\s*\*\s*(\d|[a-zA-Z]|\()/g, '$1 \\cdot $2');

  // Logarithms and Trig
  s = s.replace(/\blog_([a-zA-Z0-9]+)\(([^)]+)\)/g, '\\log_{$1}\\left($2\\right)');
  s = s.replace(/\blog_([a-zA-Z0-9]+)/g, '\\log_{$1}');
  s = s.replace(/\bln\(([^)]+)\)/g, '\\ln\\left($1\\right)');
  s = s.replace(/\bcos\(([^)]+)\)/g, '\\cos\\left($1\\right)');
  s = s.replace(/\bsin\(([^)]+)\)/g, '\\sin\\left($1\\right)');
  s = s.replace(/\btan\(([^)]+)\)/g, '\\tan\\left($1\\right)');
  s = s.replace(/\bcot\(([^)]+)\)/g, '\\cot\\left($1\\right)');

  // Handle fractions like (x^2/2) or 1 / (x - 2) or x^2/4 or -b / (2a)
  s = s.replace(/\(([^()]+)\s*\/\s*([^()]+)\)/g, '\\left(\\frac{$1}{$2}\\right)');
  s = s.replace(/([a-zA-Z0-9\-+*]+)\s*\/\s*\(([^()]+)\)/g, '\\frac{$1}{$2}');
  s = s.replace(/\(([^()]+)\)\s*\/\s*([a-zA-Z0-9]+)/g, '\\frac{$1}{$2}');
  s = s.replace(/([a-zA-Z0-9]+(?:\^[a-zA-Z0-9()\-+*]+)?)\s*\/\s*([a-zA-Z0-9]+)/g, '\\frac{$1}{$2}');

  // Handle powers: 3^(x-1) -> 3^{x-1}, e^(x^2) -> e^{x^2}, (1/2)^(x^2 - x) -> \left(\frac{1}{2}\right)^{x^2 - x}
  s = s.replace(/\(([^()]+)\)\^\(([^()]+)\)/g, (_, base, exp) => `\\left(${base}\\right)^{${exp}}`);
  s = s.replace(/([a-zA-Z0-9]+)\^\(([^()]+)\)/g, (_, base, exp) => `${base}^{${exp}}`);
  s = s.replace(/([a-zA-Z0-9]+)\^([a-zA-Z0-9]+)/g, (_, base, exp) => `${base}^{${exp}}`);
  s = s.replace(/\(([^()]+)\)\^([a-zA-Z0-9]+)/g, (_, base, exp) => `\\left(${base}\\right)^{${exp}}`);

  // Multi-equations like x = -1, x = -3
  s = s.replace(/,\s*([a-zA-Z]\s*=)/g, ',\\ $1');

  // Semicolon in intervals [1; +\infty) or (-1; 1)
  s = s.replace(/;\s*/g, ';\\ ');

  // Clean up any double spaces
  s = s.replace(/  +/g, ' ');

  return { latex: s, trailingPunct };
}

export function isPureMathString(str) {
  const s = str.trim();
  if (!s) return false;
  if (/_{2,}/.test(s)) return false;

  if (/^\\(frac|sqrt|int|sum|prod|lim|vec|alpha|beta|gamma|theta|Delta|begin|left|mathbf|text|displaystyle)\b|^\$|\\\[|\\\(|\bR\s*\\\s*\{/.test(s)) return true;
  if (/^[-+]?\d+(\.\d+)?$/.test(s)) return true;
  if (/^[\[\(]\s*[-+]?(?:\d+|vô cùng|\+vô cùng|-vô cùng|\\infty|\+\\infty|-\\infty)\s*[;,]\s*[-+]?(?:\d+|vô cùng|\+vô cùng|-vô cùng|\\infty|\+\\infty|-\\infty)\s*[\]\)]$/.test(s)) return true;
  if (/^(?:[A-Z]\s*)?\(\s*[-+]?\d+(?:\.\d+)?\s*,\s*[-+]?\d+(?:\.\d+)?\s*\)$/.test(s)) return true;

  if (/^[a-zA-Z0-9\s+\-*/^'(),;\\{}<>=!]+$/.test(s) && /[0-9=+\-*/^'<>]/.test(s)) {
    const words = s.split(/[\s,;]+/).filter((w) => /^[a-zA-Z]{2,}$/.test(w) && !/^(?:ln|log|sin|cos|tan|cot|lim|exp|min|max|dx|du|dv|dt)$/i.test(w));
    if (words.length === 0) return true;
  }
  return false;
}

const MATH_FUNCS = '(?:ln|log|sin|cos|tan|cot|lim|exp|min|max|det|dim|gcd|lcm|deg|mod|dx|du|dv|dt|dz|dy|da|db|dc|sqrt|frac)(?:_[a-zA-Z0-9]+|[({]|\\b)';
const VIETNAMESE_OR_PROSE_WORD = `(?:[A-ZÀ-Ỹa-zà-ỹ]*[àáảãạăắằẳẵặâấầẩẫậèéẻẽẹêếềểễệìíỉĩịòóỏõọôốồổỗộơớờởỡợùúủũụưứừửữựỳýỷỹỵđĐ][A-ZÀ-Ỹa-zà-ỹ]*|(?!(?:${MATH_FUNCS}))[a-zA-Z]{2,})`;
const WORD_STOPPER = `(?=[.,:;!?]?(?:\\s+${VIETNAMESE_OR_PROSE_WORD}|\\s*$|[.,:;!?]\\s|[.,:;!?]$|$))`;

const MATH_LHS_SINGLE_TOKEN = '(?:[-+]?[0-9]+(?:/[0-9]+)?|[a-zA-Z]\'(?:\\(x\\))?|[a-zA-Z](?:\\([a-zA-Z0-9, ]+\\))?|[a-zA-Z0-9]+(?:\\^[0-9a-zA-Z()\\-+*/^ ]+)?|\\([0-9a-zA-Z/+\\- ]+\\)(?:\\^[0-9a-zA-Z()\\-+*/^ ]+)?|(?:log_[a-zA-Z0-9]+|ln|sin|cos|tan|cot)\\([^)]+\\)|e\\^[0-9a-zA-Z()\\-+*/^ ]+)';

const MATH_PATTERNS = [
  /(?:D\s*=\s*)?R\s*\\\s*\{[^}]+\}/,
  /[\[\(]\s*[-+]?(?:\d+|vô cùng|\+vô cùng|-vô cùng|\\infty|\+\\infty|-\\infty)\s*[;,]\s*[-+]?(?:\d+|vô cùng|\+vô cùng|-vô cùng|\\infty|\+\\infty|-\\infty)\s*[\]\)]/,
  /(?:[A-Z]\s*)?\(\s*[-+]?\d+(?:\.\d+)?\s*,\s*[-+]?\d+(?:\.\d+)?\s*\)/,
  new RegExp(
    '(?:' +
      '(?<=\\s|^)' + MATH_LHS_SINGLE_TOKEN + '(?:\\s*[+\\-*/]\\s*' + MATH_LHS_SINGLE_TOKEN + ')*' +
      '\\s*(?:=|<|>|<=|>=|!=|<=>|=>)\\s*' +
      '[-0-9a-zA-Z+\\-*/^_(),;\\\\{}<=>| \t]+?' +
    ')' +
    WORD_STOPPER
  ),
  new RegExp(
    '(?<=\\s|^)[-+]?(?:[a-zA-Z0-9]+\\^|\\([a-zA-Z0-9/+\\- ]+\\)\\^|log_|ln\\(|sin\\(|cos\\(|tan\\()[a-zA-Z0-9+\\-*/^_(),;\\\\{} \t]+?' +
    WORD_STOPPER
  )
];

const COMBINED_MATH_REGEX = new RegExp(
  MATH_PATTERNS.map((p) => `(?:${p.source})`).join('|'),
  'g'
);

export function tokenizePlainText(text) {
  if (!text) return [];
  if (isPureMathString(text)) {
    const { latex } = normalizeMathExpression(text);
    return [{ type: 'math', raw: text, latex }];
  }

  const parts = [];
  let lastIndex = 0;
  let match;

  const regex = new RegExp(COMBINED_MATH_REGEX.source, 'g');
  while ((match = regex.exec(text)) !== null) {
    const start = match.index;
    const matchStr = match[0];
    const end = start + matchStr.length;

    if (start > lastIndex) {
      parts.push({ type: 'text', value: text.slice(lastIndex, start) });
    }

    const { latex, trailingPunct } = normalizeMathExpression(matchStr);
    parts.push({ type: 'math', raw: matchStr, latex });
    if (trailingPunct) {
      parts.push({ type: 'text', value: trailingPunct });
    }

    lastIndex = end;
  }

  if (lastIndex < text.length) {
    parts.push({ type: 'text', value: text.slice(lastIndex) });
  }

  return parts;
}

const testCases = [
  { input: 'Cho hàm số bậc nhất y = 2x + 1. Tính giá trị của y khi x = 3.', expectedMathCount: 2 },
  { input: 'Tìm tập xác định của hàm số y = 1 / (x - 2).', expectedMathCount: 1 },
  { input: 'R \\ {2}', expectedMathCount: 1 },
  { input: 'Đồ thị hàm số y = x^2 - 4x + 3 cắt trục hoành tại các điểm có hoành độ là bao nhiêu?', expectedMathCount: 1 },
  { input: 'x = -1, x = -3', expectedMathCount: 1 },
  { input: 'Tìm tọa độ đỉnh của parabol y = -2x^2 + 4x - 1.', expectedMathCount: 1 },
  { input: '(1, 1)', expectedMathCount: 1 },
  { input: 'Chứng minh hàm số y = x^3 - 3x nghịch biến trên khoảng (-1; 1).', expectedMathCount: 2 },
  { input: 'Tập xác định của hàm số y = log_2(x - 1) là:', expectedMathCount: 1 },
  { input: '[1; +vô cùng)', expectedMathCount: 1 },
  { input: 'Tính giá trị biểu thức A = log_3(9) + log_2(8).', expectedMathCount: 1 },
  { input: 'Nghiệm của phương trình 3^(x-1) = 27 là:', expectedMathCount: 1 },
  { input: 'Họ tất cả các nguyên hàm của hàm số f(x) = 2x là:', expectedMathCount: 1 },
  { input: 'x^2 + C', expectedMathCount: 1 },
  { input: 'Dùng phương pháp đổi biến, tìm nguyên hàm của f(x) = 2x * e^(x^2).', expectedMathCount: 1 },
  { input: 'Dùng phương pháp nguyên hàm từng phần, tính nguyên hàm của f(x) = x * ln(x).', expectedMathCount: 1 },
  { input: '(x^2/2)ln(x) - x^2/4 + C', expectedMathCount: 1 },
  { input: 'I _____ to school every day.', expectedMathCount: 0 }
];

let allPassed = true;
for (const tc of testCases) {
  const tokens = tokenizePlainText(tc.input);
  const mathTokens = tokens.filter(t => t.type === 'math');
  for (const m of mathTokens) {
    try {
      katex.renderToString(m.latex, { throwOnError: true, output: 'htmlAndMathml' });
    } catch (e) {
      console.error('KaTeX Error on ' + m.latex + ': ' + e.message);
      allPassed = false;
    }
  }
  if (mathTokens.length !== tc.expectedMathCount) {
    console.warn('Count mismatch on: ' + tc.input + ' (expected ' + tc.expectedMathCount + ', got ' + mathTokens.length + ')');
    allPassed = false;
  } else {
    console.log('[PASS] ' + tc.input + ' => ' + (mathTokens.map(m => `"${m.latex}"`).join(' | ') || '(none)'));
  }
}
if (allPassed) {
  console.log('\n>>> ALL 18 TEST CASES PASSED WITH 100% SUCCESS! <<<');
}
