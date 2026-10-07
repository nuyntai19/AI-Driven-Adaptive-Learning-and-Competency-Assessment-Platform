import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";
import ts from "typescript";
import katex from "katex";
import * as answerHelpers from "../src/components/math/answer-editor/answerEditorHelpers.ts";
import * as richHelpers from "../src/components/math/richMathEditorHelpers.ts";
import * as formulaValidation from "../src/pages/centerManagerQuestionEditorHelpers.ts";

// Execute production TSX with hook/ref adapters. These are component contract tests,
// not browser E2E; the prose editor's change/clear/value logic is not copied here.
function hookHarness() {
  const slots: any[] = [];
  let cursor = 0;
  let effects: (() => void)[] = [];
  const effect = (callback: () => void, deps: unknown[]) => {
    const index = cursor++;
    const previous = slots[index];
    if (!previous || deps.some((dep, i) => dep !== previous[i])) effects.push(callback);
    slots[index] = deps;
  };
  return {
    begin() { cursor = 0; effects = []; },
    flushEffects() { for (const callback of effects) callback(); effects = []; },
    react: {
      forwardRef: (render: unknown) => render,
      useRef(initial: unknown) {
        const index = cursor++;
        return slots[index] ??= { current: initial };
      },
      useState(initial: unknown) {
        const index = cursor++;
        slots[index] ??= { value: initial };
        return [slots[index].value, (next: unknown) => {
          slots[index].value = typeof next === "function" ? next(slots[index].value) : next;
        }];
      },
      useEffect: effect,
      useLayoutEffect: effect,
      useCallback: (callback: unknown) => { cursor++; return callback; },
      useImperativeHandle(ref: any, factory: () => unknown) { cursor++; ref.current = factory(); },
    },
  };
}

function loadComponent(file: string, hooks: ReturnType<typeof hookHarness>, modules: Record<string, unknown>) {
  const source = fs.readFileSync(new URL("../src/components/math/" + file, import.meta.url), "utf8");
  const output = ts.transpileModule(source, {
    compilerOptions: { jsx: ts.JsxEmit.ReactJSX, module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true },
  }).outputText;
  const result = { exports: {} as any };
  vm.runInNewContext(output, {
    exports: result.exports, module: result,
    require(name: string) {
      if (name === "react") return hooks.react;
      if (name === "react/jsx-runtime") return {
        jsx: (type: unknown, props: unknown) => ({ type, props }),
        jsxs: (type: unknown, props: unknown) => ({ type, props }),
      };
      if (name in modules) return modules[name];
      throw new Error("Unexpected dependency in component test: " + name);
    },
  });
  return result.exports;
}

function flatten(node: any): any[] {
  if (!node || typeof node !== "object") return [];
  if (Array.isArray(node)) return node.flatMap(flatten);
  return [node, ...flatten(node.props?.children)];
}

function visibleText(node: any): string {
  if (typeof node === "string" || typeof node === "number") return String(node);
  if (Array.isArray(node)) return node.map(visibleText).join("");
  return visibleText(node?.props?.children ?? "");
}

function proseHarness(extra: Record<string, unknown> = {}) {
  const hooks = hookHarness();
  const RichMathEditor = () => null;
  const { MultilineProseAnswerEditor } = loadComponent("answer-editor/MultilineProseAnswerEditor.tsx", hooks, {
    "./answerEditorHelpers": answerHelpers, "../RichMathEditor": { RichMathEditor },
  });
  const ref = { current: null as any };
  const changes: any[] = [];
  const calls: string[] = [];
  let props: any = { value: { rawText: "Kết luận: ", displayLatex: "legacy" }, onChange: (value: unknown) => changes.push(value), ...extra };
  let tree: any;
  const render = (next: Record<string, unknown> = {}) => {
    props = { ...props, ...next };
    hooks.begin();
    tree = MultilineProseAnswerEditor(props, ref);
    assert.equal(tree.type, RichMathEditor);
    // Simulated child emits serialized rich content through the real adapter callback.
    tree.props.ref.current = {
      insertLatex(latex: string) { calls.push(latex); tree.props.onChange(ref.current.getValue().rawText + "$" + latex + "$"); },
      focus() { calls.push("focus"); },
      clear() { calls.push("clear"); tree.props.onChange(""); },
    };
    hooks.flushEffects();
    return tree;
  };
  render();
  return { ref, changes, calls, render, get tree() { return tree; } };
}

test("essay always exposes the shared visual formula composer even when preview is disabled", () => {
  const h = proseHarness({ showPreview: false });
  assert.equal(h.tree.props.variant, "student");
  assert.equal(h.tree.props.disabled, false);
  assert.equal(h.tree.props.minHeight, "144px");
  assert.equal(h.tree.props.value, "Kết luận: ");
});

test("mixed Vietnamese prose + variable/formula is available to submission without a parent render", () => {
  const h = proseHarness();
  const content = "Vậy nghiệm là $x=2a+1$ và $y=\\frac{1}{2}$.";
  h.tree.props.onChange(content);
  assert.deepEqual(h.ref.current.getValue(), { rawText: content, displayLatex: "" });
  assert.deepEqual(answerHelpers.buildSubmissionAnswerFields("Essay", "Manual", h.ref.current.getValue()),
    { finalAnswer: content, answerDisplayLatex: "" });
  assert.equal(h.changes.length, 1);
});

test("Casio and formula insertion aliases delegate to the same composer and preserve fresh content", () => {
  const h = proseHarness();
  h.ref.current.insertLatex("x=2a+1");
  h.ref.current.insertAtCursor("\\frac{1}{2}");
  assert.deepEqual(h.calls, ["x=2a+1", "\\frac{1}{2}"]);
  assert.deepEqual(h.ref.current.getValue(), { rawText: "Kết luận: $x=2a+1$$\\frac{1}{2}$", displayLatex: "" });
});

test("clear delegates to the visual editor and synchronously clears the submission value", () => {
  const h = proseHarness();
  h.tree.props.onChange("Vậy $x=3$.");
  h.ref.current.clear();
  assert.deepEqual(h.calls, ["clear"]);
  assert.deepEqual(h.ref.current.getValue(), { rawText: "", displayLatex: "" });
});

test("restoring another draft updates getValue and strips legacy displayLatex from the prose contract", () => {
  const h = proseHarness();
  h.tree.props.onChange("LOCAL");
  h.render({ value: { rawText: "Bản nháp $a+b$", displayLatex: "old-latex" } });
  assert.deepEqual(h.ref.current.getValue(), { rawText: "Bản nháp $a+b$", displayLatex: "" });
});

for (const flag of ["disabled", "readOnly"]) {
  test(flag + " prevents formula insertion, clear and late change callbacks", () => {
    const h = proseHarness({ [flag]: true, autoFocus: true });
    h.ref.current.insertLatex("x=3");
    h.ref.current.insertAtCursor("y=4");
    h.ref.current.clear();
    h.tree.props.onChange("MUTATED");
    assert.equal(h.tree.props.disabled, true);
    assert.equal(h.changes.length, 0);
    assert.equal(h.calls.length, 0);
    assert.equal(h.ref.current.getValue().rawText, "Kết luận: ");
  });
}

test("focus and actor styling are forwarded without overwriting the answer", () => {
  const onFocus = () => {};
  const h = proseHarness({ autoFocus: true, onFocus, variant: "teacher", ariaLabel: "Đáp án chuẩn" });
  assert.deepEqual(h.calls, ["focus"]);
  assert.equal(h.tree.props.onFocus, onFocus);
  assert.equal(h.tree.props.variant, "teacher");
  assert.equal(h.tree.props.label, "Đáp án chuẩn");
  assert.equal(h.changes.length, 0);
});

test("manual short answers reuse the essay bridge with compact height and forward props/ref", () => {
  const hooks = hookHarness();
  const MultilineProseAnswerEditor = () => null;
  const { PlainOrMultilineAnswerInput } = loadComponent("answer-editor/PlainOrMultilineAnswerInput.tsx", hooks, {
    "./MultilineProseAnswerEditor": { MultilineProseAnswerEditor },
  });
  const ref = { current: null };
  const value = { rawText: "Vậy $x=a$.", displayLatex: "" };
  const onFocus = () => {};
  const tree = PlainOrMultilineAnswerInput({ value, onFocus, variant: "student", showPreview: false }, ref);
  assert.equal(tree.type, MultilineProseAnswerEditor);
  assert.equal(tree.props.rows, 2);
  assert.equal(tree.props.value, value);
  assert.equal(tree.props.onFocus, onFocus);
  assert.equal(tree.props.ref, ref);
});

function modeHarness(props: Record<string, unknown>) {
  const hooks = hookHarness();
  const types: Record<string, any> = {};
  const modules: Record<string, unknown> = { "./answerEditorHelpers": answerHelpers };
  for (const name of ["PlainTextAnswerInput", "NumericRationalMathInput", "Coordinate2DInput", "PlainOrMultilineAnswerInput", "MultilineProseAnswerEditor"]) {
    types[name] = () => null;
    modules["./" + name] = { [name]: types[name] };
  }
  types.MathPreviewCore = () => null;
  modules["../MathPreviewCore"] = { MathPreviewCore: types.MathPreviewCore };
  const { ModeAwareAnswerEditor } = loadComponent("answer-editor/ModeAwareAnswerEditor.tsx", hooks, modules);
  hooks.begin();
  const tree = ModeAwareAnswerEditor({ value: { rawText: "Vậy $x=a$.", displayLatex: "" }, ...props }, { current: null });
  return { nodes: flatten(tree), types };
}

for (const [questionType, name] of [["Essay", "MultilineProseAnswerEditor"], ["ShortAnswer", "PlainOrMultilineAnswerInput"]]) {
  test(questionType + ":Manual keeps its grading mode and defaults to student formula UI", () => {
    const { nodes, types } = modeHarness({ questionType, evaluationMode: "Manual", profile: "answering", showPreview: false });
    const editor = nodes.find(node => node.type === types[name]);
    assert.ok(editor);
    assert.equal(editor.props.variant, "student");
    assert.equal(editor.props.showPreview, false);
    assert.equal(answerHelpers.resolveAnswerInputType(questionType, "Manual").isValid, true);
  });
}

test("TextExact and numeric answers keep specialized inputs, not a prose formula editor", () => {
  for (const [mode, name] of [["TextExact", "PlainTextAnswerInput"], ["NumericRational", "NumericRationalMathInput"]]) {
    const { nodes, types } = modeHarness({ questionType: "ShortAnswer", evaluationMode: mode });
    assert.ok(nodes.some(node => node.type === types[name]));
    assert.ok(!nodes.some(node => node.type === types.MultilineProseAnswerEditor || node.type === types.PlainOrMultilineAnswerInput));
  }
});

test("submitted essay answers render rich read-only content without an editable composer", () => {
  const { nodes, types } = modeHarness({ questionType: "Essay", evaluationMode: "Manual", readOnly: true });
  assert.ok(!nodes.some(node => node.type === types.MultilineProseAnswerEditor));
  const preview = nodes.find(node => node.type === types.MathPreviewCore);
  assert.equal(preview.props.mode, "rich");
  assert.equal(preview.props.content, "Vậy $x=a$.");
});

function richHarness(props: Record<string, unknown> = {}) {
  const hooks = hookHarness();
  const changes: string[] = [];
  const { RichMathEditor } = loadComponent("RichMathEditor.tsx", hooks, {
    katex,
    "../../pages/centerManagerQuestionEditorHelpers": formulaValidation,
    "./VisualMathField": { VisualMathField: () => null },
    "./richMathEditorHelpers": richHelpers,
    "../../utils/themeMode": { useThemeMode: () => ({ isDark: false }) },
    "../../utils/visualMathFieldLifecycle": {
      hideVirtualKeyboard() {}, clearVirtualKeyboardDismissalProtection() {},
      isVirtualKeyboardVisible: () => false, isVirtualKeyboardDismissalActive: () => false,
      consumeVirtualKeyboardDismissalProtection: () => false,
    },
  });
  const ref = { current: null as any };
  const render = () => {
    hooks.begin();
    return flatten(RichMathEditor({ value: "Vậy $x=3$.", onChange: (text: string) => changes.push(text), ...props }, ref));
  };
  return { ref, render, changes };
}

test("production student toolbar includes Chèn công thức without exposing raw-source editing", () => {
  const h = richHarness({ variant: "student" });
  const buttons = h.render().filter(node => node.type === "button");
  assert.ok(buttons.some(node => visibleText(node).includes("Chèn công thức")));
  assert.ok(!buttons.some(node => visibleText(node).includes("Xem mã")));
});

test("shared composer clears DOM and value together; source and disabled modes stay immutable", () => {
  const h = richHarness({ variant: "teacher" });
  let nodes = h.render();
  const textbox = nodes.find(node => node.props?.role === "textbox");
  let emptied = false;
  textbox.props.ref.current = {
    replaceChildren() { emptied = true; }, textContent: "",
    querySelector: () => null, setAttribute() {}, removeAttribute() {},
  };
  h.ref.current.clear();
  assert.equal(emptied, true);
  assert.deepEqual(h.changes, [""]);
  nodes.find(node => node.type === "button" && visibleText(node).includes("Xem mã")).props.onClick();
  nodes = h.render();
  assert.equal(nodes.find(node => node.type === "textarea").props.readOnly, true);
  h.ref.current.clear();
  assert.deepEqual(h.changes, [""]);
  const disabled = richHarness({ disabled: true });
  assert.ok(!disabled.render().some(node => node.type === "button" && visibleText(node).includes("Chèn công thức")));
  disabled.ref.current.clear();
  assert.equal(disabled.changes.length, 0);
});
