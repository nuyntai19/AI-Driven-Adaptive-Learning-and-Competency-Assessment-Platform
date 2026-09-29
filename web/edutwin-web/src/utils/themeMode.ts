import { useEffect, useState } from "react";

export const THEME_STORAGE_KEY = "edutwin-theme";
export const THEME_CHANGE_EVENT = "edutwin-theme-change";

export type ThemeMode = "dark" | "light";

/**
 * Resolves the active initial theme by checking:
 * 1. Explicitly stored user preference in localStorage ('edutwin-theme').
 * 2. If no stored preference, falls back to system preference via matchMedia('(prefers-color-scheme: dark)').
 * 3. Defaults to 'light' if neither is set or in non-browser environments.
 *
 * Supports optional storage and matchMedia overrides for pure deterministic unit testing.
 */
export function resolveInitialTheme(
  storageOverride?: { getItem: (key: string) => string | null } | null,
  matchMediaOverride?: ((query: string) => { matches: boolean }) | null
): ThemeMode {
  const storage =
    storageOverride !== undefined
      ? storageOverride
      : typeof window !== "undefined"
      ? window.localStorage
      : null;

  if (storage) {
    try {
      const stored = storage.getItem(THEME_STORAGE_KEY);
      if (stored === "dark" || stored === "light") {
        return stored;
      }
    } catch {
      // Ignore storage access errors
    }
  }

  const matchMediaFn =
    matchMediaOverride !== undefined
      ? matchMediaOverride
      : typeof window !== "undefined" && typeof window.matchMedia === "function"
      ? window.matchMedia.bind(window)
      : null;

  if (matchMediaFn) {
    try {
      if (matchMediaFn("(prefers-color-scheme: dark)").matches) {
        return "dark";
      }
    } catch {
      // Ignore matchMedia errors
    }
  }

  return "light";
}

/**
 * Applies the given theme mode to the document DOM (adding/removing the 'dark' class).
 */
export function applyTheme(theme: ThemeMode): void {
  if (typeof document === "undefined") return;
  const root = document.documentElement;
  if (theme === "dark") {
    root.classList.add("dark");
  } else {
    root.classList.remove("dark");
  }
}

/**
 * Backwards compatibility alias: resolves stored theme or system fallback.
 */
export function getStoredTheme(): ThemeMode {
  return resolveInitialTheme();
}

export function useThemeMode() {
  const [theme, setTheme] = useState<ThemeMode>(() => resolveInitialTheme());

  useEffect(() => {
    const handleThemeChange = () => {
      setTheme(resolveInitialTheme());
    };

    window.addEventListener(THEME_CHANGE_EVENT, handleThemeChange);
    window.addEventListener("storage", handleThemeChange);

    const mediaQuery =
      typeof window !== "undefined" && typeof window.matchMedia === "function"
        ? window.matchMedia("(prefers-color-scheme: dark)")
        : null;

    const handleMediaChange = () => {
      try {
        if (!localStorage.getItem(THEME_STORAGE_KEY)) {
          const next = resolveInitialTheme();
          applyTheme(next);
          setTheme(next);
        }
      } catch {
        // Ignore storage errors
      }
    };

    mediaQuery?.addEventListener?.("change", handleMediaChange);

    return () => {
      window.removeEventListener(THEME_CHANGE_EVENT, handleThemeChange);
      window.removeEventListener("storage", handleThemeChange);
      mediaQuery?.removeEventListener?.("change", handleMediaChange);
    };
  }, []);

  const toggleTheme = () => {
    const nextTheme: ThemeMode = theme === "dark" ? "light" : "dark";
    applyTheme(nextTheme);
    try {
      localStorage.setItem(THEME_STORAGE_KEY, nextTheme);
    } catch {
      // Ignore storage errors
    }
    setTheme(nextTheme);
    window.dispatchEvent(new CustomEvent(THEME_CHANGE_EVENT, { detail: nextTheme }));
  };

  return {
    theme,
    isDark: theme === "dark",
    toggleTheme,
  };
}
