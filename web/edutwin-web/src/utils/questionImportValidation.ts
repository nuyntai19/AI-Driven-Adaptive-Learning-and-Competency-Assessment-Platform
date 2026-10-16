import type { QuestionImportConfirmRequest, QuestionImportItemDto, QuestionImportPreviewDataDto, QuestionImportRowErrorDto } from "../types/questions.ts";
import { validateAnswerMathFormulas, validateTextMathFormulas } from "../pages/centerManagerQuestionEditorHelpers.ts";
import { validateQuestionImportModes } from "./questionEvaluationModes.ts";

/** The V1 profile preserves the inline representation serialized by both editors. */
export function validateImportMathProfile(value: string): string | null {
  if (value.length > 20_000) return "Nội dung vượt quá 20.000 ký tự.";
  if (/\$\$|\\[()[\]]/.test(value)) return "Mẫu V1 chỉ hỗ trợ $...$ cho công thức inline. Chuyển delimiter và kiểm tra lại; công thức block chưa được hỗ trợ.";
  if (/\\(?:href|url|includegraphics|html\w*|def|gdef|edef|xdef|newcommand|renewcommand|providecommand|let|futurelet|input|include|write|csname)\b/.test(value)) return "Công thức chứa lệnh liên kết, HTML hoặc macro không thuộc phạm vi V1.";
  const formulas: string[] = [];
  let start: number | null = null;
  for (let index = 0; index < value.length; index++) {
    if (value[index] !== "$") continue;
    let precedingSlashes = 0;
    for (let cursor = index - 1; cursor >= 0 && value[cursor] === "\\"; cursor--) precedingSlashes++;
    if (precedingSlashes % 2) continue;
    if (start === null) start = index;
    else { formulas.push(value.slice(start, index + 1)); start = null; }
  }
  if (formulas.some((formula) => formula.length - 2 > 2_000)) return "Một công thức vượt quá 2.000 ký tự.";
  if (formulas.some((formula) => /[\r\n]/.test(formula))) return "Công thức inline phải nằm trên một dòng; đặt xuống dòng ngoài $...$.";
  return null;
}

export function validateImportQuestionMath(question: QuestionImportItemDto): QuestionImportRowErrorDto[] {
  const fields: Array<[string, string, boolean?]> = [
    ["QuestionText", question.questionText], ["CorrectAnswer", question.correctAnswer, true],
    ["Solution", question.solution], ["ExpectedReasoning", question.expectedReasoning ?? ""],
    ["ScoringNotes", question.scoringNotes ?? ""],
    ...question.options.flatMap((option): Array<[string, string]> => [
      [`Option${option.optionLabel}`, option.optionText], [`Misconception${option.optionLabel}`, option.misconception ?? ""],
    ]),
    ...question.requiredIdeas.map((value, index): [string, string] => [`RequiredIdeas[${index}]`, value]),
    ...question.commonErrors.map((value, index): [string, string] => [`CommonErrors[${index}]`, value]),
  ];
  const error = (field: string, message: string, rawValue?: string): QuestionImportRowErrorDto => ({
    rowIndex: question.rowIndex, sheetName: question.sheetName, questionKey: question.questionKey,
    field, columnName: field, code: "CLIENT_MATH_INVALID", errorMessage: message, rawValue,
  });
  const errors = validateQuestionImportModes([question]).map((message) => error("AnswerEvaluationMode", message));
  for (const [field, value, isAnswer] of fields) {
    const profileError = validateImportMathProfile(value);
    // Bare MathEquivalent answers are formula source, so enforce the same formula limit.
    const answerLimit = isAnswer && question.answerEvaluationMode === "MathEquivalent" && value.length > 2_000;
    if (profileError || answerLimit) {
      errors.push(error(field, profileError ?? "Đáp án công thức vượt quá 2.000 ký tự."));
      continue;
    }
    const diagnostics = isAnswer
      ? validateAnswerMathFormulas(value, question.answerEvaluationMode ?? "Manual")
      : validateTextMathFormulas(value);
    for (const diagnostic of diagnostics) {
      errors.push(error(field, `${diagnostic.message}${diagnostic.position == null ? "" : ` (vị trí ${diagnostic.position + 1})`}`, diagnostic.raw));
    }
  }
  return errors;
}

export function importConfirmationError(
  preview: QuestionImportPreviewDataDto, currentSubjectId: string,
  selectedKeys: readonly string[], explicitSubset: boolean,
  clientErrors: readonly QuestionImportRowErrorDto[], now = Date.now(),
): string | null {
  if (preview.subjectId !== currentSubjectId) return "Môn học đã thay đổi. Vui lòng kiểm tra file lại.";
  if (!preview.revision || !preview.previewToken) return "Phiên xem trước không hợp lệ. Vui lòng kiểm tra file lại.";
  const expires = Date.parse(preview.expiresAt);
  if (!Number.isFinite(expires) || expires <= now) return "Phiên xem trước đã hết hạn. Vui lòng kiểm tra file lại.";
  if ((preview.invalidCount > 0 || preview.errors.length > 0 || clientErrors.length > 0) && !explicitSubset)
    return "Sửa mọi lỗi hoặc chủ động bật nhập các câu hợp lệ và chọn những câu muốn nhập.";
  if (!selectedKeys.length) return "Chưa chọn câu hợp lệ để nhập.";
  if (new Set(selectedKeys).size !== selectedKeys.length) return "Danh sách câu chọn bị trùng.";
  const validKeys = new Set(preview.validQuestions.map((question) => question.questionKey));
  const blocked = new Set([...preview.errors, ...clientErrors].map((error) => error.questionKey));
  if (selectedKeys.some((key) => !validKeys.has(key) || blocked.has(key))) return "Có câu chưa được xác thực hoặc đang có lỗi trong danh sách chọn.";
  if (!explicitSubset && selectedKeys.length !== preview.validQuestions.length) return "Phải bật nhập các câu được chọn khi không nhập toàn bộ lô.";
  return null;
}

export function questionImportFileError(file: Pick<File, "name" | "size">): string | null {
  if (!/\.(xlsx|csv)$/i.test(file.name)) return "V1 hỗ trợ Excel .xlsx và CSV UTF-8. Định dạng này chưa được hỗ trợ.";
  if (!file.size) return "File rỗng. Vui lòng chọn file có dữ liệu.";
  if (file.size > 10 * 1024 * 1024) return "File vượt quá 10 MiB.";
  return null;
}

/** AllValid means all server-valid rows, so the API forbids a selection list in that mode. */
export function buildImportConfirmRequest(preview: QuestionImportPreviewDataDto, keys: readonly string[], explicitSubset: boolean): QuestionImportConfirmRequest {
  return {
    previewToken: preview.previewToken, revision: preview.revision,
    selectedQuestionKeys: explicitSubset ? Array.from(keys) : [],
    importMode: explicitSubset ? "SelectedValid" : "AllValid", targetStatus: "Draft",
  };
}
