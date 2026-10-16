import { QUESTION_IMPORT_TEMPLATE_VERSION, QUESTION_IMPORT_V1_COLUMNS, questionImportExampleRows, type ImportTemplateSubject, type ImportTemplateTopic } from "./questionImportTemplate.ts";

/** SheetJS CE writes the workbook; its bundled ZIP utility adds native Excel guidance. */
export async function createQuestionImportWorkbook(subject: ImportTemplateSubject, topics: readonly ImportTemplateTopic[]): Promise<Uint8Array> {
  const spreadsheet = await import("xlsx");
  // Browser ESM exports CFB directly; Node's CommonJS bridge exposes it on default.
  const XLSX = spreadsheet.CFB ? spreadsheet : (spreadsheet as unknown as { default: typeof spreadsheet }).default;
  const book = XLSX.utils.book_new();
  const questions = XLSX.utils.aoa_to_sheet([Array.from(QUESTION_IMPORT_V1_COLUMNS), ...questionImportExampleRows(topics)]);
  // Preformat all editable rows as Text BEFORE Excel receives user input.
  for (let row = 0; row <= 500; row++) {
    for (let column = 0; column < QUESTION_IMPORT_V1_COLUMNS.length; column++) {
      const address = XLSX.utils.encode_cell({ r: row, c: column });
      const cell = questions[address] ?? { t: "s", v: "" };
      cell.t = "s"; cell.z = "@";
      questions[address] = cell;
    }
  }
  const lastColumn = XLSX.utils.encode_col(QUESTION_IMPORT_V1_COLUMNS.length - 1);
  questions["!ref"] = `A1:${lastColumn}501`;
  questions["!cols"] = QUESTION_IMPORT_V1_COLUMNS.map((column) => ({ wch: /Text|Solution|Reasoning|Ideas|Errors|Notes/.test(column) ? 48 : 22 }));
  questions["!autofilter"] = { ref: `A1:${lastColumn}501` };
  const topicSheet = XLSX.utils.aoa_to_sheet([
    ["TopicCode", "TopicName", "NodeId", "ParentNodeId"],
    ...topics.map((topic) => [topic.nodeCode, topic.nodeName, topic.nodeId, topic.parentNodeId ?? ""]),
  ]);
  topicSheet["!cols"] = [{ wch: 28 }, { wch: 60 }, { wch: 25 }, { wch: 25 }];
  for (const [address, cell] of Object.entries(topicSheet)) if (!address.startsWith("!")) { cell.t = "s"; cell.z = "@"; }
  const guide = XLSX.utils.aoa_to_sheet([
    ["TemplateVersion", QUESTION_IMPORT_TEMPLATE_VERSION], ["SubjectId", subject.subjectId],
    ["Subject", `${subject.subjectName} (${subject.subjectCode})`],
    ["Bắt đầu", "Sheet Questions: mỗi dòng là một câu; xóa ba câu ví dụ trước khi nhập dữ liệu thật."],
    ["Chủ đề", "TopicCode bắt buộc từng câu, lấy đúng mã trên sheet Topics của môn đã chọn."],
    ["Bắt buộc", "QuestionType, TopicCode, QuestionText, CorrectAnswer, Solution; GradeLevel 10–12 theo câu hoặc mặc định của lô."],
    ["Mặc định", "Ô trống dùng thiết lập lô cho Difficulty, MaxScore, EstimatedTimeSeconds, ReasoningRequired, Visibility, LanguageCode. Ô sai phải sửa."],
    ["Công thức", "Gõ văn bản + LaTeX inline $...$, ví dụ Tính $\\frac{1}{2}$. Giữ một backslash thật; công thức không xuống dòng."],
    ["Ô Text", "Các ô đã định dạng Text. Nhập 1/2, 001 hoặc LaTeX dưới dạng text; không đổi thành kiểu ngày hoặc dùng =... như formula Excel."],
    ["Chế độ chấm", "MCQ: TextExact với CorrectAnswer=A/B/C/D. Essay: Manual. ShortAnswer: chọn mode phù hợp hoặc dùng mặc định Manual."],
    ["Danh sách", 'RequiredIdeas và CommonErrors: JSON array, ví dụ ["Ý thứ nhất","Ý thứ hai"]. Không dùng ; hoặc | để tách.' ],
    ["Quan niệm sai", "MisconceptionA–D là ghi chú theo từng lựa chọn; CommonErrors là các lỗi thường gặp ở cấp câu hỏi."],
    ["Giới hạn V1", "Tối đa 500 câu, file 10 MiB. Không HTML, macro LaTeX, $$...$$, \\[...\\], Equation, ảnh/drawing, sheet rubric/mapping hoặc URL ảnh."],
    ["Sau import", "Lưu Draft; rà soát và kích hoạt bằng quyền xuất bản. Thêm ảnh/rubric nâng cao bằng editor sau import."],
    ["Xem trước", "Kiểm tra cả đề, lựa chọn, đáp án, lời giải, tiêu chí và các mặc định. Nếu có lỗi, sửa file hoặc chủ động chọn câu hợp lệ."],
  ]);
  guide["!cols"] = [{ wch: 24 }, { wch: 110 }];
  XLSX.utils.book_append_sheet(book, questions, "Questions");
  XLSX.utils.book_append_sheet(book, topicSheet, "Topics");
  XLSX.utils.book_append_sheet(book, guide, "Guide");
  const bytes = new Uint8Array(XLSX.write(book, { type: "array", bookType: "xlsx", compression: true }));
  const zip = XLSX.CFB.read(bytes, { type: "buffer" });
  const updateXml = (path: string, update: (xml: string) => string) => {
    const entry = XLSX.CFB.find(zip, `/${path}`);
    if (!entry) throw new Error(`Thiếu thành phần workbook ${path}.`);
    const xml = new TextDecoder().decode(new Uint8Array(entry.content as ArrayLike<number>));
    XLSX.CFB.utils.cfb_add(zip, `/${path}`, new TextEncoder().encode(update(xml)));
  };
  const column = (name: typeof QUESTION_IMPORT_V1_COLUMNS[number]) => XLSX.utils.encode_col(QUESTION_IMPORT_V1_COLUMNS.indexOf(name));
  const rules: Array<[string, string]> = [
    ["QuestionType", '"MultipleChoice,ShortAnswer,Essay"'], ["GradeLevel", '"10,11,12"'],
    ["Difficulty", '"1,2,3,4,5"'], ["ReasoningRequired", '"TRUE,FALSE"'],
    ["AnswerEvaluationMode", '"TextExact,NumericRational,Coordinate2D,MathEquivalent,Manual"'],
    ["Visibility", '"Private,Shared"'], ["LanguageCode", '"vi,en"'],
  ];
  if (topics.length) rules.push(["TopicCode", `ImportTopics`]);
  const validations = rules.map(([name, formula]) => {
    const letter = column(name as typeof QUESTION_IMPORT_V1_COLUMNS[number]);
    return `<dataValidation type="list" allowBlank="1" showErrorMessage="1" errorStyle="stop" errorTitle="Giá trị không hợp lệ" error="Chọn một giá trị trong danh sách." sqref="${letter}2:${letter}501"><formula1>${formula}</formula1></dataValidation>`;
  }).join("");
  updateXml("xl/worksheets/sheet1.xml", (xml) => xml
    .replace(/<c([^>]*?) t="str"><v><\/v><\/c>/g, '<c$1/>')
    .replace(/<sheetViews>[\s\S]*?<\/sheetViews>/, '<sheetViews><sheetView workbookViewId="0"><pane xSplit="2" ySplit="1" topLeftCell="C2" activePane="bottomRight" state="frozen"/></sheetView></sheetViews>')
    .replace(/(?=<ignoredErrors\b|<\/worksheet>)/, `<dataValidations count="${rules.length}">${validations}</dataValidations>`));
  updateXml("xl/styles.xml", (xml) => xml.replace(/<cellXfs\b[^>]*>[\s\S]*?<\/cellXfs>/, (group) => group.replace(/<xf([^>]*)\/>/g, '<xf$1 applyAlignment="1"><alignment vertical="top" wrapText="1"/></xf>')));
  if (topics.length) updateXml("xl/workbook.xml", (xml) => {
    const name = `<definedName name="ImportTopics">Topics!$A$2:$A$${topics.length + 1}</definedName>`;
    return xml.includes("</definedNames>") ? xml.replace("</definedNames>", `${name}</definedNames>`) : xml.replace("</workbook>", `<definedNames>${name}</definedNames></workbook>`);
  });
  return new Uint8Array(XLSX.CFB.write(zip, { type: "array", fileType: "zip" }) as ArrayLike<number>);
}
