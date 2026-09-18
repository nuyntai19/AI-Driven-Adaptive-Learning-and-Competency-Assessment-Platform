import type { ReactNode } from "react";
import { TeacherSkeleton } from "./TeacherPrimitives";

export interface TeacherTableColumn<T> {
  key: string;
  header: ReactNode;
  render: (item: T) => ReactNode;
  className?: string;
  headerClassName?: string;
}

interface TeacherDataTableProps<T> {
  columns: TeacherTableColumn<T>[];
  data: T[];
  keyExtractor: (item: T) => string;
  isLoading?: boolean;
  emptyMessage?: string;
  className?: string;
}

export function TeacherDataTable<T>({
  columns,
  data,
  keyExtractor,
  isLoading = false,
  emptyMessage = "Không tìm thấy dữ liệu phù hợp.",
  className = "",
}: TeacherDataTableProps<T>) {
  if (isLoading) {
    return (
      <div className={`th-surface overflow-hidden ${className}`}>
        <div className="space-y-3 p-6">
          <TeacherSkeleton className="h-8 w-1/3" />
          <TeacherSkeleton className="h-12 w-full" />
          <TeacherSkeleton className="h-12 w-full" />
          <TeacherSkeleton className="h-12 w-full" />
        </div>
      </div>
    );
  }

  return (
    <div className={`th-surface overflow-x-auto ${className}`}>
      <table className="w-full text-left text-sm border-collapse">
        <thead>
          <tr className="border-b-[1.5px] border-[var(--th-border)] bg-[var(--th-surface-muted)] text-[11px] font-black uppercase tracking-wider text-[var(--th-text)]">
            {columns.map((col) => (
              <th key={col.key} scope="col" className={`px-5 py-3.5 ${col.headerClassName ?? ""}`}>
                {col.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-[var(--th-border-subtle)]">
          {data.length === 0 ? (
            <tr>
              <td colSpan={columns.length} className="p-8 text-center text-xs font-medium text-[var(--th-text-muted)]">
                {emptyMessage}
              </td>
            </tr>
          ) : (
            data.map((item) => (
              <tr key={keyExtractor(item)} className="transition-colors hover:bg-[var(--th-surface-muted)]/60">
                {columns.map((col) => (
                  <td key={`${keyExtractor(item)}-${col.key}`} className={`px-5 py-3.5 font-medium text-[var(--th-text)] ${col.className ?? ""}`}>
                    {col.render(item)}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
