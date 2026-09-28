import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { QUESTION_IMPORT_CSV_TEMPLATE } from "../src/utils/questionImportTemplate.ts";

test("contains AnswerEvaluationMode header as the 18th column", () => {
  const lines = QUESTION_IMPORT_CSV_TEMPLATE.trim().split("\n");
  const header = lines[0];
  const columns = header.split(",");

  assert.equal(columns.length, 18);
  assert.equal(columns[17], "AnswerEvaluationMode");
  assert.ok(columns.includes("QuestionType"));
  assert.ok(columns.includes("QuestionText"));
  assert.ok(columns.includes("CorrectAnswer"));
});

test("contains examples covering all supported evaluation modes", () => {
  assert.ok(QUESTION_IMPORT_CSV_TEMPLATE.includes("TextExact"));
  assert.ok(QUESTION_IMPORT_CSV_TEMPLATE.includes("NumericRational"));
  assert.ok(QUESTION_IMPORT_CSV_TEMPLATE.includes("Coordinate2D"));
  assert.ok(QUESTION_IMPORT_CSV_TEMPLATE.includes("Manual"));
});

test("ensures every example row contains valid matching question types and evaluation modes", () => {
  const lines = QUESTION_IMPORT_CSV_TEMPLATE.trim().split("\n");
  // Row 0 is header, rows 1..4 are data rows
  assert.equal(lines.length, 5);

  // Row 1: MultipleChoice + TextExact
  assert.match(lines[1], /^MultipleChoice,.*TextExact$/);

  // Row 2: ShortAnswer + NumericRational (1/2)
  assert.match(lines[2], /^ShortAnswer,.*1\/2.*NumericRational$/);

  // Row 3: ShortAnswer + Coordinate2D ((1; 1))
  assert.match(lines[3], /^ShortAnswer,.*\(1; 1\).*Coordinate2D$/);

  // Row 4: Essay + Manual
  assert.match(lines[4], /^Essay,.*Manual$/);
});

test("matches public canonical CSV template file byte-for-byte to prevent template drift", () => {
  const publicCsvPath = path.resolve(import.meta.dirname, "../public/EduTwin_Question_Import_Template.csv");
  assert.ok(fs.existsSync(publicCsvPath), "Canonical CSV file must exist in public directory");
  const fileContent = fs.readFileSync(publicCsvPath, "utf-8").replace(/\r\n/g, "\n").trim();
  const tsContent = QUESTION_IMPORT_CSV_TEMPLATE.replace(/\r\n/g, "\n").trim();
  assert.equal(tsContent, fileContent);
});
