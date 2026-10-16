export type MathDelimiter = "$" | "$$" | "\\(" | "\\[";
export type DelimitedMathToken =
  | { type: "text"; raw: string }
  | { type: "math"; raw: string; latex: string; delimiter: MathDelimiter; displayMode: boolean };

/**
 * Split rich math without treating a literal \\$ as a delimiter. Raw tokens always
 * concatenate to the original source; malformed/unclosed formulas remain text.
 * Dollar inline formulas cannot cross a line break, matching the editor profile.
 */
export function tokenizeDelimitedMath(
  source: string,
  delimiters: readonly MathDelimiter[] = ["$", "$$", "\\(", "\\["],
): DelimitedMathToken[] {
  const tokens: DelimitedMathToken[] = [];
  let textStart = 0;
  let precedingSlashes = 0;
  let active: { delimiter: MathDelimiter; close: string; start: number; contentStart: number } | null = null;
  for (let index = 0; index < source.length;) {
    // Track parity as we scan, rather than rescanning long backslash runs.
    const escaped = precedingSlashes % 2 === 1;
    if (active) {
      if (active.delimiter === "$" && /[\r\n]/.test(source[index])) {
        active = null;
      } else if (source.startsWith(active.close, index) && !escaped) {
        const end = index + active.close.length;
        if (index > active.contentStart || active.delimiter !== "$") {
          if (active.start > textStart) tokens.push({ type: "text", raw: source.slice(textStart, active.start) });
          tokens.push({ type: "math", raw: source.slice(active.start, end), latex: source.slice(active.contentStart, index),
            delimiter: active.delimiter, displayMode: active.delimiter === "$$" || active.delimiter === "\\[" });
          textStart = end;
        }
        active = null;
        index = end;
        precedingSlashes = 0;
        continue;
      }
    } else if (!escaped) {
      const delimiter = (["$$", "$", "\\(", "\\["] as const).find((candidate) =>
        delimiters.includes(candidate) && source.startsWith(candidate, index));
      if (delimiter) {
        active = { delimiter, close: delimiter === "\\(" ? "\\)" : delimiter === "\\[" ? "\\]" : delimiter,
          start: index, contentStart: index + delimiter.length };
        index += delimiter.length;
        precedingSlashes = 0;
        continue;
      }
    }
    precedingSlashes = source[index] === "\\" ? precedingSlashes + 1 : 0;
    index++;
  }
  if (textStart < source.length) tokens.push({ type: "text", raw: source.slice(textStart) });
  return tokens;
}

/** Display literal dollars as text while retaining escapes in stored/editor source. */
export function displayMathText(source: string): string {
  return source.replace(/(\\+)\$/g, (raw, slashes: string) =>
    slashes.length % 2 ? `${slashes.slice(0, -1)}$` : raw);
}
