import React, { useRef, useImperativeHandle, forwardRef } from "react";
import type {
  AnswerEditorProfile,
  AnswerEditorValue,
  AnswerEditorRef,
  QuestionType,
  QuestionAnswerEvaluationMode,
} from "./answerEditorHelpers";
import {
  resolveAnswerInputType,
  resolveReadonlyDisplay,
} from "./answerEditorHelpers";
import { PlainTextAnswerInput } from "./PlainTextAnswerInput";
import { NumericRationalMathInput } from "./NumericRationalMathInput";
import { Coordinate2DInput } from "./Coordinate2DInput";
import { PlainOrMultilineAnswerInput } from "./PlainOrMultilineAnswerInput";
import { MultilineProseAnswerEditor } from "./MultilineProseAnswerEditor";
import { MathPreviewCore } from "../MathPreviewCore";

export interface ModeAwareAnswerEditorProps {
  profile?: AnswerEditorProfile;
  questionType: QuestionType | string;
  evaluationMode: QuestionAnswerEvaluationMode | string;
  value: AnswerEditorValue;
  onChange?: (val: AnswerEditorValue) => void;
  disabled?: boolean;
  readOnly?: boolean;
  placeholder?: string;
  className?: string;
  autoFocus?: boolean;
  validationError?: string | null;
  ariaLabel?: string;
  showPreview?: boolean;
  showSyntaxHint?: boolean;
  onFocus?: () => void;
}

/**
 * SafeErrorState
 * Fail-closed fallback UI rendered when an invalid combination of QuestionType and EvaluationMode is detected.
 * Strictly prevents silent plain-text fallback and data corruption.
 */
export const SafeErrorState: React.FC<{
  errorMessage?: string;
  questionType: string;
  evaluationMode: string;
}> = ({ errorMessage, questionType, evaluationMode }) => (
  <div
    role="alert"
    className="rounded-xl border border-rose-300 dark:border-rose-800 bg-rose-50 dark:bg-rose-950/40 p-4 text-rose-900 dark:text-rose-200 text-xs space-y-1.5"
  >
    <div className="flex items-center gap-2 font-bold text-rose-700 dark:text-rose-300">
      <span aria-hidden="true" className="text-sm">🚫</span>
      <span>Cấu hình chế độ đánh giá không hợp lệ (Fail-Closed)</span>
    </div>
    <p>
      {errorMessage ||
        `Tổ hợp loại câu hỏi '${questionType}' và chế độ chấm '${evaluationMode}' không hợp lệ.`}
    </p>
    <div className="text-[11px] text-rose-600 dark:text-rose-400 font-mono">
      Type: {questionType} | Mode: {evaluationMode}
    </div>
  </div>
);

/**
 * ModeAwareAnswerEditor
 * Central orchestrator for all math & text answer editing across Student, Teacher, and CenterManager.
 * Enforces strict fail-closed compatibility matrix, delegates to specialized sub-inputs,
 * and maintains complete ref capability for Casio/SideAssistant tools.
 */
export const ModeAwareAnswerEditor = forwardRef<AnswerEditorRef, ModeAwareAnswerEditorProps>(
  (
    {
      profile = "answering",
      questionType,
      evaluationMode,
      value,
      onChange,
      disabled = false,
      readOnly = false,
      placeholder,
      className = "",
      autoFocus = false,
      validationError = null,
      ariaLabel,
      showPreview = true,
      showSyntaxHint,
      onFocus,
    },
    ref
  ) => {
    const childRef = useRef<AnswerEditorRef>(null);

    // 1. Resolve compatible input type
    const resolution = resolveAnswerInputType(questionType, evaluationMode);

    // 2. Delegate imperative ref methods to active child component
    useImperativeHandle(ref, () => ({
      insertLatex: (latex: string) => {
        if (disabled || readOnly) return;
        childRef.current?.insertLatex(latex);
      },
      insertAtCursor: (latex: string) => {
        if (disabled || readOnly) return;
        if (childRef.current?.insertAtCursor) {
          childRef.current.insertAtCursor(latex);
        } else {
          childRef.current?.insertLatex(latex);
        }
      },
      focus: () => {
        childRef.current?.focus();
      },
      clear: () => {
        if (disabled || readOnly) return;
        childRef.current?.clear();
      },
      getValue: () => {
        return childRef.current?.getValue() ?? value;
      },
    }));

    // 3. Fail-Closed guard for invalid combinations
    if (!resolution.isValid) {
      return (
        <SafeErrorState
          errorMessage={resolution.errorMessage}
          questionType={String(questionType)}
          evaluationMode={String(evaluationMode)}
        />
      );
    }

    // 4. MultipleChoice delegation: ModeAwareAnswerEditor delegates MCQ to OptionSelector for both editing and readonly
    // Never renders radios and never exposes internal optionId in readonly mode
    if (resolution.type === "option-selector") {
      if (profile === "readonly" || readOnly) {
        return (
          <div
            className="rounded-xl border border-dashed border-slate-300 dark:border-slate-700 bg-slate-50/50 dark:bg-slate-900/40 p-3 text-xs text-slate-600 dark:text-slate-400 flex items-center gap-2"
            role="region"
            aria-label="Phương án trắc nghiệm chỉ đọc"
          >
            <span aria-hidden="true">🔘</span>
            <span>Phương án trắc nghiệm được hiển thị bởi bộ xem lại phương án chuyên biệt (OptionSelector).</span>
          </div>
        );
      }

      return (
        <div
          className="rounded-xl border border-dashed border-indigo-300 dark:border-indigo-800 bg-indigo-50/30 dark:bg-indigo-950/20 p-3 text-xs text-indigo-800 dark:text-indigo-300 flex items-center gap-2"
          role="region"
          aria-label="Khu vực lựa chọn trắc nghiệm"
        >
          <span aria-hidden="true">🔘</span>
          <span>
            Câu hỏi trắc nghiệm: Học sinh hoặc người soạn chọn trực tiếp trong danh sách các phương án (OptionSelector).
          </span>
        </div>
      );
    }

    // 5. Handle Readonly profile for ShortAnswer and Essay
    if (profile === "readonly" || readOnly) {
      const display = resolveReadonlyDisplay(evaluationMode, value, questionType);
      return (
        <div className={`space-y-1.5 ${className}`}>
          <div className="rounded-xl border border-slate-200 dark:border-slate-800 bg-slate-50/70 dark:bg-slate-900/50 p-3 select-text">
            {display.content ? (
              <MathPreviewCore
                formula={display.mode === "formula" ? display.content : undefined}
                content={display.mode === "rich" ? display.content : undefined}
                mode={display.mode}
                displayMode={display.mode === "formula"}
              />
            ) : (
              <span className="text-xs text-slate-400 italic">Chưa có câu trả lời</span>
            )}
          </div>
        </div>
      );
    }

    // 6. Child change handler
    const handleChildChange = (nextVal: AnswerEditorValue) => {
      onChange?.(nextVal);
    };

    return (
      <div className={`space-y-2 ${className}`}>
        {/* Validation Error Alert */}
        {validationError && (
          <div
            role="alert"
            className="flex items-center gap-2 p-2.5 rounded-lg bg-rose-50 dark:bg-rose-950/50 border border-rose-200 dark:border-rose-800 text-xs text-rose-700 dark:text-rose-300"
          >
            <span aria-hidden="true">⚠</span>
            <span>{validationError}</span>
          </div>
        )}

        {/* Specialized Active Sub-Input */}
        {resolution.type === "plain-text" && (
          <PlainTextAnswerInput
            ref={childRef}
            value={value}
            onChange={handleChildChange}
            disabled={disabled}
            readOnly={readOnly}
            placeholder={placeholder}
            autoFocus={autoFocus}
            onFocus={onFocus}
            ariaLabel={ariaLabel}
          />
        )}

        {resolution.type === "numeric-rational" && (
          <NumericRationalMathInput
            ref={childRef}
            value={value}
            onChange={handleChildChange}
            disabled={disabled}
            readOnly={readOnly}
            placeholder={placeholder}
            autoFocus={autoFocus}
            showPreview={showPreview}
            showSyntaxHint={showSyntaxHint ?? profile === "authoring"}
            onFocus={onFocus}
            ariaLabel={ariaLabel}
          />
        )}

        {resolution.type === "coordinate-2d" && (
          <Coordinate2DInput
            ref={childRef}
            value={value}
            onChange={handleChildChange}
            disabled={disabled}
            readOnly={readOnly}
            autoFocus={autoFocus}
            showPreview={showPreview}
            showSyntaxHint={showSyntaxHint ?? profile === "authoring"}
            onFocus={onFocus}
            ariaLabel={ariaLabel}
          />
        )}

        {resolution.type === "manual-short-answer" && (
          <PlainOrMultilineAnswerInput
            ref={childRef}
            value={value}
            onChange={handleChildChange}
            disabled={disabled}
            readOnly={readOnly}
            placeholder={placeholder}
            autoFocus={autoFocus}
            showPreview={showPreview}
            onFocus={onFocus}
            ariaLabel={ariaLabel}
          />
        )}

        {resolution.type === "essay-prose" && (
          <MultilineProseAnswerEditor
            ref={childRef}
            value={value}
            onChange={handleChildChange}
            disabled={disabled}
            readOnly={readOnly}
            placeholder={placeholder}
            autoFocus={autoFocus}
            showPreview={showPreview}
            onFocus={onFocus}
            ariaLabel={ariaLabel}
          />
        )}
      </div>
    );
  }
);

ModeAwareAnswerEditor.displayName = "ModeAwareAnswerEditor";
