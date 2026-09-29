import { describe, it } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import katex from "katex";
import {
  parseRichSegments,
  serializeRichSegments,
  hasUnfilledPlaceholder,
  validateAndCleanFormula,
  findIncompleteFormulasInText,
  buildAuthoritativeQuestionPayload,
} from "../src/pages/centerManagerQuestionEditorHelpers.ts";
import {
  serializeEditorDom,
} from "../src/components/math/richMathEditorHelpers.ts";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Minimal DOM mock helper for Node environment
function createMockElement(tagName = "div"): any {
  const children: any[] = [];
  const classList = new Set<string>();
  const dataset: Record<string, string> = {};

  return {
    nodeType: 1,
    tagName: tagName.toUpperCase(),
    childNodes: children,
    dataset,
    classList: {
      contains: (c: string) => classList.has(c),
      add: (c: string) => classList.add(c),
    },
    appendChild(child: any) {
      children.push(child);
      return child;
    },
    set innerHTML(_: string) {
      children.length = 0;
    },
    title: "",
    contentEditable: "true",
  };
}

function createMockTextNode(text: string): any {
  return {
    nodeType: 3,
    nodeValue: text,
  };
}

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

describe("Defensive Placeholder Validation for Raw LaTeX without Delimiters", () => {
  it("detects raw MathLive placeholder without $ delimiters", () => {
    const rawLatex = "\\frac{\\placeholder{}}{2}";
    assert.equal(hasUnfilledPlaceholder(rawLatex), true);

    const found = findIncompleteFormulasInText(rawLatex);
    assert.equal(found.length, 1);
    assert.equal(found[0], rawLatex);
  });

  it("buildAuthoritativeQuestionPayload blocks raw LaTeX correctAnswer containing placeholder", () => {
    const formData: any = {
      subjectId: "sub-1",
      primaryTopicNodeId: "node-1",
      questionType: "ShortAnswer",
      difficulty: 3,
      questionText: "Tính giá trị biểu thức.",
      maxScore: 10,
      estimatedTimeSeconds: 60,
      reasoningRequired: false,
      languageCode: "vi",
      answerEvaluationMode: "NumericRational",
    };

    const answerDrafts = {
      "ShortAnswer:NumericRational": {
        rawText: "\\frac{\\placeholder{}}{2}",
        displayLatex: "\\frac{\\placeholder{}}{2}",
      },
    };

    const result = buildAuthoritativeQuestionPayload({
      formData,
      modeDrafts: answerDrafts,
    });
    assert.ok(result.error);
    assert.ok(result.error.includes("Đáp án chuẩn chứa công thức chưa hoàn thành"));
  });

  it("buildAuthoritativeQuestionPayload blocks questionText containing raw placeholder without delimiters", () => {
    const formData: any = {
      subjectId: "sub-1",
      primaryTopicNodeId: "node-1",
      questionType: "ShortAnswer",
      difficulty: 3,
      questionText: "Cho biểu thức \\placeholder{} hãy tính",
      maxScore: 10,
      estimatedTimeSeconds: 60,
      reasoningRequired: false,
      languageCode: "vi",
      answerEvaluationMode: "TextExact",
    };

    const answerDrafts = {
      "ShortAnswer:TextExact": {
        rawText: "x = 1",
        displayLatex: "",
      },
    };

    const result = buildAuthoritativeQuestionPayload({
      formData,
      modeDrafts: answerDrafts,
    });
    assert.ok(result.error);
    assert.ok(result.error.includes("Nội dung câu hỏi chứa công thức chưa hoàn thành"));
  });
});

describe("DOM Serialization & Spacer (NBSP) Management", () => {
  it("serializeEditorDom normalizes non-breaking spaces into normal spaces", () => {
    const container = createMockElement("div");
    container.appendChild(createMockTextNode("Cho\u00A0hàm\u00A0số\u00A0"));

    const mathSpan = createMockElement("span");
    mathSpan.classList.add("inline-math-node");
    mathSpan.dataset.latex = "f(x)=x^2";
    container.appendChild(mathSpan);

    container.appendChild(createMockTextNode("\u00A0liên\u00A0tục."));

    const serialized = serializeEditorDom(container);
    assert.equal(serialized, "Cho hàm số $f(x)=x^2$ liên tục.");
    assert.ok(!serialized.includes("\u00A0"));
  });

  it("serializeEditorDom handles multiple paragraphs and line breaks cleanly", () => {
    const container = createMockElement("div");
    container.appendChild(createMockTextNode("Dòng 1: "));

    const math1 = createMockElement("span");
    math1.classList.add("inline-math-node");
    math1.dataset.latex = "x=1";
    container.appendChild(math1);

    container.appendChild(createMockElement("br"));
    container.appendChild(createMockTextNode("Dòng 2: "));

    const math2 = createMockElement("span");
    math2.classList.add("inline-math-node");
    math2.dataset.latex = "y=2";
    container.appendChild(math2);

    const serialized = serializeEditorDom(container);
    assert.equal(serialized, "Dòng 1: $x=1$\nDòng 2: $y=2$");
  });
});

describe("VisualMathField onCommit (Enter) and onCancel (Escape) Keyboard Isolation", () => {
  it("VisualMathField implementation intercepts Enter and Escape before stopPropagation", () => {
    const vmfPath = path.resolve(__dirname, "../src/components/math/VisualMathField.tsx");
    const content = fs.readFileSync(vmfPath, "utf-8");

    // Static code assertions confirming onCommit & onCancel hooks
    assert.ok(content.includes("onCommit?: () => void;"));
    assert.ok(content.includes("onCancel?: () => void;"));
    assert.ok(content.includes('e.key === "Enter" && !e.shiftKey && onCommitRef.current'));
    assert.ok(content.includes('e.key === "Escape" && onCancelRef.current'));
  });

  it("RichMathEditor uses anchored in-place popover without fullscreen modal backdrop", () => {
    const rmePath = path.resolve(__dirname, "../src/components/math/RichMathEditor.tsx");
    const content = fs.readFileSync(rmePath, "utf-8");

    // Verify fullscreen black blur modal is eliminated
    assert.ok(!content.includes("fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"));

    // Verify anchored popover positioning
    assert.ok(content.includes("popoverPos"));
    assert.ok(content.includes("getBoundingClientRect()"));
    assert.ok(content.includes("onCommit={handleConfirmMath}"));
    assert.ok(content.includes("onCancel={handleCancelMath}"));
  });
});

describe("Cross-Actor Unification (Center Manager, Teacher, Student)", () => {
  it("TeacherQuestionEditorView uses RichMathEditor and does not use MathInputToolbar", () => {
    const teacherPath = path.resolve(__dirname, "../src/pages/teacher/TeacherQuestionEditorView.tsx");
    const content = fs.readFileSync(teacherPath, "utf-8");

    assert.ok(content.includes('import { RichMathEditor } from "../../components/math/RichMathEditor";'));
    assert.ok(!content.includes('import { MathInputToolbar } from "../../components/math/MathInputToolbar";'));
    assert.ok(!content.includes("<MathInputToolbar"));
    assert.ok(content.includes("<RichMathEditor"));
  });

  it("LearningPlayerPage does not render legacy MathInputToolbar for students", () => {
    const studentPath = path.resolve(__dirname, "../src/pages/LearningPlayerPage.tsx");
    const content = fs.readFileSync(studentPath, "utf-8");

    assert.ok(!content.includes('import { MathInputToolbar } from "../components/math/MathInputToolbar";'));
    assert.ok(!content.includes("<MathInputToolbar"));
    assert.ok(!content.includes("Bảng gõ ký hiệu Toán"));
  });

  it("TeacherQuestionEditorView uses ModeAwareAnswerEditor instead of plain text input", () => {
    const teacherPath = path.resolve(__dirname, "../src/pages/teacher/TeacherQuestionEditorView.tsx");
    const content = fs.readFileSync(teacherPath, "utf-8");

    assert.ok(content.includes('import { ModeAwareAnswerEditor } from "../../components/math/answer-editor/ModeAwareAnswerEditor";'));
    assert.ok(content.includes("<ModeAwareAnswerEditor"));
    assert.ok(!content.includes('<input\n                type="text"\n                value={correctAnswer}'));
    assert.ok(!content.includes('type="text"\n                value={correctAnswer}'));
    assert.ok(content.includes('variant="teacher"'));
  });
});

describe("KaTeX Real Syntax Validation (throwOnError: true)", () => {
  it("rejects incomplete syntax such as unclosed braces \\frac{1}{", () => {
    const invalid = "\\frac{1}{";
    const result = validateAndCleanFormula(invalid);

    assert.equal(result.isComplete, false);
    assert.equal(result.hasPlaceholder, false);
    assert.ok(result.error);
    assert.ok(result.error.toLowerCase().includes("cú pháp") || result.error.includes("KaTeX"));
  });

  it("rejects incomplete root \\sqrt{", () => {
    const invalid = "\\sqrt{";
    const result = validateAndCleanFormula(invalid);

    assert.equal(result.isComplete, false);
    assert.ok(result.error);
  });

  it("accepts valid LaTeX expressions with isComplete: true", () => {
    const valid = "\\frac{1}{2} + \\sqrt{3}";
    const result = validateAndCleanFormula(valid);

    assert.equal(result.isComplete, true);
    assert.equal(result.cleanLatex, "\\frac{1}{2} + \\sqrt{3}");
    assert.equal(result.error, undefined);
  });

  it("findIncompleteFormulasInText flags syntax-broken formulas in text flow", () => {
    const text = "Biểu thức $\\frac{1}{$ bị lỗi.";
    const incomplete = findIncompleteFormulasInText(text);

    assert.equal(incomplete.length, 1);
    assert.equal(incomplete[0], "$\\frac{1}{$");
  });
});

describe("Contenteditable singleLine Enforcement & Placeholder Architecture", () => {
  it("serializeEditorDom with singleLine=true strips newlines into single spaces", () => {
    const container = createMockElement("div");
    container.appendChild(createMockTextNode("Dòng 1\n"));
    container.appendChild(createMockElement("br"));
    container.appendChild(createMockTextNode("Dòng 2"));

    const serializedSingle = serializeEditorDom(container, true);
    assert.ok(!serializedSingle.includes("\n"));
    assert.equal(serializedSingle, "Dòng 1 Dòng 2");

    const serializedMulti = serializeEditorDom(container, false);
    assert.ok(serializedMulti.includes("\n"));
  });

  it("RichMathEditor defines actor-aware CSS tokens for Teacher and Center Manager", () => {
    const rmePath = path.resolve(__dirname, "../src/components/math/RichMathEditor.tsx");
    const content = fs.readFileSync(rmePath, "utf-8");

    assert.ok(content.includes('variant === "teacher"'));
    assert.ok(content.includes('"--rme-border"'));
    assert.ok(content.includes('"--rme-surface"'));
    assert.ok(content.includes('"--rme-text"'));
    assert.ok(content.includes('"--rme-math-bg"'));
    assert.ok(content.includes('"--rme-math-text"'));
    assert.ok(content.includes('checkEmpty'));
    assert.ok(content.includes('data-empty'));
  });

  it("index.css defines empty placeholder and actor-aware inline math pill styling", () => {
    const cssPath = path.resolve(__dirname, "../src/index.css");
    const content = fs.readFileSync(cssPath, "utf-8");

    assert.ok(content.includes(".rich-math-content-editable:empty::before"));
    assert.ok(content.includes(".rich-math-content-editable[data-empty=\"true\"]::before"));
    assert.ok(content.includes("content: attr(data-placeholder);"));
    assert.ok(content.includes(".inline-math-node"));
    assert.ok(content.includes("var(--rme-math-bg"));
  });
});
