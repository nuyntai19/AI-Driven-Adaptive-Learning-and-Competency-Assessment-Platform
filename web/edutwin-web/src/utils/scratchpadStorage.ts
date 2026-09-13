import type { ScratchpadDraft } from "../types/scratchpad";

export const DB_NAME = "edutwin_scratchpad_db";
export const STORE_NAME = "drafts";
export const DB_VERSION = 1;
export const DEFAULT_TTL_MS = 24 * 60 * 60 * 1000; // 24 hours

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

export async function saveScratchpadDraft(draft: ScratchpadDraft): Promise<void> {
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

  if (!isIndexedDbAvailable()) {
    inMemoryStore.set(normalized.storageKey, normalized);
    return;
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
  } catch {
    // Fallback to in-memory if IndexedDB transaction fails
    inMemoryStore.set(normalized.storageKey, normalized);
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
export function __resetScratchpadStorageForTesting(): void {
  inMemoryStore.clear();
  if (dbInstance) {
    dbInstance.close();
    dbInstance = null;
  }
  dbOpening = null;
}
