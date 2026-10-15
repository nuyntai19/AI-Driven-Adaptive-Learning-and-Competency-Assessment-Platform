import { QUESTION_IMPORT_COLUMN_LABELS, QUESTION_IMPORT_V1_COLUMNS, questionImportExampleRows, type ImportTemplateSubject, type ImportTemplateTopic } from "./questionImportTemplate.ts";
import { QUESTION_IMPORT_LATEX_EXAMPLES } from "./questionImportLatexGuide.ts";
import { createQuestionImportWorkbook } from "./questionImportWorkbook.ts";

/** A tiny PNG illustrates the media linkage; teachers replace it with their own image. */
export const QUESTION_IMPORT_SAMPLE_PNG = Uint8Array.from(atob("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="), (character) => character.charCodeAt(0));
const xmlEscape = (value: string) => value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&apos;");

async function zipFiles(files: ReadonlyArray<readonly [string, string | Uint8Array]>): Promise<Uint8Array> {
  const module = await import("xlsx");
  const XLSX = module.CFB ? module : (module as unknown as { default: typeof module }).default;
  const zip = XLSX.CFB.utils.cfb_new();
  for (const [path, value] of files) XLSX.CFB.utils.cfb_add(zip, path, typeof value === "string" ? new TextEncoder().encode(value) : value);
  return new Uint8Array(XLSX.CFB.write(zip, { type: "array", fileType: "zip", compression: true }) as ArrayLike<number>);
}

export async function createQuestionImportMediaZip(subject: ImportTemplateSubject, topics: readonly ImportTemplateTopic[]): Promise<Uint8Array> {
  const workbook = await createQuestionImportWorkbook(subject, topics, { templateVersion: "2.0", imageRef: "media/example.png" });
  return zipFiles([["questions.xlsx", workbook], ["media/example.png", QUESTION_IMPORT_SAMPLE_PNG]]);
}

function paragraph(text: string): string {
  return `<w:p><w:r><w:t xml:space="preserve">${xmlEscape(text)}</w:t></w:r></w:p>`;
}
function table(rows: readonly (readonly [string, string])[], pictureRow?: string): string {
  return `<w:tbl><w:tblPr><w:tblW w:w="5000" w:type="pct"/><w:tblBorders><w:top w:val="single" w:sz="4" w:color="CBD5E1"/><w:left w:val="single" w:sz="4" w:color="CBD5E1"/><w:bottom w:val="single" w:sz="4" w:color="CBD5E1"/><w:right w:val="single" w:sz="4" w:color="CBD5E1"/><w:insideH w:val="single" w:sz="4" w:color="CBD5E1"/><w:insideV w:val="single" w:sz="4" w:color="CBD5E1"/></w:tblBorders></w:tblPr><w:tblGrid><w:gridCol w:w="3000"/><w:gridCol w:w="6500"/></w:tblGrid>${rows.map(([field, value], index) => `<w:tr>${[field, value].map((text, cellIndex) => `<w:tc><w:tcPr><w:tcW w:w="${cellIndex ? 6500 : 3000}" w:type="dxa"/>${index === 0 ? '<w:shd w:fill="E2F5F1"/>' : ""}</w:tcPr>${field === "Ảnh đề bài" && cellIndex === 1 && pictureRow ? pictureRow : text.split(/\r?\n/).map(paragraph).join("")}</w:tc>`).join("")}</w:tr>`).join("")}</w:tbl><w:p/>`;
}

const samplePicture = '<w:p><w:r><w:drawing><wp:inline distT="0" distB="0" distL="0" distR="0"><wp:extent cx="304800" cy="304800"/><wp:docPr id="1" name="Anh mau" descr="Anh mau cau truc; thay bang anh de bai that"/><wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect="1"/></wp:cNvGraphicFramePr><a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:pic><pic:nvPicPr><pic:cNvPr id="1" name="example.png"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed="rIdExampleImage"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="304800" cy="304800"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>';

/** Native DOCX, with one flat field/value table per question and no external resources. */
export async function createQuestionImportDocx(subject: ImportTemplateSubject, topics: readonly ImportTemplateTopic[], includeExampleImage = true): Promise<Uint8Array> {
  const metadata = table([["EduTwin Import", "docx-1.0"], ["TemplateVersion", "2.0"], ["SubjectId", subject.subjectId]]);
  const topicTable = table([["EduTwin Topics", `${subject.subjectName} (${subject.subjectCode})`], ...topics.map((topic): [string, string] => [topic.nodeCode, topic.nodeName])]);
  const guide = table([
    ["EduTwin Guide", "Hướng dẫn nhập câu hỏi Word"],
    ["Bắt đầu", "Mỗi câu là một bảng Trường / Nội dung. Xóa các bảng câu ví dụ và sao chép một bảng trống để điền câu mới; giữ bảng metadata, chủ đề và hướng dẫn."],
    ["Mã câu hỏi", "Bắt buộc, duy nhất trong file; ví dụ Q001. Mã chủ đề lấy từ bảng EduTwin Topics của môn đã chọn."],
    ["Công thức", "Dùng LaTeX $...$ cho inline hoặc $$...$$ cho công thức riêng dòng. Có thể dùng công thức Word thuộc phạm vi hỗ trợ; hệ thống sẽ báo vị trí nếu chưa chuyển đổi được. Không dùng ảnh công thức thay cho dữ liệu có cấu trúc."],
    ["Ảnh đề bài", "Chèn một ảnh PNG/JPG/WebP trong ô Nội dung của hàng Ảnh đề bài, chọn In Line with Text. Không đặt ảnh nổi, crop, xoay, liên kết URL, tên file hoặc nội dung khác trong ô ảnh."],
    ["Giới hạn", "Tối đa 500 câu, 20 câu có ảnh; một ảnh mỗi câu. DOCX tối đa 30 MiB. Không OCR, bảng lồng nhau hoặc thay cấu trúc gộp ô."],
    ["Ảnh mẫu", "Ảnh mẫu nhỏ ở câu đầu chỉ minh họa cấu trúc, không phải dữ kiện đề. Thay ảnh và nội dung câu trước khi dùng dữ liệu thật; xóa ảnh nếu câu chỉ có chữ."],
    ["Danh sách", 'Ý bắt buộc và Lỗi thường gặp dùng JSON, ví dụ ["Ý thứ nhất","Ý thứ hai"].'],
    ["Loại / chế độ chấm", "MultipleChoice = trắc nghiệm / TextExact; ShortAnswer = trả lời ngắn / mode phù hợp; Essay = tự luận / Manual. Đáp án NumericRational nhập 1/2, không thêm dấu $."],
    ["Kết quả", "Xem lại đầy đủ nội dung, ảnh, chủ đề và công thức trước khi lưu Draft. Chỉ nhập các câu hợp lệ khi bạn đã chủ động chọn rõ."],
    ...QUESTION_IMPORT_LATEX_EXAMPLES.map(({ label, latex }): [string, string] => [label, latex]),
  ]);
  const questionTables = questionImportExampleRows(topics).map((values, index) => table([
    ["Trường", "Nội dung"],
    ...QUESTION_IMPORT_V1_COLUMNS.map((column, columnIndex): [string, string] => [QUESTION_IMPORT_COLUMN_LABELS[column], values[columnIndex]]),
    ["Ảnh đề bài", ""],
  ], includeExampleImage && index === 0 ? samplePicture : undefined)).join("");
  const document = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"><w:body>${metadata}${topicTable}${guide}${questionTables}<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1134" w:right="1134" w:bottom="1134" w:left="1134"/></w:sectPr></w:body></w:document>`;
  return zipFiles([
    ["[Content_Types].xml", '<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Default Extension="png" ContentType="image/png"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/><Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/></Types>'],
    ["_rels/.rels", '<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rIdDocument" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>'],
    ["word/document.xml", document],
    ["word/styles.xml", '<?xml version="1.0" encoding="UTF-8"?><w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri"/><w:sz w:val="22"/></w:rPr></w:rPrDefault></w:docDefaults></w:styles>'],
    ["word/_rels/document.xml.rels", `<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rIdStyles" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>${includeExampleImage ? '<Relationship Id="rIdExampleImage" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/example.png"/>' : ""}</Relationships>`],
    ...(includeExampleImage ? [["word/media/example.png", QUESTION_IMPORT_SAMPLE_PNG] as const] : []),
  ]);
}
