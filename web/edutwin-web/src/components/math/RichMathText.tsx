import React, { useMemo } from "react";
import katex from "katex";

export interface RichMathTextProps {
  text?: string | null;
  content?: string | null;
  className?: string;
  displayMode?: boolean;
}

/**
 * Checks whether a token enclosed in $...$ is actually natural prose
 * (e.g. English or Vietnamese sentences mistakenly wrapped in dollars by LLMs)
 * rather than an actual mathematical expression.
 */
function isLikelyProse(token: string): boolean {
  const trimmed = token.trim();
  if (!trimmed) return true;

  // If it has standard LaTeX commands like \frac, \sqrt, \alpha, etc., it's definitely math
  if (/\\(frac|sqrt|sum|int|prod|pm|times|div|vec|mathbf|alpha|beta|gamma|theta|pi|le|ge|ne|neq|approx|left|right|cdot)\b/.test(trimmed)) {
    return false;
  }

  // If it has math operator symbols: =, +, *, ^, _, <, >, \
  if (/[=+\*^_<>\\]/.test(trimmed)) {
    return false;
  }

  // If it contains minus or slash, check if it's math like "x - y" or "a/b"
  if (/[\-\/]/.test(trimmed) && /\d|[xyzabcn]/.test(trimmed)) {
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
 * Intelligent renderer for mixed plain text and LaTeX formulas.
 * Preserves natural word spacing and typography for Vietnamese/English text,
 * while seamlessly rendering math expressions ($...$, $$...$$, \(...\), \[...\], or LaTeX commands).
 * Also gracefully handles newlines and falls back to plain text for prose erroneously wrapped in $...$.
 */
export const RichMathText: React.FC<RichMathTextProps> = ({
  text,
  content,
  className = "",
  displayMode = false,
}) => {
  const rawContent = (text ?? content ?? "").trim();

  const renderedNodes = useMemo(() => {
    if (!rawContent) return null;

    // 1. Check if the entire string is a pure LaTeX formula (e.g. starts with backslash commands)
    const isPureLatex =
      /^\\(frac|sqrt|int|sum|prod|lim|vec|alpha|beta|gamma|theta|Delta|begin|left|mathbf|text|displaystyle)\b/.test(rawContent) ||
      /^\\\[[\s\S]*\\\]$/.test(rawContent) ||
      /^\$\$[\s\S]*\$\$$/.test(rawContent);

    if (isPureLatex) {
      const formulaOnly = rawContent.replace(/^(\$\$|\\\[)|(\$\$|\\\])$/g, "");
      try {
        const html = katex.renderToString(formulaOnly, {
          throwOnError: false,
          displayMode: displayMode || isPureLatex,
          trust: false,
          output: "htmlAndMathml",
        });
        return (
          <span
            className={displayMode ? "katex-block my-1 block text-center" : "katex-inline inline-block align-middle mx-0.5"}
            dangerouslySetInnerHTML={{ __html: html }}
          />
        );
      } catch {
        return <span>{rawContent}</span>;
      }
    }

    // 2. Tokenize text with explicit LaTeX delimiters: $$...$$, $...$, \[...\], \(...\)
    const delimiterRegex = /(\$\$[\s\S]*?\$\$|\$[^$\n]+?\$|\\\[[\s\S]*?\\\]|\\\([\s\S]*?\\\))/g;
    const parts = rawContent.split(delimiterRegex);

    if (parts.length === 1 && !delimiterRegex.test(rawContent)) {
      // 3. Text without dollar delimiters: check for inline LaTeX commands like \frac{a}{b}, \sqrt{x}, \pm, \times
      const inlineCmdRegex = /(\\(?:frac|sqrt|vec|mathbf|pm|times|div|le|ge|ne|leq|geq|neq|approx|infty|int|sum|alpha|beta|gamma|theta|pi|partial|cdot|int|sum|prod)\b(?:\{[^}]*\}|\s+[^$\s]+)?)/g;
      if (inlineCmdRegex.test(rawContent)) {
        const subParts = rawContent.split(inlineCmdRegex);
        return (
          <>
            {subParts.map((sub, idx) => {
              if (idx % 2 === 1) {
                try {
                  const html = katex.renderToString(sub, {
                    throwOnError: false,
                    displayMode: false,
                    trust: false,
                    output: "htmlAndMathml",
                  });
                  return (
                    <span
                      key={idx}
                      className="katex-inline inline-block align-middle mx-0.5"
                      dangerouslySetInnerHTML={{ __html: html }}
                    />
                  );
                } catch {
                  return <span key={idx}>{sub}</span>;
                }
              }
              return <React.Fragment key={idx}>{sub}</React.Fragment>;
            })}
          </>
        );
      }

      // Plain natural language text: preserve spacing & typography completely
      return <span>{rawContent}</span>;
    }

    // 4. Render delimited LaTeX tokens alongside plaintext tokens
    return (
      <>
        {parts.map((part, index) => {
          if (!part) return null;

          // Block Math: $$...$$ or \[...\]
          if (
            (part.startsWith("$$") && part.endsWith("$$")) ||
            (part.startsWith("\\[") && part.endsWith("\\]"))
          ) {
            const math = part.slice(2, -2).trim();
            try {
              const html = katex.renderToString(math, {
                throwOnError: false,
                displayMode: true,
                trust: false,
                output: "htmlAndMathml",
              });
              return (
                <span
                  key={index}
                  className="katex-block my-1.5 block text-center"
                  dangerouslySetInnerHTML={{ __html: html }}
                />
              );
            } catch {
              return <span key={index}>{part}</span>;
            }
          }

          // Inline Math: $...$ or \(...\)
          if (
            (part.startsWith("$") && part.endsWith("$")) ||
            (part.startsWith("\\(") && part.endsWith("\\)"))
          ) {
            const math = part.startsWith("$") ? part.slice(1, -1).trim() : part.slice(2, -2).trim();

            // Check prose heuristic: if it's natural language wrapped in dollars, do NOT KaTeX it!
            if (isLikelyProse(math)) {
              return <React.Fragment key={index}>{math}</React.Fragment>;
            }

            try {
              const html = katex.renderToString(math, {
                throwOnError: false,
                displayMode: false,
                trust: false,
                output: "htmlAndMathml",
              });
              return (
                <span
                  key={index}
                  className="katex-inline inline-block align-middle mx-0.5"
                  dangerouslySetInnerHTML={{ __html: html }}
                />
              );
            } catch {
              return <span key={index}>{part}</span>;
            }
          }

          // Plain text token with natural spaces
          return <React.Fragment key={index}>{part}</React.Fragment>;
        })}
      </>
    );
  }, [rawContent, displayMode]);

  if (!rawContent) return null;

  return (
    <span className={`rich-math-text inline leading-relaxed whitespace-pre-wrap break-words ${className}`}>
      {renderedNodes}
    </span>
  );
};
