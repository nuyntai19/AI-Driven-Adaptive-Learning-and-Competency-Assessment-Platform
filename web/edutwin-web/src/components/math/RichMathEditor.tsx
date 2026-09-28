import React, { useState, useRef, useEffect, useCallback } from "react";
import katex from "katex";
import {
  cleanFormulaForInsertion,
  validateAndCleanFormula,
  hasUnfilledPlaceholder,
} from "../../pages/centerManagerQuestionEditorHelpers";
import { VisualMathField } from "./VisualMathField";
import {
  serializeEditorDom,
  createMathSpan,
  hydrateEditorDom,
} from "./richMathEditorHelpers";

export interface RichMathEditorProps {
  value: string;
  onChange: (nextValue: string) => void;
  placeholder?: string;
  disabled?: boolean;
  minHeight?: string;
  singleLine?: boolean;
  label?: string;
  className?: string;
  id?: string;
}

/**
 * RichMathEditor: WYSIWYG mixed-content editor for Vietnamese prose + interactive inline math formulas.
 *
 * Core Capabilities:
 * - Direct Vietnamese prose input (native IME, copy/paste, text selection).
 * - Inline math nodes rendered via KaTeX directly in the text flow (no raw LaTeX visible).
 * - Typing "$" or pressing Ctrl+M seamlessly spawns an anchored in-place MathLive popover at the caret.
 * - Placeholder safety: blocks committing/saving when \placeholder{} remains unfilled.
 * - In-place editing: clicking any rendered formula opens an anchored popover for modification.
 * - Enter commits formula; Escape or click-outside cancels.
 * - Clean whitespace management (no stray NBSP).
 * - Under-the-hood serialization to canonical "prose + $latex$" for API contracts.
 */
export const RichMathEditor: React.FC<RichMathEditorProps> = ({
  value,
  onChange,
  placeholder = "Nhập văn bản... (Gõ $ hoặc Ctrl+M để chèn công thức toán)",
  disabled = false,
  minHeight = "96px",
  singleLine = false,
  label,
  className = "",
  id,
}) => {
  const [viewMode, setViewMode] = useState<"visual" | "source">("visual");
  const [activeMathNode, setActiveMathNode] = useState<{
    element: HTMLElement;
    latex: string;
    isNew?: boolean;
  } | null>(null);

  const [dialogLatex, setDialogLatex] = useState<string>("");
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [popoverPos, setPopoverPos] = useState<{ top: number; left: number } | null>(null);

  const editorRef = useRef<HTMLDivElement>(null);
  const isLocalChangeRef = useRef<boolean>(false);

  const handleOpenMathNode = useCallback((span: HTMLElement, isNew = false) => {
    const currentLatex = span.dataset.latex || "";
    setActiveMathNode({ element: span, latex: currentLatex, isNew });
    setDialogLatex(currentLatex);
    setDialogError(null);
  }, []);

  // Update floating popover position anchored directly to the active math node
  useEffect(() => {
    if (!activeMathNode?.element) {
      setPopoverPos(null);
      return;
    }

    const updatePosition = () => {
      const rect = activeMathNode.element.getBoundingClientRect();
      const popoverWidth = 380;
      const popoverHeight = 240;

      let left = rect.left;
      if (left + popoverWidth > window.innerWidth - 16) {
        left = window.innerWidth - popoverWidth - 16;
      }
      if (left < 16) left = 16;

      let top = rect.bottom + 6;
      if (top + popoverHeight > window.innerHeight - 16 && rect.top > popoverHeight + 16) {
        top = rect.top - popoverHeight - 6;
      }

      setPopoverPos({ top, left });
    };

    updatePosition();
    window.addEventListener("scroll", updatePosition, true);
    window.addEventListener("resize", updatePosition);
    return () => {
      window.removeEventListener("scroll", updatePosition, true);
      window.removeEventListener("resize", updatePosition);
    };
  }, [activeMathNode]);

  // Hydrate DOM on initial mount or external value changes
  useEffect(() => {
    if (viewMode !== "visual") return;
    if (isLocalChangeRef.current) {
      isLocalChangeRef.current = false;
      return;
    }
    if (editorRef.current) {
      hydrateEditorDom(editorRef.current, value, (span) =>
        handleOpenMathNode(span, false)
      );
    }
  }, [value, viewMode, handleOpenMathNode]);

  // Insert inline math node at current browser caret
  const handleInsertMathAtCursor = useCallback(() => {
    if (disabled || viewMode !== "visual") return;
    const container = editorRef.current;
    if (!container) return;

    container.focus();
    const sel = window.getSelection();

    let targetRange: Range | null = null;
    if (sel && sel.rangeCount > 0 && container.contains(sel.anchorNode)) {
      targetRange = sel.getRangeAt(0);
    } else {
      targetRange = document.createRange();
      targetRange.selectNodeContents(container);
      targetRange.collapse(false);
    }

    const mathSpan = createMathSpan("", (span) =>
      handleOpenMathNode(span, false)
    );
    mathSpan.dataset.isNew = "true";

    targetRange.deleteContents();
    targetRange.insertNode(mathSpan);

    handleOpenMathNode(mathSpan, true);
  }, [disabled, viewMode, handleOpenMathNode]);

  // Handle typing inside contenteditable
  const handleEditorKeyDown = (e: React.KeyboardEvent<HTMLDivElement>) => {
    if (disabled) return;

    if (singleLine && e.key === "Enter") {
      e.preventDefault();
      return;
    }

    // Trigger math insertion on "$" or "Ctrl+M"
    if (e.key === "$" || (e.ctrlKey && (e.key === "m" || e.key === "M"))) {
      e.preventDefault();
      handleInsertMathAtCursor();
      return;
    }
  };

  // Event delegation: clicking any inline math node opens the in-place editor
  const handleEditorClick = (e: React.MouseEvent<HTMLDivElement>) => {
    if (disabled) return;
    const target = e.target as HTMLElement;
    const mathNode = target.closest<HTMLElement>(".inline-math-node");
    if (mathNode && editorRef.current?.contains(mathNode)) {
      e.preventDefault();
      e.stopPropagation();
      handleOpenMathNode(mathNode, false);
    }
  };

  const handleEditorInput = () => {
    if (disabled || !editorRef.current) return;
    isLocalChangeRef.current = true;
    const serialized = serializeEditorDom(editorRef.current);
    onChange(serialized);
  };

  // Handle paste in visual mode
  const handleEditorPaste = (e: React.ClipboardEvent<HTMLDivElement>) => {
    if (disabled) return;
    const text = e.clipboardData.getData("text/plain");
    if (text.includes("$")) {
      e.preventDefault();
      const sel = window.getSelection();
      if (!sel || sel.rangeCount === 0 || !editorRef.current) return;

      const range = sel.getRangeAt(0);
      range.deleteContents();

      const tempContainer = document.createElement("div");
      hydrateEditorDom(tempContainer, text, (span) =>
        handleOpenMathNode(span, false)
      );

      const frag = document.createDocumentFragment();
      while (tempContainer.firstChild) {
        frag.appendChild(tempContainer.firstChild);
      }
      range.insertNode(frag);

      isLocalChangeRef.current = true;
      const serialized = serializeEditorDom(editorRef.current);
      onChange(serialized);
    }
  };

  // Confirm and commit formula from the popover
  const handleConfirmMath = () => {
    if (!activeMathNode || !editorRef.current) return;

    const validation = validateAndCleanFormula(dialogLatex);
    if (!validation.isComplete || !validation.cleanLatex) {
      setDialogError(
        validation.error ||
          "Công thức chưa hoàn thành. Vui lòng kiểm tra lại."
      );
      return;
    }

    const { element } = activeMathNode;
    element.dataset.latex = validation.cleanLatex;
    delete element.dataset.isNew;

    try {
      const html = katex.renderToString(validation.cleanLatex, {
        throwOnError: false,
        displayMode: false,
        output: "htmlAndMathml",
      });
      element.innerHTML = `<span class="katex-rendered pointer-events-none">${html}</span>`;
    } catch {
      element.innerHTML = `<span class="katex-fallback font-mono text-xs text-rose-300 pointer-events-none">${validation.cleanLatex}</span>`;
    }

    // Position caret right after the confirmed formula node
    const sel = window.getSelection();
    if (sel) {
      const range = document.createRange();
      range.setStartAfter(element);
      range.setEndAfter(element);
      sel.removeAllRanges();
      sel.addRange(range);
    }

    setActiveMathNode(null);
    setDialogLatex("");
    setDialogError(null);

    isLocalChangeRef.current = true;
    const serialized = serializeEditorDom(editorRef.current);
    onChange(serialized);
  };

  // Delete math node
  const handleDeleteMath = () => {
    if (!activeMathNode || !editorRef.current) return;
    const el = activeMathNode.element;
    el.remove();

    setActiveMathNode(null);
    setDialogLatex("");
    setDialogError(null);

    isLocalChangeRef.current = true;
    const serialized = serializeEditorDom(editorRef.current);
    onChange(serialized);
  };

  // Cancel popover (removes uncommitted new node cleanly)
  const handleCancelMath = () => {
    if (!activeMathNode) return;
    if (activeMathNode.isNew) {
      activeMathNode.element.remove();
      if (editorRef.current) {
        isLocalChangeRef.current = true;
        onChange(serializeEditorDom(editorRef.current));
      }
    }
    setActiveMathNode(null);
    setDialogLatex("");
    setDialogError(null);
  };

  const hasIncompleteFormulas = hasUnfilledPlaceholder(value);

  return (
    <div
      className={`rich-math-editor group flex flex-col rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-subtle)] transition-all focus-within:border-[var(--cm-cyan)] focus-within:ring-1 focus-within:ring-[var(--cm-cyan)] ${className}`}
    >
      {/* Micro-toolbar */}
      <div className="flex items-center justify-between px-3 py-1.5 border-b border-[var(--cm-border-subtle)] bg-black/20 text-xs select-none">
        <div className="flex items-center gap-2">
          {label && (
            <span className="font-semibold text-[var(--cm-text-muted)] tracking-wider uppercase text-[11px]">
              {label}
            </span>
          )}
          <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-medium bg-cyan-950/60 border border-cyan-500/30 text-cyan-300">
            WYSIWYG
          </span>
          {hasIncompleteFormulas && (
            <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-bold bg-rose-950/70 border border-rose-500/50 text-rose-300 animate-pulse">
              ⚠️ Có công thức chưa điền xong
            </span>
          )}
        </div>

        <div className="flex items-center gap-2">
          {viewMode === "visual" && !disabled && (
            <button
              type="button"
              onClick={handleInsertMathAtCursor}
              className="inline-flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-semibold bg-cyan-500/10 hover:bg-cyan-500/20 text-cyan-300 border border-cyan-500/30 transition-colors cursor-pointer"
              title="Chèn công thức toán tại con trỏ (hoặc gõ $ hoặc phím tắt Ctrl+M)"
            >
              <span>+ Σ</span>
              <span>Chèn công thức</span>
              <kbd className="hidden sm:inline-block ml-1 text-[10px] font-mono px-1 py-0.2 bg-black/30 rounded text-cyan-400">
                $ / Ctrl+M
              </kbd>
            </button>
          )}

          <button
            type="button"
            onClick={() => {
              if (viewMode === "visual") {
                setViewMode("source");
              } else {
                setViewMode("visual");
              }
            }}
            className="text-[11px] font-medium text-[var(--cm-text-muted)] hover:text-[var(--cm-text)] px-2 py-0.5 rounded hover:bg-white/5 transition-colors cursor-pointer"
            title="Chuyển đổi giữa chế độ trực quan WYSIWYG và mã nguồn $...$"
          >
            {viewMode === "visual" ? "⌨️ Mã nguồn" : "👁️ Trực quan"}
          </button>
        </div>
      </div>

      {/* Surface Area */}
      <div className="relative p-3">
        {viewMode === "visual" ? (
          <div
            id={id}
            ref={editorRef}
            contentEditable={!disabled}
            suppressContentEditableWarning
            onClick={handleEditorClick}
            onKeyDown={handleEditorKeyDown}
            onInput={handleEditorInput}
            onPaste={handleEditorPaste}
            data-placeholder={placeholder}
            role="textbox"
            aria-multiline={!singleLine}
            aria-label={label || placeholder}
            style={{ minHeight }}
            className={`rich-math-content-editable outline-none text-sm text-[var(--cm-text)] leading-relaxed select-text font-sans ${
              disabled ? "opacity-60 cursor-not-allowed" : "cursor-text"
            }`}
          />
        ) : (
          <textarea
            id={id}
            value={value}
            disabled={disabled}
            onChange={(e) => onChange(e.target.value)}
            style={{ minHeight }}
            rows={singleLine ? 2 : 4}
            placeholder="Mã nguồn hỗn hợp (VD: Cho hàm số $f(x)=x^2+1$ liên tục...)"
            className="w-full bg-transparent outline-none font-mono text-xs text-cyan-300 leading-relaxed resize-y"
          />
        )}
      </div>

      {/* In-place Anchored Math Editing Popover (No fullscreen blur veil!) */}
      {activeMathNode && popoverPos && (
        <>
          {/* Transparent click-catcher to dismiss popover on outside click without obscuring text */}
          <div
            className="fixed inset-0 z-40 bg-transparent"
            onClick={handleCancelMath}
            aria-hidden="true"
          />

          <div
            role="dialog"
            aria-modal="true"
            aria-label="Chỉnh sửa công thức toán học"
            style={{
              top: `${popoverPos.top}px`,
              left: `${popoverPos.left}px`,
            }}
            className="fixed z-50 w-[380px] max-w-[90vw] rounded-2xl border border-cyan-500/50 bg-slate-900/95 shadow-2xl p-4 space-y-3 backdrop-blur-md text-[var(--cm-text)] animate-scale-in"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between pb-2 border-b border-slate-800">
              <div className="flex items-center gap-2">
                <span className="p-1 rounded-md bg-cyan-500/20 text-cyan-400 font-bold text-xs">
                  Σ
                </span>
                <h4 className="text-xs font-semibold text-white">
                  Soạn công thức (MathLive)
                </h4>
              </div>
              <button
                type="button"
                onClick={handleCancelMath}
                aria-label="Đóng popover"
                className="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-slate-800 transition-colors cursor-pointer text-xs"
              >
                ✕
              </button>
            </div>

            {/* Error banner */}
            {dialogError && (
              <div
                role="alert"
                className="rounded-lg p-2 bg-rose-950/80 border border-rose-500/50 text-rose-200 text-xs flex items-center gap-1.5"
              >
                <span>⚠️</span>
                <span>{dialogError}</span>
              </div>
            )}

            {/* MathLive Input with onCommit (Enter) and onCancel (Escape) */}
            <div className="space-y-1">
              <label className="block text-[11px] font-semibold text-slate-300 uppercase tracking-wider">
                Nhập công thức trực quan
              </label>
              <div className="rounded-xl border border-cyan-500/30 bg-slate-950 p-1.5">
                <VisualMathField
                  value={dialogLatex}
                  onChange={(latex) => {
                    setDialogLatex(latex);
                    if (dialogError) setDialogError(null);
                  }}
                  onCommit={handleConfirmMath}
                  onCancel={handleCancelMath}
                  placeholder="Gõ công thức hoặc chọn ký hiệu..."
                  autoFocus
                />
              </div>
            </div>

            {/* Live KaTeX Preview and Placeholder Indicator */}
            <div className="rounded-lg border border-slate-800 bg-slate-950/70 p-2 space-y-1">
              <div className="flex items-center justify-between text-[10px] text-slate-400">
                <span>Xem trước kết quả:</span>
                {dialogLatex ? (
                  hasUnfilledPlaceholder(dialogLatex) ? (
                    <span className="text-rose-400 font-semibold">
                      Chưa hoàn thành
                    </span>
                  ) : (
                    <span className="text-emerald-400 font-semibold">
                      ✓ Hợp lệ
                    </span>
                  )
                ) : (
                  <span>(trống)</span>
                )}
              </div>
              <div className="min-h-[28px] flex items-center justify-center text-sm text-cyan-200 overflow-x-auto py-1">
                {dialogLatex ? (
                  (() => {
                    const clean = cleanFormulaForInsertion(dialogLatex);
                    try {
                      const html = katex.renderToString(clean, {
                        throwOnError: false,
                        displayMode: false,
                      });
                      return (
                        <span
                          dangerouslySetInnerHTML={{ __html: html }}
                        />
                      );
                    } catch {
                      return (
                        <span className="text-xs text-rose-300 font-mono">
                          {dialogLatex}
                        </span>
                      );
                    }
                  })()
                ) : (
                  <span className="text-xs text-slate-500 italic">
                    Chưa có công thức
                  </span>
                )}
              </div>
            </div>

            {/* Actions */}
            <div className="flex items-center justify-between pt-1">
              {!activeMathNode.isNew ? (
                <button
                  type="button"
                  onClick={handleDeleteMath}
                  className="px-2.5 py-1 text-xs font-medium text-rose-400 hover:text-rose-300 hover:bg-rose-950/50 rounded-lg transition-colors cursor-pointer"
                >
                  Xóa công thức
                </button>
              ) : (
                <div />
              )}

              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={handleCancelMath}
                  className="px-3 py-1 text-xs font-semibold text-slate-400 hover:text-white rounded-lg transition-colors cursor-pointer"
                >
                  Hủy (Esc)
                </button>
                <button
                  type="button"
                  onClick={handleConfirmMath}
                  disabled={
                    !dialogLatex.trim() ||
                    hasUnfilledPlaceholder(dialogLatex)
                  }
                  className={`px-3.5 py-1 text-xs font-semibold rounded-lg shadow-sm transition-all flex items-center gap-1.5 ${
                    !dialogLatex.trim() ||
                    hasUnfilledPlaceholder(dialogLatex)
                      ? "bg-slate-700 text-slate-400 cursor-not-allowed opacity-50"
                      : "bg-cyan-500 hover:bg-cyan-400 text-slate-950 font-bold cursor-pointer"
                  }`}
                >
                  <span>Hoàn tất</span>
                  <kbd className="text-[10px] opacity-75 font-mono">Enter</kbd>
                </button>
              </div>
            </div>
          </div>
        </>
      )}
    </div>
  );
};
