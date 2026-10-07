using System.Diagnostics;
using System.Text.Json;
using EduTwin.API.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.Contracts.CurriculumAndQuestions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

// Opt-in, bounded provider experiment. Synthetic cases only; no login, SQL, or student data.
if (!args.Contains("--live"))
{
    Console.WriteLine("Use --live --model <model> [--key-index 0] [--batch-size 3] [--expanded]. Basic: 5 cases, expanded: 13 cases; max 5 calls/model.");
    return;
}
string Argument(string name, string fallback) => Array.IndexOf(args, name) is var i && i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
try
{
    var env = File.ReadLines(Path.Combine(Directory.GetCurrentDirectory(), ".env"))
        .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#') && line.Contains('='))
        .Select(line => (key: line[..line.IndexOf('=')].Trim(), value: line[(line.IndexOf('=') + 1)..].Trim()))
        .ToDictionary(x => x.key, x => x.value);
    var configured = new GeminiOptions { ListKey = env.GetValueOrDefault("GEMINI_LIST_KEY"),
        ApiKey = env.GetValueOrDefault("Gemini__ApiKey"), BackupKeys = env.GetValueOrDefault("Gemini__BackupKeys"),
        BackupKeys_2 = env.GetValueOrDefault("Gemini__BackupKeys_2") };
    var model = Argument("--model", env.GetValueOrDefault("Gemini__Model") ?? "gemini-2.5-flash");
    var keyIndex = int.Parse(Argument("--key-index", "0"));
    var size = int.Parse(Argument("--batch-size", "3"));
    if (size is < 3 or > 5) throw new InvalidOperationException("Use batch size 3 through 5.");
    var gemini = new GeminiOptions { ApiKey = configured.GetAllApiKeys()[keyIndex], Model = model, Timeout = TimeSpan.FromSeconds(60) };
    var providerLog = new SafeStatusLogger();
    using var client = new GoogleGenAIGenerateContentClient(Options.Create(gemini), providerLog);
    var settings = new AIGradingOptions { MicroBatchEnabled = true, BatchSize = size };
    var executor = new ReasoningBatchExecutor(Options.Create(gemini), Options.Create(settings), client, null!,
        new(), new(), new StrictAIAnalysisResponseParser(new AnalyzeReasoningResponseValidator()));
    var reports = new List<object>();
    var calls = 0;
    var matches = 0;
    var cases = (args.Contains("--probe") ? Cases().Take(1) : Cases().Concat(args.Contains("--expanded") ? ExpandedCases() : [])).ToArray();
    if ((int)Math.Ceiling(cases.Length / (double)size) > 5) throw new InvalidOperationException("Call cap exceeded.");
    foreach (var group in cases.Chunk(size))
    {
        var started = Stopwatch.GetTimestamp();
        IReadOnlyDictionary<string, ReasoningBatchResult> result;
        try { result = await executor.ExecuteAsync(group.Select(c => new ReasoningBatchItem(c.Id, c.Request)).ToArray(), default); }
        catch
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new { model, keyIndex, status = providerLog.LastStatus, failed = true }));
            throw;
        }
        calls++;
        foreach (var test in group)
        {
            var item = result[test.Id]; var r = item.Response;
            if (r?.AnswerAssessment == test.Answer && r.ReasoningVerdict == test.Reasoning) matches++;
            reports.Add(new
            {
                caseId = test.Id, expectedAnswer = test.Answer, expectedReasoning = test.Reasoning,
                validContract = r is not null, observationsMatch = r?.AnswerAssessment == test.Answer && r?.ReasoningVerdict == test.Reasoning,
                response = r, errorCode = item.Error is AIAnalysisValidationException e ? e.ErrorCode : item.Error?.GetType().Name
            });
        }
        Console.WriteLine(JsonSerializer.Serialize(new { model, questions = group.Length, milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds }));
    }
    var directory = Path.Combine(Directory.GetCurrentDirectory(), "storage", "verification"); Directory.CreateDirectory(directory);
    var safeModel = System.Text.RegularExpressions.Regex.Replace(model, @"[^A-Za-z0-9._-]", "_");
    var path = Path.Combine(directory, $"AI-GRADING-MICROBATCH-{safeModel}-key{keyIndex+1}-{size}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json");
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { syntheticOnly = true, model, keyIndex, size, calls, matches, total = cases.Length, reports }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine(JsonSerializer.Serialize(new { model, keyIndex, calls, matches, total = cases.Length, report = path, productionModelUnchanged = true }));
    if (matches != cases.Length) Environment.ExitCode = 1;
}
catch (Exception e)
{
    // Exception messages from provider SDKs can contain credentials/request bodies.
    Console.Error.WriteLine(JsonSerializer.Serialize(new { failed = true, errorType = e.GetType().Name,
        errorCode = e is GeminiAdapterException a ? a.ErrorCode : e is AIAnalysisValidationException v ? v.ErrorCode
            : e is AIAnalysisDeferredException d ? d.ErrorCode : null }));
    Environment.ExitCode = 1;
}

static IEnumerable<Case> Cases()
{
    yield return Build("mcq-valid", "Cho y=2x+1, tính y tại x=3", "7", "7", "Thay x=3: y=2*3+1=7.", "Thay giá trị", "Correct", "Valid", QuestionType.MultipleChoice);
    yield return Build("equivalent-domain", "Tìm tập xác định của y=1/(x-2)", @"R\{2}", @"D=\mathbb{R}\setminus\{2\}", "Mẫu số khác 0 nên x-2 khác 0, suy ra x khác 2.", "Mẫu khác không", "Correct", "Valid", QuestionType.ShortAnswer);
    yield return Build("digit-cancel-fallacy", "Rút gọn phân số 16/64 và giải thích", "1/4", "1/4", "Gạch bỏ chữ số 6 ở tử 16 và mẫu 64, còn 1/4.", "Chia tử và mẫu cho 16.", "Correct", "Invalid");
    yield return Build("alternative-method", "Giải x^2-5x+6=0", "x=2 hoặc x=3", "x=2 hoặc x=3", "Ta phân tích (x-2)(x-3)=0 nên x=2 hoặc x=3.", "Dùng công thức nghiệm với delta=25-24=1.", "Correct", "Valid");
    yield return Build("missing-root", "Giải x^2=4 trên tập số thực", "x=2 hoặc x=-2", "x=2", "Lấy căn hai vế được x=2.", "x^2=4 tương đương x=2 hoặc x=-2.", "Incorrect", "Invalid");
}
static IEnumerable<Case> ExpandedCases()
{
    yield return Build("integration-parts", "Tính nguyên hàm của x ln(x) với x>0", "(x^2/2)ln(x)-x^2/4+C",
        @"\frac{x^2}{2}\ln(x)-\frac{x^2}{4}+C", "Đặt u=ln(x), dv=x dx; du=dx/x, v=x^2/2. I=(x^2/2)ln(x)-(1/2)∫x dx=(x^2/2)ln(x)-x^2/4+C.",
        "Dùng nguyên hàm từng phần trên miền x>0.", "Correct", "Valid");
    yield return Build("extraneous-root", "Giải sqrt(x+1)=x-1 trên tập số thực", "x=3", "x=0 hoặc x=3",
        "Bình phương hai vế: x+1=x^2-2x+1; x(x-3)=0, suy ra x=0 hoặc x=3.",
        "Điều kiện x>=1. Bình phương được x=0 hoặc 3, loại x=0, nhận x=3.", "Incorrect", "Invalid");
    yield return Build("division-by-zero", "Giải x(x-1)=0 trên tập số thực", "x=0 hoặc x=1", "x=1",
        "Chia cả hai vế cho x, ta có x-1=0 nên x=1.", "Tích bằng 0 nên x=0 hoặc x=1.", "Incorrect", "Invalid");
    yield return Build("physics-units", "Vật rơi tự do từ độ cao 5 m, g=10 m/s^2. Tính vận tốc chạm đất, bỏ qua cản không khí.", "10 m/s", "10 km/h",
        "v^2=2gh=2*10*5=100. Suy ra v=10 km/h.", "Bảo toàn cơ năng: v=sqrt(2gh)=10 m/s.", "Incorrect", "Invalid", node: "Đơn vị và bảo toàn cơ năng");
    yield return Build("english-subject-verb", "Điền từ: He ___ to school every day. A: go; B: goes; C: going; D: gone.", "B", "B",
        "Every day là dấu hiệu hiện tại đơn. Chủ ngữ He là ngôi thứ ba số ít nên động từ go thêm es thành goes.",
        "Đáp án B, goes, theo quy tắc hòa hợp chủ ngữ động từ ở hiện tại đơn.", "Correct", "Valid", QuestionType.MultipleChoice, "Hòa hợp chủ ngữ động từ");
    yield return Build("chemistry-balance", "Cân bằng phản ứng H2 + O2 -> H2O bằng hệ số nguyên tối giản.", "2H2 + O2 -> 2H2O", "2H2+O2→2H2O",
        "Đặt hệ số H2O là 2 để có hai nguyên tử O ở vế phải; giữ O2 là 1, đặt H2 là 2 để hai vế cùng bốn nguyên tử H.",
        "Bảo toàn H và O cho hệ số tối giản 2:1:2.", "Correct", "Valid", node: "Bảo toàn nguyên tố và cân bằng phản ứng");
    foreach (var valid in new[] { true, false })
    {
        var test = Build(valid ? "image-valid" : "image-invalid", "Tam giác vuông có hai cạnh góc vuông 3 và 4. Tìm cạnh huyền c. Toàn bộ lập luận học sinh nằm trong ảnh nháp đính kèm.",
            "5", "5", "", "Định lý Pythagore: c^2=3^2+4^2=25, c=5 vì c>0.", "Correct", valid ? "Valid" : "Invalid");
        var bytes = File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "storage", "verification", "ai-vision-fixtures",
            valid ? "triangle-valid.png" : "triangle-invalid.png"));
        yield return test with { Request = test.Request with { StudentSubmission = test.Request.StudentSubmission with
            { ReasoningText = null, ImageParts = [new(bytes, "image/png")] } } };
    }
}
static Case Build(string id, string question, string correct, string answer, string reasoning, string solution,
    string answerVerdict, string reasoningVerdict, QuestionType type = QuestionType.Essay,
    string node = "Điều kiện xác định và biến đổi tương đương") => new(id, new()
{
    SchemaVersion = AIAnalysisContract.SchemaVersion, Language = "vi",
    Question = new() { QuestionType = type, QuestionText = question, CorrectAnswer = correct, Solution = solution,
        AnswerEvaluationMode = type == QuestionType.ShortAnswer ? QuestionAnswerEvaluationMode.MathEquivalent
            : type == QuestionType.Essay ? QuestionAnswerEvaluationMode.Manual : QuestionAnswerEvaluationMode.TextExact,
        GradingCriteria = new() { SchemaVersion = "1", RequiredIdeas = [], CommonErrors = [], ScoringNotes = "Chấp nhận cách giải khác hợp lệ; không công nhận phép biến đổi sai." } },
    StudentSubmission = new() { FinalAnswer = answer, ReasoningText = reasoning, Confidence = 80 },
    AllowedKnowledgeNodes = [new() { NodeId = "1", NodeName = node }]
}, answerVerdict, reasoningVerdict);
sealed record Case(string Id, AnalyzeReasoningRequest Request, string Answer, string Reasoning);
sealed class SafeStatusLogger : ILogger<GoogleGenAIGenerateContentClient>
{
    public int? LastStatus { get; private set; }
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel level) => true;
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (state is IEnumerable<KeyValuePair<string, object?>> fields)
            foreach (var pair in fields) if (pair.Key == "Status" && pair.Value is int value) LastStatus = value;
        // Never call formatter: only the numeric HTTP status is retained.
    }
}
