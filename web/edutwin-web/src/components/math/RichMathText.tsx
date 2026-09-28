import React, { useMemo } from "react";
import katex from "katex";

export interface RichMathTextProps {
  text?: string | null;
  content?: string | null;
  className?: string;
  displayMode?: boolean;
}

import {
  isLikelyProse,
  normalizeMathExpression,
  isPureMathString,
  MATH_PATTERNS,
  COMBINED_MATH_REGEX,
  tokenizePlainText,
} from "../../utils/mathExpression";
import type { TokenPart } from "../../utils/mathExpression";

export {
  isLikelyProse,
  normalizeMathExpression,
  isPureMathString,
  MATH_PATTERNS,
  COMBINED_MATH_REGEX,
  tokenizePlainText,
};
export type { TokenPart };

/**
 * Intelligent renderer for mixed natural text (Vietnamese/English) and LaTeX formulas.
 * Preserves natural word spacing and typography for Vietnamese/English text,
 * while seamlessly rendering math expressions ($...$, $$...$$, \(...\), \[...\], or LaTeX commands),
 * exponents, intervals, and set notations via KaTeX.
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
      // 3. No explicit delimiters: run intelligent natural text math tokenizer
      const tokens = tokenizePlainText(rawContent);
      if (tokens.length > 0 && tokens.some((t) => t.type === "math")) {
        return (
          <>
            {tokens.map((token, idx) => {
              if (token.type === "math" && token.latex) {
                try {
                  const html = katex.renderToString(token.latex, {
                    throwOnError: false,
                    displayMode: false,
                    trust: false,
                    output: "htmlAndMathml",
                  });
                  return (
                    <span
                      key={idx}
                      className="katex-inline inline-block align-baseline mx-0.5"
                      dangerouslySetInnerHTML={{ __html: html }}
                    />
                  );
                } catch {
                  return <span key={idx}>{token.raw ?? token.latex}</span>;
                }
              }
              return <React.Fragment key={idx}>{token.value}</React.Fragment>;
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

            const { latex } = normalizeMathExpression(math);
            try {
              const html = katex.renderToString(latex, {
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
