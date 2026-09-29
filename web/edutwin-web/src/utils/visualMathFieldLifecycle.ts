/**
 * Lifecycle and state reconciliation utilities for VisualMathField.
 * Ensures state preservation across dynamic chunk loading, fallback mode, and retries.
 */

export interface MathFieldSyncTarget {
  readOnly: boolean;
  value: string;
  tabIndex: number;
  setAttribute: (name: string, val: string) => void;
  removeAttribute: (name: string) => void;
  setValue: (val: string, options?: { silenceNotifications?: boolean }) => void;
  getValue?: (format?: string) => string;
  focus?: () => void;
}

export interface HydrateMathFieldOptions {
  latestValue: string;
  latestDisabled: boolean;
  latestAutoFocus: boolean;
}

/**
 * Hydrates a newly mounted or retried MathField instance with the latest component props/refs.
 * Crucial for preserving text typed by users into the fallback textarea during chunk failures.
 */
export function hydrateMathFieldInstance(
  mf: MathFieldSyncTarget,
  options: HydrateMathFieldOptions
): { hydratedValue: string; appliedDisabled: boolean } {
  // 1. Apply latest disabled state
  if (options.latestDisabled) {
    mf.readOnly = true;
    mf.setAttribute("readonly", "true");
    mf.setAttribute("disabled", "true");
    mf.tabIndex = -1;
  } else {
    mf.readOnly = false;
    mf.removeAttribute("readonly");
    mf.removeAttribute("disabled");
    mf.removeAttribute("tabindex");
  }

  // 2. Hydrate latest value (from fallback textarea or latest parent state)
  const valToSet = options.latestValue || "";
  mf.setValue(valToSet, { silenceNotifications: true });

  // 3. Apply autoFocus only if active and not disabled
  if (options.latestAutoFocus && !options.latestDisabled && typeof mf.focus === "function") {
    setTimeout(() => mf.focus?.(), 150);
  }

  return {
    hydratedValue: valToSet,
    appliedDisabled: options.latestDisabled,
  };
}

/**
 * Determines whether an external prop value differs from the MathField's current state
 * and is not an echo of the last value emitted by the field itself.
 */
export function shouldSyncExternalValue(
  propValue: string,
  currentMathFieldValue: string,
  lastEmittedValue: string
): boolean {
  return propValue !== currentMathFieldValue && propValue !== lastEmittedValue;
}

export type EventPathNode = {
  tagName?: string;
  classList?: { contains: (cls: string) => boolean };
  getAttribute?: (attr: string) => string | null;
  closest?: (selector: string) => unknown;
};

/**
 * Checks whether an event target path is considered "outside" the active virtual keyboard interaction area.
 * Returns true if the click was outside all math fields, outside virtual keyboard, and outside virtual keyboard toggles.
 */
export function isOutsideVirtualKeyboardClick(composedPath: (EventTarget | EventPathNode)[]): boolean {
  for (const item of composedPath) {
    if (!item || typeof item !== "object") continue;
    const el = item as EventPathNode;

    // 1. Inside the virtual keyboard itself
    if (
      el.classList?.contains?.("ML__keyboard") ||
      el.classList?.contains?.("MLK__plate") ||
      el.classList?.contains?.("MLK__backdrop") ||
      el.tagName?.toLowerCase() === "math-virtual-keyboard"
    ) {
      return false;
    }

    // 2. The virtual keyboard toggle button
    if (
      el.getAttribute?.("part") === "virtual-keyboard-toggle" ||
      el.classList?.contains?.("ML__virtual-keyboard-toggle")
    ) {
      return false;
    }

    // 3. Inside a math field or its active contenteditable container
    if (
      el.tagName?.toLowerCase() === "math-field" ||
      el.classList?.contains?.("rich-math-content-editable") ||
      el.classList?.contains?.("inline-math-node")
    ) {
      return false;
    }
  }

  return true;
}

let activeListenerCount = 0;
let removeGlobalListener: (() => void) | null = null;

/**
 * Registers a document-level capture listener that automatically dismisses/closes
 * the MathLive virtual keyboard when the user taps or clicks anywhere outside the
 * keyboard and math field.
 */
export function registerVirtualKeyboardDismissListener(): () => void {
  if (typeof window === "undefined") {
    return () => {};
  }

  activeListenerCount++;
  if (activeListenerCount === 1) {
    const handlePointerDown = (e: PointerEvent | MouseEvent) => {
      const keyboard = (
        window as unknown as {
          mathVirtualKeyboard?: { visible?: boolean; hide: () => void };
        }
      ).mathVirtualKeyboard;

      const isVisible = Boolean(
        keyboard?.visible || document.querySelector?.(".ML__keyboard.is-visible")
      );

      if (!isVisible) return;

      const path = typeof e.composedPath === "function" ? e.composedPath() : [];
      if (path.length === 0 && e.target) {
        let curr: Node | null = e.target as Node;
        while (curr) {
          path.push(curr);
          curr = curr.parentNode;
        }
      }

      if (isOutsideVirtualKeyboardClick(path)) {
        keyboard?.hide?.();
      }
    };

    window.addEventListener("pointerdown", handlePointerDown, true);
    removeGlobalListener = () => {
      window.removeEventListener("pointerdown", handlePointerDown, true);
    };
  }

  return () => {
    activeListenerCount = Math.max(0, activeListenerCount - 1);
    if (activeListenerCount === 0 && removeGlobalListener) {
      removeGlobalListener();
      removeGlobalListener = null;
    }
  };
}

export function resetVirtualKeyboardListenerForTests(): void {
  if (removeGlobalListener) {
    removeGlobalListener();
    removeGlobalListener = null;
  }
  activeListenerCount = 0;
}

export const DEFAULT_MATH_INLINE_SHORTCUTS = {
  sqrt: "\\sqrt{#?}",
  cbrt: "\\sqrt[3]{#?}",
  abs: "\\left|#?\\right|",
  "|": "\\left|#?\\right|",
  norm: "\\left\\|#?\\right\\|",
} as const;

/**
 * Normalizes user- or calculator-provided LaTeX/text into structural MathLive commands
 * containing explicit placeholders (#?) for radicals and fences, ensuring newly created
 * empty blocks place the caret inside rather than selecting the whole block (which causes
 * immediate replacement/erasure upon typing).
 */
export function normalizeMathInsertContent(latexOrText: string): string {
  if (!latexOrText) return "";
  const trimmed = latexOrText.trim();

  // 1. Exact function calls or raw keywords from Casio / Calculator / Toolbar
  if (trimmed === "sqrt(" || trimmed === "\\sqrt" || trimmed === "\\sqrt{}" || trimmed === "sqrt") {
    return "\\sqrt{#?}";
  }
  if (trimmed === "cbrt(" || trimmed === "\\cbrt" || trimmed === "\\cbrt{}" || trimmed === "cbrt") {
    return "\\sqrt[3]{#?}";
  }
  if (trimmed === "\\sqrt[]{}" || trimmed === "\\sqrt[]") {
    return "\\sqrt[#?]{#?}";
  }
  if (
    trimmed === "abs(" ||
    trimmed === "\\abs" ||
    trimmed === "\\abs{}" ||
    trimmed === "abs" ||
    trimmed === "|" ||
    trimmed === "\\left|\\right|" ||
    trimmed === "\\left| \\right|" ||
    trimmed === "\\vert\\vert"
  ) {
    return "\\left|#?\\right|";
  }
  if (
    trimmed === "||" ||
    trimmed === "\\left\\|\\right\\|" ||
    trimmed === "\\left\\| \\right\\|" ||
    trimmed === "\\Vert\\Vert" ||
    trimmed === "norm(" ||
    trimmed === "norm"
  ) {
    return "\\left\\|#?\\right\\|";
  }
  if (trimmed === "\\frac" || trimmed === "\\frac{}" || trimmed === "\\frac{}{}") {
    return "\\frac{#?}{#?}";
  }
  if (trimmed === "^" || trimmed === "^{}") {
    return "^{#?}";
  }
  if (trimmed === "_" || trimmed === "_{}") {
    return "_{#?}";
  }

  // 2. Structural substitutions for empty containers without placeholders
  let normalized = latexOrText;
  normalized = normalized.replace(/\\sqrt\{\s*\}/g, "\\sqrt{#?}");
  normalized = normalized.replace(/\\left\|\s*\\right\|/g, "\\left|#?\\right|");
  normalized = normalized.replace(/\\left\\\|\s*\\right\\\|/g, "\\left\\|#?\\right\\|");
  normalized = normalized.replace(/\\frac\{\s*\}\{\s*\}/g, "\\frac{#?}{#?}");

  return normalized;
}
