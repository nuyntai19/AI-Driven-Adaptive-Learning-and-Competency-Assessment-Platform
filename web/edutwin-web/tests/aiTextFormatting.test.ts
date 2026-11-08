import assert from "node:assert/strict";
import test from "node:test";
import { normalizeAITextLineBreaks } from "../src/utils/aiTextFormatting.ts";

test("restores LaTeX commands whose backslash JSON turned into a control character", () => {
  assert.equal(
    normalizeAITextLineBreaks("mẫu số $x - 2 \neq 0$, suy ra $x \neq 2$."),
    "mẫu số $x - 2 \\neq 0$, suy ra $x \\neq 2$.",
  );
  assert.equal(normalizeAITextLineBreaks("Ta có $\frac{1}{2} \times 4$."), "Ta có $\\frac{1}{2} \\times 4$.");
});

test("a real line break between formulas stays a line break", () => {
  assert.equal(normalizeAITextLineBreaks("Bước 1: $x=1$\nBước 2: $y=2$"), "Bước 1: $x=1$\nBước 2: $y=2$");
});

test("closes a formula that swallowed the following sentence", () => {
  assert.equal(
    normalizeAITextLineBreaks("Ta có $1 - 4 + 3 = 0 nên phương trình có nghiệm là $x = 1$ và $x = 3$. Hết."),
    "Ta có $1 - 4 + 3 = 0$ nên phương trình có nghiệm là $x = 1$ và $x = 3$. Hết.",
  );
});

test("leaves well-formed formulas and \\text alone", () => {
  const text = "Vậy $x \\neq 2$ và $y = 1 \\text{ nên đúng}$.";
  assert.equal(normalizeAITextLineBreaks(text), text);
});
