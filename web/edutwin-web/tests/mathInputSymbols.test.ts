import assert from "node:assert/strict";
import test from "node:test";
import { MATH_INPUT_SYMBOLS, resolveMathSymbolValue } from "../src/components/math/mathInputSymbols.ts";

const findSymbol = (tab: keyof typeof MATH_INPUT_SYMBOLS, label: string) => {
  const symbol = MATH_INPUT_SYMBOLS[tab].find((item) => item.label === label);
  assert.ok(symbol, `Missing ${label} in ${tab}`);
  return symbol;
};

test("root toolbar provides square, cube, fourth and arbitrary degree templates", () => {
  assert.equal(findSymbol("basic", "√x").value, String.raw`\sqrt{#?}`);
  assert.equal(findSymbol("basic", "∛x").value, String.raw`\sqrt[3]{#?}`);
  assert.equal(findSymbol("basic", "∜x").value, String.raw`\sqrt[4]{#?}`);
  assert.equal(findSymbol("basic", "ⁿ√x").value, String.raw`\sqrt[#?]{#?}`);
});

test("integral toolbar provides editable integrand, bounds and differential variable", () => {
  assert.equal(findSymbol("calculus", "∫ f dx").value, String.raw`\int #?\,\mathrm{d}#?`);
  assert.equal(
    findSymbol("calculus", "∫ₐᵇ f dx").value,
    String.raw`\int_{#?}^{#?}#?\,\mathrm{d}#?`
  );
});

test("compound math templates use interactive MathLive placeholders", () => {
  const compoundLabels = ["a/b", "xⁿ", "ⁿ√x"];
  for (const label of compoundLabels) {
    assert.match(findSymbol("basic", label).value, /#\?/);
  }

  for (const symbol of MATH_INPUT_SYMBOLS.calculus.slice(0, 5)) {
    assert.match(symbol.value, /#\?/, `${symbol.label} should have an editable slot`);
  }
});

test("textarea consumers receive renderable LaTeX without MathLive template markers", () => {
  for (const symbols of Object.values(MATH_INPUT_SYMBOLS)) {
    for (const symbol of symbols) {
      assert.doesNotMatch(resolveMathSymbolValue(symbol, "latex"), /#\?/);
    }
  }

  assert.equal(resolveMathSymbolValue(findSymbol("basic", "ⁿ√x"), "latex"), String.raw`\sqrt[n]{x}`);
  assert.equal(
    resolveMathSymbolValue(findSymbol("calculus", "∫ₐᵇ f dx"), "latex"),
    String.raw`\int_{a}^{b}f(x)\,\mathrm{d}x`
  );
});
