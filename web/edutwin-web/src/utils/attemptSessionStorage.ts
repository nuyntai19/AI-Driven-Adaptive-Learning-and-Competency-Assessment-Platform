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
