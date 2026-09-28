import { describe, it } from "node:test";
import assert from "node:assert/strict";
import {
  getAnswerDraftKey,
  syncMcqCorrectAnswer,
  cleanFormulaForInsertion,
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
