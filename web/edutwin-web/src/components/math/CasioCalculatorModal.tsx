import React, { useState, useMemo, useRef, useEffect } from "react";
import { CalculatorEngine, type AngleMode } from "../../utils/calculatorEngine";

export interface CasioCalculatorModalProps {
  isOpen: boolean;
  onClose: () => void;
  onInsertResult?: (result: string) => void;
}

export const CasioCalculatorModal: React.FC<CasioCalculatorModalProps> = ({
  isOpen,
  onClose,
  onInsertResult,
}) => {
  const [activeTab, setActiveTab] = useState<"online" | "offline">("online");
  const [expression, setExpression] = useState("");
  const [result, setResult] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [angleMode, setAngleMode] = useState<AngleMode>("deg");
  const [lastAnswer, setLastAnswer] = useState<string>("0");

  const inputRef = useRef<HTMLInputElement>(null);
  const engine = useMemo(() => new CalculatorEngine(angleMode), [angleMode]);

  useEffect(() => {
    if (isOpen && activeTab === "offline") {
      setTimeout(() => inputRef.current?.focus(), 150);
    }
  }, [isOpen, activeTab]);

  // Handle ESC key to close
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape" && isOpen) {
        onClose();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose]);

  const handleEvaluate = () => {
    if (!expression.trim()) return;
    try {
      const resNum = engine.evaluate(expression);
      const resStr = String(resNum);
      setResult(resStr);
      setError(null);
      setLastAnswer(resStr);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Lỗi cú pháp");
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

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/75 p-2 sm:p-4 backdrop-blur-xs animate-in fade-in duration-150"
      role="dialog"
      aria-modal="true"
      aria-label="Máy tính khoa học Casio fx-580VN X"
    >
      {/* Backdrop click to close */}
      <div className="absolute inset-0" onClick={onClose} />

      {/* Main Container */}
      <div className="relative z-10 flex flex-col w-full max-w-xl max-h-[92vh] rounded-2xl bg-[#1e2229] border border-slate-700/80 shadow-2xl overflow-hidden">
        {/* Header Bar */}
        <div className="flex items-center justify-between px-4 py-3 border-b border-slate-700/60 bg-[#16191f]">
          <div className="flex items-center gap-3">
            <span className="text-sm font-bold tracking-wide text-amber-400 font-mono flex items-center gap-1.5">
              <span className="text-base">🖩</span> CASIO fx-580VN X
            </span>
            <div className="flex items-center bg-slate-800/80 p-0.5 rounded-lg border border-slate-700 text-xs">
              <button
                type="button"
                onClick={() => setActiveTab("online")}
                className={`px-2.5 py-1 rounded-md font-medium transition-colors ${
                  activeTab === "online"
                    ? "bg-amber-500 text-slate-950 font-bold shadow-xs"
                    : "text-slate-400 hover:text-white"
                }`}
              >
                Bản Full (CalcES)
              </button>
              <button
                type="button"
                onClick={() => setActiveTab("offline")}
                className={`px-2.5 py-1 rounded-md font-medium transition-colors ${
                  activeTab === "offline"
                    ? "bg-amber-500 text-slate-950 font-bold shadow-xs"
                    : "text-slate-400 hover:text-white"
                }`}
              >
                Bản Mini (Offline)
              </button>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={onClose}
              className="p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 transition-colors"
              aria-label="Đóng máy tính"
            >
              ✕
            </button>
          </div>
        </div>

        {/* Tab 1: CalcES Iframe (Full Casio fx-580VN X) */}
        {activeTab === "online" && (
          <div className="flex flex-col flex-1 min-h-[640px] bg-[#121418] relative">
            <iframe
              src="https://mathda.com/calculator/vi"
              title="Máy tính Casio fx-580VN X (CalcES)"
              className="w-full h-[650px] border-0"
              allow="camera; microphone; clipboard-write; clipboard-read"
            />
            <div className="flex items-center justify-between px-4 py-2 bg-[#16191f] border-t border-slate-800 text-xs text-slate-400">
              <span>💡 Có đầy đủ chức năng giải phương trình, ma trận, bảng tính, tích phân & quét AI.</span>
              <button
                type="button"
                onClick={onClose}
                className="text-amber-400 hover:text-amber-300 font-medium cursor-pointer"
              >
                Quay lại bài làm →
              </button>
            </div>
          </div>
        )}

        {/* Tab 2: Casio Mini (Offline 3D Keypad) */}
        {activeTab === "offline" && (
          <div className="flex flex-col p-4 sm:p-5 bg-[#1a1d24] overflow-y-auto max-h-[80vh]">
            {/* Casio LCD Screen Container */}
            <div className="rounded-xl border-4 border-[#121418] bg-[#d5e0d5] p-3 shadow-inner text-slate-900 font-mono select-none mb-4">
              {/* Status Header */}
              <div className="flex items-center justify-between text-[11px] font-bold text-slate-600 border-b border-slate-400/50 pb-1 mb-1">
                <div className="flex items-center gap-2">
                  <span>NATURAL-V.P.A.M.</span>
                  <button
                    type="button"
                    onClick={() => setAngleMode(angleMode === "deg" ? "rad" : "deg")}
                    className="px-1.5 py-0.5 rounded bg-slate-800 text-white text-[10px]"
                  >
                    {angleMode.toUpperCase()}
                  </button>
                </div>
                <span>{result !== null ? "STAT" : "MATH"}</span>
              </div>

              {/* Expression Input */}
              <div className="min-h-[36px] flex items-center">
                <input
                  ref={inputRef}
                  type="text"
                  value={expression}
                  onChange={(e) => setExpression(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") {
                      e.preventDefault();
                      handleEvaluate();
                    }
                  }}
                  placeholder="0"
                  className="w-full bg-transparent font-mono text-lg font-bold text-slate-900 placeholder-slate-500 outline-hidden tracking-wider"
                />
              </div>

              {/* Result display */}
              <div className="min-h-[30px] flex items-center justify-end text-xl font-black text-slate-950">
                {error ? (
                  <span className="text-xs text-rose-700 font-bold">{error}</span>
                ) : result !== null ? (
                  <span>= {result}</span>
                ) : null}
              </div>
            </div>

            {/* Quick Action: Insert Result */}
            {result !== null && onInsertResult && (
              <div className="mb-3 flex justify-end">
                <button
                  type="button"
                  onClick={() => {
                    onInsertResult(result);
                    onClose();
                  }}
                  className="w-full py-2 px-3 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs shadow-md transition-all flex items-center justify-center gap-1.5 cursor-pointer"
                >
                  <span>📥</span>
                  <span>Chèn kết quả ({result}) vào con trỏ bài thi</span>
                </button>
              </div>
            )}

            {/* Casio Keypad Grid */}
            <div className="grid grid-cols-5 gap-1.5 select-none">
              {/* Function Row 1 */}
              <button
                type="button"
                className="py-2 rounded-md bg-[#b8860b] hover:bg-[#cda01e] active:translate-y-0.5 text-slate-950 font-black text-[11px] shadow-xs"
              >
                SHIFT
              </button>
              <button
                type="button"
                className="py-2 rounded-md bg-[#c0392b] hover:bg-[#d44637] active:translate-y-0.5 text-white font-black text-[11px] shadow-xs"
              >
                ALPHA
              </button>
              <button
                type="button"
                onClick={() => appendText("sqrt(")}
                className="py-2 rounded-md bg-[#2d323b] hover:bg-[#383e4a] active:translate-y-0.5 text-white font-mono font-bold text-sm shadow-xs"
              >
                √
              </button>
              <button
                type="button"
                onClick={() => appendText("^2")}
                className="py-2 rounded-md bg-[#2d323b] hover:bg-[#383e4a] active:translate-y-0.5 text-white font-mono font-bold text-sm shadow-xs"
              >
                x²
              </button>
              <button
                type="button"
                onClick={() => appendText("^")}
                className="py-2 rounded-md bg-[#2d323b] hover:bg-[#383e4a] active:translate-y-0.5 text-white font-mono font-bold text-sm shadow-xs"
              >
                xⁿ
              </button>

              {/* Function Row 2 */}
              <button
                type="button"
                onClick={() => appendText("sin(")}
                className="py-2 rounded-md bg-[#2d323b] hover:bg-[#383e4a] active:translate-y-0.5 text-slate-200 font-mono text-xs shadow-xs"
              >
                sin
              </button>
              <button
                type="button"
                onClick={() => appendText("cos(")}
                className="py-2 rounded-md bg-[#2d323b] hover:bg-[#383e4a] active:translate-y-0.5 text-slate-200 font-mono text-xs shadow-xs"
              >
                cos
              </button>
              <button
                type="button"
                onClick={() => appendText("tan(")}
                className="py-2 rounded-md bg-[#2d323b] hover:bg-[#383e4a] active:translate-y-0.5 text-slate-200 font-mono text-xs shadow-xs"
              >
                tan
              </button>
              <button
                type="button"
                onClick={() => appendText("log(")}
                className="py-2 rounded-md bg-[#2d323b] hover:bg-[#383e4a] active:translate-y-0.5 text-slate-200 font-mono text-xs shadow-xs"
              >
                log
              </button>
              <button
                type="button"
                onClick={() => appendText("ln(")}
                className="py-2 rounded-md bg-[#2d323b] hover:bg-[#383e4a] active:translate-y-0.5 text-slate-200 font-mono text-xs shadow-xs"
              >
                ln
              </button>

              {/* Function Row 3 */}
              <button
                type="button"
                onClick={() => appendText("(")}
                className="py-2 rounded-md bg-[#2d323b] hover:bg-[#383e4a] active:translate-y-0.5 text-slate-200 font-mono text-xs shadow-xs"
              >
                (
              </button>
              <button
                type="button"
                onClick={() => appendText(")")}
                className="py-2 rounded-md bg-[#2d323b] hover:bg-[#383e4a] active:translate-y-0.5 text-slate-200 font-mono text-xs shadow-xs"
              >
                )
              </button>
              <button
                type="button"
                onClick={() => appendText("π")}
                className="py-2 rounded-md bg-[#2d323b] hover:bg-[#383e4a] active:translate-y-0.5 text-slate-200 font-mono text-xs shadow-xs"
              >
                π
              </button>
              <button
                type="button"
                onClick={handleDelete}
                className="py-2 rounded-md bg-[#922b21] hover:bg-[#a93226] active:translate-y-0.5 text-white font-bold text-xs shadow-xs"
              >
                DEL
              </button>
              <button
                type="button"
                onClick={handleClear}
                className="py-2 rounded-md bg-[#c0392b] hover:bg-[#d44637] active:translate-y-0.5 text-white font-bold text-xs shadow-xs"
              >
                AC
              </button>

              {/* Numbers Row 1: 7 8 9 * / */}
              <button
                type="button"
                onClick={() => appendText("7")}
                className="py-3 rounded-md bg-[#3c414d] hover:bg-[#464c5a] active:translate-y-0.5 text-white font-bold text-base shadow-xs"
              >
                7
              </button>
              <button
                type="button"
                onClick={() => appendText("8")}
                className="py-3 rounded-md bg-[#3c414d] hover:bg-[#464c5a] active:translate-y-0.5 text-white font-bold text-base shadow-xs"
              >
                8
              </button>
              <button
                type="button"
                onClick={() => appendText("9")}
                className="py-3 rounded-md bg-[#3c414d] hover:bg-[#464c5a] active:translate-y-0.5 text-white font-bold text-base shadow-xs"
              >
                9
              </button>
              <button
                type="button"
                onClick={() => appendText(" * ")}
                className="py-3 rounded-md bg-[#272b33] hover:bg-[#313640] active:translate-y-0.5 text-amber-400 font-bold text-base shadow-xs"
              >
                ×
              </button>
              <button
                type="button"
                onClick={() => appendText(" / ")}
                className="py-3 rounded-md bg-[#272b33] hover:bg-[#313640] active:translate-y-0.5 text-amber-400 font-bold text-base shadow-xs"
              >
                ÷
              </button>

              {/* Numbers Row 2: 4 5 6 + - */}
              <button
                type="button"
                onClick={() => appendText("4")}
                className="py-3 rounded-md bg-[#3c414d] hover:bg-[#464c5a] active:translate-y-0.5 text-white font-bold text-base shadow-xs"
              >
                4
              </button>
              <button
                type="button"
                onClick={() => appendText("5")}
                className="py-3 rounded-md bg-[#3c414d] hover:bg-[#464c5a] active:translate-y-0.5 text-white font-bold text-base shadow-xs"
              >
                5
              </button>
              <button
                type="button"
                onClick={() => appendText("6")}
                className="py-3 rounded-md bg-[#3c414d] hover:bg-[#464c5a] active:translate-y-0.5 text-white font-bold text-base shadow-xs"
              >
                6
              </button>
              <button
                type="button"
                onClick={() => appendText(" + ")}
                className="py-3 rounded-md bg-[#272b33] hover:bg-[#313640] active:translate-y-0.5 text-amber-400 font-bold text-base shadow-xs"
              >
                +
              </button>
              <button
                type="button"
                onClick={() => appendText(" - ")}
                className="py-3 rounded-md bg-[#272b33] hover:bg-[#313640] active:translate-y-0.5 text-amber-400 font-bold text-base shadow-xs"
              >
                −
              </button>

              {/* Numbers Row 3: 1 2 3 Ans = */}
              <button
                type="button"
                onClick={() => appendText("1")}
                className="py-3 rounded-md bg-[#3c414d] hover:bg-[#464c5a] active:translate-y-0.5 text-white font-bold text-base shadow-xs"
              >
                1
              </button>
              <button
                type="button"
                onClick={() => appendText("2")}
                className="py-3 rounded-md bg-[#3c414d] hover:bg-[#464c5a] active:translate-y-0.5 text-white font-bold text-base shadow-xs"
              >
                2
              </button>
              <button
                type="button"
                onClick={() => appendText("3")}
                className="py-3 rounded-md bg-[#3c414d] hover:bg-[#464c5a] active:translate-y-0.5 text-white font-bold text-base shadow-xs"
              >
                3
              </button>
              <button
                type="button"
                onClick={() => appendText(lastAnswer)}
                className="py-3 rounded-md bg-[#272b33] hover:bg-[#313640] active:translate-y-0.5 text-slate-300 font-mono text-xs shadow-xs"
              >
                Ans
              </button>
              <button
                type="button"
                onClick={handleEvaluate}
                className="row-span-2 py-3 rounded-md bg-amber-500 hover:bg-amber-400 active:translate-y-0.5 text-slate-950 font-black text-xl shadow-md flex items-center justify-center"
              >
                =
              </button>

              {/* Numbers Row 4: 0 . *10^x */}
              <button
                type="button"
                onClick={() => appendText("0")}
                className="col-span-2 py-3 rounded-md bg-[#3c414d] hover:bg-[#464c5a] active:translate-y-0.5 text-white font-bold text-base shadow-xs"
              >
                0
              </button>
              <button
                type="button"
                onClick={() => appendText(".")}
                className="py-3 rounded-md bg-[#3c414d] hover:bg-[#464c5a] active:translate-y-0.5 text-white font-bold text-base shadow-xs"
              >
                .
              </button>
              <button
                type="button"
                onClick={() => appendText(" * 10^")}
                className="py-3 rounded-md bg-[#272b33] hover:bg-[#313640] active:translate-y-0.5 text-slate-300 font-mono text-xs shadow-xs"
              >
                ×10ˣ
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
