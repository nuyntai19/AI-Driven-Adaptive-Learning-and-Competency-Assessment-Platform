import { describe, it } from "node:test";
import assert from "node:assert/strict";
import {
  getAnswerDraftKey,
  syncMcqCorrectAnswer,
  cleanFormulaForInsertion,
  validateAndCleanFormula,
  hasUnfilledPlaceholder,
  findIncompleteFormulasInText,
  parseRichSegments,
  serializeRichSegments,
  insertFormulaAtCursor,
  hydrateAnswerEditorValue,
  serializeAnswerEditorValue,
  resetAndHydrateDraftStore,
  buildAuthoritativeQuestionPayload,
  type AnswerDraftKey,
  type DraftStore,
} from "../src/pages/centerManagerQuestionEditorHelpers.ts";
import type { CreateQuestionRequest } from "../src/types/questions.ts";

describe("CenterManager Question Editor Helpers & Integration (Gate 2A.2)", () => {
  describe("Draft Key Resolution (getAnswerDraftKey)", () => {
    it("resolves all valid combinations for ShortAnswer", () => {
      assert.equal(getAnswerDraftKey("ShortAnswer", "TextExact"), "ShortAnswer:TextExact");
      assert.equal(getAnswerDraftKey("ShortAnswer", "NumericRational"), "ShortAnswer:NumericRational");
      assert.equal(getAnswerDraftKey("ShortAnswer", "Coordinate2D"), "ShortAnswer:Coordinate2D");
      assert.equal(getAnswerDraftKey("ShortAnswer", "Manual"), "ShortAnswer:Manual");
    });

    it("resolves Essay:Manual for Essay question type", () => {
      assert.equal(getAnswerDraftKey("Essay", "Manual"), "Essay:Manual");
    });

    it("returns null for MultipleChoice or invalid combinations (fail-closed)", () => {
      assert.equal(getAnswerDraftKey("MultipleChoice", "TextExact"), null);
      assert.equal(getAnswerDraftKey("MultipleChoice", "Manual"), null);
      assert.equal(getAnswerDraftKey("Essay", "NumericRational"), null);
      assert.equal(getAnswerDraftKey("Essay", "Coordinate2D"), null);
      assert.equal(getAnswerDraftKey("Essay", "TextExact"), null);
      assert.equal(getAnswerDraftKey("ShortAnswer", "UnsupportedMode" as any), null);
    });
  });

  describe("Draft Store Isolation: ShortAnswer:Manual ↔ Essay:Manual", () => {
    it("preserves independent drafts without cross-contaminating short answer and essay rubrics", () => {
      const store: DraftStore = {};

      // 1. Author writes short answer manual text
      const shortAnswerVal = hydrateAnswerEditorValue("Hà Nội", "Manual");
      store["ShortAnswer:Manual"] = shortAnswerVal;

      // 2. Author writes essay scoring rubric
      const essayRubricVal = hydrateAnswerEditorValue(
        "1. Mở bài: Giới thiệu định lý Thales (1.0đ)\n2. Thân bài: Chứng minh tỉ lệ thức (2.0đ)",
        "Manual"
      );
      store["Essay:Manual"] = essayRubricVal;

      // 3. Verify drafts are strictly separated by key
      assert.equal(store["ShortAnswer:Manual"]?.rawText, "Hà Nội");
      assert.equal(
        store["Essay:Manual"]?.rawText,
        "1. Mở bài: Giới thiệu định lý Thales (1.0đ)\n2. Thân bài: Chứng minh tỉ lệ thức (2.0đ)"
      );

      // Switching questionType from ShortAnswer to Essay retrieves Essay:Manual
      const retrievedEssayKey = getAnswerDraftKey("Essay", "Manual") as AnswerDraftKey;
      assert.equal(
        store[retrievedEssayKey]?.rawText,
        "1. Mở bài: Giới thiệu định lý Thales (1.0đ)\n2. Thân bài: Chứng minh tỉ lệ thức (2.0đ)"
      );

      // Switching back to ShortAnswer retrieves ShortAnswer:Manual
      const retrievedShortKey = getAnswerDraftKey("ShortAnswer", "Manual") as AnswerDraftKey;
      assert.equal(store[retrievedShortKey]?.rawText, "Hà Nội");
    });
  });

  describe("Draft Store Lifecycle (resetAndHydrateDraftStore)", () => {
    it("resets previous question drafts when loading a new question (Question A -> Question B)", () => {
      // Question A is an Essay question with a detailed rubric
      const questionA = {
        questionType: "Essay" as const,
        answerEvaluationMode: "Manual" as const,
        correctAnswer: "Rubric của câu A: các luận điểm cần đạt...",
      };
      const storeA = resetAndHydrateDraftStore(questionA, true);
      assert.equal(storeA["Essay:Manual"]?.rawText, "Rubric của câu A: các luận điểm cần đạt...");
      assert.equal(storeA["ShortAnswer:Manual"], undefined);

      // User navigates to Question B (ShortAnswer, NumericRational)
      const questionB = {
        questionType: "ShortAnswer" as const,
        answerEvaluationMode: "NumericRational" as const,
        correctAnswer: "3/4",
      };
      const storeB = resetAndHydrateDraftStore(questionB, true);

      // Verify Question A's Essay rubric is completely gone from Question B's store
      assert.equal(storeB["Essay:Manual"], undefined);
      assert.equal(storeB["ShortAnswer:NumericRational"]?.rawText, "3/4");
      assert.equal(storeB["ShortAnswer:NumericRational"]?.displayLatex, "3/4");
    });

    it("resets store completely when entering Create mode", () => {
      const createStore = resetAndHydrateDraftStore(null, false);
      assert.deepEqual(createStore, {});
    });

    it("hydrates Coordinate2D question with semicolon format", () => {
      const coordQuestion = {
        questionType: "ShortAnswer" as const,
        answerEvaluationMode: "Coordinate2D" as const,
        correctAnswer: "(2; 5)",
      };
      const store = resetAndHydrateDraftStore(coordQuestion, true);
      assert.equal(store["ShortAnswer:Coordinate2D"]?.rawText, "(2; 5)");
      assert.ok(store["ShortAnswer:Coordinate2D"]?.displayLatex.includes("2"));
      assert.ok(store["ShortAnswer:Coordinate2D"]?.displayLatex.includes("5"));
    });
  });

  describe("MultipleChoice Synchronization (syncMcqCorrectAnswer)", () => {
    it("synchronizes correctAnswer from the option with isCorrect = true", () => {
      const options = [
        { optionLabel: "A", optionText: "Phương án 1", isCorrect: false },
        { optionLabel: "B", optionText: "Phương án 2", isCorrect: true },
        { optionLabel: "C", optionText: "Phương án 3", isCorrect: false },
      ];
      assert.equal(syncMcqCorrectAnswer(options), "B");
    });

    it("supports legacy label property", () => {
      const options = [
        { label: "C", optionText: "Phương án C", isCorrect: true },
        { label: "D", optionText: "Phương án D", isCorrect: false },
      ];
      assert.equal(syncMcqCorrectAnswer(options), "C");
    });

    it("returns empty string if no option is marked correct", () => {
      const options = [
        { optionLabel: "A", optionText: "Phương án 1", isCorrect: false },
        { optionLabel: "B", optionText: "Phương án 2", isCorrect: false },
      ];
      assert.equal(syncMcqCorrectAnswer(options), "");
    });

    it("handles null or empty options array gracefully", () => {
      assert.equal(syncMcqCorrectAnswer(null), "");
      assert.equal(syncMcqCorrectAnswer([]), "");
    });
  });

  describe("Formula Delimiter Cleaning (cleanFormulaForInsertion)", () => {
    it("strips outer single dollars $...$", () => {
      assert.equal(cleanFormulaForInsertion("$x^2 + 1$"), "x^2 + 1");
    });

    it("strips outer double dollars $$...$$", () => {
      assert.equal(cleanFormulaForInsertion("$$\\frac{a}{b}$$"), "\\frac{a}{b}");
    });

    it("preserves formulas without delimiters and trims whitespace", () => {
      assert.equal(cleanFormulaForInsertion("  \\sqrt{x}  "), "\\sqrt{x}");
    });

    it("handles empty or whitespace-only inputs", () => {
      assert.equal(cleanFormulaForInsertion(""), "");
      assert.equal(cleanFormulaForInsertion("   "), "");
      assert.equal(cleanFormulaForInsertion(null), "");
    });
  });

  describe("Inline Formula Insertion (insertFormulaAtCursor)", () => {
    it("inserts formula at cursor position in questionText", () => {
      const text = "Tính giá trị biểu thức:  khi x = 2";
      const selectionStart = 24; // immediately after colon and space
      const selectionEnd = 24;
      const formula = "\\frac{x+1}{x-1}";

      const { newText, newCursorPos } = insertFormulaAtCursor(
        text,
        selectionStart,
        selectionEnd,
        formula
      );

      assert.equal(
        newText,
        "Tính giá trị biểu thức: $\\frac{x+1}{x-1}$ khi x = 2"
      );
      assert.equal(newCursorPos, 24 + "$\\frac{x+1}{x-1}$".length);
    });

    it("inserts formula at cursor position in solution", () => {
      const solution = "Ta có: . Suy ra kết quả.";
      const { newText, newCursorPos } = insertFormulaAtCursor(solution, 7, 7, "y = 2x + 1");

      assert.equal(newText, "Ta có: $y = 2x + 1$. Suy ra kết quả.");
      assert.equal(newCursorPos, 7 + "$y = 2x + 1$".length);
    });

    it("inserts formula into specific active MCQ option (e.g. option B)", () => {
      const optionBText = "Điểm M có tọa độ ";
      const { newText, newCursorPos } = insertFormulaAtCursor(
        optionBText,
        optionBText.length,
        optionBText.length,
        "(1; 2)"
      );

      assert.equal(newText, "Điểm M có tọa độ $(1; 2)$");
      assert.equal(newCursorPos, optionBText.length + "$(1; 2)$".length);
    });

    it("replaces selected text range cleanly", () => {
      const text = "Thay [CONG_THUC] vào biểu thức";
      const start = text.indexOf("[CONG_THUC]");
      const end = start + "[CONG_THUC]".length;

      const { newText, newCursorPos } = insertFormulaAtCursor(text, start, end, "x = 3");
      assert.equal(newText, "Thay $x = 3$ vào biểu thức");
      assert.equal(newCursorPos, start + "$x = 3$".length);
    });

    it("does not insert when formula is empty and leaves text unchanged", () => {
      const text = "Giữ nguyên văn bản này";
      const { newText, newCursorPos } = insertFormulaAtCursor(text, 5, 5, "   ");
      assert.equal(newText, text);
      assert.equal(newCursorPos, 5);
    });

    it("never creates nested $$ delimiters when input formula already has $", () => {
      const text = "Kết quả: ";
      const { newText } = insertFormulaAtCursor(text, 9, 9, "$x^2$");
      assert.equal(newText, "Kết quả: $x^2$");
      assert.ok(!newText.includes("$$"));
    });
  });

  describe("Authoritative Payload Construction (buildAuthoritativeQuestionPayload)", () => {
    const baseFormData: CreateQuestionRequest = {
      subjectId: "subj-1",
      primaryTopicNodeId: "node-1",
      questionType: "MultipleChoice",
      difficulty: 3,
      questionText: "Nội dung câu hỏi mẫu",
      maxScore: 1,
      estimatedTimeSeconds: 60,
      reasoningRequired: false,
      languageCode: "vi",
      options: [
        { optionLabel: "A", optionText: "Phương án A", isCorrect: true, orderIndex: 0 },
        { optionLabel: "B", optionText: "Phương án B", isCorrect: false, orderIndex: 1 },
      ],
      correctAnswer: "",
    };

    it("synchronizes MultipleChoice correctAnswer from correct option", () => {
      const { payload, error } = buildAuthoritativeQuestionPayload({
        formData: baseFormData,
        modeDrafts: {},
      });

      assert.equal(error, undefined);
      assert.equal(payload.correctAnswer, "A");
      assert.equal(payload.answerEvaluationMode, "TextExact");
    });

    it("returns error if MultipleChoice has no correct option", () => {
      const noCorrectOptions = baseFormData.options?.map((o) => ({ ...o, isCorrect: false }));
      const { error } = buildAuthoritativeQuestionPayload({
        formData: { ...baseFormData, options: noCorrectOptions },
        modeDrafts: {},
      });

      assert.ok(error?.includes("Vui lòng chọn một phương án đúng"));
    });

    it("enforces Essay:Manual requires non-empty correctAnswer (QuestionActivationPolicy)", () => {
      const essayFormData: CreateQuestionRequest = {
        ...baseFormData,
        questionType: "Essay",
        answerEvaluationMode: "Manual",
        correctAnswer: "",
      };

      // When draft is empty
      const emptyResult = buildAuthoritativeQuestionPayload({
        formData: essayFormData,
        modeDrafts: {},
      });
      assert.ok(emptyResult.error?.includes("Vui lòng nhập đáp án mẫu hoặc rubric"));

      // When activeDraftValue has rubric content
      const validResult = buildAuthoritativeQuestionPayload({
        formData: essayFormData,
        activeDraftValue: { rawText: "Rubric chi tiết", displayLatex: "" },
        modeDrafts: {},
      });
      assert.equal(validResult.error, undefined);
      assert.equal(validResult.payload.correctAnswer, "Rubric chi tiết");
      assert.equal(validResult.payload.answerEvaluationMode, "Manual");
    });

    it("uses activeDraftValue as Single Source of Truth over stale formData.correctAnswer", () => {
      const shortFormData: CreateQuestionRequest = {
        ...baseFormData,
        questionType: "ShortAnswer",
        answerEvaluationMode: "NumericRational",
        correctAnswer: "giá_trị_cũ_bị_lệch",
      };

      const { payload, error } = buildAuthoritativeQuestionPayload({
        formData: shortFormData,
        activeDraftValue: { rawText: "5/7", displayLatex: "5/7" },
        modeDrafts: {},
      });

      assert.equal(error, undefined);
      assert.equal(payload.correctAnswer, "5/7");
    });

    it("rejects saving when questionText contains incomplete MathLive placeholder", () => {
      const invalidFormData: CreateQuestionRequest = {
        ...baseFormData,
        questionText: "Cho tích phân $\\int_0^1 \\placeholder{dx}$ hãy tính kết quả.",
      };

      const { error } = buildAuthoritativeQuestionPayload({
        formData: invalidFormData,
        modeDrafts: {},
      });

      assert.ok(error?.includes("Nội dung câu hỏi chứa công thức chưa hoàn thành"));
    });

    it("rejects saving when solution contains incomplete MathLive placeholder", () => {
      const invalidFormData: CreateQuestionRequest = {
        ...baseFormData,
        solution: "Lời giải: Áp dụng công thức $\\frac{\\placeholder{a}}{b}$.",
      };

      const { error } = buildAuthoritativeQuestionPayload({
        formData: invalidFormData,
        modeDrafts: {},
      });

      assert.ok(error?.includes("Lời giải chứa công thức chưa hoàn thành"));
    });

    it("rejects saving when an MCQ option contains incomplete MathLive placeholder", () => {
      const invalidOptions = [
        { optionLabel: "A", optionText: "$\\sqrt{\\placeholder{x}}$", isCorrect: true, orderIndex: 0 },
        { optionLabel: "B", optionText: "Phương án B", isCorrect: false, orderIndex: 1 },
      ];

      const { error } = buildAuthoritativeQuestionPayload({
        formData: { ...baseFormData, options: invalidOptions },
        modeDrafts: {},
      });

      assert.ok(error?.includes("Phương án A chứa công thức chưa hoàn thành"));
    });

    it("rejects saving when correctAnswer contains incomplete MathLive placeholder", () => {
      const invalidFormData: CreateQuestionRequest = {
        ...baseFormData,
        questionType: "ShortAnswer",
        answerEvaluationMode: "TextExact",
      };

      const { error } = buildAuthoritativeQuestionPayload({
        formData: invalidFormData,
        activeDraftValue: { rawText: "$\\placeholder{}$", displayLatex: "" },
        modeDrafts: {},
      });

      assert.ok(error?.includes("Đáp án chuẩn chứa công thức chưa hoàn thành"));
    });
  });

  describe("Formula Placeholder Detection & Validation (validateAndCleanFormula)", () => {
    it("detects unfilled placeholders in standard MathLive templates", () => {
      assert.equal(hasUnfilledPlaceholder("\\int_0^\\infty \\placeholder{} dx"), true);
      assert.equal(hasUnfilledPlaceholder("\\frac{\\placeholder{a}}{\\placeholder{b}}"), true);
      assert.equal(hasUnfilledPlaceholder("\\placeholder"), true);
      assert.equal(hasUnfilledPlaceholder("x^2 + 2x + 1"), false);
      assert.equal(hasUnfilledPlaceholder("\\int_0^1 x^2 dx"), false);
    });

    it("validates and cleans complete formulas successfully", () => {
      const res = validateAndCleanFormula("  $\\frac{1}{2} + \\sqrt{x}$  ");
      assert.equal(res.isComplete, true);
      assert.equal(res.hasPlaceholder, false);
      assert.equal(res.cleanLatex, "\\frac{1}{2} + \\sqrt{x}");
      assert.equal(res.error, undefined);
    });

    it("rejects formulas with unfilled placeholder and returns descriptive error", () => {
      const res = validateAndCleanFormula("\\int_0^1 \\placeholder{} dx");
      assert.equal(res.isComplete, false);
      assert.equal(res.hasPlaceholder, true);
      assert.ok(res.error?.includes("còn ô trống chưa điền"));
    });

    it("rejects empty formula", () => {
      const res = validateAndCleanFormula("   $$   ");
      assert.equal(res.isComplete, false);
      assert.equal(res.cleanLatex, "");
      assert.ok(res.error?.includes("không được để trống"));
    });

    it("finds all incomplete formulas across a mixed-content prose text", () => {
      const text = "Cho $\\int_0^1 \\placeholder{} dx$ và $\\frac{1}{2}$ và $\\sqrt{\\placeholder{a}}$.";
      const incomplete = findIncompleteFormulasInText(text);
      assert.equal(incomplete.length, 2);
      assert.ok(incomplete[0].includes("\\placeholder{}"));
      assert.ok(incomplete[1].includes("\\placeholder{a}"));

      const cleanText = "Cho hàm số $y=x^2+1$ và $\\frac{1}{2}$.";
      assert.equal(findIncompleteFormulasInText(cleanText).length, 0);
    });
  });

  describe("Rich Segment Parsing & Serialization (parseRichSegments, serializeRichSegments)", () => {
    it("parses mixed text and formulas into structured RichSegments", () => {
      const input = "Cho một tam giác vuông có cạnh $a^2+b^2=c^2$ tính cạnh huyền.";
      const segments = parseRichSegments(input);

      assert.equal(segments.length, 3);
      assert.equal(segments[0].type, "text");
      assert.equal(segments[0].content, "Cho một tam giác vuông có cạnh ");
      assert.equal(segments[1].type, "math");
      assert.equal(segments[1].content, "a^2+b^2=c^2");
      assert.equal(segments[2].type, "text");
      assert.equal(segments[2].content, " tính cạnh huyền.");
    });

    it("achieves 100% round-trip fidelity between prose + $latex$ and parsed segments", () => {
      const input = "Cho hàm số $f(x)=x^2+1$ liên tục trên $\\mathbb{R}$ và $\\int_0^1 f(x)dx = 2$.";
      const segments = parseRichSegments(input);
      const output = serializeRichSegments(segments);

      assert.equal(output, input);
    });

    it("handles plain text with no formulas without corruption", () => {
      const input = "Tìm từ đồng nghĩa với từ in đậm dưới đây:";
      const segments = parseRichSegments(input);
      assert.equal(segments.length, 1);
      assert.equal(segments[0].type, "text");
      assert.equal(serializeRichSegments(segments), input);
    });

    it("hydrates legacy double-dollar math $$...$$ cleanly into single-dollar canonical form", () => {
      const input = "Tích phân $$\\int_0^1 x^2 dx$$ có giá trị là:";
      const segments = parseRichSegments(input);
      assert.equal(segments.length, 3);
      assert.equal(segments[1].type, "math");
      assert.equal(segments[1].content, "\\int_0^1 x^2 dx");
      assert.equal(serializeRichSegments(segments), "Tích phân $\\int_0^1 x^2 dx$ có giá trị là:");
    });
  });

  describe("Inline Formula Insertion Placeholder Safety (insertFormulaAtCursor)", () => {
    it("refuses to insert formula containing placeholder and returns error without modifying text", () => {
      const text = "Cho hàm số f(x) = ";
      const result = insertFormulaAtCursor(text, text.length, text.length, "\\int_0^\\infty \\placeholder{} dx");

      assert.equal(result.newText, text);
      assert.equal(result.newCursorPos, text.length);
      assert.ok(result.error?.includes("còn ô trống chưa điền"));
    });

    it("inserts complete formula cleanly when valid", () => {
      const text = "Cho hàm số f(x) = ";
      const result = insertFormulaAtCursor(text, text.length, text.length, "x^2 + 1");

      assert.equal(result.newText, "Cho hàm số f(x) = $x^2 + 1$");
      assert.equal(result.newCursorPos, text.length + "$x^2 + 1$".length);
      assert.equal(result.error, undefined);
    });
  });

  describe("AnswerEditorValue Serialization (serializeAnswerEditorValue)", () => {
    it("serializes NumericRational trimmed rawText", () => {
      const val = { rawText: "  3/4  ", displayLatex: "\\frac{3}{4}" };
      assert.equal(serializeAnswerEditorValue(val, "NumericRational"), "3/4");
    });

    it("serializes Coordinate2D trimmed rawText", () => {
      const val = { rawText: "  (2; 5)  ", displayLatex: "\\left(2;\\,5\\right)" };
      assert.equal(serializeAnswerEditorValue(val, "Coordinate2D"), "(2; 5)");
    });

    it("serializes TextExact trimmed rawText", () => {
      const val = { rawText: "  Paris  ", displayLatex: "" };
      assert.equal(serializeAnswerEditorValue(val, "TextExact"), "Paris");
    });

    it("serializes Manual trimmed rawText", () => {
      const val = { rawText: "  Rubric chấm bài  ", displayLatex: "" };
      assert.equal(serializeAnswerEditorValue(val, "Manual"), "Rubric chấm bài");
    });

    it("returns empty string for null or undefined value", () => {
      assert.equal(serializeAnswerEditorValue(null, "TextExact"), "");
      assert.equal(serializeAnswerEditorValue(undefined, "Manual"), "");
    });
  });
});
