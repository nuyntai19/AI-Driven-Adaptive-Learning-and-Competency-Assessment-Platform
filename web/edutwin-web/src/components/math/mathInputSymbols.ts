export type MathSymbolTab = "basic" | "algebra" | "calculus" | "sets" | "geometry";

export interface MathSymbolItem {
  label: string;
  /** Interactive MathLive insertion template. */
  value: string;
  /** Valid, immediately renderable LaTeX for legacy textarea consumers. */
  textValue?: string;
  tooltip?: string;
}

export type MathToolbarInputMode = "visual" | "latex";

export const resolveMathSymbolValue = (symbol: MathSymbolItem, mode: MathToolbarInputMode): string =>
  mode === "visual" ? symbol.value : symbol.textValue ?? symbol.value.split("#?").join("x");

/**
 * MathLive templates use #? as an interactive placeholder. The first empty
 * slot is selected after insertion; Arrow keys or Tab move to the next slot.
 * The same values remain valid LaTeX when a toolbar is attached to a textarea.
 */
export const MATH_INPUT_SYMBOLS: Record<MathSymbolTab, MathSymbolItem[]> = {
  basic: [
    { label: "a/b", value: String.raw`\frac{#?}{#?}`, textValue: String.raw`\frac{a}{b}`, tooltip: "Phân số — điền tử số và mẫu số" },
    { label: "x²", value: String.raw`{#?}^{2}`, textValue: "x^{2}", tooltip: "Bình phương" },
    { label: "x³", value: String.raw`{#?}^{3}`, textValue: "x^{3}", tooltip: "Lập phương" },
    { label: "xⁿ", value: String.raw`{#?}^{#?}`, textValue: "x^{n}", tooltip: "Lũy thừa tùy ý" },
    { label: "√x", value: String.raw`\sqrt{#?}`, textValue: String.raw`\sqrt{x}`, tooltip: "Căn bậc hai" },
    { label: "∛x", value: String.raw`\sqrt[3]{#?}`, textValue: String.raw`\sqrt[3]{x}`, tooltip: "Căn bậc ba" },
    { label: "∜x", value: String.raw`\sqrt[4]{#?}`, textValue: String.raw`\sqrt[4]{x}`, tooltip: "Căn bậc bốn" },
    { label: "ⁿ√x", value: String.raw`\sqrt[#?]{#?}`, textValue: String.raw`\sqrt[n]{x}`, tooltip: "Căn bậc tùy ý — điền bậc căn rồi biểu thức" },
    { label: "+", value: "+", tooltip: "Cộng" },
    { label: "−", value: "-", tooltip: "Trừ" },
    { label: "×", value: String.raw`\times`, tooltip: "Nhân" },
    { label: "÷", value: String.raw`\div`, tooltip: "Chia" },
    { label: "±", value: String.raw`\pm`, tooltip: "Cộng trừ" },
    { label: "=", value: "=", tooltip: "Bằng" },
    { label: "≠", value: String.raw`\ne`, tooltip: "Khác" },
    { label: "<", value: "<", tooltip: "Nhỏ hơn" },
    { label: ">", value: ">", tooltip: "Lớn hơn" },
    { label: "≤", value: String.raw`\le`, tooltip: "Nhỏ hơn hoặc bằng" },
    { label: "≥", value: String.raw`\ge`, tooltip: "Lớn hơn hoặc bằng" },
  ],
  algebra: [
    { label: "x", value: "x" },
    { label: "y", value: "y" },
    { label: "z", value: "z" },
    { label: "( )", value: String.raw`\left(#?\right)`, textValue: String.raw`\left(x\right)`, tooltip: "Ngoặc tròn tự co giãn" },
    { label: "[ ]", value: String.raw`\left[#?\right]`, textValue: String.raw`\left[x\right]`, tooltip: "Ngoặc vuông tự co giãn" },
    { label: "{ }", value: String.raw`\left\{#?\right\}`, textValue: String.raw`\left\{x\right\}`, tooltip: "Ngoặc nhọn tự co giãn" },
    { label: "|x|", value: String.raw`\left|#?\right|`, textValue: String.raw`\left|x\right|`, tooltip: "Giá trị tuyệt đối" },
    { label: "∑", value: String.raw`\sum_{#?}^{#?}#?`, textValue: String.raw`\sum_{i=1}^{n}x_i`, tooltip: "Tổng — cận dưới, cận trên và biểu thức" },
    { label: "∏", value: String.raw`\prod_{#?}^{#?}#?`, textValue: String.raw`\prod_{i=1}^{n}x_i`, tooltip: "Tích — cận dưới, cận trên và biểu thức" },
    { label: "∞", value: String.raw`\infty` },
    { label: "≈", value: String.raw`\approx` },
    { label: "log", value: String.raw`\log\left(#?\right)`, textValue: String.raw`\log\left(x\right)` },
    { label: "ln", value: String.raw`\ln\left(#?\right)`, textValue: String.raw`\ln\left(x\right)` },
  ],
  calculus: [
    {
      label: "d/dx",
      value: String.raw`\frac{\mathrm{d}}{\mathrm{d}#?}\left(#?\right)`,
      textValue: String.raw`\frac{\mathrm{d}}{\mathrm{d}x}\left(f(x)\right)`,
      tooltip: "Đạo hàm — điền biến và hàm số",
    },
    {
      label: "∂/∂x",
      value: String.raw`\frac{\partial}{\partial #?}\left(#?\right)`,
      textValue: String.raw`\frac{\partial}{\partial x}\left(f(x,y)\right)`,
      tooltip: "Đạo hàm riêng — điền biến và hàm số",
    },
    {
      label: "∫ f dx",
      value: String.raw`\int #?\,\mathrm{d}#?`,
      textValue: String.raw`\int f(x)\,\mathrm{d}x`,
      tooltip: "Nguyên hàm — điền hàm số dưới dấu tích phân rồi biến",
    },
    {
      label: "∫ₐᵇ f dx",
      value: String.raw`\int_{#?}^{#?}#?\,\mathrm{d}#?`,
      textValue: String.raw`\int_{a}^{b}f(x)\,\mathrm{d}x`,
      tooltip: "Tích phân xác định — điền cận dưới, cận trên, hàm số và biến",
    },
    {
      label: "lim",
      value: String.raw`\lim_{#?\to #?}#?`,
      textValue: String.raw`\lim_{x\to a}f(x)`,
      tooltip: "Giới hạn — điền biến, giá trị tiến tới và biểu thức",
    },
    { label: "∇", value: String.raw`\nabla` },
    { label: "Δ", value: String.raw`\Delta` },
    { label: "f′(x)", value: String.raw`{#?}'\left(#?\right)`, textValue: String.raw`f'\left(x\right)`, tooltip: "Đạo hàm dạng f′(x)" },
  ],
  sets: [
    { label: "∈", value: String.raw`\in` },
    { label: "∉", value: String.raw`\notin` },
    { label: "⊂", value: String.raw`\subset` },
    { label: "⊆", value: String.raw`\subseteq` },
    { label: "∪", value: String.raw`\cup` },
    { label: "∩", value: String.raw`\cap` },
    { label: "∅", value: String.raw`\varnothing` },
    { label: "ℝ", value: String.raw`\mathbb{R}` },
    { label: "ℕ", value: String.raw`\mathbb{N}` },
    { label: "ℤ", value: String.raw`\mathbb{Z}` },
    { label: "ℚ", value: String.raw`\mathbb{Q}` },
    { label: "⇒", value: String.raw`\Rightarrow` },
    { label: "⇔", value: String.raw`\Leftrightarrow` },
  ],
  geometry: [
    { label: "π", value: String.raw`\pi` },
    { label: "θ", value: String.raw`\theta` },
    { label: "α", value: String.raw`\alpha` },
    { label: "β", value: String.raw`\beta` },
    { label: "γ", value: String.raw`\gamma` },
    { label: "λ", value: String.raw`\lambda` },
    { label: "°", value: String.raw`^{\circ}` },
    { label: "∠", value: String.raw`\angle` },
    { label: "△", value: String.raw`\triangle` },
    { label: "∥", value: String.raw`\parallel` },
    { label: "⊥", value: String.raw`\perp` },
  ],
};
