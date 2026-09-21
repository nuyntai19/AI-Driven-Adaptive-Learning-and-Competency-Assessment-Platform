import React, { useState } from "react";

export interface MathInputToolbarProps {
  onInsert: (symbol: string) => void;
  className?: string;
}

type TabType = "basic" | "algebra" | "calculus" | "sets" | "geometry";

interface SymbolItem {
  label: string;
  value: string;
  tooltip?: string;
}

const TABS: { id: TabType; name: string }[] = [
  { id: "basic", name: "Cơ bản" },
  { id: "algebra", name: "Đại số" },
  { id: "calculus", name: "Giải tích" },
  { id: "sets", name: "Tập hợp & Logic" },
  { id: "geometry", name: "Hình học & Hy Lạp" },
];

const SYMBOLS: Record<TabType, SymbolItem[]> = {
  basic: [
    { label: "a/b", value: "□/□", tooltip: "Phân số" },
    { label: "x²", value: "²", tooltip: "Bình phương" },
    { label: "x³", value: "³", tooltip: "Lập phương" },
    { label: "√x", value: "√", tooltip: "Căn bậc hai" },
    { label: "+", value: " + ", tooltip: "Cộng" },
    { label: "−", value: " − ", tooltip: "Trừ" },
    { label: "×", value: " × ", tooltip: "Nhân" },
    { label: "÷", value: " ÷ ", tooltip: "Chia" },
    { label: "±", value: " ± ", tooltip: "Cộng trừ" },
    { label: "=", value: " = ", tooltip: "Bằng" },
    { label: "≠", value: " ≠ ", tooltip: "Khác" },
    { label: "<", value: " < ", tooltip: "Nhỏ hơn" },
    { label: ">", value: " > ", tooltip: "Lớn hơn" },
    { label: "≤", value: " ≤ ", tooltip: "Nhỏ hơn hoặc bằng" },
    { label: "≥", value: " ≥ ", tooltip: "Lớn hơn hoặc bằng" },
  ],
  algebra: [
    { label: "x", value: "x" }, { label: "y", value: "y" }, { label: "z", value: "z" },
    { label: "( )", value: "()" }, { label: "[ ]", value: "[]" }, { label: "{ }", value: "{}" },
    { label: "|x|", value: "||" }, { label: "∑", value: "∑" }, { label: "∏", value: "∏" },
    { label: "∞", value: "∞" }, { label: "≈", value: " ≈ " }, { label: "log", value: "log()" }, { label: "ln", value: "ln()" },
  ],
  calculus: [
    { label: "d/dx", value: "d/dx()" }, { label: "∂/∂x", value: "∂/∂x()" },
    { label: "∫", value: "∫ dx" }, { label: "∫ₐᵇ", value: "∫ₐᵇ dx" }, { label: "lim", value: "lim → " },
    { label: "∇", value: "∇" }, { label: "Δ", value: "Δ" }, { label: "f'(x)", value: "f'(x)" },
  ],
  sets: [
    { label: "∈", value: " ∈ " }, { label: "∉", value: " ∉ " }, { label: "⊂", value: " ⊂ " },
    { label: "⊆", value: " ⊆ " }, { label: "∪", value: " ∪ " }, { label: "∩", value: " ∩ " },
    { label: "∅", value: "∅" }, { label: "ℝ", value: "ℝ" }, { label: "ℕ", value: "ℕ" },
    { label: "ℤ", value: "ℤ" }, { label: "ℚ", value: "ℚ" }, { label: "⇒", value: " ⇒ " }, { label: "⇔", value: " ⇔ " },
  ],
  geometry: [
    { label: "π", value: "π" }, { label: "θ", value: "θ" }, { label: "α", value: "α" },
    { label: "β", value: "β" }, { label: "γ", value: "γ" }, { label: "λ", value: "λ" },
    { label: "°", value: "°" }, { label: "∠", value: "∠" }, { label: "△", value: "△" },
    { label: "∥", value: " ∥ " }, { label: "⊥", value: " ⊥ " },
  ],
};

export interface MathInputToolbarProps {
  onInsert: (symbol: string) => void;
  className?: string;
  disabled?: boolean;
}

export const MathInputToolbar: React.FC<MathInputToolbarProps> = ({
  onInsert,
  className = "",
  disabled = false,
}) => {
  const [activeTab, setActiveTab] = useState<TabType>("basic");

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

      {/* Symbol buttons */}
      <div className="p-2 flex flex-wrap gap-1.5 max-h-32 overflow-y-auto">
        {SYMBOLS[activeTab].map((sym, index) => (
          <button
            key={index}
            type="button"
            title={sym.tooltip || sym.label}
            disabled={disabled}
            onMouseDown={(e) => e.preventDefault()}
            onClick={() => {
              if (!disabled) onInsert(sym.value);
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
