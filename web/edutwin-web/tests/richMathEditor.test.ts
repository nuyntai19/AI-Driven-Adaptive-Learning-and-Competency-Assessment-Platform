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
  validateTextMathFormulas,
  formatFormulaDiagnosticMessage,
  buildAuthoritativeQuestionPayload,
} from "../src/pages/centerManagerQuestionEditorHelpers.ts";
import {
  serializeEditorDom,
  resolveRichMathPopoverTheme,
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

  it("Legacy MathInputToolbar.tsx is completely removed from the codebase", () => {
    const toolbarPath = path.resolve(__dirname, "../src/components/math/MathInputToolbar.tsx");
    assert.equal(fs.existsSync(toolbarPath), false, "MathInputToolbar.tsx must be completely removed");
  });

  it("LearningPlayerPage uses RichMathEditor for student reasoning and ModeAwareAnswerEditor for answer dispatching", () => {
    const studentPath = path.resolve(__dirname, "../src/pages/LearningPlayerPage.tsx");
    const content = fs.readFileSync(studentPath, "utf-8");

    assert.ok(content.includes('import { RichMathEditor, type RichMathEditorRef } from "../components/math/RichMathEditor";'));
    assert.ok(content.includes('import { ModeAwareAnswerEditor } from "../components/math/answer-editor/ModeAwareAnswerEditor";'));
    assert.ok(content.includes('variant="student"'));
    assert.ok(content.includes('profile="answering"'));
    assert.ok(content.includes('showPreview={false}'));
    assert.ok(content.includes('showSyntaxHint={false}'));
    assert.ok(!content.includes("KaTeX Preview"));
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

    assert.ok(content.includes('case "teacher":') || content.includes('variant === "teacher"'));
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

  it("RichMathEditor defines dedicated theme tokens for all 4 variants", () => {
    const rmePath = path.resolve(__dirname, "../src/components/math/RichMathEditor.tsx");
    const content = fs.readFileSync(rmePath, "utf-8");

    assert.ok(content.includes('case "teacher":'));
    assert.ok(content.includes('case "student":'));
    assert.ok(content.includes('case "neutral":'));
    assert.ok(content.includes('case "center-manager":'));
    assert.ok(content.includes('--student-brand'));
    assert.ok(content.includes('--th-teal'));
  });
});

describe("Structured Math Formula Diagnostics & State Machine Scanner", () => {
  it("detects unclosed inline dollar delimiter in 'Cho $\\frac{1}{' and 'Cho $\\sqrt{'", () => {
    const input1 = "Cho $\\frac{1}{";
    const diags1 = validateTextMathFormulas(input1);

    assert.equal(diags1.length, 1);
    assert.equal(diags1[0].type, "unclosed-delimiter");
    assert.equal(diags1[0].raw, "$\\frac{1}{");
    assert.ok(diags1[0].message.includes('chưa được đóng dấu "$"'));

    const input2 = "Cho $\\sqrt{";
    const diags2 = validateTextMathFormulas(input2);
    assert.equal(diags2.length, 1);
    assert.equal(diags2[0].type, "unclosed-delimiter");
    assert.equal(diags2[0].raw, "$\\sqrt{");

    const formatted = formatFormulaDiagnosticMessage("Nội dung câu hỏi", diags1[0]);
    assert.ok(formatted.includes('chưa được đóng dấu "$"'));
  });

  it("detects unclosed display double-dollar delimiter in 'Cho $$\\sqrt{x}'", () => {
    const input = "Cho $$\\sqrt{x}";
    const diags = validateTextMathFormulas(input);

    assert.equal(diags.length, 1);
    assert.equal(diags[0].type, "unclosed-delimiter");
    assert.equal(diags[0].raw, "$$\\sqrt{x}");
    assert.ok(diags[0].message.includes('chưa được đóng dấu "$$"'));
  });

  it("does not treat escaped dollar \\$ as formula delimiter", () => {
    const input = "Giá 5\\$ cho học sinh.";
    const diags = validateTextMathFormulas(input);

    assert.equal(diags.length, 0);

    const inputWithMath = "Giá 5\\$ cho bài toán có $x = 1$.";
    const diagsWithMath = validateTextMathFormulas(inputWithMath);
    assert.equal(diagsWithMath.length, 0);
  });

  it("detects empty formulas for both '$ $' and '$$$$'", () => {
    const inputSpace = "Công thức $ $ bị rỗng.";
    const diagsSpace = validateTextMathFormulas(inputSpace);
    assert.equal(diagsSpace.length, 1);
    assert.equal(diagsSpace[0].type, "empty-formula");
    assert.ok(diagsSpace[0].message.includes("không được để trống"));

    const inputEmptyDisplay = "Công thức $$$$ bị rỗng.";
    const diagsEmptyDisplay = validateTextMathFormulas(inputEmptyDisplay);
    assert.equal(diagsEmptyDisplay.length, 1);
    assert.equal(diagsEmptyDisplay[0].type, "empty-formula");
  });

  it("identifies error in multiple formulas when one formula is invalid", () => {
    // Case 1: unclosed delimiter among multiple formulas
    const inputUnclosed = "Cho $x = 1$ và $\\frac{1}{";
    const diagsUnclosed = validateTextMathFormulas(inputUnclosed);
    assert.ok(diagsUnclosed.length >= 1);
    assert.equal(diagsUnclosed[0].type, "unclosed-delimiter");

    // Case 2: syntax error among multiple closed formulas
    const inputSyntax = "Cho $x = 1$, $\\frac{1}{2$, và $y = 2$";
    const diagsSyntax = validateTextMathFormulas(inputSyntax);
    assert.ok(diagsSyntax.length >= 1);
    assert.equal(diagsSyntax[0].type, "invalid-syntax");
    assert.equal(diagsSyntax[0].raw, "$\\frac{1}{2$");
    assert.ok(diagsSyntax[0].message.includes("sai cú pháp LaTeX"));
  });

  it("classifies placeholder separately from syntax and unclosed delimiters", () => {
    const placeholderInput = "Tính giá trị $A = \\placeholder{x} + 1$.";
    const diags = validateTextMathFormulas(placeholderInput);

    assert.equal(diags.length, 1);
    assert.equal(diags[0].type, "placeholder");
    assert.ok(diags[0].message.includes("\\placeholder"));

    const formatted = formatFormulaDiagnosticMessage("Đáp án chuẩn", diags[0]);
    assert.ok(formatted.includes("còn ô trống \\placeholder"));
  });

  it("detects placeholder in prose outside math delimiters (with or without $)", () => {
    // Case 1: text with formula and trailing placeholder
    const case1 = "Tính $x+1$ rồi điền \\placeholder{}";
    const diags1 = validateTextMathFormulas(case1);
    assert.equal(diags1.length, 1);
    assert.equal(diags1[0].type, "placeholder");

    // Case 2: text with escaped dollar and placeholder
    const case2 = "Giá 5\\$ rồi điền \\placeholder{}";
    const diags2 = validateTextMathFormulas(case2);
    assert.equal(diags2.length, 1);
    assert.equal(diags2[0].type, "placeholder");

    // Case 3: leading placeholder before formula
    const case3 = "Văn bản \\placeholder{} và $x$";
    const diags3 = validateTextMathFormulas(case3);
    assert.equal(diags3.length, 1);
    assert.equal(diags3[0].type, "placeholder");

    // Case 4: placeholder between two valid formulas
    const case4 = "$x$ \\placeholder{} $y$";
    const diags4 = validateTextMathFormulas(case4);
    assert.equal(diags4.length, 1);
    assert.equal(diags4[0].type, "placeholder");
  });

  it("ensures placeholder inside formula generates exactly one diagnostic without duplicates", () => {
    const input = "Cho $x = \\placeholder{}$ nhé.";
    const diags = validateTextMathFormulas(input);
    assert.equal(diags.length, 1);
    assert.equal(diags[0].type, "placeholder");
    assert.equal(diags[0].raw, "$x = \\placeholder{}$");
  });

  it("detects raw LaTeX syntax error outside delimiter even when string contains valid formulas", () => {
    const input = "\\frac{1}{ và $x$";
    const diags = validateTextMathFormulas(input);
    assert.ok(diags.length >= 1);
    assert.equal(diags[0].type, "invalid-syntax");
    assert.ok(diags[0].message.includes("sai cú pháp LaTeX"));
  });

  it("buildAuthoritativeQuestionPayload blocks payloads with prose placeholders outside delimiters", () => {
    const formDataProsePlaceholder: any = {
      subjectId: "sub-1",
      primaryTopicNodeId: "node-1",
      questionType: "ShortAnswer",
      difficulty: 3,
      questionText: "Tính $x+1$ rồi điền \\placeholder{}",
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
      formData: formDataProsePlaceholder,
      modeDrafts: answerDrafts,
    });

    assert.ok(result.error);
    assert.ok(result.error.includes("Nội dung câu hỏi chứa công thức chưa hoàn thành (còn ô trống \\placeholder)"));
  });

  it("buildAuthoritativeQuestionPayload reports exact diagnostic reason in Vietnamese", () => {
    const formDataUnclosed: any = {
      subjectId: "sub-1",
      primaryTopicNodeId: "node-1",
      questionType: "ShortAnswer",
      difficulty: 3,
      questionText: "Cho $\\frac{1}{",
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
      formData: formDataUnclosed,
      modeDrafts: answerDrafts,
    });

    assert.ok(result.error);
    assert.ok(result.error.includes('chưa được đóng dấu "$"'));
    assert.ok(!result.error.includes("\\placeholder"));
  });

  it("fails-closed on valid raw LaTeX commands outside delimiters with 'unwrapped-latex' diagnostic", () => {
    const rawCases = [
      { input: "Cho \\frac{1}{2}", expectedRaw: "\\frac{1}{2}" },
      { input: "\\frac{1}{2} và $x$", expectedRaw: "\\frac{1}{2}" },
      { input: "Văn bản \\sqrt{x} hợp lệ", expectedRaw: "\\sqrt{x}" },
      { input: "\\alpha và $x$", expectedRaw: "\\alpha" },
    ];

    for (const { input, expectedRaw } of rawCases) {
      const diags = validateTextMathFormulas(input);
      assert.ok(diags.length >= 1, `Expected diagnostics for '${input}'`);
      const unwrappedDiag = diags.find((d) => d.type === "unwrapped-latex");
      assert.ok(unwrappedDiag, `Expected 'unwrapped-latex' diagnostic for '${input}', got: ${JSON.stringify(diags)}`);
      assert.equal(unwrappedDiag.raw, expectedRaw);
      assert.equal(
        unwrappedDiag.message,
        "Công thức LaTeX phải được chèn bằng trình soạn công thức hoặc đặt trong $...$."
      );

      const formatted = formatFormulaDiagnosticMessage("Nội dung câu hỏi", unwrappedDiag);
      assert.ok(
        formatted.includes("Công thức LaTeX phải được chèn bằng trình soạn công thức hoặc đặt trong $...$."),
        `Formatted message mismatch for '${input}': ${formatted}`
      );
    }
  });

  it("buildAuthoritativeQuestionPayload blocks payloads with unwrapped LaTeX in questionText, solution, or options", () => {
    // 1. Unwrapped LaTeX in questionText
    const baseFormData: any = {
      subjectId: "sub-1",
      primaryTopicNodeId: "node-1",
      questionType: "ShortAnswer",
      difficulty: 3,
      questionText: "Cho \\frac{1}{2}",
      maxScore: 10,
      estimatedTimeSeconds: 60,
      reasoningRequired: false,
      languageCode: "vi",
      answerEvaluationMode: "TextExact",
    };
    const answerDrafts = {
      "ShortAnswer:TextExact": {
        rawText: "1/2",
        displayLatex: "",
      },
    };

    const resQuestionText = buildAuthoritativeQuestionPayload({
      formData: baseFormData,
      modeDrafts: answerDrafts,
    });
    assert.ok(resQuestionText.error);
    assert.ok(
      resQuestionText.error.includes("Công thức LaTeX phải được chèn bằng trình soạn công thức hoặc đặt trong $...$.")
    );

    // 2. Unwrapped LaTeX in solution
    const resSolution = buildAuthoritativeQuestionPayload({
      formData: {
        ...baseFormData,
        questionText: "Tính $x+1$",
        solution: "Văn bản \\sqrt{x} hợp lệ",
      },
      modeDrafts: answerDrafts,
    });
    assert.ok(resSolution.error);
    assert.ok(
      resSolution.error.includes("Lời giải: Công thức LaTeX phải được chèn bằng trình soạn công thức hoặc đặt trong $...$.")
    );

    // 3. Unwrapped LaTeX in options
    const resOption = buildAuthoritativeQuestionPayload({
      formData: {
        ...baseFormData,
        questionType: "MultipleChoice",
        questionText: "Chọn đáp án đúng",
        options: [
          { optionLabel: "A", optionText: "\\alpha và $x$", isCorrect: true, orderIndex: 0 },
          { optionLabel: "B", optionText: "$y$", isCorrect: false, orderIndex: 1 },
        ],
      },
      modeDrafts: answerDrafts,
    });
    assert.ok(resOption.error);
    assert.ok(
      resOption.error.includes("Phương án A: Công thức LaTeX phải được chèn bằng trình soạn công thức hoặc đặt trong $...$.")
    );
  });
});

describe("RichMath Popover Theme Resolution (resolveRichMathPopoverTheme pure function)", () => {
  it("resolves Center Manager light mode with isDark: false and cyan accent styling", () => {
    const theme = resolveRichMathPopoverTheme("center-manager", false);
    assert.equal(theme.isDark, false);
    assert.ok(theme.popoverBorder.includes("cyan"));
    assert.ok(theme.badgeBg.includes("cyan"));
    assert.ok(theme.inputBorder.includes("cyan"));
    assert.ok(theme.previewBox.includes("cyan"));
    assert.ok(theme.previewFormulaColor.includes("cyan"));
    assert.ok(theme.confirmBtn.includes("bg-cyan-600"));
  });

  it("resolves Center Manager dark mode with isDark: true and cyan accent styling", () => {
    const theme = resolveRichMathPopoverTheme("center-manager", true);
    assert.equal(theme.isDark, true);
    assert.ok(theme.popoverBorder.includes("cyan"));
    assert.ok(theme.badgeBg.includes("cyan"));
    assert.ok(theme.inputBorder.includes("cyan"));
    assert.ok(theme.previewFormulaColor.includes("cyan"));
    assert.ok(theme.confirmBtn.includes("bg-cyan-500"));
  });

  it("resolves Teacher variants accurately in both light and dark modes", () => {
    const lightTheme = resolveRichMathPopoverTheme("teacher", false);
    assert.equal(lightTheme.isDark, false);
    assert.ok(lightTheme.popoverBorder.includes("teal"));
    assert.ok(lightTheme.badgeBg.includes("teal"));
    assert.ok(lightTheme.confirmBtn.includes("bg-teal-600"));

    const darkTheme = resolveRichMathPopoverTheme("teacher", true);
    assert.equal(darkTheme.isDark, true);
    assert.ok(darkTheme.popoverBorder.includes("teal"));
    assert.ok(darkTheme.badgeBg.includes("teal"));
    assert.ok(darkTheme.confirmBtn.includes("bg-teal-500"));
  });

  it("resolves Student variants accurately in both light and dark modes", () => {
    const lightTheme = resolveRichMathPopoverTheme("student", false);
    assert.equal(lightTheme.isDark, false);
    assert.ok(lightTheme.popoverBorder.includes("#6746E8"));
    assert.ok(lightTheme.confirmBtn.includes("#6746E8"));

    const darkTheme = resolveRichMathPopoverTheme("student", true);
    assert.equal(darkTheme.isDark, true);
    assert.ok(darkTheme.popoverBorder.includes("#6746E8"));
    assert.ok(darkTheme.confirmBtn.includes("#6746E8"));
  });

  it("resolves Neutral variants accurately in both light and dark modes", () => {
    const lightTheme = resolveRichMathPopoverTheme("neutral", false);
    assert.equal(lightTheme.isDark, false);
    assert.ok(lightTheme.popoverBorder.includes("slate-300"));
    assert.ok(lightTheme.confirmBtn.includes("indigo-600"));

    const darkTheme = resolveRichMathPopoverTheme("neutral", true);
    assert.equal(darkTheme.isDark, true);
    assert.ok(darkTheme.popoverBorder.includes("slate-700"));
    assert.ok(darkTheme.confirmBtn.includes("indigo-600"));
  });

  it("defaults to Center Manager when variant is omitted or unrecognized", () => {
    const defaultLight = resolveRichMathPopoverTheme(undefined as any, false);
    assert.equal(defaultLight.isDark, false);
    assert.ok(defaultLight.popoverBorder.includes("cyan"));

    const defaultDark = resolveRichMathPopoverTheme(undefined as any, true);
    assert.equal(defaultDark.isDark, true);
    assert.ok(defaultDark.popoverBorder.includes("cyan"));
  });
});

describe("RichMathEditor Caret Preservation and External Insertion", () => {
  const richMathEditorPath = path.resolve(__dirname, "../src/components/math/RichMathEditor.tsx");
  const source = fs.readFileSync(richMathEditorPath, "utf-8");

  it("defines lastValidRangeRef and tracks selectionchange to preserve valid caret within editor", () => {
    assert.match(source, /lastValidRangeRef\s*=\s*useRef<Range \| null>\(null\)/);
    assert.match(source, /document\.addEventListener\("selectionchange",\s*handleSelectionChange\)/);
    assert.match(source, /container\.contains\(range\.startContainer\)\s*&&\s*container\.contains\(range\.endContainer\)/);
    assert.match(source, /lastValidRangeRef\.current\s*=\s*range\.cloneRange\(\)/);
  });

  it("safely resets lastValidRangeRef to null on external value hydration and unmount", () => {
    // On hydration:
    assert.match(source, /if\s*\(isLocalChangeRef\.current\)\s*\{[\s\S]*?\}\s*lastValidRangeRef\.current\s*=\s*null;/);
    // On unmount:
    assert.match(source, /return\s*\(\)\s*=>\s*\{[\s\S]*?lastValidRangeRef\.current\s*=\s*null;\s*\};/);
  });

  it("calls getEffectiveTargetRange BEFORE container.focus to prevent selection reset upon focus stealing", () => {
    // In handleInsertMathAtCursor:
    assert.match(
      source,
      /handleInsertMathAtCursor\s*=\s*useCallback\(\(\)\s*=>\s*\{[\s\S]*?const targetRange = getEffectiveTargetRange\(container\);[\s\S]*?targetRange\.deleteContents\(\);/
    );
    // In insertLatexAtCursor:
    assert.match(
      source,
      /insertLatexAtCursor\s*=\s*useCallback\([\s\S]*?const targetRange = getEffectiveTargetRange\(container\);[\s\S]*?targetRange\.deleteContents\(\);/
    );
    // In insertTextAtCursor:
    assert.match(
      source,
      /insertTextAtCursor\s*=\s*useCallback\([\s\S]*?const targetRange = getEffectiveTargetRange\(container\);[\s\S]*?targetRange\.deleteContents\(\);/
    );
    // Ensure container.focus() is NOT called before getEffectiveTargetRange in any of these functions
    assert.doesNotMatch(
      source,
      /container\.focus\(\);\s*const targetRange = getEffectiveTargetRange\(container\);/
    );
  });

  it("simulates DOM contentEditable caret: preserves range when focus is lost and inserts node in middle", () => {
    // Emulate DOM container and nodes
    const container = createMockElement("div");
    const part1 = createMockTextNode("Cho hàm số ");
    const part2 = createMockTextNode(" đồng biến trên R.");
    container.appendChild(part1);
    container.appendChild(part2);

    // Initial state: Caret placed between part1 and part2
    let savedRange: any = {
      startContainer: container,
      startOffset: 1,
      endContainer: container,
      endOffset: 1,
      isCloned: true,
      deleteContents() {},
      insertNode(node: any) {
        // Insert node at child index 1
        container.childNodes.splice(1, 0, node);
      },
      cloneRange() {
        return { ...this };
      },
    };

    // User clicks external Casio / toolbar button:
    // Window selection is cleared or points outside container
    const activeWindowSelection: any = null;

    // Implementation of getEffectiveTargetRange:
    function getEffectiveTargetRange(cont: any) {
      if (
        activeWindowSelection &&
        activeWindowSelection.rangeCount > 0 &&
        cont.childNodes.includes(activeWindowSelection.anchorNode)
      ) {
        return activeWindowSelection.getRangeAt(0);
      }
      if (savedRange) {
        return savedRange.cloneRange();
      }
      return null;
    }

    // Step 1: Range is retrieved BEFORE any container.focus()
    const targetRange = getEffectiveTargetRange(container);
    assert.ok(targetRange);
    assert.equal(targetRange.startOffset, 1);

    // Step 2: Create math span
    const mathSpan = createMockElement("span");
    mathSpan.classList.add("inline-math-node");
    mathSpan.dataset.latex = "y=f(x)";

    // Step 3: Insert into targetRange
    targetRange.deleteContents();
    targetRange.insertNode(mathSpan);

    // Step 4: After range placed after mathSpan (at index 2)
    const afterRange = {
      startContainer: container,
      startOffset: 2,
      collapse: true,
    };
    savedRange = { ...afterRange, cloneRange: () => ({ ...afterRange }) };

    // Step 5: Serialize container DOM
    const serialized = serializeEditorDom(container as any);
    assert.equal(serialized, "Cho hàm số $y=f(x)$ đồng biến trên R.");
    // Verify it was inserted in the middle, NOT appended at the end
    assert.notEqual(serialized, "Cho hàm số  đồng biến trên R.$y=f(x)$");
  });

  it("restricts Source Mode: hidden for Student actor, read-only with copy button for Teacher and Manager", () => {
    // 1. Student hides source button:
    assert.match(source, /variant\s*!==\s*"student"\s*&&\s*\(\s*<button/);
    // 2. Teacher/Manager displays 'Xem mã':
    assert.match(source, /viewMode\s*===\s*"visual"\s*\?\s*"📄 Xem mã"\s*:\s*"👁️ Trực quan"/);
    // 3. Textarea is read-only with no mutating onChange:
    assert.match(source, /<textarea[^>]*?readOnly/);
    assert.doesNotMatch(source, /<textarea[^>]*?onChange/);
    // 4. Includes copy button and 'Chế độ xem mã (Chỉ đọc)':
    assert.match(source, /Chế độ xem mã \(Chỉ đọc\)/);
    assert.match(source, /handleCopySource/);
    assert.match(source, /Sao chép/);
  });
});
