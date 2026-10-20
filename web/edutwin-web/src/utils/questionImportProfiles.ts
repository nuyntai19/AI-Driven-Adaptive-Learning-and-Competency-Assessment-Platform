import type { QuestionImportSourceLocation, QuestionImportV2PreviewRequest, QuestionImportPreviewRequest } from "../types/questions.ts";

export type QuestionImportSource = "excel" | "docx" | "media";
export const QUESTION_IMPORT_SOURCES = {
  excel: { label: "Excel / CSV — văn bản và công thức", accept: ".xlsx,.csv", maxMiB: 10 },
  docx: { label: "Word (.docx) — bảng câu hỏi và ảnh", accept: ".docx", maxMiB: 30 },
  media: { label: "ZIP — Excel / CSV kèm thư mục ảnh", accept: ".zip", maxMiB: 30 },
} as const;

export function questionImportSourceFileError(file: Pick<File, "name" | "size">, source: QuestionImportSource): string | null {
  const config = QUESTION_IMPORT_SOURCES[source];
  const extensions = config.accept.split(",");
  if (!extensions.some((extension) => file.name.toLowerCase().endsWith(extension))) return `Nguồn đã chọn chỉ nhận ${config.accept}. Vui lòng chọn đúng loại file.`;
  if (!file.size) return "File rỗng. Vui lòng chọn file có dữ liệu.";
  if (file.size > config.maxMiB * 1024 * 1024) return `File vượt quá ${config.maxMiB} MiB.`;
  return null;
}

export function buildQuestionImportV2Settings(settings: QuestionImportPreviewRequest, source: Exclude<QuestionImportSource, "excel">): QuestionImportV2PreviewRequest {
  const request: QuestionImportV2PreviewRequest & { legacyMode?: boolean; defaultTopicNodeId?: string } = { ...settings, templateVersion: "2.0", sourceProfile: source === "docx" ? "docx-1.0" : "media-1.0" };
  delete request.legacyMode; delete request.defaultTopicNodeId;
  return request;
}

export function formatQuestionImportSourceLocation(location?: QuestionImportSourceLocation | null): string {
  if (!location) return "";
  return [
    location.partPath, location.questionIndex == null ? null : `câu ${location.questionIndex}`,
    location.tableIndex == null ? null : `bảng ${location.tableIndex}`,
    location.tableRowIndex == null ? null : `hàng ${location.tableRowIndex}`,
    location.cellIndex == null ? null : `ô ${location.cellIndex}`,
    location.paragraphIndex == null ? null : `đoạn ${location.paragraphIndex}`,
    location.field, location.relationshipId ? `quan hệ ${location.relationshipId}` : null,
  ].filter(Boolean).join(" • ");
}
