import type { ReactNode } from "react";
import "./centerManagerDesignSystem.css";
import { useThemeMode } from "../../utils/themeMode";

interface CenterManagerThemeScopeProps {
  children: ReactNode;
  className?: string;
}

export function CenterManagerThemeScope({ children, className = "" }: CenterManagerThemeScopeProps) {
  const { theme } = useThemeMode();

  return (
    <div
      data-actor="center-manager"
      data-theme={theme}
      className={`min-h-screen bg-[var(--cm-bg)] text-[var(--cm-text)] transition-colors duration-200 ${className}`.trim()}
    >
      {children}
    </div>
  );
}
