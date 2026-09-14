import React, { useEffect, useState } from "react";

export const ThemeToggle: React.FC = () => {
  const [isDark, setIsDark] = useState<boolean>(() => {
    if (typeof window === "undefined") return false;
    const stored = localStorage.getItem("edutwin-theme");
    if (stored) {
      return stored === "dark";
    }
    return window.matchMedia("(prefers-color-scheme: dark)").matches;
  });

  useEffect(() => {
    const root = document.documentElement;
    if (isDark) {
      root.classList.add("dark");
      localStorage.setItem("edutwin-theme", "dark");
    } else {
      root.classList.remove("dark");
      localStorage.setItem("edutwin-theme", "light");
    }
  }, [isDark]);

  const toggleTheme = () => {
    setIsDark((prev) => !prev);
  };

  return (
    <button
      type="button"
      onClick={toggleTheme}
      className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-700 dark:text-gray-200 hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors shadow-sm"
      title={isDark ? "Chuyển sang giao diện Sáng" : "Chuyển sang giao diện Tối"}
      aria-label="Chuyển đổi giao diện sáng tối"
    >
      {isDark ? (
        <>
          <span className="text-amber-400 text-sm">☀️</span>
          <span className="hidden sm:inline">Giao diện Sáng</span>
        </>
      ) : (
        <>
          <span className="text-indigo-400 text-sm">🌙</span>
          <span className="hidden sm:inline">Giao diện Tối</span>
        </>
      )}
    </button>
  );
};
