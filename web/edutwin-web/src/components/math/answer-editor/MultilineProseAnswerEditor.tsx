import { useRef, useEffect, useLayoutEffect, useImperativeHandle, forwardRef } from "react";
import type { AnswerEditorValue, AnswerEditorRef } from "./answerEditorHelpers";
import { buildProseAnswer } from "./answerEditorHelpers";
import { RichMathEditor, type RichMathEditorProps, type RichMathEditorRef } from "../RichMathEditor";

export interface MultilineProseAnswerEditorProps {
  value: AnswerEditorValue;
  onChange?: (val: AnswerEditorValue) => void;
  disabled?: boolean;
  readOnly?: boolean;
  placeholder?: string;
  className?: string;
  autoFocus?: boolean;
  rows?: number;
  /** Retained for callers; formulas now render inline without a separate preview pane. */
  showPreview?: boolean;
  onFocus?: () => void;
  ariaLabel?: string;
  variant?: RichMathEditorProps["variant"];
}

/**
 * Shared visual prose answer editor for Essay + Manual and ShortAnswer + Manual.
 * The API contract remains rawText = prose + $latex$, displayLatex = "".
 * Reuses the reasoning editor's formula composer, placeholder guards and read-only source view.
 */
export const MultilineProseAnswerEditor = forwardRef<AnswerEditorRef, MultilineProseAnswerEditorProps>(
  (
    {
      value,
      onChange,
      disabled = false,
      readOnly = false,
      placeholder = "Nhập kết luận hoặc câu trả lời của bạn; dùng Chèn công thức để thêm biểu thức toán...",
      className = "",
      autoFocus = false,
      rows = 6,
      onFocus,
      ariaLabel = "Đáp án tự luận",
      variant = "student",
    },
    ref
  ) => {
    const richRef = useRef<RichMathEditorRef>(null);
    const latestValueRef = useRef<AnswerEditorValue>(buildProseAnswer(value.rawText));

    // External draft/question changes must be visible to getValue before the next user action.
    useLayoutEffect(() => {
      latestValueRef.current = buildProseAnswer(value.rawText);
    }, [value.rawText]);

    useEffect(() => {
      if (autoFocus && !disabled && !readOnly) richRef.current?.focus();
    }, [autoFocus, disabled, readOnly]);

    useImperativeHandle(ref, () => ({
      insertLatex: (latex: string) => {
        if (disabled || readOnly) return;
        richRef.current?.insertLatex(latex);
      },
      insertAtCursor: (latex: string) => {
        if (disabled || readOnly) return;
        richRef.current?.insertLatex(latex);
      },
      focus: () => richRef.current?.focus(),
      clear: () => {
        if (disabled || readOnly) return;
        // Clear both the DOM and serialized value, not just a potentially stale parent prop.
        richRef.current?.clear();
      },
      getValue: () => latestValueRef.current,
    }));

    return (
      <RichMathEditor
        ref={richRef}
        value={value.rawText ?? ""}
        onChange={(text) => {
          if (disabled || readOnly) return;
          const nextValue = buildProseAnswer(text);
          latestValueRef.current = nextValue;
          onChange?.(nextValue);
        }}
        onFocus={onFocus}
        disabled={disabled || readOnly}
        placeholder={placeholder}
        minHeight={`${Math.max(2, rows) * 24}px`}
        label={ariaLabel}
        variant={variant}
        className={className}
      />
    );
  }
);

MultilineProseAnswerEditor.displayName = "MultilineProseAnswerEditor";
