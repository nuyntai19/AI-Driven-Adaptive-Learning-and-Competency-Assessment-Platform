import { useRef, useState, useEffect, useImperativeHandle, forwardRef } from "react";
import type { AnswerEditorValue, AnswerEditorRef } from "./answerEditorHelpers";
import {
  deserializeCoordinateWithLatex,
  updateCoordinateAxis,
  shouldSyncCoordinateExternalValue,
  getAnswerSyntaxHint,
} from "./answerEditorHelpers";
import { VisualMathField, type VisualMathFieldRef } from "../VisualMathField";
import { MathFormulaPreview } from "../MathFormulaPreview";

export interface Coordinate2DInputProps {
  value: AnswerEditorValue;
  onChange?: (val: AnswerEditorValue) => void;
  disabled?: boolean;
  readOnly?: boolean;
  className?: string;
  autoFocus?: boolean;
  showPreview?: boolean;
  showSyntaxHint?: boolean;
  onFocus?: () => void;
  ariaLabel?: string;
}

/**
 * Coordinate2DInput
 * Structured 2D Coordinate editor utilizing two unified VisualMathField instances (MathLive).
 * Delivers full interactive math input for both X and Y axes, including fractions, radicals,
 * and virtual keyboard support.
 *
 * Serialization Invariants:
 * - finalAnswer = (${xPlain}; ${yPlain}) (strictly semicolon)
 * - answerDisplayLatex = \left(${xLatex};\,${yLatex}\right)
 * - Guarantees exactly one onChange per user operation without side effects in state updaters.
 * - External sync evaluates both rawText and displayLatex using pure helper shouldSyncCoordinateExternalValue.
 */
export const Coordinate2DInput = forwardRef<AnswerEditorRef, Coordinate2DInputProps>(
  (
    {
      value,
      onChange,
      disabled = false,
      readOnly = false,
      className = "",
      autoFocus = false,
      showPreview = true,
      showSyntaxHint = true,
      onFocus,
      ariaLabel = "Ô nhập tọa độ 2D",
    },
    ref
  ) => {
    const xRef = useRef<VisualMathFieldRef>(null);
    const yRef = useRef<VisualMathFieldRef>(null);
    const lastActiveAxisRef = useRef<"x" | "y">("x");
    const latestValueRef = useRef<AnswerEditorValue>(value);
    const isInternalClearingRef = useRef(false);

    const initialCoords = deserializeCoordinateWithLatex(value?.rawText, value?.displayLatex);
    const [coords, setCoords] = useState(initialCoords);
    const latestCoordsRef = useRef(initialCoords);

    // Sync external prop updates checking both rawText and displayLatex via pure lifecycle helper
    useEffect(() => {
      if (shouldSyncCoordinateExternalValue(value, latestValueRef.current)) {
        const next = deserializeCoordinateWithLatex(value?.rawText, value?.displayLatex);
        latestCoordsRef.current = next;
        setCoords(next);
      }
      latestValueRef.current = value;
    }, [value]);

    const handleXChange = (latex: string, plainText: string) => {
      if (isInternalClearingRef.current) return;
      const { nextCoords, nextValue } = updateCoordinateAxis(latestCoordsRef.current, "x", {
        plainText,
        latex,
      });
      latestCoordsRef.current = nextCoords;
      setCoords(nextCoords);
      latestValueRef.current = nextValue;
      onChange?.(nextValue);
    };

    const handleYChange = (latex: string, plainText: string) => {
      if (isInternalClearingRef.current) return;
      const { nextCoords, nextValue } = updateCoordinateAxis(latestCoordsRef.current, "y", {
        plainText,
        latex,
      });
      latestCoordsRef.current = nextCoords;
      setCoords(nextCoords);
      latestValueRef.current = nextValue;
      onChange?.(nextValue);
    };

    useImperativeHandle(ref, () => ({
      insertLatex: (latex: string) => {
        if (disabled || readOnly) return;
        if (lastActiveAxisRef.current === "y") {
          yRef.current?.insertAtCursor(latex);
        } else {
          xRef.current?.insertAtCursor(latex);
        }
      },
      insertAtCursor: (latex: string) => {
        if (disabled || readOnly) return;
        if (lastActiveAxisRef.current === "y") {
          yRef.current?.insertAtCursor(latex);
        } else {
          xRef.current?.insertAtCursor(latex);
        }
      },
      focus: () => {
        if (lastActiveAxisRef.current === "y") {
          yRef.current?.focus();
        } else {
          xRef.current?.focus();
        }
      },
      clear: () => {
        if (disabled || readOnly) return;
        isInternalClearingRef.current = true;
        try {
          xRef.current?.clear();
          yRef.current?.clear();
        } finally {
          isInternalClearingRef.current = false;
        }

        const emptyCoords = {
          x: { plainText: "", latex: "" },
          y: { plainText: "", latex: "" },
        };
        latestCoordsRef.current = emptyCoords;
        setCoords(emptyCoords);

        const emptyValue = { rawText: "", displayLatex: "" };
        latestValueRef.current = emptyValue;
        onChange?.(emptyValue);
      },
      getValue: () => {
        return latestValueRef.current;
      },
    }));

    const syntaxHint = showSyntaxHint ? getAnswerSyntaxHint("Coordinate2D", value) : null;
    const isInteractive = !disabled && !readOnly;

    return (
      <div className={`space-y-2.5 ${className}`} aria-label={ariaLabel}>
        {/* Structured Coordinate Container with Two VisualMathFields */}
        <div
          className={`flex flex-col sm:flex-row items-center justify-center gap-2 p-3 rounded-2xl border transition-all ${
            disabled
              ? "border-slate-200 dark:border-slate-800 bg-slate-50/70 dark:bg-slate-900/60"
              : "border-2 border-indigo-200/80 bg-white dark:bg-slate-900 focus-within:border-indigo-600 focus-within:ring-4 focus-within:ring-indigo-500/15"
          }`}
        >
          <span className="text-2xl font-serif font-medium text-slate-400 select-none hidden sm:inline">(</span>

          {/* X Coordinate Field */}
          <div className="flex-1 w-full sm:max-w-[260px] relative">
            <div className="text-[11px] font-bold text-slate-500 dark:text-slate-400 mb-1 px-1">
              Hoành độ (x):
            </div>
            <VisualMathField
              ref={xRef}
              value={coords.x.latex}
              onChange={handleXChange}
              onFocus={() => {
                lastActiveAxisRef.current = "x";
                onFocus?.();
              }}
              placeholder="Nhập x (ví dụ: 1/2)..."
              disabled={disabled || readOnly}
              autoFocus={autoFocus}
            />
          </div>

          <span className="text-xl font-black text-indigo-600 dark:text-indigo-400 select-none px-2 hidden sm:inline">
            ;
          </span>

          {/* Y Coordinate Field */}
          <div className="flex-1 w-full sm:max-w-[260px] relative">
            <div className="text-[11px] font-bold text-slate-500 dark:text-slate-400 mb-1 px-1">
              Tung độ (y):
            </div>
            <VisualMathField
              ref={yRef}
              value={coords.y.latex}
              onChange={handleYChange}
              onFocus={() => {
                lastActiveAxisRef.current = "y";
                onFocus?.();
              }}
              placeholder="Nhập y (ví dụ: -3)..."
              disabled={disabled || readOnly}
            />
          </div>

          <span className="text-2xl font-serif font-medium text-slate-400 select-none hidden sm:inline">)</span>
        </div>

        {/* Syntax Hint */}
        {syntaxHint && isInteractive && (
          <div className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800 text-xs text-amber-800 dark:text-amber-200">
            <span aria-hidden="true">💡</span>
            <span>{syntaxHint}</span>
          </div>
        )}

        {/* KaTeX Live Preview */}
        {showPreview && value.displayLatex && (
          <MathFormulaPreview
            formula={value.displayLatex}
            label="Tọa độ hiển thị (KaTeX)"
            className="text-xs"
          />
        )}
      </div>
    );
  }
);

Coordinate2DInput.displayName = "Coordinate2DInput";
