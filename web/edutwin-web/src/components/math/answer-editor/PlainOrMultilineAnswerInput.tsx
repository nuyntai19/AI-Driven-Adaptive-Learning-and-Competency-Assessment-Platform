import { useRef, useImperativeHandle, forwardRef } from "react";
import type { AnswerEditorValue, AnswerEditorRef } from "./answerEditorHelpers";
import { buildProseAnswer } from "./answerEditorHelpers";
import { RichMathText } from "../RichMathText";

export interface PlainOrMultilineAnswerInputProps {
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
 * PlainOrMultilineAnswerInput
 * Input component for ShortAnswer + Manual questions.
 * Enforces contract: rawText = prose/answer, displayLatex = "" (strictly empty).
 * Renders preview using rawText via RichMathText.
 */
export const PlainOrMultilineAnswerInput = forwardRef<
  AnswerEditorRef,
  PlainOrMultilineAnswerInputProps
>(
  (
    {
      value,
      onChange,
      disabled = false,
      readOnly = false,
      placeholder = "Nhập câu trả lời ngắn của bạn (có thể dùng $công_thức$)...",
      className = "",
      autoFocus = false,
      rows = 2,
      showPreview = true,
      onFocus,
      ariaLabel = "Câu trả lời ngắn",
    },
    ref
  ) => {
    const textareaRef = useRef<HTMLTextAreaElement>(null);

    useImperativeHandle(ref, () => ({
      insertLatex: (latex: string) => {
        if (disabled || readOnly || !textareaRef.current) return;
        const textarea = textareaRef.current;
        const start = textarea.selectionStart ?? textarea.value.length;
        const end = textarea.selectionEnd ?? textarea.value.length;
        const current = textarea.value;

        // Wrap in dollar signs if not already wrapped
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

    const hasMathSymbols = /\$.*?\$|\\[a-zA-Z]+|[_{^]/.test(value.rawText ?? "");

    return (
      <div className={`space-y-2 ${className}`}>
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
          aria-label={ariaLabel}
          className={`w-full p-3 text-sm rounded-xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-900 dark:text-white outline-none focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-500 transition-all resize-y ${
            disabled || readOnly ? "cursor-default opacity-80 select-text" : ""
          }`}
        />

        {showPreview && hasMathSymbols && value.rawText && (
          <div className="rounded-lg border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-900/60 p-2.5 text-xs text-slate-700 dark:text-slate-300">
            <span className="block font-semibold text-slate-400 mb-1">Xem trước hiển thị:</span>
            <RichMathText text={value.rawText} />
          </div>
        )}
      </div>
    );
  }
);

PlainOrMultilineAnswerInput.displayName = "PlainOrMultilineAnswerInput";
