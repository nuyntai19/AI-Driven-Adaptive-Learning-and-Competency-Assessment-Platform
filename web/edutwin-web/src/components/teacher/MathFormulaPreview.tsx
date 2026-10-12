import React from "react";
import { MathPreviewCore } from "../math/MathPreviewCore";

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
 * Wraps MathPreviewCore with the Teacher Theme styling (th-*).
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
    <MathPreviewCore
      content={targetContent}
      displayMode={displayMode}
      mode="rich"
      className={`rounded-xl border border-[var(--th-border)] bg-[var(--th-surface)] p-3 text-xs shadow-sm transition-colors text-[var(--th-text)] ${className}`}
      headerSlot={
        <div className="flex items-center justify-between pb-1.5 mb-2 border-b border-[var(--th-border-subtle)] text-[11px] font-bold text-[var(--th-text-secondary)]">
          <span>{label}</span>
          <span className="text-[10px] px-2 py-0.5 rounded border border-[var(--th-border)] bg-[var(--at-accent-wash)] text-[var(--at-accent-text)] font-mono font-bold uppercase tracking-wider">
            KaTeX
          </span>
        </div>
      }
    />
  );
};

// Also export as TeacherMathFormulaPreview for explicit naming
export const TeacherMathFormulaPreview = MathFormulaPreview;
