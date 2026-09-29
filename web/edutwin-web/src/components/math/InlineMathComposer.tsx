import React, { useState, useRef, useEffect, useCallback } from "react";
import { VisualMathField, type VisualMathFieldRef } from "./VisualMathField";
import { MathPreviewCore } from "./MathPreviewCore";
import {
  cleanFormulaForInsertion,
  insertFormulaAtCursor,
} from "../../pages/centerManagerQuestionEditorHelpers";
import {
  hideVirtualKeyboard,
  isVirtualKeyboardVisible,
  wasVirtualKeyboardJustDismissed,
  markVirtualKeyboardDismissed,
} from "../../utils/visualMathFieldLifecycle";

export interface InlineMathComposerProps {
  /** Target text element (textarea or input) to insert into and restore focus/caret */
  targetRef?: React.RefObject<HTMLTextAreaElement | HTMLInputElement | null>;
  /** Callback when formula is confirmed. Receives clean latex, updated text, and new cursor position. */
  onInsert: (formulaLatex: string, newText?: string, newCursorPos?: number) => void;
  /** Custom button label */
  buttonLabel?: string;
  /** Button title / tooltip */
  buttonTitle?: string;
  /** Custom trigger button className */
  className?: string;
  /** Size variant for button */
  size?: "xs" | "sm" | "md";
  /** Disabled state */
  disabled?: boolean;
}

/**
 * InlineMathComposer
 * Modal-based MathLive equation composer for mixed-content fields (questionText, options, solution).
 *
 * Contract:
 * 1. Reuses VisualMathField and existing MathLive loader/fallback infrastructure (zero duplicate loaders).
 * 2. Snapshots selectionStart/selectionEnd before modal causes target to blur.
 * 3. Encloses formula in clean $...$ delimiters without nesting or empty strings.
 * 4. Restores DOM focus and places caret immediately after the inserted formula.
 * 5. Full keyboard support (Escape to cancel) and accessibility attributes.
 */
export const InlineMathComposer: React.FC<InlineMathComposerProps> = ({
  targetRef,
  onInsert,
  buttonLabel = "Chèn công thức",
  buttonTitle = "Mở bảng soạn thảo công thức Toán (MathLive)",
  className = "",
  size = "sm",
  disabled = false,
}) => {
  const [isOpen, setIsOpen] = useState(false);
  const [formulaLatex, setFormulaLatex] = useState("");
  const visualFieldRef = useRef<VisualMathFieldRef>(null);

  // Snapshot cursor / selection from target input or textarea
  const savedSelectionRef = useRef<{ start: number; end: number }>({ start: 0, end: 0 });

  const snapshotSelection = useCallback(() => {
    const el = targetRef?.current;
    if (el) {
      const start = el.selectionStart ?? el.value.length;
      const end = el.selectionEnd ?? start;
      savedSelectionRef.current = { start, end };
    }
  }, [targetRef]);

  const handleOpen = () => {
    if (disabled) return;
    snapshotSelection();
    setFormulaLatex("");
    setIsOpen(true);
  };

  const handleClose = useCallback(() => {
    hideVirtualKeyboard();
    setIsOpen(false);
    setFormulaLatex("");

    // Restore focus to the target element and return caret to saved start position
    const el = targetRef?.current;
    if (el) {
      requestAnimationFrame(() => {
        el.focus();
        try {
          const { start } = savedSelectionRef.current;
          el.setSelectionRange(start, start);
        } catch {
          // Ignore if input type doesn't support selection range
        }
      });
    }
  }, [targetRef]);

  const handleConfirm = useCallback(() => {
    hideVirtualKeyboard();
    const clean = cleanFormulaForInsertion(formulaLatex);
    if (!clean) return;

    const el = targetRef?.current;
    if (el) {
      const currentText = el.value || "";
      const { start, end } = savedSelectionRef.current;
      const { newText, newCursorPos } = insertFormulaAtCursor(currentText, start, end, clean);

      onInsert(clean, newText, newCursorPos);

      setIsOpen(false);
      setFormulaLatex("");

      // Restore focus to target and place caret immediately after inserted formula
      requestAnimationFrame(() => {
        el.focus();
        try {
          el.setSelectionRange(newCursorPos, newCursorPos);
        } catch {
          // Ignore if input type doesn't support selection range
        }
      });
    } else {
      onInsert(clean);
      setIsOpen(false);
      setFormulaLatex("");
    }
  }, [formulaLatex, targetRef, onInsert]);

  // Global Escape key handler
  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.preventDefault();
        handleClose();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, handleClose]);

  // Size styling variants
  const sizeClasses = {
    xs: "px-2 py-0.5 text-[11px]",
    sm: "px-2.5 py-1 text-xs",
    md: "px-3.5 py-1.5 text-sm",
  }[size];

  const hasContent = !!cleanFormulaForInsertion(formulaLatex);

  return (
    <>
      {/* Trigger Button */}
      <button
        type="button"
        disabled={disabled}
        onMouseDown={snapshotSelection}
        onClick={handleOpen}
        title={buttonTitle}
        aria-label={buttonTitle}
        className={`inline-flex items-center gap-1.5 font-semibold rounded-lg border transition-all duration-150 ${sizeClasses} ${
          disabled
            ? "opacity-50 cursor-not-allowed bg-slate-800/40 text-slate-500 border-slate-700/40"
            : "bg-cyan-500/10 hover:bg-cyan-500/20 text-cyan-400 hover:text-cyan-300 border-cyan-500/30 hover:border-cyan-400/50 shadow-sm active:scale-[0.98]"
        } ${className}`}
      >
        <span className="font-mono font-bold text-[1.1em] text-cyan-300 select-none">∑</span>
        <span>{buttonLabel}</span>
      </button>

      {/* Modal Dialog */}
      {isOpen && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="math-composer-dialog-title"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-sm animate-in fade-in duration-150"
          onPointerDown={(e) => {
            if (e.target === e.currentTarget && isVirtualKeyboardVisible()) {
              markVirtualKeyboardDismissed();
              hideVirtualKeyboard();
            }
          }}
          onClick={(e) => {
            if (e.target === e.currentTarget) {
              if (isVirtualKeyboardVisible() || wasVirtualKeyboardJustDismissed()) {
                markVirtualKeyboardDismissed();
                hideVirtualKeyboard();
                return;
              }
              handleClose();
            }
          }}
        >
          <div
            className="w-full max-w-xl rounded-2xl border border-cyan-500/30 bg-slate-900/95 p-5 shadow-2xl space-y-4 text-slate-100 flex flex-col max-h-[90vh] rich-math-popover"
            onClick={(e) => e.stopPropagation()}
          >
            {/* Header */}
            <div className="flex items-center justify-between pb-3 border-b border-slate-800">
              <div className="flex items-center gap-2">
                <span className="flex items-center justify-center w-7 h-7 rounded-lg bg-cyan-500/20 text-cyan-300 font-mono font-bold text-sm">
                  ∑
                </span>
                <h3
                  id="math-composer-dialog-title"
                  className="text-base font-semibold text-white tracking-wide"
                >
                  Soạn thảo công thức Toán học
                </h3>
              </div>
              <button
                type="button"
                onClick={handleClose}
                aria-label="Đóng bảng soạn thảo công thức"
                className="p-1 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 transition-colors text-sm"
              >
                ✕
              </button>
            </div>

            {/* Instruction */}
            <p className="text-xs text-slate-400 leading-relaxed">
              Nhập công thức bằng bàn phím ảo MathLive hoặc gõ mã LaTeX. Khi chèn vào văn bản, công thức sẽ tự động được bọc trong ký hiệu chuẩn{" "}
              <code className="px-1 py-0.5 rounded bg-slate-800 text-cyan-300 font-mono text-[11px]">
                $công_thức$
              </code>
              .
            </p>

            {/* MathLive VisualMathField Input */}
            <div className="space-y-1.5 flex-1 overflow-visible">
              <label className="block text-xs font-semibold uppercase tracking-wider text-slate-400">
                Ô nhập công thức trực quan (MathLive)
              </label>
              <div className="rounded-xl border border-slate-700 bg-slate-950/70 p-2 focus-within:border-cyan-400 focus-within:ring-1 focus-within:ring-cyan-400 transition-all">
                <VisualMathField
                  ref={visualFieldRef}
                  value={formulaLatex}
                  onChange={(latex) => setFormulaLatex(latex)}
                  placeholder="Gõ công thức hoặc chọn ký hiệu trên bàn phím ảo..."
                  autoFocus
                />
              </div>
            </div>

            {/* Live KaTeX Preview */}
            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <span className="text-xs font-semibold uppercase tracking-wider text-slate-400">
                  Xem trước trực tiếp (KaTeX)
                </span>
                {hasContent && (
                  <button
                    type="button"
                    onClick={() => {
                      setFormulaLatex("");
                      visualFieldRef.current?.clear();
                    }}
                    className="text-[11px] text-rose-400 hover:text-rose-300 hover:underline"
                  >
                    Xóa công thức
                  </button>
                )}
              </div>
              <div className="min-h-[50px] max-h-[120px] overflow-auto rounded-xl border border-slate-800 bg-slate-950/40 p-3 flex items-center justify-center">
                {hasContent ? (
                  <MathPreviewCore
                    formula={cleanFormulaForInsertion(formulaLatex)}
                    mode="formula"
                    displayMode={false}
                    className="text-cyan-200 text-sm font-medium"
                  />
                ) : (
                  <span className="text-xs text-slate-500 italic select-none">
                    Chưa có công thức để xem trước
                  </span>
                )}
              </div>
            </div>

            {/* Footer Actions */}
            <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-800">
              <button
                type="button"
                onClick={handleClose}
                className="px-4 py-2 text-xs font-medium rounded-lg text-slate-300 hover:text-white hover:bg-slate-800 transition-colors"
              >
                Hủy (Esc)
              </button>
              <button
                type="button"
                disabled={!hasContent}
                onClick={handleConfirm}
                className={`px-4 py-2 text-xs font-semibold rounded-lg transition-all flex items-center gap-1.5 ${
                  hasContent
                    ? "bg-cyan-500 hover:bg-cyan-400 text-slate-950 shadow-md shadow-cyan-500/20 active:scale-[0.98]"
                    : "bg-slate-800 text-slate-500 cursor-not-allowed border border-slate-700/50"
                }`}
              >
                <span>Chèn vào văn bản</span>
                <span className="font-mono text-[10px]">↵</span>
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
};
