import React, { useState } from "react";

export interface MathInputToolbarProps {
  onInsert: (symbol: string) => void;
  className?: string;
}

type TabType = "basic" | "algebra" | "calculus" | "sets" | "geometry";

interface SymbolItem {
  label: string;
  latex: string;
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
    { label: "a/b", latex: "\\frac{\\placeholder{?}}{\\placeholder{?}}", tooltip: "Phân số" },
    { label: "x²", latex: "^{2}", tooltip: "Bình phương" },
    { label: "xⁿ", latex: "^{\\placeholder{?}}", tooltip: "Số mũ" },
    { label: "√x", latex: "\\sqrt{\\placeholder{?}}", tooltip: "Căn bậc hai" },
    { label: "ⁿ√x", latex: "\\sqrt[\\placeholder{?}]{\\placeholder{?}}", tooltip: "Căn bậc n" },
    { label: "+", latex: " + ", tooltip: "Cộng" },
    { label: "−", latex: " - ", tooltip: "Trừ" },
    { label: "×", latex: " \\times ", tooltip: "Nhân" },
    { label: "÷", latex: " \\div ", tooltip: "Chia" },
    { label: "±", latex: "\\pm", tooltip: "Cộng trừ" },
    { label: "=", latex: " = ", tooltip: "Bằng" },
    { label: "≠", latex: " \\ne ", tooltip: "Khác" },
    { label: "<", latex: " < ", tooltip: "Nhỏ hơn" },
    { label: ">", latex: " > ", tooltip: "Lớn hơn" },
    { label: "≤", latex: " \\le ", tooltip: "Nhỏ hơn hoặc bằng" },
    { label: "≥", latex: " \\ge ", tooltip: "Lớn hơn hoặc bằng" },
  ],
  algebra: [
    { label: "x", latex: "x", tooltip: "Biến x" },
    { label: "y", latex: "y", tooltip: "Biến y" },
    { label: "z", latex: "z", tooltip: "Biến z" },
    { label: "( )", latex: "(\\placeholder{?})", tooltip: "Ngoặc đơn" },
    { label: "[ ]", latex: "[\\placeholder{?}]", tooltip: "Ngoặc vuông" },
    { label: "{ }", latex: "\\{\\placeholder{?}\\}", tooltip: "Ngoặc nhọn" },
    { label: "|x|", latex: "|\\placeholder{?}|", tooltip: "Giá trị tuyệt đối" },
    { label: "∑", latex: "\\sum_{\\placeholder{?}}^{\\placeholder{?}}", tooltip: "Tổng xích-ma" },
    { label: "∏", latex: "\\prod_{\\placeholder{?}}^{\\placeholder{?}}", tooltip: "Tích pi" },
    { label: "∞", latex: "\\infty", tooltip: "Vô cùng" },
    { label: "≈", latex: " \\approx ", tooltip: "Xấp xỉ" },
    { label: "log", latex: "\\log_{\\placeholder{?}}(\\placeholder{?})", tooltip: "Logarit" },
    { label: "ln", latex: "\\ln(\\placeholder{?})", tooltip: "Logarit tự nhiên" },
  ],
  calculus: [
    { label: "d/dx", latex: "\\frac{d}{dx}(\\placeholder{?})", tooltip: "Đạo hàm d/dx" },
    { label: "∂/∂x", latex: "\\frac{\\partial}{\\partial x}(\\placeholder{?})", tooltip: "Đạo hàm riêng" },
    { label: "∫", latex: "\\int \\placeholder{?} \\,dx", tooltip: "Tích phân bất định" },
    { label: "∫ₐᵇ", latex: "\\int_{\\placeholder{?}}^{\\placeholder{?}} \\placeholder{?} \\,dx", tooltip: "Tích phân xác định" },
    { label: "lim", latex: "\\lim_{\\placeholder{?} \\to \\placeholder{?}} \\placeholder{?}", tooltip: "Giới hạn" },
    { label: "∇", latex: "\\nabla", tooltip: "Del / Nabla" },
    { label: "Δ", latex: "\\Delta", tooltip: "Delta" },
    { label: "f'(x)", latex: "f'(\\placeholder{?})", tooltip: "Đạo hàm f'(x)" },
  ],
  sets: [
    { label: "∈", latex: " \\in ", tooltip: "Thuộc" },
    { label: "∉", latex: " \\notin ", tooltip: "Không thuộc" },
    { label: "⊂", latex: " \\subset ", tooltip: "Tập con" },
    { label: "⊆", latex: " \\subseteq ", tooltip: "Tập con hoặc bằng" },
    { label: "∪", latex: " \\cup ", tooltip: "Hợp" },
    { label: "∩", latex: " \\cap ", tooltip: "Giao" },
    { label: "∅", latex: "\\emptyset", tooltip: "Tập rỗng" },
    { label: "ℝ", latex: "\\mathbb{R}", tooltip: "Tập số thực R" },
    { label: "ℕ", latex: "\\mathbb{N}", tooltip: "Tập số tự nhiên N" },
    { label: "ℤ", latex: "\\mathbb{Z}", tooltip: "Tập số nguyên Z" },
    { label: "ℚ", latex: "\\mathbb{Q}", tooltip: "Tập số hữu tỉ Q" },
    { label: "⇒", latex: " \\implies ", tooltip: "Suy ra" },
    { label: "⇔", latex: " \\iff ", tooltip: "Tương đương" },
  ],
  geometry: [
    { label: "π", latex: "\\pi", tooltip: "Số Pi" },
    { label: "θ", latex: "\\theta", tooltip: "Góc Theta" },
    { label: "α", latex: "\\alpha", tooltip: "Góc Alpha" },
    { label: "β", latex: "\\beta", tooltip: "Góc Beta" },
    { label: "γ", latex: "\\gamma", tooltip: "Góc Gamma" },
    { label: "λ", latex: "\\lambda", tooltip: "Góc Lambda" },
    { label: "°", latex: "^{\\circ}", tooltip: "Độ" },
    { label: "∠", latex: "\\angle", tooltip: "Góc" },
    { label: "△", latex: "\\triangle", tooltip: "Tam giác" },
    { label: "∥", latex: " \\parallel ", tooltip: "Song song" },
    { label: "⊥", latex: " \\perp ", tooltip: "Vuông góc" },
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
              if (!disabled) onInsert(sym.latex);
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
