import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import vm from "node:vm";
import ts from "typescript";

const source = fs.readFileSync(new URL("../src/pages/LearningPlayerPage.tsx", import.meta.url), "utf8");
const start = source.indexOf("  const handleNextPracticeQuestion =");
const end = source.indexOf("\n  };", start) + "\n  };".length;
assert.ok(start >= 0 && end > start);
const production = ts.transpileModule(source.slice(start, end) + "\nglobalThis.nextQuestion = handleNextPracticeQuestion;",
  { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.None } }).outputText;

function harness(writable: boolean, session: boolean) {
  const state: Record<string, unknown> = { FeedbackData: { attemptId: "old" }, FinalAnswer: "old", AnswerDisplayLatex: "old latex",
    ReasoningText: "old reasoning", Confidence: 20, TimeSpentSeconds: 120, AnswerChanges: 4,
    AttachedSnapshotDataUrl: "old image", AttachedSnapshotBlob: new Blob(["old"]), AttachedSnapshotTime: "old time",
    ShowFullSnapshotModal: true, DrawingUploadToken: "consumed-token" };
  let questions = 0, sessions = 0;
  const context: Record<string, any> = { learningAccess: { writable }, pathSessionId: session ? "session" : null,
    sessionQuery: { refetch: () => { sessions++; } }, refetchAdaptiveQuestion: () => { questions++; },
    answerChangesRef: { current: 4 }, snapshotUploadsRef: { current: { old: { token: "old" } } } };
  context.practiceSubmissionRef={current:{clientSubmissionId:"previous-attempt"}};
  for (const name of Object.keys(state)) context[`set${name}`] = (value: unknown) => { state[name] = value; };
  vm.createContext(context); vm.runInContext(production, context);
  return { state, context, counts: () => ({ questions, sessions }) };
}

for (const session of [false, true]) test(`next practice question isolates behavioral, math and attachment inputs (session=${session})`, () => {
  const h = harness(true, session); h.context.nextQuestion();
  for (const name of ["FinalAnswer", "AnswerDisplayLatex", "ReasoningText"]) assert.equal(h.state[name], "");
  for (const name of ["FeedbackData", "AttachedSnapshotDataUrl", "AttachedSnapshotBlob", "AttachedSnapshotTime", "DrawingUploadToken"]) assert.equal(h.state[name], null);
  assert.equal(h.state.AnswerChanges, 0); assert.equal(h.context.answerChangesRef.current, 0);
  assert.equal(h.state.Confidence, 80); assert.equal(h.state.TimeSpentSeconds, 0); assert.equal(h.state.ShowFullSnapshotModal, false);
  assert.equal(Object.keys(h.context.snapshotUploadsRef.current).length, 0);
  assert.equal(h.context.practiceSubmissionRef.current,null);
  assert.deepEqual(h.counts(), { questions: 1, sessions: session ? 1 : 0 });
});
test("read-only practice does not clear inputs or fetch a new question", () => {
  const h = harness(false, true); h.context.nextQuestion();
  assert.equal(h.state.FinalAnswer, "old"); assert.equal(h.state.DrawingUploadToken, "consumed-token");
  assert.equal(h.context.answerChangesRef.current, 4); assert.deepEqual(h.counts(), { questions: 0, sessions: 0 });
});
