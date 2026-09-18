import React, { useState, useMemo, useRef, useEffect } from "react";
import { CalculatorEngine, type AngleMode } from "../../utils/calculatorEngine";

export interface CasioInlinePanelProps {
  onInsertResult?: (result: string) => void;
}

export const CasioInlinePanel: React.FC<CasioInlinePanelProps> = ({ onInsertResult }) => {
  // Default to the built-in authentic Casio fx-580VN engine for instant 1-click insertion
  const [activeTab, setActiveTab] = useState<"offline" | "online">("offline");
  const [expression, setExpression] = useState("");
  const [result, setResult] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [angleMode, setAngleMode] = useState<AngleMode>("deg");
  const [lastAnswer, setLastAnswer] = useState<string>("0");
  const [manualInsertVal, setManualInsertVal] = useState<string>("");
  const [justInserted, setJustInserted] = useState<boolean>(false);

  const inputRef = useRef<HTMLInputElement>(null);
  const engine = useMemo(() => new CalculatorEngine(angleMode), [angleMode]);

  useEffect(() => {
    if (activeTab === "offline") {
      setTimeout(() => inputRef.current?.focus(), 150);
    }
  }, [activeTab]);

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
    setJustInserted(false);
    inputRef.current?.focus();
  };

  const handleClear = () => {
    setExpression("");
    setResult(null);
    setError(null);
    setJustInserted(false);
    inputRef.current?.focus();
  };

  const handleDelete = () => {
    setExpression((prev) => prev.slice(0, -1));
    setError(null);
    inputRef.current?.focus();
  };

  const handleQuickInsert = (valToInsert: string) => {
    if (!onInsertResult || !valToInsert) return;
    onInsertResult(valToInsert);
    setJustInserted(true);
    setTimeout(() => setJustInserted(false), 2500);
  };

  return (
    <div className="flex flex-col h-full bg-[#181b20] rounded-2xl overflow-hidden border border-slate-700/80 shadow-2xl text-slate-100 select-none">
      {/* Sub-header tabs: Casio Tích hợp vs CalcES Web */}
      <div className="flex items-center justify-between px-3.5 py-2.5 bg-[#121418] border-b border-slate-700/70">
        <div className="flex items-center gap-2">
          <span className="text-xs font-black font-mono text-amber-400 flex items-center gap-1.5">
            <span className="text-base">🖩</span> CASIO fx-580VN X CLASSWIZ
          </span>
        </div>

        <div className="flex items-center bg-slate-800 p-0.5 rounded-xl border border-slate-700 text-xs">
          <button
            type="button"
            onClick={() => setActiveTab("offline")}
            className={`px-3 py-1 rounded-lg font-bold transition-colors cursor-pointer ${
              activeTab === "offline"
                ? "bg-amber-500 text-slate-950 shadow-xs"
                : "text-slate-400 hover:text-white"
            }`}
          >
            Casio Tích hợp
          </button>
          <button
            type="button"
            onClick={() => setActiveTab("online")}
            className={`px-3 py-1 rounded-lg font-bold transition-colors cursor-pointer ${
              activeTab === "online"
                ? "bg-amber-500 text-slate-950 shadow-xs"
                : "text-slate-400 hover:text-white"
            }`}
          >
            Mô phỏng Web
          </button>
        </div>
      </div>

      {/* Tab 1: Casio Tích Hợp (Default - Có nút tự chèn 1 chạm) */}
      {activeTab === "offline" && (
        <div className="flex flex-col flex-1 p-3.5 bg-[#1a1e24] overflow-y-auto space-y-3">
          {/* LCD Screen authentic Casio style */}
          <div className="rounded-xl bg-[#c5d5b7] p-3.5 shadow-inner border-2 border-[#8a997f] text-slate-900 font-mono">
            <div className="flex justify-between text-[11px] text-slate-700 border-b border-[#a4b595] pb-1 mb-1.5 font-sans font-semibold">
              <span className="tracking-wide">CASIO fx-580VN X</span>
              <div className="flex gap-2.5 font-bold items-center">
                <button
                  type="button"
                  onClick={() => setAngleMode(angleMode === "deg" ? "rad" : "deg")}
                  className="cursor-pointer hover:underline text-indigo-900 font-extrabold px-1 bg-[#b6c7a7] rounded"
                  title="Chuyển đổi góc Độ (DEG) và Radian (RAD)"
                >
                  [{angleMode.toUpperCase()}]
                </button>
                <span className="text-[10px] text-slate-600">MATH</span>
              </div>
            </div>

            {/* Expression line */}
            <div className="min-h-[26px] text-sm text-slate-800 truncate tracking-wide font-mono font-medium">
              {expression || "0"}
            </div>

            {/* Result line */}
            <div className="min-h-[38px] text-2xl font-black text-right text-slate-950 flex items-center justify-end tracking-wider">
              {error ? (
                <span className="text-rose-700 text-xs font-bold font-sans">{error}</span>
              ) : (
                result !== null ? result : ""
              )}
            </div>
          </div>

          {/* ⚡ 1-Click Auto Insert Result Button (To & Nổi bật ngay dưới LCD) */}
          {result !== null && onInsertResult && (
            <div className="animate-in fade-in zoom-in-95 duration-150">
              <button
                type="button"
                onClick={() => handleQuickInsert(result)}
                className={`w-full py-2.5 px-4 rounded-xl font-extrabold text-sm flex items-center justify-center gap-2 shadow-lg transition-all cursor-pointer ${
                  justInserted
                    ? "bg-emerald-500 text-white ring-2 ring-emerald-300"
                    : "bg-gradient-to-r from-amber-500 to-amber-400 hover:from-amber-400 hover:to-amber-300 text-slate-950 shadow-amber-500/25 active:scale-[0.98]"
                }`}
              >
                {justInserted ? (
                  <>
                    <span className="text-base">✓</span>
                    <span>Đã chèn {result} vào bài làm!</span>
                  </>
                ) : (
                  <>
                    <span className="text-base">⚡</span>
                    <span>Chèn ngay kết quả: <strong className="underline decoration-2">{result}</strong> vào bài làm</span>
                  </>
                )}
              </button>
            </div>
          )}

          {/* Keypad Grid (Buttons lớn, sắc nét, phản hồi tốt) */}
          <div className="grid grid-cols-5 gap-1.5 text-xs font-bold flex-1">
            {/* Row 1: Scientific Functions */}
            <button type="button" onClick={() => appendText("sin(")} className="p-2.5 rounded-xl bg-slate-700 hover:bg-slate-600 text-slate-200 transition-colors cursor-pointer text-xs">sin</button>
            <button type="button" onClick={() => appendText("cos(")} className="p-2.5 rounded-xl bg-slate-700 hover:bg-slate-600 text-slate-200 transition-colors cursor-pointer text-xs">cos</button>
            <button type="button" onClick={() => appendText("tan(")} className="p-2.5 rounded-xl bg-slate-700 hover:bg-slate-600 text-slate-200 transition-colors cursor-pointer text-xs">tan</button>
            <button type="button" onClick={() => appendText("sqrt(")} className="p-2.5 rounded-xl bg-slate-700 hover:bg-slate-600 text-slate-200 transition-colors cursor-pointer text-sm">√</button>
            <button type="button" onClick={() => appendText("^2")} className="p-2.5 rounded-xl bg-slate-700 hover:bg-slate-600 text-slate-200 transition-colors cursor-pointer text-xs">x²</button>

            {/* Row 2: Advanced Math */}
            <button type="button" onClick={() => appendText("log(")} className="p-2.5 rounded-xl bg-slate-700 hover:bg-slate-600 text-slate-200 transition-colors cursor-pointer text-xs">log</button>
            <button type="button" onClick={() => appendText("ln(")} className="p-2.5 rounded-xl bg-slate-700 hover:bg-slate-600 text-slate-200 transition-colors cursor-pointer text-xs">ln</button>
            <button type="button" onClick={() => appendText("(")} className="p-2.5 rounded-xl bg-slate-700 hover:bg-slate-600 text-slate-200 transition-colors cursor-pointer text-sm">(</button>
            <button type="button" onClick={() => appendText(")")} className="p-2.5 rounded-xl bg-slate-700 hover:bg-slate-600 text-slate-200 transition-colors cursor-pointer text-sm">)</button>
            <button type="button" onClick={() => appendText("^")} className="p-2.5 rounded-xl bg-slate-700 hover:bg-slate-600 text-slate-200 transition-colors cursor-pointer text-xs">xʸ</button>

            {/* Row 3: 7, 8, 9, DEL, AC */}
            <button type="button" onClick={() => appendText("7")} className="p-3 rounded-xl bg-slate-800 hover:bg-slate-700 text-white font-extrabold cursor-pointer text-sm shadow-2xs">7</button>
            <button type="button" onClick={() => appendText("8")} className="p-3 rounded-xl bg-slate-800 hover:bg-slate-700 text-white font-extrabold cursor-pointer text-sm shadow-2xs">8</button>
            <button type="button" onClick={() => appendText("9")} className="p-3 rounded-xl bg-slate-800 hover:bg-slate-700 text-white font-extrabold cursor-pointer text-sm shadow-2xs">9</button>
            <button type="button" onClick={handleDelete} className="p-3 rounded-xl bg-amber-600 hover:bg-amber-500 text-white font-black cursor-pointer text-xs shadow-2xs">DEL</button>
            <button type="button" onClick={handleClear} className="p-3 rounded-xl bg-rose-600 hover:bg-rose-500 text-white font-black cursor-pointer text-xs shadow-2xs">AC</button>

            {/* Row 4: 4, 5, 6, *, / */}
            <button type="button" onClick={() => appendText("4")} className="p-3 rounded-xl bg-slate-800 hover:bg-slate-700 text-white font-extrabold cursor-pointer text-sm shadow-2xs">4</button>
            <button type="button" onClick={() => appendText("5")} className="p-3 rounded-xl bg-slate-800 hover:bg-slate-700 text-white font-extrabold cursor-pointer text-sm shadow-2xs">5</button>
            <button type="button" onClick={() => appendText("6")} className="p-3 rounded-xl bg-slate-800 hover:bg-slate-700 text-white font-extrabold cursor-pointer text-sm shadow-2xs">6</button>
            <button type="button" onClick={() => appendText(" * ")} className="p-3 rounded-xl bg-slate-700 hover:bg-slate-600 text-amber-300 font-bold cursor-pointer text-base">×</button>
            <button type="button" onClick={() => appendText(" / ")} className="p-3 rounded-xl bg-slate-700 hover:bg-slate-600 text-amber-300 font-bold cursor-pointer text-base">÷</button>

            {/* Row 5: 1, 2, 3, +, - */}
            <button type="button" onClick={() => appendText("1")} className="p-3 rounded-xl bg-slate-800 hover:bg-slate-700 text-white font-extrabold cursor-pointer text-sm shadow-2xs">1</button>
            <button type="button" onClick={() => appendText("2")} className="p-3 rounded-xl bg-slate-800 hover:bg-slate-700 text-white font-extrabold cursor-pointer text-sm shadow-2xs">2</button>
            <button type="button" onClick={() => appendText("3")} className="p-3 rounded-xl bg-slate-800 hover:bg-slate-700 text-white font-extrabold cursor-pointer text-sm shadow-2xs">3</button>
            <button type="button" onClick={() => appendText(" + ")} className="p-3 rounded-xl bg-slate-700 hover:bg-slate-600 text-amber-300 font-bold cursor-pointer text-base">+</button>
            <button type="button" onClick={() => appendText(" - ")} className="p-3 rounded-xl bg-slate-700 hover:bg-slate-600 text-amber-300 font-bold cursor-pointer text-base">−</button>

            {/* Row 6: 0, ., *10^x, Ans, = */}
            <button type="button" onClick={() => appendText("0")} className="p-3 rounded-xl bg-slate-800 hover:bg-slate-700 text-white font-extrabold cursor-pointer text-sm shadow-2xs">0</button>
            <button type="button" onClick={() => appendText(".")} className="p-3 rounded-xl bg-slate-800 hover:bg-slate-700 text-white font-extrabold cursor-pointer text-sm shadow-2xs">.</button>
            <button type="button" onClick={() => appendText(" * 10^")} className="p-3 rounded-xl bg-slate-800 hover:bg-slate-700 text-slate-300 font-bold cursor-pointer text-[10px]">×10ˣ</button>
            <button type="button" onClick={() => appendText(lastAnswer)} className="p-3 rounded-xl bg-slate-800 hover:bg-slate-700 text-amber-300 font-bold cursor-pointer text-xs">Ans</button>
            <button type="button" onClick={handleEvaluate} className="p-3 rounded-xl bg-emerald-600 hover:bg-emerald-500 text-white font-black cursor-pointer text-lg shadow-emerald-600/30">=</button>
          </div>
        </div>
      )}

      {/* Tab 2: CalcES Web Iframe (Giữ tùy chọn mô phỏng web nếu học sinh thích) */}
      {activeTab === "online" && (
        <div className="flex flex-col flex-1 bg-[#121418]">
          <iframe
            src="https://mathda.com/calculator/vi"
            title="Máy tính Casio fx-580VN X (CalcES)"
            className="w-full flex-1 border-0 min-h-[500px]"
            allow="camera; microphone; clipboard-write; clipboard-read"
          />

          {/* Quick manual insert helper */}
          {onInsertResult && (
            <div className="p-3 bg-[#16191f] border-t border-slate-700/80 flex items-center justify-between gap-2">

              <div className="flex items-center gap-1.5 flex-1 min-w-[200px]">
                <input
                  type="text"
                  value={manualInsertVal}
                  onChange={(e) => setManualInsertVal(e.target.value)}
                  placeholder="Nhập hoặc dán kết quả..."
                  className="flex-1 px-2.5 py-1 text-xs bg-slate-900 border border-slate-700 rounded-xl text-amber-300 font-mono focus:outline-none focus:border-amber-400"
                />
                <button
                  type="button"
                  disabled={!manualInsertVal.trim()}
                  onClick={() => {
                    handleQuickInsert(manualInsertVal.trim());
                    setManualInsertVal("");
                  }}
                  className="px-3 py-1 bg-amber-500 hover:bg-amber-400 disabled:opacity-40 text-slate-950 font-bold rounded-xl text-xs transition-colors cursor-pointer shrink-0"
                >
                  Chèn ➔
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
};
