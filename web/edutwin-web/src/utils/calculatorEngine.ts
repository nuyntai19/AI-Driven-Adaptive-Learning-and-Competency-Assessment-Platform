export type AngleMode = "deg" | "rad";

export interface CalculatorState {
  expression: string;
  result: string | null;
  angleMode: AngleMode;
  error: string | null;
  history: Array<{ expression: string; result: string }>;
}

export class CalculatorEngine {
  private angleMode: AngleMode = "deg";

  constructor(initialAngleMode: AngleMode = "deg") {
    this.angleMode = initialAngleMode;
  }

  public getAngleMode(): AngleMode {
    return this.angleMode;
  }

  public setAngleMode(mode: AngleMode): void {
    this.angleMode = mode;
  }

  /**
   * Evaluates a mathematical string expression deterministically.
   * Supports: +, -, *, /, %, ^, sqrt, sin, cos, tan, log (base 10), ln, pi, e, parentheses.
   * Contains zero symbolic algebra solvers or step-by-step problem-solving engines.
   */
  public evaluate(rawExpr: string): number {
    if (!rawExpr || rawExpr.trim() === "") {
      throw new Error("Empty expression");
    }

    const tokens = this.tokenize(rawExpr);
    if (tokens.length === 0) {
      throw new Error("Empty expression");
    }

    let pos = 0;

    const peek = (): string | null => (pos < tokens.length ? tokens[pos] : null);
    const consume = (expected?: string): string => {
      const current = tokens[pos];
      if (expected !== undefined && current !== expected) {
        throw new Error(`Expected '${expected}' but found '${current ?? "EOF"}'`);
      }
      pos++;
      return current;
    };

    // Parser grammar:
    // expr = term (( '+' | '-' ) term)*
    // term = unary (( '*' | '/' | '%' ) unary)*
    // unary = ( '+' | '-' ) unary | power
    // power = primary ( '^' unary )?   (right-associative, looser than primary, tighter than unary)
    // primary = NUMBER | CONSTANT | FUNCTION '(' expr ')' | '(' expr ')'

    const parseExpr = (): number => {
      let val = parseTerm();
      while (peek() === "+" || peek() === "-") {
        const op = consume();
        const next = parseTerm();
        if (op === "+") val += next;
        else val -= next;
      }
      return val;
    };

    const parseTerm = (): number => {
      let val = parseUnary();
      while (peek() === "*" || peek() === "/" || peek() === "%") {
        const op = consume();
        const next = parseUnary();
        if (op === "*") {
          val *= next;
        } else if (op === "/") {
          if (next === 0) {
            throw new Error("Division by zero");
          }
          val /= next;
        } else {
          if (next === 0) {
            throw new Error("Division by zero");
          }
          val %= next;
        }
      }
      return val;
    };

    const parseUnary = (): number => {
      if (peek() === "-") {
        consume();
        return -parseUnary();
      }
      if (peek() === "+") {
        consume();
        return parseUnary();
      }
      return parsePower();
    };

    const parsePower = (): number => {
      const base = parsePrimary();
      if (peek() === "^") {
        consume();
        const exponent = parseUnary();
        return Math.pow(base, exponent);
      }
      return base;
    };

    const parsePrimary = (): number => {
      const token = peek();
      if (!token) {
        throw new Error("Unexpected end of expression");
      }

      // Constant
      if (token.toLowerCase() === "pi") {
        consume();
        return Math.PI;
      }
      if (token.toLowerCase() === "e") {
        consume();
        return Math.E;
      }

      // Parentheses
      if (token === "(") {
        consume("(");
        const val = parseExpr();
        consume(")");
        return val;
      }

      // Functions: sin, cos, tan, sqrt, log, ln
      const lower = token.toLowerCase();
      if (["sin", "cos", "tan", "sqrt", "cbrt", "log", "ln", "abs"].includes(lower)) {
        consume();
        consume("(");
        const arg = parseExpr();
        consume(")");

        switch (lower) {
          case "sin": {
            const rad = this.angleMode === "deg" ? (arg * Math.PI) / 180 : arg;
            const res = Math.sin(rad);
            return Math.abs(res) < 1e-15 ? 0 : res;
          }
          case "cos": {
            const rad = this.angleMode === "deg" ? (arg * Math.PI) / 180 : arg;
            const res = Math.cos(rad);
            return Math.abs(res) < 1e-15 ? 0 : res;
          }
          case "tan": {
            const rad = this.angleMode === "deg" ? (arg * Math.PI) / 180 : arg;
            // Check for vertical asymptotes: 90, 270 deg
            if (this.angleMode === "deg" && Math.abs((Math.abs(arg) % 180) - 90) < 1e-9) {
              throw new Error("Undefined (tangent vertical asymptote)");
            }
            const res = Math.tan(rad);
            return Math.abs(res) < 1e-15 ? 0 : res;
          }
          case "sqrt": {
            if (arg < 0) throw new Error("Square root of negative number");
            return Math.sqrt(arg);
          }
          case "cbrt":
            return Math.cbrt(arg);
          case "log": {
            if (arg <= 0) throw new Error("Logarithm of non-positive number");
            return Math.log10(arg);
          }
          case "ln": {
            if (arg <= 0) throw new Error("Natural logarithm of non-positive number");
            return Math.log(arg);
          }
          case "abs":
            return Math.abs(arg);
          default:
            throw new Error(`Unknown function '${lower}'`);
        }
      }

      // Number
      const num = Number(token);
      if (!isNaN(num)) {
        consume();
        return num;
      }

      throw new Error(`Invalid token '${token}'`);
    };

    const result = parseExpr();
    if (pos < tokens.length) {
      throw new Error(`Unexpected token '${tokens[pos]}'`);
    }

    if (!isFinite(result) || isNaN(result)) {
      throw new Error("Calculation resulted in non-finite value");
    }

    // Round subtle float inaccuracies, e.g. 0.1 + 0.2 = 0.30000000000000004
    return Math.round(result * 1e12) / 1e12;
  }

  private tokenize(expr: string): string[] {
    const tokens: string[] = [];
    let i = 0;
    const clean = expr.trim();

    while (i < clean.length) {
      const char = clean[i];

      if (/\s/.test(char)) {
        i++;
        continue;
      }

      // Numbers (integers or decimals)
      if (/\d/.test(char) || (char === "." && i + 1 < clean.length && /\d/.test(clean[i + 1]))) {
        let numStr = "";
        while (i < clean.length && (/[\d.]/.test(clean[i]))) {
          numStr += clean[i];
          i++;
        }
        tokens.push(numStr);
        continue;
      }

      // Words (function names, constants: sin, cos, tan, sqrt, log, ln, pi, e)
      if (/[a-zA-Z]/.test(char)) {
        let word = "";
        while (i < clean.length && /[a-zA-Z0-9]/.test(clean[i])) {
          word += clean[i];
          i++;
        }
        tokens.push(word);
        continue;
      }

      // Multi-char operators or symbols (e.g. ^, +, -, *, /, %, (, ))
      if (["+", "-", "*", "/", "%", "^", "(", ")"].includes(char)) {
        tokens.push(char);
        i++;
        continue;
      }

      // Special symbols (e.g. ×, ÷, π)
      if (char === "×") {
        tokens.push("*");
        i++;
        continue;
      }
      if (char === "÷") {
        tokens.push("/");
        i++;
        continue;
      }
      if (char === "π") {
        tokens.push("pi");
        i++;
        continue;
      }

      throw new Error(`Unsupported character: '${char}'`);
    }

    return tokens;
  }
}
