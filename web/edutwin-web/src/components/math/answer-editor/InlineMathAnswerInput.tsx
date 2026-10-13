import { forwardRef, useEffect, useImperativeHandle, useLayoutEffect, useRef } from "react";
import { RichMathEditor, type RichMathEditorRef, type RichMathEditorProps } from "../RichMathEditor";
import { buildInlineMathAnswer, inlineMathEditingText, type AnswerEditorRef, type AnswerEditorValue } from "./answerEditorHelpers";

interface Props {
  value: AnswerEditorValue; onChange?: (value: AnswerEditorValue) => void;
  disabled?: boolean; readOnly?: boolean; autoFocus?: boolean;
  placeholder?: string; onFocus?: () => void; ariaLabel?: string;
  variant?: RichMathEditorProps["variant"];
}
/** Authoring and answering share the formula button, not a permanent MathLive field. */
export const InlineMathAnswerInput = forwardRef<AnswerEditorRef, Props>(({
  value, onChange, disabled = false, readOnly = false, autoFocus = false,
  placeholder = "Nhập đáp án; chọn Chèn công thức khi cần phân số, căn, tọa độ hoặc biểu thức.", onFocus, ariaLabel = "Đáp án của bạn",
  variant = "student",
}, ref) => {
  const editor = useRef<RichMathEditorRef>(null);
  const latest = useRef(value);
  useLayoutEffect(() => { latest.current = value; }, [value]);
  useEffect(() => { if (autoFocus && !disabled && !readOnly) editor.current?.focus(); }, [autoFocus, disabled, readOnly]);
  useImperativeHandle(ref, () => ({
    insertLatex: latex => { if (!disabled && !readOnly) editor.current?.insertLatex(latex); },
    insertAtCursor: latex => { if (!disabled && !readOnly) editor.current?.insertLatex(latex); },
    focus: () => editor.current?.focus(),
    clear: () => { if (!disabled && !readOnly) editor.current?.clear(); },
    getValue: () => latest.current,
  }));
  return <RichMathEditor ref={editor} value={inlineMathEditingText(value)} variant={variant} disabled={disabled || readOnly}
    label={ariaLabel} minHeight="96px" placeholder={placeholder} onFocus={onFocus}
    onChange={text => { if (disabled || readOnly) return; const next = buildInlineMathAnswer(text); latest.current = next; onChange?.(next); }} />;
});
InlineMathAnswerInput.displayName = "InlineMathAnswerInput";
