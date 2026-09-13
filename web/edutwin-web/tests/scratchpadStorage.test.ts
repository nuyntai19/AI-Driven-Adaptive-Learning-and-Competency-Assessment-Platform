import assert from "node:assert/strict";
import test, { beforeEach } from "node:test";
import {
  buildScratchpadKey,
  saveScratchpadDraft,
  getScratchpadDraft,
  deleteScratchpadDraft,
  clearUserScratchpadDrafts,
  cleanupExpiredScratchpadDrafts,
  __resetScratchpadStorageForTesting,
  DEFAULT_TTL_MS,
} from "../src/utils/scratchpadStorage.ts";
import type { ScratchpadDraft } from "../src/types/scratchpad.ts";

beforeEach(() => {
  __resetScratchpadStorageForTesting();
});

test("buildScratchpadKey formats key correctly with centerId, userId, and clientSubmissionId", () => {
  const key = buildScratchpadKey("center-alpha", "user-123", "sub-xyz-789");
  assert.equal(key, "draft:center-alpha:user-123:sub-xyz-789");

  // Trims spaces
  const trimmedKey = buildScratchpadKey(" center-alpha ", " user-123 ", " sub-xyz-789 ");
  assert.equal(trimmedKey, "draft:center-alpha:user-123:sub-xyz-789");

  assert.throws(() => buildScratchpadKey("", "user-123", "submission"), /centerId is required/);
  assert.throws(() => buildScratchpadKey("center", "  ", "submission"), /userId is required/);
  assert.throws(() => buildScratchpadKey("center", "user", ""), /clientSubmissionId is required/);
});

test("saveScratchpadDraft and getScratchpadDraft persist and retrieve scoped drafts", async () => {
  const sampleDraft: ScratchpadDraft = {
    storageKey: "",
    centerId: "center-01",
    userId: "student-42",
    clientSubmissionId: "submission-abc",
    strokes: [
      {
        id: "stroke-1",
        tool: "pen",
        color: "#000000",
        lineWidth: 2,
        points: [{ x: 10, y: 10 }, { x: 20, y: 25 }],
      },
      {
        id: "stroke-2",
        tool: "line",
        color: "#2563eb",
        lineWidth: 3,
        points: [],
        start: { x: 0, y: 0 },
        end: { x: 100, y: 100 },
      },
    ],
    gridType: "o_ly",
    canvasWidth: 800,
    canvasHeight: 600,
    updatedAt: 0,
    expiresAt: 0,
  };

  await saveScratchpadDraft(sampleDraft);

  const retrieved = await getScratchpadDraft("center-01", "student-42", "submission-abc");
  assert.ok(retrieved !== null, "Draft should be found");
  assert.equal(retrieved.storageKey, "draft:center-01:student-42:submission-abc");
  assert.equal(retrieved.strokes.length, 2);
  assert.equal(retrieved.strokes[0].tool, "pen");
  assert.equal(retrieved.strokes[1].tool, "line");
  assert.equal(retrieved.gridType, "o_ly");
  assert.ok(retrieved.updatedAt > 0);
  assert.ok(retrieved.expiresAt >= retrieved.updatedAt + DEFAULT_TTL_MS - 1000);
});

test("getScratchpadDraft strictly scopes by centerId, userId, and clientSubmissionId", async () => {
  const draftA: ScratchpadDraft = {
    storageKey: "",
    centerId: "center-01",
    userId: "student-42",
    clientSubmissionId: "sub-1",
    strokes: [],
    gridType: "none",
    canvasWidth: 600,
    canvasHeight: 400,
    updatedAt: Date.now(),
    expiresAt: Date.now() + 60000,
  };

  await saveScratchpadDraft(draftA);

  // Cross-tenant access must return null
  const wrongCenter = await getScratchpadDraft("center-02", "student-42", "sub-1");
  assert.equal(wrongCenter, null);

  // Cross-user access must return null
  const wrongUser = await getScratchpadDraft("center-01", "student-99", "sub-1");
  assert.equal(wrongUser, null);

  // Different attempt submission ID must return null
  const wrongSubmission = await getScratchpadDraft("center-01", "student-42", "sub-2");
  assert.equal(wrongSubmission, null);

  // Correct tuple returns draft
  const correct = await getScratchpadDraft("center-01", "student-42", "sub-1");
  assert.ok(correct !== null);
});

test("deleteScratchpadDraft removes specific draft from storage", async () => {
  const draft: ScratchpadDraft = {
    storageKey: "",
    centerId: "center-01",
    userId: "student-42",
    clientSubmissionId: "sub-del",
    strokes: [],
    gridType: "math_grid",
    canvasWidth: 500,
    canvasHeight: 500,
    updatedAt: Date.now(),
    expiresAt: Date.now() + 60000,
  };

  await saveScratchpadDraft(draft);
  const foundBefore = await getScratchpadDraft("center-01", "student-42", "sub-del");
  assert.ok(foundBefore !== null);

  await deleteScratchpadDraft("center-01", "student-42", "sub-del");
  const foundAfter = await getScratchpadDraft("center-01", "student-42", "sub-del");
  assert.equal(foundAfter, null);
});

test("getScratchpadDraft auto-purges expired drafts based on TTL", async () => {
  const expiredDraft: ScratchpadDraft = {
    storageKey: "",
    centerId: "center-01",
    userId: "student-42",
    clientSubmissionId: "sub-expired",
    strokes: [],
    gridType: "none",
    canvasWidth: 500,
    canvasHeight: 500,
    updatedAt: Date.now() - 100000,
    expiresAt: Date.now() - 1000, // Expired 1 second ago
  };

  await saveScratchpadDraft(expiredDraft);

  const result = await getScratchpadDraft("center-01", "student-42", "sub-expired");
  assert.equal(result, null, "Expired draft should return null");
});

test("cleanupExpiredScratchpadDrafts purges all stale drafts and returns count", async () => {
  const now = Date.now();
  const draftValid: ScratchpadDraft = {
    storageKey: "",
    centerId: "center-01",
    userId: "student-1",
    clientSubmissionId: "sub-valid",
    strokes: [],
    gridType: "none",
    canvasWidth: 500,
    canvasHeight: 500,
    updatedAt: now,
    expiresAt: now + 3600000,
  };

  const draftExpired1: ScratchpadDraft = {
    storageKey: "",
    centerId: "center-01",
    userId: "student-2",
    clientSubmissionId: "sub-exp-1",
    strokes: [],
    gridType: "none",
    canvasWidth: 500,
    canvasHeight: 500,
    updatedAt: now - 7200000,
    expiresAt: now - 3600000,
  };

  const draftExpired2: ScratchpadDraft = {
    storageKey: "",
    centerId: "center-02",
    userId: "student-3",
    clientSubmissionId: "sub-exp-2",
    strokes: [],
    gridType: "none",
    canvasWidth: 500,
    canvasHeight: 500,
    updatedAt: now - 7200000,
    expiresAt: now - 1000,
  };

  await saveScratchpadDraft(draftValid);
  await saveScratchpadDraft(draftExpired1);
  await saveScratchpadDraft(draftExpired2);

  const purgedCount = await cleanupExpiredScratchpadDrafts();
  assert.equal(purgedCount, 2, "Should purge exactly 2 expired drafts");

  const validResult = await getScratchpadDraft("center-01", "student-1", "sub-valid");
  assert.ok(validResult !== null, "Valid draft should remain");
});

test("clearUserScratchpadDrafts purges only the target user's drafts across attempts", async () => {
  const now = Date.now();
  // Target user drafts
  await saveScratchpadDraft({
    storageKey: "",
    centerId: "center-alpha",
    userId: "user-target",
    clientSubmissionId: "sub-1",
    strokes: [],
    gridType: "none",
    canvasWidth: 500,
    canvasHeight: 500,
    updatedAt: now,
    expiresAt: now + 60000,
  });
  await saveScratchpadDraft({
    storageKey: "",
    centerId: "center-alpha",
    userId: "user-target",
    clientSubmissionId: "sub-2",
    strokes: [],
    gridType: "o_ly",
    canvasWidth: 500,
    canvasHeight: 500,
    updatedAt: now,
    expiresAt: now + 60000,
  });

  // Another user in the same center
  await saveScratchpadDraft({
    storageKey: "",
    centerId: "center-alpha",
    userId: "user-other",
    clientSubmissionId: "sub-3",
    strokes: [],
    gridType: "math_grid",
    canvasWidth: 500,
    canvasHeight: 500,
    updatedAt: now,
    expiresAt: now + 60000,
  });

  // User in a different center
  await saveScratchpadDraft({
    storageKey: "",
    centerId: "center-beta",
    userId: "user-target",
    clientSubmissionId: "sub-4",
    strokes: [],
    gridType: "none",
    canvasWidth: 500,
    canvasHeight: 500,
    updatedAt: now,
    expiresAt: now + 60000,
  });

  const cleared = await clearUserScratchpadDrafts("center-alpha", "user-target");
  assert.equal(cleared, 2, "Should delete exactly 2 drafts for target user in center-alpha");

  // Verify target user's drafts are gone
  assert.equal(await getScratchpadDraft("center-alpha", "user-target", "sub-1"), null);
  assert.equal(await getScratchpadDraft("center-alpha", "user-target", "sub-2"), null);

  // Verify other user in same center remains
  assert.ok(await getScratchpadDraft("center-alpha", "user-other", "sub-3") !== null);

  // Verify same user in different center remains
  assert.ok(await getScratchpadDraft("center-beta", "user-target", "sub-4") !== null);
});

test("saveScratchpadDraft rejects a caller-supplied key that does not match the scoped identity", async () => {
  const draft: ScratchpadDraft = {
    storageKey: "draft:center-a:student-a:other-submission",
    centerId: "center-a",
    userId: "student-a",
    clientSubmissionId: "submission-a",
    strokes: [],
    gridType: "none",
    canvasWidth: 500,
    canvasHeight: 500,
    updatedAt: Date.now(),
    expiresAt: Date.now() + 60_000,
  };

  await assert.rejects(saveScratchpadDraft(draft), /does not match its scoped identity/);
});
