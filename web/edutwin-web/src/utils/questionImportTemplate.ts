export const QUESTION_IMPORT_CSV_TEMPLATE =
  "QuestionType,Difficulty,QuestionText,OptionA,MisconceptionA,OptionB,MisconceptionB,OptionC,MisconceptionC,OptionD,MisconceptionD,CorrectAnswer,Solution,ExpectedReasoning,MaxScore,EstimatedTimeSeconds,ReasoningRequired,AnswerEvaluationMode\n" +
  'MultipleChoice,3,"Cho hàm số $y=x^2$. Tính $y\'(2)$.","2","Nhầm đạo hàm của hằng số","4","","8","Nhầm mũ thành nhân","0","Nhầm cực trị","B","Ta có $y\'=2x$, thay $x=2$ được $y\'(2)=4$.","Tính đạo hàm cơ bản và thế số",10,120,TRUE,TextExact\n' +
  'ShortAnswer,2,"Tính giá trị của biểu thức $\\frac{1}{4} + \\frac{1}{4}$.","","","","","","","","","1/2","Ta có 1/4 + 1/4 = 2/4 = 1/2.","Quy đồng và rút gọn phân số",10,60,TRUE,NumericRational\n' +
  'ShortAnswer,2,"Tìm tọa độ giao điểm của $y=x$ và $y=2-x$.","","","","","","","","","(1; 1)","Phương trình hoành độ: x = 2 - x <=> 2x = 2 <=> x = 1 => y = 1.","Giải hệ phương trình tọa độ giao điểm",10,90,TRUE,Coordinate2D\n' +
  'Essay,3,"Trình bày ý nghĩa hình học của đạo hàm tại một điểm.","","","","","","","","","Đạo hàm tại điểm x0 là hệ số góc của tiếp tuyến với đồ thị hàm số tại điểm đó.","Học sinh nêu định nghĩa và liên hệ hệ số góc tiếp tuyến.","Hiểu bản chất hình học của đạo hàm",10,180,TRUE,Manual\n' +
  'ShortAnswer,2,"Tìm tập xác định của $y=1/(x-2)$.","","","","","","","","","\\mathbb{R}\\setminus\\{2\\}","Mẫu số khác 0 nên $x \\ne 2$.","Loại giá trị làm mẫu số bằng 0",10,120,TRUE,MathEquivalent\n';

export const QUESTION_IMPORT_TEMPLATE_VERSION = "1.0";
export const QUESTION_IMPORT_V1_COLUMNS = [
  "QuestionKey", "TopicCode", "QuestionType", "GradeLevel", "Difficulty", "QuestionText",
  "OptionA", "MisconceptionA", "OptionB", "MisconceptionB", "OptionC", "MisconceptionC", "OptionD", "MisconceptionD", "CorrectAnswer", "Solution", "ExpectedReasoning",
  "RequiredIdeas", "CommonErrors", "ScoringNotes", "MaxScore", "EstimatedTimeSeconds",
  "ReasoningRequired", "AnswerEvaluationMode", "Visibility", "LanguageCode",
] as const;

export const QUESTION_IMPORT_COLUMN_LABELS: Record<typeof QUESTION_IMPORT_V1_COLUMNS[number], string> = {
  QuestionKey: "Mã câu hỏi",
  TopicCode: "Mã chủ đề",
  QuestionType: "Loại câu hỏi",
  GradeLevel: "Khối",
  Difficulty: "Độ khó",
  QuestionText: "Nội dung câu hỏi",
  OptionA: "Lựa chọn A",
  MisconceptionA: "Quan niệm sai A",
  OptionB: "Lựa chọn B",
  MisconceptionB: "Quan niệm sai B",
  OptionC: "Lựa chọn C",
  MisconceptionC: "Quan niệm sai C",
  OptionD: "Lựa chọn D",
  MisconceptionD: "Quan niệm sai D",
  CorrectAnswer: "Đáp án đúng",
  Solution: "Lời giải",
  ExpectedReasoning: "Lập luận mong đợi",
  RequiredIdeas: "Ý bắt buộc",
  CommonErrors: "Lỗi thường gặp",
  ScoringNotes: "Ghi chú chấm",
  MaxScore: "Điểm tối đa",
  EstimatedTimeSeconds: "Thời gian (giây)",
  ReasoningRequired: "Yêu cầu giải trình",
  AnswerEvaluationMode: "Chế độ chấm",
  Visibility: "Phạm vi",
  LanguageCode: "Ngôn ngữ",
};
export const QUESTION_IMPORT_V1_HEADERS = QUESTION_IMPORT_V1_COLUMNS.map((column) => QUESTION_IMPORT_COLUMN_LABELS[column]);

/** Compatibility template keeps its original values and layout with readable column labels. */
export const QUESTION_IMPORT_LEGACY_CSV_TEMPLATE = QUESTION_IMPORT_CSV_TEMPLATE.replace(/^[^\n]+/, (header) =>
  header.split(",").map((column) => QUESTION_IMPORT_COLUMN_LABELS[column as keyof typeof QUESTION_IMPORT_COLUMN_LABELS] ?? column).join(","));

export interface ImportTemplateTopic { nodeCode: string; nodeName: string; nodeId: string; parentNodeId?: string | null; }
export interface ImportTemplateSubject { subjectId: string; subjectCode: string; subjectName: string; }

export function questionImportExampleRows(topics: readonly ImportTemplateTopic[]): string[][] {
  const examples: Record<string, string>[] = [
    { QuestionKey: "Q001", QuestionType: "MultipleChoice", QuestionText: "Cho $y=x^2$. Tính $y'(2)$.", OptionA: "2", MisconceptionA: "Quên thay giá trị x vào đạo hàm.", OptionB: "4", OptionC: "8", MisconceptionC: "Nhầm mũ với hệ số.", OptionD: "0", CorrectAnswer: "B", Solution: "Ta có $y'=2x$, nên $y'(2)=4$.", AnswerEvaluationMode: "TextExact" },
    { QuestionKey: "Q002", QuestionType: "ShortAnswer", QuestionText: "Tính $\\frac{1}{2}+\\frac{1}{3}$.", CorrectAnswer: "5/6", Solution: "Quy đồng: $\\frac{1}{2}+\\frac{1}{3}=\\frac{5}{6}$.", AnswerEvaluationMode: "NumericRational" },
    { QuestionKey: "Q003", QuestionType: "Essay", QuestionText: "Giải thích ý nghĩa hình học của đạo hàm.", CorrectAnswer: "Đạo hàm là hệ số góc của tiếp tuyến.", Solution: "Nêu định nghĩa và liên hệ tiếp tuyến của đồ thị.", RequiredIdeas: '["Nêu hệ số góc","Liên hệ tiếp tuyến"]', CommonErrors: '["Nhầm đạo hàm với giá trị hàm số"]', ScoringNotes: "Giáo viên chấm theo nội dung trình bày.", AnswerEvaluationMode: "Manual" },
  ];
  return examples.map((example, index) => {
    const values: Record<string, string> = { GradeLevel: "12", Difficulty: "3", MaxScore: "10", EstimatedTimeSeconds: "120", ReasoningRequired: "FALSE", Visibility: "Private", LanguageCode: "vi", TopicCode: topics[index % Math.max(1, topics.length)]?.nodeCode ?? "", ...example };
    return QUESTION_IMPORT_V1_COLUMNS.map((column) => values[column] ?? "");
  });
}

export function createQuestionImportCsvTemplate(topics: readonly ImportTemplateTopic[]): string {
  const escape = (value: string) => `"${value.replace(/"/g, '""')}"`;
  return [QUESTION_IMPORT_V1_HEADERS.join(","), ...questionImportExampleRows(topics).map((row) => row.map(escape).join(","))].join("\n") + "\n";
}
