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
  variant?: "center-manager" | "teacher" | "student" | "neutral";
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
  variant = "center-manager",
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

  const checkEmpty = useCallback(() => {
    if (!editorRef.current) return;
    const hasText = Boolean(editorRef.current.textContent?.trim());
    const hasMath = Boolean(editorRef.current.querySelector(".inline-math-node"));
    if (!hasText && !hasMath) {
      editorRef.current.setAttribute("data-empty", "true");
    } else {
      editorRef.current.removeAttribute("data-empty");
    }
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
      checkEmpty();
      return;
    }
    if (editorRef.current) {
      hydrateEditorDom(editorRef.current, value, (span) =>
        handleOpenMathNode(span, false)
      );
      checkEmpty();
    }
  }, [value, viewMode, handleOpenMathNode, checkEmpty]);

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
    checkEmpty();
  }, [disabled, viewMode, handleOpenMathNode, checkEmpty]);

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
    const serialized = serializeEditorDom(editorRef.current, singleLine);
    onChange(serialized);
    checkEmpty();
  };

  // Handle paste in visual mode
  const handleEditorPaste = (e: React.ClipboardEvent<HTMLDivElement>) => {
    if (disabled) return;
    let text = e.clipboardData.getData("text/plain");
    if (singleLine) {
      text = text.replace(/[\r\n]+/g, " ");
    }
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
      const serialized = serializeEditorDom(editorRef.current, singleLine);
      onChange(serialized);
      checkEmpty();
    } else if (singleLine && /[\r\n]/.test(e.clipboardData.getData("text/plain"))) {
      e.preventDefault();
      document.execCommand("insertText", false, text);
      isLocalChangeRef.current = true;
      const serialized = serializeEditorDom(editorRef.current!, singleLine);
      onChange(serialized);
      checkEmpty();
    }
  };

  // Confirm and commit formula from the popover
  const handleConfirmMath = () => {
    if (!activeMathNode || !editorRef.current) return;

    const validation = validateAndCleanFormula(dialogLatex);
    if (!validation.isComplete || !validation.cleanLatex) {
      setDialogError(
        validation.error ||
          "Công thức chưa hoàn thành hoặc cú pháp không hợp lệ. Vui lòng kiểm tra lại."
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

    // Return focus to editor and position caret in text node right after the confirmed formula node
    if (editorRef.current) {
      editorRef.current.focus();
    }
    const sel = window.getSelection();
    if (sel && editorRef.current) {
      let nextNode = element.nextSibling;
      if (!nextNode || nextNode.nodeType !== 3) {
        nextNode = document.createTextNode("");
        element.after(nextNode);
      }
      const range = document.createRange();
      range.setStart(nextNode, 0);
      range.setEnd(nextNode, 0);
      sel.removeAllRanges();
      sel.addRange(range);
    }

    setActiveMathNode(null);
    setDialogLatex("");
    setDialogError(null);

    isLocalChangeRef.current = true;
    const serialized = serializeEditorDom(editorRef.current, singleLine);
    onChange(serialized);
    checkEmpty();
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
    const serialized = serializeEditorDom(editorRef.current, singleLine);
    onChange(serialized);
    checkEmpty();
    if (editorRef.current) {
      editorRef.current.focus();
    }
  };

  // Cancel popover (removes uncommitted new node cleanly)
  const handleCancelMath = () => {
    if (!activeMathNode) return;
    if (activeMathNode.isNew) {
      activeMathNode.element.remove();
      if (editorRef.current) {
        isLocalChangeRef.current = true;
        onChange(serializeEditorDom(editorRef.current, singleLine));
        checkEmpty();
      }
    }
    setActiveMathNode(null);
    setDialogLatex("");
    setDialogError(null);
    if (editorRef.current) {
      editorRef.current.focus();
    }
  };

  const hasIncompleteFormulas = hasUnfilledPlaceholder(value);

  const isTeacher = variant === "teacher";
  const containerTokens: React.CSSProperties = isTeacher
    ? ({
        "--rme-border": "var(--th-border-subtle, #cbd5e1)",
        "--rme-border-focus": "var(--th-primary, #0d9488)",
        "--rme-surface": "var(--th-surface, #ffffff)",
        "--rme-surface-subtle": "var(--th-surface-subtle, #f8fafc)",
        "--rme-surface-toolbar": "var(--th-surface-ground, #e5edf5)",
        "--rme-text": "var(--th-text-primary, #0f172a)",
        "--rme-text-muted": "var(--th-text-secondary, #64748b)",
        "--rme-accent": "var(--th-primary, #0d9488)",
        "--rme-badge-bg": "rgba(13, 148, 136, 0.12)",
        "--rme-badge-border": "rgba(13, 148, 136, 0.3)",
        "--rme-badge-text": "var(--th-teal, #0d9488)",
        "--rme-math-bg": "rgba(13, 148, 136, 0.12)",
        "--rme-math-border": "rgba(13, 148, 136, 0.35)",
        "--rme-math-text": "var(--th-teal, #0f766e)",
      } as React.CSSProperties)
    : ({
        "--rme-border": "var(--cm-border-subtle, #334155)",
        "--rme-border-focus": "var(--cm-cyan, #06b6d4)",
        "--rme-surface": "var(--cm-surface-subtle, #0f172a)",
        "--rme-surface-subtle": "var(--cm-surface-subtle, #0f172a)",
        "--rme-surface-toolbar": "var(--cm-surface, rgba(0, 0, 0, 0.2))",
        "--rme-text": "var(--cm-text, #f8fafc)",
        "--rme-text-muted": "var(--cm-text-muted, #94a3b8)",
        "--rme-accent": "var(--cm-cyan, #06b6d4)",
        "--rme-badge-bg": "rgba(6, 182, 212, 0.1)",
        "--rme-badge-border": "rgba(6, 182, 212, 0.3)",
        "--rme-badge-text": "var(--cm-cyan, #22d3ee)",
        "--rme-math-bg": "rgba(6, 182, 212, 0.15)",
        "--rme-math-border": "rgba(6, 182, 212, 0.35)",
        "--rme-math-text": "var(--cm-cyan, #67e8f9)",
      } as React.CSSProperties);

  return (
    <div
      style={containerTokens}
      className={`rich-math-editor group flex flex-col rounded-xl border border-[var(--rme-border)] bg-[var(--rme-surface)] transition-all focus-within:border-[var(--rme-border-focus)] focus-within:ring-1 focus-within:ring-[var(--rme-border-focus)] ${className}`}
    >
      {/* Micro-toolbar */}
      <div className="flex items-center justify-between px-3 py-1.5 border-b border-[var(--rme-border)] bg-[var(--rme-surface-toolbar)] text-xs select-none">
        <div className="flex items-center gap-2">
          {label && (
            <span className="font-semibold text-[var(--rme-text-muted)] tracking-wider uppercase text-[11px]">
              {label}
            </span>
          )}
          <span
            className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-medium border"
            style={{
              backgroundColor: "var(--rme-badge-bg)",
              borderColor: "var(--rme-badge-border)",
              color: "var(--rme-badge-text)",
            }}
          >
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
              className="inline-flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-semibold border transition-colors cursor-pointer"
              style={{
                backgroundColor: "var(--rme-badge-bg)",
                borderColor: "var(--rme-badge-border)",
                color: "var(--rme-badge-text)",
              }}
              title="Chèn công thức toán tại con trỏ (hoặc gõ $ hoặc phím tắt Ctrl+M)"
            >
              <span>+ Σ</span>
              <span>Chèn công thức</span>
              <kbd className="hidden sm:inline-block ml-1 text-[10px] font-mono px-1 py-0.2 bg-black/20 rounded opacity-80">
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
            className="text-[11px] font-medium text-[var(--rme-text-muted)] hover:text-[var(--rme-text)] px-2 py-0.5 rounded hover:bg-white/5 transition-colors cursor-pointer"
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
            className={`rich-math-content-editable outline-none text-sm text-[var(--rme-text)] leading-relaxed select-text font-sans ${
              disabled ? "opacity-60 cursor-not-allowed" : "cursor-text"
            }`}
          />
        ) : (
          <textarea
            id={id}
            value={value}
            disabled={disabled}
            onChange={(e) => {
              const val = singleLine ? e.target.value.replace(/[\r\n]+/g, " ") : e.target.value;
              onChange(val);
            }}
            onKeyDown={(e) => {
              if (singleLine && e.key === "Enter") {
                e.preventDefault();
              }
            }}
            style={{ minHeight }}
            rows={singleLine ? 1 : 4}
            placeholder={placeholder}
            className="w-full bg-transparent outline-none font-mono text-xs text-[var(--rme-text)] leading-relaxed resize-y"
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
            className="fixed z-50 w-[380px] max-w-[90vw] rounded-2xl border border-cyan-500/50 bg-slate-900/95 shadow-2xl p-4 space-y-3 backdrop-blur-md text-slate-100 animate-scale-in"
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

            {/* Live KaTeX Preview and Placeholder / Syntax Indicator */}
            <div className="rounded-lg border border-slate-800 bg-slate-950/70 p-2 space-y-1">
              <div className="flex items-center justify-between text-[10px] text-slate-400">
                <span>Xem trước kết quả:</span>
                {dialogLatex ? (
                  (() => {
                    const validation = validateAndCleanFormula(dialogLatex);
                    if (validation.hasPlaceholder) {
                      return (
                        <span className="text-rose-400 font-semibold">
                          Chưa hoàn thành (\placeholder)
                        </span>
                      );
                    }
                    if (!validation.isComplete) {
                      return (
                        <span className="text-amber-400 font-semibold">
                          ⚠️ Lỗi cú pháp
                        </span>
                      );
                    }
                    return (
                      <span className="text-emerald-400 font-semibold">
                        ✓ Hợp lệ
                      </span>
                    );
                  })()
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
                    !validateAndCleanFormula(dialogLatex).isComplete
                  }
                  className={`px-3.5 py-1 text-xs font-semibold rounded-lg shadow-sm transition-all flex items-center gap-1.5 ${
                    !dialogLatex.trim() ||
                    !validateAndCleanFormula(dialogLatex).isComplete
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
