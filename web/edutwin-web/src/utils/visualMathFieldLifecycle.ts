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

    // 3. Inside a math field, rich-math contenteditable container, or formula popover dialog
    if (
      el.tagName?.toLowerCase() === "math-field" ||
      el.classList?.contains?.("rich-math-content-editable") ||
      el.classList?.contains?.("inline-math-node") ||
      el.classList?.contains?.("rich-math-popover")
    ) {
      return false;
    }
  }

  return true;
}

let activeListenerCount = 0;
let removeGlobalListener: (() => void) | null = null;
let vkDismissalActive = false;
let vkDismissalTimer: ReturnType<typeof setTimeout> | null = null;

/**
 * Marks that a virtual keyboard dismissal has occurred, activating one-shot protection
 * for the immediate subsequent click gesture. A safety timer (600ms) automatically clears
 * protection if a click event never arrives (e.g., gesture canceled or dragged away).
 */
export function markVirtualKeyboardDismissed(): void {
  vkDismissalActive = true;
  if (typeof window !== "undefined") {
    (window as unknown as { __edutwin_vk_dismissal_active?: boolean }).__edutwin_vk_dismissal_active = true;
    (window as unknown as { __edutwin_last_vk_dismissed?: number }).__edutwin_last_vk_dismissed = Date.now();
  }
  if (vkDismissalTimer) {
    clearTimeout(vkDismissalTimer);
  }
  vkDismissalTimer = setTimeout(() => {
    clearVirtualKeyboardDismissalProtection();
  }, 600);
}

/**
 * Checks whether virtual keyboard dismissal protection is currently pending/active.
 */
export function isVirtualKeyboardDismissalActive(): boolean {
  if (vkDismissalActive) return true;
  if (typeof window !== "undefined") {
    return Boolean((window as unknown as { __edutwin_vk_dismissal_active?: boolean }).__edutwin_vk_dismissal_active);
  }
  return false;
}

/**
 * Consumes the one-shot virtual keyboard dismissal protection.
 * Returns true if protection was active (meaning this click should be ignored),
 * and resets protection immediately so subsequent clicks operate normally.
 */
export function consumeVirtualKeyboardDismissalProtection(): boolean {
  const wasActive = isVirtualKeyboardDismissalActive();
  clearVirtualKeyboardDismissalProtection();
  return wasActive;
}

/**
 * Resets virtual keyboard dismissal protection unconditionally.
 * Called when opening a new popover or after consuming dismissal.
 */
export function clearVirtualKeyboardDismissalProtection(): void {
  vkDismissalActive = false;
  if (vkDismissalTimer) {
    clearTimeout(vkDismissalTimer);
    vkDismissalTimer = null;
  }
  if (typeof window !== "undefined") {
    delete (window as unknown as { __edutwin_vk_dismissal_active?: boolean }).__edutwin_vk_dismissal_active;
    delete (window as unknown as { __edutwin_last_vk_dismissed?: number }).__edutwin_last_vk_dismissed;
  }
}

/**
 * Backward compatibility alias: returns true if virtual keyboard dismissal protection is active.
 */
export function wasVirtualKeyboardJustDismissed(thresholdMs?: number): boolean {
  void thresholdMs;
  return isVirtualKeyboardDismissalActive();
}

/**
 * Checks whether MathLive's virtual keyboard is currently visible.
 */
export function isVirtualKeyboardVisible(): boolean {
  if (typeof window === "undefined") return false;
  const keyboard = (
    window as unknown as {
      mathVirtualKeyboard?: { visible?: boolean; boundingRect?: { height?: number }; hide?: () => void };
    }
  ).mathVirtualKeyboard;
  return Boolean(
    keyboard?.visible === true ||
      (keyboard?.boundingRect?.height || 0) > 0 ||
      document.querySelector?.(".ML__keyboard.is-visible") ||
      document.querySelector?.("math-virtual-keyboard.is-visible") ||
      (document.querySelector?.(".MLK__plate") &&
        ((document.querySelector?.(".MLK__plate") as HTMLElement)?.offsetHeight || 0) > 0)
  );
}

/**
 * Safely hides the MathLive virtual keyboard if active.
 */
export function hideVirtualKeyboard(): void {
  if (typeof window === "undefined") return;
  const keyboard = (
    window as unknown as {
      mathVirtualKeyboard?: { visible?: boolean; hide?: () => void };
    }
  ).mathVirtualKeyboard;
  keyboard?.hide?.();
}

/**
 * Registers a document-level capture listener that automatically dismisses/closes
 * the MathLive virtual keyboard when the user taps or clicks anywhere outside the
 * keyboard and math field.
 */
export function registerVirtualKeyboardDismissListener(): () => void {
  if (typeof window === "undefined") {
    return () => {};
  }

  // Hook into MathLive's internal virtual-keyboard-toggle event if already initialized
  const vk = (
    window as unknown as {
      mathVirtualKeyboard?: EventTarget & { visible?: boolean };
    }
  ).mathVirtualKeyboard;
  let handleVkToggle: (() => void) | null = null;
  if (vk && typeof vk.addEventListener === "function") {
    handleVkToggle = () => {
      if (!vk.visible) {
        markVirtualKeyboardDismissed();
      }
    };
    vk.addEventListener("virtual-keyboard-toggle", handleVkToggle);
  }

  activeListenerCount++;
  if (activeListenerCount === 1) {
    const handlePointerDown = (e: PointerEvent | MouseEvent) => {
      const keyboard = (
        window as unknown as {
          mathVirtualKeyboard?: { visible?: boolean; hide: () => void };
        }
      ).mathVirtualKeyboard;

      const isVisible = isVirtualKeyboardVisible();
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
        markVirtualKeyboardDismissed();
        keyboard?.hide?.();
      }
    };

    window.addEventListener("pointerdown", handlePointerDown, true);
    window.addEventListener("mousedown", handlePointerDown, true);
    removeGlobalListener = () => {
      window.removeEventListener("pointerdown", handlePointerDown, true);
      window.removeEventListener("mousedown", handlePointerDown, true);
    };
  }

  return () => {
    activeListenerCount = Math.max(0, activeListenerCount - 1);
    if (activeListenerCount === 0 && removeGlobalListener) {
      removeGlobalListener();
      removeGlobalListener = null;
    }
    if (vk && handleVkToggle && typeof vk.removeEventListener === "function") {
      vk.removeEventListener("virtual-keyboard-toggle", handleVkToggle);
    }
  };
}

export function resetVirtualKeyboardListenerForTests(): void {
  if (removeGlobalListener) {
    removeGlobalListener();
    removeGlobalListener = null;
  }
  activeListenerCount = 0;
  clearVirtualKeyboardDismissalProtection();
}

export const DEFAULT_MATH_INLINE_SHORTCUTS = {
  sqrt: "\\sqrt{#?}",
  cbrt: "\\sqrt[3]{#?}",
  abs: "\\left|#?\\right|",
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
