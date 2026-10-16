using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace EduTwin.BLL.CurriculumAndQuestions.Import;

public sealed record ImportFileDiagnostic(
    string Code,
    string Message,
    string? SheetName = null,
    int? RowIndex = null,
    string? ColumnName = null,
    string? RawValue = null);

public sealed record RawImportRow(
    string SheetName,
    int RowIndex,
    Dictionary<string, string> Values,
    List<ImportFileDiagnostic> Diagnostics);

public sealed record ParsedQuestionImportFile(
    List<RawImportRow> Rows,
    List<ImportFileDiagnostic> Diagnostics,
    string FileHash,
    long FileSize)
{
    public Guid? SourceSubjectId { get; init; }
}

public sealed class QuestionImportFileException : Exception
{
    public string Code { get; }
    public QuestionImportFileException(string code, string message) : base(message) => Code = code;
}

/// <summary>
/// Text-only V1 adapter. Never evaluates formulas or resolves external content.
/// Every archive entry is actually read under a shared decompression budget before XML is parsed.
/// </summary>
public static class QuestionImportFileParser
{
    public const int MaxUploadBytes = 10 * 1024 * 1024;
    public const int MaxExpandedBytes = 50 * 1024 * 1024;
    public const int MaxEntries = 200;
    public const int MaxQuestionRows = 500;
    public const int MaxFieldCharacters = 20_000;
    public const int MaxNormalizedTextBytes = 10 * 1024 * 1024;
    public const int MaxPopulatedColumns = 128;
    public const int MaxXmlNodesPerPart = 200_000;

    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly Dictionary<string, string> HeaderAliases = BuildHeaderAliases();
    private static readonly uint[] CrcTable = BuildCrcTable();
    private static readonly HashSet<string> NumericColumns = new(StringComparer.OrdinalIgnoreCase)
        { "GradeLevel", "Difficulty", "MaxScore", "EstimatedTimeSeconds" };

    private sealed record Cell(string Value, string Type = "", int Style = 0, bool HasFormula = false);
    private sealed record SourceRow(int Index, Dictionary<int, Cell> Cells);
    private sealed record WorkbookData(List<RawImportRow> Rows, Guid? SourceSubjectId);
    private sealed class ExpandedTextBudget
    {
        private long _usedBytes;
        public void Charge(string value)
        {
            _usedBytes += Encoding.UTF8.GetByteCount(value);
            if (_usedBytes > MaxNormalizedTextBytes)
                Fail("NORMALIZED_DATA_LIMIT", "Tổng nội dung câu hỏi sau mở rộng vượt 10 MiB. Chia thành nhiều tệp nhỏ hơn; mỗi ô shared string được tính theo số lần dùng.");
        }
    }

    public static async Task<ParsedQuestionImportFile> ParseAsync(
        Stream stream, string fileName, bool legacyMode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();
        var uploaded = await ReadBoundedAsync(stream, MaxUploadBytes, cancellationToken);
        if (uploaded.Length == 0) Fail("EMPTY_FILE", "Tệp không có dữ liệu.");
        var hash = Convert.ToHexString(SHA256.HashData(uploaded));
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        try
        {
            List<RawImportRow> rows;
            Guid? sourceSubjectId = null;
            List<ImportFileDiagnostic> diagnostics = [];
            if (extension == ".csv" || (extension == ".txt" && legacyMode))
            {
                var text = new UTF8Encoding(false, true).GetString(uploaded).TrimStart('\uFEFF');
                rows = ParseCsv(text, cancellationToken);
            }
            else if (extension == ".xlsx")
            {
                var workbook = await ParseWorkbookAsync(uploaded, legacyMode, cancellationToken);
                rows = workbook.Rows;
                sourceSubjectId = workbook.SourceSubjectId;
            }
            else
            {
                Fail("UNSUPPORTED_FILE_TYPE", "Chỉ hỗ trợ XLSX và CSV UTF-8; TXT chỉ hỗ trợ trong chế độ tương thích.");
                throw new InvalidOperationException();
            }
            if (rows.Count == 0) Fail("NO_QUESTIONS", "Tệp không có dòng câu hỏi.");
            return new(rows, diagnostics, hash, uploaded.LongLength) { SourceSubjectId = sourceSubjectId };
        }
        catch (OperationCanceledException) { throw; }
        catch (QuestionImportFileException) { throw; }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or DecoderFallbackException or IOException or FormatException or OverflowException or ArgumentException)
        {
            throw new QuestionImportFileException("INVALID_FILE", "Tệp hỏng, mã hóa/password, XML không an toàn hoặc CSV không phải UTF-8 hợp lệ.");
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, long budget, CancellationToken ct)
    {
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        long total = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var count = await stream.ReadAsync(buffer.AsMemory(), ct);
            if (count == 0) break;
            total += count;
            if (total > budget) Fail("FILE_LIMIT_EXCEEDED", "Tệp hoặc tổng dữ liệu giải nén vượt giới hạn cho phép.");
            await output.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        return output.ToArray();
    }

    private static async Task<WorkbookData> ParseWorkbookAsync(byte[] uploaded, bool legacyMode, CancellationToken ct)
    {
        using var zipStream = new MemoryStream(uploaded, false);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, false);
        if (archive.Entries.Count > MaxEntries) Fail("ZIP_ENTRY_LIMIT", "Workbook có quá 200 thành phần.");
        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long expanded = 0;
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (entry.IsEncrypted) Fail("ENCRYPTED_WORKBOOK", "Workbook chứa dữ liệu mã hóa/password; hãy lưu một bản XLSX không mã hóa để nhập.");
            var path = entry.FullName;
            if (path.Contains('\\') || path.StartsWith('/') || path.Split('/').Any(p => p == ".."))
                Fail("UNSAFE_ARCHIVE_PATH", "Workbook chứa đường dẫn không hợp lệ.");
            if (entries.ContainsKey(path)) Fail("DUPLICATE_ARCHIVE_ENTRY", "Workbook chứa thành phần trùng tên.");
            if (entry.Length > MaxExpandedBytes - expanded) Fail("FILE_LIMIT_EXCEEDED", "Tổng dữ liệu giải nén vượt 50 MiB.");
            await using var opened = entry.Open();
            var bytes = await ReadBoundedAsync(opened, MaxExpandedBytes - expanded, ct);
            if (bytes.LongLength != entry.Length || ComputeCrc32(bytes, ct) != entry.Crc32)
                Fail("ZIP_INTEGRITY_FAILED", "Tệp Excel bị hỏng hoặc dữ liệu không nhất quán. Hãy lưu lại tệp và thử lại.");
            expanded += bytes.LongLength;
            entries.Add(path, bytes);
            if (path.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("xl/activeX/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("xl/embeddings/", StringComparison.OrdinalIgnoreCase))
                Fail("UNSUPPORTED_ACTIVE_CONTENT", "Không hỗ trợ macro, ActiveX hoặc đối tượng nhúng trong workbook.");
            if (path.StartsWith("xl/drawings/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("xl/media/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("xl/charts/", StringComparison.OrdinalIgnoreCase))
                Fail("UNSUPPORTED_DRAWING", "Ảnh, drawing hoặc Equation trong Excel chưa được hỗ trợ. Hãy dùng LaTeX dạng Text và thêm ảnh trong editor.");
            if (path.StartsWith("xl/externalLinks/", StringComparison.OrdinalIgnoreCase))
                Fail("EXTERNAL_REFERENCE", "Không hỗ trợ liên kết ngoài trong workbook.");
        }

        foreach (var entry in entries.Where(e => e.Key.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)))
        {
            var rels = ReadXml(entry.Value, ct);
            if (rels.Descendants().Any(e => e.Name.LocalName == "Relationship" &&
                string.Equals((string?)e.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase)))
                Fail("EXTERNAL_REFERENCE", "Workbook có liên kết ngoài; không thể nhập an toàn trong profile V1.");
        }
        if (entries.TryGetValue("[Content_Types].xml", out var contentTypes))
        {
            var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
            var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var declaration in ReadXml(contentTypes, ct).Descendants())
            {
                var type = (string?)declaration.Attribute("ContentType") ?? "";
                if (declaration.Name.LocalName == "Override" && declaration.Attribute("PartName") is { } partName && !overrides.TryAdd(partName.Value, type))
                    Fail("INVALID_WORKBOOK", "Workbook có khai báo content type trùng PartName.");
                if (declaration.Name.LocalName == "Default" && declaration.Attribute("Extension") is { } extension && !defaults.TryAdd(extension.Value, type))
                    Fail("INVALID_WORKBOOK", "Workbook có khai báo content type trùng Extension.");
            }
            foreach (var part in entries.Keys)
            {
                ct.ThrowIfCancellationRequested();
                // Some writers include unused macro-enabled defaults. Inspect the effective type
                // of real parts; an explicit Override takes precedence over an extension default.
                var type = overrides.GetValueOrDefault("/" + part) ?? defaults.GetValueOrDefault(Path.GetExtension(part).TrimStart('.'));
                if (type?.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase) == true)
                    Fail("UNSUPPORTED_ACTIVE_CONTENT", "Không hỗ trợ workbook có macro.");
            }
        }

        var workbook = ReadXml(RequiredEntry(entries, "xl/workbook.xml"), ct);
        var workbookRels = ReadXml(RequiredEntry(entries, "xl/_rels/workbook.xml.rels"), ct);
        var relationshipMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rel in workbookRels.Descendants().Where(e => e.Name.LocalName == "Relationship"))
        {
            var id = (string?)rel.Attribute("Id");
            var target = (string?)rel.Attribute("Target");
            if (id is not null && target is not null)
            {
                if (!relationshipMap.TryAdd(id, ResolvePartPath("xl/workbook.xml", target)))
                    Fail("INVALID_WORKBOOK", "Workbook có relationship trùng ID.");
            }
        }
        var sheets = workbook.Descendants(Spreadsheet + "sheet").ToList();
        var selected = sheets.Where(s => string.Equals((string?)s.Attribute("name"), "Questions", StringComparison.OrdinalIgnoreCase)).ToList();
        if (selected.Count > 1) Fail("DUPLICATE_QUESTIONS_SHEET", "Workbook có nhiều sheet Questions.");
        if (sheets.GroupBy(s => (string?)s.Attribute("name") ?? "", StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            Fail("DUPLICATE_SHEET", "Workbook có tên worksheet trùng nhau.");
        var selectedSheet = selected.SingleOrDefault();
        if (selectedSheet is null && legacyMode)
            selectedSheet = sheets.FirstOrDefault(s => !IsInformational((string?)s.Attribute("name")));
        if (selectedSheet is null) Fail("MISSING_QUESTIONS_SHEET", "Workbook cần sheet Questions chứa dữ liệu câu hỏi.");

        var sharedStrings = new List<string>();
        if (entries.TryGetValue("xl/sharedStrings.xml", out var sharedBytes))
        {
            var sharedDocument = ReadXml(sharedBytes, ct);
            foreach (var item in sharedDocument.Root?.Elements(Spreadsheet + "si") ?? [])
            {
                ct.ThrowIfCancellationRequested();
                // A shared string may be referenced tens of thousands of times. Normalize it
                // once, preserving one string instance instead of allocating per reference.
                sharedStrings.Add(NormalizeNewlines(ReadString(item)));
            }
        }
        var dateStyles = ReadDateStyles(entries, ct);
        List<SourceRow>? questionRows = null;
        Guid? sourceSubjectId = null;
        string questionSheetName = "Questions";
        var textBudget = new ExpandedTextBudget();
        foreach (var sheet in sheets)
        {
            ct.ThrowIfCancellationRequested();
            var name = (string?)sheet.Attribute("name") ?? "";
            var id = (string?)sheet.Attribute(OfficeRelationships + "id");
            var path = id is null ? null : relationshipMap.GetValueOrDefault(id);
            if (path is null)
                Fail("INVALID_WORKBOOK", "Không resolve được worksheet qua workbook relationships.");
            var document = ReadXml(RequiredEntry(entries, path!), ct);
            if (document.Descendants().Any(e => e.Name.LocalName is "drawing" or "legacyDrawing" or "oleObject" ||
                e.Name.NamespaceName.Contains("officeDocument/2006/math", StringComparison.Ordinal)))
                Fail("UNSUPPORTED_DRAWING", "Worksheet chứa drawing/Equation hoặc đối tượng nhúng chưa hỗ trợ.");
            if (ReferenceEquals(sheet, selectedSheet))
            {
                if (document.Descendants(Spreadsheet + "mergeCell").Any())
                    Fail("MERGED_QUESTION_CELLS", "Không hỗ trợ ô gộp trong sheet câu hỏi.");
                questionRows = ReadSheetRows(document, sharedStrings, textBudget, ct, MaxQuestionRows + 1);
                questionSheetName = name;
            }
            else if (string.Equals(name, "Guide", StringComparison.OrdinalIgnoreCase))
            {
                sourceSubjectId = ReadGuideMetadata(ReadSheetRows(document, sharedStrings, textBudget, ct));
            }
            else if (!IsInformational(name) && document.Descendants(Spreadsheet + "c").Any(c =>
                c.Element(Spreadsheet + "f") is not null ||
                !string.IsNullOrWhiteSpace(c.Element(Spreadsheet + "v")?.Value) ||
                !string.IsNullOrWhiteSpace(ReadString(c.Element(Spreadsheet + "is")))))
            {
                Fail("UNSUPPORTED_SHEET", $"Sheet '{name}' có dữ liệu chưa được profile V1 hỗ trợ. Hãy hoàn thiện rubric/mapping nâng cao trong editor.");
            }
        }
        return new(BuildRows(questionRows ?? [], questionSheetName, dateStyles, !legacyMode, ct), sourceSubjectId);
    }

    private static XDocument ReadXml(byte[] bytes, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var stream = new CancellationReadStream(bytes, ct);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxExpandedBytes,
            IgnoreComments = true
        };
        // Bound the number of allocated XML objects, not just decompressed bytes.
        using (var probe = XmlReader.Create(stream, settings))
        {
            var nodes = 0;
            while (probe.Read())
            {
                ct.ThrowIfCancellationRequested();
                if (++nodes > MaxXmlNodesPerPart || probe.Depth > 64)
                    Fail("XML_COMPLEXITY_LIMIT", "XML trong workbook vượt giới hạn 200.000 node hoặc độ sâu 64.");
            }
        }
        stream.Position = 0;
        using var reader = XmlReader.Create(stream, settings);
        var document = XDocument.Load(reader, LoadOptions.None);
        ct.ThrowIfCancellationRequested();
        return document;
    }

    private static byte[] RequiredEntry(Dictionary<string, byte[]> entries, string path)
    {
        if (!entries.TryGetValue(path, out var bytes)) Fail("MISSING_WORKBOOK_PART", $"Workbook thiếu thành phần '{path}'.");
        return bytes!;
    }

    private static string ResolvePartPath(string sourcePart, string target)
    {
        if (target.Contains('\\') || target.Contains(':') || target.Contains('?') || target.Contains('#'))
            Fail("UNSAFE_RELATIONSHIP", "Workbook có relationship target không hợp lệ.");
        var components = new List<string>();
        if (!target.StartsWith('/')) components.AddRange(sourcePart.Split('/').SkipLast(1));
        foreach (var segment in target.Split('/'))
        {
            if (segment is "" or ".") continue;
            if (segment == "..")
            {
                if (components.Count == 0) Fail("UNSAFE_RELATIONSHIP", "Relationship đi ra ngoài package.");
                components.RemoveAt(components.Count - 1);
            }
            else components.Add(segment);
        }
        return string.Join('/', components);
    }

    private static List<SourceRow> ReadSheetRows(XDocument document, List<string> sharedStrings, ExpandedTextBudget textBudget, CancellationToken ct, int? maxNonemptyRows = null)
    {
        var result = new List<SourceRow>();
        var lastRow = 0;
        foreach (var row in document.Descendants(Spreadsheet + "sheetData").Elements(Spreadsheet + "row"))
        {
            ct.ThrowIfCancellationRequested();
            var index = int.TryParse((string?)row.Attribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out var explicitRow)
                ? explicitRow : lastRow + 1;
            if (index <= lastRow || index > 1_048_576) Fail("INVALID_ROW_LOCATION", "Worksheet có số dòng trùng hoặc không hợp lệ.");
            lastRow = index;
            var cells = new Dictionary<int, Cell>();
            var lastColumn = -1;
            foreach (var cell in row.Elements(Spreadsheet + "c"))
            {
                ct.ThrowIfCancellationRequested();
                var reference = (string?)cell.Attribute("r");
                var column = reference is null ? lastColumn + 1 : ColumnIndex(reference, index);
                if (column <= lastColumn || column >= 16384) Fail("INVALID_CELL_LOCATION", "Worksheet có ô trùng hoặc vị trí không hợp lệ.");
                lastColumn = column;
                var type = (string?)cell.Attribute("t") ?? "";
                var raw = cell.Element(Spreadsheet + "v")?.Value ?? "";
                var value = raw;
                if (type == "inlineStr") value = ReadString(cell.Element(Spreadsheet + "is"));
                else if (type == "s")
                {
                    if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var stringIndex) ||
                        stringIndex < 0 || stringIndex >= sharedStrings.Count)
                        Fail("INVALID_SHARED_STRING", "Worksheet tham chiếu shared string không tồn tại.");
                    value = sharedStrings[stringIndex];
                }
                // Charge even unknown, invalid or whitespace cells before normalization or
                // allocating their source-row objects. Shared-string usages each count.
                textBudget.Charge(value);
                var style = 0;
                if (cell.Attribute("s") is not null && !int.TryParse((string?)cell.Attribute("s"), NumberStyles.None, CultureInfo.InvariantCulture, out style))
                    Fail("INVALID_CELL_STYLE", "Worksheet có style không hợp lệ.");
                var hasFormula = cell.Element(Spreadsheet + "f") is not null;
                if (hasFormula || !string.IsNullOrWhiteSpace(value))
                {
                    cells.Add(column, new Cell(type == "s" ? value : NormalizeNewlines(value), type, style, hasFormula));
                    if (cells.Count > MaxPopulatedColumns) Fail("COLUMN_LIMIT_EXCEEDED", "Mỗi dòng chỉ hỗ trợ tối đa 128 cột có dữ liệu.");
                }
            }
            var sourceRow = new SourceRow(index, cells);
            if (IsNonempty(sourceRow))
            {
                result.Add(sourceRow);
                if (maxNonemptyRows is not null && result.Count > maxNonemptyRows)
                    Fail("ROW_LIMIT_EXCEEDED", "Mỗi lô chỉ hỗ trợ tối đa 500 câu hỏi.");
            }
        }
        return result;
    }

    private static int ColumnIndex(string reference, int rowIndex)
    {
        var letters = 0;
        var column = 0;
        while (letters < reference.Length && reference[letters] is >= 'A' and <= 'Z')
        {
            if (letters >= 3) Fail("INVALID_CELL_LOCATION", "Worksheet có tham chiếu ô không hợp lệ.");
            column = column * 26 + reference[letters] - 'A' + 1;
            letters++;
        }
        if (letters == 0 || !int.TryParse(reference.AsSpan(letters), NumberStyles.None, CultureInfo.InvariantCulture, out var row) || row != rowIndex)
            Fail("INVALID_CELL_LOCATION", "Tham chiếu ô không khớp số dòng worksheet.");
        return column - 1;
    }

    private static string ReadString(XElement? element) => element is null ? "" :
        string.Concat(element.Elements().Where(e => e.Name == Spreadsheet + "t" || e.Name == Spreadsheet + "r")
            .SelectMany(e => e.Name == Spreadsheet + "t" ? new[] { e.Value } : e.Elements(Spreadsheet + "t").Select(t => t.Value)));

    private static HashSet<int> ReadDateStyles(Dictionary<string, byte[]> entries, CancellationToken ct)
    {
        var result = new HashSet<int>();
        if (!entries.TryGetValue("xl/styles.xml", out var styles)) return result;
        var document = ReadXml(styles, ct);
        var custom = document.Descendants(Spreadsheet + "numFmt")
            .Where(e => e.Attribute("numFmtId") is not null)
            .ToDictionary(e => (int)e.Attribute("numFmtId")!, e => (string?)e.Attribute("formatCode") ?? "");
        var index = 0;
        foreach (var xf in document.Descendants(Spreadsheet + "cellXfs").Elements(Spreadsheet + "xf"))
        {
            var id = (int?)xf.Attribute("numFmtId") ?? 0;
            var date = id is >= 14 and <= 22 or >= 27 and <= 36 or >= 45 and <= 47 or >= 50 and <= 58;
            if (custom.TryGetValue(id, out var format))
            {
                var withoutLiterals = Regex.Replace(format, "\"[^\"]*\"|\\\\.|\\[[^\\]]*\\]", "");
                date |= Regex.IsMatch(withoutLiterals, "[ymdhs]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            if (date) result.Add(index);
            index++;
        }
        return result;
    }

    private static Guid? ReadGuideMetadata(List<SourceRow> rows)
    {
        Guid? subjectId = null;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (!row.Cells.TryGetValue(0, out var key)) continue;
            var name = key.Value.Trim();
            if (!name.Equals("TemplateVersion", StringComparison.OrdinalIgnoreCase) && !name.Equals("SubjectId", StringComparison.OrdinalIgnoreCase)) continue;
            if (!seen.Add(name)) Fail("DUPLICATE_GUIDE_METADATA", $"Guide khai báo {name} nhiều lần.");
            var valueCell = row.Cells.GetValueOrDefault(1);
            if (key.HasFormula || valueCell?.HasFormula == true)
                Fail("INVALID_GUIDE_METADATA", $"{name} trong Guide phải là dữ liệu cố định, không phải công thức Excel.");
            var value = valueCell?.Value.Trim();
            if (name.Equals("TemplateVersion", StringComparison.OrdinalIgnoreCase))
            {
                if (value is not ("1" or "1.0" or "V1"))
                    Fail("UNSUPPORTED_TEMPLATE_VERSION", $"TemplateVersion '{value}' chưa được hỗ trợ; hãy tải template V1 mới.");
            }
            else
            {
                if (!Guid.TryParseExact(value, "D", out var parsed) || parsed == Guid.Empty || valueCell?.Type is "e" or "b" or "d" or "n")
                    Fail("INVALID_GUIDE_METADATA", "SubjectId trong Guide phải là GUID môn nguồn hợp lệ.");
                subjectId = parsed;
            }
        }
        return subjectId;
    }

    private static bool IsInformational(string? name) => name?.Equals("Guide", StringComparison.OrdinalIgnoreCase) == true ||
        name?.Equals("Topics", StringComparison.OrdinalIgnoreCase) == true;
    private static bool IsNonempty(SourceRow row) => row.Cells.Values.Any(c => c.HasFormula || !string.IsNullOrWhiteSpace(c.Value));

    private static List<RawImportRow> BuildRows(List<SourceRow> source, string sheetName, HashSet<int> dateStyles, bool requireFirstRow, CancellationToken ct)
    {
        var header = source.FirstOrDefault(IsNonempty);
        if (header is null) Fail("MISSING_HEADER", "Sheet câu hỏi thiếu header.");
        if (requireFirstRow && header!.Index != 1) Fail("MISSING_HEADER", "Header Questions phải nằm ở dòng 1.");
        var columns = new Dictionary<int, string>();
        var recognized = new HashSet<int>();
        var uniqueHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (column, cell) in header!.Cells)
        {
            var rawHeader = cell.Value.Trim();
            if (cell.HasFormula || cell.Type is "e" or "b" or "d") Fail("INVALID_HEADER", "Header phải là văn bản cố định.");
            if (rawHeader.Length == 0) continue;
            if (rawHeader.Length > 256) Fail("INVALID_HEADER", "Header quá dài.");
            var known = HeaderAliases.TryGetValue(rawHeader, out var canonical);
            canonical ??= rawHeader;
            if (!uniqueHeaders.Add(canonical)) Fail("DUPLICATE_HEADER", $"Header '{canonical}' bị trùng sau mapping alias.");
            columns.Add(column, canonical);
            if (known) recognized.Add(column);
        }
        if (!columns.Values.Contains("QuestionText", StringComparer.OrdinalIgnoreCase))
            Fail("MISSING_QUESTION_TEXT_HEADER", "Tệp thiếu cột QuestionText.");
        var result = new List<RawImportRow>();
        foreach (var row in source.Where(r => r.Index > header.Index))
        {
            ct.ThrowIfCancellationRequested();
            if (!IsNonempty(row)) continue;
            if (result.Count == MaxQuestionRows) Fail("ROW_LIMIT_EXCEEDED", "Mỗi lô chỉ hỗ trợ tối đa 500 câu hỏi.");
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<ImportFileDiagnostic>();
            foreach (var (column, cell) in row.Cells)
            {
                var field = columns.GetValueOrDefault(column) ?? ColumnLetters(column);
                void Error(string code, string message) => errors.Add(new(code, message, sheetName, row.Index, field, cell.Value));
                if (!recognized.Contains(column) && (cell.HasFormula || !string.IsNullOrWhiteSpace(cell.Value)))
                {
                    Error("UNKNOWN_COLUMN", $"Cột '{field}' có dữ liệu chưa được hỗ trợ; dữ liệu này sẽ không được nhập.");
                    continue;
                }
                if (!columns.ContainsKey(column)) continue;
                if (cell.HasFormula)
                {
                    Error("EXCEL_FORMULA", "Ô chứa công thức tính Excel. Hãy nhập nội dung LaTeX dưới dạng Text; không dùng kết quả cached.");
                    values[field] = "";
                    continue;
                }
                if (cell.Value.Length > MaxFieldCharacters) Error("FIELD_TOO_LONG", "Mỗi trường chỉ hỗ trợ tối đa 20.000 ký tự.");
                if (cell.Type == "e") Error("EXCEL_CELL_ERROR", "Ô Excel chứa giá trị lỗi.");
                if (!string.IsNullOrWhiteSpace(cell.Value) && (cell.Type == "d" ||
                    (dateStyles.Contains(cell.Style) && (cell.Type is "" or "n") && !NumericColumns.Contains(field))))
                    Error("EXCEL_DATE_CELL", "Ô đã được Excel diễn giải thành ngày/giờ. Đổi sang Text và nhập lại dữ liệu từ nguồn.");
                var value = cell.Value;
                if (cell.Type == "b")
                {
                    if (!field.Equals("ReasoningRequired", StringComparison.OrdinalIgnoreCase))
                        Error("EXCEL_BOOLEAN_CELL", "Giá trị boolean không phù hợp với trường văn bản/số này.");
                    else if (value is "0" or "1") value = value == "1" ? "true" : "false";
                    else Error("INVALID_BOOLEAN", "Ô boolean Excel phải có giá trị 0 hoặc 1.");
                }
                else if (cell.Type is not ("" or "n" or "s" or "str" or "inlineStr" or "e" or "d"))
                    Error("UNSUPPORTED_CELL_TYPE", "Kiểu ô Excel chưa được hỗ trợ.");
                values[field] = value;
            }
            result.Add(new(sheetName, row.Index, values, errors));
        }
        return result;
    }

    private static List<RawImportRow> ParseCsv(string text, CancellationToken ct)
    {
        var delimiter = DetectDelimiter(text);
        var source = new List<SourceRow>();
        var field = new StringBuilder();
        var cells = new Dictionary<int, Cell>();
        var column = 0;
        var quoted = false;
        var closedQuote = false;
        var physicalLine = 1;
        var startLine = 1;
        var rowColumnCounts = new Dictionary<int, int>();
        void EndField()
        {
            if (field.Length > MaxFieldCharacters) Fail("FIELD_TOO_LONG", $"Ô CSV ở dòng {startLine} vượt 20.000 ký tự.");
            var value = field.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                cells.Add(column, new Cell(value, "str"));
                if (cells.Count > MaxPopulatedColumns) Fail("COLUMN_LIMIT_EXCEEDED", "Mỗi dòng chỉ hỗ trợ tối đa 128 cột có dữ liệu.");
            }
            column++;
            field.Clear();
            closedQuote = false;
        }
        void EndRecord()
        {
            EndField();
            if (cells.Count != 0)
            {
                source.Add(new(startLine, cells));
                rowColumnCounts[startLine] = column;
                if (source.Count > MaxQuestionRows + 1) Fail("ROW_LIMIT_EXCEEDED", "Mỗi lô chỉ hỗ trợ tối đa 500 câu hỏi.");
            }
            cells = new Dictionary<int, Cell>();
            column = 0;
        }
        for (var i = 0; i < text.Length; i++)
        {
            if ((i & 4095) == 0) ct.ThrowIfCancellationRequested();
            var c = text[i];
            if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                physicalLine++;
                if (quoted) field.Append('\n');
                else { EndRecord(); startLine = physicalLine; }
                continue;
            }
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else { quoted = false; closedQuote = true; }
                }
                else field.Append(c);
            }
            else if (c == delimiter) EndField();
            else if (c == '"')
            {
                if (field.Length != 0 || closedQuote) Fail("MALFORMED_CSV", $"Dấu nháy CSV sai vị trí ở dòng {physicalLine}.");
                quoted = true;
            }
            else
            {
                if (closedQuote) Fail("MALFORMED_CSV", $"Có ký tự sau dấu nháy đóng ở dòng {physicalLine}.");
                field.Append(c);
            }
            if (field.Length > MaxFieldCharacters) Fail("FIELD_TOO_LONG", $"Ô CSV ở dòng {startLine} vượt 20.000 ký tự.");
        }
        if (quoted) Fail("MALFORMED_CSV", $"Ô CSV bắt đầu ở dòng {startLine} chưa đóng dấu nháy.");
        if (field.Length != 0 || column != 0 || closedQuote) EndRecord();
        var header = source.FirstOrDefault(IsNonempty);
        var result = BuildRows(source, "CSV", [], false, ct);
        var expectedColumns = header is null ? 0 : rowColumnCounts[header.Index];
        foreach (var row in result)
        {
            if (rowColumnCounts[row.RowIndex] != expectedColumns)
                row.Diagnostics.Add(new("CSV_COLUMN_COUNT", $"Dòng CSV có {rowColumnCounts[row.RowIndex]} ô; header có {expectedColumns} ô.", "CSV", row.RowIndex));
        }
        return result;
    }

    private static char DetectDelimiter(string text)
    {
        // Headers may be quoted, but may not contain embedded physical newlines.
        var header = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var candidates = new[] { ',', ';', '\t' }.Select(delimiter =>
        {
            var names = header.Split(delimiter).Select(v => v.Trim().Trim('"'));
            return (delimiter, known: names.Count(HeaderAliases.ContainsKey));
        }).OrderByDescending(c => c.known).ToArray();
        if (candidates[0].known == 0) Fail("MISSING_HEADER", "Không nhận diện được header CSV. Hãy dùng template UTF-8 có dấu phẩy, chấm phẩy hoặc tab.");
        if (candidates[0].known == candidates[1].known && candidates[0].known > 1)
            Fail("AMBIGUOUS_CSV_DELIMITER", "Không xác định được dấu phân cách CSV từ header.");
        return candidates[0].delimiter;
    }

    private static string ColumnLetters(int column)
    {
        var name = "";
        for (var value = column + 1; value > 0; value = (value - 1) / 26) name = (char)('A' + (value - 1) % 26) + name;
        return name;
    }

    private static string NormalizeNewlines(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++) value = (value >> 1) ^ ((value & 1) != 0 ? 0xedb88320u : 0);
            table[index] = value;
        }
        return table;
    }

    private static uint ComputeCrc32(byte[] bytes, CancellationToken ct)
    {
        var value = uint.MaxValue;
        for (var index = 0; index < bytes.Length; index++)
        {
            if ((index & 8191) == 0) ct.ThrowIfCancellationRequested();
            value = (value >> 8) ^ CrcTable[(value ^ bytes[index]) & 0xff];
        }
        return ~value;
    }

    private sealed class CancellationReadStream(byte[] bytes, CancellationToken token) : MemoryStream(bytes, false)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            token.ThrowIfCancellationRequested();
            return base.Read(buffer, offset, count);
        }
        public override int Read(Span<byte> buffer)
        {
            token.ThrowIfCancellationRequested();
            return base.Read(buffer);
        }
        public override int ReadByte()
        {
            token.ThrowIfCancellationRequested();
            return base.ReadByte();
        }
    }

    [DoesNotReturn]
    private static void Fail(string code, string message) => throw new QuestionImportFileException(code, message);

    private static Dictionary<string, string> BuildHeaderAliases()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Add(string canonical, params string[] aliases)
        {
            result.Add(canonical, canonical);
            foreach (var alias in aliases) result.TryAdd(alias, canonical);
        }
        Add("QuestionKey", "Mã câu hỏi", "MaCauHoi");
        Add("TopicCode", "Mã chủ đề", "MaChuDe");
        Add("QuestionType", "Loại câu hỏi", "LoaiCauHoi", "Type");
        Add("QuestionText", "Câu hỏi", "CauHoi", "Nội dung", "NoiDung", "Nội dung câu hỏi", "NoiDungCauHoi", "Content");
        Add("GradeLevel", "Khối", "Khoi");
        Add("Difficulty", "Độ khó", "DoKho", "Level");
        Add("CorrectAnswer", "Đáp án đúng", "DapAnDung", "Answer");
        Add("Solution", "Lời giải", "LoiGiai", "Giải thích", "GiaiThich", "Explanation");
        Add("ExpectedReasoning", "Lập luận mong đợi", "LapLuanMongDoi");
        Add("ScoringNotes", "Ghi chú chấm", "GhiChuCham");
        Add("RequiredIdeas", "Ý bắt buộc", "YBatBuoc");
        Add("CommonErrors", "Lỗi thường gặp", "LoiThuongGap");
        Add("MaxScore", "Điểm tối đa", "DiemToiDa", "Score", "Điểm", "Diem");
        Add("EstimatedTimeSeconds", "Thời gian", "ThoiGian", "Time", "TimeSeconds");
        Add("ReasoningRequired", "Yêu cầu giải trình", "YeuCauGiaiTrinh", "RequireReasoning");
        Add("AnswerEvaluationMode", "Chế độ chấm", "CheDoCham", "EvaluationMode");
        Add("Visibility", "Phạm vi", "PhamVi");
        Add("LanguageCode", "Ngôn ngữ", "NgonNgu");
        foreach (var letter in new[] { 'A', 'B', 'C', 'D' })
        {
            Add($"Option{letter}", $"Lựa chọn {letter}", $"LuaChon{letter}", $"Đáp án {letter}", $"DapAn{letter}");
            Add($"Misconception{letter}", $"Quan niệm sai {letter}", $"QuanNiemSai{letter}");
        }
        return result;
    }
}
