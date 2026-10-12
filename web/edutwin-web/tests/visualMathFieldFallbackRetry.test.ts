import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import {
  hydrateMathFieldInstance,
  shouldSyncExternalValue,
  type MathFieldSyncTarget,
} from "../src/utils/visualMathFieldLifecycle.ts";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

function createMockMathField(): MathFieldSyncTarget & {
  attributes: Record<string, string>;
  focusCalled: boolean;
  valueSets: string[];
} {
  const attributes: Record<string, string> = {};
  const valueSets: string[] = [];

  return {
    readOnly: false,
    value: "",
    tabIndex: 0,
    attributes,
    focusCalled: false,
    valueSets,
    setAttribute(name: string, val: string) {
      attributes[name] = val;
    },
    removeAttribute(name: string) {
      delete attributes[name];
    },
    setValue(val: string) {
      this.value = val;
      valueSets.push(val);
    },
    getValue() {
      return this.value;
    },
    focus() {
      this.focusCalled = true;
    },
  };
}

test("VisualMathField: Static code assertion ensures latest refs are used instead of stale initial refs", () => {
  const componentPath = path.resolve(
    __dirname,
    "../src/components/math/VisualMathField.tsx"
  );
  const componentContent = fs.readFileSync(componentPath, "utf-8");

  // Verify that stale initial refs were eliminated
  assert.doesNotMatch(
    componentContent,
    /initialValueRef/,
    "Component MUST NOT use initialValueRef which causes stale state overwrites on retry"
  );
  assert.doesNotMatch(
    componentContent,
    /initialDisabledRef/,
    "Component MUST NOT use initialDisabledRef which causes stale disabled state"
  );
  assert.doesNotMatch(
    componentContent,
    /initialAutoFocusRef/,
    "Component MUST NOT use initialAutoFocusRef"
  );

  // Verify latest refs are maintained
  assert.match(
    componentContent,
    /const latestValueRef = useRef\(value\);/,
    "Component must maintain latestValueRef"
  );
  assert.match(
    componentContent,
    /latestValueRef\.current = value;/,
    "latestValueRef must be updated on every render"
  );
  assert.match(
    componentContent,
    /const latestDisabledRef = useRef\(disabled\);/,
    "Component must maintain latestDisabledRef"
  );
  assert.match(
    componentContent,
    /latestDisabledRef\.current = disabled;/,
    "latestDisabledRef must be updated on every render"
  );
});

test("VisualMathField Regression: Fallback typing followed by successful retry preserves user content without loss", () => {
  // Step 1: Initial state before chunk load
  let parentStateValue = "";
  const parentStateDisabled = false;
  const initialValueAtMount = "";

  // Step 2: MathLive chunk fails to load -> user is presented with fallback textarea
  const fallbackTypedAnswer = "x = \\frac{-b \\pm \\sqrt{b^2 - 4ac}}{2a}";

  // User types into fallback textarea -> updates parent state & latestValueRef
  parentStateValue = fallbackTypedAnswer;
  const latestValueRef = { current: parentStateValue };
  const latestDisabledRef = { current: parentStateDisabled };

  // Step 3: User clicks 'Thử tải lại' -> retry succeeds and MathLive mounts
  const mockMf = createMockMathField();

  // If the old code ran, it would pass initialValueAtMount (""), wiping the user's input!
  // In the fixed code, hydrateMathFieldInstance uses latestValueRef.current:
  const hydrationResult = hydrateMathFieldInstance(mockMf, {
    latestValue: latestValueRef.current,
    latestDisabled: latestDisabledRef.current,
    latestAutoFocus: false,
  });

  assert.equal(
    mockMf.value,
    fallbackTypedAnswer,
    "MathLive instance must be hydrated with the answer typed in fallback textarea"
  );
  assert.equal(
    hydrationResult.hydratedValue,
    fallbackTypedAnswer,
    "Hydration result must match the user's typed fallback answer"
  );
  assert.notEqual(
    mockMf.value,
    initialValueAtMount,
    "MathLive must NOT revert to the initial empty/stale value"
  );
});

test("VisualMathField Regression: Disabled state toggled while chunk is loading/retrying is properly enforced", () => {
  // Scenario: Quiz time limit expires while MathLive chunk is downloading or retrying
  const initialDisabled = false;
  let currentDisabled = initialDisabled;

  // Time limit expires before chunk finishes loading
  currentDisabled = true;
  const latestDisabledRef = { current: currentDisabled };
  const latestValueRef = { current: "3x + 12 = 45" };

  const mockMf = createMockMathField();

  // MathLive finally finishes loading and mounts
  hydrateMathFieldInstance(mockMf, {
    latestValue: latestValueRef.current,
    latestDisabled: latestDisabledRef.current,
    latestAutoFocus: false,
  });

  assert.equal(mockMf.readOnly, true, "MathLive readOnly must reflect the latest disabled state");
  assert.equal(mockMf.attributes["readonly"], "true", "readonly attribute must be set");
  assert.equal(mockMf.attributes["disabled"], "true", "disabled attribute must be set");
  assert.equal(mockMf.tabIndex, -1, "tabIndex must be -1 when disabled");
});

test("VisualMathField: shouldSyncExternalValue correctly identifies real external updates vs local echoes", () => {
  // Echo from user typing: propValue matches lastEmittedValue -> do NOT sync
  assert.equal(
    shouldSyncExternalValue("x^2", "x^2", "x^2"),
    false,
    "Same value across prop, field, and last emitted should not trigger sync"
  );
  assert.equal(
    shouldSyncExternalValue("x^2 + 1", "x^2", "x^2 + 1"),
    false,
    "Prop matches lastEmittedValue (echo from onChange) -> should not overwrite"
  );

  // External reset (e.g. 'Xóa hết' button or question change in quiz)
  assert.equal(
    shouldSyncExternalValue("", "x^2 + 5", "x^2 + 5"),
    true,
    "External reset to empty string must trigger sync"
  );
  assert.equal(
    shouldSyncExternalValue("new_question_draft", "old_question_answer", "old_question_answer"),
    true,
    "External question switch must trigger sync"
  );
});
