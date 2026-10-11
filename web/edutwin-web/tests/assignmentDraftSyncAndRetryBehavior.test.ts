import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import vm from "node:vm";
import ts from "typescript";
import { isQuestionSubmissionLocked } from "../src/utils/questionReview.ts";
import {
  executeSnapshotUpload,
  pruneMismatchedSnapshotUploads,
  type DraftAnswerLike,
  type ExecuteSnapshotUploadOptions,
  type SnapshotUploadTracker,
} from "../src/utils/assignmentSnapshotUploader.ts";

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (error: Error) => void;
  const promise = new Promise<T>((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
}

function uploaderHarness() {
  const uploads: ReturnType<typeof deferred<{ drawingUploadToken?: string }>>[] = [];
  const stored: { questionId: string; token: string }[] = [];
  const activeTokens: string[] = [];
  const state = {
    answers: { "1": { snapshotDataUrl: "photo-A" } } as Record<string, DraftAnswerLike>,
    activeQuestionId: "1",
    scope: "assignment-A",
  };
  const scope = state.scope;
  const options: ExecuteSnapshotUploadOptions = {
    qId: "1", dataUrl: "photo-A", blob: new Blob(["image"]),
    snapshotUploadSeqRef: { current: 0 },
    snapshotUploadsRef: { current: {} },
    getCurrentDraftAnswers: () => state.answers,
    getActiveQuestionId: () => state.activeQuestionId,
    isCurrentScope: () => state.scope === scope,
    onTokenStored: (questionId, token) => {
      stored.push({ questionId, token });
      state.answers[questionId].drawingUploadToken = token;
    },
    onActiveQuestionTokenUpdated: (token) => activeTokens.push(token),
    prepareUpload: () => {
      const pending = deferred<{ drawingUploadToken?: string }>();
      uploads.push(pending);
      return pending.promise;
    },
  };
  return { options, state, uploads, stored, activeTokens };
}

test("production uploader deduplicates concurrent calls and caches successful uploads", async () => {
  const h = uploaderHarness();
  const first = executeSnapshotUpload(h.options);
  const second = executeSnapshotUpload(h.options);
  assert.equal(first, second);
  await Promise.resolve();
  assert.equal(h.uploads.length, 1);
  h.uploads[0].resolve({ drawingUploadToken: "token-A" });
  assert.deepEqual(await Promise.all([first, second]), ["token-A", "token-A"]);
  assert.equal(await executeSnapshotUpload(h.options), "token-A");
  assert.equal(h.uploads.length, 1);
  assert.deepEqual(h.activeTokens, ["token-A"]);
});

test("production uploader clears rejected promises and makes a new request on retry", async () => {
  const h = uploaderHarness();
  const first = executeSnapshotUpload(h.options);
  await Promise.resolve();
  h.uploads[0].reject(new Error("offline"));
  assert.equal(await first, null);
  assert.equal(h.options.snapshotUploadsRef.current["1"], undefined);
  const retry = executeSnapshotUpload(h.options);
  await Promise.resolve();
  h.uploads[1].resolve({ drawingUploadToken: "recovered" });
  assert.equal(await retry, "recovered");
  assert.equal(h.uploads.length, 2);
});

test("production uploader clears a missing-token response and allows retry", async () => {
  const h = uploaderHarness();
  const first = executeSnapshotUpload(h.options);
  await Promise.resolve();
  h.uploads[0].resolve({});
  assert.equal(await first, null);
  assert.equal(h.options.snapshotUploadsRef.current["1"], undefined);
  const retry = executeSnapshotUpload(h.options);
  await Promise.resolve();
  h.uploads[1].resolve({ drawingUploadToken: "recovered" });
  assert.equal(await retry, "recovered");
});

test("synchronous uploader exceptions cannot leave a failed promise cached", async () => {
  const h = uploaderHarness();
  assert.equal(await executeSnapshotUpload({ ...h.options, prepareUpload: () => { throw new Error("sync failure"); } }), null);
  assert.equal(h.options.snapshotUploadsRef.current["1"], undefined);
  const retry = executeSnapshotUpload(h.options);
  await Promise.resolve();
  h.uploads[0].resolve({ drawingUploadToken: "recovered" });
  assert.equal(await retry, "recovered");
});

test("replacement invalidates only the old upload, never the newer tracker", async () => {
  const h = uploaderHarness();
  const old = executeSnapshotUpload(h.options);
  await Promise.resolve();
  h.state.answers["1"] = { snapshotDataUrl: "photo-B" };
  const next = executeSnapshotUpload({ ...h.options, dataUrl: "photo-B" });
  await Promise.resolve();
  const newTracker = h.options.snapshotUploadsRef.current["1"];
  h.uploads[0].resolve({ drawingUploadToken: "old" });
  assert.equal(await old, null);
  assert.equal(h.options.snapshotUploadsRef.current["1"], newTracker);
  h.uploads[1].resolve({ drawingUploadToken: "new" });
  assert.equal(await next, "new");
  assert.deepEqual(h.stored, [{ questionId: "1", token: "new" }]);
});

test("late failure of the old image does not delete a newer image's tracker", async () => {
  const h = uploaderHarness();
  const old = executeSnapshotUpload(h.options);
  await Promise.resolve();
  h.state.answers["1"] = { snapshotDataUrl: "photo-B" };
  const next = executeSnapshotUpload({ ...h.options, dataUrl: "photo-B" });
  await Promise.resolve();
  const tracker = h.options.snapshotUploadsRef.current["1"];
  h.uploads[0].reject(new Error("old upload failed"));
  assert.equal(await old, null);
  assert.equal(h.options.snapshotUploadsRef.current["1"], tracker);
  h.uploads[1].resolve({ drawingUploadToken: "new" });
  assert.equal(await next, "new");
});

test("draft mismatch clears its own null promise and reattaching the same image can retry", async () => {
  const h = uploaderHarness();
  const old = executeSnapshotUpload(h.options);
  await Promise.resolve();
  h.state.answers["1"] = { drawingUploadToken: "server-token" };
  h.uploads[0].resolve({ drawingUploadToken: "discard" });
  assert.equal(await old, null);
  assert.equal(h.options.snapshotUploadsRef.current["1"], undefined);
  h.state.answers["1"] = { snapshotDataUrl: "photo-A" };
  const retry = executeSnapshotUpload(h.options);
  await Promise.resolve();
  h.uploads[1].resolve({ drawingUploadToken: "retry" });
  assert.equal(await retry, "retry");
});

test("sync prunes an in-flight image; its late result cannot delete the reattached image", async () => {
  const h = uploaderHarness();
  const old = executeSnapshotUpload(h.options);
  await Promise.resolve();
  h.state.answers = { "1": { drawingUploadToken: "server-token" } };
  pruneMismatchedSnapshotUploads(h.options.snapshotUploadsRef, h.state.answers);
  h.state.answers["1"] = { snapshotDataUrl: "photo-A" };
  const retry = executeSnapshotUpload(h.options);
  await Promise.resolve();
  const tracker = h.options.snapshotUploadsRef.current["1"];
  h.uploads[0].resolve({ drawingUploadToken: "old" });
  assert.equal(await old, null);
  assert.equal(h.options.snapshotUploadsRef.current["1"], tracker);
  h.uploads[1].resolve({ drawingUploadToken: "reattached" });
  assert.equal(await retry, "reattached");
  assert.equal(h.uploads.length, 2);
});

test("deleting a question snapshot discards the late token", async () => {
  const h = uploaderHarness();
  const pending = executeSnapshotUpload(h.options);
  await Promise.resolve();
  delete h.state.answers["1"];
  h.uploads[0].resolve({ drawingUploadToken: "removed" });
  assert.equal(await pending, null);
  assert.equal(h.options.snapshotUploadsRef.current["1"], undefined);
  assert.deepEqual(h.stored, []);
  assert.deepEqual(h.activeTokens, []);
});

test("question switching stores the token on its own question, not the active input", async () => {
  const h = uploaderHarness();
  const pending = executeSnapshotUpload(h.options);
  await Promise.resolve();
  h.state.activeQuestionId = "2";
  h.uploads[0].resolve({ drawingUploadToken: "question-1" });
  assert.equal(await pending, "question-1");
  assert.deepEqual(h.stored, [{ questionId: "1", token: "question-1" }]);
  assert.deepEqual(h.activeTokens, []);
});

test("scope changes reject old tokens even when question ID and image are identical", async () => {
  const h = uploaderHarness();
  const old = executeSnapshotUpload(h.options);
  await Promise.resolve();
  h.state.scope = "assignment-B";
  h.uploads[0].resolve({ drawingUploadToken: "wrong-scope" });
  assert.equal(await old, null);
  assert.equal(h.options.snapshotUploadsRef.current["1"], undefined);
  assert.deepEqual(h.stored, []);
  assert.deepEqual(h.activeTokens, []);
});

test("scope changes during image conversion prevent sending the old image", async () => {
  const h = uploaderHarness();
  const conversion = deferred<Blob>();
  const pending = executeSnapshotUpload({
    ...h.options, blob: null, convertDataUrlToBlob: () => conversion.promise,
  });
  await Promise.resolve();
  h.state.scope = "assignment-B";
  conversion.resolve(new Blob(["image"]));
  assert.equal(await pending, null);
  assert.equal(h.uploads.length, 0);
  assert.equal(h.options.snapshotUploadsRef.current["1"], undefined);
});

// Run the actual page callbacks with hook/state adapters. No copy of the upload
// or sync algorithm is used; networking is the only externally controlled work.
const pageSource = fs.readFileSync(new URL("../src/pages/LearningPlayerPage.tsx", import.meta.url), "utf8");
function sliceCallback(start: string, end: string) {
  const from = pageSource.indexOf(start);
  const to = pageSource.indexOf(end, from);
  assert.ok(from >= 0 && to > from, `Production callback markers missing: ${start}`);
  return pageSource.slice(from, to);
}
const pageCallbacks = ts.transpileModule(
  sliceCallback("  const uploadQuestionSnapshot =", "  const performSaveDraft =") +
  sliceCallback("  const handleSyncWithServer =", "  const handleForceOverwriteLocal =") +
  sliceCallback("  const persistCurrentAnswer =", "  // Switch to another question") +
  "\nglobalThis.callbacks = { uploadQuestionSnapshot, handleSyncWithServer, persistCurrentAnswer };",
  { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.None } }
).outputText;

function pageHarness() {
  const uploads: ReturnType<typeof deferred<{ drawingUploadToken: string }>>[] = [];
  const writes: string[] = [];
  const c: Record<string, any> = {
    useCallback: (callback: unknown) => callback,
    executeSnapshotUpload, pruneMismatchedSnapshotUploads, isQuestionSubmissionLocked,
    currentUser: { userId: "student", centerId: "center" },
    assignmentDraftScope: { assignmentId: "A" }, snapshotScopeKey: "A", snapshotScopeRef: { current: "A" },
    question: { questionId: "1" }, assignmentQuestion: { questionId: "1", questionType: "ShortAnswer" },
    finalAnswer: "LOCAL", answerDisplayLatex: "LOCAL", reasoningText: "LOCAL REASON",
    confidence: 50, timeSpentSeconds: 12, answerChanges: 2,
    attachedSnapshotDataUrl: "photo-A", attachedSnapshotBlob: new Blob(["image"]), attachedSnapshotTime: "10:00",
    attachedSnapshotDataUrlRef: { current: "photo-A" }, effectiveQuestionIdRef: { current: "1" },
    drawingUploadToken: null, activeQuestionIdRef: { current: "1" }, answerChangesRef: { current: 2 },
    snapshotUploadSeqRef: { current: 0 }, snapshotUploadsRef: { current: {} as Record<string, SnapshotUploadTracker> },
    assignmentAnswers: { "1": { finalAnswer: "LOCAL", snapshotDataUrl: "photo-A" } }, assignmentAnswersRef: { current: {} },
    assignmentDraftLoadVersion: 0, saveVersionRef: { current: 2 }, lastSavedVersionRef: { current: 2 },
    latestQueuedVersionRef: { current: 2 }, isDraftConflictRef: { current: true },
    draftConflict: { serverVersion: 8, message: "Conflict" },
    setAssignmentAnswers(update: any) {
      c.assignmentAnswers = typeof update === "function" ? update(c.assignmentAnswers) : update;
      c.assignmentAnswersRef.current = c.assignmentAnswers;
    },
    writeAssignmentDraft(_scope: unknown, value: string) { writes.push(value); },
    setLastDraftSavedTime() {},
    fetch: async () => ({ blob: async () => new Blob(["image"]) }),
    prepareAttemptAttachmentUpload: () => { const pending = deferred<{ drawingUploadToken: string }>(); uploads.push(pending); return pending.promise; },
    refetchAssignment: async () => ({ isSuccess: true, data: { data: { draftVersion: 8, draftAnswers: [
      { questionId: 1, finalAnswer: "SERVER", answerDisplayLatex: "SERVER", reasoningText: "SERVER REASON",
        confidence: 95, timeSpentSeconds: 60, answerChanges: 4, drawingUploadToken: "server-token" },
    ] } } }),
    console: { warn() {}, error() {} }, Date, Math, JSON, Object, Blob,
  };
  const setters = {
    setFinalAnswer: "finalAnswer", setAnswerDisplayLatex: "answerDisplayLatex", setReasoningText: "reasoningText",
    setConfidence: "confidence", setTimeSpentSeconds: "timeSpentSeconds", setAnswerChanges: "answerChanges",
    setAttachedSnapshotDataUrl: "attachedSnapshotDataUrl", setAttachedSnapshotBlob: "attachedSnapshotBlob",
    setAttachedSnapshotTime: "attachedSnapshotTime", setDrawingUploadToken: "drawingUploadToken",
    setDraftConflict: "draftConflict", setDraftSaveStatus: "draftSaveStatus", setAssignmentDraftLoadVersion: "assignmentDraftLoadVersion",
  };
  for (const [setter, key] of Object.entries(setters)) {
    c[setter] = (value: any) => {
      c[key] = typeof value === "function" ? value(c[key]) : value;
      if (key === "attachedSnapshotDataUrl") c.attachedSnapshotDataUrlRef.current = c[key];
    };
  }
  c.assignmentAnswersRef.current = c.assignmentAnswers;
  vm.createContext(c);
  vm.runInContext(pageCallbacks, c);
  return { c, uploads, writes };
}

test("page adapter uses the shared production uploader, not an independent implementation", () => {
  const body = sliceCallback("  const uploadQuestionSnapshot =", "  const performSaveDraft =");
  assert.ok(body.includes("return executeSnapshotUpload({"));
  assert.ok(!body.includes("await prepareAttemptAttachmentUpload("));
});

test("draft save failure exposes manual retry and retries when the browser comes online", () => {
  assert.match(pageSource, /window\.addEventListener\("online", retryFailedDraftSave\)/);
  assert.match(pageSource, /window\.removeEventListener\("online", retryFailedDraftSave\)/);
  assert.match(pageSource, />\s*Kết nối lại\s*<\/button>/);
  assert.match(pageSource, /<span>Lỗi mạng<\/span>/);
  assert.match(pageSource, /Math\.max\(saveVersionRef\.current, lastSavedVersionRef\.current \+ 1\)/);
});

test("actual sync callback preserves local draft and conflict on cached refetch error", async () => {
  const { c, writes } = pageHarness();
  c.refetchAssignment = async () => ({ isSuccess: false, isError: true, data: { data: { draftVersion: 1, draftAnswers: [] } } });
  await c.callbacks.handleSyncWithServer();
  assert.equal(c.assignmentAnswers["1"].finalAnswer, "LOCAL");
  assert.equal(c.finalAnswer, "LOCAL");
  assert.equal(c.isDraftConflictRef.current, true);
  assert.equal(c.draftConflict.serverVersion, 8);
  assert.equal(c.saveVersionRef.current, 2);
  assert.equal(c.draftSaveStatus, "error");
  assert.equal(writes.length, 0);
});

test("actual sync callback rejects missing payload without wiping answers", async () => {
  const { c, writes } = pageHarness();
  c.refetchAssignment = async () => ({ isSuccess: true });
  await c.callbacks.handleSyncWithServer();
  assert.equal(c.assignmentAnswers["1"].finalAnswer, "LOCAL");
  assert.equal(c.isDraftConflictRef.current, true);
  assert.equal(writes.length, 0);
});

test("actual sync updates active inputs and a subsequent persist keeps server answers", async () => {
  const { c } = pageHarness();
  await c.callbacks.handleSyncWithServer();
  c.callbacks.persistCurrentAnswer();
  assert.equal(c.assignmentAnswers["1"].finalAnswer, "SERVER");
  assert.equal(c.assignmentAnswers["1"].reasoningText, "SERVER REASON");
  assert.equal(c.confidence, 95);
  assert.equal(c.timeSpentSeconds, 60);
  assert.equal(c.answerChangesRef.current, 4);
  assert.equal(c.drawingUploadToken, "server-token");
  assert.equal(c.attachedSnapshotDataUrl, null);
  assert.equal(c.attachedSnapshotBlob, null);
  assert.equal(c.saveVersionRef.current, 8);
  assert.equal(c.isDraftConflictRef.current, false);
});

test("actual sync clears the active inputs when the server draft is empty", async () => {
  const { c } = pageHarness();
  c.refetchAssignment = async () => ({ isSuccess: true, data: { data: { draftVersion: 8, draftAnswers: [] } } });
  await c.callbacks.handleSyncWithServer();
  assert.equal(c.finalAnswer, "");
  assert.equal(c.reasoningText, "");
  assert.equal(c.drawingUploadToken, null);
  assert.equal(c.answerChangesRef.current, 0);
  assert.equal(c.attachedSnapshotDataUrl, null);
});

test("actual page sync during upload permits retrying the same image after reattachment", async () => {
  const { c, uploads } = pageHarness();
  const old = c.callbacks.uploadQuestionSnapshot("1", "photo-A", new Blob(["image"]));
  await Promise.resolve();
  await c.callbacks.handleSyncWithServer();
  uploads[0].resolve({ drawingUploadToken: "discard-old" });
  assert.equal(await old, null);
  assert.equal(c.snapshotUploadsRef.current["1"], undefined);
  c.assignmentAnswers["1"] = { ...c.assignmentAnswers["1"], snapshotDataUrl: "photo-A", drawingUploadToken: null };
  c.attachedSnapshotDataUrl = "photo-A";
  c.attachedSnapshotDataUrlRef.current = "photo-A";
  const retry = c.callbacks.uploadQuestionSnapshot("1", "photo-A", new Blob(["image"]));
  await Promise.resolve();
  uploads[1].resolve({ drawingUploadToken: "fresh-photo" });
  assert.equal(await retry, "fresh-photo");
  assert.equal(uploads.length, 2);
  assert.equal(c.drawingUploadToken, "fresh-photo");
  assert.equal(c.assignmentAnswers["1"].drawingUploadToken, "fresh-photo");
});

test("adaptive page uploads remain valid without an assignment draft", async () => {
  const { c, uploads } = pageHarness();
  c.assignmentDraftScope = null;
  c.assignmentAnswers = {};
  c.assignmentAnswersRef.current = {};
  const pending = c.callbacks.uploadQuestionSnapshot("1", "photo-A", new Blob(["image"]));
  await Promise.resolve();
  uploads[0].resolve({ drawingUploadToken: "adaptive-photo" });
  assert.equal(await pending, "adaptive-photo");
  assert.equal(c.drawingUploadToken, "adaptive-photo");
  assert.equal(Object.keys(c.assignmentAnswers).length, 0);
});
