import React, { useMemo } from "react";
import katex from "katex";

export interface MathFormulaPreviewProps {
  formula: string;
  displayMode?: boolean;
  className?: string;
  label?: string;
}

export const MathFormulaPreview: React.FC<MathFormulaPreviewProps> = ({
  formula,
  displayMode = true,
  className = "",
  label = "Math Preview",
}) => {
  const trimmed = formula.trim();

  const renderedHtml = useMemo(() => {
    if (!trimmed) return null;
    try {
      // Invariant: trust: false strictly enforced to block malicious commands/macros
      return katex.renderToString(trimmed, {
        throwOnError: false,
        displayMode,
        trust: false,
        output: "htmlAndMathml",
      });
    } catch {
      return null;
    }
  }, [trimmed, displayMode]);

  if (!trimmed) {
    return null;
  }

  return (
    <div
      className={`rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50/75 dark:bg-slate-900/75 p-3 transition-colors ${className}`}
      aria-label="Mathematical formula preview"
    >
      <div className="flex items-center justify-between pb-1 mb-2 border-b border-slate-200 dark:border-slate-800 text-xs font-semibold text-slate-500 dark:text-slate-400">
        <span>{label}</span>
        <span className="text-[10px] px-1.5 py-0.5 rounded bg-blue-100 dark:bg-blue-950 text-blue-700 dark:text-blue-300 font-mono">
          KaTeX
        </span>
      </div>
      {renderedHtml ? (
        <div
          className="overflow-x-auto text-slate-900 dark:text-slate-100 text-center py-1"
          dangerouslySetInnerHTML={{ __html: renderedHtml }}
        />
      ) : (
        <div className="text-xs text-amber-600 dark:text-amber-400 font-mono break-all py-1">
          {trimmed}
        </div>
      )}
    </div>
  );
};
