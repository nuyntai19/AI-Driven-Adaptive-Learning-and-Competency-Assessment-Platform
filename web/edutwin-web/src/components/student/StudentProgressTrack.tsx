import React from "react";

interface StudentProgressTrackProps {
  currentValue: number;
  targetValue: number;
  maxValue?: number;
  max?: number;
  minValue?: number;
  currentLabel?: string;
  targetLabel?: string;
  unit?: string;
  accentColor?: string;
  color?: string;
  showValues?: boolean;
  className?: string;
  subText?: string;
}

/**
 * Twin Track Visual Motif:
 * CURRENT 0.8 ━━━━━━━●━━━━━━━━━━━━ 8.0 TARGET
 * Displays dual-rail / dual-node progress representing current distance to learning target.
 */
export const StudentProgressTrack: React.FC<StudentProgressTrackProps> = ({
  currentValue,
  targetValue,
  maxValue = 10,
  max,
  minValue = 0,
  currentLabel = "Hiện tại",
  targetLabel = "Mục tiêu",
  unit = "",
  accentColor,
  color,
  showValues = true,
  className = "",
  subText,
}) => {
  const effectiveMax = max !== undefined ? max : maxValue;
  const effectiveAccent = color || accentColor || "var(--student-brand, #6546D7)";
  const range = Math.max(0.1, effectiveMax - minValue);
  const currentPercent = Math.min(100, Math.max(0, ((currentValue - minValue) / range) * 100));
  const targetPercent = Math.min(100, Math.max(0, ((targetValue - minValue) / range) * 100));

  return (
    <div className={`w-full ${className}`}>
      {showValues && (
        <div className="flex items-center justify-between text-xs mb-2">
          <div className="flex items-baseline gap-1.5">
            <span className="text-[11px] font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider">
              {currentLabel}
            </span>
            <span className="text-base font-bold text-slate-900 dark:text-slate-100">
              {typeof currentValue === "number" ? currentValue.toFixed(1) : currentValue}
              {unit}
            </span>
          </div>

          <div className="flex items-baseline gap-1.5">
            <span className="text-[11px] font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider">
              {targetLabel}
            </span>
            <span className="text-base font-bold text-slate-900 dark:text-slate-100" style={{ color: effectiveAccent }}>
              {typeof targetValue === "number" ? targetValue.toFixed(1) : targetValue}
              {unit}
            </span>
          </div>
        </div>
      )}

      {/* Track rail */}
      <div className="relative h-2 w-full bg-slate-100 dark:bg-slate-800 rounded-full overflow-visible">
        {/* Background target marker line */}
        <div
          className="absolute top-0 bottom-0 bg-slate-200 dark:bg-slate-700/80 rounded-full"
          style={{ width: `${targetPercent}%` }}
        />

        {/* Current progress fill */}
        <div
          className="absolute top-0 bottom-0 rounded-full transition-all duration-500"
          style={{
            width: `${currentPercent}%`,
            backgroundColor: effectiveAccent,
          }}
        />

        {/* Current Node indicator */}
        <div
          className="absolute top-1/2 -translate-y-1/2 w-3.5 h-3.5 rounded-full border-2 border-white dark:border-slate-900 shadow-xs transition-all duration-500 pointer-events-none"
          style={{
            left: `calc(${currentPercent}% - 7px)`,
            backgroundColor: effectiveAccent,
          }}
          title={`${currentLabel}: ${currentValue}`}
        />

        {/* Target Node indicator */}
        <div
          className="absolute top-1/2 -translate-y-1/2 w-3 h-3 rounded-full border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-800 shadow-2xs pointer-events-none"
          style={{
            left: `calc(${targetPercent}% - 6px)`,
          }}
          title={`${targetLabel}: ${targetValue}`}
        />
      </div>

      {subText && (
        <p className="mt-1.5 text-[11px] text-slate-500 dark:text-slate-400 font-medium">
          {subText}
        </p>
      )}
    </div>
  );
};
