import { useEffect, useRef, useImperativeHandle, forwardRef, useState } from "react";
import "mathlive";

export interface VisualMathFieldRef {
  insertAtCursor: (latex: string) => void;
  focus: () => void;
  clear: () => void;
  getValue: () => string;
  setValue: (latex: string) => void;
}

export interface VisualMathFieldProps {
  value: string;
  onChange: (value: string) => void;
  onFocus?: () => void;
  placeholder?: string;
  disabled?: boolean;
  className?: string;
  autoFocus?: boolean;
}

/**
 * VisualMathField: WYSIWYG mathematical expression input field using MathLive.
 * Renders actual math symbols (radicals, fractions, exponents) with interactive [?] placeholders.
 * Isolates keyboard navigation (arrows, Tab, Enter) to prevent quiz page hijacking.
 */
export const VisualMathField = forwardRef<VisualMathFieldRef, VisualMathFieldProps>(
  (
    {
      value,
      onChange,
      onFocus,
      placeholder = "Nhấp vào đây để nhập công thức hoặc chọn ký hiệu...",
      disabled = false,
      className = "",
      autoFocus = false,
    },
    ref
  ) => {
    const containerRef = useRef<HTMLDivElement>(null);
    const mathfieldRef = useRef<any>(null);
    const [isReady, setIsReady] = useState(false);
    const lastEmittedValueRef = useRef<string>(value);

    const onFocusRef = useRef(onFocus);
    onFocusRef.current = onFocus;

    // Initialize Mathfield element inside container
    useEffect(() => {
      if (!containerRef.current) return;

      // Create <math-field> instance
      const mf = document.createElement("math-field") as any;
      mf.style.width = "100%";
      mf.style.minHeight = "46px";
      mf.style.fontSize = "1.3rem";
      mf.style.outline = "none";
      mf.style.border = "none";
      mf.style.backgroundColor = "transparent";
      mf.style.display = "block";
      mf.style.padding = "6px 8px";
      mf.style.color = "inherit";
      mf.style.setProperty("--color", "currentColor");
      mf.style.setProperty("--placeholder-color", "#818cf8");
      mf.style.setProperty("--caret-color", "#6366f1");
      mf.style.setProperty("--selection-background-color", "rgba(99, 102, 241, 0.25)");

      // Configure MathLive settings
      mf.mathVirtualKeyboardPolicy = "manual"; // Prevent unwanted mobile popups on desktop
      mf.smartFence = true;
      mf.smartSuperscript = true;

      if (disabled) {
        mf.readOnly = true;
        mf.setAttribute("readonly", "true");
        mf.setAttribute("disabled", "true");
        mf.tabIndex = -1;
      }

      if (value) {
        mf.setValue(value, { silenceNotifications: true });
        lastEmittedValueRef.current = value;
      }

      // Handle user typing / input
      const handleInput = () => {
        if (disabled || mf.readOnly) return;
        const currentLatex = mf.getValue ? mf.getValue("latex-expanded") : mf.value;
        lastEmittedValueRef.current = currentLatex;
        onChange(currentLatex);
      };

      const handleFocus = () => {
        onFocusRef.current?.();
      };

      // Keyboard Event Isolation: Prevent arrow keys, Tab, Enter, Space from bubbling up to quiz page
      const handleKeyDown = (e: KeyboardEvent) => {
        e.stopPropagation();

        if (disabled || mf.readOnly) {
          // In read-only mode, only allow copy (Ctrl+C / Cmd+C) and navigation arrow keys
          const isCopyOrNav =
            (e.ctrlKey || e.metaKey) && (e.key === "c" || e.key === "C" || e.key === "a" || e.key === "A");
          const isArrow = e.key.startsWith("Arrow");
          if (!isCopyOrNav && !isArrow) {
            e.preventDefault();
            return;
          }
        }
      };

      mf.addEventListener("input", handleInput);
      mf.addEventListener("keydown", handleKeyDown);
      mf.addEventListener("focus", handleFocus);
      mf.addEventListener("pointerdown", handleFocus);

      containerRef.current.innerHTML = "";
      containerRef.current.appendChild(mf);
      mathfieldRef.current = mf;
      setIsReady(true);

      if (autoFocus && !disabled) {
        setTimeout(() => mf.focus(), 150);
      }

      return () => {
        mf.removeEventListener("input", handleInput);
        mf.removeEventListener("keydown", handleKeyDown);
        mf.removeEventListener("focus", handleFocus);
        mf.removeEventListener("pointerdown", handleFocus);
        if (containerRef.current) {
          containerRef.current.innerHTML = "";
        }
        mathfieldRef.current = null;
      };
    }, []);

    // Sync external value updates if changed from outside (e.g. reset or clear)
    useEffect(() => {
      if (!isReady || !mathfieldRef.current) return;
      const currentVal = mathfieldRef.current.getValue ? mathfieldRef.current.getValue("latex-expanded") : mathfieldRef.current.value;
      if (value !== currentVal && value !== lastEmittedValueRef.current) {
        mathfieldRef.current.setValue(value || "", { silenceNotifications: true });
        lastEmittedValueRef.current = value;
      }
    }, [value, isReady]);

    // Sync disabled state
    useEffect(() => {
      if (!mathfieldRef.current) return;
      mathfieldRef.current.readOnly = Boolean(disabled);
      if (disabled) {
        mathfieldRef.current.setAttribute("readonly", "true");
        mathfieldRef.current.setAttribute("disabled", "true");
        mathfieldRef.current.tabIndex = -1;
      } else {
        mathfieldRef.current.removeAttribute("readonly");
        mathfieldRef.current.removeAttribute("disabled");
        mathfieldRef.current.removeAttribute("tabindex");
      }
    }, [disabled]);

    // Expose ref methods for toolbar and Casio calculator insertion
    useImperativeHandle(ref, () => ({
      insertAtCursor: (latexOrText: string) => {
        if (disabled) return;
        const mf = mathfieldRef.current;
        if (!mf || mf.readOnly) return;
        mf.focus();
        if (typeof mf.insert === "function") {
          mf.insert(latexOrText, {
            mode: "math",
            selectionMode: "placeholder",
            focus: true,
          });
        } else {
          mf.executeCommand(["insert", latexOrText]);
        }
        const newVal = mf.getValue ? mf.getValue("latex-expanded") : mf.value;
        lastEmittedValueRef.current = newVal;
        onChange(newVal);
      },
      focus: () => {
        if (!disabled) {
          mathfieldRef.current?.focus();
        }
      },
      clear: () => {
        if (disabled || !mathfieldRef.current) return;
        mathfieldRef.current.setValue("", { silenceNotifications: true });
        lastEmittedValueRef.current = "";
        onChange("");
      },
      getValue: () => {
        if (!mathfieldRef.current) return "";
        return mathfieldRef.current.getValue ? mathfieldRef.current.getValue("latex-expanded") : mathfieldRef.current.value;
      },
      setValue: (latex: string) => {
        if (disabled || !mathfieldRef.current) return;
        mathfieldRef.current.setValue(latex, { silenceNotifications: true });
        lastEmittedValueRef.current = latex;
        onChange(latex);
      },
    }));

    return (
      <div
        className={`relative rounded-2xl border transition-all ${
          disabled
            ? "border-slate-200 dark:border-slate-800 bg-slate-50/70 dark:bg-slate-900/60 shadow-none text-slate-800 dark:text-slate-200"
            : "border-2 border-indigo-200/80 bg-white dark:bg-slate-900 focus-within:border-indigo-600 focus-within:ring-4 focus-within:ring-indigo-500/15 shadow-sm text-slate-900 dark:text-white"
        } ${className}`}
      >
        {/* Helper guide */}
        <div
          className={`flex items-center justify-between px-3.5 py-2 border-b text-xs select-none rounded-t-2xl ${
            disabled
              ? "border-slate-200 dark:border-slate-800 bg-slate-100/70 dark:bg-slate-800/60 text-slate-600 dark:text-slate-400"
              : "border-slate-100 dark:border-slate-800 bg-indigo-50/50 dark:bg-indigo-950/40 text-slate-700 dark:text-slate-300"
          }`}
        >
          {disabled ? (
            <div className="flex items-center gap-2">
              <span className="text-sm leading-none">🔒</span>
              <span className="font-extrabold text-emerald-800 dark:text-emerald-300">
                Đáp án đã nộp · Chế độ chỉ đọc
              </span>
            </div>
          ) : (
            <div className="flex items-center gap-2">
              <span className="inline-block w-2.5 h-2.5 rounded-full bg-indigo-600 animate-pulse" />
              <span className="font-semibold text-indigo-950 dark:text-indigo-200">Trình gõ toán trực quan</span>
              <span className="hidden sm:inline text-slate-500 dark:text-slate-400">
                · Dùng phím mũi tên hoặc Tab để di chuyển giữa các ô{" "}
                <strong className="text-indigo-600 dark:text-indigo-400 font-bold">[?]</strong>
              </span>
            </div>
          )}
          {value.trim() && !disabled && (
            <button
              type="button"
              onClick={() => {
                mathfieldRef.current?.setValue("");
                lastEmittedValueRef.current = "";
                onChange("");
                mathfieldRef.current?.focus();
              }}
              className="text-slate-400 hover:text-rose-600 transition-colors text-xs cursor-pointer font-medium"
              title="Xóa trắng nội dung"
            >
              Xóa hết
            </button>
          )}
        </div>

        {/* MathLive Container */}
        <div
          ref={containerRef}
          className={`p-3.5 min-h-[56px] text-slate-900 dark:text-white rounded-b-2xl overflow-x-auto min-w-0 ${
            disabled ? "cursor-default select-text" : "cursor-text bg-white dark:bg-slate-900"
          }`}
          onPointerDown={() => {
            if (!disabled) onFocusRef.current?.();
          }}
          onClick={() => {
            if (!disabled) {
              onFocusRef.current?.();
              mathfieldRef.current?.focus();
            }
          }}
        />

        {/* Empty state hint */}
        {!value && (
          <div
            className="absolute left-4 top-[46px] text-slate-400 text-sm pointer-events-none select-none font-sans"
            style={{ display: isReady ? "block" : "none" }}
          >
            {placeholder}
          </div>
        )}
      </div>
    );
  }
);

VisualMathField.displayName = "VisualMathField";
