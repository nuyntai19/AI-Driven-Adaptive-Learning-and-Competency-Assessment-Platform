import React, { useMemo } from "react";
import { renderSafeKatex } from "./mathPreviewUtils";
import { RichMathText } from "./RichMathText";

export interface MathPreviewCoreProps {
  formula?: string | null;
  content?: string | null;
  displayMode?: boolean;
  className?: string;
  headerSlot?: React.ReactNode;
  mode?: "formula" | "rich";
  emptyPlaceholder?: React.ReactNode;
}

/**
 * MathPreviewCore
 * Centralized, secure KaTeX preview component providing consistent rendering
 * across Student, Teacher, and CenterManager surfaces.
 */
export const MathPreviewCore: React.FC<MathPreviewCoreProps> = ({
  formula,
  content,
  displayMode = false,
  className = "",
  headerSlot,
  mode = "rich",
  emptyPlaceholder = null,
}) => {
  const targetContent = (formula ?? content ?? "").trim();

  const renderedFormulaHtml = useMemo(() => {
    if (!targetContent || mode !== "formula") return null;
    return renderSafeKatex(targetContent, displayMode);
  }, [targetContent, displayMode, mode]);

  if (!targetContent) {
    return emptyPlaceholder ? <>{emptyPlaceholder}</> : null;
  }

  return (
    <div className={className} aria-label="Mathematical formula preview">
      {headerSlot}
      {mode === "formula" ? (
        renderedFormulaHtml ? (
          <div
            className="overflow-x-auto text-center py-1 select-text"
            dangerouslySetInnerHTML={{ __html: renderedFormulaHtml }}
          />
        ) : (
          <div className="text-xs font-mono break-all py-1 opacity-80">
            {targetContent}
          </div>
        )
      ) : (
        <div className="overflow-x-auto py-1 text-sm font-medium leading-relaxed select-text">
          <RichMathText text={targetContent} displayMode={displayMode} />
        </div>
      )}
    </div>
  );
};
