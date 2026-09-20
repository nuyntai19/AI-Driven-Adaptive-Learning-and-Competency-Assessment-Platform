import type { ReactNode } from "react";

interface TeacherFilterBarProps {
  search?: {
    value: string;
    onChange: (value: string) => void;
    placeholder?: string;
  };
  children?: ReactNode;
  actions?: ReactNode;
}

export function TeacherFilterBar({ search, children, actions }: TeacherFilterBarProps) {
  return (
    <div className="th-surface flex flex-col gap-3.5 sm:flex-row sm:items-center sm:justify-between p-3.5 sm:p-4">
      <div className="flex flex-1 flex-wrap items-center gap-2.5">
        {search && (
          <div className="relative min-w-[240px] flex-1 max-w-sm">
            <input
              type="text"
              value={search.value}
              onChange={(e) => search.onChange(e.target.value)}
              placeholder={search.placeholder ?? "Tìm kiếm nhanh..."}
              className="th-input w-full pl-9 pr-3 text-xs font-medium"
            />
            <span aria-hidden="true" className="absolute left-3 top-1/2 -translate-y-1/2 text-xs text-[var(--th-text-muted)]">
              🔍
            </span>
          </div>
        )}
        {children}
      </div>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </div>
  );
}
