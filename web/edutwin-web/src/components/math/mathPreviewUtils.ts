import katex from "katex";

/**
 * Safely renders a LaTeX formula into HTML using KaTeX with strict invariants:
 * - trust: false (blocks malicious execution / arbitrary commands)
 * - throwOnError: false (graceful degradation)
 * - output: "htmlAndMathml"
 */
export function renderSafeKatex(formula: string, displayMode: boolean = false): string | null {
  const trimmed = formula.trim();
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
