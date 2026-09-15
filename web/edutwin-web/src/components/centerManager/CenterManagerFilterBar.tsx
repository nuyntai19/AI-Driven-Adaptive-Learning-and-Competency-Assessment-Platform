import type { ReactNode } from "react";

interface FilterBarProps {
  searchValue?: string;
  searchLabel?: string;
  searchPlaceholder?: string;
  onSearchChange?: (value: string) => void;
  filters?: ReactNode;
  actions?: ReactNode;
}

export function FilterBar({
  searchValue,
  searchLabel = "Tìm kiếm",
  searchPlaceholder = "Tìm kiếm…",
  onSearchChange,
  filters,
  actions,
}: FilterBarProps) {
  return (
    <section aria-label="Bộ lọc dữ liệu" className="cm-surface flex flex-col gap-3 p-4 lg:flex-row lg:items-center">
      {onSearchChange && (
        <label className="min-w-0 flex-1">
          <span className="sr-only">{searchLabel}</span>
          <input
            type="search"
            className="cm-field w-full px-3.5"
            value={searchValue ?? ""}
            placeholder={searchPlaceholder}
            onChange={(event) => onSearchChange(event.target.value)}
          />
        </label>
      )}
      {filters && <div className="flex flex-wrap items-center gap-2">{filters}</div>}
      {actions && <div className="flex flex-wrap items-center gap-2 lg:ml-auto">{actions}</div>}
    </section>
  );
}
