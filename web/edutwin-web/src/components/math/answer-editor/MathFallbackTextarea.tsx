import React from "react";

export interface MathFallbackTextareaProps {
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  readOnly?: boolean;
  placeholder?: string;
  className?: string;
  onRetry?: () => void;
  errorMessage?: string;
  onFocus?: () => void;
  onCommit?: () => void;
  onCancel?: () => void;
}

/**
 * MathFallbackTextarea
 * Graceful fallback component rendered when the dynamic MathLive chunk fails to load.
 * Preserves user edits in a standard monospace textarea and provides an explicit reload trigger.
 */
export const MathFallbackTextarea: React.FC<MathFallbackTextareaProps> = ({
  value,
  onChange,
  disabled = false,
  readOnly = false,
  placeholder = "Nhập biểu thức toán học hoặc đáp án...",
  className = "",
  onRetry,
  errorMessage = "Không thể tải trình gõ công thức trực quan MathLive. Bạn vẫn có thể nhập đáp án bên dưới hoặc thử tải lại.",
  onFocus,
  onCommit,
  onCancel,
}) => {
  return (
    <div className={`rounded-xl border border-amber-300 dark:border-amber-700 bg-amber-50/50 dark:bg-amber-950/30 p-3 space-y-2.5 ${className}`}>
      <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-amber-900 dark:text-amber-200">
        <span className="flex items-center gap-1.5 font-medium">
          <span aria-hidden="true">⚠</span>
          <span>{errorMessage}</span>
        </span>
        {onRetry && !disabled && !readOnly && (
          <button
            type="button"
            onClick={onRetry}
            className="px-2.5 py-1 text-xs font-semibold rounded bg-amber-200 dark:bg-amber-800 text-amber-900 dark:text-amber-100 hover:bg-amber-300 dark:hover:bg-amber-700 transition-colors shrink-0 cursor-pointer"
          >
            Thử tải lại
          </button>
        )}
      </div>

      <textarea
        value={value}
        onChange={(e) => onChange(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === "Enter" && !e.shiftKey && onCommit) {
            e.preventDefault();
            e.stopPropagation();
            onCommit();
            return;
          }
          if (e.key === "Escape" && onCancel) {
            e.preventDefault();
            e.stopPropagation();
            onCancel();
            return;
          }
        }}
        onFocus={onFocus}
        disabled={disabled}
        readOnly={readOnly}
        placeholder={placeholder}
        rows={2}
        className={`w-full p-2.5 text-sm font-mono rounded-lg border border-amber-300 dark:border-amber-600 bg-white dark:bg-slate-900 text-slate-900 dark:text-white outline-none focus:ring-2 focus:ring-amber-500/30 transition-all ${
          disabled || readOnly ? "cursor-default opacity-80" : ""
        }`}
      />
    </div>
  );
};
