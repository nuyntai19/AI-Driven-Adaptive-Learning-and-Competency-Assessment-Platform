import { useState } from "react";

export type WorkInputMode = "keyboard" | "draw" | "photo";

const PREFIX = "edutwin-input-mode";

function readMode(storageKey: string | null): WorkInputMode {
  if (!storageKey) return "keyboard";
  try {
    const value = localStorage.getItem(storageKey);
    return value === "draw" || value === "photo" ? value : "keyboard";
  } catch {
    return "keyboard";
  }
}

/**
 * How a free-response answer is entered (keyboard / A4 sheet / photo). The choice is remembered per subject and
 * starts on the keyboard: drawing on A4 in Math must not make every English essay open on the A4 sheet.
 */
export function useWorkInputMode(subjectKey?: string | null) {
  const storageKey = subjectKey ? `${PREFIX}:${subjectKey}` : null;
  const [state, setState] = useState(() => ({ key: storageKey, mode: readMode(storageKey) }));
  // The subject can arrive after the first render (loaded with the session); read its own choice then.
  const mode = state.key === storageKey ? state.mode : readMode(storageKey);
  const setMode = (next: WorkInputMode) => {
    setState({ key: storageKey, mode: next });
    if (!storageKey) return;
    try { localStorage.setItem(storageKey, next); } catch { /* storage unavailable: keep the choice for this page only */ }
  };
  return [mode, setMode] as const;
}
