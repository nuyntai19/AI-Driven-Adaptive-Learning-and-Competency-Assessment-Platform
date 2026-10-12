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
    (DEFAULT_MATH_INLINE_SHORTCUTS as Record<string, string>)["|"],
    undefined,
    "Pipe character inline shortcut (|) must NOT be present in DEFAULT_MATH_INLINE_SHORTCUTS to protect set and probability notation"
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

  // Preserves existing filled content and whitespace/raw text
  assert.equal(normalizeMathInsertContent("\\sqrt{3}"), "\\sqrt{3}");
  assert.equal(normalizeMathInsertContent("\\left|x+1\\right|"), "\\left|x+1\\right|");
  assert.equal(normalizeMathInsertContent(""), "");
  assert.equal(normalizeMathInsertContent(" "), " ");
  assert.equal(normalizeMathInsertContent(" x "), " x ");
  assert.equal(normalizeMathInsertContent("5"), "5");
  assert.equal(normalizeMathInsertContent("y + 2"), "y + 2");
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
    /prepareMathFieldInsertion\(mf, latexOrText/,
    "insertAtCursor must use the tested shared insertion preparation"
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
    isVirtualKeyboardDismissalActive,
    consumeVirtualKeyboardDismissalProtection,
    clearVirtualKeyboardDismissalProtection,
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
  assert.equal(
    isVirtualKeyboardDismissalActive(),
    false,
    "Initially dismissal protection is inactive"
  );

  markVirtualKeyboardDismissed();
  assert.equal(
    wasVirtualKeyboardJustDismissed(),
    true,
    "Immediately after markVirtualKeyboardDismissed(), wasVirtualKeyboardJustDismissed() must return true"
  );
  assert.equal(
    isVirtualKeyboardDismissalActive(),
    true,
    "Immediately after markVirtualKeyboardDismissed(), isVirtualKeyboardDismissalActive() must return true"
  );

  // Consume protection (one-shot)
  assert.equal(
    consumeVirtualKeyboardDismissalProtection(),
    true,
    "First consumption must return true (suppressing click)"
  );
  assert.equal(
    isVirtualKeyboardDismissalActive(),
    false,
    "After consumption, protection must be inactive"
  );
  assert.equal(
    consumeVirtualKeyboardDismissalProtection(),
    false,
    "Second consumption must return false (allowing next click)"
  );

  // Clear protection
  markVirtualKeyboardDismissed();
  assert.equal(isVirtualKeyboardDismissalActive(), true);
  clearVirtualKeyboardDismissalProtection();
  assert.equal(
    isVirtualKeyboardDismissalActive(),
    false,
    "clearVirtualKeyboardDismissalProtection must clear state immediately"
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
    /consumeVirtualKeyboardDismissalProtection/,
    "RichMathEditor must consume one-shot dismissal protection on backdrop click"
  );
  assert.match(
    rmeContent,
    /clearVirtualKeyboardDismissalProtection/,
    "RichMathEditor must clear dismissal protection when opening or closing popover"
  );
  assert.match(
    rmeContent,
    /isVirtualKeyboardVisible/,
    "RichMathEditor must check isVirtualKeyboardVisible() before dismissing popover"
  );
  assert.match(
    rmeContent,
    /handleBackdropMouseDown/,
    "RichMathEditor backdrop must bind handleBackdropMouseDown"
  );
  assert.match(
    rmeContent,
    /handleBackdropPointerDown/,
    "RichMathEditor backdrop must bind handleBackdropPointerDown"
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
    /consumeVirtualKeyboardDismissalProtection/,
    "InlineMathComposer must consume one-shot dismissal protection on backdrop click"
  );
  assert.match(
    composerContent,
    /clearVirtualKeyboardDismissalProtection/,
    "InlineMathComposer must clear dismissal protection when opening or closing modal"
  );
  assert.match(
    composerContent,
    /isVirtualKeyboardVisible/,
    "InlineMathComposer must check isVirtualKeyboardVisible() before closing modal"
  );
});

test("behavioral: one-shot dismissal consumption guarantees only 1 click is ignored and second click operates normally", async () => {
  const {
    markVirtualKeyboardDismissed,
    isVirtualKeyboardDismissalActive,
    consumeVirtualKeyboardDismissalProtection,
    resetVirtualKeyboardListenerForTests,
  } = await import("../src/utils/visualMathFieldLifecycle.ts");

  resetVirtualKeyboardListenerForTests();

  // Step 1: Virtual keyboard is dismissed
  markVirtualKeyboardDismissed();
  assert.equal(isVirtualKeyboardDismissalActive(), true, "Protection is active after dismissal");

  // Step 2: First click event sequence on backdrop
  const click1Suppressed = consumeVirtualKeyboardDismissalProtection();
  assert.equal(click1Suppressed, true, "Click #1 must be suppressed to preserve popover");
  assert.equal(isVirtualKeyboardDismissalActive(), false, "Protection must be consumed immediately");

  // Step 3: Second click event sequence on backdrop
  const click2Suppressed = consumeVirtualKeyboardDismissalProtection();
  assert.equal(click2Suppressed, false, "Click #2 must NOT be suppressed; popover must process click normally");
});

test("behavioral: RichMathEditor backdrop dismisses virtual keyboard on click 1 and commits/closes on click 2 without indefinite renewal", async () => {
  const {
    consumeVirtualKeyboardDismissalProtection,
    clearVirtualKeyboardDismissalProtection,
    resetVirtualKeyboardListenerForTests,
  } = await import("../src/utils/visualMathFieldLifecycle.ts");

  resetVirtualKeyboardListenerForTests();

  // Set up RichMathEditor simulation
  let virtualKeyboardVisible = true;
  let popoverOpen = true;
  let confirmedLatex: string | null = null;
  const isDismissingKeyboardRef = { current: false };

  const hideVirtualKeyboard = () => {
    virtualKeyboardVisible = false;
  };
  const isVirtualKeyboardVisible = () => virtualKeyboardVisible;

  const handleBackdropPointerDown = () => {
    if (isVirtualKeyboardVisible()) {
      isDismissingKeyboardRef.current = true;
      hideVirtualKeyboard();
    }
  };

  const handleBackdropMouseDown = () => {
    if (isDismissingKeyboardRef.current || isVirtualKeyboardVisible()) {
      isDismissingKeyboardRef.current = true;
    }
  };

  const handleBackdropClick = (dialogLatex: string) => {
    const wasDismissing =
      isDismissingKeyboardRef.current ||
      isVirtualKeyboardVisible() ||
      consumeVirtualKeyboardDismissalProtection();

    isDismissingKeyboardRef.current = false;
    clearVirtualKeyboardDismissalProtection();

    if (wasDismissing) {
      hideVirtualKeyboard();
      return;
    }

    if (dialogLatex.trim()) {
      confirmedLatex = dialogLatex.trim();
      popoverOpen = false;
      return;
    }

    popoverOpen = false;
  };

  // --- Click #1: User clicks backdrop while virtual keyboard is visible ---
  handleBackdropPointerDown();
  assert.equal(isDismissingKeyboardRef.current, true, "Pointerdown must set isDismissingKeyboardRef to true");
  assert.equal(virtualKeyboardVisible, false, "Pointerdown must hide virtual keyboard");

  handleBackdropMouseDown();
  assert.equal(isDismissingKeyboardRef.current, true, "Mousedown retains dismissal intent");

  handleBackdropClick("x^2 + 2x + 1");
  assert.equal(popoverOpen, true, "Click #1 must keep popover open");
  assert.equal(confirmedLatex, null, "Click #1 must not prematurely commit formula");
  assert.equal(isDismissingKeyboardRef.current, false, "Click #1 must clear isDismissingKeyboardRef");

  // --- Click #2: User clicks backdrop again to auto-commit formula ---
  handleBackdropPointerDown();
  assert.equal(isDismissingKeyboardRef.current, false, "Click #2 pointerdown must not set dismissal");

  handleBackdropMouseDown();
  assert.equal(isDismissingKeyboardRef.current, false, "Click #2 mousedown must not set dismissal");

  handleBackdropClick("x^2 + 2x + 1");
  assert.equal(popoverOpen, false, "Click #2 must close popover");
  assert.equal(confirmedLatex, "x^2 + 2x + 1", "Click #2 must commit valid formula");
});

test("behavioral: newly opened popover starts clean and is unaffected by prior virtual keyboard dismissal", async () => {
  const {
    markVirtualKeyboardDismissed,
    isVirtualKeyboardDismissalActive,
    clearVirtualKeyboardDismissalProtection,
    consumeVirtualKeyboardDismissalProtection,
  } = await import("../src/utils/visualMathFieldLifecycle.ts");

  // Scenario: Popover 1 had virtual keyboard dismissed
  markVirtualKeyboardDismissed();
  assert.equal(isVirtualKeyboardDismissalActive(), true, "Protection is active from Popover 1");

  // Popover 2 opens: handleOpen clears any existing dismissal protection and resets its local ref
  const isDismissingKeyboardRef = { current: false };
  clearVirtualKeyboardDismissalProtection();
  isDismissingKeyboardRef.current = false;

  assert.equal(
    isVirtualKeyboardDismissalActive(),
    false,
    "Popover 2 must start with clean dismissal state"
  );

  let popover2Closed = false;
  const handlePopover2BackdropClick = () => {
    const wasDismissing =
      isDismissingKeyboardRef.current ||
      consumeVirtualKeyboardDismissalProtection();

    isDismissingKeyboardRef.current = false;
    clearVirtualKeyboardDismissalProtection();

    if (wasDismissing) {
      return;
    }
    popover2Closed = true;
  };

  // Click #1 on Popover 2 backdrop without keyboard
  handlePopover2BackdropClick();
  assert.equal(
    popover2Closed,
    true,
    "Popover 2 must close immediately on Click #1, completely unhindered by prior Popover 1 state"
  );
});

test("behavioral: literal pipe symbol is preserved for set builder, probability, and divisibility expressions", async () => {
  const { DEFAULT_MATH_INLINE_SHORTCUTS, normalizeMathInsertContent } = await import(
    "../src/utils/visualMathFieldLifecycle.ts"
  );

  // 1. DEFAULT_MATH_INLINE_SHORTCUTS must NOT contain "|"
  assert.equal(
    (DEFAULT_MATH_INLINE_SHORTCUTS as Record<string, string>)["|"],
    undefined,
    "DEFAULT_MATH_INLINE_SHORTCUTS must not map '|' directly"
  );

  // 2. abs and norm remain available
  assert.equal(DEFAULT_MATH_INLINE_SHORTCUTS.abs, "\\left|#?\\right|");
  assert.equal(DEFAULT_MATH_INLINE_SHORTCUTS.norm, "\\left\\|#?\\right\\|");

  // 3. Mathematical expressions containing pipe symbol must keep literal pipe
  const mathExpressions = [
    "{x | x > 0}",      // Set-builder notation
    "P(A|B)",           // Conditional probability
    "a|b",              // Divisibility (a divides b)
    "{x \\in \\mathbb{R} | x \\ge 0}", // Real number set
  ];

  for (const expr of mathExpressions) {
    // MathLive inline shortcut application simulation:
    // If '|' is in shortcuts, it would expand to \\left|#?\\right| and corrupt the notation
    const expanded = expr.replace(
      /\|/g,
      (DEFAULT_MATH_INLINE_SHORTCUTS as Record<string, string>)["|"] || "|"
    );
    assert.equal(expanded, expr, `Expression '${expr}' must retain literal pipe without being converted to absolute value`);
  }

  // 4. Dedicated absolute value buttons or toolbar actions still normalize to placeholders
  assert.equal(
    normalizeMathInsertContent("|"),
    "\\left|#?\\right|",
    "Dedicated toolbar button inserting '|' must be normalized to interactive absolute value template"
  );
  assert.equal(
    normalizeMathInsertContent("abs"),
    "\\left|#?\\right|",
    "Dedicated toolbar button inserting 'abs' must be normalized to interactive absolute value template"
  );

  // 5. Virtual Keyboard symbols with selection arg #0 normalize to placeholder #? when collapsed
  assert.equal(
    normalizeMathInsertContent("\\sqrt{#0}"),
    "\\sqrt{#?}",
    "Virtual keyboard square root \\sqrt{#0} must be converted to \\sqrt{#?} placeholder"
  );
  assert.equal(
    normalizeMathInsertContent("\\sqrt[#0]{#1}"),
    "\\sqrt[#?]{#?}",
    "Virtual keyboard root-n \\sqrt[#0]{#1} must convert #0 and #1 to #? placeholders"
  );
  assert.equal(
    normalizeMathInsertContent("\\left\\vert#0\\right\\vert"),
    "\\left|#?\\right|",
    "Virtual keyboard absolute value \\left\\vert#0\\right\\vert must be normalized to \\left|#?\\right|"
  );
  assert.equal(
    normalizeMathInsertContent("\\left\\Vert#0\\right\\Vert"),
    "\\left\\|#?\\right\\|",
    "Virtual keyboard norm \\left\\Vert#0\\right\\Vert must be normalized to \\left\\|#?\\right\\|"
  );
  assert.equal(
    normalizeMathInsertContent("\\left|#0\\right|"),
    "\\left|#?\\right|",
    "Virtual keyboard absolute value \\left|#0\\right| must be normalized to \\left|#?\\right|"
  );
  assert.equal(
    normalizeMathInsertContent("\\left\\|#0\\right\\|"),
    "\\left\\|#?\\right\\|",
    "Virtual keyboard norm \\left\\|#0\\right\\| must be normalized to \\left\\|#?\\right\\|"
  );
  assert.equal(
    normalizeMathInsertContent("\\vert"),
    "\\left|#?\\right|",
    "Virtual keyboard single pipe symbol \\vert must be normalized to \\left|#?\\right|"
  );
  assert.equal(
    normalizeMathInsertContent("\\Vert"),
    "\\left\\|#?\\right\\|",
    "Virtual keyboard double pipe symbol \\Vert must be normalized to \\left\\|#?\\right\\|"
  );
});

test("behavioral: InlineMathComposer backdrop ignores click 1 when dismissing keyboard and closes on click 2", async () => {
  const {
    consumeVirtualKeyboardDismissalProtection,
    clearVirtualKeyboardDismissalProtection,
  } = await import("../src/utils/visualMathFieldLifecycle.ts");

  let modalOpen = true;
  let virtualKeyboardVisible = true;
  const isDismissingKeyboardRef = { current: false };

  const handleClose = () => {
    modalOpen = false;
  };

  const handleBackdropPointerDown = (targetIsCurrentTarget: boolean) => {
    if (!targetIsCurrentTarget) return;
    if (virtualKeyboardVisible) {
      isDismissingKeyboardRef.current = true;
      virtualKeyboardVisible = false;
    }
  };

  const handleBackdropMouseDown = (targetIsCurrentTarget: boolean) => {
    if (!targetIsCurrentTarget) return;
    if (isDismissingKeyboardRef.current || virtualKeyboardVisible) {
      isDismissingKeyboardRef.current = true;
    }
  };

  const handleBackdropClick = (targetIsCurrentTarget: boolean) => {
    if (!targetIsCurrentTarget) return;
    const wasDismissing =
      isDismissingKeyboardRef.current ||
      virtualKeyboardVisible ||
      consumeVirtualKeyboardDismissalProtection();

    isDismissingKeyboardRef.current = false;
    clearVirtualKeyboardDismissalProtection();

    if (wasDismissing) {
      return;
    }

    handleClose();
  };

  // Click #1: User clicks backdrop while virtual keyboard is visible
  handleBackdropPointerDown(true);
  handleBackdropMouseDown(true);
  handleBackdropClick(true);
  assert.equal(modalOpen, true, "Modal must remain open on Click #1 (which dismissed keyboard)");
  assert.equal(virtualKeyboardVisible, false, "Virtual keyboard must now be hidden");

  // Click #2: User clicks backdrop again
  handleBackdropPointerDown(true);
  handleBackdropMouseDown(true);
  handleBackdropClick(true);
  assert.equal(modalOpen, false, "Modal must close on Click #2");
});

test("normalizeMathInsertContent strictly preserves text, escaped hashes, and whitespace while converting math templates", async () => {
  const { normalizeMathInsertContent } = await import("../src/utils/visualMathFieldLifecycle.ts");

  // 1. Text preservation: \text{...} and plain text containing #1, #0, or escaped \# must NEVER be modified
  assert.equal(
    normalizeMathInsertContent("\\text{Mã \\#1}"),
    "\\text{Mã \\#1}",
    "\\text{Mã \\#1} must remain strictly unchanged"
  );
  assert.equal(
    normalizeMathInsertContent("\\text{Mã #1}"),
    "\\text{Mã #1}",
    "\\text{Mã #1} must remain strictly unchanged"
  );
  assert.equal(
    normalizeMathInsertContent("Mã #1"),
    "Mã #1",
    "Plain text 'Mã #1' must remain unchanged"
  );
  assert.equal(
    normalizeMathInsertContent(" x "),
    " x ",
    "Spaces in ' x ' must be preserved"
  );
  assert.equal(
    normalizeMathInsertContent("   "),
    "   ",
    "Whitespace-only strings must be preserved"
  );

  // 2. Mathematical templates: MathLive keyboard triggers convert to explicit placeholders (#?)
  assert.equal(
    normalizeMathInsertContent("\\sqrt{#0}", { isSelectionCollapsed: true }),
    "\\sqrt{#?}",
    "\\sqrt{#0} must convert to \\sqrt{#?} when selection is collapsed"
  );
  assert.equal(
    normalizeMathInsertContent("\\sqrt{#0}", { isSelectionCollapsed: false }),
    "\\sqrt{#0}",
    "\\sqrt{#0} must be preserved when text is selected for wrapping"
  );
  assert.equal(
    normalizeMathInsertContent("\\frac{#0}{#1}", { isSelectionCollapsed: true }),
    "\\frac{#?}{#?}",
    "\\frac{#0}{#1} must convert to \\frac{#?}{#?}"
  );
  assert.equal(
    normalizeMathInsertContent("\\left|#0\\right|", { isSelectionCollapsed: true }),
    "\\left|#?\\right|",
    "\\left|#0\\right| must convert to \\left|#?\\right|"
  );
  assert.equal(
    normalizeMathInsertContent("\\left\\Vert#0\\right\\Vert", { isSelectionCollapsed: true }),
    "\\left\\|#?\\right\\|",
    "\\left\\Vert#0\\right\\Vert must convert to \\left\\|#?\\right\\|"
  );
  assert.equal(
    normalizeMathInsertContent("^{#0}", { isSelectionCollapsed: true }),
    "^{#?}",
    "^{#0} must convert to ^{#?}"
  );
  assert.equal(
    normalizeMathInsertContent("_{#0}", { isSelectionCollapsed: true }),
    "_{#?}",
    "_{#0} must convert to _{#?}"
  );
  assert.equal(
    normalizeMathInsertContent("\\sqrt[#0]{#1}", { isSelectionCollapsed: true }),
    "\\sqrt[#?]{#?}",
    "\\sqrt[#0]{#1} must convert to \\sqrt[#?]{#?}"
  );
});

test("VisualMathField command wrapper preserves and merges original command options", async () => {
  const { installMathFieldInsertionGuards } = await import("../src/utils/visualMathFieldLifecycle.ts");
  // Test executeCommand wrapper logic with original command[2] options
  const executedCommands: any[] = [];
  const fakeMf: any = {
    selectionIsCollapsed: true,
    executeCommand: (cmd: any) => {
      executedCommands.push(cmd);
      return true;
    },
    insert: (s: string, opts?: any) => {
      executedCommands.push(["insert", s, opts]);
      return true;
    },
  };

  // Exercise the exact guard installed on production MathLive instances.
  installMathFieldInsertionGuards(fakeMf);

  // Scenario A: MathLive insert with existing options { mode: "math", focus: true }
  fakeMf.executeCommand(["insert", "\\sqrt{#0}", { mode: "math", focus: true }]);
  assert.equal(executedCommands.length, 1);
  assert.deepEqual(executedCommands[0], [
    "insert",
    "\\sqrt{#?}",
    { mode: "math", focus: true, selectionMode: "placeholder" },
  ]);

  // Scenario B: Plain text insertion preserves original options without adding selectionMode
  fakeMf.executeCommand(["insert", "\\text{Mã \\#1}", { mode: "text", silenceNotifications: true }]);
  assert.equal(executedCommands.length, 2);
  assert.deepEqual(executedCommands[1], [
    "insert",
    "\\text{Mã \\#1}",
    { mode: "text", silenceNotifications: true },
  ]);

  const originalOptions = { mode: "math", selectionMode: "after", focus: true };
  fakeMf.executeCommand(["insert", "\\sqrt{#0}", originalOptions, "retained-tail"]);
  assert.deepEqual(executedCommands[2], ["insert", "\\sqrt{#?}", originalOptions, "retained-tail"]);
  fakeMf.executeCommand("moveToNextChar");
  assert.equal(executedCommands[3], "moveToNextChar");
  fakeMf.insert(" ", { mode: "text" });
  assert.deepEqual(executedCommands[4], ["insert", " ", { mode: "text" }]);
  fakeMf.insert("|", { mode: "text" });
  assert.deepEqual(executedCommands[5], ["insert", "|", { mode: "text" }]);
});

test("toolbar insertion preserves selected content and only targets genuine empty placeholders", async () => {
  const { prepareMathFieldInsertion, installMathFieldInsertionGuards } = await import("../src/utils/visualMathFieldLifecycle.ts");
  const inserted: any[] = [];
  const mf = { selectionIsCollapsed: false, insert: (content: string, options?: any) => inserted.push({ content, options }) };
  installMathFieldInsertionGuards(mf);
  const options = { mode: "math", focus: true, silenceNotifications: true };
  const selected = prepareMathFieldInsertion(mf, "\\sqrt{#0}", options);
  mf.insert(selected.content, selected.options);
  assert.deepEqual(inserted[0], { content: "\\sqrt{#0}", options });
  mf.selectionIsCollapsed = true;
  const collapsed = prepareMathFieldInsertion(mf, "\\sqrt{#0}", options);
  mf.insert(collapsed.content, collapsed.options);
  assert.deepEqual(inserted[1], { content: "\\sqrt{#?}", options: { ...options, selectionMode: "placeholder" } });
  assert.deepEqual(prepareMathFieldInsertion(mf, "x + y", options), { content: "x + y", options });
});

test("isMathFieldSelectionCollapsed correctly reports collapsed vs non-collapsed selection state", async () => {
  const { isMathFieldSelectionCollapsed } = await import("../src/utils/visualMathFieldLifecycle.ts");

  assert.equal(isMathFieldSelectionCollapsed(null), true, "null mathfield defaults to collapsed");
  assert.equal(isMathFieldSelectionCollapsed(undefined), true, "undefined mathfield defaults to collapsed");
  assert.equal(isMathFieldSelectionCollapsed({}), true, "mathfield without selection properties defaults to true");

  // MathLive selectionIsCollapsed boolean property
  assert.equal(isMathFieldSelectionCollapsed({ selectionIsCollapsed: true }), true);
  assert.equal(isMathFieldSelectionCollapsed({ selectionIsCollapsed: false }), false);

  // MathLive selection ranges
  assert.equal(isMathFieldSelectionCollapsed({ selection: { ranges: [[3, 3]] } }), true);
  assert.equal(isMathFieldSelectionCollapsed({ selection: { ranges: [[3, 5]] } }), false);
  assert.equal(
    isMathFieldSelectionCollapsed({ selection: { ranges: [[1, 1], [4, 4]] } }),
    true
  );
  assert.equal(
    isMathFieldSelectionCollapsed({ selection: { ranges: [[1, 1], [4, 6]] } }),
    false
  );
});

test("normalizeMathInsertContent preserves #0 and does not force placeholder when selection is active", async () => {
  const { normalizeMathInsertContent } = await import("../src/utils/visualMathFieldLifecycle.ts");

  // When selection is active (collapsed === false), #0 must NOT be converted to #?
  assert.equal(
    normalizeMathInsertContent("\\sqrt{#0}", { isSelectionCollapsed: false }),
    "\\sqrt{#0}",
    "Radical with active selection must keep #0 so MathLive wraps selected content"
  );
  assert.equal(
    normalizeMathInsertContent("\\left|#0\\right|", { isSelectionCollapsed: false }),
    "\\left|#0\\right|",
    "Fences with active selection must keep #0"
  );

  // When selection is collapsed, #0 converts to #? for immediate typing into placeholder
  assert.equal(
    normalizeMathInsertContent("\\sqrt{#0}", { isSelectionCollapsed: true }),
    "\\sqrt{#?}",
    "Radical with collapsed selection converts #0 to #?"
  );
});
