/** Titles are nullable in the API. Use the same fallback for display and search. */
export function frameworkTitle(version: { title?: string | null; gradeLevel: number }): string {
  return version.title?.trim() || `Khung Khối ${version.gradeLevel}`;
}
