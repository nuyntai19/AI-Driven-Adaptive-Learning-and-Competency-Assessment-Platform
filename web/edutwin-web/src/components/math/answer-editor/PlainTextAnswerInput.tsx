import { useRef, useImperativeHandle, forwardRef } from "react";
import type { AnswerEditorValue, AnswerEditorRef } from "./answerEditorHelpers";
import { buildTextExactAnswer } from "./answerEditorHelpers";

export interface PlainTextAnswerInputProps {
  value: AnswerEditorValue;
  onChange?: (val: AnswerEditorValue) => void;
  disabled?: boolean;
  readOnly?: boolean;
  placeholder?: string;
  className?: string;
  autoFocus?: boolean;
  onFocus?: () => void;
  ariaLabel?: string;
}

/**
 * PlainTextAnswerInput
 * Standard HTML input for ShortAnswer + TextExact questions.
 * Enforces contract: rawText = user input, displayLatex = "" (strictly empty).
 */
export const PlainTextAnswerInput = forwardRef<AnswerEditorRef, PlainTextAnswerInputProps>(
  (
    {
      value,
      onChange,
      disabled = false,
      readOnly = false,
      placeholder = "Nhập câu trả lời chính xác...",
      className = "",
      autoFocus = false,
      onFocus,
      ariaLabel = "Câu trả lời dạng văn bản",
    },
    ref
  ) => {
    const inputRef = useRef<HTMLInputElement>(null);

    useImperativeHandle(ref, () => ({
      insertLatex: (text: string) => {
        if (disabled || readOnly || !inputRef.current) return;
        const input = inputRef.current;
        const start = input.selectionStart ?? input.value.length;
        const end = input.selectionEnd ?? input.value.length;
        const current = input.value;
        const next = current.substring(0, start) + text + current.substring(end);
        onChange?.(buildTextExactAnswer(next));
      },
      insertAtCursor: (text: string) => {
        if (disabled || readOnly || !inputRef.current) return;
        const input = inputRef.current;
        const start = input.selectionStart ?? input.value.length;
        const end = input.selectionEnd ?? input.value.length;
        const current = input.value;
        const next = current.substring(0, start) + text + current.substring(end);
        onChange?.(buildTextExactAnswer(next));
      },
      focus: () => {
        inputRef.current?.focus();
      },
      clear: () => {
        if (disabled || readOnly) return;
        onChange?.(buildTextExactAnswer(""));
      },
      getValue: () => {
        return buildTextExactAnswer(inputRef.current?.value ?? value.rawText ?? "");
      },
    }));

    return (
      <input
        ref={inputRef}
        type="text"
        value={value.rawText}
        onChange={(e) => {
          onChange?.(buildTextExactAnswer(e.target.value));
        }}
        onFocus={onFocus}
        disabled={disabled}
        readOnly={readOnly}
        placeholder={placeholder}
        autoFocus={autoFocus}
        aria-label={ariaLabel}
        className={`w-full p-3 text-sm font-medium rounded-xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-900 dark:text-white outline-none focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-500 transition-all ${
          disabled || readOnly ? "cursor-default opacity-80 select-text" : ""
        } ${className}`}
      />
    );
  }
);

PlainTextAnswerInput.displayName = "PlainTextAnswerInput";
