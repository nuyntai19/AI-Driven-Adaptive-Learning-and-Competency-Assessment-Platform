using System.IO.Compression;
using System.Buffers.Binary;
using System.Text;
using System.Xml.Linq;
using EduTwin.BLL.CurriculumAndQuestions.Import;
using Xunit;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public sealed class QuestionImportFileParserTests
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    [Fact]
    public async Task Csv_PreservesMathUnicodeQuotesAndPhysicalRecordLocations()
    {
        const string content = "\uFEFFQuestionText,CorrectAnswer,Solution\r\n\r\n\"Tính $\\frac{1}{2}$, \"\"ví dụ\"\"\",1/2,\"Dòng một\r\nDòng hai\"\r\nCâu kế,2,Lời giải\r\n";
        var result = await Csv(content);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(3, result.Rows[0].RowIndex);
        Assert.Equal(5, result.Rows[1].RowIndex);
        Assert.Equal("Tính $\\frac{1}{2}$, \"ví dụ\"", result.Rows[0].Values["QuestionText"]);
        Assert.Equal("Dòng một\nDòng hai", result.Rows[0].Values["Solution"]);
        Assert.All(result.Rows, row => Assert.Empty(row.Diagnostics));
        Assert.Equal(64, result.FileHash.Length);
        Assert.Equal(Encoding.UTF8.GetByteCount(content), result.FileSize);
    }

    [Theory]
    [InlineData(';')]
    [InlineData('\t')]
    public async Task Csv_DetectsDocumentedDelimiterAndMapsOnlyExplicitAliases(char delimiter)
    {
        var result = await Csv($"Câu hỏi{delimiter}Đáp án đúng{delimiter}Lời giải\nĐề{delimiter}1{delimiter}Giải");
        Assert.Equal("Đề", result.Rows.Single().Values["QuestionText"]);
        Assert.Equal("1", result.Rows.Single().Values["CorrectAnswer"]);
    }

    [Theory]
    [InlineData("QuestionText,Solution\n\"abc,Lời giải", "MALFORMED_CSV")]
    [InlineData("QuestionText,Solution\nabc\"def,Lời giải", "MALFORMED_CSV")]
    [InlineData("QuestionText,Solution\n\"abc\"x,Lời giải", "MALFORMED_CSV")]
    [InlineData("QuestionText,Câu hỏi\nabc,def", "DUPLICATE_HEADER")]
    public async Task Csv_RejectsMalformedQuotesAndAliasHeaderCollisions(string text, string code)
    {
        var ex = await Assert.ThrowsAsync<QuestionImportFileException>(() => Csv(text));
        Assert.Equal(code, ex.Code);
    }

    [Fact]
    public async Task Csv_ReportsWrongColumnCountsUnknownDataAndKeepsSourceRow()
    {
        var result = await Csv("QuestionText,Solution,IgnoredMetadata\nCâu,Giải,Không bỏ âm thầm\nCâu 2,Giải 2\n");
        Assert.Contains(result.Rows[0].Diagnostics, d => d.Code == "UNKNOWN_COLUMN" && d.ColumnName == "IgnoredMetadata" && d.RowIndex == 2);
        Assert.Contains(result.Rows[1].Diagnostics, d => d.Code == "CSV_COLUMN_COUNT" && d.RowIndex == 3);
    }

    [Fact]
    public async Task Csv_RejectsTooManyQuestionsAndOverlongCells()
    {
        var rows = "QuestionText\n" + string.Join('\n', Enumerable.Repeat("Câu hỏi", 501));
        var limit = await Assert.ThrowsAsync<QuestionImportFileException>(() => Csv(rows));
        Assert.Equal("ROW_LIMIT_EXCEEDED", limit.Code);
        var field = await Assert.ThrowsAsync<QuestionImportFileException>(() => Csv("QuestionText\n" + new string('x', 20_001)));
        Assert.Equal("FIELD_TOO_LONG", field.Code);
    }

    [Fact]
    public async Task Workbook_ResolvesQuestionsByRelationshipAndReadsRichSparseStrings()
    {
        var questions = Sheet(
            Row(1, Inline("A1", "QuestionText"), Inline("B1", "CorrectAnswer"), Inline("D1", "Solution")),
            Row(5, new XElement(Ns + "c", new XAttribute("r", "A5"), new XAttribute("t", "s"), new XElement(Ns + "v", "0")),
                Inline("B5", "1/2"), Inline("D5", "Giải $\\frac{1}{2}$")));
        var guide = Sheet(Row(1, Inline("A1", "TemplateVersion"), Inline("B1", "1.0")));
        var shared = new XElement(Ns + "sst", new XElement(Ns + "si",
            new XElement(Ns + "r", new XElement(Ns + "t", "Tính ")),
            new XElement(Ns + "r", new XElement(Ns + "t", "$\\frac{1}{2}$"))));
        using var workbook = Workbook([( "Guide", "guide.xml", guide), ("Questions", "unusual-name.xml", questions)],
            new Dictionary<string, string> { ["xl/sharedStrings.xml"] = shared.ToString() });
        var result = await Parse(workbook);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Questions", row.SheetName);
        Assert.Equal(5, row.RowIndex);
        Assert.Equal("Tính $\\frac{1}{2}$", row.Values["QuestionText"]);
        Assert.Equal("1/2", row.Values["CorrectAnswer"]);
        Assert.Empty(row.Diagnostics);
    }

    [Fact]
    public async Task Workbook_NormalizesCarriageReturnSharedStringOnceAndReusesIt()
    {
        // XML character references preserve CR rather than applying XML's newline normalization.
        const string shared = "<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><si><t>Đề&#13;dòng hai</t></si></sst>";
        var questions = Sheet(Row(1, Inline("A1", "QuestionText")),
            Row(2, Cell("A2", "0", "s")), Row(3, Cell("A3", "0", "s")), Row(4, Cell("A4", "0", "s")));
        using var workbook = Workbook([("Questions", "sheet.xml", questions)], new Dictionary<string, string> { ["xl/sharedStrings.xml"] = shared });
        var parsed = await Parse(workbook);
        Assert.Equal(3, parsed.Rows.Count);
        var first = parsed.Rows[0].Values["QuestionText"];
        Assert.Equal("Đề\ndòng hai", first);
        Assert.All(parsed.Rows, row => Assert.Same(first, row.Values["QuestionText"]));
    }

    [Fact]
    public async Task Workbook_ChargesRepeatedCarriageReturnSharedStringsBeforeRowExpansion()
    {
        var shared = "<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><si><t>" +
            new string('x', 19_999) + "&#13;</t></si></sst>";
        // Only a few hundred KiB of XML, but nearly 750 MiB of logical cell content.
        // Each CR used to cause a new 20,000-character normalized string per cell.
        var rows = new List<XElement> { Row(1, Inline("A1", "QuestionText"), Inline("B1", "Solution")) };
        for (var row = 2; row <= 500; row++)
        {
            var cells = Enumerable.Range(0, 78).Select(column => Cell(TestColumnName(column) + row, "0", "s")).ToArray();
            rows.Add(Row(row, cells));
        }
        using var workbook = Workbook([("Questions", "sheet.xml", Sheet(rows.ToArray()))], new Dictionary<string, string> { ["xl/sharedStrings.xml"] = shared }, compact: true);
        var error = await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(workbook));
        Assert.Equal("NORMALIZED_DATA_LIMIT", error.Code);
        Assert.Contains("10 MiB", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Workbook_RejectsFormulaCacheErrorDatesAndWrongBooleanTypes()
    {
        var questions = Sheet(
            Row(1, Inline("A1", "QuestionText"), Inline("B1", "CorrectAnswer"), Inline("C1", "Solution"), Inline("D1", "ReasoningRequired")),
            Row(2, new XElement(Ns + "c", new XAttribute("r", "A2"), new XElement(Ns + "f", "1+1"), new XElement(Ns + "v", "2")),
                Cell("B2", "45000", "n", 1), Cell("C2", "#DIV/0!", "e"), Cell("D2", "0", "b")),
            Row(3, Inline("A3", "Câu 3"), Cell("B3", "1", "b"), Inline("C3", "Giải"), Cell("D3", "bad", "b")));
        var styles = new XElement(Ns + "styleSheet", new XElement(Ns + "cellXfs",
            new XElement(Ns + "xf", new XAttribute("numFmtId", "0")), new XElement(Ns + "xf", new XAttribute("numFmtId", "14"))));
        using var workbook = Workbook([("Questions", "sheet.xml", questions)], new Dictionary<string, string> { ["xl/styles.xml"] = styles.ToString() });
        var result = await Parse(workbook);
        Assert.Equal("", result.Rows[0].Values["QuestionText"]);
        Assert.Equal("false", result.Rows[0].Values["ReasoningRequired"]);
        Assert.Contains(result.Rows[0].Diagnostics, d => d.Code == "EXCEL_FORMULA" && d.ColumnName == "QuestionText");
        Assert.Contains(result.Rows[0].Diagnostics, d => d.Code == "EXCEL_DATE_CELL" && d.ColumnName == "CorrectAnswer");
        Assert.Contains(result.Rows[0].Diagnostics, d => d.Code == "EXCEL_CELL_ERROR");
        Assert.Contains(result.Rows[1].Diagnostics, d => d.Code == "EXCEL_BOOLEAN_CELL");
        Assert.Contains(result.Rows[1].Diagnostics, d => d.Code == "INVALID_BOOLEAN");
    }

    [Theory]
    [InlineData("xl/drawings/drawing1.xml", "<drawing/>", "UNSUPPORTED_DRAWING")]
    [InlineData("xl/vbaProject.bin", "macro", "UNSUPPORTED_ACTIVE_CONTENT")]
    [InlineData("xl/externalLinks/externalLink1.xml", "<external/>", "EXTERNAL_REFERENCE")]
    [InlineData("xl/worksheets/_rels/sheet.xml.rels", "<Relationships><Relationship Id=\"r1\" Target=\"https://example.test\" TargetMode=\"External\" /></Relationships>", "EXTERNAL_REFERENCE")]
    [InlineData("[Content_Types].xml", "<Types><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.ms-excel.sheet.macroEnabled.main+xml\" /></Types>", "UNSUPPORTED_ACTIVE_CONTENT")]
    public async Task Workbook_RejectsUnsupportedActiveAndExternalContent(string path, string data, string code)
    {
        using var workbook = BasicWorkbook(new Dictionary<string, string> { [path] = data });
        var ex = await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(workbook));
        Assert.Equal(code, ex.Code);
    }

    [Fact]
    public async Task Workbook_RejectsNonemptyAdvancedSheetsInsteadOfDiscardingThem()
    {
        using var workbook = Workbook([
            ("Questions", "sheet.xml", Sheet(Row(1, Inline("A1", "QuestionText")), Row(2, Inline("A2", "Đề")))),
            ("RubricCriteria", "rubric.xml", Sheet(Row(1, Inline("A1", "QuestionKey")), Row(2, Inline("A2", "Q1"))))]);
        var ex = await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(workbook));
        Assert.Equal("UNSUPPORTED_SHEET", ex.Code);
    }

    [Fact]
    public async Task Workbook_RequiresQuestionsExceptExplicitLegacyMode()
    {
        using var workbook = Workbook([("OldData", "sheet.xml", Sheet(Row(1, Inline("A1", "QuestionText")), Row(2, Inline("A2", "Đề"))))]);
        var ex = await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(workbook));
        Assert.Equal("MISSING_QUESTIONS_SHEET", ex.Code);
        workbook.Position = 0;
        var legacy = await QuestionImportFileParser.ParseAsync(workbook, "questions.xlsx", true, CancellationToken.None);
        Assert.Equal("OldData", legacy.Rows.Single().SheetName);
    }

    [Fact]
    public async Task Workbook_ValidatesTemplateVersionAndDisablesXmlEntities()
    {
        using var future = Workbook([
            ("Guide", "guide.xml", Sheet(Row(1, Inline("A1", "TemplateVersion"), Inline("B1", "2.0")))),
            ("Questions", "sheet.xml", Sheet(Row(1, Inline("A1", "QuestionText")), Row(2, Inline("A2", "Đề"))))]);
        var ex = await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(future));
        Assert.Equal("UNSUPPORTED_TEMPLATE_VERSION", ex.Code);
        using var unsafeXml = BasicWorkbook(new Dictionary<string, string>
        {
            ["xl/sharedStrings.xml"] = "<!DOCTYPE sst [<!ENTITY file SYSTEM 'file:///nonexistent/private'>]><sst>&file;</sst>"
        });
        var unsafeError = await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(unsafeXml));
        Assert.Equal("INVALID_FILE", unsafeError.Code);
    }

    [Fact]
    public async Task Parser_BoundsNonseekableUploadAndHonorsCancellation()
    {
        using var large = new NonSeekableStream(new byte[QuestionImportFileParser.MaxUploadBytes + 1]);
        var ex = await Assert.ThrowsAsync<QuestionImportFileException>(() => QuestionImportFileParser.ParseAsync(large, "x.csv", false, CancellationToken.None));
        Assert.Equal("FILE_LIMIT_EXCEEDED", ex.Code);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("QuestionText\nĐề"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => QuestionImportFileParser.ParseAsync(input, "x.csv", false, cancelled.Token));
    }

    [Fact]
    public async Task Workbook_BoundsArchiveEntriesAndExpandedPayload()
    {
        var extras = Enumerable.Range(0, 200).ToDictionary(i => $"extra/{i}.txt", _ => "small");
        using var tooMany = BasicWorkbook(extras);
        var entries = await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(tooMany));
        Assert.Equal("ZIP_ENTRY_LIMIT", entries.Code);
        using var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, true))
        {
            using var entry = archive.CreateEntry("large.bin", CompressionLevel.Fastest).Open();
            var zeros = new byte[1024 * 1024];
            for (var i = 0; i < 51; i++) entry.Write(zeros);
        }
        zip.Position = 0;
        var expanded = await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(zip));
        Assert.Equal("FILE_LIMIT_EXCEEDED", expanded.Code);
        var deceptiveBytes = zip.ToArray();
        for (var i = 0; i <= deceptiveBytes.Length - 46; i++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(deceptiveBytes.AsSpan(i, 4)) == 0x02014b50)
                BinaryPrimitives.WriteUInt32LittleEndian(deceptiveBytes.AsSpan(i + 24, 4), 1);
        }
        using var deceptive = new MemoryStream(deceptiveBytes);
        var malformed = await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(deceptive));
        // .NET clamps entry output to its declared size; CRC verification detects concealed truncation.
        Assert.Equal("ZIP_INTEGRITY_FAILED", malformed.Code);
    }

    [Fact]
    public async Task Workbook_ExplicitlyRejectsEncryptedZipEntries()
    {
        using var original = BasicWorkbook();
        var bytes = original.ToArray();
        for (var i = 0; i <= bytes.Length - 46; i++)
        {
            var signature = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i, 4));
            if (signature == 0x02014b50) bytes[i + 8] |= 1;
            else if (signature == 0x04034b50) bytes[i + 6] |= 1;
        }
        using var encrypted = new MemoryStream(bytes);
        var error = await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(encrypted));
        Assert.Equal("ENCRYPTED_WORKBOOK", error.Code);
    }

    [Fact]
    public async Task Parser_BoundsXmlObjectAmplificationDepthAndPopulatedColumns()
    {
        using var tooManyNodes = BasicWorkbook(new Dictionary<string, string>
        {
            ["xl/sharedStrings.xml"] = "<sst>" + string.Concat(Enumerable.Repeat("<si/>", QuestionImportFileParser.MaxXmlNodesPerPart)) + "</sst>"
        });
        var nodes = await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(tooManyNodes));
        Assert.Equal("XML_COMPLEXITY_LIMIT", nodes.Code);
        using var tooDeep = BasicWorkbook(new Dictionary<string, string>
        {
            ["xl/sharedStrings.xml"] = string.Concat(Enumerable.Repeat("<x>", 66)) + string.Concat(Enumerable.Repeat("</x>", 66))
        });
        var depth = await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(tooDeep));
        Assert.Equal("XML_COMPLEXITY_LIMIT", depth.Code);
        var columns = await Assert.ThrowsAsync<QuestionImportFileException>(() => Csv("QuestionText," + string.Join(',', Enumerable.Repeat("Other", 128)) + "\nQuestion"));
        Assert.Equal("COLUMN_LIMIT_EXCEEDED", columns.Code);
    }

    [Fact]
    public async Task Csv_ManyEmptyPhysicalRecordsDoNotChangeQuestionLocationOrBudget()
    {
        var result = await Csv("QuestionText\n" + new string('\n', 20_000) + "Đề");
        Assert.Equal(20_002, result.Rows.Single().RowIndex);
    }

    [Fact]
    public async Task Workbook_ParsesActualFrontendGeneratedV1TemplateIncludingStyledBlankRows()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "CurriculumAndQuestions", "Fixtures", "Import", "frontend-template-v1.xlsx");
        await using var workbook = File.OpenRead(path);
        var result = await Parse(workbook);
        Assert.Equal(3, result.Rows.Count);
        Assert.Equal(new[] { 2, 3, 4 }, result.Rows.Select(r => r.RowIndex));
        Assert.Equal(new[] { "MATH-DERIV", "MATH-FRAC", "MATH-GEOM" }, result.Rows.Select(r => r.Values["TopicCode"]));
        Assert.All(result.Rows, row => Assert.Empty(row.Diagnostics));
        Assert.Contains("\\frac", result.Rows[1].Values["QuestionText"], StringComparison.Ordinal);
        Assert.Equal("Essay", result.Rows[2].Values["QuestionType"]);
        Assert.StartsWith("[", result.Rows[2].Values["RequiredIdeas"], StringComparison.Ordinal);
        Assert.Equal(Guid.Parse("0904f0c4-2901-4b47-8504-bbe84239cd1f"), result.SourceSubjectId);
    }

    [Theory]
    [InlineData("invalid", false, false)]
    [InlineData("00000000-0000-0000-0000-000000000000", false, false)]
    [InlineData("0904f0c4-2901-4b47-8504-bbe84239cd1f", true, false)]
    [InlineData("0904f0c4-2901-4b47-8504-bbe84239cd1f", false, true)]
    public async Task Workbook_RejectsInvalidDuplicateOrFormulaSourceSubjectMetadata(string value, bool duplicate, bool formula)
    {
        var cell = Inline("B1", value);
        if (formula) cell.Add(new XElement(Ns + "f", "1+1"));
        var rows = new List<XElement> { Row(1, Inline("A1", "SubjectId"), cell) };
        if (duplicate) rows.Add(Row(2, Inline("A2", "SubjectId"), Inline("B2", value)));
        using var workbook = Workbook([
            ("Guide", "guide.xml", Sheet(rows.ToArray())),
            ("Questions", "sheet.xml", Sheet(Row(1, Inline("A1", "QuestionText")), Row(2, Inline("A2", "Đề"))))]);
        var error = await Assert.ThrowsAsync<QuestionImportFileException>(() => Parse(workbook));
        Assert.Equal(duplicate ? "DUPLICATE_GUIDE_METADATA" : "INVALID_GUIDE_METADATA", error.Code);
    }

    private static Task<ParsedQuestionImportFile> Csv(string content) =>
        QuestionImportFileParser.ParseAsync(new MemoryStream(Encoding.UTF8.GetBytes(content)), "questions.csv", false, CancellationToken.None);
    private static Task<ParsedQuestionImportFile> Parse(Stream stream) =>
        QuestionImportFileParser.ParseAsync(stream, "questions.xlsx", false, CancellationToken.None);
    private static XElement Sheet(params XElement[] rows) => new(Ns + "worksheet", new XElement(Ns + "sheetData", rows));
    private static XElement Row(int number, params XElement[] cells) => new(Ns + "row", new XAttribute("r", number), cells);
    private static XElement Inline(string reference, string text) => new(Ns + "c", new XAttribute("r", reference), new XAttribute("t", "inlineStr"),
        new XElement(Ns + "is", new XElement(Ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
    private static XElement Cell(string reference, string value, string type, int style = 0) => new(Ns + "c", new XAttribute("r", reference),
        new XAttribute("t", type), new XAttribute("s", style), new XElement(Ns + "v", value));
    private static string TestColumnName(int column)
    {
        var name = "";
        for (var value = column + 1; value > 0; value = (value - 1) / 26) name = (char)('A' + (value - 1) % 26) + name;
        return name;
    }
    private static MemoryStream BasicWorkbook(Dictionary<string, string>? extra = null) => Workbook([
        ("Questions", "sheet.xml", Sheet(Row(1, Inline("A1", "QuestionText")), Row(2, Inline("A2", "Đề"))))], extra);

    private static MemoryStream Workbook((string Name, string Path, XElement Xml)[] sheets, Dictionary<string, string>? extra = null, bool compact = false)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            void Write(string path, string data)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write(data);
            }
            Write("xl/workbook.xml", new XElement(Ns + "workbook", new XElement(Ns + "sheets", sheets.Select((s, i) =>
                new XElement(Ns + "sheet", new XAttribute("name", s.Name), new XAttribute("sheetId", i + 1), new XAttribute(Rel + "id", $"r{i}"))))).ToString());
            Write("xl/_rels/workbook.xml.rels", new XElement("Relationships", sheets.Select((s, i) => new XElement("Relationship",
                new XAttribute("Id", $"r{i}"), new XAttribute("Target", $"worksheets/{s.Path}")))).ToString());
            foreach (var sheet in sheets) Write($"xl/worksheets/{sheet.Path}", sheet.Xml.ToString(compact ? SaveOptions.DisableFormatting : SaveOptions.None));
            foreach (var entry in extra ?? []) Write(entry.Key, entry.Value);
        }
        stream.Position = 0;
        return stream;
    }

    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
    }
}
