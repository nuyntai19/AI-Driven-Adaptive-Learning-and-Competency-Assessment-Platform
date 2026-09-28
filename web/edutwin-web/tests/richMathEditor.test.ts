import { describe, it } from "node:test";
import assert from "node:assert/strict";
import katex from "katex";
import {
  parseRichSegments,
  serializeRichSegments,
  hasUnfilledPlaceholder,
  validateAndCleanFormula,
  findIncompleteFormulasInText,
} from "../src/pages/centerManagerQuestionEditorHelpers.ts";

describe("WYSIWYG RichMathEditor Contract & Serialization", () => {
  it("parses mixed Vietnamese prose and KaTeX formulas into separate nodes", () => {
    const raw = "Cho hàm số $y=f(x)$ liên tục trên đoạn $[0; 1]$ và $\\int_0^1 f(x)dx = 2$.";
    const segments = parseRichSegments(raw);

    assert.equal(segments.length, 7);
    assert.equal(segments[0].type, "text");
    assert.equal(segments[0].content, "Cho hàm số ");
    assert.equal(segments[1].type, "math");
    assert.equal(segments[1].content, "y=f(x)");
    assert.equal(segments[2].type, "text");
    assert.equal(segments[2].content, " liên tục trên đoạn ");
    assert.equal(segments[3].type, "math");
    assert.equal(segments[3].content, "[0; 1]");
    assert.equal(segments[4].type, "text");
    assert.equal(segments[4].content, " và ");
    assert.equal(segments[5].type, "math");
    assert.equal(segments[5].content, "\\int_0^1 f(x)dx = 2");
    assert.equal(segments[6].type, "text");
    assert.equal(segments[6].content, ".");
  });

  it("KaTeX renders inline math nodes into .katex elements without exposing raw delimiters", () => {
    const latex = "\\frac{a}{b} + \\sqrt{c}";
    const html = katex.renderToString(latex, {
      throwOnError: false,
      displayMode: false,
      output: "htmlAndMathml",
    });

    assert.ok(html.includes("class=\"katex\""));
    assert.ok(!html.includes("$"));
    assert.ok(!html.includes("\\placeholder"));
  });

  it("detects and flags unfilled MathLive placeholder templates in real-time", () => {
    const incompleteIntegral = "\\int_{\\placeholder{}}^{\\placeholder{}} \\placeholder{f(x)} dx";
    assert.equal(hasUnfilledPlaceholder(incompleteIntegral), true);

    const validation = validateAndCleanFormula(incompleteIntegral);
    assert.equal(validation.isComplete, false);
    assert.equal(validation.hasPlaceholder, true);
    assert.ok(validation.error?.includes("\\placeholder"));
  });

  it("detects and blocks saving when incomplete formulas exist in question prose", () => {
    const textWithIncomplete = "Tính giá trị của biểu thức $A = \\frac{\\placeholder{1}}{2} + \\sqrt{4}$.";
    const found = findIncompleteFormulasInText(textWithIncomplete);

    assert.equal(found.length, 1);
    assert.ok(found[0].includes("\\placeholder{1}"));
  });

  it("preserves exact round-trip serialization between visual nodes and storage format", () => {
    const original = "Cho một tam giác vuông $ABC$ vuông tại $A$ có cạnh $AB = 3$ và $AC = 4$. Tính $BC$.";
    const segments = parseRichSegments(original);
    const serialized = serializeRichSegments(segments);

    assert.equal(serialized, original);
  });
});
