import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import path from "node:path";
import XLSX from "xlsx";
import { createQuestionImportCsvTemplate, QUESTION_IMPORT_V1_COLUMNS } from "../src/utils/questionImportTemplate.ts";
import { createQuestionImportWorkbook } from "../src/utils/questionImportWorkbook.ts";
import { buildImportConfirmRequest, importConfirmationError, questionImportFileError, validateImportMathProfile, validateImportQuestionMath } from "../src/utils/questionImportValidation.ts";
import type { QuestionImportItemDto, QuestionImportPreviewDataDto, QuestionImportRowErrorDto } from "../src/types/questions.ts";

const topics = [{ nodeCode: "MATH-DERIV", nodeName: "Đạo hàm", nodeId: "9007199254740993" }, { nodeCode: "MATH-FRAC", nodeName: "Phân số", nodeId: "2" }, { nodeCode: "MATH-GEOM", nodeName: "Hình học", nodeId: "3" }];
const subject = { subjectId: "0904f0c4-2901-4b47-8504-bbe84239cd1f", subjectCode: "MATH", subjectName: "Toán" };
const question: QuestionImportItemDto = { questionKey: "Q001", sheetName: "Questions", rowIndex: 2, topicCode: "MATH-FRAC", topicName: "Phân số", primaryTopicNodeId: "2", gradeLevel: 12, visibility: "Private", languageCode: "vi", scoringNotes: "", defaultedFields: [], questionType: "ShortAnswer", difficulty: 3, questionText: "Tính $\\frac{1}{2}$.", correctAnswer: "1/2", solution: "Kết quả là $\\frac{1}{2}$.", maxScore: 10, estimatedTimeSeconds: 120, reasoningRequired: false, answerEvaluationMode: "NumericRational", options: [], requiredIdeas: [], commonErrors: [] };
const now = Date.parse("2026-10-12T08:00:00Z");
const preview: QuestionImportPreviewDataDto = { previewToken: "token", revision: 1, subjectId: subject.subjectId, expiresAt: "2026-10-12T08:30:00Z", templateVersion: "1.0", errorCount: 0, warningCount: 0, warnings: [], totalRows: 1, validCount: 1, invalidCount: 0, validQuestions: [question], errors: [] };

test("V1 workbook has subject Guide, per-row topic codes, native dropdowns and Text answer cells", async () => {
  const bytes = await createQuestionImportWorkbook(subject, topics);
  const workbook = XLSX.read(bytes, { type: "array", cellStyles: true, cellNF: true, sheetStubs: true });
  assert.deepEqual(workbook.SheetNames, ["Questions", "Topics", "Guide"]);
  const rows = XLSX.utils.sheet_to_json<Record<string, string>>(workbook.Sheets.Questions);
  assert.equal(rows.length, 3, "preformatted blank rows must not become example questions");
  assert.deepEqual(rows.map((row) => row.TopicCode), topics.map((topic) => topic.nodeCode));
  assert.equal(rows[1].CorrectAnswer, "5/6");
  assert.equal(rows[1].QuestionText, "Tính $\\frac{1}{2}+\\frac{1}{3}$.");
  assert.equal(workbook.Sheets.Topics.C2.v, "9007199254740993", "large topic IDs must remain strings");
  const answerColumn = XLSX.utils.encode_col(QUESTION_IMPORT_V1_COLUMNS.indexOf("CorrectAnswer"));
  assert.equal(workbook.Sheets.Questions[`${answerColumn}3`].t, "s");
  assert.equal(workbook.Sheets.Questions[`${answerColumn}3`].z, "@");
  assert.equal(workbook.Sheets.Questions[`${answerColumn}501`].z, "@");
  assert.equal(workbook.Sheets.Guide.B1.v, "1.0");
  assert.equal(workbook.Sheets.Guide.B2.v, subject.subjectId);
  const zip = XLSX.CFB.read(bytes, { type: "buffer" });
  const xml = (name: string) => new TextDecoder().decode(new Uint8Array(XLSX.CFB.find(zip, `/${name}`).content));
  const sheet = xml("xl/worksheets/sheet1.xml");
  assert.match(sheet, /<pane[^>]*state="frozen"/);
  assert.match(sheet, /<dataValidations count="8">/);
  assert.match(sheet, /<formula1>ImportTopics<\/formula1>/);
  assert.ok(sheet.indexOf("<dataValidations") < sheet.indexOf("<ignoredErrors"), "validation must precede ignoredErrors in SpreadsheetML order");
  assert.match(xml("xl/styles.xml"), /wrapText="1"/);
  assert.match(xml("xl/workbook.xml"), /name="ImportTopics">Topics!\$A\$2:\$A\$4/);
});

test("V1 CSV quotes JSON arrays and retains one actual LaTeX backslash", () => {
  const csv = createQuestionImportCsvTemplate(topics);
  const sheet = XLSX.read(csv, { type: "string", raw: true }).Sheets.Sheet1;
  const rows = XLSX.utils.sheet_to_json<Record<string, string>>(sheet);
  assert.equal(rows[1].CorrectAnswer, "5/6");
  assert.equal(rows[1].QuestionText, "Tính $\\frac{1}{2}+\\frac{1}{3}$.");
  assert.deepEqual(JSON.parse(rows[2].RequiredIdeas), ["Nêu hệ số góc", "Liên hệ tiếp tuyến"]);
  assert.ok(QUESTION_IMPORT_V1_COLUMNS.includes("MisconceptionA"));
});

test("math validation checks every prose field and preserves spreadsheet cell location", () => {
  assert.deepEqual(validateImportQuestionMath(question), []);
  const errors = validateImportQuestionMath({ ...question, expectedReasoning: "$\\unknowncommand{x}$", scoringNotes: "$x", requiredIdeas: ["$\\placeholder{}$"], commonErrors: ["$$x$$"], options: [{ optionLabel: "A", optionText: "$x$", misconception: "$\\frac{1}{$", isCorrect: false, orderIndex: 0 }] });
  for (const field of ["ExpectedReasoning", "ScoringNotes", "RequiredIdeas[0]", "CommonErrors[0]", "MisconceptionA"]) assert.ok(errors.some((error) => error.field === field), `missing ${field}`);
  assert.ok(errors.every((error) => error.sheetName === "Questions" && error.rowIndex === 2 && error.questionKey === "Q001"));
});

test("V1 rejects unsupported block math and overlong/macro formulas before rendering", () => {
  for (const value of ["$$x$$", "\\[x\\]", "\\(x\\)", "$\\href{https://example.com}{x}$", "$\\def\\x{\\x}\\x$", `$${"x".repeat(2001)}$`, "$x\ny$"]) assert.ok(validateImportMathProfile(value), value.slice(0, 40));
  assert.equal(validateImportMathProfile("Tính $\\sqrt{x}$ với $x>0$."), null);
  assert.deepEqual(validateImportQuestionMath({ ...question, answerEvaluationMode: "MathEquivalent", correctAnswer: "\\mathbb{R}\\setminus\\{2\\}" }), []);
});

test("confirm never silently skips server/client errors, requires explicit selected valid keys", () => {
  const bad: QuestionImportRowErrorDto = { rowIndex: 3, sheetName: "Questions", questionKey: "Q002", columnName: "TopicCode", code: "TOPIC_INVALID", field: "TopicCode", errorMessage: "Invalid topic" };
  const mixed = { ...preview, totalRows: 2, invalidCount: 1, errorCount: 1, errors: [bad] };
  assert.ok(importConfirmationError(mixed, subject.subjectId, ["Q001"], false, [], now));
  assert.equal(importConfirmationError(mixed, subject.subjectId, ["Q001"], true, [], now), null);
  assert.ok(importConfirmationError(mixed, subject.subjectId, ["Q002"], true, [], now));
  const client = validateImportQuestionMath({ ...question, solution: "$x" });
  assert.ok(importConfirmationError(preview, subject.subjectId, ["Q001"], true, client, now));
  assert.ok(importConfirmationError(preview, "another-subject", ["Q001"], false, [], now));
  assert.ok(importConfirmationError(preview, subject.subjectId, ["Q001"], false, [], Date.parse(preview.expiresAt)));
  assert.equal(importConfirmationError(preview, subject.subjectId, ["Q001"], false, [], now), null);
});

test("file gate rejects unsupported formats, empty and oversized files", () => {
  for (const name of ["question.docx", "question.xlsm", "question.txt"]) assert.ok(questionImportFileError({ name, size: 100 }));
  assert.ok(questionImportFileError({ name: "question.xlsx", size: 0 }));
  assert.ok(questionImportFileError({ name: "question.csv", size: 10 * 1024 * 1024 + 1 }));
  assert.equal(questionImportFileError({ name: "question.XLSX", size: 100 }), null);
});

test("confirm request sends keys only for explicit SelectedValid mode", () => {
  assert.deepEqual(buildImportConfirmRequest(preview, ["Q001"], false), { previewToken: "token", revision: 1, selectedQuestionKeys: [], importMode: "AllValid", targetStatus: "Draft" });
  assert.deepEqual(buildImportConfirmRequest(preview, ["Q001"], true), { previewToken: "token", revision: 1, selectedQuestionKeys: ["Q001"], importMode: "SelectedValid", targetStatus: "Draft" });
});

test("valid inequalities and comparison characters are accepted as formulas", () => {
  assert.deepEqual(validateImportQuestionMath({ ...question, questionText: "Với $a<b>c$ và $x>0$, tính $\\sqrt{x}$." }), []);
});

test("teacher editor hydrates imported language for create and update payloads", () => {
  const source = fs.readFileSync(path.resolve(import.meta.dirname, "../src/pages/teacher/TeacherQuestionEditorView.tsx"), "utf8");
  assert.match(source, /setLanguageCode\(q\.languageCode \|\| "vi"\)/);
  assert.equal((source.match(/\n\s+languageCode,/g) ?? []).length, 2);
});
