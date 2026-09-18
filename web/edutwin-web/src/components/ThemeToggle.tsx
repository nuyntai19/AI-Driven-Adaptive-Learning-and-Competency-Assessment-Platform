import React from "react";
import { useThemeMode } from "../utils/themeMode";

export const ThemeToggle: React.FC<{ className?: string; showText?: boolean }> = ({
  className = "",
  showText = false,
}) => {
  const { isDark, toggleTheme } = useThemeMode();

  return (
    <button
      type="button"
      onClick={toggleTheme}
      aria-pressed={isDark}
      className={`inline-flex items-center justify-center p-2 text-xs font-semibold rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-700 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-700 transition-colors shadow-2xs focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 cursor-pointer ${className}`.trim()}
      title={isDark ? "Chuyển sang giao diện Sáng" : "Chuyển sang giao diện Tối"}
      aria-label="Chuyển đổi giao diện sáng tối"
    >
      {isDark ? (
        <span className="text-amber-400 text-base leading-none" role="img" aria-label="Giao diện sáng">
          ☀️
        </span>
      ) : (
        <span className="text-slate-600 dark:text-indigo-400 text-base leading-none" role="img" aria-label="Giao diện tối">
          ◐
        </span>
      )}
      {showText && (
        <span className="ml-1.5 text-xs font-bold">
          {isDark ? "Sáng" : "Tối"}
        </span>
      )}
    </button>
  );
};
