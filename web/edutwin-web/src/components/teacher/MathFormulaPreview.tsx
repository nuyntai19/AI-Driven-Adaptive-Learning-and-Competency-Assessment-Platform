import React from "react";
import { RichMathText } from "../math/RichMathText";

export interface TeacherMathFormulaPreviewProps {
  formula?: string;
  content?: string;
  displayMode?: boolean;
  className?: string;
  label?: string;
}

/**
 * MathFormulaPreview (Teacher)
 * Independent Math/KaTeX formula preview component specifically designed and isolated for the Teacher workspace.
 * Renders LaTeX formulas seamlessly with the Teacher Theme styling.
 */
export const MathFormulaPreview: React.FC<TeacherMathFormulaPreviewProps> = ({
  formula,
  content,
  displayMode = false,
  className = "",
  label = "Xem trước KaTeX",
}) => {
  const targetContent = (formula ?? content ?? "").trim();

  if (!targetContent) {
    return null;
  }

  return (
    <div
      className={`rounded-xl border border-[var(--th-border)] bg-[var(--th-surface)] p-3 text-xs shadow-sm transition-colors ${className}`}
      aria-label="Mathematical formula preview"
    >
      <div className="flex items-center justify-between pb-1.5 mb-2 border-b border-[var(--th-border-subtle)] text-[11px] font-bold text-[var(--th-text-secondary)]">
        <span>{label}</span>
        <span className="text-[10px] px-2 py-0.5 rounded border border-[var(--th-border)] bg-[var(--at-accent-wash)] text-[var(--at-accent-text)] font-mono font-bold uppercase tracking-wider">
          KaTeX
        </span>
      </div>
      <div className="overflow-x-auto text-[var(--th-text)] py-1 text-sm font-medium leading-relaxed">
        <RichMathText text={targetContent} displayMode={displayMode} />
      </div>
    </div>
  );
};

// Also export as TeacherMathFormulaPreview for explicit naming
export const TeacherMathFormulaPreview = MathFormulaPreview;
