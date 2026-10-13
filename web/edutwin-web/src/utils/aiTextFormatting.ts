/** Repair double-escaped line breaks in AI prose without unescaping LaTeX. */
export function normalizeAITextLineBreaks(content: string | null | undefined): string {
  if (!content) return "";
  const formulas = /(\$\$[\s\S]*?\$\$|\$[^$\n]+?\$|\\\[[\s\S]*?\\\]|\\\([\s\S]*?\\\))/g;
  return content.split(formulas).map((part, index) => {
    if (index % 2 === 1) return part;
    // Only unambiguous prose boundaries: leave commands such as \\neq,
    // \\nabla, \\nu and \\nRightarrow intact, including undelimited legacy math.
    return part.replace(/\\r\\n|\\[nr](?![a-z]|Rightarrow\b|Leftarrow\b|Leftrightarrow\b)/g, "\n");
  }).join("");
}
