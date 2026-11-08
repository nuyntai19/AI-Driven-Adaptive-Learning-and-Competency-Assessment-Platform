import assert from "node:assert/strict";
import test from "node:test";
import { deriveFinalAnswer, isAnswerReadable, normalizeFinalAnswer } from "../src/utils/finalAnswer.ts";

test("math answers are reduced to what the grader parses", () => {
  assert.equal(normalizeFinalAnswer("$\\frac{1}{2}$", "NumericRational"), "\\frac{1}{2}");
  assert.equal(normalizeFinalAnswer("Vậy x = 5", "NumericRational"), "5");
  assert.equal(normalizeFinalAnswer("Đáp số: $x = \\dfrac{3}{4}$.", "NumericRational"), "\\frac{3}{4}");
  assert.equal(normalizeFinalAnswer("x = 10/2 = 5", "NumericRational"), "5");
  assert.equal(normalizeFinalAnswer("M = (1;2)", "Coordinate2D"), "(1;2)");
  assert.equal(normalizeFinalAnswer("x = 1; y = 2", "MathEquivalent"), "x = 1; y = 2");
});

test("text answers keep their wording", () => {
  assert.equal(normalizeFinalAnswer("x = 5", "TextExact"), "x = 5");
  assert.equal(normalizeFinalAnswer("Vậy là đúng", "TextExact"), "Vậy là đúng");
  assert.equal(normalizeFinalAnswer("Đáp án: curriculum vitae", "TextExact"), "curriculum vitae");
  assert.equal(normalizeFinalAnswer("$-1$", undefined), "-1");
});

test("essays are submitted whole, other questions by their last line", () => {
  const work = "Mở bài\n\nKết luận\n";
  assert.equal(deriveFinalAnswer(work, "Essay", "Manual"), "Mở bài\n\nKết luận");
  assert.equal(deriveFinalAnswer("2x = 10\nx = 5\n\n", "ShortAnswer", "NumericRational"), "5");
  assert.equal(deriveFinalAnswer("   \n", "ShortAnswer", "NumericRational"), "");
});

test("a sentence concludes with its last formula", () => {
  const numeric = (line: string) => normalizeFinalAnswer(line, "NumericRational");
  assert.equal(numeric("Thay $x=2$ được $12$"), "12");
  assert.equal(numeric("Vậy đáp án là $12$"), "12");
  assert.equal(numeric("Kết quả bằng $\\dfrac{1}{2}$ nhé"), "\\frac{1}{2}");
  assert.equal(numeric("$y'=3x^2$, tại $x=2$ thì $y'=12$"), "12");
  assert.equal(numeric("Vậy $y'(2)=12$."), "12");
});

test("formulas that form one answer are not cut down to the last", () => {
  assert.equal(normalizeFinalAnswer("$x=1$; $y=2$", "MathEquivalent"), "x=1; y=2");
  assert.equal(normalizeFinalAnswer("Vậy $x=2$ hoặc $x=3$", "MathEquivalent"), "x=2; x=3");
});

test("a value written after the formula is not replaced by the formula", () => {
  assert.equal(normalizeFinalAnswer("$y'(2)$ = 12", "NumericRational"), "12");
});

test("lines without formulas keep the plain reading", () => {
  assert.equal(normalizeFinalAnswer("y' = 3x^2 nên y'(2) = 12", "NumericRational"), "12");
  assert.equal(normalizeFinalAnswer("Đáp án 12", "NumericRational"), "12");
});

test("an answer the grader cannot read is flagged before submitting", () => {
  assert.equal(isAnswerReadable("12", "NumericRational"), true);
  assert.equal(isAnswerReadable("\\frac{1}{2}", "NumericRational"), true);
  assert.equal(isAnswerReadable("là 12", "NumericRational"), false);
  assert.equal(isAnswerReadable("2 được 12", "NumericRational"), false);
  assert.equal(isAnswerReadable("y' = 3x^2, tai x = 2", "NumericRational"), false);
  assert.equal(isAnswerReadable("x = 1; y = 2", "MathEquivalent"), true);
  assert.equal(isAnswerReadable("sin x + 2", "MathEquivalent"), true);
  assert.equal(isAnswerReadable("2x + 1 nhé", "MathEquivalent"), false);
  assert.equal(isAnswerReadable("bất kỳ câu gì", "TextExact"), true);
  assert.equal(isAnswerReadable("   ", "TextExact"), false);
});

test("language answers use every line, unless the last one is marked", () => {
  const whole = (work: string) => deriveFinalAnswer(work, "ShortAnswer", "TextExact", true);
  assert.equal(whole("The girl who is wearing a red dress\nis my sister."), "The girl who is wearing a red dress is my sister.");
  assert.equal(whole("She said go\nAnswer: went"), "went");
  assert.equal(whole("bought"), "bought");
  assert.equal(whole("  \n "), "");
  assert.equal(deriveFinalAnswer("first\nsecond", "ShortAnswer", "TextExact"), "second");
  assert.equal(deriveFinalAnswer("Mở bài\nKết bài", "Essay", "Manual", true), "Mở bài\nKết bài");
});
