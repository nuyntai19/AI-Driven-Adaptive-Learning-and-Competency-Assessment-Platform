export interface AssignmentDraftScope {
  centerId: string;
  userId: string;
  assignmentId: string;
}

export interface LocalStorageLike {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
  readonly length?: number;
  key?(index: number): string | null;
}

const SCOPED_ANSWERS_PREFIX = "edutwin:assignment-answers";
const LEGACY_ANSWERS_PREFIX = "edutwin_assignment_answers_";
const LEGACY_SNAPSHOT_PREFIX = "edutwin_assignment_snapshot_";

export function buildAssignmentDraftKey(scope: AssignmentDraftScope): string {
  const parts = [scope.centerId, scope.userId, scope.assignmentId].map((part, index) => {
    const normalized = part.trim();
    if (!normalized) throw new Error(`Assignment draft scope part ${index} is required.`);
    return normalized;
  });

  return [SCOPED_ANSWERS_PREFIX, ...parts].join(":");
}

export function readAssignmentDraft(
  scope: AssignmentDraftScope,
  storage: LocalStorageLike | null = getBrowserLocalStorage()
): string | null {
  try {
    return storage?.getItem(buildAssignmentDraftKey(scope)) ?? null;
  } catch {
    return null;
  }
}

export function writeAssignmentDraft(
  scope: AssignmentDraftScope,
  value: string,
  storage: LocalStorageLike | null = getBrowserLocalStorage()
): void {
  try {
    storage?.setItem(buildAssignmentDraftKey(scope), value);
  } catch {
    // Draft persistence is best effort when browser storage is unavailable or full.
  }
}

export function removeAssignmentDraft(
  scope: AssignmentDraftScope,
  storage: LocalStorageLike | null = getBrowserLocalStorage()
): void {
  try {
    storage?.removeItem(buildAssignmentDraftKey(scope));
  } catch {
    // Draft cleanup is best effort.
  }
}

/**
 * Deletes the historical assignment-only keys. They cannot be migrated safely
 * because the browser does not record which student originally created them.
 */
export function clearLegacyAssignmentDraftsForAssignment(
  assignmentId: string,
  storage: LocalStorageLike | null = getBrowserLocalStorage()
): void {
  const normalizedAssignmentId = assignmentId.trim();
  if (!normalizedAssignmentId || !storage) return;

  try {
    storage.removeItem(`${LEGACY_ANSWERS_PREFIX}${normalizedAssignmentId}`);
    storage.removeItem(`${LEGACY_SNAPSHOT_PREFIX}${normalizedAssignmentId}`);
    storage.removeItem(`${LEGACY_SNAPSHOT_PREFIX}time_${normalizedAssignmentId}`);
  } catch {
    // Legacy cleanup is best effort.
  }
}

export function clearAllLegacyAssignmentDrafts(
  storage: LocalStorageLike | null = getBrowserLocalStorage()
): void {
  if (!storage?.key || typeof storage.length !== "number") return;

  const keysToRemove: string[] = [];
  try {
    for (let index = 0; index < storage.length; index += 1) {
      const key = storage.key(index);
      if (key?.startsWith(LEGACY_ANSWERS_PREFIX) || key?.startsWith(LEGACY_SNAPSHOT_PREFIX)) {
        keysToRemove.push(key);
      }
    }
    keysToRemove.forEach((key) => storage.removeItem(key));
  } catch {
    // Legacy cleanup is best effort.
  }
}

function getBrowserLocalStorage(): LocalStorageLike | null {
  try {
    return typeof window === "undefined" ? null : window.localStorage;
  } catch {
    return null;
  }
}
