import type { StudentAcademicContextDto } from "../types/dashboards";

export function studentClassesInView(classes: StudentAcademicContextDto["classes"], history: boolean) {
  return classes.filter(c => c.isHistorical === history);
}

export type StudentAcademicClassItem = StudentAcademicContextDto["classes"][number];

/** Format class name into a natural Vietnamese display label, e.g. 'Lớp Toán 11', 'Lớp 11A1', 'Khối 11'. */
export function formatStudentClassBadge(cls?: { className?: string; gradeLevel?: number | null } | null): string {
  if (!cls) return "";
  const raw = cls.className?.trim() || "";
  if (raw) {
    const lower = raw.toLowerCase();
    if (lower.startsWith("lớp") || lower.startsWith("khối")) {
      return raw;
    }
    return `Lớp ${raw}`;
  }
  if (cls.gradeLevel) {
    return `Khối ${cls.gradeLevel}`;
  }
  return "";
}

/** Format the header/profile subtitle, e.g. 'Lớp Toán 11 · Học sinh' or 'Học sinh'. */
export function formatStudentProfileSubtitle(cls?: { className?: string; gradeLevel?: number | null } | null): string {
  const badge = formatStudentClassBadge(cls);
  return badge ? `${badge} · Học sinh` : "Học sinh";
}

/** Resolve student's currently active class for header display. */
export function resolveStudentActiveDisplayClass(
  classes: StudentAcademicClassItem[],
  selectedClassId?: string | null,
  activeClassId?: string | null
): StudentAcademicClassItem | null {
  if (!classes || classes.length === 0) return null;
  const targetId = selectedClassId || activeClassId;
  if (targetId) {
    const matched = classes.find((c) => c.classId === targetId);
    if (matched) return matched;
  }
  return classes.find((c) => !c.isHistorical) || classes[0] || null;
}

/** Replace stale bookmarked selections; never fall back from history to a current class. */
export function reconcileStudentClassParam(params: URLSearchParams, selectedClassId: string | null): URLSearchParams | null {
  const nextId = params.get("subjectId") ? selectedClassId || "" : "";
  if ((params.get("classId") || "") === nextId) return null;
  const next = new URLSearchParams(params);
  if (nextId) next.set("classId", nextId); else next.delete("classId");
  return next;
}

