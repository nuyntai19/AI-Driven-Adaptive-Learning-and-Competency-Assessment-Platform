import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import katex from "katex";
import { normalizeMathExpression } from "../src/components/math/RichMathText";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

test("index.css hides virtual keyboard toggle and menu toggle in text mode", () => {
  const cssPath = path.resolve(__dirname, "../src/index.css");
  const cssContent = fs.readFileSync(cssPath, "utf-8");

  assert.match(
    cssContent,
    /math-field\[data-input-mode="text"\]::part\(virtual-keyboard-toggle\)/,
    "Expected selector for hiding virtual-keyboard-toggle on data-input-mode='text'"
  );
  assert.match(
    cssContent,
    /math-field\[data-input-mode="text"\]::part\(menu-toggle\)/,
    "Expected selector for hiding menu-toggle on data-input-mode='text'"
  );
  assert.match(
    cssContent,
    /\.math-field-text-mode math-field::part\(virtual-keyboard-toggle\)/,
    "Expected selector for hiding virtual-keyboard-toggle on .math-field-text-mode container"
  );
  assert.match(
    cssContent,
    /\.math-field-text-mode math-field::part\(menu-toggle\)/,
    "Expected selector for hiding menu-toggle on .math-field-text-mode container"
  );
  assert.match(
    cssContent,
    /display:\s*none\s*!important/,
    "Expected display: none !important rule"
  );
});

test("VisualMathField sets data-input-mode and configures shadow DOM toggle suppression", () => {
  const componentPath = path.resolve(
    __dirname,
    "../src/components/math/VisualMathField.tsx"
  );
  const componentContent = fs.readFileSync(componentPath, "utf-8");

  assert.match(
    componentContent,
    /mf\.setAttribute\("data-input-mode",\s*"math"\)/,
    "Initial data-input-mode must be math"
  );
  assert.match(
    componentContent,
    /data-edutwin-toggle-guard/,
    "Expected shadow root style tag to guard toggles in text mode"
  );
  assert.match(
    componentContent,
    /:host\(\[data-input-mode="text"\]\)\s*\[part="virtual-keyboard-toggle"\]/,
    "Expected shadow rule for virtual keyboard toggle suppression"
  );
  assert.match(
    componentContent,
    /:host\(\[data-input-mode="text"\]\)\s*\[part="menu-toggle"\]/,
    "Expected shadow rule for menu toggle suppression"
  );
  assert.match(
    componentContent,
    /mathVirtualKeyboard\?\.hide\(\)/,
    "Expected virtual keyboard to be automatically closed when switching to text mode"
  );
  assert.match(
    componentContent,
    /math-field-text-mode/,
    "Expected container to carry math-field-text-mode class"
  );
});

test("normalizeMathExpression renders exponential fractions without duplicate left/right tags (Question #20009)", () => {
  const { latex: qLatex } = normalizeMathExpression("(1/2)^(x^2 - x) >= 1/4");
  assert.equal(qLatex.includes("\\left\\left"), false);
  assert.equal(qLatex.includes("\\right\\right"), false);
  assert.doesNotThrow(() => {
    katex.renderToString(qLatex, { throwOnError: true });
  });

  const { latex: solLatex } = normalizeMathExpression("1/4 = (1/2)^2");
  assert.equal(solLatex.includes("\\left\\left"), false);
  assert.equal(solLatex.includes("\\right\\right"), false);
  assert.doesNotThrow(() => {
    katex.renderToString(solLatex, { throwOnError: true });
  });
});
