import { forwardRef } from "react";
import type { AnswerEditorRef } from "./answerEditorHelpers";
import { MultilineProseAnswerEditor, type MultilineProseAnswerEditorProps } from "./MultilineProseAnswerEditor";

export type PlainOrMultilineAnswerInputProps = MultilineProseAnswerEditorProps;

/** ShortAnswer + Manual uses the same visual composer and prose payload contract as Essay. */
export const PlainOrMultilineAnswerInput = forwardRef<AnswerEditorRef, PlainOrMultilineAnswerInputProps>(
  (
    {
      rows = 2,
      placeholder = "Nhập câu trả lời ngắn; dùng Chèn công thức để thêm biến hoặc biểu thức toán...",
      ariaLabel = "Câu trả lời ngắn",
      ...props
    },
    ref
  ) => (
    <MultilineProseAnswerEditor
      {...props}
      ref={ref}
      rows={rows}
      placeholder={placeholder}
      ariaLabel={ariaLabel}
    />
  )
);

PlainOrMultilineAnswerInput.displayName = "PlainOrMultilineAnswerInput";
