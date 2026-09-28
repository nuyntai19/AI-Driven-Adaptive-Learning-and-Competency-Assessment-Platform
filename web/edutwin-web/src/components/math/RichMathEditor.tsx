import React, { useState, useEffect, useRef, useCallback } from "react";
import katex from "katex";
import { VisualMathField } from "./VisualMathField";
import { MathPreviewCore } from "./MathPreviewCore";
import {
  cleanFormulaForInsertion,
  hasUnfilledPlaceholder,
  validateAndCleanFormula,
} from "../../pages/centerManagerQuestionEditorHelpers";

export interface RichMathEditorProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
  minHeight?: string;
  singleLine?: boolean;
  label?: string;
  className?: string;
  id?: string;
}

/**
 * Serializes the contenteditable DOM tree into a canonical string with $...$ formulas.
 */
export function serializeEditorDom(container: HTMLElement): string {
  let result = "";

  function walk(node: Node) {
    if (node.nodeType === Node.TEXT_NODE) {
      result += node.nodeValue || "";
    } else if (node.nodeType === Node.ELEMENT_NODE) {
      const el = node as HTMLElement;
      if (el.classList.contains("inline-math-node")) {
        const latex = cleanFormulaForInsertion(el.dataset.latex || "");
        if (latex) {
          result += `$${latex}$`;
        }
      } else if (el.tagName === "BR") {
        result += "\n";
      } else if (el.tagName === "DIV" || el.tagName === "P") {
        if (result.length > 0 && !result.endsWith("\n")) {
          result += "\n";
        }
        for (const child of Array.from(el.childNodes)) {
          walk(child);
        }
      } else {
        for (const child of Array.from(el.childNodes)) {
          walk(child);
        }
      }
    }
  }

  for (const child of Array.from(container.childNodes)) {
    walk(child);
  }

  return result;
}

/**
 * Creates an inline math node representing a formula rendered with KaTeX.
 */
function createMathSpan(
  latex: string,
  onEdit: (span: HTMLElement) => void
): HTMLElement {
  const span = document.createElement("span");
  span.className =
    "inline-math-node inline-flex items-center align-middle mx-1 px-2 py-0.5 rounded-lg border border-cyan-500/40 bg-cyan-950/40 hover:bg-cyan-900/60 hover:border-cyan-400 text-cyan-200 cursor-pointer select-none transition-all group font-normal text-sm shadow-sm";
  span.contentEditable = "false";
  span.dataset.latex = latex;
  span.title = "Nhấp để chỉnh sửa công thức toán";

  const clean = cleanFormulaForInsertion(latex);
  if (clean) {
    try {
      const html = katex.renderToString(clean, {
        throwOnError: false,
        displayMode: false,
        output: "htmlAndMathml",
      });
      span.innerHTML = `<span class="katex-rendered pointer-events-none">${html}</span>`;
    } catch {
      span.innerHTML = `<span class="katex-fallback font-mono text-xs text-rose-300 pointer-events-none">${clean}</span>`;
    }
  } else {
    span.innerHTML =
      '<span class="katex-placeholder text-xs italic px-1 text-cyan-300 bg-cyan-900/50 rounded pointer-events-none">[Công thức...]</span>';
  }

  span.addEventListener("click", (e) => {
    e.stopPropagation();
    onEdit(span);
  });

  return span;
}

/**
 * Hydrates the contenteditable DOM tree from a serialized raw string containing $...$ or $$...$$.
 */
function hydrateEditorDom(
  container: HTMLElement,
  raw: string,
  onEdit: (span: HTMLElement) => void
): void {
  container.innerHTML = "";
  if (!raw) {
    return;
  }

  const regex = /(\$\$[\s\S]*?\$\$|\$[^$\n]+?\$)/g;
  const parts = raw.split(regex);

  for (const part of parts) {
    if (!part) continue;
    if (
      (part.startsWith("$$") && part.endsWith("$$") && part.length >= 4) ||
      (part.startsWith("$") && part.endsWith("$") && part.length >= 2)
    ) {
      const formula = part.startsWith("$$")
        ? part.slice(2, -2).trim()
        : part.slice(1, -1).trim();
      const mathSpan = createMathSpan(formula, onEdit);
      container.appendChild(mathSpan);
    } else {
      const lines = part.split("\n");
      for (let i = 0; i < lines.length; i++) {
        if (lines[i]) {
          container.appendChild(document.createTextNode(lines[i]));
        }
        if (i < lines.length - 1) {
          container.appendChild(document.createElement("br"));
        }
      }
    }
  }

  const lastChild = container.lastChild;
  if (
    lastChild &&
    (lastChild as HTMLElement).classList?.contains("inline-math-node")
  ) {
    container.appendChild(document.createTextNode("\u00A0"));
  }
}

/**
 * RichMathEditor: WYSIWYG mixed-content editor for prose + interactive inline math formulas.
 *
 * Core Capabilities:
 * - Direct Vietnamese prose input (native IME, copy/paste, text selection).
 * - Inline math nodes rendered via KaTeX directly in the text flow (no raw LaTeX visible).
 * - Typing "$" or pressing Ctrl+M seamlessly spawns an inline MathLive field at the caret.
 * - Placeholder safety: blocks committing/saving when \placeholder{} remains unfilled.
 * - In-place editing: clicking any rendered formula opens it for modification.
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

  const editorRef = useRef<HTMLDivElement>(null);
  const isLocalChangeRef = useRef<boolean>(false);

  const handleOpenMathNode = useCallback((span: HTMLElement, isNew = false) => {
    const currentLatex = span.dataset.latex || "";
    setActiveMathNode({ element: span, latex: currentLatex, isNew });
    setDialogLatex(currentLatex);
    setDialogError(null);
  }, []);

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

    const trailingSpace = document.createTextNode("\u00A0");
    mathSpan.after(trailingSpace);

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

  // Confirm and commit formula from the dialog
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

    setActiveMathNode(null);
    setDialogLatex("");
    setDialogError(null);

    isLocalChangeRef.current = true;
    const serialized = serializeEditorDom(editorRef.current);
    onChange(serialized);

    // Place caret in text node right after formula
    setTimeout(() => {
      if (element.nextSibling && editorRef.current) {
        const nextNode = element.nextSibling;
        const sel = window.getSelection();
        if (sel) {
          const range = document.createRange();
          range.setStart(nextNode, Math.min(1, nextNode.nodeValue?.length || 0));
          range.collapse(true);
          sel.removeAllRanges();
          sel.addRange(range);
        }
      }
    }, 20);
  };

  // Delete formula
  const handleDeleteMath = () => {
    if (!activeMathNode || !editorRef.current) return;
    const { element } = activeMathNode;
    element.remove();

    setActiveMathNode(null);
    setDialogLatex("");
    setDialogError(null);

    isLocalChangeRef.current = true;
    const serialized = serializeEditorDom(editorRef.current);
    onChange(serialized);
  };

  // Cancel dialog
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
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-medium bg-rose-950/80 border border-rose-500/50 text-rose-300 animate-pulse">
              ⚠️ Có công thức chứa \placeholder chưa điền
            </span>
          )}
        </div>

        <div className="flex items-center gap-2">
          {!disabled && viewMode === "visual" && (
            <>
              <span className="hidden sm:inline text-[11px] text-[var(--cm-text-muted)]">
                Phím tắt: <code className="text-cyan-400 font-mono font-bold">$</code> hoặc <code className="text-cyan-400 font-mono font-bold">Ctrl+M</code>
              </span>
              <button
                type="button"
                onClick={handleInsertMathAtCursor}
                className="inline-flex items-center gap-1.5 px-2 py-1 rounded-lg text-xs font-semibold bg-cyan-500/10 hover:bg-cyan-500/20 text-cyan-300 border border-cyan-500/30 transition-all cursor-pointer shadow-sm active:scale-95"
                title="Chèn công thức toán tại con trỏ ($ hoặc Ctrl+M)"
              >
                <span>+ Σ</span>
                <span>Chèn công thức</span>
              </button>
            </>
          )}

          {/* Mode Switcher */}
          <button
            type="button"
            onClick={() => {
              if (viewMode === "visual") {
                setViewMode("source");
              } else {
                setViewMode("visual");
              }
            }}
            className="text-[11px] text-[var(--cm-text-muted)] hover:text-white px-1.5 py-0.5 rounded border border-transparent hover:border-slate-700 transition-colors cursor-pointer"
            title="Chuyển đổi giữa chế độ Trực quan (WYSIWYG) và Mã nguồn ($...$)"
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

      {/* In-place Math Editing Modal / Popover */}
      {activeMathNode && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Chỉnh sửa công thức toán học"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm animate-fade-in"
          onClick={handleCancelMath}
          onKeyDown={(e) => {
            if (e.key === "Escape") handleCancelMath();
          }}
        >
          <div
            className="w-full max-w-lg rounded-2xl border border-cyan-500/40 bg-slate-900 shadow-2xl p-5 space-y-4 animate-scale-in text-[var(--cm-text)]"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between pb-3 border-b border-slate-800">
              <div className="flex items-center gap-2">
                <span className="p-1.5 rounded-lg bg-cyan-500/20 text-cyan-400 font-bold text-sm">
                  Σ
                </span>
                <h4 className="text-sm font-semibold text-white">
                  Soạn thảo Công thức Toán (MathLive)
                </h4>
              </div>
              <button
                type="button"
                onClick={handleCancelMath}
                aria-label="Đóng popover"
                className="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-slate-800 transition-colors cursor-pointer"
              >
                ✕
              </button>
            </div>

            {/* Error banner */}
            {dialogError && (
              <div
                role="alert"
                className="rounded-lg p-2.5 bg-rose-950/70 border border-rose-500/50 text-rose-200 text-xs flex items-center gap-2"
              >
                <span>⚠️</span>
                <span>{dialogError}</span>
              </div>
            )}

            {/* MathLive Input */}
            <div className="space-y-1.5">
              <label className="block text-xs font-semibold text-slate-300 uppercase tracking-wider">
                Nhập công thức trực quan
              </label>
              <div
                className="rounded-xl border border-cyan-500/30 bg-slate-950 p-2"
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    e.preventDefault();
                    handleConfirmMath();
                  }
                }}
              >
                <VisualMathField
                  value={dialogLatex}
                  onChange={(val) => {
                    setDialogLatex(val);
                    if (dialogError) setDialogError(null);
                  }}
                  autoFocus={true}
                  className="w-full min-h-[48px] text-lg font-mono text-cyan-200"
                />
              </div>
              <p className="text-[11px] text-slate-400">
                Sử dụng bàn phím ảo MathLive hoặc gõ phím tắt (VD: <kbd className="px-1 py-0.5 rounded bg-slate-800 text-slate-300 font-mono">/</kbd> cho phân số, <kbd className="px-1 py-0.5 rounded bg-slate-800 text-slate-300 font-mono">^</kbd> cho số mũ).
              </p>
            </div>

            {/* Live KaTeX Preview */}
            <div className="rounded-xl border border-slate-800 bg-slate-950/60 p-3">
              <div className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider mb-1">
                Xem trước kết quả (KaTeX)
              </div>
              <div className="min-h-[36px] flex items-center justify-center">
                {dialogLatex.trim() ? (
                  <MathPreviewCore
                    formula={cleanFormulaForInsertion(dialogLatex)}
                    mode="formula"
                    displayMode={false}
                  />
                ) : (
                  <span className="text-xs italic text-slate-500">
                    Chưa có công thức
                  </span>
                )}
              </div>
            </div>

            {/* Action Buttons */}
            <div className="flex items-center justify-between pt-2 border-t border-slate-800">
              <button
                type="button"
                onClick={handleDeleteMath}
                className="px-3 py-1.5 rounded-lg text-xs font-semibold text-rose-400 hover:text-rose-300 hover:bg-rose-950/50 transition-colors cursor-pointer"
              >
                🗑️ Xóa công thức
              </button>

              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={handleCancelMath}
                  className="px-4 py-2 rounded-xl text-xs font-semibold text-slate-300 hover:bg-slate-800 transition-colors cursor-pointer"
                >
                  Hủy (Esc)
                </button>
                <button
                  type="button"
                  onClick={handleConfirmMath}
                  disabled={!dialogLatex.trim() || hasUnfilledPlaceholder(dialogLatex)}
                  className="px-4 py-2 rounded-xl text-xs font-semibold text-white bg-blue-600 hover:bg-blue-500 shadow-md shadow-blue-600/30 disabled:opacity-40 disabled:cursor-not-allowed transition-all cursor-pointer"
                >
                  ✓ Hoàn tất (Enter)
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
