import { useRef, useState, useImperativeHandle, forwardRef } from "react";
import type { AnswerEditorValue, AnswerEditorRef } from "./answerEditorHelpers";
import { buildProseAnswer } from "./answerEditorHelpers";
import { RichMathText } from "../RichMathText";

export interface MultilineProseAnswerEditorProps {
  value: AnswerEditorValue;
  onChange?: (val: AnswerEditorValue) => void;
  disabled?: boolean;
  readOnly?: boolean;
  placeholder?: string;
  className?: string;
  autoFocus?: boolean;
  rows?: number;
  showPreview?: boolean;
  onFocus?: () => void;
  ariaLabel?: string;
}

/**
 * MultilineProseAnswerEditor
 * Dedicated editor for Essay + Manual questions.
 * Enforces contract: rawText = main essay prose (finalAnswer), displayLatex = "" (strictly empty).
 * Manages the primary essay prose answer with full support for inline formulas ($...$).
 * Preview utilizes rawText via RichMathText.
 */
export const MultilineProseAnswerEditor = forwardRef<
  AnswerEditorRef,
  MultilineProseAnswerEditorProps
>(
  (
    {
      value,
      onChange,
      disabled = false,
      readOnly = false,
      placeholder = "Trình bày bài giải chi tiết của bạn tại đây... Sử dụng $công_thức$ để viết công thức toán học (ví dụ: $x^2 + 2x + 1 = 0$).",
      className = "",
      autoFocus = false,
      rows = 6,
      showPreview = true,
      onFocus,
      ariaLabel = "Bài làm tự luận",
    },
    ref
  ) => {
    const textareaRef = useRef<HTMLTextAreaElement>(null);
    const [previewTab, setPreviewTab] = useState<"write" | "split" | "preview">("write");

    useImperativeHandle(ref, () => ({
      insertLatex: (latex: string) => {
        if (disabled || readOnly || !textareaRef.current) return;
        const textarea = textareaRef.current;
        const start = textarea.selectionStart ?? textarea.value.length;
        const end = textarea.selectionEnd ?? textarea.value.length;
        const current = textarea.value;

        // Ensure formula is wrapped in inline math dollars
        const formatted = latex.startsWith("$") && latex.endsWith("$") ? latex : `$${latex}$`;
        const next = current.substring(0, start) + formatted + current.substring(end);

        onChange?.(buildProseAnswer(next));
      },
      focus: () => {
        textareaRef.current?.focus();
      },
      clear: () => {
        if (disabled || readOnly) return;
        onChange?.(buildProseAnswer(""));
      },
      getValue: () => {
        return buildProseAnswer(textareaRef.current?.value ?? value.rawText ?? "");
      },
    }));

    const hasMath = /\$.*?\$|\\[a-zA-Z]+|[_{^]/.test(value.rawText ?? "");

    return (
      <div
        className={`rounded-2xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 overflow-hidden shadow-sm transition-all focus-within:ring-2 focus-within:ring-indigo-500/20 focus-within:border-indigo-500 ${className}`}
        aria-label={ariaLabel}
      >
        {/* Editor Toolbar Header */}
        <div className="flex items-center justify-between px-3 py-2 border-b border-slate-200 dark:border-slate-800 bg-slate-50/75 dark:bg-slate-800/50 text-xs">
          <div className="flex items-center gap-2 text-slate-600 dark:text-slate-300 font-semibold">
            <span>✍ Tự luận & Trình bày</span>
            {hasMath && (
              <span className="px-1.5 py-0.5 rounded text-[10px] bg-indigo-100 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300 font-mono">
                $ Toán inline
              </span>
            )}
          </div>

          {showPreview && !disabled && !readOnly && (
            <div className="flex items-center bg-slate-200/80 dark:bg-slate-700/80 rounded-lg p-0.5">
              <button
                type="button"
                onClick={() => setPreviewTab("write")}
                className={`px-2 py-0.5 rounded text-xs font-medium transition-colors ${
                  previewTab === "write"
                    ? "bg-white dark:bg-slate-900 text-indigo-600 dark:text-indigo-300 shadow-sm"
                    : "text-slate-600 dark:text-slate-300 hover:text-slate-900"
                }`}
              >
                Soạn thảo
              </button>
              <button
                type="button"
                onClick={() => setPreviewTab("split")}
                className={`px-2 py-0.5 rounded text-xs font-medium transition-colors ${
                  previewTab === "split"
                    ? "bg-white dark:bg-slate-900 text-indigo-600 dark:text-indigo-300 shadow-sm"
                    : "text-slate-600 dark:text-slate-300 hover:text-slate-900"
                }`}
              >
                Song song
              </button>
              <button
                type="button"
                onClick={() => setPreviewTab("preview")}
                className={`px-2 py-0.5 rounded text-xs font-medium transition-colors ${
                  previewTab === "preview"
                    ? "bg-white dark:bg-slate-900 text-indigo-600 dark:text-indigo-300 shadow-sm"
                    : "text-slate-600 dark:text-slate-300 hover:text-slate-900"
                }`}
              >
                Xem trước
              </button>
            </div>
          )}
        </div>

        {/* Content Body */}
        <div className={`grid ${previewTab === "split" ? "grid-cols-1 md:grid-cols-2 divide-y md:divide-y-0 md:divide-x divide-slate-200 dark:divide-slate-800" : "grid-cols-1"}`}>
          {/* Write Pane */}
          {previewTab !== "preview" && (
            <textarea
              ref={textareaRef}
              value={value.rawText}
              onChange={(e) => {
                onChange?.(buildProseAnswer(e.target.value));
              }}
              onFocus={onFocus}
              disabled={disabled}
              readOnly={readOnly}
              rows={rows}
              placeholder={placeholder}
              autoFocus={autoFocus}
              className={`w-full p-3.5 text-sm leading-relaxed text-slate-900 dark:text-white bg-transparent outline-none resize-y transition-colors font-sans ${
                disabled || readOnly ? "cursor-default opacity-80 select-text" : ""
              }`}
            />
          )}

          {/* Preview Pane */}
          {(previewTab === "preview" || previewTab === "split") && (
            <div className="p-3.5 min-h-[140px] bg-slate-50/50 dark:bg-slate-900/40 text-sm leading-relaxed overflow-y-auto max-h-[400px]">
              {value.rawText?.trim() ? (
                <RichMathText text={value.rawText} />
              ) : (
                <div className="text-slate-400 italic text-xs">
                  Nội dung xem trước sẽ hiển thị tại đây khi bạn nhập bài làm...
                </div>
              )}
            </div>
          )}
        </div>

        {/* Word / Char Counter */}
        <div className="flex items-center justify-between px-3 py-1.5 border-t border-slate-100 dark:border-slate-800 bg-slate-50/30 dark:bg-slate-950/20 text-[11px] text-slate-400">
          <span>Hỗ trợ định dạng KaTeX: dùng dấu $...$ bao quanh công thức</span>
          <span>{value.rawText?.length ?? 0} ký tự</span>
        </div>
      </div>
    );
  }
);

MultilineProseAnswerEditor.displayName = "MultilineProseAnswerEditor";
