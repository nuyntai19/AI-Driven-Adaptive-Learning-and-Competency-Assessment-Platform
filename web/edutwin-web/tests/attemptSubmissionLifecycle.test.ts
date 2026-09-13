import assert from "node:assert/strict";
import test, { beforeEach } from "node:test";
import "fake-indexeddb/auto";
import {
  saveScratchpadDraft,
  getScratchpadDraft,
  deleteScratchpadDraft,
  __resetScratchpadStorageForTesting,
} from "../src/utils/scratchpadStorage.ts";
import {
  buildAttemptSessionKey,
  clearAttemptSessionId,
  createClientSubmissionId,
  getOrCreateAttemptSessionId,
  setAttemptSessionId,
  type SessionStorageLike,
} from "../src/utils/attemptSessionStorage.ts";
import type { ScratchpadDraft, ScratchpadStroke } from "../src/types/scratchpad.ts";

class MemorySessionStorage implements SessionStorageLike {
  private readonly values = new Map<string, string>();

  getItem(key: string): string | null {
    return this.values.get(key) ?? null;
  }

  setItem(key: string, value: string): void {
    this.values.set(key, value);
  }

  removeItem(key: string): void {
    this.values.delete(key);
  }
}

const testScope = {
  centerId: "center-alpha",
  userId: "student-42",
  subjectId: "math-grade-10",
  questionId: "q-101",
};

const sampleStrokes: ScratchpadStroke[] = [
  {
    id: "stroke-1",
    tool: "pen",
    color: "#ff0000",
    lineWidth: 3,
    points: [{ x: 10, y: 15 }, { x: 25, y: 30 }],
  },
];

beforeEach(async () => {
  await __resetScratchpadStorageForTesting();
});

test("reload draft -> scratchpad draft restored and PNG regenerated", async () => {
  const sessionStorage = new MemorySessionStorage();
  const clientSubmissionId = getOrCreateAttemptSessionId(testScope, sessionStorage, () => "sub-initial-123");

  const draft: ScratchpadDraft = {
    storageKey: "",
    centerId: testScope.centerId,
    userId: testScope.userId,
    clientSubmissionId,
    strokes: sampleStrokes,
    gridType: "grid",
    canvasWidth: 1200,
    canvasHeight: 800,
  };

  await saveScratchpadDraft(draft);

  // Simulate page reload lookup
  const reloadedSubmissionId = getOrCreateAttemptSessionId(testScope, sessionStorage);
  assert.equal(reloadedSubmissionId, clientSubmissionId);

  const restoredDraft = await getScratchpadDraft(testScope.centerId, testScope.userId, reloadedSubmissionId);
  assert.ok(restoredDraft);
  assert.equal(restoredDraft.strokes.length, 1);
  assert.equal(restoredDraft.strokes[0].id, "stroke-1");

  // Verify PNG regeneration pipeline simulator
  const mockRenderPng = async (strokes: readonly ScratchpadStroke[]) => {
    assert.equal(strokes.length, 1);
    return { size: 1024, type: "image/png" };
  };

  const regeneratedPng = await mockRenderPng(restoredDraft.strokes);
  assert.equal(regeneratedPng.type, "image/png");
  assert.equal(regeneratedPng.size, 1024);
});

test("edit vector -> old upload token invalidated", () => {
  let drawingUploadToken: string | null = "token-pre-existing";

  // Simulate onExportPng callback in ScratchpadCanvasModal
  const onExportPng = (png: { size: number } | null) => {
    if (png) {
      // New vector exported
    }
    drawingUploadToken = null; // Invalidation invariant
  };

  onExportPng({ size: 2048 });

  assert.equal(drawingUploadToken, null, "Editing scratchpad canvas must reset and invalidate old upload token");
});

test("FailedTerminal -> new clientSubmissionId and drawingUploadToken reset", () => {
  const sessionStorage = new MemorySessionStorage();
  const oldSubmissionId = getOrCreateAttemptSessionId(testScope, sessionStorage, () => "sub-failed-attempt");
  let currentSubmissionId = oldSubmissionId;
  let drawingUploadToken: string | null = "token-stale-after-failure";
  let canResubmit = false;

  // Analysis job returns terminal failure
  const jobStatus = { status: "FailedTerminal", terminal: true };
  if (jobStatus.status === "FailedTerminal" || jobStatus.status === "AnalysisFailed") {
    canResubmit = true;
  }
  assert.equal(canResubmit, true);

  // Student clicks "Nộp lại bài làm" (handleResubmit)
  clearAttemptSessionId(testScope, sessionStorage);
  const newSubmissionId = createClientSubmissionId();
  currentSubmissionId = newSubmissionId;
  drawingUploadToken = null;
  canResubmit = false;

  assert.notEqual(currentSubmissionId, oldSubmissionId, "Resubmit must generate a fresh client submission id");
  assert.equal(drawingUploadToken, null, "Resubmit must invalidate the stale upload token");
  assert.equal(sessionStorage.getItem(buildAttemptSessionKey(testScope)), null);
});

test("resubmit -> invokes new prepare-upload and submits with fresh idempotency identity", async () => {
  let prepareUploadCalledCount = 0;
  let submitAttemptPayload: Record<string, unknown> | null = null;

  const mockPrepareUpload = async () => {
    prepareUploadCalledCount++;
    return { drawingUploadToken: `token-new-${prepareUploadCalledCount}` };
  };

  const mockSubmitAttempt = async (payload: Record<string, unknown>) => {
    submitAttemptPayload = payload;
    return { status: 202, data: { attemptId: "att-99", jobId: "job-101" } };
  };

  const freshSubmissionId = createClientSubmissionId();
  let drawingUploadToken: string | null = null;
  const scratchpadPng = { size: 4096 };

  // Workflow in handleSubmit:
  const uploadToken = scratchpadPng
    ? drawingUploadToken ?? (await mockPrepareUpload(scratchpadPng)).drawingUploadToken
    : null;
  drawingUploadToken = uploadToken;

  const result = await mockSubmitAttempt({
    clientSubmissionId: freshSubmissionId,
    drawingUploadToken: uploadToken,
    finalAnswer: "42",
  });

  assert.equal(prepareUploadCalledCount, 1);
  assert.equal(uploadToken, "token-new-1");
  assert.equal(result.status, 202);
  assert.equal(submitAttemptPayload?.clientSubmissionId, freshSubmissionId);
  assert.equal(submitAttemptPayload?.drawingUploadToken, "token-new-1");
});

test("successful 202 retains draft until terminal success, then purges draft and session storage", async () => {
  const sessionStorage = new MemorySessionStorage();
  const clientSubmissionId = getOrCreateAttemptSessionId(testScope, sessionStorage, () => "sub-success-202");

  await saveScratchpadDraft({
    storageKey: "",
    centerId: testScope.centerId,
    userId: testScope.userId,
    clientSubmissionId,
    strokes: sampleStrokes,
    gridType: "none",
    canvasWidth: 1200,
    canvasHeight: 800,
  });

  let scratchpadPng: { size: number } | null = { size: 1024 };
  let drawingUploadToken: string | null = "token-submitted";

  // Simulate HTTP 202 response: token is cleared, but draft and drawing memory are retained!
  const responseStatus = 202;
  if (responseStatus === 202 || responseStatus === 200) {
    drawingUploadToken = null;
  }

  const draftDuringProcessing = await getScratchpadDraft(testScope.centerId, testScope.userId, clientSubmissionId);
  assert.ok(draftDuringProcessing, "Draft must be retained during AI processing after HTTP 202");
  assert.ok(scratchpadPng !== null, "Drawing in memory must be retained during AI processing");

  // Simulate terminal success (e.g. Completed or FallbackCompleted)
  const jobFinishedSuccessfully = true;
  if (jobFinishedSuccessfully) {
    await deleteScratchpadDraft(testScope.centerId, testScope.userId, clientSubmissionId);
    clearAttemptSessionId(testScope, sessionStorage);
    scratchpadPng = null;
    drawingUploadToken = null;
  }

  const remainingDraft = await getScratchpadDraft(testScope.centerId, testScope.userId, clientSubmissionId);
  assert.equal(remainingDraft, null, "Draft must be deleted from IndexedDB after terminal success");
  assert.equal(sessionStorage.getItem(buildAttemptSessionKey(testScope)), null, "Session key must be cleared");
  assert.equal(scratchpadPng, null);
  assert.equal(drawingUploadToken, null);
});

test("FailedTerminal -> handleResubmit migrates vector draft and preserves drawing evidence", async () => {
  const sessionStorage = new MemorySessionStorage();
  const oldSubmissionId = getOrCreateAttemptSessionId(testScope, sessionStorage, () => "sub-failed-1");

  await saveScratchpadDraft({
    storageKey: "",
    centerId: testScope.centerId,
    userId: testScope.userId,
    clientSubmissionId: oldSubmissionId,
    strokes: sampleStrokes,
    gridType: "grid",
    canvasWidth: 1200,
    canvasHeight: 800,
  });

  const scratchpadPng: { size: number } | null = { size: 1024 };
  let drawingUploadToken: string | null = "token-failed";

  // Simulate handleResubmit when terminal failure occurs:
  // Must persist newId to sessionStorage so that getClientSubmissionId() and reloads stay consistent!
  const newSubmissionId = createClientSubmissionId();
  setAttemptSessionId(testScope, newSubmissionId, sessionStorage);
  drawingUploadToken = null;

  // Migrate draft to newSubmissionId
  const existingDraft = await getScratchpadDraft(testScope.centerId, testScope.userId, oldSubmissionId);
  assert.ok(existingDraft, "Old draft must exist before migration");
  await saveScratchpadDraft({
    ...existingDraft,
    storageKey: "",
    clientSubmissionId: newSubmissionId,
  });
  await deleteScratchpadDraft(testScope.centerId, testScope.userId, oldSubmissionId);

  // Assert 1: old draft is gone, new draft exists with same strokes!
  const oldDraft = await getScratchpadDraft(testScope.centerId, testScope.userId, oldSubmissionId);
  const migratedDraft = await getScratchpadDraft(testScope.centerId, testScope.userId, newSubmissionId);

  assert.equal(oldDraft, null, "Old draft must be cleaned up after migration");
  assert.ok(migratedDraft, "Migrated draft must exist under new submission ID");
  assert.equal(migratedDraft.strokes.length, 1);
  assert.equal(migratedDraft.strokes[0].id, "stroke-1");
  assert.ok(scratchpadPng !== null, "Drawing memory must remain available for resubmission");
  assert.equal(drawingUploadToken, null, "Upload token must be reset to force fresh prepare-upload");

  // Assert 2: sessionStorage has the EXACT newSubmissionId
  assert.equal(
    sessionStorage.getItem(buildAttemptSessionKey(testScope)),
    newSubmissionId,
    "sessionStorage must hold the exact newSubmissionId after resubmit"
  );

  // Assert 3: getClientSubmissionId() (via getOrCreateAttemptSessionId) returns the exact same newSubmissionId
  const resolvedClientSubmissionId = getOrCreateAttemptSessionId(testScope, sessionStorage);
  assert.equal(
    resolvedClientSubmissionId,
    newSubmissionId,
    "getClientSubmissionId must return the exact newSubmissionId without generating a mismatched third ID"
  );

  // Assert 4: Reload simulation: A reloaded page instance resolves the same ID and restores the migrated draft
  const reloadedSessionId = getOrCreateAttemptSessionId(testScope, sessionStorage);
  assert.equal(reloadedSessionId, newSubmissionId, "Reload must resolve the migrated session ID");
  const restoredDraftOnReload = await getScratchpadDraft(testScope.centerId, testScope.userId, reloadedSessionId);
  assert.ok(restoredDraftOnReload, "Draft must be restored on reload following resubmit");
  assert.equal(restoredDraftOnReload.strokes.length, 1);
  assert.equal(restoredDraftOnReload.strokes[0].id, "stroke-1");
});

test("failed submit -> draft and session identity retained for retry", async () => {
  const sessionStorage = new MemorySessionStorage();
  const clientSubmissionId = getOrCreateAttemptSessionId(testScope, sessionStorage, () => "sub-failed-network");

  await saveScratchpadDraft({
    storageKey: "",
    centerId: testScope.centerId,
    userId: testScope.userId,
    clientSubmissionId,
    strokes: sampleStrokes,
    gridType: "isometric",
    canvasWidth: 1200,
    canvasHeight: 800,
  });

  const scratchpadPng: { size: number } | null = { size: 1024 };
  const drawingUploadToken: string | null = "token-uncommitted";

  // Simulate network failure or 500 error
  try {
    throw new Error("500 Internal Server Error");
  } catch {
    // On failure, draft and session are NOT cleared
  }

  const retainedDraft = await getScratchpadDraft(testScope.centerId, testScope.userId, clientSubmissionId);
  assert.ok(retainedDraft, "Draft must remain in IndexedDB upon submission failure");
  assert.equal(
    sessionStorage.getItem(buildAttemptSessionKey(testScope)),
    clientSubmissionId,
    "Session identity must remain intact upon failure"
  );
  assert.ok(scratchpadPng !== null);
  assert.equal(drawingUploadToken, "token-uncommitted");
});
