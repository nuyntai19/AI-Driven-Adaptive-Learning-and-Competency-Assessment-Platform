import type { ReactNode } from "react";
import "./centerManagerDesignSystem.css";

interface CenterManagerThemeScopeProps {
  children: ReactNode;
  className?: string;
}

export function CenterManagerThemeScope({ children, className = "" }: CenterManagerThemeScopeProps) {
  return (
    <div data-actor="center-manager" className={`min-h-screen bg-[var(--cm-bg)] text-[var(--cm-text)] ${className}`.trim()}>
      {children}
    </div>
  );
}
