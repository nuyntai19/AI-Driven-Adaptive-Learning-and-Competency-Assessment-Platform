let mathLivePromise: Promise<unknown> | null = null;

/**
 * Dynamically imports MathLive only when a math input field is rendered.
 * Automatically handles failure by clearing the promise cache to allow retries.
 */
export function loadMathLive(): Promise<unknown> {
  if (typeof window === "undefined") {
    return Promise.resolve();
  }
  if (typeof customElements !== "undefined" && customElements.get("math-field")) {
    return Promise.resolve();
  }
  if (!mathLivePromise) {
    mathLivePromise = import("mathlive").catch((err) => {
      mathLivePromise = null; // Clear cached promise so next attempt can retry
      throw err;
    });
  }
  return mathLivePromise;
}

/**
 * Resets the loader promise (e.g. for testing or explicit user retry).
 */
export function resetMathLiveLoader(): void {
  mathLivePromise = null;
}
