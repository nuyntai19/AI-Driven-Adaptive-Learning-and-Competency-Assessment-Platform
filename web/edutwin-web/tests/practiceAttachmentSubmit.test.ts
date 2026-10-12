import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import vm from "node:vm";
import ts from "typescript";

const source = fs.readFileSync(new URL("../src/pages/LearningPlayerPage.tsx", import.meta.url), "utf8");
const start = source.indexOf("  const handleFinalSubmit =");
const end = source.indexOf("  const handleRetrySubmission =", start);
assert.ok(start >= 0 && end > start);
const production = ts.transpileModule(source.slice(start, end) + "\nglobalThis.submit = handleFinalSubmit;",
  { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.None } }).outputText;
const retryEnd=source.indexOf("  // Auto-submit when",end);
const retryProduction=ts.transpileModule(source.slice(end,retryEnd)+"\nglobalThis.retry=handleRetrySubmission;",
  {compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.None}}).outputText;

function harness(image: boolean, upload: () => Promise<string | null>, token: string | null = null) {
  const submitted: Record<string, unknown>[] = [];
  const c: Record<string, any> = {
    classReadOnly: false, assignmentId: null, learningAccess: { writable: true }, draftHydrated: true,
    question: { questionId: "10000", reasoningRequired: false }, submissionInFlightRef: { current: false },
    practiceSubmissionRef: { current:null },clientSubmissionIdRef:{current:"stable-submission-id"},
    attemptSessionScope:{questionId:"10000"}, createClientSubmissionId:()=>"new-submission-id",
    isAssignmentSubmitted: false, feedbackData: null, isSubmitting: false, pollingJobId: null, isAssignmentExpired: false,
    canSubmitLearningWork: () => true, assignmentQuestions: [], isQuestionSubmissionLocked: () => false,
    persistCurrentAnswer: () => {}, drawingUploadToken: token, attachedSnapshotDataUrl: image ? "image" : null,
    uploadScratchpadAttachmentIfAny: upload, finalAnswer: "2", reasoningText: "reason", answerDisplayLatex: "2",
    timeSpentSeconds: 12, confidence: 80, answerChanges: 1, isHistory: false, pathSessionId: null,
    getClientSubmissionId: () => c.clientSubmissionIdRef.current, queryClient: { invalidateQueries: () => {} },
    submitAttempt: async (payload: Record<string, unknown>) => { submitted.push(payload); return { data: { attemptId: "saved" } }; },
    getAttemptFeedback: async () => ({ attemptId: "saved" }), storeAttemptFeedback: () => {},
  };
  c.setAttemptSessionId=(_scope:unknown,id:string)=>{c.clientSubmissionIdRef.current=id;};
  for (const name of ["ShowBatchConfirmModal", "ActiveSideTool", "SubmissionSaveError", "CanRetrySubmission", "IsSubmitting", "AiBanner", "PollingStatus"])
    c[`set${name}`] = (value: unknown) => { c[name] = value; if (name === "IsSubmitting") c.isSubmitting = value; };
  vm.createContext(c); vm.runInContext(production, c);vm.runInContext(retryProduction,c);
  return { c, submitted };
}

test("practice with an image does not submit an attempt when uploader returns no token", async () => {
  const h = harness(true, async () => null); await h.c.submit();
  assert.equal(h.submitted.length, 0);
  assert.match(h.c.SubmissionSaveError, /ảnh|vẽ nháp/);
  assert.equal(h.c.CanRetrySubmission, true); assert.equal(h.c.isSubmitting, false);
  assert.equal(h.c.submissionInFlightRef.current, false);
  assert.equal(h.c.finalAnswer, "2"); assert.equal(h.c.attachedSnapshotDataUrl, "image");
});
test("practice retries attachment upload without losing answer or creating a partial attempt", async () => {
  let calls = 0;
  const h = harness(true, async () => ++calls === 1 ? null : "uploaded-image");
  await h.c.submit(); assert.equal(h.submitted.length, 0);
  await h.c.submit(); assert.equal(h.submitted.length, 1);
  assert.equal(h.submitted[0].drawingUploadToken, "uploaded-image");
  assert.equal(h.submitted[0].clientSubmissionId, "stable-submission-id");
});
test("practice uploader rejection releases submission lock and offers retry", async () => {
  const h = harness(true, async () => { throw new Error("offline"); }); await h.c.submit();
  assert.equal(h.submitted.length, 0); assert.equal(h.c.CanRetrySubmission, true);
  assert.equal(h.c.submissionInFlightRef.current, false); assert.equal(h.c.isSubmitting, false);
});
test("practice without an attachment submits normally", async () => {
  const h = harness(false, async () => { throw new Error("must not upload"); }); await h.c.submit();
  assert.equal(h.submitted.length, 1); assert.equal(h.submitted[0].drawingUploadToken, undefined);
});
test("practice reuses a prepared attachment token without uploading again", async () => {
  const h = harness(true, async () => { throw new Error("must reuse"); }, "prepared-token"); await h.c.submit();
  assert.equal(h.submitted.length, 1); assert.equal(h.submitted[0].drawingUploadToken, "prepared-token");
});
test("actual retry after a lost response replays identical payload and identity despite timer/input drift",async()=>{
  const h=harness(false,async()=>null);
  h.c.submitAttempt=async(payload:Record<string,unknown>)=>{
    h.submitted.push({...payload});
    if(h.submitted.length===1)throw new Error("response lost after server committed");
    return {data:{attemptId:"same-saved-attempt"}};
  };
  await h.c.submit();assert.equal(h.submitted.length,1);
  h.c.timeSpentSeconds=999;h.c.answerChanges=5;h.c.finalAnswer="changed after timeout";
  h.c.question.reasoningRequired=true;h.c.reasoningText="";
  await h.c.retry();
  assert.equal(h.submitted.length,2);assert.deepEqual(h.submitted[1],h.submitted[0]);
  assert.equal(h.c.clientSubmissionIdRef.current,"stable-submission-id");
});
test("ambiguous attachment submit retries the frozen token without uploading a replacement",async()=>{
  const h=harness(true,async()=>{throw new Error("must not upload consumed token again");},"prepared-token");
  h.c.submitAttempt=async(payload:Record<string,unknown>)=>{
    h.submitted.push({...payload});if(h.submitted.length===1)throw new Error("response lost");
    return {data:{attemptId:"same-attempt"}};
  };
  await h.c.submit();h.c.drawingUploadToken=null;h.c.attachedSnapshotDataUrl="edited-image";
  await h.c.retry();assert.equal(h.submitted.length,2);assert.deepEqual(h.submitted[1],h.submitted[0]);
  assert.equal(h.submitted[1].drawingUploadToken,"prepared-token");
});
test("explicit validation rejection allows corrected inputs while retaining the unconsumed identity",async()=>{
  const h=harness(false,async()=>null);
  h.c.submitAttempt=async(payload:Record<string,unknown>)=>{
    h.submitted.push({...payload});if(h.submitted.length===1)throw {response:{status:400},message:"validation"};
    return {data:{attemptId:"saved-correction"}};
  };
  await h.c.submit();assert.equal(h.c.practiceSubmissionRef.current,null);
  h.c.finalAnswer="corrected answer";await h.c.retry();
  assert.equal(h.submitted[1].finalAnswer,"corrected answer");
  assert.equal(h.submitted[1].clientSubmissionId,h.submitted[0].clientSubmissionId);
});
