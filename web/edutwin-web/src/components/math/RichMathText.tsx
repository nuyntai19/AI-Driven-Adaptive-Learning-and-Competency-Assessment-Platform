import React, { useMemo } from "react";
import katex from "katex";

export interface RichMathTextProps {
  text?: string | null;
  content?: string | null;
  className?: string;
  displayMode?: boolean;
}

/**
 * Intelligent renderer for mixed plain text and LaTeX formulas.
 * Preserves natural word spacing and typography for Vietnamese/English text,
 * while seamlessly rendering math expressions ($...$, $$...$$, \(...\), \[...\], or LaTeX commands).
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
      const inlineCmdRegex = /(\\(?:frac|sqrt|vec|mathbf|pm|times|div|leq|geq|neq|approx|infty|int|sum|alpha|beta|gamma|theta|pi|partial|cdot|int|sum|prod)\b(?:\{[^}]*\}|\s+[^$\s]+)?)/g;
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

  return <span className={`rich-math-text inline leading-relaxed ${className}`}>{renderedNodes}</span>;
};
