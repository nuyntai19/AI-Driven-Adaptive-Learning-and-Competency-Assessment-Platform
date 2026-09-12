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
  { id: "basic", name: "Basic" },
  { id: "algebra", name: "Algebra" },
  { id: "calculus", name: "Calculus" },
  { id: "sets", name: "Sets & Logic" },
  { id: "geometry", name: "Geometry & Greek" },
];

const SYMBOLS: Record<TabType, SymbolItem[]> = {
  basic: [
    { label: "a/b", latex: "\\frac{a}{b}", tooltip: "Fraction" },
    { label: "x²", latex: "^{2}", tooltip: "Square" },
    { label: "xⁿ", latex: "^{n}", tooltip: "Power" },
    { label: "√x", latex: "\\sqrt{x}", tooltip: "Square root" },
    { label: "ⁿ√x", latex: "\\sqrt[n]{x}", tooltip: "N-th root" },
    { label: "+", latex: " + ", tooltip: "Plus" },
    { label: "−", latex: " - ", tooltip: "Minus" },
    { label: "×", latex: " \\times ", tooltip: "Times" },
    { label: "÷", latex: " \\div ", tooltip: "Divide" },
    { label: "±", latex: "\\pm", tooltip: "Plus-minus" },
    { label: "=", latex: " = ", tooltip: "Equals" },
    { label: "≠", latex: " \\ne ", tooltip: "Not equal" },
    { label: "<", latex: " < ", tooltip: "Less than" },
    { label: ">", latex: " > ", tooltip: "Greater than" },
    { label: "≤", latex: " \\le ", tooltip: "Less than or equal" },
    { label: "≥", latex: " \\ge ", tooltip: "Greater than or equal" },
  ],
  algebra: [
    { label: "x", latex: "x", tooltip: "x" },
    { label: "y", latex: "y", tooltip: "y" },
    { label: "z", latex: "z", tooltip: "z" },
    { label: "( )", latex: "()", tooltip: "Parentheses" },
    { label: "[ ]", latex: "[]", tooltip: "Brackets" },
    { label: "{ }", latex: "\\{\\}", tooltip: "Braces" },
    { label: "|x|", latex: "|x|", tooltip: "Absolute value" },
    { label: "∑", latex: "\\sum_{i=1}^{n}", tooltip: "Summation" },
    { label: "∏", latex: "\\prod_{i=1}^{n}", tooltip: "Product" },
    { label: "∞", latex: "\\infty", tooltip: "Infinity" },
    { label: "≈", latex: " \\approx ", tooltip: "Approximately" },
    { label: "log", latex: "\\log(x)", tooltip: "Logarithm" },
    { label: "ln", latex: "\\ln(x)", tooltip: "Natural log" },
  ],
  calculus: [
    { label: "d/dx", latex: "\\frac{d}{dx}", tooltip: "Derivative" },
    { label: "∂/∂x", latex: "\\frac{\\partial}{\\partial x}", tooltip: "Partial derivative" },
    { label: "∫", latex: "\\int", tooltip: "Indefinite integral" },
    { label: "∫ₐᵇ", latex: "\\int_{a}^{b} f(x) \\,dx", tooltip: "Definite integral" },
    { label: "lim", latex: "\\lim_{x \\to 0}", tooltip: "Limit" },
    { label: "∇", latex: "\\nabla", tooltip: "Del / Nabla" },
    { label: "Δ", latex: "\\Delta", tooltip: "Delta" },
    { label: "f'(x)", latex: "f'(x)", tooltip: "Prime notation" },
  ],
  sets: [
    { label: "∈", latex: " \\in ", tooltip: "Element of" },
    { label: "∉", latex: " \\notin ", tooltip: "Not element of" },
    { label: "⊂", latex: " \\subset ", tooltip: "Subset" },
    { label: "⊆", latex: " \\subseteq ", tooltip: "Subset or equal" },
    { label: "∪", latex: " \\cup ", tooltip: "Union" },
    { label: "∩", latex: " \\cap ", tooltip: "Intersection" },
    { label: "∅", latex: "\\emptyset", tooltip: "Empty set" },
    { label: "ℝ", latex: "\\mathbb{R}", tooltip: "Real numbers" },
    { label: "ℕ", latex: "\\mathbb{N}", tooltip: "Natural numbers" },
    { label: "ℤ", latex: "\\mathbb{Z}", tooltip: "Integers" },
    { label: "ℚ", latex: "\\mathbb{Q}", tooltip: "Rationals" },
    { label: "⇒", latex: " \\implies ", tooltip: "Implies" },
    { label: "⇔", latex: " \\iff ", tooltip: "If and only if" },
  ],
  geometry: [
    { label: "π", latex: "\\pi", tooltip: "Pi" },
    { label: "θ", latex: "\\theta", tooltip: "Theta" },
    { label: "α", latex: "\\alpha", tooltip: "Alpha" },
    { label: "β", latex: "\\beta", tooltip: "Beta" },
    { label: "γ", latex: "\\gamma", tooltip: "Gamma" },
    { label: "λ", latex: "\\lambda", tooltip: "Lambda" },
    { label: "°", latex: "^{\\circ}", tooltip: "Degree" },
    { label: "∠", latex: "\\angle", tooltip: "Angle" },
    { label: "△", latex: "\\triangle", tooltip: "Triangle" },
    { label: "∥", latex: " \\parallel ", tooltip: "Parallel" },
    { label: "⊥", latex: " \\perp ", tooltip: "Perpendicular" },
  ],
};

export const MathInputToolbar: React.FC<MathInputToolbarProps> = ({
  onInsert,
  className = "",
}) => {
  const [activeTab, setActiveTab] = useState<TabType>("basic");

  return (
    <div
      className={`rounded-lg border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-sm overflow-hidden ${className}`}
      aria-label="Math input toolbar"
    >
      {/* Tabs */}
      <div className="flex border-b border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-950 px-2 pt-1 gap-1 overflow-x-auto text-xs">
        {TABS.map((tab) => (
          <button
            key={tab.id}
            type="button"
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
            onClick={() => onInsert(sym.latex)}
            className="min-w-8 h-8 px-2 flex items-center justify-center text-xs font-mono font-medium rounded border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 hover:bg-blue-50 dark:hover:bg-blue-900/30 hover:border-blue-300 dark:hover:border-blue-700 text-slate-800 dark:text-slate-200 transition-colors active:scale-95"
          >
            {sym.label}
          </button>
        ))}
      </div>
    </div>
  );
};
