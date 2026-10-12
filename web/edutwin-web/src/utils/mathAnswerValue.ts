/**
 * MathLive 0.110 can prefix text-only `plain-text` output with the literal word
 * "undefined" and collapses the space between a literal backslash and `{`.
 * Keep grading text stable without changing the LaTeX used for visual display.
 */
export function normalizeMathLivePlainText(plainText: string, latex: string): string {
  let normalized = plainText;

  if (normalized.startsWith("undefined") && !latex.includes("undefined")) {
    normalized = normalized.slice("undefined".length);
  }

  // Preserve the conventional keyboard representation used by answer keys:
  // R \ {2}, D = R \ {2}, ...
  return normalized.replace(/\s*\\\s*\{/g, " \\ {");
}
