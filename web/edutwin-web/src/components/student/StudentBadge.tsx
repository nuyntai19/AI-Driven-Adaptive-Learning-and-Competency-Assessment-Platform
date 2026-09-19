import React from "react";

export type StudentBadgeVariant =
  | "neutral"
  | "primary"
  | "accent"
  | "success"
  | "warning"
  | "danger"
  | "subject";

interface StudentBadgeProps {
  children: React.ReactNode;
  variant?: StudentBadgeVariant;
  size?: "xs" | "sm" | "md";
  className?: string;
  customColor?: string;
  customBg?: string;
  dot?: boolean;
}

export const StudentBadge: React.FC<StudentBadgeProps> = ({
  children,
  variant = "neutral",
  size = "md",
  className = "",
  customColor,
  customBg,
  dot = false,
}) => {
  const sizeClasses =
    size === "xs"
      ? "px-1.5 py-0.2 text-[10px] leading-tight"
      : size === "sm"
      ? "px-2 py-0.5 text-[11px] leading-tight"
      : "px-2.5 py-1 text-xs leading-normal";

  let variantClasses = "bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 border-slate-200 dark:border-slate-700";
  let dotColor = "bg-slate-400";

  switch (variant) {
    case "primary":
    case "accent":
      variantClasses =
        "bg-[var(--student-brand-soft,#EEF2FF)] dark:bg-[var(--student-brand-soft,#1E1B4B)] text-[var(--student-brand,#4F46E5)] dark:text-[#A5B4FC] border-[var(--student-brand-border,#C7D2FE)] dark:border-[#3730A3]";
      dotColor = "bg-[var(--student-brand,#4F46E5)]";
      break;
    case "success":
      variantClasses =
        "bg-[#EDF8F3] dark:bg-[#143B2C] text-[#16835B] dark:text-[#29AC7B] border-[#BAE5D1] dark:border-[#205A43]";
      dotColor = "bg-[#16835B]";
      break;
    case "warning":
      variantClasses =
        "bg-[#FFF5EB] dark:bg-[#3D2611] text-[#B85C00] dark:text-[#E68A2E] border-[#FCD8B3] dark:border-[#66411E]";
      dotColor = "bg-[#B85C00]";
      break;
    case "danger":
      variantClasses =
        "bg-[#FEF2F3] dark:bg-[#3D161C] text-[#B42338] dark:text-[#E04D60] border-[#F9C3CA] dark:border-[#6B2732]";
      dotColor = "bg-[#B42338]";
      break;
    case "neutral":
    default:
      variantClasses =
        "bg-slate-100/90 dark:bg-slate-800/90 text-slate-700 dark:text-slate-300 border-slate-200 dark:border-slate-700";
      dotColor = "bg-slate-400";
      break;
  }

  const customStyle: React.CSSProperties = {};
  if (customColor) customStyle.color = customColor;
  if (customBg) customStyle.backgroundColor = customBg;

  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-lg border font-semibold tracking-normal transition-colors ${sizeClasses} ${variantClasses} ${className}`.trim()}
      style={customStyle}
    >
      {dot && <span className={`w-1.5 h-1.5 rounded-full shrink-0 ${dotColor}`} />}
      {children}
    </span>
  );
};
