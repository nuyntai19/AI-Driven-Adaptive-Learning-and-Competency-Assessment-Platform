import type { ReactNode } from "react";

type WorkInputMode = "keyboard" | "draw" | "photo";
type WorkSideTool = "casio" | "graph";

function Icon({ children }: { children: ReactNode }) {
  return (
    <svg aria-hidden="true" className="h-4 w-4 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round">
      {children}
    </svg>
  );
}

const MODES: Array<{ value: WorkInputMode; label: string; hint: string; icon: ReactNode }> = [
  {
    value: "keyboard",
    label: "Bàn phím",
    hint: "Gõ bài làm bằng bàn phím",
    icon: <><rect x="2" y="6" width="20" height="12" rx="2" /><path d="M6 10h.01M10 10h.01M14 10h.01M18 10h.01M7 14h10" /></>,
  },
  {
    value: "draw",
    label: "Vẽ trên A4",
    hint: "Viết tay, làm nháp trên giấy A4 rồi quét vào bài",
    icon: <><path d="M12 20h9" /><path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z" /></>,
  },
  {
    value: "photo",
    label: "Ảnh chụp",
    hint: "Chụp bài giải trên giấy rồi tải lên",
    icon: <><path d="M14.5 4h-5L7.5 7H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-3.5Z" /><circle cx="12" cy="13" r="3" /></>,
  },
];

const TOOLS: Array<{ value: WorkSideTool; label: string; hint: string; icon: ReactNode }> = [
  {
    value: "casio",
    label: "Máy tính",
    hint: "Máy tính Casio fx-580VN X",
    icon: <><rect x="5" y="2" width="14" height="20" rx="2" /><path d="M9 6h6M9 11h.01M12 11h.01M15 11h.01M9 14.5h.01M12 14.5h.01M15 14.5h.01M9 18h.01M12 18h.01M15 18h.01" /></>,
  },
  {
    value: "graph",
    label: "Đồ thị",
    hint: "Vẽ và khảo sát đồ thị hàm số",
    icon: <><path d="M3 3v18h18" /><path d="m7 15 4-5 3 3 5-7" /></>,
  },
];

const BUTTON =
  "inline-flex h-9 items-center gap-1.5 rounded-lg px-3 text-sm font-semibold whitespace-nowrap cursor-pointer " +
  "transition-[background-color,color,transform] duration-150 ease-out active:scale-[0.97] motion-reduce:transform-none " +
  "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500";
const IDLE = "text-slate-600 dark:text-slate-300 hover:text-slate-900 dark:hover:text-white";

interface WorkToolbarProps {
  /** Hidden once the work is read-only: the input method can no longer change. */
  showModes: boolean;
  mode: WorkInputMode;
  onModeChange: (mode: WorkInputMode) => void;
  /** Calculator and graph only make sense for math subjects. */
  showTools: boolean;
  activeTool: string | null;
  onToggleTool: (tool: WorkSideTool) => void;
}

/** The one row of controls for a question: how to answer, and which helper is open. */
export function WorkToolbar({ showModes, mode, onModeChange, showTools, activeTool, onToggleTool }: WorkToolbarProps) {
  if (!showModes && !showTools) return null;
  return (
    <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
      {showModes ? (
        <div role="tablist" aria-label="Cách làm bài" className="inline-flex gap-0.5 rounded-xl bg-slate-100 p-1 dark:bg-slate-800/70">
          {MODES.map((item) => {
            const selected = mode === item.value;
            return (
              <button
                key={item.value}
                type="button"
                role="tab"
                aria-selected={selected}
                title={`${item.hint}. Đổi cách làm vẫn giữ nguyên bài đã làm.`}
                onClick={() => onModeChange(item.value)}
                className={`${BUTTON} ${selected ? "bg-white text-indigo-700 shadow-xs dark:bg-slate-950 dark:text-indigo-300" : IDLE}`}
              >
                <Icon>{item.icon}</Icon>
                {item.label}
              </button>
            );
          })}
        </div>
      ) : <span />}
      {showTools && (
        <div role="group" aria-label="Công cụ hỗ trợ" className="inline-flex gap-1">
          {TOOLS.map((item) => {
            const open = activeTool === item.value;
            return (
              <button
                key={item.value}
                type="button"
                aria-pressed={open}
                title={item.hint}
                onClick={() => onToggleTool(item.value)}
                className={`${BUTTON} ${open ? "bg-indigo-50 text-indigo-700 dark:bg-indigo-950/70 dark:text-indigo-300" : `${IDLE} hover:bg-slate-100 dark:hover:bg-slate-800/70`}`}
              >
                <Icon>{item.icon}</Icon>
                {item.label}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
