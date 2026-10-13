import React, { useMemo } from "react";
import katex from "katex";
import { displayMathText, tokenizeDelimitedMath } from "../../utils/mathDelimiters.ts";

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
 * Preserves natural word spacing and typography while seamlessly rendering mathematical
 * expressions, formulas, exponents, intervals, and set notations via KaTeX.
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
    const parts = tokenizeDelimitedMath(rawContent);
    const hasDelimitedMath = parts.some((part) => part.type === "math");

    // 1. Check if the entire string is a pure LaTeX formula (e.g. starts with backslash commands)
    const isPureLatex = !hasDelimitedMath &&
      /^\\(frac|sqrt|int|sum|prod|lim|vec|alpha|beta|gamma|theta|Delta|begin|left|mathbf|text|displaystyle)\b/.test(rawContent);

    if (isPureLatex) {
      const formulaOnly = rawContent;
      const blockFormula = displayMode;
      try {
        const html = katex.renderToString(formulaOnly, {
          throwOnError: false,
          displayMode: blockFormula,
          trust: false,
          maxExpand: 100,
          maxSize: 10,
          output: "htmlAndMathml",
        });
        return (
          <span
            className={blockFormula ? "katex-block my-1 block text-center" : "katex-inline inline-block align-middle mx-0.5"}
            dangerouslySetInnerHTML={{ __html: html }}
          />
        );
      } catch {
        return <span>{rawContent}</span>;
      }
    }

    if (!hasDelimitedMath) {
      // 3. No explicit delimiters: run intelligent natural text math tokenizer
      const tokens = tokenizePlainText(rawContent);
      if (tokens.some((t) => t.type === "math")) {
        return (
          <>
            {tokens.map((token, idx) => {
              if (token.type === "math" && token.latex) {
                try {
                  const html = katex.renderToString(token.latex, {
                    throwOnError: false,
                    displayMode: false,
                    trust: false,
                    maxExpand: 100,
                    maxSize: 10,
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
                  return <span key={idx}>{token.raw ?? token.latex}</span>;
                }
              }
              return <React.Fragment key={idx}>{displayMathText(token.value ?? "")}</React.Fragment>;
            })}
          </>
        );
      }

      // Text without dollar delimiters: check for inline LaTeX commands like \frac{a}{b}, \sqrt{x}, \pm, \times
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
                    maxExpand: 100,
                    maxSize: 10,
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
              return <React.Fragment key={idx}>{displayMathText(sub)}</React.Fragment>;
            })}
          </>
        );
      }

      // Plain natural language text: preserve spacing & typography completely
      return <span>{displayMathText(rawContent)}</span>;
    }

    // 4. Render delimited LaTeX tokens alongside plaintext tokens
    return (
      <>
        {parts.map((part, index) => {

          // Block Math: $$...$$ or \[...\]
          if (part.type === "math" && part.displayMode) {
            const math = part.latex.trim();
            try {
              const html = katex.renderToString(math, {
                throwOnError: false,
                displayMode: true,
                trust: false,
                maxExpand: 100,
                maxSize: 10,
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
              return <span key={index}>{part.raw}</span>;
            }
          }

          // Inline Math: $...$ or \(...\)
          if (part.type === "math") {
            const math = part.latex.trim();

            // Check prose heuristic: if it's natural language wrapped in dollars, do NOT KaTeX it!
            if (isLikelyProse(math)) {
              return <React.Fragment key={index}>{displayMathText(math)}</React.Fragment>;
            }

            const { latex } = normalizeMathExpression(math);
            try {
              const html = katex.renderToString(latex, {
                throwOnError: false,
                displayMode: false,
                trust: false,
                maxExpand: 100,
                maxSize: 10,
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
              return <span key={index}>{part.raw}</span>;
            }
          }

          // Plain text token with natural spaces
          return <React.Fragment key={index}>{displayMathText(part.raw)}</React.Fragment>;
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
