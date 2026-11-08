/** Which subjects a student may switch between, derived from their classes. */
export function studentSubjectsInView<S extends { subjectId: string }>(
  subjects: S[],
  classes: Array<{ subjectId: string; isHistorical: boolean }>,
  history: boolean
): S[] {
  const inView = classes.filter((c) => c.isHistorical === history);
  // A student without past classes still needs a subject to see the empty history.
  const source = history && inView.length === 0 ? classes.filter((c) => !c.isHistorical) : inView;
  const ids = new Set(source.map((c) => c.subjectId));
  return subjects.filter((s) => ids.has(s.subjectId));
}

/** The subject to show: the current one if the student studies it, else the last chosen, else the first. */
export function resolveStudentSubjectId(
  subjects: Array<{ subjectId: string }>,
  currentId: string,
  rememberedId: string | null
): string | null {
  const studies = (id: string | null) => !!id && subjects.some((s) => s.subjectId === id);
  if (studies(currentId)) return currentId;
  if (studies(rememberedId)) return rememberedId;
  return subjects[0]?.subjectId ?? null;
}

const lastSubjectKey = (userId: string) => `edutwin_last_subject_id:${userId}`;

export function readLastStudentSubject(userId?: string): string | null {
  if (!userId) return null;
  try {
    return localStorage.getItem(lastSubjectKey(userId));
  } catch {
    return null;
  }
}

export function rememberStudentSubject(userId: string | undefined, subjectId: string): void {
  if (!userId) return;
  try {
    localStorage.setItem(lastSubjectKey(userId), subjectId);
  } catch {
    // The URL still carries the selection when storage is unavailable.
  }
}
