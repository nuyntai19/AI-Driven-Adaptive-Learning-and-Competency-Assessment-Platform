export const teacherNotesKey = (centerId?: string, userId?: string): string | null =>
  centerId && userId ? `teacher_pedagogical_notes:${centerId}:${userId}` : null;

/** Notes written before they moved to the server, for a one-time upload. */
export function legacyTeacherNotes(centerId?: string, userId?: string) {
  const key = teacherNotesKey(centerId, userId);
  return {
    notes: readTeacherNotes(key),
    clear: () => { if (key) { try { localStorage.removeItem(key); } catch { /* best effort */ } } },
  };
}

export function readTeacherNotes(key: string | null, storage: Pick<Storage, "getItem"> = localStorage): Record<string, string> {
  if (!key) return {};
  try {
    const parsed: unknown = JSON.parse(storage.getItem(key) || "{}");
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return {};
    return Object.fromEntries(Object.entries(parsed).filter(([, value]) => typeof value === "string"));
  } catch { return {}; }
}
