import React from "react";
import { useThemeMode } from "../utils/themeMode";

export const ThemeToggle: React.FC<{ className?: string }> = ({ className = "" }) => {
  const { isDark, toggleTheme } = useThemeMode();

  return (
    <button
      type="button"
      onClick={toggleTheme}
      aria-pressed={isDark}
      className={`inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-700 dark:text-gray-200 hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors shadow-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 ${className}`.trim()}
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
