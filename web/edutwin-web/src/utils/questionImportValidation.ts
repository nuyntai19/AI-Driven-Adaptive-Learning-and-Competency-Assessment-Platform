import type { QuestionImportConfirmRequest, QuestionImportItemDto, QuestionImportPreviewDataDto, QuestionImportRowErrorDto } from "../types/questions.ts";
import { validateAnswerMathFormulas, validateTextMathFormulas } from "../pages/centerManagerQuestionEditorHelpers.ts";
import { validateQuestionImportModes } from "./questionEvaluationModes.ts";

/** V1 stays inline-only; the V2 profiles preserve canonical display formulas too. */
export function validateImportMathProfile(value: string, allowDisplay = false): string | null {
  if (value.length > 20_000) return "Nội dung vượt quá 20.000 ký tự.";
  if ((!allowDisplay && value.includes("$$")) || /\\[()[\]]/.test(value)) return allowDisplay
    ? "Dùng $...$ cho công thức cùng dòng hoặc $$...$$ cho công thức khối."
    : "Mẫu V1 chỉ hỗ trợ $...$ cho công thức inline. Chuyển delimiter và kiểm tra lại; công thức block chưa được hỗ trợ.";
  if (/\\(?:href|url|includegraphics|html\w*|def|gdef|edef|xdef|newcommand|renewcommand|providecommand|let|futurelet|input|include|write|csname)\b/.test(value)) return "Công thức chứa lệnh liên kết, HTML hoặc macro không thuộc phạm vi V1.";
  let start = -1, width = 0, slashes = 0;
  for (let index = 0; index < value.length; index++) {
    const escaped = slashes % 2 === 1;
    slashes = value[index] === "\\" ? slashes + 1 : 0;
    if (value[index] !== "$" || escaped) continue;
    const currentWidth = start >= 0 && width === 1 ? 1 : value[index + 1] === "$" ? 2 : 1;
    if (start < 0) { start = index + currentWidth; width = currentWidth; index += currentWidth - 1; continue; }
    if (currentWidth !== width) return "Dấu phân cách công thức không khớp ($ hoặc $$).";
    const formula = value.slice(start, index);
    if (!formula.trim()) return "Công thức không được rỗng.";
    if (formula.length > 2_000) return "Một công thức vượt quá 2.000 ký tự.";
    if (width === 1 && /[\r\n]/.test(formula)) return "Công thức inline phải nằm trên một dòng; đặt xuống dòng ngoài $...$.";
    start = -1;
    index += currentWidth - 1;
  }
  if (start >= 0) return "Công thức có dấu $ hoặc $$ chưa đóng.";
  return null;
}

export function validateImportQuestionMath(question: QuestionImportItemDto): QuestionImportRowErrorDto[] {
  const allowDisplay = question.sourceProfile === "docx-1.0" || question.sourceProfile === "media-1.0";
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
    candidateId: question.candidateId, sourceLocation: question.sourceLocation ? { ...question.sourceLocation, field } : undefined,
    field, columnName: field, code: "CLIENT_MATH_INVALID", errorMessage: message, rawValue,
  });
  const errors = validateQuestionImportModes([question]).map((message) => error("AnswerEvaluationMode", message));
  for (const [field, value, isAnswer] of fields) {
    const profileError = validateImportMathProfile(value, allowDisplay);
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
