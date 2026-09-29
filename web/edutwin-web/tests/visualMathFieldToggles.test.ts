import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import katex from "katex";
import { normalizeMathExpression } from "../src/utils/mathExpression.ts";

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

test("tokenizePlainText preserves Vietnamese sentence spacing and tokenizes set notation (Question #20001)", async () => {
  const { tokenizePlainText, isPureMathString } = await import("../src/utils/mathExpression.ts");

  const sol = "Hàm số xác định khi mẫu số khác 0, tức là x - 2 khác 0, suy ra x khác 2. Vậy tập xác định là D = R \\ {2}.";
  assert.equal(isPureMathString(sol), false, "Full explanation sentence must not be flagged as pure math");

  const tokens = tokenizePlainText(sol);
  const textTokens = tokens.filter((t) => t.type === "text");
  assert.ok(textTokens.length > 0, "Must contain text tokens for Vietnamese words");
  assert.ok(textTokens[0].value?.includes("Hàm số xác định khi mẫu số khác 0"), "Must keep word spaces in text");

  const mathTokens = tokens.filter((t) => t.type === "math");
  assert.ok(mathTokens.some((m) => m.latex?.includes("\\mathbb{R} \\setminus")), "Set notation must be KaTeX formatted");
});

test("isOutsideVirtualKeyboardClick correctly differentiates outside clicks vs keyboard interactions", async () => {
  const { isOutsideVirtualKeyboardClick } = await import(
    "../src/utils/visualMathFieldLifecycle.ts"
  );

  // 1. Click inside keyboard plate or backdrop should NOT be considered outside
  const keyboardPlateMock = {
    tagName: "div",
    classList: { contains: (cls: string) => cls === "MLK__plate" },
  };
  assert.equal(
    isOutsideVirtualKeyboardClick([keyboardPlateMock]),
    false,
    "Click inside .MLK__plate must return false"
  );

  const keyboardContainerMock = {
    tagName: "div",
    classList: { contains: (cls: string) => cls === "ML__keyboard" },
  };
  assert.equal(
    isOutsideVirtualKeyboardClick([keyboardContainerMock]),
    false,
    "Click inside .ML__keyboard must return false"
  );

  const customKeyboardMock = {
    tagName: "math-virtual-keyboard",
    classList: { contains: () => false },
  };
  assert.equal(
    isOutsideVirtualKeyboardClick([customKeyboardMock]),
    false,
    "Click on <math-virtual-keyboard> must return false"
  );

  // 2. Click on the keyboard toggle icon should NOT be considered outside
  const toggleMock = {
    tagName: "button",
    classList: { contains: (cls: string) => cls === "ML__virtual-keyboard-toggle" },
    getAttribute: (attr: string) => (attr === "part" ? "virtual-keyboard-toggle" : null),
  };
  assert.equal(
    isOutsideVirtualKeyboardClick([toggleMock]),
    false,
    "Click on virtual-keyboard-toggle must return false"
  );

  // 3. Click inside math-field or rich-math contenteditable should NOT be considered outside
  const mathFieldMock = {
    tagName: "math-field",
    classList: { contains: () => false },
  };
  assert.equal(
    isOutsideVirtualKeyboardClick([mathFieldMock]),
    false,
    "Click inside <math-field> must return false"
  );

  const richContentMock = {
    tagName: "div",
    classList: { contains: (cls: string) => cls === "rich-math-content-editable" },
  };
  assert.equal(
    isOutsideVirtualKeyboardClick([richContentMock]),
    false,
    "Click inside .rich-math-content-editable must return false"
  );

  const richPopoverMock = {
    tagName: "div",
    classList: { contains: (cls: string) => cls === "rich-math-popover" },
  };
  assert.equal(
    isOutsideVirtualKeyboardClick([richPopoverMock]),
    false,
    "Click inside .rich-math-popover must return false"
  );

  // 4. Click outside (on page container, button, card, header) must return true
  const pageBackgroundMock = {
    tagName: "div",
    classList: { contains: (cls: string) => cls === "page-container" },
  };
  assert.equal(
    isOutsideVirtualKeyboardClick([pageBackgroundMock]),
    true,
    "Click on regular page background must return true"
  );

  const submitButtonMock = {
    tagName: "button",
    classList: { contains: (cls: string) => cls === "btn-submit" },
  };
  assert.equal(
    isOutsideVirtualKeyboardClick([submitButtonMock]),
    true,
    "Click on submit button outside math-field must return true"
  );
});

test("registerVirtualKeyboardDismissListener hides visible virtual keyboard on outside click", async () => {
  const { registerVirtualKeyboardDismissListener, resetVirtualKeyboardListenerForTests } =
    await import("../src/utils/visualMathFieldLifecycle.ts");

  resetVirtualKeyboardListenerForTests();

  let hideCalled = false;
  let isVisible = true;
  let attachedHandler: ((e: unknown) => void) | null = null;

  // Mock browser window and mathVirtualKeyboard environment
  const mockWindow = {
    mathVirtualKeyboard: {
      get visible() {
        return isVisible;
      },
      hide() {
        hideCalled = true;
        isVisible = false;
      },
    },
    addEventListener: (_type: string, handler: (e: unknown) => void) => {
      attachedHandler = handler;
    },
    removeEventListener: () => {
      attachedHandler = null;
    },
  };

  (globalThis as unknown as { window: typeof mockWindow }).window = mockWindow;

  const unregister = registerVirtualKeyboardDismissListener();
  assert.ok(attachedHandler !== null, "Pointerdown listener must be attached to window");
  const invokeHandler = attachedHandler as (e: unknown) => void;

  // 1. Simulate outside click when keyboard is visible -> hide() should be called
  hideCalled = false;
  isVisible = true;
  const outsideEvent = {
    composedPath: () => [
      {
        tagName: "div",
        classList: { contains: (cls: string) => cls === "learning-player-main" },
      },
    ],
  };
  invokeHandler(outsideEvent);
  assert.equal(hideCalled, true, "Outside click must call hide() on virtual keyboard");

  // 2. Simulate click inside virtual keyboard -> hide() must NOT be called
  hideCalled = false;
  isVisible = true;
  const insideKeyboardEvent = {
    composedPath: () => [
      {
        tagName: "div",
        classList: { contains: (cls: string) => cls === "ML__keyboard" },
      },
    ],
  };
  invokeHandler(insideKeyboardEvent);
  assert.equal(hideCalled, false, "Click inside virtual keyboard must NOT call hide()");

  // 3. Simulate click inside math field -> hide() must NOT be called
  hideCalled = false;
  isVisible = true;
  const insideMathFieldEvent = {
    composedPath: () => [
      {
        tagName: "math-field",
        classList: { contains: () => false },
      },
    ],
  };
  invokeHandler(insideMathFieldEvent);
  assert.equal(hideCalled, false, "Click inside <math-field> must NOT call hide()");

  // 4. Clean up listener
  unregister();
  resetVirtualKeyboardListenerForTests();
});

test("VisualMathField configures smartMode: false and smartFence: false for radical, absolute value, and norm stability", () => {
  const componentPath = path.resolve(
    __dirname,
    "../src/components/math/VisualMathField.tsx"
  );
  const componentContent = fs.readFileSync(componentPath, "utf-8");

  assert.match(
    componentContent,
    /mf\.smartFence\s*=\s*false/,
    "mf.smartFence must be explicitly false to prevent vertical pipe auto-pairing interference"
  );
  assert.match(
    componentContent,
    /mf\.smartMode\s*=\s*false/,
    "mf.smartMode must be explicitly false to prevent unwanted automatic text mode switching"
  );
});

test("VisualMathField shadow DOM and index.css inject border-bottom dashed for placeholder styling", () => {
  const componentPath = path.resolve(
    __dirname,
    "../src/components/math/VisualMathField.tsx"
  );
  const componentContent = fs.readFileSync(componentPath, "utf-8");

  assert.match(
    componentContent,
    /\.ML__placeholder\s*\{[\s\S]*?border-bottom:\s*1\.5px\s*dashed\s*currentColor\s*!important/,
    "VisualMathField shadow root must directly style .ML__placeholder with dashed border"
  );

  const cssPath = path.resolve(__dirname, "../src/index.css");
  const cssContent = fs.readFileSync(cssPath, "utf-8");

  assert.match(
    cssContent,
    /border-bottom:\s*1\.5px\s*dashed\s*currentColor\s*!important/,
    "index.css must provide fallback dashed border-bottom styling for placeholders"
  );
  assert.match(
    cssContent,
    /\.bg-slate-950\s+math-field/,
    "index.css must provide high-contrast text color on dark containers like .bg-slate-950"
  );
});

test("VisualMathField insertAtCursor passes silenceNotifications: true and emits onChange exactly once", () => {
  const componentPath = path.resolve(
    __dirname,
    "../src/components/math/VisualMathField.tsx"
  );
  const componentContent = fs.readFileSync(componentPath, "utf-8");

  assert.match(
    componentContent,
    /silenceNotifications:\s*true/,
    "mf.insert must specify silenceNotifications: true to prevent duplicate input event emission"
  );

  let nativeInputEventDispatched = 0;
  let onChangeCallbackCount = 0;

  const mockMathfield: any = {
    value: "2",
    focus: () => {},
    setAttribute: () => {},
    executeCommand: () => {},
    insert: (text: string, options: { silenceNotifications?: boolean }) => {
      if (!options?.silenceNotifications) {
        nativeInputEventDispatched++;
      }
      mockMathfield.value = "2 + " + text;
    },
    getValue: () => mockMathfield.value,
  };

  const insertAtCursorSimulation = (latexToInsert: string) => {
    mockMathfield.insert(latexToInsert, {
      mode: "math",
      selectionMode: "placeholder",
      focus: true,
      silenceNotifications: true,
    });
    onChangeCallbackCount++;
  };

  insertAtCursorSimulation("\\sqrt{3}");
  assert.equal(nativeInputEventDispatched, 0, "No duplicate native input event should be dispatched");
  assert.equal(onChangeCallbackCount, 1, "onChange must be called exactly once per Casio insertion");
});

test("DEFAULT_MATH_INLINE_SHORTCUTS ensures radicals and fences provide interactive caret placeholders", async () => {
  const { DEFAULT_MATH_INLINE_SHORTCUTS } = await import(
    "../src/utils/visualMathFieldLifecycle.ts"
  );

  assert.equal(
    DEFAULT_MATH_INLINE_SHORTCUTS.sqrt,
    "\\sqrt{#?}",
    "Square root inline shortcut must include placeholder (#?) to position caret inside root"
  );
  assert.equal(
    DEFAULT_MATH_INLINE_SHORTCUTS.cbrt,
    "\\sqrt[3]{#?}",
    "Cube root inline shortcut must include placeholder (#?) to position caret inside root"
  );
  assert.equal(
    DEFAULT_MATH_INLINE_SHORTCUTS.abs,
    "\\left|#?\\right|",
    "Absolute value inline shortcut (abs) must include placeholder (#?) to position caret between fences"
  );
  assert.equal(
    DEFAULT_MATH_INLINE_SHORTCUTS["|"],
    "\\left|#?\\right|",
    "Pipe character inline shortcut (|) must include placeholder (#?) to position caret between fences"
  );
  assert.equal(
    DEFAULT_MATH_INLINE_SHORTCUTS.norm,
    "\\left\\|#?\\right\\|",
    "Norm inline shortcut must include placeholder (#?) to position caret between double fences"
  );
});

test("normalizeMathInsertContent maps calculator and toolbar expressions to interactive placeholders", async () => {
  const { normalizeMathInsertContent } = await import(
    "../src/utils/visualMathFieldLifecycle.ts"
  );

  // Radicals
  assert.equal(normalizeMathInsertContent("sqrt("), "\\sqrt{#?}");
  assert.equal(normalizeMathInsertContent("\\sqrt{}"), "\\sqrt{#?}");
  assert.equal(normalizeMathInsertContent("\\sqrt"), "\\sqrt{#?}");
  assert.equal(normalizeMathInsertContent("sqrt"), "\\sqrt{#?}");
  assert.equal(normalizeMathInsertContent("cbrt("), "\\sqrt[3]{#?}");
  assert.equal(normalizeMathInsertContent("\\sqrt[]{}"), "\\sqrt[#?]{#?}");

  // Absolute values and norms
  assert.equal(normalizeMathInsertContent("abs("), "\\left|#?\\right|");
  assert.equal(normalizeMathInsertContent("|"), "\\left|#?\\right|");
  assert.equal(normalizeMathInsertContent("\\abs"), "\\left|#?\\right|");
  assert.equal(normalizeMathInsertContent("\\left|\\right|"), "\\left|#?\\right|");
  assert.equal(normalizeMathInsertContent("||"), "\\left\\|#?\\right\\|");
  assert.equal(normalizeMathInsertContent("norm("), "\\left\\|#?\\right\\|");

  // Fractions and powers
  assert.equal(normalizeMathInsertContent("\\frac{}{}"), "\\frac{#?}{#?}");
  assert.equal(normalizeMathInsertContent("^"), "^{#?}");

  // Preserves existing filled content
  assert.equal(normalizeMathInsertContent("\\sqrt{3}"), "\\sqrt{3}");
  assert.equal(normalizeMathInsertContent("\\left|x+1\\right|"), "\\left|x+1\\right|");
  assert.equal(normalizeMathInsertContent(""), "");
});

test("VisualMathField configures DEFAULT_MATH_INLINE_SHORTCUTS and guards Enter in LaTeX command mode", () => {
  const componentPath = path.resolve(
    __dirname,
    "../src/components/math/VisualMathField.tsx"
  );
  const componentContent = fs.readFileSync(componentPath, "utf-8");

  assert.match(
    componentContent,
    /DEFAULT_MATH_INLINE_SHORTCUTS/,
    "VisualMathField must bind DEFAULT_MATH_INLINE_SHORTCUTS to ensure radical and fence shortcuts"
  );
  assert.match(
    componentContent,
    /normalizeMathInsertContent\(latexOrText\)/,
    "insertAtCursor must normalize incoming formula text via normalizeMathInsertContent"
  );
  assert.match(
    componentContent,
    /mode\s*===\s*"latex"/,
    "Enter key handler must not hijack Enter when MathLive is in LaTeX command mode"
  );
});

test("virtual keyboard dismissal tracking and backdrop protection helpers", async () => {
  const {
    markVirtualKeyboardDismissed,
    wasVirtualKeyboardJustDismissed,
    isVirtualKeyboardVisible,
    hideVirtualKeyboard,
    resetVirtualKeyboardListenerForTests,
  } = await import("../src/utils/visualMathFieldLifecycle.ts");

  resetVirtualKeyboardListenerForTests();

  assert.equal(
    wasVirtualKeyboardJustDismissed(),
    false,
    "Initially virtual keyboard was not just dismissed"
  );

  markVirtualKeyboardDismissed();
  assert.equal(
    wasVirtualKeyboardJustDismissed(500),
    true,
    "Immediately after markVirtualKeyboardDismissed(), wasVirtualKeyboardJustDismissed() must return true"
  );

  let hideCalled = false;
  (globalThis as unknown as { window: unknown }).window = {
    mathVirtualKeyboard: {
      visible: true,
      hide: () => {
        hideCalled = true;
      },
    },
  };

  assert.equal(
    isVirtualKeyboardVisible(),
    true,
    "isVirtualKeyboardVisible() must return true when window.mathVirtualKeyboard.visible is true"
  );

  hideVirtualKeyboard();
  assert.equal(hideCalled, true, "hideVirtualKeyboard() must invoke window.mathVirtualKeyboard.hide()");
});

test("RichMathEditor and InlineMathComposer protect against accidental popover closure on virtual keyboard dismissal", () => {
  const rmePath = path.resolve(__dirname, "../src/components/math/RichMathEditor.tsx");
  const rmeContent = fs.readFileSync(rmePath, "utf-8");

  assert.match(
    rmeContent,
    /wasVirtualKeyboardJustDismissed/,
    "RichMathEditor must check wasVirtualKeyboardJustDismissed() before dismissing popover"
  );
  assert.match(
    rmeContent,
    /isVirtualKeyboardVisible/,
    "RichMathEditor must check isVirtualKeyboardVisible() before dismissing popover"
  );
  assert.match(
    rmeContent,
    /rich-math-popover/,
    "RichMathEditor dialog must include rich-math-popover class"
  );

  const composerPath = path.resolve(__dirname, "../src/components/math/InlineMathComposer.tsx");
  const composerContent = fs.readFileSync(composerPath, "utf-8");

  assert.match(
    composerContent,
    /wasVirtualKeyboardJustDismissed/,
    "InlineMathComposer must check wasVirtualKeyboardJustDismissed() before closing modal"
  );
  assert.match(
    composerContent,
    /isVirtualKeyboardVisible/,
    "InlineMathComposer must check isVirtualKeyboardVisible() before closing modal"
  );
});
