import type { ReactNode } from "react";
import { Skeleton } from "./CenterManagerPrimitives";

export interface DataTableColumn<T> {
  id: string;
  header: string;
  render: (item: T) => ReactNode;
  align?: "left" | "center" | "right";
  width?: string;
}

interface DataTableProps<T> {
  caption: string;
  columns: DataTableColumn<T>[];
  rows: T[];
  rowKey: (item: T) => string;
  isLoading?: boolean;
  emptyTitle?: string;
  emptyDescription?: string;
  page?: number;
  totalPages?: number;
  totalItems?: number;
  onPageChange?: (page: number) => void;
}

export function DataTable<T>({
  caption,
  columns,
  rows,
  rowKey,
  isLoading = false,
  emptyTitle = "Chưa có dữ liệu",
  emptyDescription = "Dữ liệu phù hợp sẽ xuất hiện tại đây.",
  page = 1,
  totalPages = 1,
  totalItems,
  onPageChange,
}: DataTableProps<T>) {
  const alignment = (value?: DataTableColumn<T>["align"]) => value === "right" ? "text-right" : value === "center" ? "text-center" : "text-left";

  return (
    <section className="cm-surface overflow-hidden" aria-busy={isLoading}>
      <div className="cm-table-scroll max-w-full overflow-x-auto">
        <table className="w-full min-w-[42rem] border-collapse text-sm">
          <caption className="sr-only">{caption}</caption>
          <thead className="sticky top-0 z-10 bg-[var(--cm-surface-raised)] text-xs uppercase tracking-wide text-[var(--cm-text-secondary)]">
            <tr>
              {columns.map((column) => (
                <th key={column.id} scope="col" className={`border-b border-[var(--cm-border-subtle)] px-5 py-3.5 font-semibold ${alignment(column.align)}`} style={{ width: column.width }}>
                  {column.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-[var(--cm-border-subtle)]">
            {isLoading && Array.from({ length: 4 }, (_, rowIndex) => (
              <tr key={`skeleton-${rowIndex}`}>
                {columns.map((column) => <td key={column.id} className="px-5 py-4"><Skeleton decorative /></td>)}
              </tr>
            ))}
            {!isLoading && rows.map((item) => (
              <tr key={rowKey(item)} className="bg-[var(--cm-surface)] transition-colors hover:bg-[var(--cm-surface-raised)]">
                {columns.map((column) => (
                  <td key={column.id} className={`px-5 py-4 text-[var(--cm-text)] ${alignment(column.align)}`}>{column.render(item)}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {!isLoading && rows.length === 0 && (
        <div className="px-6 py-14 text-center" role="status">
          <p className="font-semibold text-[var(--cm-text)]">{emptyTitle}</p>
          <p className="mt-1 text-sm text-[var(--cm-text-secondary)]">{emptyDescription}</p>
        </div>
      )}

      {!isLoading && rows.length > 0 && (totalPages > 1 || totalItems !== undefined) && (
        <nav aria-label={`Phân trang ${caption}`} className="flex items-center justify-between gap-4 border-t border-[var(--cm-border-subtle)] px-5 py-3">
          <p className="text-xs text-[var(--cm-text-secondary)]">
            {totalItems === undefined ? `Trang ${page} / ${totalPages}` : `${totalItems.toLocaleString("vi-VN")} kết quả · Trang ${page} / ${totalPages}`}
          </p>
          <div className="flex gap-2">
            <button type="button" className="cm-secondary-button" disabled={!onPageChange || page <= 1} onClick={() => onPageChange?.(page - 1)}>Trước</button>
            <button type="button" className="cm-secondary-button" disabled={!onPageChange || page >= totalPages} onClick={() => onPageChange?.(page + 1)}>Sau</button>
          </div>
        </nav>
      )}
    </section>
  );
}
