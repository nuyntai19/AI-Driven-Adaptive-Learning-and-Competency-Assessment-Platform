import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";
import { createRequire } from "node:module";
import ts from "typescript";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import * as previewUtils from "../src/components/math/mathPreviewUtils.ts";
import * as mathExpression from "../src/utils/mathExpression.ts";

const require = createRequire(import.meta.url);
function component(relative: string, dependencies: Record<string, unknown> = {}) {
  const code = ts.transpileModule(fs.readFileSync(new URL(`../src/components/${relative}.tsx`, import.meta.url), "utf8"),
    { compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX, esModuleInterop: true, target: ts.ScriptTarget.ES2020 } }).outputText;
  const exports: Record<string, any> = {};
  vm.runInNewContext(code, { exports, require: (name: string) => dependencies[name] ?? require(name) });
  return exports;
}
const rich = component("math/RichMathText", { "../../utils/mathExpression": mathExpression });
const preview = component("math/MathPreviewCore", { "./mathPreviewUtils": previewUtils, "./RichMathText": rich });
const rubric = component("reviews/RubricGradeView", { "../math/RichMathText": rich });

test("whole formula delimiters are removed for the KaTeX formula API, not mixed content", () => {
  for (const formula of ["$ \\text{10} $", "$$ \\text{10} $$", "\\( \\text{10} \\)", "\\[ \\text{10} \\]", "\\text{10}"]) {
    assert.equal(previewUtils.unwrapMathDelimiters(formula), "\\text{10}");
    assert.doesNotMatch(previewUtils.renderSafeKatex(formula)!, /katex-error/);
  }
  assert.equal(previewUtils.unwrapMathDelimiters("$x$ + $y$"), "$x$ + $y$");
  assert.equal(previewUtils.unwrapMathDelimiters("Giá $10"), "Giá $10");
  assert.equal(previewUtils.renderSafeKatex("$ $"), null);
});

test("actual readonly preview renders the saved numeric answer instead of red dollar notation", () => {
  const html = renderToStaticMarkup(React.createElement(preview.MathPreviewCore, { formula: "$ \\text{10} $", mode: "formula" }));
  assert.match(html, /class="katex"/);
  assert.doesNotMatch(html, /katex-error/);
});

test("the shared rubric renders inline math in titles and AI comments without changing scores", () => {
  const html = renderToStaticMarkup(React.createElement(rubric.RubricGradeView, { grade: { maxScore: 10, awardedScore: 10,
    criteria: [{ criterionId: "method", title: "Góc $60^{\\circ}$", maxScore: 4, awardedScore: 4,
      comment: "Tính đúng $BC^2 = 52$." }, { criterionId: "result", title: "Kết quả", maxScore: 6, awardedScore: 6 }] } }));
  assert.equal((html.match(/class="katex"/g) ?? []).length, 2);
  assert.doesNotMatch(html, /katex-error/);
  assert.match(html, /4 \/ 4/);
  assert.match(html, /Tổng: 10 \/ 10/);
  assert.doesNotMatch(html, /<input|<textarea|<button/);
});

test("math remains untrusted: HTML commands do not create executable markup", () => {
  const html = previewUtils.renderSafeKatex("$\\href{javascript:alert(1)}{x}$")!;
  assert.doesNotMatch(html, /href="javascript:/);
});
