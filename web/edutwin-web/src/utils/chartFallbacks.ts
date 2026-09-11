export interface TableColumn<T> {
  key: keyof T;
  label: string;
  format?: (val: T[keyof T], row: T) => string;
}

export function isChartDataEmpty<T>(data: T[] | null | undefined): boolean {
  return !data || data.length === 0;
}

export function toAccessibleTableData<T extends Record<string, unknown>>(
  data: T[] | null | undefined,
  columns: TableColumn<T>[]
): { headers: string[]; rows: string[][] } {
  if (isChartDataEmpty(data)) {
    return {
      headers: columns.map((c) => c.label),
      rows: [],
    };
  }

  const headers = columns.map((c) => c.label);
  const rows = (data ?? []).map((row) =>
    columns.map((col) => {
      const val = row[col.key];
      if (col.format) {
        return col.format(val, row);
      }
      return val !== null && val !== undefined ? String(val) : "-";
    })
  );

  return { headers, rows };
}
