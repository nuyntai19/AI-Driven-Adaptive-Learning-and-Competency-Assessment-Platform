import katex from "katex";
import { cleanFormulaForInsertion } from "../../pages/centerManagerQuestionEditorHelpers.ts";

/**
 * Serializes the contenteditable DOM tree into a canonical string with $...$ formulas.
 * Normalizes non-breaking spaces (\u00A0) into normal spaces to avoid hidden character accumulation.
 */
export function serializeEditorDom(container: HTMLElement, singleLine = false): string {
  let result = "";

  function walk(node: Node) {
    if (node.nodeType === 3) {
      // Normalize NBSP to regular space to prevent hidden character accumulation
      const text = (node.nodeValue || "").replace(/\u00A0/g, " ");
      result += text;
    } else if (node.nodeType === 1) {
      const el = node as HTMLElement;
      if (el.classList.contains("inline-math-node")) {
        const latex = cleanFormulaForInsertion(el.dataset.latex || "");
        if (latex) {
          result += `$${latex}$`;
        }
      } else if (el.tagName === "BR") {
        result += "\n";
      } else if (el.tagName === "DIV" || el.tagName === "P") {
        if (result.length > 0 && !result.endsWith("\n")) {
          result += "\n";
        }
        for (const child of Array.from(el.childNodes)) {
          walk(child);
        }
      } else {
        for (const child of Array.from(el.childNodes)) {
          walk(child);
        }
      }
    }
  }

  for (const child of Array.from(container.childNodes)) {
    walk(child);
  }

  if (singleLine) {
    result = result.replace(/[\r\n]+/g, " ").trim();
  }

  return result;
}

/**
 * Creates an inline math node representing a formula rendered with KaTeX.
 */
export function createMathSpan(
  latex: string,
  onEdit?: (span: HTMLElement) => void
): HTMLElement {
  const span = document.createElement("span");
  span.className =
    `inline-math-node inline-flex items-center align-middle mx-1 px-2 py-0.5 rounded-lg border ${onEdit ? "cursor-pointer select-none" : "cursor-text"} transition-all group font-normal text-sm shadow-sm`;
  span.contentEditable = "false";
  span.dataset.latex = latex;
  span.title = onEdit ? "Nhấp để chỉnh sửa công thức toán" : "Công thức toán (chỉ đọc)";

  const clean = cleanFormulaForInsertion(latex);
  if (clean) {
    try {
      const html = katex.renderToString(clean, {
        throwOnError: false,
        displayMode: false,
        output: "htmlAndMathml",
      });
      span.innerHTML = `<span class="katex-rendered pointer-events-none">${html}</span>`;
    } catch {
      span.innerHTML = `<span class="katex-fallback font-mono text-xs text-rose-300 pointer-events-none">${clean}</span>`;
    }
  } else {
    span.innerHTML =
      '<span class="katex-placeholder text-xs italic px-1 text-cyan-300 bg-cyan-900/50 rounded pointer-events-none">[Công thức...]</span>';
  }

  if (onEdit) {
    span.addEventListener("click", (e) => {
      e.stopPropagation();
      onEdit(span);
    });
  }

  return span;
}

/**
 * Hydrates the contenteditable DOM tree from a serialized raw string containing $...$ or $$...$$.
 */
export function hydrateEditorDom(
  container: HTMLElement,
  raw: string,
  onEdit?: (span: HTMLElement) => void
): void {
  container.innerHTML = "";
  if (!raw) {
    return;
  }

  const regex = /(\$\$[\s\S]*?\$\$|\$[^$\n]+?\$)/g;
  const parts = raw.split(regex);

  for (const part of parts) {
    if (!part) continue;
    if (
      (part.startsWith("$$") && part.endsWith("$$") && part.length >= 4) ||
      (part.startsWith("$") && part.endsWith("$") && part.length >= 2)
    ) {
      const formula = part.startsWith("$$")
        ? part.slice(2, -2).trim()
        : part.slice(1, -1).trim();
      const mathSpan = createMathSpan(formula, onEdit);
      container.appendChild(mathSpan);
    } else {
      const lines = part.split("\n");
      for (let i = 0; i < lines.length; i++) {
        if (lines[i]) {
          container.appendChild(document.createTextNode(lines[i]));
        }
        if (i < lines.length - 1) {
          container.appendChild(document.createElement("br"));
        }
      }
    }
  }
}

export interface RichMathPopoverTheme {
  isDark: boolean;
  popoverBorder: string;
  popoverShadow: string;
  badgeBg: string;
  inputBorder: string;
  previewBox: string;
  previewFormulaColor: string;
  confirmBtn: string;
}

/**
 * Pure function mapping editor actor variant & active theme darkness to popover styling tokens.
 * Handles all 4 actor variants across both Dark and Light modes.
 */
export function resolveRichMathPopoverTheme(
  variant: "center-manager" | "teacher" | "student" | "neutral" = "center-manager",
  isDark: boolean = false
): RichMathPopoverTheme {
  if (isDark) {
    switch (variant) {
      case "teacher":
        return {
          isDark: true,
          popoverBorder: "border-teal-500/50",
          popoverShadow: "shadow-teal-950/40",
          badgeBg: "bg-teal-500/20 border-teal-500/30 text-teal-400",
          inputBorder: "border-teal-500/40 focus-within:border-teal-400 focus-within:ring-2 focus-within:ring-teal-500/20",
          previewBox: "border-slate-800 bg-slate-950/80 text-slate-300",
          previewFormulaColor: "text-teal-200",
          confirmBtn: "bg-teal-500 hover:bg-teal-400 text-slate-950",
        };
      case "student":
        return {
          isDark: true,
          popoverBorder: "border-[#6746E8]/50",
          popoverShadow: "shadow-purple-950/40",
          badgeBg: "bg-[#6746E8]/20 border-[#6746E8]/30 text-[#a5b4fc]",
          inputBorder: "border-[#6746E8]/40 focus-within:border-[#818cf8] focus-within:ring-2 focus-within:ring-[#6746E8]/20",
          previewBox: "border-slate-800 bg-slate-950/80 text-slate-300",
          previewFormulaColor: "text-indigo-200",
          confirmBtn: "bg-[#6746E8] hover:bg-[#5839ce] text-white",
        };
      case "neutral":
        return {
          isDark: true,
          popoverBorder: "border-slate-700/80",
          popoverShadow: "shadow-slate-950/50",
          badgeBg: "bg-indigo-500/20 border-indigo-500/30 text-indigo-400",
          inputBorder: "border-slate-700 focus-within:border-indigo-500",
          previewBox: "border-slate-800 bg-slate-950/80 text-slate-300",
          previewFormulaColor: "text-indigo-200",
          confirmBtn: "bg-indigo-600 hover:bg-indigo-500 text-white",
        };
      case "center-manager":
      default:
        return {
          isDark: true,
          popoverBorder: "border-cyan-500/50",
          popoverShadow: "shadow-cyan-950/40",
          badgeBg: "bg-cyan-500/20 border-cyan-500/30 text-cyan-400",
          inputBorder: "border-cyan-500/40 focus-within:border-cyan-400 focus-within:ring-2 focus-within:ring-cyan-500/20",
          previewBox: "border-slate-800 bg-slate-950/80 text-slate-300",
          previewFormulaColor: "text-cyan-200",
          confirmBtn: "bg-cyan-500 hover:bg-cyan-400 text-slate-950",
        };
    }
  }

  // Light mode
  switch (variant) {
    case "teacher":
      return {
        isDark: false,
        popoverBorder: "border-teal-500/40",
        popoverShadow: "shadow-teal-950/15",
        badgeBg: "bg-teal-50 border-teal-200 text-teal-700",
        inputBorder: "border-teal-500/35 focus-within:border-teal-600 focus-within:ring-2 focus-within:ring-teal-500/15",
        previewBox: "border-teal-100 bg-teal-50/50 text-teal-950",
        previewFormulaColor: "text-teal-900",
        confirmBtn: "bg-teal-600 hover:bg-teal-500 text-white",
      };
    case "student":
      return {
        isDark: false,
        popoverBorder: "border-[#6746E8]/35",
        popoverShadow: "shadow-purple-950/15",
        badgeBg: "bg-purple-50 border-purple-200 text-[#6746E8]",
        inputBorder: "border-[#6746E8]/35 focus-within:border-[#6746E8] focus-within:ring-2 focus-within:ring-[#6746E8]/15",
        previewBox: "border-purple-100 bg-purple-50/50 text-purple-950",
        previewFormulaColor: "text-purple-900",
        confirmBtn: "bg-[#6746E8] hover:bg-[#5839ce] text-white",
      };
    case "neutral":
      return {
        isDark: false,
        popoverBorder: "border-slate-300",
        popoverShadow: "shadow-slate-900/15",
        badgeBg: "bg-indigo-50 border-indigo-200 text-indigo-600",
        inputBorder: "border-slate-300 focus-within:border-indigo-600 focus-within:ring-2 focus-within:ring-indigo-500/15",
        previewBox: "border-slate-200 bg-slate-50/80 text-slate-800",
        previewFormulaColor: "text-indigo-950",
        confirmBtn: "bg-indigo-600 hover:bg-indigo-500 text-white",
      };
    case "center-manager":
    default:
      return {
        isDark: false,
        popoverBorder: "border-cyan-500/40",
        popoverShadow: "shadow-cyan-950/15",
        badgeBg: "bg-cyan-50 border-cyan-200 text-cyan-700",
        inputBorder: "border-cyan-500/35 focus-within:border-cyan-600 focus-within:ring-2 focus-within:ring-cyan-500/15",
        previewBox: "border-cyan-100 bg-cyan-50/50 text-cyan-950",
        previewFormulaColor: "text-cyan-900",
        confirmBtn: "bg-cyan-600 hover:bg-cyan-500 text-white",
      };
  }
}
