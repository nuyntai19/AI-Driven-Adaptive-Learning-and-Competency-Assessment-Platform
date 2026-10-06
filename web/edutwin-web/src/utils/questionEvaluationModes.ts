import { resolveAnswerInputType } from "../components/math/answer-editor/answerEditorHelpers.ts";

export const MATH_EQUIVALENT_HELP = "So khớp trong phạm vi: số hữu tỉ, tọa độ 2D, tập hữu hạn các số hữu tỉ và R trừ hữu hạn điểm hữu tỉ (ví dụ D = R \\ {2}). Không phải CAS đại số tổng quát; dạng chưa hỗ trợ cần giáo viên xem xét, không tự kết luận sai.";

export function validateQuestionImportModes(
  questions: Array<{ rowIndex: number; questionType: string; answerEvaluationMode?: string | null }>,
): string[] {
  return questions.flatMap((question) => {
    const mode = question.answerEvaluationMode || (question.questionType === "Essay" ? "Manual" : "TextExact");
    const result = resolveAnswerInputType(question.questionType, mode);
    return result.isValid ? [] : [`Dòng ${question.rowIndex}: ${result.errorMessage}`];
  });
}
