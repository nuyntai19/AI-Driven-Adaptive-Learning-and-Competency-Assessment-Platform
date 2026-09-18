import type { ReactNode } from "react";
import { useThemeMode } from "../../utils/themeMode";
import "./teacherDesignSystem.css";

interface TeacherThemeScopeProps {
  children: ReactNode;
  className?: string;
  "data-actor"?: string;
}

export function TeacherThemeScope({
  children,
  className = "",
  "data-actor": dataActor = "teacher",
}: TeacherThemeScopeProps) {
  const { theme } = useThemeMode();

  return (
    <div
      data-actor={dataActor}
      data-theme={theme}
      className={`min-h-screen bg-[var(--th-bg)] text-[var(--th-text)] transition-colors duration-200 ${className}`}
    >
      {children}
    </div>
  );
}
