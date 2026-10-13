export interface SnapshotUploadTracker {
  uploadId: number;
  dataUrl: string;
  promise: Promise<string | null>;
}

export interface DraftAnswerLike {
  snapshotDataUrl?: string | null;
  drawingUploadToken?: string | null;
}

export interface ExecuteSnapshotUploadOptions {
  qId: string;
  dataUrl: string;
  blob?: Blob | null;
  snapshotUploadSeqRef: { current: number };
  snapshotUploadsRef: { current: Record<string, SnapshotUploadTracker> };
  getCurrentDraftAnswers: () => Record<string, DraftAnswerLike | undefined>;
  isCurrentScope?: () => boolean;
  onTokenStored?: (qId: string, token: string) => void;
  getActiveQuestionId?: () => string | null;
  onActiveQuestionTokenUpdated?: (token: string) => void;
  prepareUpload: (blob: Blob) => Promise<{ drawingUploadToken?: string } | null | undefined>;
  convertDataUrlToBlob?: (dataUrl: string) => Promise<Blob>;
}

/**
 * Prunes any snapshot upload trackers whose tracked dataUrl does not match
 * the authoritative snapshot in draftAnswers (e.g. after sync from server or draft reset).
 */
export function pruneMismatchedSnapshotUploads(
  snapshotUploadsRef: { current: Record<string, SnapshotUploadTracker> },
  draftAnswers: Record<string, DraftAnswerLike | undefined>
): void {
  for (const qId of Object.keys(snapshotUploadsRef.current)) {
    const tracker = snapshotUploadsRef.current[qId];
    if (!tracker) continue;
    const currentSnap = draftAnswers[qId]?.snapshotDataUrl;
    if (currentSnap !== tracker.dataUrl) {
      delete snapshotUploadsRef.current[qId];
    }
  }
}

/**
 * Executes or deduplicates an attempt attachment upload for a question's scratchpad snapshot.
 *
 * Guarantees:
 * 1. Monotonic uploadId tracks each separate upload attempt.
 * 2. Reuses in-flight or cached promise for the exact same question + dataUrl.
 * 3. On upload failure (network error, null token, invalidation), cleans up tracker ONLY if it still belongs
 *    to this uploadId (never deletes a newer upload's tracker).
 * 4. On draft replacement / server sync during in-flight upload (mismatched snapshotDataUrl),
 *    discards the token AND cleans up this upload's tracker so subsequent re-attaching starts a fresh upload.
 */
export function executeSnapshotUpload(options: ExecuteSnapshotUploadOptions): Promise<string | null> {
  const {
    qId,
    dataUrl,
    blob,
    snapshotUploadSeqRef,
    snapshotUploadsRef,
    getCurrentDraftAnswers,
    isCurrentScope,
    onTokenStored,
    getActiveQuestionId,
    onActiveQuestionTokenUpdated,
    prepareUpload,
    convertDataUrlToBlob,
  } = options;

  if (!qId || !dataUrl || isCurrentScope?.() === false) return Promise.resolve(null);

  // 1. If an upload for this EXACT question and dataUrl is ALREADY in flight, reuse its promise
  const existing = snapshotUploadsRef.current[qId];
  if (existing && existing.dataUrl === dataUrl) {
    return existing.promise;
  }

  // 2. Start a new upload with a monotonic uploadId
  const uploadId = ++snapshotUploadSeqRef.current;

  const clearOwnTracker = () => {
    if (snapshotUploadsRef.current[qId]?.uploadId === uploadId) {
      delete snapshotUploadsRef.current[qId];
    }
  };

  // Register the tracker before invoking external code, including an uploader
  // that throws synchronously. Failed promises must never remain cached.
  const uploadPromise = Promise.resolve().then(async (): Promise<string | null> => {
    try {
      if (isCurrentScope?.() === false) {
        clearOwnTracker();
        return null;
      }
      let blobToUpload = blob;
      if (!blobToUpload) {
        if (convertDataUrlToBlob) {
          blobToUpload = await convertDataUrlToBlob(dataUrl);
        } else {
          const res = await fetch(dataUrl);
          blobToUpload = await res.blob();
        }
      }

      if (isCurrentScope?.() === false) {
        clearOwnTracker();
        return null;
      }
      const uploadIntent = await prepareUpload(blobToUpload);
      const token = uploadIntent?.drawingUploadToken;

      if (!token) {
        clearOwnTracker();
        return null;
      }

      // Validate on completion:
      // Check if this upload tracker is still active and matches the current dataUrl
      const current = snapshotUploadsRef.current[qId];
      if (!current || current.uploadId !== uploadId || current.dataUrl !== dataUrl) {
        // The snapshot was replaced or deleted while upload was in flight! Discard token.
        clearOwnTracker();
        return null;
      }

      if (isCurrentScope?.() === false) {
        clearOwnTracker();
        return null;
      }

      // Check current answers in memory/draft: does question still have this exact snapshotDataUrl?
      const draftAnswers = getCurrentDraftAnswers();
      const currentAns = draftAnswers[qId];
      if (!currentAns || currentAns.snapshotDataUrl !== dataUrl) {
        // Snapshot was deleted or replaced in assignment answers (e.g. server sync or user removal)
        clearOwnTracker();
        return null;
      }

      // Update question's stored answers with the token
      onTokenStored?.(qId, token);

      // Synchronize component active state ONLY if user is currently viewing this question
      if (getActiveQuestionId && onActiveQuestionTokenUpdated) {
        if (getActiveQuestionId() === qId) {
          onActiveQuestionTokenUpdated(token);
        }
      }

      return token;
    } catch (err) {
      console.warn(`Snapshot upload error for question ${qId}:`, err);
      clearOwnTracker();
      return null;
    }
  });

  snapshotUploadsRef.current[qId] = {
    uploadId,
    dataUrl,
    promise: uploadPromise,
  };

  return uploadPromise;
}
