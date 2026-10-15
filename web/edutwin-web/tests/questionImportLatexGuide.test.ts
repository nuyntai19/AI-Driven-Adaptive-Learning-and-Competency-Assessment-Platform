import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";
import test from "node:test";
import ts from "typescript";
import React from "react";
import * as jsxRuntime from "react/jsx-runtime";
import { renderToStaticMarkup } from "react-dom/server";
import { QUESTION_IMPORT_LATEX_EXAMPLES } from "../src/utils/questionImportLatexGuide.ts";
import { validateImportMathProfile } from "../src/utils/questionImportValidation.ts";
import { validateTextMathFormulas } from "../src/pages/centerManagerQuestionEditorHelpers.ts";

const read = (relative: string) => fs.readFileSync(new URL(relative, import.meta.url), "utf8");
const renderer = ts.transpileModule(read("../src/components/math/RichMathText.tsx"), {
  compilerOptions: { module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX, target: ts.ScriptTarget.ES2020 },
}).outputText.replace(/from\s+["']([^"']+)["']/g, (_match, specifier: string) => {
  const target = specifier === "../../utils/mathExpression"
    ? new URL("../src/utils/mathExpression.ts", import.meta.url).href
    : specifier === "../../utils/mathDelimiters.ts"
      ? new URL("../src/utils/mathDelimiters.ts", import.meta.url).href
      : import.meta.resolve(specifier);
  return `from ${JSON.stringify(target)}`;
});
const { RichMathText } = await import(`data:text/javascript;base64,${Buffer.from(renderer).toString("base64")}`);

type Element = React.ReactElement<Record<string, unknown>>;
function elements(node: React.ReactNode, predicate: (element: Element) => boolean): Element[] {
  if (Array.isArray(node)) return node.flatMap((child) => elements(child, predicate));
  if (!React.isValidElement<Record<string, unknown>>(node)) return [];
  return [...(predicate(node) ? [node] : []), ...elements(node.props.children as React.ReactNode, predicate)];
}

/** Run production event handlers with state slots; render the actual RichMathText. */
function guideHarness(writeText: (text: string) => Promise<void>) {
  const slots: unknown[] = [];
  let cursor = 0;
  const source = ts.transpileModule(read("../src/components/teacher/ImportLatexGuide.tsx"), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX, target: ts.ScriptTarget.ES2020 },
  }).outputText;
  const result = { exports: {} as { ImportLatexGuide: () => React.ReactNode } };
  vm.runInNewContext(source, {
    module: result, exports: result.exports,
    navigator: { clipboard: { writeText } },
    require(specifier: string) {
      if (specifier === "react/jsx-runtime") return jsxRuntime;
      if (specifier === "react") return { ...React, useId: () => "guide-panel", useState(initial: unknown) {
        const index = cursor++;
        if (!(index in slots)) slots[index] = initial;
        return [slots[index], (next: unknown) => { slots[index] = typeof next === "function" ? next(slots[index]) : next; }];
      } };
      if (specifier === "../../utils/questionImportLatexGuide") return { QUESTION_IMPORT_LATEX_EXAMPLES };
      if (specifier === "../math/RichMathText") return { RichMathText };
      throw new Error(`Unexpected guide dependency: ${specifier}`);
    },
  });
  return { render() { cursor = 0; return result.exports.ImportLatexGuide(); } };
}

test("all copied examples are complete V1 inline sources accepted by the real syntax validator", () => {
  assert.equal(QUESTION_IMPORT_LATEX_EXAMPLES.length, 12);
  for (const { label, latex } of QUESTION_IMPORT_LATEX_EXAMPLES) {
    assert.ok(latex.startsWith("$") && latex.endsWith("$"), label);
    assert.equal(validateImportMathProfile(latex), null, label);
    assert.deepEqual(validateTextMathFormulas(latex), [], label);
  }
});

test("guide opens accessibly inside the import modal and renders every example with actual KaTeX", () => {
  const harness = guideHarness(async () => {});
  let tree = harness.render();
  let button = elements(tree, (element) => element.type === "button" && element.props["aria-controls"] === "guide-panel")[0];
  assert.equal(button.props.type, "button");
  assert.equal(button.props["aria-expanded"], false);
  assert.equal(elements(tree, (element) => element.props.id === "guide-panel" && element.props.hidden === true).length, 1);
  assert.equal(elements(tree, (element) => element.type === RichMathText).length, 0);
  (button.props.onClick as () => void)();
  tree = harness.render();
  button = elements(tree, (element) => element.type === "button" && element.props["aria-controls"] === "guide-panel")[0];
  assert.equal(button.props["aria-expanded"], true);
  assert.equal(elements(tree, (element) => element.props.id === "guide-panel").length, 1);
  const html = renderToStaticMarkup(tree);
  assert.equal((html.match(/class="katex"/g) ?? []).length, 12);
  assert.doesNotMatch(html, /katex-error|katex-display|role="dialog"/);
  assert.doesNotMatch(html, /[\u0000-\u0008\u000b\u000c\u000e-\u001f]/, "LaTeX commands must not become control characters in any instructional example");
  assert.match(html, /NumericRational/);
  for (const { latex } of QUESTION_IMPORT_LATEX_EXAMPLES)
    assert.ok(elements(tree, (element) => element.type === "code" && element.props.children === latex).length);
  (button.props.onClick as () => void)();
  assert.equal(elements(harness.render(), (element) => element.type === RichMathText).length, 0);
});

test("copy uses the exact complete source including dollars and preserves guide state", async () => {
  const copied: string[] = [];
  const harness = guideHarness(async (text) => { copied.push(text); });
  const openButton = elements(harness.render(), (element) => element.props["aria-controls"] === "guide-panel")[0];
  (openButton.props.onClick as () => void)();
  const copyButton = elements(harness.render(), (element) => element.props["aria-label"] === "Sao chép mã phân số")[0];
  await (copyButton.props.onClick as () => Promise<void>)();
  assert.deepEqual(copied, [QUESTION_IMPORT_LATEX_EXAMPLES[0].latex]);
  const tree = harness.render();
  assert.equal(elements(tree, (element) => element.props["aria-expanded"] === true).length, 1);
  assert.match(renderToStaticMarkup(tree), /Đã sao chép mã: Phân số/);
});

test("clipboard failure gives manual copy guidance without dismissing the help", async () => {
  const harness = guideHarness(async () => { throw new Error("Clipboard denied"); });
  const openButton = elements(harness.render(), (element) => element.props["aria-controls"] === "guide-panel")[0];
  (openButton.props.onClick as () => void)();
  const copyButton = elements(harness.render(), (element) => element.props["aria-label"] === "Sao chép mã phân số")[0];
  await (copyButton.props.onClick as () => Promise<void>)();
  const html = renderToStaticMarkup(harness.render());
  assert.match(html, /Ctrl\+C/);
  assert.match(html, /aria-expanded="true"/);
  assert.doesNotMatch(html, /Clipboard denied/);
});

test("import form mounts the isolated help beside its current settings and preview", () => {
  const source = read("../src/components/teacher/QuestionImportModal.tsx");
  assert.match(source, /!result && <ImportLatexGuide allowDisplay=\{source !== "excel"\} \/>/);
  assert.doesNotMatch(source, /<ImportLatexGuide[^>]+(?:onClose|onToggle|onClick|key)=/);
});
