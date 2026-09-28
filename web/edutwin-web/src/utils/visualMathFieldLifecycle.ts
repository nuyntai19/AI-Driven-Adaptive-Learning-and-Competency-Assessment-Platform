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
