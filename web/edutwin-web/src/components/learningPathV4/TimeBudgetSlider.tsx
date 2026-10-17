import { useId } from "react";
import { MAX_STUDY_MINUTES, MIN_STUDY_MINUTES, formatStudyMinutes } from "../../utils/studyTime";

/** Clock-style readout, e.g. 1:30 for 90 minutes. */
function clock(minutes: number): string {
  return `${Math.floor(minutes / 60)}:${String(minutes % 60).padStart(2, "0")}`;
}

const marks = [5, 60, 120, 180, 240, 300];

export function TimeBudgetSlider({ value, onChange, label = "Thời gian học", summary, min = MIN_STUDY_MINUTES, max = MAX_STUDY_MINUTES }: {
  value: number; onChange: (minutes: number) => void; label?: string; summary?: string; min?: number; max?: number;
}) {
  const id = useId();
  const step = 5;
  const percent = ((value - min) / (max - min)) * 100;
  return <div className="w-full">
    <div className="flex items-end justify-between gap-3">
      <label htmlFor={id} className="text-sm font-medium">{label}</label>
      <div className="text-right" aria-live="polite">
        <span className="font-mono text-2xl font-semibold tabular-nums text-indigo-700 dark:text-indigo-300">{clock(value)}</span>
        <span className="ml-2 text-sm text-slate-500">{formatStudyMinutes(value)}</span>
      </div>
    </div>
    <input id={id} type="range" min={min} max={max} step={step} value={value}
      onChange={e => onChange(Math.min(max, Math.max(min, Math.round(Number(e.target.value) / step) * step || min)))}
      aria-valuetext={formatStudyMinutes(value)}
      className="mt-3 h-2 w-full cursor-pointer appearance-none rounded-full accent-indigo-600"
      style={{ background: `linear-gradient(to right, rgb(79 70 229) ${percent}%, rgb(226 232 240) ${percent}%)` }} />
    <div className="mt-1 flex justify-between text-[11px] text-slate-400" aria-hidden="true">
      {marks.filter(m => m >= min && m <= max).map(m => <button type="button" key={m} className="hover:text-indigo-600" onClick={() => onChange(m)}>{m < 60 ? `${m}p` : `${m / 60}h`}</button>)}
    </div>
    {summary && <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">{summary}</p>}
  </div>;
}
