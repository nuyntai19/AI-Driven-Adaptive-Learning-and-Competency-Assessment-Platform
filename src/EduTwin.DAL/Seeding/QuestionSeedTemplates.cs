using System.Collections.Generic;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.DAL.Seeding;

public class QuestionTemplate
{
    public string LogicalCode { get; set; } = null!;
    public string TopicCode { get; set; } = null!;
    public byte Difficulty { get; set; }
    public QuestionType QuestionType { get; set; }
    public string LanguageCode { get; set; } = null!;
    public string QuestionText { get; set; } = null!;
    public string CorrectAnswer { get; set; } = null!;
    public string Solution { get; set; } = null!;
    public string ExpectedReasoning { get; set; } = null!;
    public GradingCriteria GradingCriteria { get; set; } = null!;
    public decimal MaxScore { get; set; }
    public uint EstimatedTimeSeconds { get; set; }
    public List<OptionTemplate> Options { get; set; } = new();
}

public class OptionTemplate
{
    public string Label { get; set; } = null!;
    public string Text { get; set; } = null!;
    public bool IsCorrect { get; set; }
}

public static class QuestionSeedTemplates
{
    public static List<QuestionTemplate> GetTemplates()
    {
        var templates = new List<QuestionTemplate>();

        // MATH-FUNCTIONS (Difficulty 1-5)
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "MATH-FUNC-01", TopicCode = "MATH-FUNCTIONS", Difficulty = 1, QuestionType = QuestionType.MultipleChoice, LanguageCode = "vi", MaxScore = 10, EstimatedTimeSeconds = 60,
            QuestionText = "Cho hàm số bậc nhất y = 2x + 1. Tính giá trị của y khi x = 3.",
            CorrectAnswer = "A", Solution = "Thay x = 3 vào phương trình y = 2x + 1, ta được y = 2(3) + 1 = 6 + 1 = 7.",
            ExpectedReasoning = "Học sinh cần biết thay thế giá trị của biến x vào biểu thức hàm số và thực hiện phép tính nhân, cộng cơ bản.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Thay số đúng" }, CommonErrors = new List<string> { "Sai phép tính" }, ScoringNotes = "Cho điểm tối đa nếu chọn đúng" },
            Options = new List<OptionTemplate> {
                new OptionTemplate { Label = "A", Text = "7", IsCorrect = true }, new OptionTemplate { Label = "B", Text = "5", IsCorrect = false },
                new OptionTemplate { Label = "C", Text = "6", IsCorrect = false }, new OptionTemplate { Label = "D", Text = "8", IsCorrect = false }
            }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "MATH-FUNC-02", TopicCode = "MATH-FUNCTIONS", Difficulty = 2, QuestionType = QuestionType.ShortAnswer, LanguageCode = "vi", MaxScore = 20, EstimatedTimeSeconds = 120,
            QuestionText = "Tìm tập xác định của hàm số y = 1 / (x - 2).", CorrectAnswer = "R \\ {2}",
            Solution = "Hàm số xác định khi mẫu số khác 0, tức là x - 2 khác 0, suy ra x khác 2. Vậy tập xác định là D = R \\ {2}.",
            ExpectedReasoning = "Nhận biết điều kiện để phân thức xác định là mẫu số phải khác 0, từ đó giải phương trình x - 2 = 0.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Mẫu số khác 0", "x khác 2" }, CommonErrors = new List<string> { "Quên điều kiện mẫu", "Viết sai kí hiệu tập hợp" }, ScoringNotes = "Trừ 50% nếu chỉ viết x khác 2 mà không kết luận tập xác định" }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "MATH-FUNC-03", TopicCode = "MATH-FUNCTIONS", Difficulty = 3, QuestionType = QuestionType.MultipleChoice, LanguageCode = "vi", MaxScore = 30, EstimatedTimeSeconds = 180,
            QuestionText = "Đồ thị hàm số y = x^2 - 4x + 3 cắt trục hoành tại các điểm có hoành độ là bao nhiêu?",
            CorrectAnswer = "C", Solution = "Phương trình hoành độ giao điểm: x^2 - 4x + 3 = 0. Giải phương trình bậc 2, ta có x = 1 và x = 3.",
            ExpectedReasoning = "Hiểu giao điểm với trục hoành là nghiệm của phương trình y = 0. Giải phương trình bậc hai.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Phương trình hoành độ giao điểm" }, CommonErrors = new List<string> { "Giải sai phương trình bậc 2" }, ScoringNotes = "Đúng / Sai" },
            Options = new List<OptionTemplate> {
                new OptionTemplate { Label = "A", Text = "x = -1, x = -3", IsCorrect = false }, new OptionTemplate { Label = "B", Text = "x = 1, x = -3", IsCorrect = false },
                new OptionTemplate { Label = "C", Text = "x = 1, x = 3", IsCorrect = true }, new OptionTemplate { Label = "D", Text = "x = -1, x = 3", IsCorrect = false }
            }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "MATH-FUNC-04", TopicCode = "MATH-FUNCTIONS", Difficulty = 4, QuestionType = QuestionType.ShortAnswer, LanguageCode = "vi", MaxScore = 40, EstimatedTimeSeconds = 300,
            QuestionText = "Tìm tọa độ đỉnh của parabol y = -2x^2 + 4x - 1.", CorrectAnswer = "(1, 1)",
            Solution = "Tọa độ đỉnh I(x, y). Hoành độ x = -b / (2a) = -4 / (2 * -2) = 1. Tung độ y = -2(1)^2 + 4(1) - 1 = -2 + 4 - 1 = 1.",
            ExpectedReasoning = "Áp dụng công thức hoành độ đỉnh -b/2a. Thay hoành độ vào hàm số để tìm tung độ.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Công thức x = -b/2a", "Thay x vào tính y" }, CommonErrors = new List<string> { "Sai dấu b", "Sai dấu khi thay số" }, ScoringNotes = "Nêu đúng hoành độ được 50% điểm" }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "MATH-FUNC-05", TopicCode = "MATH-FUNCTIONS", Difficulty = 5, QuestionType = QuestionType.Essay, LanguageCode = "vi", MaxScore = 50, EstimatedTimeSeconds = 600,
            QuestionText = "Chứng minh hàm số y = x^3 - 3x nghịch biến trên khoảng (-1; 1).", CorrectAnswer = "Hàm số nghịch biến trên (-1; 1) do đạo hàm y' = 3x^2 - 3 < 0 với mọi x thuộc khoảng này.",
            Solution = "Tập xác định D = R. Đạo hàm y' = 3x^2 - 3. Xét y' < 0 <=> 3x^2 - 3 < 0 <=> x^2 < 1 <=> -1 < x < 1. Vì y' < 0 với mọi x thuộc (-1; 1) nên hàm số nghịch biến trên khoảng này.",
            ExpectedReasoning = "Sử dụng đạo hàm để xét chiều biến thiên. Tính y', giải bất phương trình y' < 0 và kết luận.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Tính y'", "Giải y' < 0", "Kết luận" }, CommonErrors = new List<string> { "Tính đạo hàm sai", "Xét dấu tam thức sai" }, ScoringNotes = "Trừ 20% nếu không kết luận" }
        });

        // MATH-EXP-LOG (Difficulty 1-5)
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "MATH-EXPLOG-01", TopicCode = "MATH-EXP-LOG", Difficulty = 1, QuestionType = QuestionType.MultipleChoice, LanguageCode = "vi", MaxScore = 10, EstimatedTimeSeconds = 60,
            QuestionText = "Tập xác định của hàm số y = log_2(x - 1) là:",
            CorrectAnswer = "B", Solution = "Hàm logarit log_a(f(x)) xác định khi f(x) > 0. Ta có x - 1 > 0 <=> x > 1. Vậy D = (1; +vô cùng).",
            ExpectedReasoning = "Học sinh nhớ điều kiện tồn tại của biểu thức logarit là biểu thức dưới dấu logarit phải dương.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "x - 1 > 0" }, CommonErrors = new List<string> { "Cho x - 1 >= 0" }, ScoringNotes = "Đúng / Sai" },
            Options = new List<OptionTemplate> {
                new OptionTemplate { Label = "A", Text = "[1; +vô cùng)", IsCorrect = false }, new OptionTemplate { Label = "B", Text = "(1; +vô cùng)", IsCorrect = true },
                new OptionTemplate { Label = "C", Text = "(-vô cùng; 1)", IsCorrect = false }, new OptionTemplate { Label = "D", Text = "R \\ {1}", IsCorrect = false }
            }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "MATH-EXPLOG-02", TopicCode = "MATH-EXP-LOG", Difficulty = 2, QuestionType = QuestionType.ShortAnswer, LanguageCode = "vi", MaxScore = 20, EstimatedTimeSeconds = 120,
            QuestionText = "Tính giá trị biểu thức A = log_3(9) + log_2(8).", CorrectAnswer = "5",
            Solution = "A = log_3(3^2) + log_2(2^3) = 2 * log_3(3) + 3 * log_2(2) = 2 * 1 + 3 * 1 = 5.",
            ExpectedReasoning = "Biến đổi số nguyên dưới dấu logarit thành lũy thừa của cơ số tương ứng. Sử dụng tính chất log_a(a^n) = n.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "log_3(9)=2", "log_2(8)=3" }, CommonErrors = new List<string> { "Nhầm phép nhân logarit" }, ScoringNotes = "Tính đúng từng phần được 50%" }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "MATH-EXPLOG-03", TopicCode = "MATH-EXP-LOG", Difficulty = 3, QuestionType = QuestionType.MultipleChoice, LanguageCode = "vi", MaxScore = 30, EstimatedTimeSeconds = 180,
            QuestionText = "Nghiệm của phương trình 3^(x-1) = 27 là:",
            CorrectAnswer = "D", Solution = "Ta có 27 = 3^3. Phương trình trở thành 3^(x-1) = 3^3. Suy ra x - 1 = 3 <=> x = 4.",
            ExpectedReasoning = "Đưa hai vế về cùng cơ số 3. Sau đó cân bằng số mũ.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Đưa về cơ số 3" }, CommonErrors = new List<string> { "Sai phép tính cộng 3+1" }, ScoringNotes = "Đúng / Sai" },
            Options = new List<OptionTemplate> {
                new OptionTemplate { Label = "A", Text = "2", IsCorrect = false }, new OptionTemplate { Label = "B", Text = "3", IsCorrect = false },
                new OptionTemplate { Label = "C", Text = "5", IsCorrect = false }, new OptionTemplate { Label = "D", Text = "4", IsCorrect = true }
            }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "MATH-EXPLOG-04", TopicCode = "MATH-EXP-LOG", Difficulty = 4, QuestionType = QuestionType.ShortAnswer, LanguageCode = "vi", MaxScore = 40, EstimatedTimeSeconds = 300,
            QuestionText = "Tìm nghiệm của phương trình log_2(x) + log_2(x - 2) = 3.", CorrectAnswer = "4",
            Solution = "Điều kiện x > 2. Phương trình <=> log_2(x(x - 2)) = 3 <=> x(x - 2) = 2^3 = 8 <=> x^2 - 2x - 8 = 0. Nghiệm x = 4 (nhận) hoặc x = -2 (loại).",
            ExpectedReasoning = "Đặt điều kiện. Dùng công thức tổng hai logarit cùng cơ số. Giải phương trình bậc 2 và đối chiếu điều kiện.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Điều kiện", "Tổng log", "Loại nghiệm" }, CommonErrors = new List<string> { "Quên điều kiện và nhận x=-2" }, ScoringNotes = "Quên loại x=-2 trừ 50%" }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "MATH-EXPLOG-05", TopicCode = "MATH-EXP-LOG", Difficulty = 5, QuestionType = QuestionType.Essay, LanguageCode = "vi", MaxScore = 50, EstimatedTimeSeconds = 600,
            QuestionText = "Giải bất phương trình (1/2)^(x^2 - x) >= 1/4.", CorrectAnswer = "[-1, 2]",
            Solution = "1/4 = (1/2)^2. BPT <=> (1/2)^(x^2 - x) >= (1/2)^2. Vì cơ số 1/2 < 1 nên x^2 - x <= 2 <=> x^2 - x - 2 <= 0. Giải BPT bậc 2 ta được -1 <= x <= 2. Tập nghiệm S = [-1; 2].",
            ExpectedReasoning = "Biến đổi vế phải về cùng cơ số. Sử dụng tính chất hàm số mũ cơ số nhỏ hơn 1 nghịch biến (đổi chiều). Giải bất phương trình bậc 2.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Đổi chiều BPT", "Giải BPT bậc 2" }, CommonErrors = new List<string> { "Không đổi chiều BPT" }, ScoringNotes = "Quên đổi chiều cho 0 điểm" }
        });

        // MATH-ANTIDERIVATIVE (Difficulty 1-5)
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "MATH-ANTI-01", TopicCode = "MATH-ANTIDERIVATIVE", Difficulty = 1, QuestionType = QuestionType.MultipleChoice, LanguageCode = "vi", MaxScore = 10, EstimatedTimeSeconds = 60,
            QuestionText = "Họ tất cả các nguyên hàm của hàm số f(x) = 2x là:",
            CorrectAnswer = "A", Solution = "Nguyên hàm của x^n là (x^(n+1))/(n+1). Do đó, nguyên hàm của 2x là 2 * (x^2)/2 + C = x^2 + C.",
            ExpectedReasoning = "Sử dụng bảng nguyên hàm cơ bản.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Nguyên hàm x là x^2/2" }, CommonErrors = new List<string> { "Nhầm sang đạo hàm" }, ScoringNotes = "Đúng / Sai" },
            Options = new List<OptionTemplate> {
                new OptionTemplate { Label = "A", Text = "x^2 + C", IsCorrect = true }, new OptionTemplate { Label = "B", Text = "2x^2 + C", IsCorrect = false },
                new OptionTemplate { Label = "C", Text = "x + C", IsCorrect = false }, new OptionTemplate { Label = "D", Text = "2 + C", IsCorrect = false }
            }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "MATH-ANTI-02", TopicCode = "MATH-ANTIDERIVATIVE", Difficulty = 2, QuestionType = QuestionType.ShortAnswer, LanguageCode = "vi", MaxScore = 20, EstimatedTimeSeconds = 120,
            QuestionText = "Tính tích phân từ 0 đến 1 của hàm số f(x) = 3x^2.", CorrectAnswer = "1",
            Solution = "Nguyên hàm của 3x^2 là x^3. Tích phân từ 0 đến 1 bằng 1^3 - 0^3 = 1.",
            ExpectedReasoning = "Tìm nguyên hàm, sau đó áp dụng công thức Newton-Leibniz.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "F(x) = x^3", "F(1) - F(0)" }, CommonErrors = new List<string> { "Lỗi phép trừ" }, ScoringNotes = "Tìm đúng F(x) được 50%" }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "MATH-ANTI-03", TopicCode = "MATH-ANTIDERIVATIVE", Difficulty = 3, QuestionType = QuestionType.MultipleChoice, LanguageCode = "vi", MaxScore = 30, EstimatedTimeSeconds = 180,
            QuestionText = "Họ nguyên hàm của hàm số f(x) = cos(x) + sin(x) là:",
            CorrectAnswer = "C", Solution = "Nguyên hàm của cos(x) là sin(x), nguyên hàm của sin(x) là -cos(x). Vậy F(x) = sin(x) - cos(x) + C.",
            ExpectedReasoning = "Biết nguyên hàm cơ bản của các hàm lượng giác.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Nguyên hàm lượng giác" }, CommonErrors = new List<string> { "Lộn dấu - cho hàm sin" }, ScoringNotes = "Đúng / Sai" },
            Options = new List<OptionTemplate> {
                new OptionTemplate { Label = "A", Text = "sin(x) + cos(x) + C", IsCorrect = false }, new OptionTemplate { Label = "B", Text = "-sin(x) - cos(x) + C", IsCorrect = false },
                new OptionTemplate { Label = "C", Text = "sin(x) - cos(x) + C", IsCorrect = true }, new OptionTemplate { Label = "D", Text = "-sin(x) + cos(x) + C", IsCorrect = false }
            }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "MATH-ANTI-04", TopicCode = "MATH-ANTIDERIVATIVE", Difficulty = 4, QuestionType = QuestionType.ShortAnswer, LanguageCode = "vi", MaxScore = 40, EstimatedTimeSeconds = 300,
            QuestionText = "Dùng phương pháp đổi biến, tìm nguyên hàm của f(x) = 2x * e^(x^2).", CorrectAnswer = "e^(x^2) + C",
            Solution = "Đặt u = x^2, suy ra du = 2x dx. Nguyên hàm trở thành nguyên hàm của e^u du, kết quả là e^u + C. Thay u = x^2 ta được e^(x^2) + C.",
            ExpectedReasoning = "Áp dụng phương pháp đổi biến số để đưa tích phân về dạng cơ bản.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "u = x^2", "du = 2xdx", "e^u" }, CommonErrors = new List<string> { "Quên đổi lại biến x", "Quên hệ số" }, ScoringNotes = "Quên +C trừ 10%" }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "MATH-ANTI-05", TopicCode = "MATH-ANTIDERIVATIVE", Difficulty = 5, QuestionType = QuestionType.Essay, LanguageCode = "vi", MaxScore = 50, EstimatedTimeSeconds = 600,
            QuestionText = "Dùng phương pháp nguyên hàm từng phần, tính nguyên hàm của f(x) = x * ln(x).", CorrectAnswer = "(x^2/2)ln(x) - x^2/4 + C",
            Solution = "Đặt u = ln(x) => du = 1/x dx. Đặt dv = x dx => v = x^2/2. Áp dụng công thức nguyên hàm từng phần uv - tích phân v du. Ta có (x^2/2)ln(x) - tích phân (x^2/2)*(1/x) dx = (x^2/2)ln(x) - tích phân (x/2) dx = (x^2/2)ln(x) - x^2/4 + C.",
            ExpectedReasoning = "Chọn chính xác u và dv cho phương pháp từng phần. Nhớ công thức từng phần và tính chính xác các tích phân.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "u = ln(x)", "v = x^2/2", "Công thức uv-vdu" }, CommonErrors = new List<string> { "Chọn ngược u và dv", "Sai dấu tích phân" }, ScoringNotes = "Tính sai tích phân phần sau trừ 30%" }
        });

        // ENG-TENSES (Difficulty 1-5)
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "ENG-TENSE-01", TopicCode = "ENG-TENSES", Difficulty = 1, QuestionType = QuestionType.MultipleChoice, LanguageCode = "en", MaxScore = 10, EstimatedTimeSeconds = 60,
            QuestionText = "I _____ to school every day.",
            CorrectAnswer = "A", Solution = "The phrase 'every day' indicates a routine, so we use the Present Simple tense. 'I' goes with the base form 'go'.",
            ExpectedReasoning = "Identify the signal word 'every day' and apply the present simple tense rule for the first person singular subject.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Present simple for routine" }, CommonErrors = new List<string> { "Using continuous form" }, ScoringNotes = "Correct/Incorrect" },
            Options = new List<OptionTemplate> {
                new OptionTemplate { Label = "A", Text = "go", IsCorrect = true }, new OptionTemplate { Label = "B", Text = "am going", IsCorrect = false },
                new OptionTemplate { Label = "C", Text = "went", IsCorrect = false }, new OptionTemplate { Label = "D", Text = "goes", IsCorrect = false }
            }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "ENG-TENSE-02", TopicCode = "ENG-TENSES", Difficulty = 2, QuestionType = QuestionType.ShortAnswer, LanguageCode = "en", MaxScore = 20, EstimatedTimeSeconds = 120,
            QuestionText = "Write the correct past tense of the verb 'buy'.", CorrectAnswer = "bought",
            Solution = "The verb 'buy' is an irregular verb. Its past tense is 'bought'.",
            ExpectedReasoning = "Recall the irregular verb paradigm for 'buy'.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Irregular verb bought" }, CommonErrors = new List<string> { "buyed", "boughted", "brought" }, ScoringNotes = "Spelling must be exact" }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "ENG-TENSE-03", TopicCode = "ENG-TENSES", Difficulty = 3, QuestionType = QuestionType.MultipleChoice, LanguageCode = "en", MaxScore = 30, EstimatedTimeSeconds = 180,
            QuestionText = "By the time we arrived at the station, the train _____.",
            CorrectAnswer = "C", Solution = "The action of the train leaving happened before another action in the past ('arrived'). Therefore, we use the Past Perfect tense 'had left'.",
            ExpectedReasoning = "Understand the sequence of events in the past and use past perfect for the earlier event.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Past Perfect for earlier past event" }, CommonErrors = new List<string> { "Using past simple for both" }, ScoringNotes = "Correct/Incorrect" },
            Options = new List<OptionTemplate> {
                new OptionTemplate { Label = "A", Text = "leaves", IsCorrect = false }, new OptionTemplate { Label = "B", Text = "left", IsCorrect = false },
                new OptionTemplate { Label = "C", Text = "had left", IsCorrect = true }, new OptionTemplate { Label = "D", Text = "has left", IsCorrect = false }
            }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "ENG-TENSE-04", TopicCode = "ENG-TENSES", Difficulty = 4, QuestionType = QuestionType.ShortAnswer, LanguageCode = "en", MaxScore = 40, EstimatedTimeSeconds = 300,
            QuestionText = "Rewrite the sentence using Present Perfect Continuous: 'She started working here three years ago and she is still working here.' (Start with: She...)", CorrectAnswer = "She has been working here for three years.",
            Solution = "To express an action starting in the past and continuing to the present, we use Present Perfect Continuous. Since it's a duration of three years, we use 'for'. 'She has been working here for three years.'",
            ExpectedReasoning = "Combine two facts into one present perfect continuous sentence. Choose 'for' instead of 'since'.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "has been working", "for three years" }, CommonErrors = new List<string> { "Using since three years", "Using has worked" }, ScoringNotes = "Accept minor punctuation variations" }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "ENG-TENSE-05", TopicCode = "ENG-TENSES", Difficulty = 5, QuestionType = QuestionType.Essay, LanguageCode = "en", MaxScore = 50, EstimatedTimeSeconds = 600,
            QuestionText = "Explain the difference between Present Perfect and Past Simple tenses. Provide one example for each.", CorrectAnswer = "Past Simple is for completed past actions (I visited). Present Perfect is for actions continuing or with present results (I have visited).",
            Solution = "Past Simple is used for completed actions at a specific time in the past (e.g., 'I visited Paris in 2010'). Present Perfect is used for actions that happened at an unspecified time before now, or actions that started in the past and continue to the present, focusing on the result (e.g., 'I have visited Paris three times').",
            ExpectedReasoning = "Clearly state the rule regarding specific versus unspecified time or completed versus continuing relevance. Provide grammatically correct examples.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Specific vs unspecified time", "Examples for both" }, CommonErrors = new List<string> { "Vague explanation", "Incorrect examples" }, ScoringNotes = "Deduct 50% if examples are missing" }
        });

        // ENG-RELATIVE-CLAUSES (Difficulty 1-5)
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "ENG-REL-01", TopicCode = "ENG-RELATIVE-CLAUSES", Difficulty = 1, QuestionType = QuestionType.MultipleChoice, LanguageCode = "en", MaxScore = 10, EstimatedTimeSeconds = 60,
            QuestionText = "The book _____ I bought yesterday is very interesting.",
            CorrectAnswer = "B", Solution = "The relative pronoun must refer to 'The book' (a thing). 'which' or 'that' is appropriate. 'who' is for people, 'where' is for places.",
            ExpectedReasoning = "Identify the antecedent 'book' as a non-human object and select the correct relative pronoun.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Relative pronoun for things" }, CommonErrors = new List<string> { "Using who" }, ScoringNotes = "Correct/Incorrect" },
            Options = new List<OptionTemplate> {
                new OptionTemplate { Label = "A", Text = "who", IsCorrect = false }, new OptionTemplate { Label = "B", Text = "which", IsCorrect = true },
                new OptionTemplate { Label = "C", Text = "whom", IsCorrect = false }, new OptionTemplate { Label = "D", Text = "where", IsCorrect = false }
            }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "ENG-REL-02", TopicCode = "ENG-RELATIVE-CLAUSES", Difficulty = 2, QuestionType = QuestionType.ShortAnswer, LanguageCode = "en", MaxScore = 20, EstimatedTimeSeconds = 120,
            QuestionText = "Combine these sentences using a relative pronoun: 'The girl is my sister. She is wearing a red dress.' (Start with: The girl...)", CorrectAnswer = "The girl who is wearing a red dress is my sister.",
            Solution = "The common noun is 'The girl'. We use 'who' to replace 'She' in the second sentence and embed it as a relative clause.",
            ExpectedReasoning = "Identify the common subject. Use 'who' for a person in subject position. Place the relative clause immediately after the antecedent.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "who is wearing", "placed correctly" }, CommonErrors = new List<string> { "Using which", "Misplaced clause" }, ScoringNotes = "Deduct 20% for minor typos" }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "ENG-REL-03", TopicCode = "ENG-RELATIVE-CLAUSES", Difficulty = 3, QuestionType = QuestionType.MultipleChoice, LanguageCode = "en", MaxScore = 30, EstimatedTimeSeconds = 180,
            QuestionText = "My father, _____ is a doctor, works in a large hospital.",
            CorrectAnswer = "A", Solution = "This is a non-defining relative clause, giving extra information about a specific person ('My father'). We must use 'who' after the comma. 'that' cannot be used in non-defining clauses.",
            ExpectedReasoning = "Distinguish between defining and non-defining clauses. Apply the rule that 'that' is not used after a comma.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Non-defining clause", "who not that" }, CommonErrors = new List<string> { "Choosing that" }, ScoringNotes = "Correct/Incorrect" },
            Options = new List<OptionTemplate> {
                new OptionTemplate { Label = "A", Text = "who", IsCorrect = true }, new OptionTemplate { Label = "B", Text = "that", IsCorrect = false },
                new OptionTemplate { Label = "C", Text = "whom", IsCorrect = false }, new OptionTemplate { Label = "D", Text = "which", IsCorrect = false }
            }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "ENG-REL-04", TopicCode = "ENG-RELATIVE-CLAUSES", Difficulty = 4, QuestionType = QuestionType.ShortAnswer, LanguageCode = "en", MaxScore = 40, EstimatedTimeSeconds = 300,
            QuestionText = "Reduce the relative clause in this sentence: 'The students who were punished yesterday are very angry.'", CorrectAnswer = "The students punished yesterday are very angry.",
            Solution = "When reducing a passive relative clause ('who were punished'), we remove the relative pronoun ('who') and the 'be' verb ('were'), leaving only the past participle ('punished').",
            ExpectedReasoning = "Identify the passive structure inside the relative clause and apply the past participle reduction rule.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Remove who were", "Keep past participle" }, CommonErrors = new List<string> { "Using present participle punishing" }, ScoringNotes = "Only exact reduced phrase is accepted" }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "ENG-REL-05", TopicCode = "ENG-RELATIVE-CLAUSES", Difficulty = 5, QuestionType = QuestionType.Essay, LanguageCode = "en", MaxScore = 50, EstimatedTimeSeconds = 600,
            QuestionText = "Explain the rules for omitting the relative pronoun in a defining relative clause. Provide two examples: one where it can be omitted and one where it cannot.", CorrectAnswer = "Omitted when it acts as an object (The book I bought). Not omitted when it acts as a subject (The man who called).",
            Solution = "A relative pronoun can be omitted in a defining relative clause if it is the object of the verb in the clause (e.g., 'The man (who) I met yesterday is nice.'). It cannot be omitted if it is the subject of the clause (e.g., 'The man who lives next door is nice.'). It also cannot be omitted in non-defining clauses.",
            ExpectedReasoning = "Distinguish between subject and object relative pronouns. State the rule clearly. Construct accurate examples to illustrate both cases.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Object vs Subject rule", "Two examples" }, CommonErrors = new List<string> { "Incorrect rules", "Examples don't match rules" }, ScoringNotes = "Missing one example deducts 30%" }
        });

        // ENG-CONTEXT-VOCAB (Difficulty 1-5)
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "ENG-VOCAB-01", TopicCode = "ENG-CONTEXT-VOCAB", Difficulty = 1, QuestionType = QuestionType.MultipleChoice, LanguageCode = "en", MaxScore = 10, EstimatedTimeSeconds = 60,
            QuestionText = "He was so _____ that he drank three glasses of water immediately.",
            CorrectAnswer = "C", Solution = "Drinking a lot of water indicates the person needs hydration. The correct adjective is 'thirsty'.",
            ExpectedReasoning = "Use context clues ('drank three glasses of water') to infer the physical state.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Context clue for thirsty" }, CommonErrors = new List<string> { "Confusing with hungry" }, ScoringNotes = "Correct/Incorrect" },
            Options = new List<OptionTemplate> {
                new OptionTemplate { Label = "A", Text = "hungry", IsCorrect = false }, new OptionTemplate { Label = "B", Text = "tired", IsCorrect = false },
                new OptionTemplate { Label = "C", Text = "thirsty", IsCorrect = true }, new OptionTemplate { Label = "D", Text = "happy", IsCorrect = false }
            }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "ENG-VOCAB-02", TopicCode = "ENG-CONTEXT-VOCAB", Difficulty = 2, QuestionType = QuestionType.ShortAnswer, LanguageCode = "en", MaxScore = 20, EstimatedTimeSeconds = 120,
            QuestionText = "What is the noun form of the verb 'decide'?", CorrectAnswer = "decision",
            Solution = "The word 'decide' is a verb. Adding the suffix '-sion' creates the noun 'decision'.",
            ExpectedReasoning = "Apply word formation rules to derive a noun from a verb.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "decision" }, CommonErrors = new List<string> { "decidement", "decisive" }, ScoringNotes = "Spelling must be exact" }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "ENG-VOCAB-03", TopicCode = "ENG-CONTEXT-VOCAB", Difficulty = 3, QuestionType = QuestionType.MultipleChoice, LanguageCode = "en", MaxScore = 30, EstimatedTimeSeconds = 180,
            QuestionText = "Despite the heavy rain, they decided to _____ with the outdoor event.",
            CorrectAnswer = "D", Solution = "The phrasal verb 'go ahead' means to proceed with something, especially despite difficulties. 'go on' means continue, but 'go ahead with' is the specific collocation.",
            ExpectedReasoning = "Understand phrasal verbs and collocations. Select the one that means 'proceed'.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Phrasal verb meaning proceed" }, CommonErrors = new List<string> { "go on with" }, ScoringNotes = "Correct/Incorrect" },
            Options = new List<OptionTemplate> {
                new OptionTemplate { Label = "A", Text = "go over", IsCorrect = false }, new OptionTemplate { Label = "B", Text = "go back", IsCorrect = false },
                new OptionTemplate { Label = "C", Text = "go through", IsCorrect = false }, new OptionTemplate { Label = "D", Text = "go ahead", IsCorrect = true }
            }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "ENG-VOCAB-04", TopicCode = "ENG-CONTEXT-VOCAB", Difficulty = 4, QuestionType = QuestionType.ShortAnswer, LanguageCode = "en", MaxScore = 40, EstimatedTimeSeconds = 300,
            QuestionText = "Find a single word synonym for the underlined phrase: The CEO's speech was 'very difficult to understand' because of the technical jargon.", CorrectAnswer = "incomprehensible",
            Solution = "Synonyms for 'very difficult to understand' include 'incomprehensible', 'obscure', or 'unintelligible'.",
            ExpectedReasoning = "Identify advanced vocabulary that encapsulates a descriptive phrase.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "incomprehensible", "unintelligible", "obscure" }, CommonErrors = new List<string> { "hard", "confusing" }, ScoringNotes = "Accept valid advanced synonyms" }
        });
        templates.Add(new QuestionTemplate
        {
            LogicalCode = "ENG-VOCAB-05", TopicCode = "ENG-CONTEXT-VOCAB", Difficulty = 5, QuestionType = QuestionType.Essay, LanguageCode = "en", MaxScore = 50, EstimatedTimeSeconds = 600,
            QuestionText = "Read this sentence: 'The politician tried to mitigate the damage caused by his controversial statement.' Explain the meaning of 'mitigate' in this context and use it in a new sentence of your own.", CorrectAnswer = "To make less severe. Example: The city built a wall to mitigate flood damage.",
            Solution = "In this context, 'mitigate' means to make something less severe, harmful, or painful. Example sentence: The government introduced new measures to mitigate the effects of the economic crisis.",
            ExpectedReasoning = "Deduce the meaning from context (damage control). Articulate the definition clearly and construct a grammatically correct sentence demonstrating the word's proper usage.",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new List<string> { "Definition: lessen severity", "Valid example sentence" }, CommonErrors = new List<string> { "Define as remove completely", "Incorrect sentence context" }, ScoringNotes = "Deduct 50% if sentence is missing" }
        });

        return templates;
    }
}
