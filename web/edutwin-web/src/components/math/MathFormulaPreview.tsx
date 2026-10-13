import React from "react";
import { MathPreviewCore } from "./MathPreviewCore";

export interface MathFormulaPreviewProps {
  formula: string;
  displayMode?: boolean;
  className?: string;
  label?: string;
}

/**
 * MathFormulaPreview (General / Student / CenterManager)
 * Lightweight KaTeX preview component wrapping MathPreviewCore with standard slate/indigo theme styling.
 */
export const MathFormulaPreview: React.FC<MathFormulaPreviewProps> = ({
  formula,
  displayMode = true,
  className = "",
  label = "Math Preview",
}) => {
  const trimmed = formula?.trim();
  if (!trimmed) return null;

  return (
    <MathPreviewCore
      formula={trimmed}
      displayMode={displayMode}
      mode="formula"
      className={`rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50/75 dark:bg-slate-900/75 p-3 transition-colors text-slate-900 dark:text-slate-100 ${className}`}
      headerSlot={
        <div className="flex items-center justify-between pb-1 mb-2 border-b border-slate-200 dark:border-slate-800 text-xs font-semibold text-slate-500 dark:text-slate-400">
          <span>{label}</span>
          <span className="text-[10px] px-1.5 py-0.5 rounded bg-blue-100 dark:bg-blue-950 text-blue-700 dark:text-blue-300 font-mono">
            KaTeX
          </span>
        </div>
      }
    />
  );
};
