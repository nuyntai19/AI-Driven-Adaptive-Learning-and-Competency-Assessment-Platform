import { useEffect, useRef, useImperativeHandle, forwardRef, useState } from "react";
import { normalizeMathLivePlainText } from "../../utils/mathAnswerValue";
import { loadMathLive, resetMathLiveLoader } from "../../utils/mathLiveLoader";
import {
  hydrateMathFieldInstance,
  shouldSyncExternalValue,
  registerVirtualKeyboardDismissListener,
  DEFAULT_MATH_INLINE_SHORTCUTS,
  normalizeMathInsertContent,
} from "../../utils/visualMathFieldLifecycle";
import { MathFallbackTextarea } from "./answer-editor/MathFallbackTextarea";

interface MathFieldElement extends HTMLElement {
  readOnly: boolean;
  value: string;
  defaultMode: "inline-math" | "math" | "text";
  mode?: string;
  mathVirtualKeyboardPolicy: string;
  smartFence: boolean;
  smartMode: boolean;
  smartSuperscript: boolean;
  inlineShortcuts?: Record<string, string | { mode?: string; after?: string; value: string }>;
  getValue: (format?: string) => string;
  setValue: (value: string, options?: { silenceNotifications?: boolean }) => void;
  insert: (
    value: string,
    options?: {
      mode?: string;
      selectionMode?: string;
      focus?: boolean;
      silenceNotifications?: boolean;
    }
  ) => void;
  executeCommand: (command: [string, string]) => void;
}


export interface VisualMathFieldRef {
  insertAtCursor: (latex: string) => void;
  focus: () => void;
  clear: () => void;
  getValue: () => string;
  setValue: (latex: string) => void;
}

export interface VisualMathFieldProps {
  value: string;
  onChange: (latex: string, plainText: string) => void;
  onFocus?: () => void;
  onCommit?: () => void;
  onCancel?: () => void;
  placeholder?: string;
  disabled?: boolean;
  className?: string;
  autoFocus?: boolean;
  showToolbar?: boolean;
  compact?: boolean;
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
      onCommit,
      onCancel,
      placeholder = "Nhấp vào đây để nhập công thức hoặc chọn ký hiệu...",
      disabled = false,
      className = "",
      autoFocus = false,
      showToolbar = true,
      compact = false,
    },
    ref
  ) => {
    const containerRef = useRef<HTMLDivElement>(null);
    const mathfieldRef = useRef<MathFieldElement | null>(null);
    const [isReady, setIsReady] = useState(false);
    const [loadError, setLoadError] = useState(false);
    const [retryCount, setRetryCount] = useState(0);
    const [inputMode, setInputMode] = useState<"math" | "text">("math");
    const lastEmittedValueRef = useRef<string>(value);

    const onFocusRef = useRef(onFocus);
    onFocusRef.current = onFocus;
    const onChangeRef = useRef(onChange);
    onChangeRef.current = onChange;
    const onCommitRef = useRef(onCommit);
    onCommitRef.current = onCommit;
    const onCancelRef = useRef(onCancel);
    onCancelRef.current = onCancel;

    const latestValueRef = useRef(value);
    latestValueRef.current = value;
    const latestDisabledRef = useRef(disabled);
    latestDisabledRef.current = disabled;
    const latestAutoFocusRef = useRef(autoFocus);
    latestAutoFocusRef.current = autoFocus;

    // Register global outside-click dismissal listener for MathLive virtual keyboard
    useEffect(() => {
      const unregister = registerVirtualKeyboardDismissListener();
      return unregister;
    }, []);

    // Initialize Mathfield element inside container after dynamically loading MathLive
    useEffect(() => {
      let isMounted = true;
      const container = containerRef.current;
      if (!container) return;

      let currentMf: MathFieldElement | null = null;
      let onInputHandler: (() => void) | null = null;
      let onKeyDownHandler: ((e: KeyboardEvent) => void) | null = null;
      let onFocusHandler: (() => void) | null = null;
      let themeObserver: MutationObserver | null = null;

      loadMathLive()
        .then(() => {
          if (!isMounted || !containerRef.current) return;

          // Create <math-field> instance
          const mf = document.createElement("math-field") as MathFieldElement;
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
          mf.style.setProperty("--placeholder-color", "var(--rme-accent, #4f46e5)");
          mf.style.setProperty("--caret-color", "var(--rme-accent, #4f46e5)");
          mf.style.setProperty("--selection-background-color", "rgba(99, 102, 241, 0.25)");

          // 1. Mount to DOM container first so math-field is connected and property getters/setters are active
          containerRef.current.innerHTML = "";
          containerRef.current.appendChild(mf);
          mathfieldRef.current = mf;

          // 2. Configure MathLive settings
          mf.mathVirtualKeyboardPolicy = "manual"; // Prevent unwanted mobile popups on desktop
          mf.defaultMode = "math";
          mf.smartFence = false;
          mf.smartMode = false;
          mf.smartSuperscript = true;
          try {
            const existingShortcuts = { ...(mf.inlineShortcuts || {}) };
            delete (existingShortcuts as Record<string, unknown>)["|"];
            mf.inlineShortcuts = {
              ...existingShortcuts,
              ...DEFAULT_MATH_INLINE_SHORTCUTS,
            };
          } catch {
            mf.inlineShortcuts = {
              ...DEFAULT_MATH_INLINE_SHORTCUTS,
            };
          }
          mf.setAttribute("data-input-mode", "math");

          // Synchronize dark theme class with document or parent container
          const syncDarkClass = () => {
            if (typeof document === "undefined") return;
            const isDark =
              document.documentElement.classList.contains("dark") ||
              Boolean(containerRef.current?.closest(".dark, .bg-slate-950, .bg-slate-900, [data-theme='dark']"));
            mf.classList.toggle("dark", isDark);
          };
          syncDarkClass();

          if (typeof MutationObserver !== "undefined") {
            themeObserver = new MutationObserver(() => syncDarkClass());
            themeObserver.observe(document.documentElement, {
              attributes: true,
              attributeFilter: ["class"],
            });
          }

          try {
            const shadowStyle = document.createElement("style");
            shadowStyle.setAttribute("data-edutwin-toggle-guard", "true");
            shadowStyle.textContent = `
              :host([data-input-mode="text"]) [part="virtual-keyboard-toggle"],
              :host([data-input-mode="text"]) [part="menu-toggle"],
              :host([data-input-mode="text"]) .ML__virtual-keyboard-toggle,
              :host([data-input-mode="text"]) .ML__menu-toggle,
              :host([data-input-mode="text"]) .ML__toggles {
                display: none !important;
                visibility: hidden !important;
                pointer-events: none !important;
              }

              .ML__placeholder {
                opacity: 0.85 !important;
                border-bottom: 1.5px dashed currentColor !important;
                padding: 0 2px !important;
                pointer-events: auto !important;
                cursor: pointer !important;
              }
            `;
            mf.shadowRoot?.appendChild(shadowStyle);
          } catch {
            // Fallback handled via global CSS
          }

          // Intercept public insert and executeCommand to guarantee that commands from the virtual keyboard
          // (such as \sqrt{#0}, \left\vert#0\right\vert, \left\Vert#0\right\vert) convert #0 to placeholder #?
          // when selection is collapsed and set selectionMode to "placeholder" only for placeholders
          if (typeof mf.insert === "function") {
            const originalInsert = mf.insert.bind(mf);
            mf.insert = (s: string, options?: any) => {
              const isCollapsed = Boolean((mf as any).selectionIsCollapsed ?? true);
              const normalized = normalizeMathInsertContent(s, { isSelectionCollapsed: isCollapsed });
              const insertOptions = normalized.includes("#?")
                ? { ...options, selectionMode: options?.selectionMode ?? "placeholder" }
                : options;
              return originalInsert(normalized, insertOptions);
            };
          }

          if (typeof mf.executeCommand === "function") {
            const originalExecuteCommand = mf.executeCommand.bind(mf);
            mf.executeCommand = (command: any) => {
              if (Array.isArray(command) && command[0] === "insert" && typeof command[1] === "string") {
                const s = command[1];
                const isCollapsed = Boolean((mf as any).selectionIsCollapsed ?? true);
                const normalized = normalizeMathInsertContent(s, { isSelectionCollapsed: isCollapsed });
                const originalOptions = typeof command[2] === "object" && command[2] !== null ? command[2] : undefined;
                const insertOptions = normalized.includes("#?")
                  ? { ...originalOptions, selectionMode: originalOptions?.selectionMode ?? "placeholder" }
                  : command[2];
                const newCommand = [...command];
                newCommand[1] = normalized;
                if (command.length > 2 || normalized.includes("#?")) {
                  newCommand[2] = insertOptions;
                }
                return originalExecuteCommand(newCommand as any);
              }
              return originalExecuteCommand(command);
            };
          }

          // Hydrate with latest value and disabled state (retaining any edits made in fallback textarea)
          const { hydratedValue } = hydrateMathFieldInstance(mf, {
            latestValue: latestValueRef.current,
            latestDisabled: latestDisabledRef.current,
            latestAutoFocus: latestAutoFocusRef.current,
          });
          lastEmittedValueRef.current = hydratedValue;

          // Handle user typing / input
          const handleInput = () => {
            if (mf.readOnly) return;
            const currentLatex = mf.getValue ? mf.getValue("latex-expanded") : mf.value;
            const rawPlainText = mf.getValue ? mf.getValue("plain-text") : currentLatex;
            const currentPlainText = normalizeMathLivePlainText(rawPlainText, currentLatex);
            lastEmittedValueRef.current = currentLatex;
            latestValueRef.current = currentLatex;
            onChangeRef.current(currentLatex, currentPlainText);
          };

          const handleFocus = () => {
            onFocusRef.current?.();
          };

          // Keyboard Event Isolation: Prevent arrow keys, Tab, Enter, Space from bubbling up to quiz page
          const handleKeyDown = (e: KeyboardEvent) => {
            if (e.key === "Enter" && !e.shiftKey && onCommitRef.current) {
              // If MathLive is in LaTeX command mode or showing suggestion popovers, let MathLive handle Enter
              const isLatexCommandMode =
                (mf as unknown as { mode?: string }).mode === "latex" ||
                Boolean(mf.shadowRoot?.querySelector(".ML__popover, .ML__latex-popover, .ML__suggestion"));
              if (isLatexCommandMode) {
                return;
              }

              e.preventDefault();
              e.stopPropagation();
              onCommitRef.current();
              return;
            }


            if (e.key === "Escape" && onCancelRef.current) {
              e.preventDefault();
              e.stopPropagation();
              onCancelRef.current();
              return;
            }

            e.stopPropagation();

            if (mf.readOnly) {
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

          onInputHandler = handleInput;
          onKeyDownHandler = handleKeyDown;
          onFocusHandler = handleFocus;
          currentMf = mf;

          mf.addEventListener("input", handleInput);
          mf.addEventListener("keydown", handleKeyDown);
          mf.addEventListener("focus", handleFocus);
          mf.addEventListener("pointerdown", handleFocus);
          setLoadError(false);
          setIsReady(true);
        })
        .catch((err) => {
          if (!isMounted) return;
          console.error("Failed to load MathLive bundle:", err);
          setLoadError(true);
        });

      return () => {
        isMounted = false;
        if (currentMf) {
          if (onInputHandler) currentMf.removeEventListener("input", onInputHandler);
          if (onKeyDownHandler) currentMf.removeEventListener("keydown", onKeyDownHandler);
          if (onFocusHandler) {
            currentMf.removeEventListener("focus", onFocusHandler);
            currentMf.removeEventListener("pointerdown", onFocusHandler);
          }
        }
        if (themeObserver) {
          themeObserver.disconnect();
        }
        if (container) {
          container.innerHTML = "";
        }
        mathfieldRef.current = null;
      };
    }, [retryCount]);

    // Sync external value updates if changed from outside (e.g. reset or clear)
    useEffect(() => {
      if (!isReady || !mathfieldRef.current) return;
      const currentVal = mathfieldRef.current.getValue ? mathfieldRef.current.getValue("latex-expanded") : mathfieldRef.current.value;
      if (shouldSyncExternalValue(value, currentVal, lastEmittedValueRef.current)) {
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

    // Sync input mode attribute and ensure virtual keyboard is hidden in text mode
    useEffect(() => {
      const mf = mathfieldRef.current;
      if (!mf) return;
      mf.setAttribute("data-input-mode", inputMode);

      if (inputMode === "text") {
        if (
          typeof window !== "undefined" &&
          (window as unknown as { mathVirtualKeyboard?: { visible?: boolean; hide: () => void } }).mathVirtualKeyboard?.visible
        ) {
          (window as unknown as { mathVirtualKeyboard?: { hide: () => void } }).mathVirtualKeyboard?.hide();
        }
      }
    }, [inputMode]);

    // Expose ref methods for toolbar and Casio calculator insertion
    useImperativeHandle(ref, () => ({
      insertAtCursor: (latexOrText: string) => {
        if (disabled) return;
        const mf = mathfieldRef.current;
        if (!mf || mf.readOnly) return;
        mf.focus();
        mf.defaultMode = "math";
        mf.smartFence = false;
        mf.smartMode = false;
        mf.inlineShortcuts = {
          ...mf.inlineShortcuts,
          ...DEFAULT_MATH_INLINE_SHORTCUTS,
        };
        mf.executeCommand(["switchMode", "math"]);
        mf.setAttribute("data-input-mode", "math");
        const toInsert = normalizeMathInsertContent(latexOrText);
        if (typeof mf.insert === "function") {
          mf.insert(toInsert, {
            mode: "math",
            selectionMode: "placeholder",
            focus: true,
            silenceNotifications: true,
          });
        } else {
          mf.executeCommand(["insert", toInsert]);
        }
        const newVal = mf.getValue ? mf.getValue("latex-expanded") : mf.value;
        const rawPlainText = mf.getValue ? mf.getValue("plain-text") : newVal;
        const plainText = normalizeMathLivePlainText(rawPlainText, newVal);
        lastEmittedValueRef.current = newVal;
        setInputMode("math");
        onChangeRef.current(newVal, plainText);
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
        onChangeRef.current("", "");
      },
      getValue: () => {
        if (!mathfieldRef.current) return latestValueRef.current || "";
        return mathfieldRef.current.getValue ? mathfieldRef.current.getValue("latex-expanded") : mathfieldRef.current.value;
      },
      setValue: (latex: string) => {
        lastEmittedValueRef.current = latex;
        latestValueRef.current = latex;
        if (latestDisabledRef.current || !mathfieldRef.current) return;
        mathfieldRef.current.setValue(latex, { silenceNotifications: true });
        lastEmittedValueRef.current = latex;
        const rawPlainText = mathfieldRef.current.getValue
          ? mathfieldRef.current.getValue("plain-text")
          : latex;
        const plainText = normalizeMathLivePlainText(rawPlainText, latex);
        onChangeRef.current(latex, plainText);
      },
    }));

    const switchInputMode = (nextMode: "math" | "text") => {
      if (disabled) return;
      const mf = mathfieldRef.current;
      if (!mf || mf.readOnly) return;

      mf.focus();
      mf.defaultMode = nextMode;
      if (nextMode === "math") {
        mf.smartFence = false;
        mf.smartMode = false;
        mf.inlineShortcuts = {
          ...mf.inlineShortcuts,
          ...DEFAULT_MATH_INLINE_SHORTCUTS,
        };
      }
      mf.executeCommand(["switchMode", nextMode]);
      mf.setAttribute("data-input-mode", nextMode);

      if (nextMode === "text") {
        if (
          typeof window !== "undefined" &&
          (window as unknown as { mathVirtualKeyboard?: { visible?: boolean; hide: () => void } }).mathVirtualKeyboard?.visible
        ) {
          (window as unknown as { mathVirtualKeyboard?: { hide: () => void } }).mathVirtualKeyboard?.hide();
        }
      }

      setInputMode(nextMode);
    };

    return (
      <div
        className={
          compact
            ? `relative w-full ${className}`
            : `relative rounded-2xl border transition-all ${
                disabled
                  ? "border-slate-200 dark:border-slate-800 bg-slate-50/70 dark:bg-slate-900/60 shadow-none text-slate-800 dark:text-slate-200"
                  : "border-2 border-indigo-200/80 bg-white dark:bg-slate-900 focus-within:border-indigo-600 focus-within:ring-4 focus-within:ring-indigo-500/15 shadow-sm text-slate-900 dark:text-white"
              } ${className}`
        }
      >
        {/* Helper guide */}
        {showToolbar && (
          <div
            className={`flex flex-wrap items-center justify-between gap-2 px-3.5 py-2 border-b text-xs select-none rounded-t-2xl ${
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
            {!disabled && (
              <div className="flex items-center gap-2">
                <div
                  className="inline-flex rounded-lg border border-indigo-200 dark:border-indigo-800 bg-white/80 dark:bg-slate-900/80 p-0.5"
                  role="group"
                  aria-label="Chế độ nhập đáp án"
                >
                  <button
                    type="button"
                    onClick={() => switchInputMode("text")}
                    aria-pressed={inputMode === "text"}
                    className={`rounded-md px-2 py-1 font-semibold transition-colors ${
                      inputMode === "text"
                        ? "bg-indigo-600 text-white shadow-sm"
                        : "text-slate-600 hover:text-indigo-700 dark:text-slate-300 dark:hover:text-indigo-300"
                    }`}
                    title="Nhập chữ, khoảng trắng và ký tự bàn phím"
                  >
                    ⌨ Bàn phím
                  </button>
                  <button
                    type="button"
                    onClick={() => switchInputMode("math")}
                    aria-pressed={inputMode === "math"}
                    className={`rounded-md px-2 py-1 font-semibold transition-colors ${
                      inputMode === "math"
                        ? "bg-indigo-600 text-white shadow-sm"
                        : "text-slate-600 hover:text-indigo-700 dark:text-slate-300 dark:hover:text-indigo-300"
                    }`}
                    title="Nhập công thức toán trực quan"
                  >
                    ∑ Toán học
                  </button>
                </div>
                {value.trim() && (
                  <button
                    type="button"
                    onClick={() => {
                      mathfieldRef.current?.setValue("");
                      lastEmittedValueRef.current = "";
                      onChangeRef.current("", "");
                      mathfieldRef.current?.focus();
                    }}
                    className="text-slate-400 hover:text-rose-600 transition-colors text-xs cursor-pointer font-medium"
                    title="Xóa trắng nội dung"
                  >
                    Xóa hết
                  </button>
                )}
              </div>
            )}
          </div>
        )}

        {/* MathLive Container or Fallback Textarea */}
        {loadError && !isReady ? (
          <div className={compact ? "p-1.5 bg-transparent" : "p-3.5 bg-white dark:bg-slate-900 rounded-b-2xl"}>
            <MathFallbackTextarea
              value={value}
              onChange={(nextVal) => {
                lastEmittedValueRef.current = nextVal;
                latestValueRef.current = nextVal;
                onChange(nextVal, nextVal);
              }}
              onFocus={() => onFocusRef.current?.()}
              onCommit={onCommit}
              onCancel={onCancel}
              disabled={disabled}
              placeholder={placeholder}
              onRetry={() => {
                resetMathLiveLoader();
                setLoadError(false);
                setRetryCount((c) => c + 1);
              }}
            />
          </div>
        ) : (
          <div
            ref={containerRef}
            className={`${
              compact ? "p-1.5 min-h-[46px]" : "p-3.5 min-h-[56px] rounded-b-2xl"
            } text-inherit overflow-x-auto min-w-0 ${
              disabled ? "cursor-default select-text" : "cursor-text bg-transparent"
            } ${inputMode === "text" ? "math-field-text-mode" : "math-field-math-mode"}`}
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
        )}

        {/* Empty state hint */}
        {!value && (
          <div
            className={`absolute text-slate-400 text-sm pointer-events-none select-none font-sans ${
              compact ? "left-2.5 top-2" : "left-4 top-[46px]"
            }`}
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
