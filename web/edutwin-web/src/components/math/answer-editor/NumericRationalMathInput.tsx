import { useRef, useEffect, useImperativeHandle, forwardRef } from "react";
import type { AnswerEditorValue, AnswerEditorRef } from "./answerEditorHelpers";
import { getAnswerSyntaxHint, buildNumericRationalAnswer } from "./answerEditorHelpers";
import { VisualMathField, type VisualMathFieldRef } from "../VisualMathField";
import { MathFormulaPreview } from "../MathFormulaPreview";

export interface NumericRationalMathInputProps {
  value: AnswerEditorValue;
  onChange?: (val: AnswerEditorValue) => void;
  disabled?: boolean;
  readOnly?: boolean;
  placeholder?: string;
  className?: string;
  autoFocus?: boolean;
  showPreview?: boolean;
  showSyntaxHint?: boolean;
  onFocus?: () => void;
  ariaLabel?: string;
}

/**
 * NumericRationalMathInput
 * Dedicated math input for ShortAnswer + NumericRational questions.
 * Leverages VisualMathField (MathLive) with state preservation, dynamic chunk fallback,
 * live KaTeX preview, and imperative value freshness via latestValueRef.
 */
export const NumericRationalMathInput = forwardRef<AnswerEditorRef, NumericRationalMathInputProps>(
  (
    {
      value,
      onChange,
      disabled = false,
      readOnly = false,
      placeholder = "Nhập số nguyên, số thập phân hoặc phân số (ví dụ: -3, 0.75, 3/4)...",
      className = "",
      autoFocus = false,
      showPreview = false,
      showSyntaxHint = true,
      onFocus,
      ariaLabel = "Ô nhập số hoặc phân số",
    },
    ref
  ) => {
    const visualRef = useRef<VisualMathFieldRef>(null);
    const latestValueRef = useRef<AnswerEditorValue>(value);

    // Keep latestValueRef in sync when external prop changes
    useEffect(() => {
      latestValueRef.current = value;
    }, [value]);

    const syntaxHint = showSyntaxHint ? getAnswerSyntaxHint("NumericRational", value) : null;

    useImperativeHandle(ref, () => ({
      insertLatex: (latex: string) => {
        if (disabled || readOnly) return;
        visualRef.current?.insertAtCursor(latex);
      },
      insertAtCursor: (latex: string) => {
        if (disabled || readOnly) return;
        visualRef.current?.insertAtCursor(latex);
      },
      focus: () => {
        visualRef.current?.focus();
      },
      clear: () => {
        if (disabled || readOnly) return;
        visualRef.current?.clear();
        latestValueRef.current = buildNumericRationalAnswer("", "");
        onChange?.(latestValueRef.current);
      },
      getValue: () => {
        // Return latestValueRef ensuring fresh plain rawText and displayLatex
        return latestValueRef.current;
      },
    }));

    const mathValue = value.displayLatex || value.rawText || "";

    return (
      <div className={`space-y-2 ${className}`} aria-label={ariaLabel}>
        <VisualMathField
          ref={visualRef}
          value={mathValue}
          onChange={(latex, plainText) => {
            const nextVal = buildNumericRationalAnswer(plainText, latex);
            latestValueRef.current = nextVal;
            onChange?.(nextVal);
          }}
          onFocus={onFocus}
          placeholder={placeholder}
          disabled={disabled || readOnly}
          autoFocus={autoFocus}
        />

        {syntaxHint && !disabled && !readOnly && (
          <div className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800 text-xs text-amber-800 dark:text-amber-200">
            <span aria-hidden="true">💡</span>
            <span>{syntaxHint}</span>
          </div>
        )}

        {showPreview && value.displayLatex && (
          <MathFormulaPreview
            formula={value.displayLatex}
            label="Xem trước KaTeX"
            className="text-xs"
          />
        )}
      </div>
    );
  }
);

NumericRationalMathInput.displayName = "NumericRationalMathInput";
