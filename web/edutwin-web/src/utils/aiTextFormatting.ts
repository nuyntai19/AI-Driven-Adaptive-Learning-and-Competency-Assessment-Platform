const CONTROL_CHAR_LETTER: Record<string, string> = { "\n": "n", "\t": "t", "\r": "r", "\f": "f", "\b": "b" };
const NEWLINE_LATEX_COMMAND = /\n(?=(?:eq|e|ot|otin|abla|u|i|ewline|mid|leq|geq|parallel|subset|supset|exists|less|gtr|sim|cong|rightarrow|leftarrow|Rightarrow|Leftarrow|Leftrightarrow)\b)/g;

/**
 * AI responses sometimes write LaTeX with a single backslash inside JSON, so
 * "\neq" is parsed as a line break followed by "eq" ("\frac" as a form feed,
 * "\times" as a tab). Inside a formula those characters can only be that.
 */
function restoreSwallowedLatexBackslashes(content: string): string {
  return content.replace(/\$([^$]+)\$/g, (_formula, body: string) => {
    const restored = body
      .replace(NEWLINE_LATEX_COMMAND, "\\n")
      .replace(/[\t\r\f\b](?=[a-zA-Z])/g, (char) => `\\${CONTROL_CHAR_LETTER[char]}`);
    return `$${restored}$`;
  });
}

const PROSE_WORD = /\s\p{L}*[À-ɏḀ-ỹ]\p{L}*/u;

/**
 * A formula the AI forgot to close swallows the following sentence and flips
 * every later "$" pair. Vietnamese words cannot be math, so end it before them.
 */
function closeRunawayFormulas(content: string): string {
  if (content.includes("$$") || content.includes("\\$")) return content;
  let output = "";
  let position = 0;
  for (;;) {
    const open = content.indexOf("$", position);
    const close = open < 0 ? -1 : content.indexOf("$", open + 1);
    if (close < 0) return output + content.slice(position);
    const body = content.slice(open + 1, close);
    const searchable = body.replace(/\\(?:text|mathrm|textbf)\{[^}]*\}/g, (command) => " ".repeat(command.length));
    const proseStart = searchable.search(PROSE_WORD);
    if (proseStart > 0) {
      output += `${content.slice(position, open)}$${body.slice(0, proseStart).trimEnd()}$${body.slice(proseStart)}`;
      position = close; // That "$" really opens the next formula.
    } else {
      output += content.slice(position, close + 1);
      position = close + 1;
    }
  }
}

/** Repair double-escaped line breaks and broken formulas in AI prose without unescaping LaTeX. */
export function normalizeAITextLineBreaks(content: string | null | undefined): string {
  if (!content) return "";
  const formulas = /(\$\$[\s\S]*?\$\$|\$[^$\n]+?\$|\\\[[\s\S]*?\\\]|\\\([\s\S]*?\\\))/g;
  return closeRunawayFormulas(restoreSwallowedLatexBackslashes(content)).split(formulas).map((part, index) => {
    if (index % 2 === 1) return part;
    // Only unambiguous prose boundaries: leave commands such as \\neq,
    // \\nabla, \\nu and \\nRightarrow intact, including undelimited legacy math.
    return part.replace(/\\r\\n|\\[nr](?![a-z]|Rightarrow\b|Leftarrow\b|Leftrightarrow\b)/g, "\n");
  }).join("");
}
