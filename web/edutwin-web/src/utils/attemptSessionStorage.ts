export interface AttemptSessionScope {
  centerId: string;
  userId: string;
  subjectId: string;
  questionId: string;
}

export interface SessionStorageLike {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
  readonly length?: number;
  key?(index: number): string | null;
}

const PREFIX = "edutwin:attempt-session";

export function buildAttemptSessionKey(scope: AttemptSessionScope): string {
  return [PREFIX, scope.centerId, scope.userId, scope.subjectId, scope.questionId]
    .map((part, index) => {
      const normalized = part.trim();
      if (!normalized) throw new Error(`Attempt session scope part ${index} is required.`);
      return normalized;
    })
    .join(":");
}

export function getOrCreateAttemptSessionId(
  scope: AttemptSessionScope,
  storage: SessionStorageLike | null = getBrowserSessionStorage(),
  createId: () => string = createClientSubmissionId
): string {
  const key = buildAttemptSessionKey(scope);
  const existing = storage?.getItem(key)?.trim();
  if (existing) return existing;

  const next = createId();
  try {
    storage?.setItem(key, next);
  } catch {
    // The caller still has an idempotency key for this page instance when browser storage is unavailable.
  }
  return next;
}

export function setAttemptSessionId(
  scope: AttemptSessionScope,
  id: string,
  storage: SessionStorageLike | null = getBrowserSessionStorage()
): void {
  const normalized = id.trim();
  if (!normalized) throw new Error("Attempt session ID must not be empty.");
  try {
    storage?.setItem(buildAttemptSessionKey(scope), normalized);
  } catch {
    // Best effort persistence when browser storage is unavailable.
  }
}

export function clearAttemptSessionId(
  scope: AttemptSessionScope,
  storage: SessionStorageLike | null = getBrowserSessionStorage()
): void {
  try {
    storage?.removeItem(buildAttemptSessionKey(scope));
  } catch {
    // Session storage cleanup is best effort; server-side idempotency remains authoritative.
  }
}

/**
 * Clears every pending idempotency identity owned by one signed-in student.
 * This is intentionally scoped by both tenant and user so switching accounts in
 * the same browser tab can never reuse another student's submission identity.
 */
export function clearUserAttemptSessionIds(
  centerId: string,
  userId: string,
  storage: SessionStorageLike | null = getBrowserSessionStorage()
): void {
  const normalizedCenterId = centerId.trim();
  const normalizedUserId = userId.trim();
  if (!normalizedCenterId || !normalizedUserId || !storage?.key || typeof storage.length !== "number") {
    return;
  }

  const userPrefix = `${PREFIX}:${normalizedCenterId}:${normalizedUserId}:`;
  const keysToRemove: string[] = [];
  try {
    for (let index = 0; index < storage.length; index += 1) {
      const key = storage.key(index);
      if (key?.startsWith(userPrefix)) keysToRemove.push(key);
    }
    keysToRemove.forEach((key) => storage.removeItem(key));
  } catch {
    // Best effort cleanup. Server-side tenant/user idempotency remains authoritative.
  }
}

export function createClientSubmissionId(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return `submission-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

function getBrowserSessionStorage(): SessionStorageLike | null {
  try {
    return typeof window === "undefined" ? null : window.sessionStorage;
  } catch {
    return null;
  }
}
