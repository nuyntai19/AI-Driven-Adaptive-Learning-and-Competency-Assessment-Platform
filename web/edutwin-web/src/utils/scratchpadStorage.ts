import type { ScratchpadDraft } from "../types/scratchpad";

export const DB_NAME = "edutwin_scratchpad_db";
export const STORE_NAME = "drafts";
export const DB_VERSION = 1;
export const DEFAULT_TTL_MS = 24 * 60 * 60 * 1000; // 24 hours
export const MAX_STROKES = 400;
export const MAX_POINTS_PER_STROKE = 1_000;
export const MAX_TOTAL_POINTS = 20_000;
export const MAX_SERIALIZED_DRAFT_BYTES = 1_000_000;

/** Whether a save survived beyond the current JavaScript process. */
export type ScratchpadPersistence = "durable" | "volatile";

export function buildScratchpadKey(centerId: string, userId: string, clientSubmissionId: string): string {
  const c = requireScopePart("centerId", centerId);
  const u = requireScopePart("userId", userId);
  const s = requireScopePart("clientSubmissionId", clientSubmissionId);
  return `draft:${c}:${u}:${s}`;
}

function requireScopePart(name: string, value: string): string {
  const normalized = value.trim();
  if (!normalized) {
    throw new Error(`Scratchpad ${name} is required.`);
  }
  return normalized;
}

// In-memory fallback for environments without native IndexedDB (e.g. Node tests, SSR, private browsing)
const inMemoryStore = new Map<string, ScratchpadDraft>();

function isIndexedDbAvailable(): boolean {
  return typeof globalThis !== "undefined" && typeof globalThis.indexedDB !== "undefined";
}

let dbInstance: IDBDatabase | null = null;
let dbOpening: Promise<IDBDatabase> | null = null;

export function openDatabase(): Promise<IDBDatabase> {
  if (!isIndexedDbAvailable()) {
    return Promise.reject(new Error("IndexedDB is not available in this environment"));
  }

  if (dbInstance) {
    return Promise.resolve(dbInstance);
  }

  dbOpening ??= new Promise((resolve, reject) => {
    const request = globalThis.indexedDB.open(DB_NAME, DB_VERSION);

    request.onupgradeneeded = (event) => {
      const db = (event.target as IDBOpenDBRequest).result;
      if (!db.objectStoreNames.contains(STORE_NAME)) {
        const store = db.createObjectStore(STORE_NAME, { keyPath: "storageKey" });
        store.createIndex("by_user", ["centerId", "userId"], { unique: false });
        store.createIndex("by_expiresAt", "expiresAt", { unique: false });
      }
    };

    request.onsuccess = (event) => {
      dbInstance = (event.target as IDBOpenDBRequest).result;
      dbInstance.onversionchange = () => {
        dbInstance?.close();
        dbInstance = null;
        dbOpening = null;
      };
      resolve(dbInstance);
    };

    request.onerror = () => {
      dbOpening = null;
      reject(request.error || new Error("Failed to open IndexedDB"));
    };
  });

  return dbOpening;
}

export async function saveScratchpadDraft(draft: ScratchpadDraft): Promise<ScratchpadPersistence> {
  const now = Date.now();
  const expectedKey = buildScratchpadKey(draft.centerId, draft.userId, draft.clientSubmissionId);
  if (draft.storageKey && draft.storageKey !== expectedKey) {
    throw new Error("Scratchpad storage key does not match its scoped identity.");
  }
  const normalized: ScratchpadDraft = {
    ...draft,
    storageKey: expectedKey,
    updatedAt: draft.updatedAt || now,
    expiresAt: draft.expiresAt || (now + DEFAULT_TTL_MS),
  };
  validateDraftLimits(normalized);

  if (!isIndexedDbAvailable()) {
    inMemoryStore.set(normalized.storageKey, normalized);
    return "volatile";
  }

  try {
    const db = await openDatabase();
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction([STORE_NAME], "readwrite");
      const store = tx.objectStore(STORE_NAME);
      store.put(normalized);

      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error || new Error("Failed to save draft"));
      tx.onabort = () => reject(tx.error || new Error("Scratchpad save transaction was aborted"));
    });
    return "durable";
  } catch {
    // A memory fallback preserves the current session only. Callers must never present it as durable storage.
    inMemoryStore.set(normalized.storageKey, normalized);
    return "volatile";
  }
}

function validateDraftLimits(draft: ScratchpadDraft): void {
  if (draft.strokes.length > MAX_STROKES) {
    throw new Error(`Scratchpad supports at most ${MAX_STROKES} strokes per draft.`);
  }

  let totalPoints = 0;
  for (const stroke of draft.strokes) {
    if (stroke.points.length > MAX_POINTS_PER_STROKE) {
      throw new Error(`Scratchpad supports at most ${MAX_POINTS_PER_STROKE} points per stroke.`);
    }
    totalPoints += stroke.points.length;
  }
  if (totalPoints > MAX_TOTAL_POINTS) {
    throw new Error(`Scratchpad supports at most ${MAX_TOTAL_POINTS} freehand points per draft.`);
  }

  const serializedBytes = new TextEncoder().encode(JSON.stringify(draft)).byteLength;
  if (serializedBytes > MAX_SERIALIZED_DRAFT_BYTES) {
    throw new Error("Scratchpad draft exceeds the 1 MB local storage limit.");
  }
}

export async function getScratchpadDraft(
  centerId: string,
  userId: string,
  clientSubmissionId: string
): Promise<ScratchpadDraft | null> {
  const key = buildScratchpadKey(centerId, userId, clientSubmissionId);
  const now = Date.now();

  if (!isIndexedDbAvailable()) {
    const draft = inMemoryStore.get(key) || null;
    if (draft && draft.expiresAt <= now) {
      inMemoryStore.delete(key);
      return null;
    }
    return draft;
  }

  try {
    const db = await openDatabase();
    const draft = await new Promise<ScratchpadDraft | null>((resolve, reject) => {
      const tx = db.transaction([STORE_NAME], "readonly");
      const store = tx.objectStore(STORE_NAME);
      const req = store.get(key);

      req.onsuccess = () => resolve((req.result as ScratchpadDraft | undefined) ?? null);
      req.onerror = () => reject(req.error || new Error("Failed to read draft"));
    });

    if (draft && draft.expiresAt <= now) {
      await deleteScratchpadDraft(centerId, userId, clientSubmissionId);
      return null;
    }

    return draft;
  } catch {
    const draft = inMemoryStore.get(key) || null;
    if (draft && draft.expiresAt <= now) {
      inMemoryStore.delete(key);
      return null;
    }
    return draft;
  }
}

export async function deleteScratchpadDraft(
  centerId: string,
  userId: string,
  clientSubmissionId: string
): Promise<void> {
  const key = buildScratchpadKey(centerId, userId, clientSubmissionId);
  inMemoryStore.delete(key);

  if (!isIndexedDbAvailable()) {
    return;
  }

  try {
    const db = await openDatabase();
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction([STORE_NAME], "readwrite");
      const store = tx.objectStore(STORE_NAME);
      store.delete(key);

      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error || new Error("Failed to delete draft"));
      tx.onabort = () => reject(tx.error || new Error("Scratchpad delete transaction was aborted"));
    });
  } catch {
    // Ignore error if already deleted
  }
}

export async function clearUserScratchpadDrafts(centerId: string, userId: string): Promise<number> {
  const targetCenterId = requireScopePart("centerId", centerId);
  const targetUserId = requireScopePart("userId", userId);
  let deletedCount = 0;

  // Clear in-memory entries
  for (const [key, draft] of inMemoryStore.entries()) {
    if (draft.centerId === targetCenterId && draft.userId === targetUserId) {
      inMemoryStore.delete(key);
      deletedCount++;
    }
  }

  if (!isIndexedDbAvailable()) {
    return deletedCount;
  }

  try {
    const db = await openDatabase();
    const dbDeletedCount = await new Promise<number>((resolve, reject) => {
      const tx = db.transaction([STORE_NAME], "readwrite");
      const store = tx.objectStore(STORE_NAME);
      let count = 0;

      const req = store.openCursor();
      req.onsuccess = (event) => {
        const cursor = (event.target as IDBRequest<IDBCursorWithValue>).result;
        if (cursor) {
          const draft = cursor.value as ScratchpadDraft;
          if (draft.centerId === targetCenterId && draft.userId === targetUserId) {
            cursor.delete();
            count++;
          }
          cursor.continue();
        } else {
          resolve(count);
        }
      };

      req.onerror = () => reject(req.error || new Error("Failed to clear user drafts"));
      tx.onerror = () => reject(tx.error || new Error("Failed to clear user drafts"));
      tx.onabort = () => reject(tx.error || new Error("Scratchpad clear transaction was aborted"));
    });

    return deletedCount + dbDeletedCount;
  } catch {
    return deletedCount;
  }
}

export async function cleanupExpiredScratchpadDrafts(): Promise<number> {
  const now = Date.now();
  let purgedCount = 0;

  // Purge in-memory entries
  for (const [key, draft] of inMemoryStore.entries()) {
    if (draft.expiresAt <= now) {
      inMemoryStore.delete(key);
      purgedCount++;
    }
  }

  if (!isIndexedDbAvailable()) {
    return purgedCount;
  }

  try {
    const db = await openDatabase();
    const dbPurgedCount = await new Promise<number>((resolve, reject) => {
      const tx = db.transaction([STORE_NAME], "readwrite");
      const store = tx.objectStore(STORE_NAME);
      let count = 0;

      const req = store.openCursor();
      req.onsuccess = (event) => {
        const cursor = (event.target as IDBRequest<IDBCursorWithValue>).result;
        if (cursor) {
          const draft = cursor.value as ScratchpadDraft;
          if (draft.expiresAt <= now) {
            cursor.delete();
            count++;
          }
          cursor.continue();
        } else {
          resolve(count);
        }
      };

      req.onerror = () => reject(req.error || new Error("Failed to cleanup expired drafts"));
      tx.onerror = () => reject(tx.error || new Error("Failed to cleanup expired drafts"));
      tx.onabort = () => reject(tx.error || new Error("Scratchpad cleanup transaction was aborted"));
    });

    return purgedCount + dbPurgedCount;
  } catch {
    return purgedCount;
  }
}

/** Test utility: reset database connection and in-memory cache */
export async function __resetScratchpadStorageForTesting(): Promise<void> {
  inMemoryStore.clear();
  if (dbInstance) {
    dbInstance.close();
    dbInstance = null;
  }
  dbOpening = null;

  if (!isIndexedDbAvailable()) return;

  await new Promise<void>((resolve, reject) => {
    const request = globalThis.indexedDB.deleteDatabase(DB_NAME);
    request.onsuccess = () => resolve();
    request.onerror = () => reject(request.error || new Error("Failed to reset IndexedDB"));
    request.onblocked = () => reject(new Error("IndexedDB reset was blocked"));
  });
}

/** Test utility: close the connection while retaining the persisted database. */
export function __closeScratchpadDatabaseForTesting(): void {
  if (dbInstance) {
    dbInstance.close();
    dbInstance = null;
  }
  dbOpening = null;
}
