import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { isLikelyProse, isPureMathString, tokenizePlainText } from "../src/utils/mathExpression.ts";

test("a LaTeX command followed by a subscript is recognised as a formula", () => {
  assert.equal(isPureMathString("\\int_{4}^{3} x^2"), true);
  assert.equal(isPureMathString("\\sum_{i=1}^{n} i"), true);
  assert.equal(isLikelyProse("\\sum_{i}"), false);
});

test("the renderer treats a leading command with a subscript as one formula", () => {
  const source = fs.readFileSync(path.resolve(import.meta.dirname, "../src/components/math/RichMathText.tsx"), "utf-8");
  const match = source.match(/isPureLatex = !hasDelimitedMath &&\s*(\/.+\/)\.test\(rawContent\)/);
  assert.ok(match);
  const pattern = new RegExp(match[1].slice(1, -1));
  assert.ok(pattern.test("\\int_{4}^{3} x^2"));
  assert.ok(!pattern.test("\\integer"));
});

test("a Unicode square stays inside the surrounding expression", () => {
  const math = tokenizePlainText("Giải phương trình x² - 5x + 6 = 0.").filter((part) => part.type === "math");
  assert.equal(math.length, 1);
  assert.match(math[0].latex ?? "", /x\^\{?2\}?\s*-\s*5x/);
});
