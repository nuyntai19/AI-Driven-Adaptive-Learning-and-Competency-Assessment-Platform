import React, { useState, useMemo, useRef, useEffect } from "react";
import { CalculatorEngine, type AngleMode } from "../../utils/calculatorEngine";

export interface ScientificCalculatorDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  onInsertResult?: (val: string) => void;
}

interface HistoryItem {
  expression: string;
  result: string;
}

export const ScientificCalculatorDrawer: React.FC<ScientificCalculatorDrawerProps> = ({
  isOpen,
  onClose,
  onInsertResult,
}) => {
  const [expression, setExpression] = useState("");
  const [result, setResult] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [angleMode, setAngleMode] = useState<AngleMode>("deg");
  const [memory, setMemory] = useState<number>(0);
  const [lastAnswer, setLastAnswer] = useState<string>("0");
  const [history, setHistory] = useState<HistoryItem[]>([]);
  const [showHistory, setShowHistory] = useState(false);

  const inputRef = useRef<HTMLInputElement>(null);

  const engine = useMemo(() => new CalculatorEngine(angleMode), [angleMode]);

  useEffect(() => {
    if (isOpen) {
      setTimeout(() => inputRef.current?.focus(), 100);
    }
  }, [isOpen]);

  const handleEvaluate = () => {
    if (!expression.trim()) return;
    try {
      const resNum = engine.evaluate(expression);
      const resStr = String(resNum);
      setResult(resStr);
      setError(null);
      setLastAnswer(resStr);
      setHistory((prev) => [
        { expression: expression.trim(), result: resStr },
        ...prev.slice(0, 19),
      ]);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Lỗi tính toán");
      setResult(null);
    }
  };

  const appendText = (text: string) => {
    setExpression((prev) => prev + text);
    setError(null);
    inputRef.current?.focus();
  };

  const handleClear = () => {
    setExpression("");
    setResult(null);
    setError(null);
    inputRef.current?.focus();
  };

  const handleDelete = () => {
    setExpression((prev) => prev.slice(0, -1));
    setError(null);
    inputRef.current?.focus();
  };

  // Memory functions
  const handleMemoryAdd = () => {
    try {
      const val = result ? Number(result) : engine.evaluate(expression);
      if (!isNaN(val)) {
        setMemory((prev) => prev + val);
      }
    } catch {
      // ignore evaluation failure for memory op
    }
  };

  const handleMemorySub = () => {
    try {
      const val = result ? Number(result) : engine.evaluate(expression);
      if (!isNaN(val)) {
        setMemory((prev) => prev - val);
      }
    } catch {
      // ignore
    }
  };

  const handleMemoryRecall = () => {
    appendText(String(memory));
  };

  const handleMemoryClear = () => {
    setMemory(0);
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter") {
      e.preventDefault();
      handleEvaluate();
    }
  };

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex justify-end bg-slate-900/40 backdrop-blur-xs transition-opacity"
      role="dialog"
      aria-modal="true"
      aria-label="Máy tính khoa học"
    >
      {/* Backdrop click to close */}
      <div className="absolute inset-0" onClick={onClose} />

      {/* Drawer panel */}
      <div className="relative w-full max-w-sm sm:max-w-md bg-white dark:bg-slate-900 shadow-2xl h-full flex flex-col border-l border-slate-200 dark:border-slate-800 z-10 animate-in slide-in-from-right duration-200">
        {/* Header */}
        <div className="flex items-center justify-between px-4 py-3 border-b border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-950">
          <div className="flex items-center gap-2">
            <span className="text-base font-semibold text-slate-900 dark:text-slate-100">
              Máy tính khoa học
            </span>
            {memory !== 0 && (
              <span className="text-[10px] font-bold px-1.5 py-0.5 rounded bg-emerald-100 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300">
                M ({memory})
              </span>
            )}
          </div>
          <div className="flex items-center gap-1.5">
            <button
              type="button"
              onClick={() => setShowHistory(!showHistory)}
              className={`text-xs px-2 py-1 rounded font-medium border ${
                showHistory
                  ? "bg-blue-50 text-blue-600 border-blue-200 dark:bg-blue-950 dark:text-blue-400 dark:border-blue-800"
                  : "bg-white dark:bg-slate-800 text-slate-600 dark:text-slate-300 border-slate-200 dark:border-slate-700"
              }`}
            >
              Lịch sử
            </button>
            <button
              type="button"
              onClick={onClose}
              className="p-1 rounded text-slate-500 hover:text-slate-700 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800"
              aria-label="Đóng máy tính"
            >
              ✕
            </button>
          </div>
        </div>

        {/* Display Area */}
        <div className="p-4 bg-slate-100 dark:bg-slate-950 border-b border-slate-200 dark:border-slate-800">
          <div className="flex items-center justify-between text-xs text-slate-500 dark:text-slate-400 mb-1">
            <div className="flex items-center gap-2">
              <span className="font-medium">CHẾ ĐỘ:</span>
              <button
                type="button"
                onClick={() => setAngleMode(angleMode === "deg" ? "rad" : "deg")}
                className="px-2 py-0.5 rounded bg-slate-200 dark:bg-slate-800 hover:bg-slate-300 dark:hover:bg-slate-700 font-mono font-bold text-slate-800 dark:text-slate-200 transition-colors"
              >
                {angleMode.toUpperCase()}
              </button>
            </div>
            {result !== null && (
              <span className="text-emerald-600 dark:text-emerald-400 font-semibold">
                Đã tính
              </span>
            )}
          </div>

          {/* Expression input */}
          <input
            ref={inputRef}
            type="text"
            value={expression}
            onChange={(e) => setExpression(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Ví dụ: sin(30) + sqrt(16)..."
            className="w-full bg-transparent font-mono text-lg text-slate-900 dark:text-slate-100 placeholder-slate-400 dark:placeholder-slate-600 outline-hidden border-b border-slate-300 dark:border-slate-700 focus:border-blue-500 pb-1"
          />

          {/* Result or Error */}
          <div className="mt-2 min-h-6 flex items-center justify-end font-mono">
            {error ? (
              <span className="text-xs text-red-600 dark:text-red-400 font-medium">
                {error}
              </span>
            ) : result !== null ? (
              <span className="text-xl font-bold text-blue-600 dark:text-blue-400">
                = {result}
              </span>
            ) : null}
          </div>

          {/* Insert Action */}
          {result !== null && onInsertResult && (
            <div className="mt-2 flex justify-end">
              <button
                type="button"
                onClick={() => onInsertResult(result)}
                className="text-xs px-2.5 py-1 rounded bg-blue-600 hover:bg-blue-700 text-white font-medium transition-colors shadow-xs"
              >
                Chèn kết quả ({result})
              </button>
            </div>
          )}
        </div>

        {/* History Drawer toggle */}
        {showHistory && (
          <div className="p-3 bg-slate-50 dark:bg-slate-950/80 border-b border-slate-200 dark:border-slate-800 max-h-48 overflow-y-auto text-xs">
            <div className="font-semibold text-slate-700 dark:text-slate-300 mb-2 flex justify-between items-center">
              <span>Lịch sử tính toán</span>
              {history.length > 0 && (
                <button
                  type="button"
                  onClick={() => setHistory([])}
                  className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
                >
                  Xóa lịch sử
                </button>
              )}
            </div>
            {history.length === 0 ? (
              <p className="text-slate-400 italic">Lịch sử trống</p>
            ) : (
              <div className="space-y-1.5">
                {history.map((h, i) => (
                  <div
                    key={i}
                    className="flex justify-between items-center p-1.5 rounded bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800"
                  >
                    <span className="font-mono text-slate-600 dark:text-slate-400 truncate max-w-[180px]">
                      {h.expression} = <span className="text-blue-600 font-semibold">{h.result}</span>
                    </span>
                    <button
                      type="button"
                      onClick={() => {
                        setExpression(h.expression);
                        setResult(h.result);
                      }}
                      className="text-[11px] text-blue-600 hover:underline ml-2"
                    >
                      Use
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* Keypad Grid */}
        <div className="p-3 grid grid-cols-5 gap-1.5 flex-1 overflow-y-auto content-start select-none">
          {/* Row 1: Memory */}
          <button
            type="button"
            onClick={handleMemoryClear}
            className="p-2 text-xs font-semibold rounded bg-slate-200 dark:bg-slate-800 hover:bg-slate-300 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-300"
          >
            MC
          </button>
          <button
            type="button"
            onClick={handleMemoryRecall}
            className="p-2 text-xs font-semibold rounded bg-slate-200 dark:bg-slate-800 hover:bg-slate-300 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-300"
          >
            MR
          </button>
          <button
            type="button"
            onClick={handleMemoryAdd}
            className="p-2 text-xs font-semibold rounded bg-slate-200 dark:bg-slate-800 hover:bg-slate-300 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-300"
          >
            M+
          </button>
          <button
            type="button"
            onClick={handleMemorySub}
            className="p-2 text-xs font-semibold rounded bg-slate-200 dark:bg-slate-800 hover:bg-slate-300 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-300"
          >
            M-
          </button>
          <button
            type="button"
            onClick={handleClear}
            className="p-2 text-xs font-bold rounded bg-rose-100 dark:bg-rose-950 hover:bg-rose-200 dark:hover:bg-rose-900 text-rose-700 dark:text-rose-300"
          >
            AC
          </button>

          {/* Row 2: Scientific 1 */}
          <button
            type="button"
            onClick={() => appendText("sin(")}
            className="p-2 text-xs font-mono rounded bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200"
          >
            sin
          </button>
          <button
            type="button"
            onClick={() => appendText("cos(")}
            className="p-2 text-xs font-mono rounded bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200"
          >
            cos
          </button>
          <button
            type="button"
            onClick={() => appendText("tan(")}
            className="p-2 text-xs font-mono rounded bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200"
          >
            tan
          </button>
          <button
            type="button"
            onClick={() => appendText("π")}
            className="p-2 text-xs font-mono rounded bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200"
          >
            π
          </button>
          <button
            type="button"
            onClick={() => appendText("e")}
            className="p-2 text-xs font-mono rounded bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200"
          >
            e
          </button>

          {/* Row 3: Scientific 2 */}
          <button
            type="button"
            onClick={() => appendText("sqrt(")}
            className="p-2 text-xs font-mono rounded bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200"
          >
            √
          </button>
          <button
            type="button"
            onClick={() => appendText("cbrt(")}
            className="p-2 text-xs font-mono rounded bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200"
          >
            ∛
          </button>
          <button
            type="button"
            onClick={() => appendText("log(")}
            className="p-2 text-xs font-mono rounded bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200"
          >
            log
          </button>
          <button
            type="button"
            onClick={() => appendText("ln(")}
            className="p-2 text-xs font-mono rounded bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200"
          >
            ln
          </button>
          <button
            type="button"
            onClick={() => appendText("^")}
            className="p-2 text-xs font-mono rounded bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200"
          >
            ^
          </button>

          {/* Row 4: Structure & Delete */}
          <button
            type="button"
            onClick={() => appendText("(")}
            className="p-2 text-xs font-mono rounded bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200"
          >
            (
          </button>
          <button
            type="button"
            onClick={() => appendText(")")}
            className="p-2 text-xs font-mono rounded bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200"
          >
            )
          </button>
          <button
            type="button"
            onClick={() => appendText("abs(")}
            className="p-2 text-xs font-mono rounded bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200"
          >
            |x|
          </button>
          <button
            type="button"
            onClick={() => appendText("%")}
            className="p-2 text-xs font-mono rounded bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200"
          >
            %
          </button>
          <button
            type="button"
            onClick={handleDelete}
            className="p-2 text-xs font-semibold rounded bg-amber-100 dark:bg-amber-950 hover:bg-amber-200 dark:hover:bg-amber-900 text-amber-800 dark:text-amber-200"
          >
            DEL
          </button>

          {/* Row 5: 7 8 9 ÷ Ans */}
          <button
            type="button"
            onClick={() => appendText("7")}
            className="p-2 text-sm font-semibold rounded bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700 text-slate-900 dark:text-slate-100 shadow-2xs"
          >
            7
          </button>
          <button
            type="button"
            onClick={() => appendText("8")}
            className="p-2 text-sm font-semibold rounded bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700 text-slate-900 dark:text-slate-100 shadow-2xs"
          >
            8
          </button>
          <button
            type="button"
            onClick={() => appendText("9")}
            className="p-2 text-sm font-semibold rounded bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700 text-slate-900 dark:text-slate-100 shadow-2xs"
          >
            9
          </button>
          <button
            type="button"
            onClick={() => appendText(" / ")}
            className="p-2 text-sm font-semibold rounded bg-slate-200 dark:bg-slate-700 hover:bg-slate-300 dark:hover:bg-slate-600 text-blue-600 dark:text-blue-400"
          >
            ÷
          </button>
          <button
            type="button"
            onClick={() => appendText(lastAnswer)}
            className="p-2 text-xs font-mono rounded bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-300"
          >
            Ans
          </button>

          {/* Row 6: 4 5 6 × */}
          <button
            type="button"
            onClick={() => appendText("4")}
            className="p-2 text-sm font-semibold rounded bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700 text-slate-900 dark:text-slate-100 shadow-2xs"
          >
            4
          </button>
          <button
            type="button"
            onClick={() => appendText("5")}
            className="p-2 text-sm font-semibold rounded bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700 text-slate-900 dark:text-slate-100 shadow-2xs"
          >
            5
          </button>
          <button
            type="button"
            onClick={() => appendText("6")}
            className="p-2 text-sm font-semibold rounded bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700 text-slate-900 dark:text-slate-100 shadow-2xs"
          >
            6
          </button>
          <button
            type="button"
            onClick={() => appendText(" * ")}
            className="p-2 text-sm font-semibold rounded bg-slate-200 dark:bg-slate-700 hover:bg-slate-300 dark:hover:bg-slate-600 text-blue-600 dark:text-blue-400"
          >
            ×
          </button>
          <button
            type="button"
            onClick={() => appendText(" - ")}
            className="p-2 text-sm font-semibold rounded bg-slate-200 dark:bg-slate-700 hover:bg-slate-300 dark:hover:bg-slate-600 text-blue-600 dark:text-blue-400"
          >
            −
          </button>

          {/* Row 7: 1 2 3 + */}
          <button
            type="button"
            onClick={() => appendText("1")}
            className="p-2 text-sm font-semibold rounded bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700 text-slate-900 dark:text-slate-100 shadow-2xs"
          >
            1
          </button>
          <button
            type="button"
            onClick={() => appendText("2")}
            className="p-2 text-sm font-semibold rounded bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700 text-slate-900 dark:text-slate-100 shadow-2xs"
          >
            2
          </button>
          <button
            type="button"
            onClick={() => appendText("3")}
            className="p-2 text-sm font-semibold rounded bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700 text-slate-900 dark:text-slate-100 shadow-2xs"
          >
            3
          </button>
          <button
            type="button"
            onClick={() => appendText(" + ")}
            className="p-2 text-sm font-semibold rounded bg-slate-200 dark:bg-slate-700 hover:bg-slate-300 dark:hover:bg-slate-600 text-blue-600 dark:text-blue-400"
          >
            +
          </button>
          <button
            type="button"
            onClick={handleEvaluate}
            className="row-span-2 p-2 text-base font-bold rounded bg-blue-600 hover:bg-blue-700 text-white shadow-md flex items-center justify-center active:scale-95 transition-transform"
          >
            =
          </button>

          {/* Row 8: 0 . */}
          <button
            type="button"
            onClick={() => appendText("0")}
            className="col-span-2 p-2 text-sm font-semibold rounded bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700 text-slate-900 dark:text-slate-100 shadow-2xs"
          >
            0
          </button>
          <button
            type="button"
            onClick={() => appendText(".")}
            className="p-2 text-sm font-semibold rounded bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-700 text-slate-900 dark:text-slate-100 shadow-2xs"
          >
            .
          </button>
          <button
            type="button"
            onClick={() => appendText(" -")}
            className="p-2 text-xs font-mono rounded bg-slate-200 dark:bg-slate-700 hover:bg-slate-300 dark:hover:bg-slate-600 text-slate-800 dark:text-slate-200"
          >
            ±
          </button>
        </div>

        {/* Footer info */}
        <div className="px-4 py-2 border-t border-slate-200 dark:border-slate-800 text-[11px] text-slate-400 dark:text-slate-500 bg-slate-50 dark:bg-slate-950 flex justify-between">
          <span>Deterministic evaluation</span>
          <span>Zero solver assistance</span>
        </div>
      </div>
    </div>
  );
};
