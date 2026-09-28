import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import {
  resolveAnswerInputType,
  buildTextExactAnswer,
  buildProseAnswer,
  buildNumericRationalAnswer,
  serializeCoordinateParts,
  serializeCoordinate,
  deserializeCoordinate,
  deserializeCoordinateWithLatex,
  updateCoordinateAxis,
  shouldSyncCoordinateExternalValue,
  buildSubmissionAnswerFields,
  shouldSyncExternalValue,
  resolveReadonlyDisplay,
  getAnswerSyntaxHint,
  type QuestionType,
  type QuestionAnswerEvaluationMode,
} from "../src/components/math/answer-editor/answerEditorHelpers.ts";
import {
  hydrateMathFieldInstance,
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

test("Gate 2A.1 - Compatibility Matrix: All 6 valid combinations resolve to expected component types", () => {
  const validCases: Array<{
    qType: QuestionType;
    evalMode: QuestionAnswerEvaluationMode;
    expectedType: string;
  }> = [
    { qType: "MultipleChoice", evalMode: "TextExact", expectedType: "option-selector" },
    { qType: "ShortAnswer", evalMode: "TextExact", expectedType: "plain-text" },
    { qType: "ShortAnswer", evalMode: "NumericRational", expectedType: "numeric-rational" },
    { qType: "ShortAnswer", evalMode: "Coordinate2D", expectedType: "coordinate-2d" },
    { qType: "ShortAnswer", evalMode: "Manual", expectedType: "manual-short-answer" },
    { qType: "Essay", evalMode: "Manual", expectedType: "essay-prose" },
  ];

  for (const { qType, evalMode, expectedType } of validCases) {
    const res = resolveAnswerInputType(qType, evalMode);
    assert.strictEqual(
      res.isValid,
      true,
      `Expected [${qType} + ${evalMode}] to be valid, but got: ${res.errorMessage}`
    );
    assert.strictEqual(
      res.type,
      expectedType,
      `Expected [${qType} + ${evalMode}] to resolve to '${expectedType}', got '${res.type}'`
    );
  }
});

test("Gate 2A.1 - Compatibility Matrix: All invalid combinations fail closed without silent fallback", () => {
  const invalidCases: Array<{
    qType: string;
    evalMode: string;
  }> = [
    // MultipleChoice with invalid modes
    { qType: "MultipleChoice", evalMode: "NumericRational" },
    { qType: "MultipleChoice", evalMode: "Coordinate2D" },
    { qType: "MultipleChoice", evalMode: "Manual" },
    { qType: "MultipleChoice", evalMode: "RandomMode" },

    // ShortAnswer with invalid modes
    { qType: "ShortAnswer", evalMode: "UnsupportedMode" },
    { qType: "ShortAnswer", evalMode: "" },

    // Essay with invalid modes
    { qType: "Essay", evalMode: "TextExact" },
    { qType: "Essay", evalMode: "NumericRational" },
    { qType: "Essay", evalMode: "Coordinate2D" },
    { qType: "Essay", evalMode: "Other" },

    // Unknown question types
    { qType: "FillInTheBlanks", evalMode: "TextExact" },
    { qType: "Matching", evalMode: "TextExact" },
    { qType: "Code", evalMode: "Manual" },
  ];

  for (const { qType, evalMode } of invalidCases) {
    const res = resolveAnswerInputType(qType, evalMode);
    assert.strictEqual(
      res.isValid,
      false,
      `Expected invalid combination [${qType} + ${evalMode}] to fail closed`
    );
    assert.strictEqual(
      res.type,
      "unsupported",
      `Expected invalid combination to have type 'unsupported'`
    );
    assert.ok(
      typeof res.errorMessage === "string" && res.errorMessage.length > 0,
      `Expected non-empty error message for [${qType} + ${evalMode}]`
    );
  }
});

test("Gate 2A.1 - Production Helper: MultipleChoice serialization invariance", () => {
  // Uses buildSubmissionAnswerFields production helper
  const submission = buildSubmissionAnswerFields("MultipleChoice", "TextExact", { optionId: "opt-mcq-a" });
  assert.strictEqual(submission.finalAnswer, "opt-mcq-a");
  assert.strictEqual(submission.answerDisplayLatex, "");

  const resolution = resolveAnswerInputType("MultipleChoice", "TextExact");
  assert.strictEqual(resolution.type, "option-selector");
  assert.strictEqual(resolution.isValid, true);
});

test("Gate 2A.1 - Production Helper: TextExact and Essay strictly emit displayLatex = ''", () => {
  // TextExact contract
  const textExactVal = buildTextExactAnswer("Hà Nội");
  assert.strictEqual(textExactVal.rawText, "Hà Nội");
  assert.strictEqual(textExactVal.displayLatex, "", "TextExact displayLatex MUST be empty");

  const textExactSubmission = buildSubmissionAnswerFields("ShortAnswer", "TextExact", textExactVal);
  assert.strictEqual(textExactSubmission.finalAnswer, "Hà Nội");
  assert.strictEqual(textExactSubmission.answerDisplayLatex, "", "TextExact answerDisplayLatex MUST be empty");

  // Essay + Manual contract
  const essayText = "Gọi $x$ là số tự nhiên cần tìm. Theo đề ta có: $x^2 - 4 = 0$.";
  const essayVal = buildProseAnswer(essayText);
  assert.strictEqual(essayVal.rawText, essayText);
  assert.strictEqual(essayVal.displayLatex, "", "Essay displayLatex MUST be empty");

  const essaySubmission = buildSubmissionAnswerFields("Essay", "Manual", essayVal);
  assert.strictEqual(essaySubmission.finalAnswer, essayText, "finalAnswer MUST hold main essay prose");
  assert.strictEqual(essaySubmission.answerDisplayLatex, "", "Essay answerDisplayLatex MUST be empty");

  // ShortAnswer + Manual contract
  const manualVal = buildProseAnswer("x = 2 và y = 3");
  assert.strictEqual(manualVal.displayLatex, "", "ShortAnswer Manual displayLatex MUST be empty");
  const manualSubmission = buildSubmissionAnswerFields("ShortAnswer", "Manual", manualVal);
  assert.strictEqual(manualSubmission.finalAnswer, "x = 2 và y = 3");
  assert.strictEqual(manualSubmission.answerDisplayLatex, "");
});

test("Gate 2A.1 - Production Helper: NumericRational preserves distinct plain rawText and displayLatex", () => {
  const numericVal = buildNumericRationalAnswer("3/4", "\\frac{3}{4}");
  assert.strictEqual(numericVal.rawText, "3/4");
  assert.strictEqual(numericVal.displayLatex, "\\frac{3}{4}");

  const submission = buildSubmissionAnswerFields("ShortAnswer", "NumericRational", numericVal);
  assert.strictEqual(submission.finalAnswer, "3/4", "finalAnswer must hold deterministic raw plainText");
  assert.strictEqual(submission.answerDisplayLatex, "\\frac{3}{4}", "answerDisplayLatex must hold visual LaTeX");
});

test("Gate 2A.1 - Production Helper: Coordinate2D preserves distinct plain and LaTeX for each axis", () => {
  const xAxis = { plainText: "1/2", latex: "\\frac{1}{2}" };
  const yAxis = { plainText: "-3", latex: "-3" };

  const coordVal = serializeCoordinateParts(xAxis, yAxis);

  // Serialized rawText strictly semicolon
  assert.strictEqual(coordVal.rawText, "(1/2; -3)");
  // Serialized displayLatex contains LaTeX thin space
  assert.strictEqual(coordVal.displayLatex, "\\left(\\frac{1}{2};\\,-3\\right)");

  const submission = buildSubmissionAnswerFields("ShortAnswer", "Coordinate2D", coordVal);
  assert.strictEqual(submission.finalAnswer, "(1/2; -3)");
  assert.strictEqual(submission.answerDisplayLatex, "\\left(\\frac{1}{2};\\,-3\\right)");

  // Legacy wrapper compatibility
  const legacyVal = serializeCoordinate("1.5", "2.5");
  assert.strictEqual(legacyVal.rawText, "(1.5; 2.5)");
  assert.strictEqual(legacyVal.displayLatex, "\\left(1.5;\\,2.5\\right)");
});

test("Gate 2A.1 - Production Helper: Coordinate2D deserialization handles semicolon, legacy comma, and LaTeX", () => {
  // Semicolon
  assert.deepStrictEqual(deserializeCoordinate("(1; 2)"), { x: "1", y: "2" });
  assert.deepStrictEqual(deserializeCoordinate("(-3.5; 4/7)"), { x: "-3.5", y: "4/7" });

  // Legacy comma
  assert.deepStrictEqual(deserializeCoordinate("(1, 2)"), { x: "1", y: "2" });
  assert.deepStrictEqual(deserializeCoordinate("(3.5, 4.5)"), { x: "3.5", y: "4.5" });

  // Deserializing with separate LaTeX
  const parsedWithLatex = deserializeCoordinateWithLatex(
    "(1/2; -3)",
    "\\left(\\frac{1}{2};\\,-3\\right)"
  );
  assert.deepStrictEqual(parsedWithLatex.x, { plainText: "1/2", latex: "\\frac{1}{2}" });
  assert.deepStrictEqual(parsedWithLatex.y, { plainText: "-3", latex: "-3" });

  // Reserialization of legacy comma input converts to semicolon ';'
  const legacy = deserializeCoordinate("(1, 2)");
  const migrated = serializeCoordinate(legacy.x, legacy.y);
  assert.strictEqual(migrated.rawText, "(1; 2)", "Reserialized coordinate must strictly use semicolon delimiter");
});

test("Gate 2A.1 - Production Helper: Coordinate2D updateCoordinateAxis and shouldSyncCoordinateExternalValue", () => {
  const currentCoords = {
    x: { plainText: "1", latex: "1" },
    y: { plainText: "2", latex: "2" },
  };

  // Update X axis
  const updateX = updateCoordinateAxis(currentCoords, "x", { plainText: "3/4", latex: "\\frac{3}{4}" });
  assert.strictEqual(updateX.nextCoords.x.plainText, "3/4");
  assert.strictEqual(updateX.nextCoords.x.latex, "\\frac{3}{4}");
  assert.strictEqual(updateX.nextCoords.y.plainText, "2");
  assert.strictEqual(updateX.nextValue.rawText, "(3/4; 2)");
  assert.strictEqual(updateX.nextValue.displayLatex, "\\left(\\frac{3}{4};\\,2\\right)");

  // Update Y axis
  const updateY = updateCoordinateAxis(currentCoords, "y", { plainText: "-5", latex: "-5" });
  assert.strictEqual(updateY.nextCoords.x.plainText, "1");
  assert.strictEqual(updateY.nextCoords.y.plainText, "-5");
  assert.strictEqual(updateY.nextValue.rawText, "(1; -5)");
  assert.strictEqual(updateY.nextValue.displayLatex, "\\left(1;\\,-5\\right)");

  // shouldSyncCoordinateExternalValue lifecycle checks
  // 1. Initial hydration from undefined/null
  assert.strictEqual(
    shouldSyncCoordinateExternalValue(
      { rawText: "(1; 2)", displayLatex: "\\left(1;\\,2\\right)" },
      null
    ),
    true
  );

  // 2. Same rawText and displayLatex -> no sync
  assert.strictEqual(
    shouldSyncCoordinateExternalValue(
      { rawText: "(1; 2)", displayLatex: "\\left(1;\\,2\\right)" },
      { rawText: "(1; 2)", displayLatex: "\\left(1;\\,2\\right)" }
    ),
    false
  );

  // 3. rawText changed -> sync
  assert.strictEqual(
    shouldSyncCoordinateExternalValue(
      { rawText: "(3; 4)", displayLatex: "\\left(1;\\,2\\right)" },
      { rawText: "(1; 2)", displayLatex: "\\left(1;\\,2\\right)" }
    ),
    true
  );

  // 4. displayLatex changed even if rawText identical -> sync
  assert.strictEqual(
    shouldSyncCoordinateExternalValue(
      { rawText: "(1; 2)", displayLatex: "\\left(1;\\,2.0\\right)" },
      { rawText: "(1; 2)", displayLatex: "\\left(1;\\,2\\right)" }
    ),
    true
  );
});

test("Gate 2A.1 - Readonly value selection: Resolves formula vs rich display accurately", () => {
  // NumericRational with displayLatex -> formula mode
  const numDisplay = resolveReadonlyDisplay("NumericRational", {
    rawText: "3/4",
    displayLatex: "\\frac{3}{4}",
  });
  assert.strictEqual(numDisplay.mode, "formula");
  assert.strictEqual(numDisplay.content, "\\frac{3}{4}");

  // Coordinate2D with displayLatex -> formula mode
  const coordDisplay = resolveReadonlyDisplay("Coordinate2D", {
    rawText: "(1; 2)",
    displayLatex: "\\left(1;\\,2\\right)",
  });
  assert.strictEqual(coordDisplay.mode, "formula");
  assert.strictEqual(coordDisplay.content, "\\left(1;\\,2\\right)");

  // TextExact -> rich text mode
  const textDisplay = resolveReadonlyDisplay("TextExact", {
    rawText: "Paris",
    displayLatex: "",
  });
  assert.strictEqual(textDisplay.mode, "rich");
  assert.strictEqual(textDisplay.content, "Paris");

  // Essay / Manual -> rich text mode using rawText
  const essayDisplay = resolveReadonlyDisplay("Manual", {
    rawText: "Xét hàm số $f(x) = x^2$",
    displayLatex: "",
  });
  assert.strictEqual(essayDisplay.mode, "rich");
  assert.strictEqual(essayDisplay.content, "Xét hàm số $f(x) = x^2$");

  // MultipleChoice -> fail-closed returns empty content and never reveals optionId (Contract b)
  const mcqDisplay = resolveReadonlyDisplay(
    "TextExact",
    {
      rawText: "b9c0d3e4-f5a6-4b2c-8d1e-9a8b7c6d5e4f",
      displayLatex: "",
    },
    "MultipleChoice"
  );
  assert.strictEqual(
    mcqDisplay.content,
    "",
    "MultipleChoice readonly must fail closed and never expose internal optionId"
  );
});

test("Gate 2A.1 - Anti-echo Lifecycle: shouldSyncExternalValue prevents echo loops", () => {
  // Initial hydration from external prop
  assert.strictEqual(shouldSyncExternalValue("3/4", "", ""), true);

  // Echo of last emitted value from child component
  assert.strictEqual(shouldSyncExternalValue("3/4", "3/4", "3/4"), false);

  // External update from question switch or reset
  assert.strictEqual(shouldSyncExternalValue("5/8", "3/4", "3/4"), true);

  // External clear
  assert.strictEqual(shouldSyncExternalValue("", "3/4", "3/4"), true);
});

test("Gate 2A.1 - Fallback Preservation: hydrateMathFieldInstance restores user input on retry", () => {
  const mf = createMockMathField();

  const fallbackTypedValue = "\\frac{7}{12}";
  const options = {
    latestValue: fallbackTypedValue,
    latestDisabled: false,
    latestAutoFocus: false,
  };

  const result = hydrateMathFieldInstance(mf, options);
  assert.strictEqual(result.hydratedValue, fallbackTypedValue);
  assert.strictEqual(mf.value, fallbackTypedValue);
  assert.strictEqual(mf.readOnly, false);
});

test("Gate 2A.1 - Syntax hints: Non-authoritative hints assist user without blocking input", () => {
  // Letters inside NumericRational
  const hintLetters = getAnswerSyntaxHint("NumericRational", {
    rawText: "abc",
    displayLatex: "abc",
  });
  assert.ok(hintLetters !== null && hintLetters.includes("Số hữu tỉ"));

  // Valid fraction in NumericRational
  const hintValid = getAnswerSyntaxHint("NumericRational", {
    rawText: "3/4",
    displayLatex: "\\frac{3}{4}",
  });
  assert.strictEqual(hintValid, null);

  // Incomplete coordinate
  const hintCoordIncomplete = getAnswerSyntaxHint("Coordinate2D", {
    rawText: "(1; )",
    displayLatex: "",
  });
  assert.ok(hintCoordIncomplete !== null && hintCoordIncomplete.includes("Tọa độ 2D"));

  // Complete coordinate
  const hintCoordComplete = getAnswerSyntaxHint("Coordinate2D", {
    rawText: "(1; 2)",
    displayLatex: "\\left(1;\\,2\\right)",
  });
  assert.strictEqual(hintCoordComplete, null);
});

test("Gate 2A.1 - Source Wiring Verification: Production components strictly adhere to contracts", () => {
  // 1. PlainTextAnswerInput uses buildTextExactAnswer
  const plainTextSrc = fs.readFileSync(
    path.resolve(__dirname, "../src/components/math/answer-editor/PlainTextAnswerInput.tsx"),
    "utf-8"
  );
  assert.match(plainTextSrc, /buildTextExactAnswer/, "PlainTextAnswerInput must use buildTextExactAnswer");
  assert.doesNotMatch(plainTextSrc, /displayLatex:\s*next/, "PlainTextAnswerInput must NOT pass next as displayLatex");
  assert.match(plainTextSrc, /onFocus=\{onFocus\}/, "PlainTextAnswerInput must wire onFocus");

  // 2. MultilineProseAnswerEditor uses buildProseAnswer
  const proseSrc = fs.readFileSync(
    path.resolve(__dirname, "../src/components/math/answer-editor/MultilineProseAnswerEditor.tsx"),
    "utf-8"
  );
  assert.match(proseSrc, /buildProseAnswer/, "MultilineProseAnswerEditor must use buildProseAnswer");
  assert.doesNotMatch(proseSrc, /displayLatex:\s*next/, "MultilineProseAnswerEditor must NOT pass next as displayLatex");
  assert.match(proseSrc, /onFocus=\{onFocus\}/, "MultilineProseAnswerEditor must wire onFocus");

  // 3. PlainOrMultilineAnswerInput uses buildProseAnswer
  const manualSrc = fs.readFileSync(
    path.resolve(__dirname, "../src/components/math/answer-editor/PlainOrMultilineAnswerInput.tsx"),
    "utf-8"
  );
  assert.match(manualSrc, /buildProseAnswer/, "PlainOrMultilineAnswerInput must use buildProseAnswer");
  assert.match(manualSrc, /onFocus=\{onFocus\}/, "PlainOrMultilineAnswerInput must wire onFocus");

  // 4. NumericRationalMathInput wires latestValueRef for fresh getValue()
  const numSrc = fs.readFileSync(
    path.resolve(__dirname, "../src/components/math/answer-editor/NumericRationalMathInput.tsx"),
    "utf-8"
  );
  assert.match(numSrc, /latestValueRef\.current = nextVal;/, "NumericRationalMathInput must update latestValueRef in onChange");
  assert.match(numSrc, /return latestValueRef\.current;/, "NumericRationalMathInput getValue must return latestValueRef.current");

  // 5. Coordinate2DInput uses two VisualMathField instances without state updater side effects
  const coordSrc = fs.readFileSync(
    path.resolve(__dirname, "../src/components/math/answer-editor/Coordinate2DInput.tsx"),
    "utf-8"
  );
  assert.match(coordSrc, /<VisualMathField[\s\S]*ref=\{xRef\}/, "Coordinate2DInput must render VisualMathField for X");
  assert.match(coordSrc, /<VisualMathField[\s\S]*ref=\{yRef\}/, "Coordinate2DInput must render VisualMathField for Y");
  assert.match(coordSrc, /updateCoordinateAxis/, "Coordinate2DInput must use updateCoordinateAxis");
  assert.match(coordSrc, /shouldSyncCoordinateExternalValue/, "Coordinate2DInput must use shouldSyncCoordinateExternalValue");
  assert.match(coordSrc, /isInternalClearingRef\.current = true/, "Coordinate2DInput must guard clear() to avoid intermediate emissions");
  assert.doesNotMatch(coordSrc, /setCoords\([^)]*=>[\s\S]*onChange/, "Coordinate2DInput must NOT call onChange inside setCoords updater");

  // 6. ModeAwareAnswerEditor wires onFocus to all child editors and handles MCQ readonly via delegation
  const orchestratorSrc = fs.readFileSync(
    path.resolve(__dirname, "../src/components/math/answer-editor/ModeAwareAnswerEditor.tsx"),
    "utf-8"
  );
  assert.match(orchestratorSrc, /<PlainTextAnswerInput[\s\S]*onFocus=\{onFocus\}/);
  assert.match(orchestratorSrc, /<NumericRationalMathInput[\s\S]*onFocus=\{onFocus\}/);
  assert.match(orchestratorSrc, /<Coordinate2DInput[\s\S]*onFocus=\{onFocus\}/);
  assert.match(orchestratorSrc, /<PlainOrMultilineAnswerInput[\s\S]*onFocus=\{onFocus\}/);
  assert.match(orchestratorSrc, /<MultilineProseAnswerEditor[\s\S]*onFocus=\{onFocus\}/);
  assert.match(orchestratorSrc, /resolveReadonlyDisplay\(evaluationMode, value, questionType\)/, "ModeAwareAnswerEditor must pass questionType to resolveReadonlyDisplay");

  // 7. VisualMathField uses MathFallbackTextarea and wires onFocus
  const fallbackSrc = fs.readFileSync(
    path.resolve(__dirname, "../src/components/math/answer-editor/MathFallbackTextarea.tsx"),
    "utf-8"
  );
  assert.match(fallbackSrc, /onFocus\?: \(\) => void;/, "MathFallbackTextareaProps must define onFocus");
  assert.match(fallbackSrc, /<textarea[\s\S]*onFocus=\{onFocus\}/, "MathFallbackTextarea must bind onFocus to textarea");

  const vmfSrc = fs.readFileSync(
    path.resolve(__dirname, "../src/components/math/VisualMathField.tsx"),
    "utf-8"
  );
  assert.match(vmfSrc, /import \{ MathFallbackTextarea \} from "\.\/answer-editor\/MathFallbackTextarea";/);
  assert.match(vmfSrc, /<MathFallbackTextarea[\s\S]*onFocus=\{\(\) => onFocusRef\.current\?\.\(\)\}/, "VisualMathField must forward onFocus to MathFallbackTextarea");

  // 8. MathPreviewCore imports renderSafeKatex from mathPreviewUtils
  const previewCoreSrc = fs.readFileSync(
    path.resolve(__dirname, "../src/components/math/MathPreviewCore.tsx"),
    "utf-8"
  );
  assert.match(previewCoreSrc, /import \{ renderSafeKatex \} from "\.\/mathPreviewUtils";/);
  assert.doesNotMatch(previewCoreSrc, /export function renderSafeKatex/, "MathPreviewCore must not export renderSafeKatex to avoid fast refresh warnings");
});
