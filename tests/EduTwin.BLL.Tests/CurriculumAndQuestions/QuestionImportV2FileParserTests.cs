using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using EduTwin.BLL.CurriculumAndQuestions.Import;
using Xunit;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public sealed class QuestionImportV2FileParserTests : IDisposable
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace M = "http://schemas.openxmlformats.org/officeDocument/2006/math";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private const string Subject = "0904f0c4-2901-4b47-8504-bbe84239cd1f";
    private readonly string _work = Path.Combine(Path.GetTempPath(), "edutwin-v2-parser-" + Guid.NewGuid().ToString("N"));
    public QuestionImportV2FileParserTests() => Directory.CreateDirectory(_work);
    public void Dispose() => Directory.Delete(_work, true);

    [Theory]
    [InlineData("frontend-template-v2.docx", "docx-1.0")]
    [InlineData("frontend-template-v2.zip", "media-1.0")]
    public async Task ActualFrontendGeneratedTemplatesRoundTrip(string fileName, string profile)
    {
        await using var input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "CurriculumAndQuestions", "Fixtures", "Import", fileName));
        var parsed = await Parse(input, fileName, profile);
        Assert.Equal(3, parsed.Content.Rows.Count);
        Assert.Equal(Guid.Parse(Subject), parsed.Content.SourceSubjectId);
        Assert.Equal(new[] { "MATH-DERIV", "MATH-FRAC", "MATH-GEOM" }, parsed.Content.Rows.Select(r => r.Values["TopicCode"]));
        Assert.All(parsed.Content.Rows, row => { Assert.Empty(row.Diagnostics); Assert.NotEmpty(row.CandidateId); Assert.NotNull(row.SourceLocation); });
        Assert.Single(parsed.MediaSources);
        Assert.All(parsed.MediaSources.Values, media =>
        {
            Assert.StartsWith(_work + Path.DirectorySeparatorChar, media.SourcePath, StringComparison.Ordinal);
            Assert.True(File.Exists(media.SourcePath));
            Assert.Equal(".tmp", Path.GetExtension(media.SourcePath));
        });
    }

    [Fact]
    public async Task PackageMapsExplicitImageRefAndKeepsDataFilePrivate()
    {
        using var input = Package(new() { ["questions.csv"] = Encoding.UTF8.GetBytes("Mã câu hỏi,Nội dung câu hỏi,Mã ảnh đề bài\nQ1,,media/a.png"), ["media/a.png"] = [1, 2, 3] });
        var parsed = await Parse(input, "questions.zip", "media-1.0");
        var row = Assert.Single(parsed.Content.Rows);
        Assert.Equal("media/a.png", row.Values["ImageRef"]);
        Assert.False(row.Values.ContainsKey("QuestionText"));
        Assert.Empty(row.Diagnostics);
        Assert.Equal("questions.csv", row.SourceLocation!.PartPath);
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(parsed.MediaSources["media/a.png"].SourcePath));
    }

    [Fact]
    public async Task V1StillRejectsImageRefData()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("QuestionText,ImageRef\nQuestion,media/a.png"));
        var parsed = await QuestionImportFileParser.ParseAsync(input, "questions.csv", false, default);
        Assert.Contains(Assert.Single(parsed.Rows).Diagnostics, d => d.Code == "UNKNOWN_COLUMN" && d.ColumnName == "ImageRef");
    }

    [Theory]
    [InlineData("../a.png", "UNSAFE_ARCHIVE_PATH")]
    [InlineData("media/../a.png", "UNSAFE_ARCHIVE_PATH")]
    [InlineData("media/a.png", "UNREFERENCED_MEDIA")]
    [InlineData("readme.txt", "UNSUPPORTED_PACKAGE_PART")]
    public async Task PackageRejectsUnsafeOrUnmappedParts(string path, string code)
    {
        using var input = Package(new() { ["questions.csv"] = Encoding.UTF8.GetBytes("QuestionText\nQuestion"), [path] = [1] });
        var error = await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(input, "questions.zip", "media-1.0"));
        Assert.Equal(code, error.Code);
    }

    [Fact]
    public async Task MissingImageIsScopedToCandidateAndNeverInvented()
    {
        using var input = Package(new() { ["questions.csv"] = Encoding.UTF8.GetBytes("QuestionKey,QuestionText,ImageRef\nQ1,Question,media/missing.png\nQ2,Other,") });
        var parsed = await Parse(input, "questions.zip", "media-1.0");
        Assert.Empty(parsed.MediaSources);
        Assert.Contains(parsed.Content.Rows[0].Diagnostics, d => d.Code == "IMAGE_NOT_FOUND");
        Assert.Empty(parsed.Content.Rows[1].Diagnostics);
    }

    [Fact]
    public async Task DocxPreservesSplitRunsNewlinesAndSourceLocation()
    {
        var question = Question("Q1", new XElement(W + "tc", new XElement(W + "p", Run("Tính $\\fr"), Run("ac{1}{2}$")), new XElement(W + "p", Run("Dòng hai"))));
        using var input = Docx(question);
        var parsed = await Parse(input);
        var row = Assert.Single(parsed.Content.Rows);
        Assert.Equal("Tính $\\frac{1}{2}$\nDòng hai", row.Values["QuestionText"]);
        Assert.Equal(1, row.SourceLocation!.QuestionIndex);
        Assert.Equal(2, row.SourceLocation.TableIndex);
        Assert.Equal("word/document.xml", row.SourceLocation.PartPath);
        Assert.Empty(row.Diagnostics);
    }

    [Fact]
    public async Task DocxConvertsInlineAndDisplayOmmlAndBareAutomaticAnswer()
    {
        var fraction = new XElement(M + "f", new XElement(M + "num", MathRun("1")), new XElement(M + "den", MathRun("2")));
        var question = Question("Q1", new XElement(W + "tc",
            new XElement(W + "p", Run("Tính "), new XElement(M + "oMath", fraction)),
            new XElement(W + "p", new XElement(M + "oMathPara", new XElement(M + "oMath", new XElement(fraction))))));
        question.Add(Field("Chế độ chấm", TextCell("NumericRational")));
        question.Add(Field("Đáp án đúng", new XElement(W + "tc", new XElement(W + "p", new XElement(M + "oMath", new XElement(fraction))))));
        using var input = Docx(question);
        var parsed = await Parse(input);
        var row = Assert.Single(parsed.Content.Rows);
        Assert.Contains("$\\frac{1}{2}$", row.Values["QuestionText"], StringComparison.Ordinal);
        Assert.Contains("$$\\frac{1}{2}$$", row.Values["QuestionText"], StringComparison.Ordinal);
        Assert.Equal("\\frac{1}{2}", row.Values["CorrectAnswer"]);
        Assert.Empty(row.Diagnostics);
    }

    [Theory]
    [InlineData("TextExact", "$x$")]
    [InlineData("", "$x$")]
    [InlineData("NumericRational", "x")]
    [InlineData("Manual", "$x$")]
    public async Task NativeMathAnswerMarkerSurvivesUntilEffectiveBatchModeValidation(string mode, string expected)
    {
        var question = Question("Q1", TextCell("Đề"));
        question.Add(Field("Chế độ chấm", TextCell(mode)));
        question.Add(Field("Đáp án đúng", new XElement(W + "tc", new XElement(W + "p", new XElement(M + "oMath", MathRun("x"))))));
        using var input = Docx(question);
        var row = Assert.Single((await Parse(input)).Content.Rows);
        Assert.True(row.CorrectAnswerContainsNativeMath);
        Assert.Equal(expected, row.Values["CorrectAnswer"]);
    }

    [Fact]
    public async Task PlainTextAnswerDoesNotReceiveNativeMathMarker()
    {
        var question = Question("Q1", new XElement(W + "tc", new XElement(W + "p", new XElement(M + "oMath", MathRun("x")))));
        question.Add(Field("Đáp án đúng", TextCell("$x$")));
        using var input = Docx(question);
        Assert.False(Assert.Single((await Parse(input)).Content.Rows).CorrectAnswerContainsNativeMath);
    }

    [Fact]
    public async Task UnsupportedOmmlIsScopedWithParagraphLocation()
    {
        var question = Question("Q1", new XElement(W + "tc", new XElement(W + "p", Run("Dòng một")),
            new XElement(W + "p", new XElement(M + "oMath", new XElement(M + "unknown", MathRun("x"))))));
        using var input = Docx(question, Question("Q2", TextCell("Câu khác")));
        var parsed = await Parse(input);
        var error = Assert.Single(parsed.Content.Rows[0].Diagnostics);
        Assert.Equal("UNSUPPORTED_OMML", error.Code);
        Assert.Equal(2, error.SourceLocation!.ParagraphIndex);
        Assert.Equal("QuestionText", error.SourceLocation.Field);
        Assert.Empty(parsed.Content.Rows[1].Diagnostics);
    }

    [Theory]
    [InlineData("crop", "UNSUPPORTED_IMAGE_TRANSFORM")]
    [InlineData("rotate", "UNSUPPORTED_IMAGE_TRANSFORM")]
    [InlineData("anchor", "UNSUPPORTED_IMAGE_LAYOUT")]
    public async Task DocxUnsupportedImageLayoutIsScopedWithoutDroppingIt(string transform, string code)
    {
        var drawing = Drawing();
        if (transform == "crop") drawing.Descendants(A + "blip").Single().AddAfterSelf(new XElement(A + "srcRect", new XAttribute("l", "1000")));
        if (transform == "rotate") drawing.Descendants(A + "blip").Single().AddAfterSelf(new XElement(A + "xfrm", new XAttribute("rot", "60000")));
        if (transform == "anchor") drawing.Element(Wp + "inline")!.Name = Wp + "anchor";
        var question = Question("Q1", TextCell("Có hình"));
        question.Add(Field("Ảnh đề bài", new XElement(W + "tc", new XElement(W + "p", new XElement(W + "r", drawing)))));
        using var input = DocxWithImage(question);
        var parsed = await Parse(input);
        Assert.Empty(parsed.MediaSources);
        Assert.Contains(parsed.Content.Rows[0].Diagnostics, e => e.Code == code && e.SourceLocation!.RelationshipId == "rImage");
    }

    [Theory]
    [InlineData("Mã câu hỏi", "DUPLICATE_FIELD")]
    [InlineData("CustomField", "UNKNOWN_FIELD")]
    public async Task DocxNeverSilentlyDropsUnknownOrDuplicateFields(string label, string code)
    {
        var question = Question("Q1", TextCell("Nội dung")); question.Add(Field(label, TextCell("Extra")));
        using var input = Docx(question);
        var parsed = await Parse(input);
        Assert.Contains(parsed.Content.Rows.Single().Diagnostics, e => e.Code == code);
    }

    [Fact]
    public async Task DocxRejectsFreeformBodyExternalRelationshipsAndUnsupportedVersion()
    {
        using var freeform = Docx(Question("Q1", TextCell("Đề")), new XElement(W + "p", Run("Câu tự do")));
        Assert.Equal("UNSUPPORTED_DOCX_BODY", (await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(freeform))).Code);
        using var external = DocxWithImage(Question("Q1", TextCell("Đề")), "<Relationships><Relationship Id='x' Target='https://example.test/a.png' TargetMode='External'/></Relationships>");
        Assert.Equal("EXTERNAL_REFERENCE", (await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(external))).Code);
        using var version = DocxParts(new XElement(W + "document", new XElement(W + "body", Metadata("1.0"), Question("Q1", TextCell("Đề")))));
        Assert.Equal("UNSUPPORTED_TEMPLATE_VERSION", (await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(version))).Code);
    }

    [Fact]
    public async Task CancellationAndProfileMismatchAreRejected()
    {
        using var input = Docx(Question("Q1", TextCell("Đề")));
        Assert.Equal("UNSUPPORTED_FILE_TYPE", (await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(input, "wrong.zip", "docx-1.0"))).Code);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => QuestionImportV2FileParser.ParseAsync(input, "q.docx", "docx-1.0", _work, cancel.Token));
    }

    [Fact]
    public async Task FiveHundredFullQuestionTablesFitV2XmlBudgetAndKeepUniqueLocations()
    {
        var questions = new List<XElement>();
        var keys = new[] { "TopicCode", "QuestionType", "GradeLevel", "Difficulty", "OptionA", "MisconceptionA", "OptionB", "MisconceptionB", "OptionC", "MisconceptionC", "OptionD", "MisconceptionD", "CorrectAnswer", "Solution", "ExpectedReasoning", "RequiredIdeas", "CommonErrors", "ScoringNotes", "MaxScore", "EstimatedTimeSeconds", "ReasoningRequired", "AnswerEvaluationMode", "Visibility", "LanguageCode" };
        for (var i = 0; i < 500; i++)
        {
            var question = Question($"Q{i:D3}", TextCell("Đề $x^2$"));
            foreach (var key in keys) question.Add(Field(key, TextCell("Value")));
            question.Add(Field("Ảnh đề bài", TextCell("")));
            questions.Add(question);
        }
        using var input = Docx(questions.ToArray());
        var parsed = await Parse(input);
        Assert.Equal(500, parsed.Content.Rows.Count);
        Assert.Equal(500, parsed.Content.Rows.Select(r => r.CandidateId).Distinct().Count());
        Assert.Equal(501, parsed.Content.Rows[^1].SourceLocation!.TableIndex);
        Assert.All(parsed.Content.Rows, r => Assert.Empty(r.Diagnostics));
    }

    [Fact]
    public async Task AggregateXmlNodesAreChargedBeforeAllocatingEachAdditionalPart()
    {
        // Three independent parts are individually below the 500,000-node ceiling.
        // A single shared one-million-node budget must reject the third DOM allocation.
        var part = Encoding.UTF8.GetBytes("<metadata>" + string.Concat(Enumerable.Repeat("<item/>", 340_000)) + "</metadata>");
        using var input = DocxParts(new XElement(W + "document", new XElement(W + "body", Metadata(), Question("Q1", TextCell("Đề")))), new()
        {
            ["docProps/part1.xml"] = part,
            ["docProps/part2.xml"] = part,
            ["docProps/part3.xml"] = part
        });
        var error = await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(input));
        Assert.Equal("XML_COMPLEXITY_LIMIT", error.Code);
        Assert.Contains("1.000.000", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReusedDocxImageHasDistinctReferencesAndOneImmutableRawSource()
    {
        XElement WithImage(string key)
        {
            var question = Question(key, TextCell("Đề có hình"));
            var drawing = Drawing();
            drawing.Element(Wp + "inline")!.AddFirst(new XElement(Wp + "extent", new XAttribute("cx", key == "Q1" ? "200" : "300"), new XAttribute("cy", "100")));
            question.Add(Field("Ảnh đề bài", new XElement(W + "tc", new XElement(W + "p", new XElement(W + "r", drawing)))));
            return question;
        }
        using var input = DocxParts(new XElement(W + "document", new XElement(W + "body", Metadata(), WithImage("Q1"), WithImage("Q2"))), new()
        {
            ["word/_rels/document.xml.rels"] = Encoding.UTF8.GetBytes("<Relationships><Relationship Id='rImage' Target='media/image.png'/></Relationships>"),
            ["word/media/image.png"] = [1, 2, 3]
        });
        var parsed = await Parse(input);
        Assert.Equal(2, parsed.MediaSources.Count);
        Assert.Equal(2, parsed.MediaSources.Values.Select(s => s.Reference).Distinct().Count());
        Assert.Single(parsed.MediaSources.Values.Select(s => s.SourcePath).Distinct());
        Assert.Equal(2.0, parsed.MediaSources[parsed.Content.Rows[0].Values["ImageRef"]].DisplayAspectRatio);
        Assert.Equal(3.0, parsed.MediaSources[parsed.Content.Rows[1].Values["ImageRef"]].DisplayAspectRatio);
        Assert.All(parsed.Content.Rows, r => Assert.Empty(r.Diagnostics));
        Assert.All(parsed.Content.Rows, r => Assert.True(parsed.MediaSources.ContainsKey(r.Values["ImageRef"])));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DocxRetainsEachReferenceDisplayAspectRatio(bool useFallback)
    {
        var drawing = Drawing();
        if (useFallback) drawing.Descendants(A + "blip").Single().AddAfterSelf(new XElement(A + "xfrm", new XElement(A + "ext", new XAttribute("cx", "300"), new XAttribute("cy", "200"))));
        else drawing.Element(Wp + "inline")!.AddFirst(new XElement(Wp + "extent", new XAttribute("cx", "300"), new XAttribute("cy", "200")));
        var question = Question("Q1", TextCell("Đề"));
        question.Add(Field("Ảnh đề bài", new XElement(W + "tc", new XElement(W + "p", new XElement(W + "r", drawing)))));
        using var input = DocxWithImage(question);
        var parsed = await Parse(input);
        Assert.Equal(1.5, Assert.Single(parsed.MediaSources).Value.DisplayAspectRatio);
        Assert.Empty(Assert.Single(parsed.Content.Rows).Diagnostics);
    }

    [Theory]
    [InlineData("0", "200")]
    [InlineData("300", "-1")]
    [InlineData("bad", "200")]
    [InlineData("300", "")]
    public async Task MalformedWordDisplaySizeCannotSilentlyDefaultToOriginalImage(string width, string height)
    {
        var drawing = Drawing();
        drawing.Element(Wp + "inline")!.AddFirst(new XElement(Wp + "extent", new XAttribute("cx", width), new XAttribute("cy", height)));
        var question = Question("Q1", TextCell("Đề"));
        question.Add(Field("Ảnh đề bài", new XElement(W + "tc", new XElement(W + "p", new XElement(W + "r", drawing)))));
        using var input = DocxWithImage(question);
        var parsed = await Parse(input);
        Assert.Empty(parsed.MediaSources);
        Assert.Contains(Assert.Single(parsed.Content.Rows).Diagnostics, e => e.Code == "UNSUPPORTED_IMAGE_LAYOUT");
    }

    [Theory]
    [InlineData("ins")]
    [InlineData("sdt")]
    [InlineData("customXml")]
    [InlineData("fldSimple")]
    public async Task WordWrappersAndTrackedContentCannotHideFields(string wrapper)
    {
        var question = Question("Q1", TextCell("Đề"));
        question.Add(new XElement(W + wrapper, Field("Solution", TextCell("Nội dung bị ẩn trong wrapper"))));
        using var input = Docx(question);
        var error = await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(input));
        Assert.Equal("UNSUPPORTED_DOCX_CONTENT", error.Code);
    }

    [Fact]
    public async Task ImageEffectsAreNotSilentlyDiscarded()
    {
        var drawing = Drawing(); drawing.Descendants(A + "blip").Single().Add(new XElement(A + "duotone"));
        var question = Question("Q1", TextCell("Đề"));
        question.Add(Field("Ảnh đề bài", new XElement(W + "tc", new XElement(W + "p", new XElement(W + "r", drawing)))));
        using var input = DocxWithImage(question);
        var parsed = await Parse(input);
        Assert.Contains(Assert.Single(parsed.Content.Rows).Diagnostics, e => e.Code == "UNSUPPORTED_IMAGE_TRANSFORM");
        Assert.Empty(parsed.MediaSources);
    }

    [Fact]
    public async Task EquationAlongsideInlineImageIsNotSilentlyDropped()
    {
        var question = Question("Q1", TextCell("Đề"));
        question.Add(Field("Ảnh đề bài", new XElement(W + "tc", new XElement(W + "p", new XElement(W + "r", Drawing()), new XElement(M + "oMath", MathRun("x"))))));
        using var input = DocxWithImage(question);
        var parsed = await Parse(input);
        Assert.Contains(Assert.Single(parsed.Content.Rows).Diagnostics, e => e.Code == "UNSUPPORTED_IMAGE_CONTENT");
        Assert.Empty(parsed.MediaSources);
    }

    [Fact]
    public async Task OversizeImageIsScopedToQuestionWithoutWritingTheAsset()
    {
        using var input = Package(new()
        {
            ["questions.csv"] = Encoding.UTF8.GetBytes("QuestionKey,QuestionText,ImageRef\nQ1,Question,media/a.png\nQ2,Other,"),
            ["media/a.png"] = new byte[8 * 1024 * 1024 + 1]
        });
        var parsed = await Parse(input, "questions.zip", "media-1.0");
        Assert.Contains(parsed.Content.Rows[0].Diagnostics, e => e.Code == "SOURCE_IMAGE_TOO_LARGE");
        Assert.Empty(parsed.Content.Rows[1].Diagnostics);
        Assert.Empty(parsed.MediaSources);
        Assert.True(Directory.EnumerateFiles(_work).Sum(p => new FileInfo(p).Length) < 1024 * 1024);
    }

    private Task<ParsedQuestionImportV2File> Parse(Stream input, string fileName = "questions.docx", string profile = "docx-1.0")
        => QuestionImportV2FileParser.ParseAsync(input, fileName, profile, _work, default);
    private static XElement Run(string text) => new(W + "r", new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text));
    private static XElement MathRun(string text) => new(M + "r", new XElement(M + "t", text));
    private static XElement TextCell(string text) => new(W + "tc", new XElement(W + "p", Run(text)));
    private static XElement Field(string label, XElement cell) => new(W + "tr", TextCell(label), cell);
    private static XElement Metadata(string version = "2.0") => new(W + "tbl", Field("EduTwin Import", TextCell("docx-1.0")), Field("TemplateVersion", TextCell(version)), Field("SubjectId", TextCell(Subject)));
    private static XElement Question(string key, XElement content) => new(W + "tbl", Field("Trường", TextCell("Nội dung")), Field("Mã câu hỏi", TextCell(key)), Field("Nội dung câu hỏi", content));
    private static XElement Drawing() => new(W + "drawing", new XElement(Wp + "inline", new XElement(A + "graphic", new XElement(A + "blip", new XAttribute(R + "embed", "rImage")))));
    private static MemoryStream Docx(params XElement[] questions) => DocxParts(new XElement(W + "document", new XElement(W + "body", Metadata(), questions)));
    private static MemoryStream DocxWithImage(XElement question, string? relationships = null) => DocxParts(
        new XElement(W + "document", new XElement(W + "body", Metadata(), question)), new()
        {
            ["word/_rels/document.xml.rels"] = Encoding.UTF8.GetBytes(relationships ?? "<Relationships><Relationship Id='rImage' Target='media/image.png'/></Relationships>"),
            ["word/media/image.png"] = [1, 2, 3]
        });
    private static MemoryStream DocxParts(XElement document, Dictionary<string, byte[]>? extras = null)
    {
        var parts = new Dictionary<string, byte[]> { ["word/document.xml"] = Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting)) };
        foreach (var extra in extras ?? []) parts.Add(extra.Key, extra.Value);
        return Package(parts);
    }
    private static MemoryStream Package(Dictionary<string, byte[]> entries)
    {
        var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var entry in entries) { using var part = zip.CreateEntry(entry.Key).Open(); part.Write(entry.Value); }
        output.Position = 0; return output;
    }
}
