import test from "node:test";
import assert from "node:assert/strict";
import { usesMathInput, answerWorkPlaceholder } from "../src/utils/answerPlaceholders.ts";
import { deriveFinalAnswer } from "../src/utils/finalAnswer.ts";
import { dateRangeError } from "../src/utils/dateRange.ts";
import { formatDateInput } from "../src/utils/dateInput.ts";
import { practiceDraftKey, readPracticeDraft, savePracticeDraft } from "../src/utils/practiceDraft.ts";

test("ENG input and answer extraction depend on catalog code after any subject rename", () => {
  for (const renamedDisplayName of ["Tiếng Anh", "Academic Writing", "Toán bằng tiếng Anh"]) {
    const subject = { subjectCode: "ENG", subjectName: renamedDisplayName };
    assert.equal(usesMathInput(subject.subjectCode), false);
    assert.doesNotMatch(answerWorkPlaceholder(usesMathInput(subject.subjectCode)), /Ctrl\+M/);
    const work = "First sentence.\nSecond sentence.";
    assert.equal(deriveFinalAnswer(work, "ShortAnswer", "TextExact", !usesMathInput(subject.subjectCode)), "First sentence. Second sentence.");
  }
  assert.equal(usesMathInput(" eng "), false);
  assert.equal(usesMathInput("MATH"), true);
  assert.equal(deriveFinalAnswer("2x = 10\nx = 5", "ShortAnswer", "NumericRational", !usesMathInput("MATH")), "5");
});

test("reversed date range is rejected while one-day and open-ended filters remain valid", () => {
  assert.ok(dateRangeError("2026-11-21", "2026-11-20"));
  assert.equal(dateRangeError("2026-11-20", "2026-11-20"), null);
  assert.equal(dateRangeError("2026-11-20", ""), null);
  assert.equal(formatDateInput("2027-01-19T16:59:59Z"), "19/01/2027");
});

test("practice draft survives re-entry and is isolated by tenant, student, session and question", t => {
  const values = new Map<string, string>();
  const old = Object.getOwnPropertyDescriptor(globalThis, "sessionStorage");
  Object.defineProperty(globalThis, "sessionStorage", { configurable: true, value: {
    getItem: (key: string) => values.get(key) ?? null,
    setItem: (key: string, value: string) => values.set(key, value),
  } });
  t.after(() => { if (old) Object.defineProperty(globalThis, "sessionStorage", old); else Reflect.deleteProperty(globalThis, "sessionStorage"); });
  const key = practiceDraftKey("center-a", "student-a", "session-a", "question-a");
  const draft = { selectedOption: "B", reasoningText: "First line\nSecond line" };
  assert.equal(savePracticeDraft(key, draft), true);
  assert.deepEqual(readPracticeDraft(key), draft);
  for (const scope of [["center-b", "student-a", "session-a", "question-a"], ["center-a", "student-b", "session-a", "question-a"], ["center-a", "student-a", "session-b", "question-a"], ["center-a", "student-a", "session-a", "question-b"]]) {
    assert.deepEqual(readPracticeDraft(practiceDraftKey(scope[0], scope[1], scope[2], scope[3])), { selectedOption: "", reasoningText: "" });
  }
  values.set(key, "invalid JSON");
  assert.deepEqual(readPracticeDraft(key), { selectedOption: "", reasoningText: "" });
});
