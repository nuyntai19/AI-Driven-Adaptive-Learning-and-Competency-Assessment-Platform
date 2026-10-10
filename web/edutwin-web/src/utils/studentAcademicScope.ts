import type { StudentAcademicContextDto } from "../types/dashboards";

export function studentClassesInView(classes: StudentAcademicContextDto["classes"], history: boolean) {
  return classes.filter(c => c.isHistorical === history);
}

/** Replace stale bookmarked selections; never fall back from history to a current class. */
export function reconcileStudentClassParam(params: URLSearchParams, selectedClassId: string | null): URLSearchParams | null {
  const nextId = params.get("subjectId") ? selectedClassId || "" : "";
  if ((params.get("classId") || "") === nextId) return null;
  const next = new URLSearchParams(params);
  if (nextId) next.set("classId", nextId); else next.delete("classId");
  return next;
}
