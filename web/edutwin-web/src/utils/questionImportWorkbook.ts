import { QUESTION_IMPORT_COLUMN_LABELS, QUESTION_IMPORT_TEMPLATE_VERSION, QUESTION_IMPORT_V1_COLUMNS, QUESTION_IMPORT_V1_HEADERS, questionImportExampleRows, type ImportTemplateSubject, type ImportTemplateTopic } from "./questionImportTemplate.ts";
import { QUESTION_IMPORT_LATEX_EXAMPLES } from "./questionImportLatexGuide.ts";

const fieldGuidance: Record<typeof QUESTION_IMPORT_V1_COLUMNS[number], string> = {
  QuestionKey: "Tùy chọn. Mã riêng cho từng câu trong file, không trùng nhau; ví dụ Q001, Q002.",
  TopicCode: "Bắt buộc. Chọn mã có sẵn trong trang Topics, thuộc môn học đã chọn.",
  QuestionType: "Chọn trong danh sách: MultipleChoice = Trắc nghiệm; ShortAnswer = Trả lời ngắn; Essay = Tự luận.",
  GradeLevel: "10, 11 hoặc 12. Có thể để trống nếu đã chọn khối mặc định trong cửa sổ import.",
  Difficulty: "Từ 1 (rất dễ) đến 5 (rất khó). Để trống để dùng độ khó mặc định của lô.",
  QuestionText: "Bắt buộc. Nội dung đề bài; công thức viết bằng văn bản LaTeX inline $...$.",
  OptionA: "Lựa chọn A của câu trắc nghiệm. Câu trắc nghiệm cần đủ A, B, C, D; dạng khác để trống.",
  MisconceptionA: "Tùy chọn. Giải thích quan niệm sai khi học sinh chọn A.",
  OptionB: "Lựa chọn B của câu trắc nghiệm; có thể chứa công thức inline $...$.",
  MisconceptionB: "Tùy chọn. Giải thích quan niệm sai khi học sinh chọn B.",
  OptionC: "Lựa chọn C của câu trắc nghiệm; có thể chứa công thức inline $...$.",
  MisconceptionC: "Tùy chọn. Giải thích quan niệm sai khi học sinh chọn C.",
  OptionD: "Lựa chọn D của câu trắc nghiệm; có thể chứa công thức inline $...$.",
  MisconceptionD: "Tùy chọn. Giải thích quan niệm sai khi học sinh chọn D.",
  CorrectAnswer: "Bắt buộc. Trắc nghiệm: A/B/C/D. Tự luận: đáp án mẫu. Chấm số hữu tỉ: nhập 5/6, không bọc dấu $. Tọa độ: (1; 1).",
  Solution: "Bắt buộc. Lời giải chuẩn để giáo viên rà soát; có thể xuống dòng văn bản và dùng LaTeX inline $...$.",
  ExpectedReasoning: "Tùy chọn. Các bước lập luận hoặc cách tiếp cận mong đợi ở học sinh.",
  RequiredIdeas: 'Tùy chọn. Danh sách JSON, ví dụ ["Nêu công thức","Thay số đúng"]. Không tách bằng ; hoặc |.',
  CommonErrors: 'Tùy chọn. Danh sách JSON, ví dụ ["Quên điều kiện xác định"]. Không tách bằng ; hoặc |.',
  ScoringNotes: "Tùy chọn. Ghi chú chấm điểm/cách giải tương đương; độc lập với lời giải.",
  MaxScore: "Lớn hơn 0 và không quá 999.99; tối đa hai chữ số thập phân. Để trống để dùng mặc định của lô.",
  EstimatedTimeSeconds: "Số giây nguyên từ 1 đến 86400, ví dụ 120. Để trống để dùng mặc định của lô.",
  ReasoningRequired: "Chọn TRUE = Có yêu cầu trình bày lời giải; FALSE = Không bắt buộc. Để trống để dùng mặc định của lô.",
  AnswerEvaluationMode: "TextExact = So khớp văn bản; NumericRational = Số hữu tỉ/phân số; Coordinate2D = Tọa độ; MathEquivalent = So khớp toán học giới hạn; Manual = Chấm theo rubric. Trắc nghiệm dùng TextExact; tự luận dùng Manual.",
  Visibility: "Private = Riêng giáo viên; Shared = Dùng chung trong trung tâm khi kích hoạt. Để trống để dùng mặc định của lô.",
  LanguageCode: "vi = Tiếng Việt; en = Tiếng Anh. Để trống để dùng ngôn ngữ mặc định của lô.",
};

/** SheetJS CE writes the workbook; its bundled ZIP utility adds native Excel guidance. */
export interface QuestionImportWorkbookOptions { templateVersion?: "1.0" | "2.0"; imageRef?: string; }
export async function createQuestionImportWorkbook(subject: ImportTemplateSubject, topics: readonly ImportTemplateTopic[], options: QuestionImportWorkbookOptions = {}): Promise<Uint8Array> {
  const spreadsheet = await import("xlsx");
  // Browser ESM exports CFB directly; Node's CommonJS bridge exposes it on default.
  const XLSX = spreadsheet.CFB ? spreadsheet : (spreadsheet as unknown as { default: typeof spreadsheet }).default;
  const book = XLSX.utils.book_new();
  const withMedia = options.templateVersion === "2.0";
  const columns = withMedia ? [...QUESTION_IMPORT_V1_COLUMNS, "ImageRef"] : [...QUESTION_IMPORT_V1_COLUMNS];
  const headers = withMedia ? [...QUESTION_IMPORT_V1_HEADERS, "Mã ảnh đề bài"] : QUESTION_IMPORT_V1_HEADERS;
  const examples = questionImportExampleRows(topics).map((row, index) => withMedia ? [...row, index === 0 ? options.imageRef ?? "media/example.png" : ""] : row);
  const questions = XLSX.utils.aoa_to_sheet([headers, ...examples]);
  // Preformat all editable rows as Text BEFORE Excel receives user input.
  for (let row = 0; row <= 500; row++) {
    for (let column = 0; column < columns.length; column++) {
      const address = XLSX.utils.encode_cell({ r: row, c: column });
      const cell = questions[address] ?? { t: "s", v: "" };
      cell.t = "s"; cell.z = "@";
      questions[address] = cell;
    }
  }
  const lastColumn = XLSX.utils.encode_col(columns.length - 1);
  questions["!ref"] = `A1:${lastColumn}501`;
  questions["!cols"] = columns.map((column) => ({ wch: /Text|Solution|Reasoning|Ideas|Errors|Notes/.test(column) ? 48 : 22 }));
  questions["!rows"] = [{ hpt: 32 }];
  questions["!autofilter"] = { ref: `A1:${lastColumn}501` };
  const topicSheet = XLSX.utils.aoa_to_sheet([
    ["Mã chủ đề", "Tên chủ đề", "Mã định danh chủ đề", "Mã định danh chủ đề cha"],
    ...topics.map((topic) => [topic.nodeCode, topic.nodeName, topic.nodeId, topic.parentNodeId ?? ""]),
  ]);
  topicSheet["!cols"] = [{ wch: 28 }, { wch: 60 }, { wch: 25 }, { wch: 25 }];
  for (const [address, cell] of Object.entries(topicSheet)) if (!address.startsWith("!")) { cell.t = "s"; cell.z = "@"; }
  const guide = XLSX.utils.aoa_to_sheet([
    ["Phiên bản mẫu", options.templateVersion ?? QUESTION_IMPORT_TEMPLATE_VERSION], ["Mã định danh môn học", subject.subjectId],
    ["Môn học", `${subject.subjectName} (${subject.subjectCode})`],
    ["Bắt đầu", "Trang Questions (Câu hỏi): mỗi dòng là một câu; xóa ba câu ví dụ trước khi nhập dữ liệu thật. Giữ nguyên tên các trang để hệ thống đọc đúng dữ liệu."],
    ["Chủ đề", "Cột Mã chủ đề bắt buộc từng câu, lấy đúng mã trên trang Topics (Chủ đề) của môn đã chọn."],
    ["Bắt buộc", "Loại câu hỏi, Mã chủ đề, Nội dung câu hỏi, Đáp án đúng, Lời giải; Khối 10–12 theo câu hoặc mặc định của lô."],
    ["Mặc định", "Ô trống dùng thiết lập lô cho Độ khó, Điểm tối đa, Thời gian (giây), Yêu cầu giải trình, Phạm vi, Ngôn ngữ. Ô sai phải sửa."],
    ["Công thức", withMedia ? "Gõ LaTeX $...$ cho inline hoặc $$...$$ cho công thức riêng dòng; giữ nguyên dấu backslash. Không dùng Excel formula." : "Gõ văn bản + LaTeX inline $...$, ví dụ Tính $\\frac{1}{2}$. Giữ một backslash thật; công thức không xuống dòng."],
    ["Ô Text", "Các ô đã định dạng Text. Nhập 1/2, 001 hoặc LaTeX dưới dạng text; không đổi thành kiểu ngày hoặc dùng =... như formula Excel."],
    ["Giá trị trong danh sách", "Tiêu đề cột hiển thị tiếng Việt. Các mã như MultipleChoice, Manual, Private, TRUE/FALSE giữ nguyên; chọn trong dropdown và xem giải thích từng cột bên dưới."],
    ["Chế độ chấm", "Trắc nghiệm: TextExact với Đáp án đúng = A/B/C/D. Tự luận: Manual. Trả lời ngắn: chọn chế độ phù hợp hoặc dùng mặc định Manual."],
    ["Danh sách", 'Ý bắt buộc và Lỗi thường gặp: danh sách JSON, ví dụ ["Ý thứ nhất","Ý thứ hai"]. Không dùng ; hoặc | để tách.' ],
    ["Quan niệm sai", "Quan niệm sai A–D là ghi chú theo từng lựa chọn; Lỗi thường gặp là các lỗi ở cấp câu hỏi."],
    [withMedia ? "Giới hạn gói ảnh V2" : "Giới hạn V1", withMedia ? "Tối đa 500 câu, 20 câu có ảnh, một ảnh mỗi câu. ZIP 30 MiB chỉ chứa questions.xlsx hoặc questions.csv và thư mục media/. Ảnh PNG/JPG/WebP; không URL, OCR hay ảnh nổi trong Excel." : "Tối đa 500 câu, file 10 MiB. Không HTML, macro LaTeX, $$...$$, \\[...\\], Equation, ảnh/drawing, sheet rubric/mapping hoặc URL ảnh."],
    ["Sau import", "Lưu Draft; rà soát và kích hoạt bằng quyền xuất bản. Thêm ảnh/rubric nâng cao bằng editor sau import."],
    ["Xem trước", "Kiểm tra cả đề, lựa chọn, đáp án, lời giải, tiêu chí và các mặc định. Nếu có lỗi, sửa file hoặc chủ động chọn câu hợp lệ."],
    [],
    ["HƯỚNG DẪN TỪNG CỘT", "Cách nhập dữ liệu"],
    ...QUESTION_IMPORT_V1_COLUMNS.map((column) => [QUESTION_IMPORT_COLUMN_LABELS[column], fieldGuidance[column]]),
    [],
    ["BẢNG CÚ PHÁP LATEX", "Sao chép ví dụ có dấu $ vào đề bài/lời giải. Với đáp án chấm số hữu tỉ, dùng 1/2 thay vì công thức bọc dấu $."],
    ...QUESTION_IMPORT_LATEX_EXAMPLES.map(({ label, latex }) => [label, latex]),
    ...(withMedia ? [
      ["SourceProfile", "media-1.0"],
      ["Mã ảnh đề bài", "Tham chiếu chính xác media/tên-file.png trong ZIP; để trống nếu câu không có ảnh. Không chèn ảnh trực tiếp lên trang Questions."],
      ["Ảnh ví dụ", "media/example.png là ảnh mẫu nhỏ để minh họa cấu trúc gói, không phải dữ kiện đề. Thay ảnh và nội dung câu trước khi nhập dữ liệu thật."],
      ["Mã câu hỏi", "Bắt buộc từng câu trong V2, duy nhất trong file. Giữ nguyên khi đối chiếu lỗi và xem trước."],
    ] : []),
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
