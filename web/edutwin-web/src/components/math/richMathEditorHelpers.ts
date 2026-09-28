import katex from "katex";
import { cleanFormulaForInsertion } from "../../pages/centerManagerQuestionEditorHelpers.ts";

/**
 * Serializes the contenteditable DOM tree into a canonical string with $...$ formulas.
 * Normalizes non-breaking spaces (\u00A0) into normal spaces to avoid hidden character accumulation.
 */
export function serializeEditorDom(container: HTMLElement): string {
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
    "inline-math-node inline-flex items-center align-middle mx-1 px-2 py-0.5 rounded-lg border border-cyan-500/40 bg-cyan-950/40 hover:bg-cyan-900/60 hover:border-cyan-400 text-cyan-200 cursor-pointer select-none transition-all group font-normal text-sm shadow-sm";
  span.contentEditable = "false";
  span.dataset.latex = latex;
  span.title = "Nhấp để chỉnh sửa công thức toán";

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
