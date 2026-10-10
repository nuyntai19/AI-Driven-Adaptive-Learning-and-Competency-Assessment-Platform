import katex from "katex";

/** KaTeX's formula API expects the body, unlike rich text which includes delimiters. */
export function unwrapMathDelimiters(formula: string): string {
  const trimmed = formula.trim();
  const match = /^(?:\$\$([\s\S]*?)\$\$|\$([^$\n]+)\$|\\\[([\s\S]*?)\\\]|\\\(([\s\S]*?)\\\))$/.exec(trimmed);
  return match ? (match.slice(1).find(part => part !== undefined) ?? "").trim() : trimmed;
}

/**
 * Safely renders a LaTeX formula into HTML using KaTeX with strict invariants:
 * - trust: false (blocks malicious execution / arbitrary commands)
 * - throwOnError: false (graceful degradation)
 * - output: "htmlAndMathml"
 */
export function renderSafeKatex(formula: string, displayMode: boolean = false): string | null {
  const trimmed = unwrapMathDelimiters(formula);
  if (!trimmed) return null;
  try {
    return katex.renderToString(trimmed, {
      throwOnError: false,
      displayMode,
      trust: false,
      output: "htmlAndMathml",
    });
  } catch {
    return null;
  }
}
