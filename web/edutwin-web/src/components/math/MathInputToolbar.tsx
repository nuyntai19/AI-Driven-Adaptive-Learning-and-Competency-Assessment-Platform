import React, { useState } from "react";
import {
  MATH_INPUT_SYMBOLS,
  resolveMathSymbolValue,
  type MathSymbolTab,
  type MathToolbarInputMode,
} from "./mathInputSymbols";

const TABS: { id: MathSymbolTab; name: string }[] = [
  { id: "basic", name: "Cơ bản" },
  { id: "algebra", name: "Đại số" },
  { id: "calculus", name: "Giải tích" },
  { id: "sets", name: "Tập hợp & Logic" },
  { id: "geometry", name: "Hình học & Hy Lạp" },
];

export interface MathInputToolbarProps {
  onInsert: (symbol: string) => void;
  className?: string;
  disabled?: boolean;
  inputMode?: MathToolbarInputMode;
}

export const MathInputToolbar: React.FC<MathInputToolbarProps> = ({
  onInsert,
  className = "",
  disabled = false,
  inputMode = "latex",
}) => {
  const [activeTab, setActiveTab] = useState<MathSymbolTab>("basic");

  return (
    <div
      className={`rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-sm overflow-hidden ${className} ${
        disabled ? "opacity-60" : ""
      }`}
      aria-label="Math input toolbar"
    >
      {/* Tabs */}
      <div className="flex border-b border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-950 px-2 pt-1 gap-1 overflow-x-auto text-xs">
        {TABS.map((tab) => (
          <button
            key={tab.id}
            type="button"
            disabled={disabled}
            onMouseDown={(e) => e.preventDefault()}
            onClick={() => setActiveTab(tab.id)}
            className={`px-2.5 py-1.5 font-medium rounded-t-md transition-colors whitespace-nowrap ${
              activeTab === tab.id
                ? "bg-white dark:bg-slate-900 text-blue-600 dark:text-blue-400 border-t-2 border-blue-600 dark:border-blue-400 font-semibold"
                : "text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-200"
            }`}
          >
            {tab.name}
          </button>
        ))}
      </div>

      {!disabled && inputMode === "visual" && (
        <div className="px-3 pt-2 text-[11px] leading-relaxed text-slate-500 dark:text-slate-400">
          Các mẫu có ô <strong className="text-indigo-600 dark:text-indigo-400">[?]</strong>: nhập giá trị rồi dùng
          <kbd className="mx-1 rounded border border-slate-300 dark:border-slate-700 px-1 py-0.5 font-sans">Tab</kbd>
          hoặc phím mũi tên để sang ô kế tiếp.
        </div>
      )}

      {/* Symbol buttons */}
      <div className="p-2 flex flex-wrap gap-1.5 max-h-32 overflow-y-auto">
        {MATH_INPUT_SYMBOLS[activeTab].map((sym) => (
          <button
            key={`${activeTab}-${sym.label}`}
            type="button"
            title={sym.tooltip || sym.label}
            disabled={disabled}
            onMouseDown={(e) => e.preventDefault()}
            onClick={() => {
              if (!disabled) onInsert(resolveMathSymbolValue(sym, inputMode));
            }}
            className={`min-w-8 h-8 px-2 flex items-center justify-center text-xs font-mono font-medium rounded border transition-colors ${
              disabled
                ? "border-slate-200 dark:border-slate-800 bg-slate-100 dark:bg-slate-800 text-slate-400 cursor-not-allowed"
                : "border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 hover:bg-blue-50 dark:hover:bg-blue-900/30 hover:border-blue-300 dark:hover:border-blue-700 text-slate-800 dark:text-slate-200 active:scale-95 cursor-pointer"
            }`}
          >
            {sym.label}
          </button>
        ))}
      </div>
    </div>
  );
};
