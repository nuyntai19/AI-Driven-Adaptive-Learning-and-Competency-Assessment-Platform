import assert from "node:assert/strict";
import test from "node:test";
import { answerReasoningPlaceholder, answerWorkPlaceholder } from "../src/utils/answerPlaceholders.ts";
import { resolveStudentSubjectId, studentSubjectsInView } from "../src/utils/studentSubjectScope.ts";

const math = { subjectId: "math" };
const english = { subjectId: "english" };
const physics = { subjectId: "physics" };
const subjects = [math, english, physics];

test("only subjects of the student's current classes are offered", () => {
  const classes = [
    { subjectId: "english", isHistorical: false },
    { subjectId: "physics", isHistorical: true },
  ];
  assert.deepEqual(studentSubjectsInView(subjects, classes, false), [english]);
  assert.deepEqual(studentSubjectsInView(subjects, classes, true), [physics]);
});

test("a student with no classes is offered no subject", () => {
  assert.deepEqual(studentSubjectsInView(subjects, [], false), []);
});

test("history view without past classes keeps the current subjects", () => {
  assert.deepEqual(studentSubjectsInView(subjects, [{ subjectId: "math", isHistorical: false }], true), [math]);
});

test("the current subject wins when the student studies it", () => {
  assert.equal(resolveStudentSubjectId([math, english], "english", "math"), "english");
});

test("a missing or foreign subject falls back to the remembered one, then the first", () => {
  assert.equal(resolveStudentSubjectId([math, english], "", "english"), "english");
  assert.equal(resolveStudentSubjectId([math, english], "physics", null), "math");
  assert.equal(resolveStudentSubjectId([math, english], "", "physics"), "math");
  assert.equal(resolveStudentSubjectId([], "", null), null);
});

test("non-math subjects never mention numbers or formulas", () => {
  assert.doesNotMatch(answerWorkPlaceholder(false), /công thức|số|biểu thức/);
  assert.doesNotMatch(answerReasoningPlaceholder(false), /công thức/);
  assert.match(answerWorkPlaceholder(true), /Ctrl\+M/);
  assert.match(answerReasoningPlaceholder(true), /Ctrl\+M/);
});
