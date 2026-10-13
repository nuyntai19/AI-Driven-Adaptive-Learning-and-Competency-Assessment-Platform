export const teacherNotesKey = (centerId?: string, userId?: string): string | null =>
  centerId && userId ? `teacher_pedagogical_notes:${centerId}:${userId}` : null;

export function readTeacherNotes(key: string | null, storage: Pick<Storage, "getItem"> = localStorage): Record<string, string> {
  if (!key) return {};
  try {
    const parsed: unknown = JSON.parse(storage.getItem(key) || "{}");
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return {};
    return Object.fromEntries(Object.entries(parsed).filter(([, value]) => typeof value === "string"));
  } catch { return {}; }
}
