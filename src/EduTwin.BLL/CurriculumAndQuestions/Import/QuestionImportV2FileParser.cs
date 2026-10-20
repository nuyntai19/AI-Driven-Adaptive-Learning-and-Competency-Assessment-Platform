using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions.Import;

public sealed record QuestionImportMediaSource(string Reference, string SourcePath, string OriginalFileName)
{
    public double? DisplayAspectRatio { get; init; }
}
public sealed record ParsedQuestionImportV2File(ParsedQuestionImportFile Content,
    Dictionary<string, QuestionImportMediaSource> MediaSources, string SourceProfile, string SourceFormat);

/// <summary>Strict DOCX template and explicit-media package adapters. The caller owns work-directory cleanup.</summary>
public static class QuestionImportV2FileParser
{
    public const long MaxUploadBytes = 30L * 1024 * 1024;
    public const long MaxExpandedBytes = 100L * 1024 * 1024;
    public const long MaxWorkBytes = 100L * 1024 * 1024;
    public const long MaxSourceImageBytes = 8L * 1024 * 1024;
    public const long MaxAllSourceImageBytes = 40L * 1024 * 1024;
    public const int MaxDocxXmlNodesPerPart = 500_000;
    public const int MaxDocxXmlNodesTotal = 1_000_000;
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace M = "http://schemas.openxmlformats.org/officeDocument/2006/math";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private sealed class Limits
    {
        public long Expanded, Written, Media, Text;
        public int Entries, XmlNodes;
        public void ChargeXmlNodes(int count)
        {
            XmlNodes = checked(XmlNodes + count);
            if (XmlNodes > MaxDocxXmlNodesTotal) Fail("XML_COMPLEXITY_LIMIT", "Tổng cấu trúc XML trong DOCX vượt 1.000.000 node.");
        }
        public void ChargeText(string value)
        {
            Text += Encoding.UTF8.GetByteCount(value);
            if (Text > QuestionImportFileParser.MaxNormalizedTextBytes) Fail("NORMALIZED_DATA_LIMIT", "Tổng nội dung sau mở rộng vượt 10 MiB.");
        }
    }
    private sealed class LocatedFieldException(string code, string message, int paragraphIndex) : Exception(message)
    {
        public string Code { get; } = code;
        public int ParagraphIndex { get; } = paragraphIndex;
    }

    public static async Task<ParsedQuestionImportV2File> ParseAsync(Stream source, string fileName,
        string sourceProfile, string workDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (sourceProfile is not ("docx-1.0" or "media-1.0")) Fail("UNSUPPORTED_SOURCE_PROFILE", "Chỉ nhận profile docx-1.0 hoặc media-1.0.");
        var extension = Path.GetExtension(fileName);
        if (!extension.Equals(sourceProfile == "docx-1.0" ? ".docx" : ".zip", StringComparison.OrdinalIgnoreCase))
            Fail("UNSUPPORTED_FILE_TYPE", "Định dạng file không khớp profile đã chọn.");
        var directory = new DirectoryInfo(Path.GetFullPath(workDirectory));
        if (!directory.Exists || directory.LinkTarget is not null) Fail("INVALID_STAGING_DIRECTORY", "Vùng tạm của phiên không hợp lệ.");
        var limits = new Limits();
        var uploadPath = NewPath(directory.FullName);
        try
        {
            string hash;
            long uploadSize;
            using (var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            await using (var upload = NewFile(uploadPath))
            {
                uploadSize = await CopyAsync(source, upload, MaxUploadBytes, limits, false, cancellationToken, digest);
                if (uploadSize == 0) Fail("EMPTY_FILE", "Tệp không có dữ liệu.");
                hash = Convert.ToHexString(digest.GetHashAndReset());
            }
            await using var input = File.OpenRead(uploadPath);
            using var zip = new ZipArchive(input, ZipArchiveMode.Read, false);
            var entries = InspectArchive(zip, limits);
            return sourceProfile == "docx-1.0"
                ? await ParseDocxAsync(entries, directory.FullName, limits, hash, uploadSize, cancellationToken)
                : await ParsePackageAsync(entries, directory.FullName, limits, hash, uploadSize, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (QuestionImportFileException) { throw; }
        catch (Exception ex) when (ex is InvalidDataException or IOException or XmlException or FormatException or OverflowException or ArgumentException or NotSupportedException)
        {
            throw new QuestionImportFileException("INVALID_FILE", "File hoặc cấu trúc archive không hợp lệ. Hãy lưu lại theo mẫu và thử lại.");
        }
    }

    private static Dictionary<string, ZipArchiveEntry> InspectArchive(ZipArchive archive, Limits limits)
    {
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            if (++limits.Entries > 500) Fail("ZIP_ENTRY_LIMIT", "Gói có quá 500 thành phần.");
            if (entry.IsEncrypted) Fail("ENCRYPTED_WORKBOOK", "Không hỗ trợ file mã hóa hoặc có mật khẩu.");
            var path = CanonicalPath(entry.FullName.TrimEnd('/'));
            if (!unique.Add(path)) Fail("DUPLICATE_ARCHIVE_ENTRY", "Archive có tên trùng sau chuẩn hóa.");
            var unixKind = (entry.ExternalAttributes >> 16) & 0xf000;
            if (unixKind == 0xa000) Fail("UNSAFE_ARCHIVE_PATH", "Không hỗ trợ symlink trong archive.");
            if (entry.FullName.EndsWith('/'))
            {
                if (entry.Length != 0) Fail("INVALID_FILE", "Thư mục archive có dữ liệu.");
                continue;
            }
            if (entry.Length > MaxExpandedBytes) Fail("FILE_LIMIT_EXCEEDED", "Thành phần giải nén vượt giới hạn.");
            if (path.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) || path.Contains("/embeddings/", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/activeX/", StringComparison.OrdinalIgnoreCase))
                Fail("UNSUPPORTED_ACTIVE_CONTENT", "Không hỗ trợ macro, OLE hoặc ActiveX.");
            entries.Add(path, entry);
        }
        return entries;
    }

    private static async Task<ParsedQuestionImportV2File> ParsePackageAsync(Dictionary<string, ZipArchiveEntry> entries,
        string directory, Limits limits, string hash, long size, CancellationToken ct)
    {
        var data = entries.Where(e => e.Key is "questions.xlsx" or "questions.csv").ToList();
        if (data.Count != 1) Fail("INVALID_MEDIA_PACKAGE", "Gói phải có đúng một file questions.xlsx hoặc questions.csv.");
        if (entries.Keys.Any(p => p is not ("questions.xlsx" or "questions.csv") && !p.StartsWith("media/", StringComparison.Ordinal)))
            Fail("UNSUPPORTED_PACKAGE_PART", "Gói chỉ nhận file câu hỏi và thư mục media/ theo mẫu.");
        var dataPath = await SpoolEntryAsync(data[0].Value, directory, QuestionImportFileParser.MaxUploadBytes, limits, ct);
        if (data[0].Key.EndsWith(".xlsx", StringComparison.Ordinal))
        {
            // The inner adapter independently bounds actual reads and verifies size/CRC. Reserving
            // each advertised size here composes its verified budget with the outer archive.
            await using var nestedFile = File.OpenRead(dataPath);
            using var nested = new ZipArchive(nestedFile, ZipArchiveMode.Read);
            foreach (var entry in nested.Entries)
            {
                if (++limits.Entries > 500) Fail("ZIP_ENTRY_LIMIT", "Tổng gói và workbook vượt 500 thành phần.");
                ChargeExpanded(limits, entry.Length);
            }
        }
        ParsedQuestionImportFile content;
        await using (var dataFile = File.OpenRead(dataPath))
            content = await QuestionImportFileParser.ParseMediaTabularAsync(dataFile, data[0].Key, ct);
        var rows = content.Rows.Select(row => row with
        {
            CandidateId = $"{data[0].Key}:{row.SheetName}:{row.RowIndex}",
            SourceLocation = new() { PartPath = data[0].Key, TableRowIndex = row.RowIndex }
        }).ToList();
        var references = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (!row.Values.TryGetValue("ImageRef", out var raw) || string.IsNullOrWhiteSpace(raw)) continue;
            var reference = CanonicalPath(raw.Trim());
            row.Values["ImageRef"] = reference;
            if (!reference.StartsWith("media/", StringComparison.Ordinal)) Fail("UNSAFE_IMAGE_REFERENCE", "ImageRef phải trỏ tới media/ trong gói.");
            references.Add(reference);
            if (string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("QuestionKey"))) AddDiagnostic(row, "QuestionKey", "REQUIRED_QUESTION_KEY", "Câu có ảnh cần Mã câu hỏi.");
            if (!entries.ContainsKey(reference)) AddDiagnostic(row, "ImageRef", "IMAGE_NOT_FOUND", "Không tìm thấy ảnh được tham chiếu trong gói.");
        }
        if (rows.Count(r => !string.IsNullOrWhiteSpace(r.Values.GetValueOrDefault("ImageRef"))) > 20)
            Fail("IMAGE_COUNT_LIMIT", "Mỗi lô chỉ nhận tối đa 20 câu có ảnh.");
        var media = new Dictionary<string, QuestionImportMediaSource>(StringComparer.Ordinal);
        foreach (var (path, entry) in entries.Where(e => e.Key.StartsWith("media/", StringComparison.Ordinal)))
        {
            if (!references.Contains(path)) Fail("UNREFERENCED_MEDIA", "Gói có ảnh không được câu nào tham chiếu.");
            try { media.Add(path, await ReadMediaAsync(path, entry, directory, limits, ct)); }
            catch (QuestionImportFileException ex) when (ex.Code == "SOURCE_IMAGE_TOO_LARGE")
            { foreach (var row in rows.Where(r => r.Values.GetValueOrDefault("ImageRef") == path)) AddDiagnostic(row, "ImageRef", ex.Code, ex.Message); }
        }
        return new(new(rows, content.Diagnostics, hash, size) { SourceSubjectId = content.SourceSubjectId }, media, "media-1.0", "zip");
    }

    private static async Task<ParsedQuestionImportV2File> ParseDocxAsync(Dictionary<string, ZipArchiveEntry> entries,
        string directory, Limits limits, string hash, long size, CancellationToken ct)
    {
        XDocument? document = null;
        XDocument? relDocument = null;
        foreach (var (path, entry) in entries)
        {
            ct.ThrowIfCancellationRequested();
            if (path.StartsWith("word/media/", StringComparison.Ordinal)) continue;
            var bytes = await ReadEntryAsync(entry, limits, ct);
            if (!path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)) continue;
            var xml = QuestionImportFileParser.ReadXml(bytes, ct, MaxDocxXmlNodesPerPart, limits.ChargeXmlNodes);
            if (path == "word/document.xml") document = xml;
            else if (path == "word/_rels/document.xml.rels") relDocument = xml;
            if (path.EndsWith(".rels", StringComparison.OrdinalIgnoreCase) && xml.Descendants().Any(e => e.Name.LocalName == "Relationship" &&
                string.Equals((string?)e.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase)))
                Fail("EXTERNAL_REFERENCE", "Không hỗ trợ liên kết hoặc ảnh ngoài tài liệu Word.");
            if (xml.Descendants().Any(e => ((string?)e.Attribute("ContentType"))?.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase) == true))
                Fail("UNSUPPORTED_ACTIVE_CONTENT", "Không hỗ trợ tài liệu Word có macro.");
            if (path.StartsWith("word/", StringComparison.Ordinal) && path != "word/document.xml" && xml.Descendants().Any(e =>
                e.Name == W + "t" && !string.IsNullOrWhiteSpace(e.Value) || e.Name == W + "drawing" || e.Name == M + "oMath"))
                Fail("UNSUPPORTED_DOCX_STORY", "Nội dung trong header/footer/footnote hoặc phần Word ngoài mẫu chưa được hỗ trợ.");
        }
        if (document is null) Fail("MISSING_DOCUMENT_PART", "DOCX thiếu word/document.xml.");
        var body = document.Root?.Name == W + "document" ? document.Root.Element(W + "body") : null;
        if (body is null) Fail("INVALID_DOCX_TEMPLATE", "DOCX thiếu phần body hợp lệ.");
        if (document.Descendants().Any(e => e.Name.Namespace == W && e.Name.LocalName is "ins" or "del" or "moveFrom" or "moveTo" or "fldChar" or "instrText" or "fldSimple" or "altChunk" or "sdt" or "customXml"))
            Fail("UNSUPPORTED_DOCX_CONTENT", "Không nhận tracked changes, field hoặc nội dung nhúng ngoài profile Word.");
        var relationships = new Dictionary<string, string>(StringComparer.Ordinal);
        if (relDocument is not null)
            foreach (var rel in relDocument.Descendants().Where(e => e.Name.LocalName == "Relationship"))
            {
                var id = (string?)rel.Attribute("Id"); var target = (string?)rel.Attribute("Target");
                if (id is null || target is null || !relationships.TryAdd(id, ResolveWordTarget(target)))
                    Fail("INVALID_DOCX_RELATIONSHIP", "DOCX có relationship thiếu hoặc trùng ID.");
            }
        var tables = body.Elements(W + "tbl").ToList();
        foreach (var node in body.Elements())
            if (node.Name != W + "tbl" && node.Name != W + "sectPr" &&
                !(node.Name == W + "p" && !node.Descendants(W + "t").Any(t => !string.IsNullOrWhiteSpace(t.Value)) && !node.Descendants(W + "drawing").Any() && !node.Descendants(M + "oMath").Any()))
                Fail("UNSUPPORTED_DOCX_BODY", "Nội dung ngoài bảng câu hỏi/hướng dẫn của mẫu chưa được hỗ trợ.");
        if (tables.Count == 0) Fail("INVALID_DOCX_TEMPLATE", "DOCX cần bảng metadata theo mẫu EduTwin.");
        var metadata = TableCells(tables[0]);
        if (metadata.Count == 0 || metadata[0].Count != 2 || Plain(metadata[0][0]) != "EduTwin Import" || Plain(metadata[0][1]) != "docx-1.0")
            Fail("INVALID_DOCX_TEMPLATE", "Bảng đầu tiên phải là metadata EduTwin Import / docx-1.0.");
        var sourceSubject = ReadMetadata(metadata);
        var candidates = new List<RawImportRow>();
        var mediaTargets = new Dictionary<string, string>(StringComparer.Ordinal);
        var displayRatios = new Dictionary<string, double?>(StringComparer.Ordinal);
        var allImageTargets = new HashSet<string>(StringComparer.Ordinal);
        for (var tableIndex = 1; tableIndex < tables.Count; tableIndex++)
        {
            ct.ThrowIfCancellationRequested();
            var table = tables[tableIndex];
            var cells = TableCells(table);
            var marker = cells.Count == 0 || cells[0].Count == 0 ? "" : Plain(cells[0][0]);
            if (marker is "EduTwin Guide" or "EduTwin Topics")
            {
                if (table.Descendants(W + "drawing").Any() || table.Descendants(M + "oMath").Any()) Fail("UNSUPPORTED_GUIDE_CONTENT", "Bảng hướng dẫn/danh mục chỉ nhận văn bản.");
                continue;
            }
            if (cells.Count == 0 || cells[0].Count != 2 || marker != "Trường" || Plain(cells[0][1]) != "Nội dung")
                Fail("INVALID_DOCX_TEMPLATE", "Không nhận diện được bảng câu hỏi Trường / Nội dung.");
            if (candidates.Count == 500) Fail("ROW_LIMIT_EXCEEDED", "Mỗi lô chỉ nhận tối đa 500 câu hỏi.");
            var index = candidates.Count + 1;
            var location = new QuestionImportSourceLocationDto { PartPath = "word/document.xml", QuestionIndex = index, TableIndex = tableIndex + 1 };
            var row = new RawImportRow("DOCX", index, new(StringComparer.OrdinalIgnoreCase), []) { CandidateId = $"docx:table:{tableIndex + 1}", SourceLocation = location };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var blip in table.Descendants(A + "blip"))
                if ((string?)blip.Attribute(R + "embed") is { } imageId && relationships.TryGetValue(imageId, out var imageTarget)) allImageTargets.Add(imageTarget);
            var modeCell = cells.Skip(1).FirstOrDefault(c => c.Count == 2 && QuestionImportFileParser.CanonicalHeader(Plain(c[0])) == "AnswerEvaluationMode");
            var autoAnswer = modeCell is not null && Plain(modeCell[1]) is "NumericRational" or "Coordinate2D" or "MathEquivalent";
            for (var fieldRow = 1; fieldRow < cells.Count; fieldRow++)
            {
                var pair = cells[fieldRow];
                var fieldLocation = location with { TableRowIndex = fieldRow + 1, CellIndex = 2 };
                if (pair.Count != 2) { AddDiagnostic(row, "File", "INVALID_DOCX_TABLE", "Bảng câu hỏi phải có đúng hai cột.", fieldLocation); continue; }
                var label = Plain(pair[0]);
                var imageField = label == "Ảnh đề bài";
                var field = imageField ? "ImageRef" : QuestionImportFileParser.CanonicalHeader(label);
                if (pair[0].Descendants().Any(e => e.Name == W + "drawing" || e.Name.Namespace == M))
                { AddDiagnostic(row, "File", "INVALID_DOCX_LABEL", "Nhãn trường chỉ nhận văn bản.", fieldLocation); continue; }
                if (field is null) { if (!string.IsNullOrWhiteSpace(Plain(pair[1])) || pair[1].Descendants().Any(e => e.Name == W + "drawing" || e.Name.Namespace == M)) AddDiagnostic(row, label, "UNKNOWN_FIELD", "Trường Word chưa được hỗ trợ.", fieldLocation); continue; }
                if (!seen.Add(field)) { AddDiagnostic(row, field, "DUPLICATE_FIELD", "Trường bị trùng sau ánh xạ Việt/Anh.", fieldLocation); continue; }
                try
                {
                    if (imageField)
                    {
                        var target = ImageTarget(pair[1], relationships, out var displayRatio);
                        if (target is not null)
                        {
                            var reference = $"media/docx-question-{index:D4}{Path.GetExtension(target)}";
                            row.Values["ImageRef"] = reference;
                            mediaTargets.Add(reference, target);
                            displayRatios.Add(reference, displayRatio);
                        }
                    }
                    else
                    {
                        if (field == "CorrectAnswer") row.CorrectAnswerContainsNativeMath = pair[1].Descendants().Any(e => e.Name == M + "oMath" || e.Name == M + "oMathPara");
                        row.Values[field] = ReadCellText(pair[1], field == "CorrectAnswer" && autoAnswer, ct);
                    }
                    limits.ChargeText(row.Values.GetValueOrDefault(field) ?? "");
                }
                catch (LocatedFieldException ex) { AddDiagnostic(row, field, ex.Code, ex.Message, fieldLocation with { ParagraphIndex = ex.ParagraphIndex }); }
                catch (QuestionImportFileException ex) when (ex.Code != "NORMALIZED_DATA_LIMIT")
                { AddDiagnostic(row, field, ex.Code, ex.Message, fieldLocation with { ParagraphIndex = 1, RelationshipId = (string?)pair[1].Descendants(A + "blip").FirstOrDefault()?.Attribute(R + "embed") }); }
            }
            if (string.IsNullOrWhiteSpace(row.Values.GetValueOrDefault("QuestionKey"))) AddDiagnostic(row, "QuestionKey", "REQUIRED_QUESTION_KEY", "Mã câu hỏi bắt buộc trong mẫu Word.");
            candidates.Add(row);
        }
        if (candidates.Count == 0) Fail("NO_QUESTIONS", "DOCX không có bảng câu hỏi.");
        if (mediaTargets.Count > 20) Fail("IMAGE_COUNT_LIMIT", "Mỗi lô chỉ nhận tối đa 20 câu có ảnh.");
        if (entries.Keys.Any(p => p.StartsWith("word/media/", StringComparison.Ordinal) && !allImageTargets.Contains(p)))
            Fail("UNREFERENCED_MEDIA", "DOCX chứa ảnh ngoài ô ảnh câu hỏi hoặc ảnh không được tham chiếu.");
        var mediaSources = new Dictionary<string, QuestionImportMediaSource>(StringComparer.Ordinal);
        var loaded = new Dictionary<string, QuestionImportMediaSource>(StringComparer.Ordinal);
        foreach (var (reference, target) in mediaTargets)
        {
            if (!entries.TryGetValue(target, out var entry))
            {
                foreach (var row in candidates.Where(r => r.Values.GetValueOrDefault("ImageRef") == reference)) AddDiagnostic(row, "ImageRef", "IMAGE_NOT_FOUND", "DOCX tham chiếu ảnh không tồn tại.");
                continue;
            }
            if (!loaded.TryGetValue(target, out var asset))
            {
                try { asset = await ReadMediaAsync(reference, entry, directory, limits, ct); loaded[target] = asset; }
                catch (QuestionImportFileException ex) when (ex.Code == "SOURCE_IMAGE_TOO_LARGE")
                { foreach (var row in candidates.Where(r => r.Values.GetValueOrDefault("ImageRef") == reference)) AddDiagnostic(row, "ImageRef", ex.Code, ex.Message); continue; }
            }
            mediaSources.Add(reference, asset with { Reference = reference, DisplayAspectRatio = displayRatios[reference] });
        }
        return new(new(candidates, [], hash, size) { SourceSubjectId = sourceSubject }, mediaSources, "docx-1.0", "docx");
    }

    private static List<List<XElement>> TableCells(XElement table)
    {
        if (table.Elements().Any(e => e.Name != W + "tr" && e.Name != W + "tblPr" && e.Name != W + "tblGrid") ||
            table.Elements(W + "tr").Any(row => row.Elements().Any(e => e.Name != W + "tc" && e.Name != W + "trPr" && e.Name != W + "tblPrEx")))
            Fail("UNSUPPORTED_DOCX_TABLE", "Bảng Word chứa wrapper hoặc cấu trúc ngoài profile; không thể bỏ qua dữ liệu.");
        if (table.Descendants(W + "tbl").Any() || table.Descendants().Any(e => e.Name == W + "gridSpan" || e.Name == W + "vMerge" || e.Name == W + "hMerge"))
            Fail("UNSUPPORTED_DOCX_TABLE", "Không nhận bảng lồng hoặc ô gộp trong mẫu Word.");
        return table.Elements(W + "tr").Select(row => row.Elements(W + "tc").ToList()).ToList();
    }

    private static Guid ReadMetadata(List<List<XElement>> rows)
    {
        string? version = null; Guid? subject = null;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Skip(1))
        {
            if (row.Count != 2) Fail("INVALID_GUIDE_METADATA", "Metadata Word cần đúng hai cột.");
            var key = Plain(row[0]); var value = Plain(row[1]);
            var canonical = key.Equals("TemplateVersion", StringComparison.OrdinalIgnoreCase) || key.Equals("Phiên bản mẫu", StringComparison.OrdinalIgnoreCase) ? "TemplateVersion" :
                key.Equals("SubjectId", StringComparison.OrdinalIgnoreCase) || key.Equals("Mã định danh môn học", StringComparison.OrdinalIgnoreCase) ? "SubjectId" : null;
            if (canonical is null) Fail("INVALID_GUIDE_METADATA", "Metadata Word có trường không được hỗ trợ.");
            if (!seen.Add(canonical)) Fail("DUPLICATE_GUIDE_METADATA", "Metadata Word bị trùng trường Việt/Anh.");
            if (row.Any(c => c.Descendants(M + "oMath").Any() || c.Descendants(W + "drawing").Any() || c.Descendants(W + "fldSimple").Any()))
                Fail("INVALID_GUIDE_METADATA", "Metadata phải là văn bản cố định.");
            if (canonical == "TemplateVersion") version = value;
            else if (Guid.TryParseExact(value, "D", out var id) && id != Guid.Empty) subject = id;
            else Fail("INVALID_GUIDE_METADATA", "Mã định danh môn học trong Word không hợp lệ.");
        }
        if (version != "2.0") Fail("UNSUPPORTED_TEMPLATE_VERSION", "DOCX cần TemplateVersion 2.0.");
        return subject ?? throw new QuestionImportFileException("INVALID_GUIDE_METADATA", "DOCX thiếu mã định danh môn học.");
    }

    private static string ReadCellText(XElement cell, bool bareMath, CancellationToken ct)
    {
        var text = new StringBuilder();
        void Append(string value) { if (text.Length + value.Length > 20_000) Fail("FIELD_TOO_LONG", "Mỗi trường chỉ nhận tối đa 20.000 ký tự."); text.Append(value); }
        void Visit(XElement element)
        {
            ct.ThrowIfCancellationRequested();
            if (element.Name == M + "oMathPara")
            {
                if (bareMath) Append(OmmlLatexConverter.Convert(element, ct));
                else Append("$$" + OmmlLatexConverter.Convert(element, ct) + "$$");
                return;
            }
            if (element.Name == M + "oMath") { var math = OmmlLatexConverter.Convert(element, ct); Append(bareMath ? math : "$" + math + "$"); return; }
            if (element.Name == W + "t") { Append(element.Value); return; }
            if (element.Name == W + "tab") { Append("\t"); return; }
            if (element.Name == W + "br" || element.Name == W + "cr") { Append("\n"); return; }
            if (element.Name == W + "tcPr" || element.Name == W + "pPr" || element.Name == W + "rPr" || element.Name == W + "bookmarkStart" || element.Name == W + "bookmarkEnd" || element.Name == W + "proofErr") return;
            if (element.Name != W + "tc" && element.Name != W + "p" && element.Name != W + "r")
                Fail("UNSUPPORTED_DOCX_CONTENT", $"Nội dung Word '{element.Name.LocalName}' chưa được hỗ trợ trong trường văn bản.");
            if (element.Name == W + "r" && element.Element(W + "rPr")?.Element(W + "vanish") is not null)
                Fail("UNSUPPORTED_DOCX_CONTENT", "Không nhận nội dung ẩn trong câu hỏi.");
            foreach (var child in element.Elements()) Visit(child);
            if (element.Name == W + "p") Append("\n");
        }
        var paragraph = 0;
        foreach (var child in cell.Elements())
        {
            if (child.Name == W + "tcPr") continue;
            paragraph++;
            try { Visit(child); }
            catch (QuestionImportFileException ex) { throw new LocatedFieldException(ex.Code, ex.Message, paragraph); }
        }
        return text.ToString().TrimEnd('\n');
    }

    private static string? ImageTarget(XElement cell, Dictionary<string, string> relationships, out double? displayAspectRatio)
    {
        displayAspectRatio = null;
        if (cell.Descendants(W + "t").Any(t => !string.IsNullOrWhiteSpace(t.Value))) Fail("UNSUPPORTED_IMAGE_CONTENT", "Ô ảnh chỉ nhận ảnh; chuyển chú thích sang Nội dung câu hỏi.");
        if (cell.Descendants().Any(e => e.Name.Namespace == M || e.Name.Namespace == W && e.Name.LocalName is "pict" or "object" or "sym" or "footnoteReference" or "endnoteReference"))
            Fail("UNSUPPORTED_IMAGE_CONTENT", "Không nhận Equation, OLE, ký hiệu hoặc tham chiếu bổ sung trong ô ảnh.");
        var drawings = cell.Descendants(W + "drawing").ToList();
        if (drawings.Count == 0)
        {
            if (cell.Descendants().Any(e => e.Name.LocalName is "pict" or "object" || e.Name.Namespace == M)) Fail("UNSUPPORTED_IMAGE_CONTENT", "Không nhận OLE/Equation/ảnh kiểu cũ trong ô ảnh.");
            return null;
        }
        if (drawings.Count != 1 || drawings[0].Elements(Wp + "inline").Count() != 1 || cell.Descendants(Wp + "anchor").Any() ||
            cell.Descendants().Any(e => e.Name.LocalName is "grpSp" or "wgp")) Fail("UNSUPPORTED_IMAGE_LAYOUT", "Chỉ nhận một ảnh inline trong đúng ô ảnh đề.");
        if (cell.Descendants(A + "srcRect").Any(e => e.Attributes().Any(a => a.Value != "0")) ||
            cell.Descendants(A + "xfrm").Any(e => e.Attributes().Any(a => a.Name.LocalName is "flipH" or "flipV" && a.Value is "1" or "true" || a.Name.LocalName == "rot" && a.Value != "0")))
            Fail("UNSUPPORTED_IMAGE_TRANSFORM", "Ảnh bị crop/xoay/lật chưa được hỗ trợ; hãy xuất ảnh đã xử lý rồi chèn lại.");
        var blips = cell.Descendants(A + "blip").ToList();
        if (blips.Any(b => b.Elements().Any(e => e.Name != A + "extLst")) ||
            cell.Descendants().Any(e => e.Name == A + "tile" || e.Name == A + "custGeom" ||
                e.Name == A + "prstGeom" && (string?)e.Attribute("prst") != "rect" ||
                e.Name == A + "effectLst" && e.HasElements || e.Name == A + "effectDag" && e.HasElements))
            Fail("UNSUPPORTED_IMAGE_TRANSFORM", "Hiệu ứng, mask hoặc lặp ảnh chưa được hỗ trợ; hãy xuất ảnh đã xử lý rồi chèn lại.");
        var extents = drawings[0].Element(Wp + "inline")!.Elements(Wp + "extent").ToArray();
        if (extents.Length > 1) Fail("UNSUPPORTED_IMAGE_LAYOUT", "Ảnh có nhiều kích thước hiển thị mâu thuẫn.");
        var extent = extents.SingleOrDefault();
        if (extent is null)
        {
            var pictureExtents = cell.Descendants(A + "xfrm").Elements(A + "ext").ToArray();
            if (pictureExtents.Length > 1) Fail("UNSUPPORTED_IMAGE_LAYOUT", "Ảnh có nhiều kích thước hiển thị mâu thuẫn.");
            extent = pictureExtents.SingleOrDefault();
        }
        if (extent is not null)
        {
            var validWidth = long.TryParse((string?)extent.Attribute("cx"), NumberStyles.None, CultureInfo.InvariantCulture, out var width);
            var validHeight = long.TryParse((string?)extent.Attribute("cy"), NumberStyles.None, CultureInfo.InvariantCulture, out var height);
            if (!validWidth || width <= 0 || !validHeight || height <= 0)
                Fail("UNSUPPORTED_IMAGE_LAYOUT", "Kích thước hiển thị ảnh trong Word phải là số nguyên dương.");
            displayAspectRatio = (double)width / height;
        }
        var id = blips.Count == 1 ? (string?)blips[0].Attribute(R + "embed") : null;
        var target = id is null ? null : relationships.GetValueOrDefault(id);
        if (id is null || blips[0].Attribute(R + "link") is not null || target is null)
            Fail("INVALID_IMAGE_RELATIONSHIP", "Ảnh inline thiếu relationship nội bộ hợp lệ.");
        if (!target.StartsWith("word/media/", StringComparison.Ordinal)) Fail("INVALID_IMAGE_RELATIONSHIP", "Ảnh phải thuộc media nội bộ của DOCX.");
        return target;
    }

    private static string Plain(XElement cell) => string.Join('\n', cell.Elements(W + "p").Select(p => string.Concat(p.Descendants(W + "t").Select(t => t.Value)))).Trim();
    private static void AddDiagnostic(RawImportRow row, string field, string code, string message, QuestionImportSourceLocationDto? location = null)
        => row.Diagnostics.Add(new(code, message, row.SheetName, row.RowIndex, field) { SourceLocation = (location ?? row.SourceLocation) is { } source ? source with { Field = field } : null });

    private static async Task<QuestionImportMediaSource> ReadMediaAsync(string reference, ZipArchiveEntry entry, string directory, Limits limits, CancellationToken ct)
    {
        if (entry.Length > MaxSourceImageBytes) Fail("SOURCE_IMAGE_TOO_LARGE", "Ảnh nguồn vượt 8 MiB.");
        if (limits.Media + entry.Length > MaxAllSourceImageBytes) Fail("SOURCE_IMAGE_LIMIT", "Tổng ảnh nguồn vượt 40 MiB.");
        var path = await SpoolEntryAsync(entry, directory, MaxSourceImageBytes, limits, ct);
        limits.Media += new FileInfo(path).Length;
        if (limits.Media > MaxAllSourceImageBytes) Fail("SOURCE_IMAGE_LIMIT", "Tổng ảnh nguồn vượt 40 MiB.");
        return new(reference, path, Path.GetFileName(entry.FullName));
    }
    private static async Task<byte[]> ReadEntryAsync(ZipArchiveEntry entry, Limits limits, CancellationToken ct)
    {
        await using var input = entry.Open(); using var output = new MemoryStream();
        var crc = uint.MaxValue; var buffer = new byte[8192]; long length = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, ct); if (read == 0) break;
            length += read; ChargeExpanded(limits, read);
            if (length > QuestionImportFileParser.MaxExpandedBytes) Fail("FILE_LIMIT_EXCEEDED", "Thành phần XML vượt giới hạn 50 MiB.");
            crc = QuestionImportFileParser.UpdateCrc32(crc, buffer.AsSpan(0, read)); await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        if (length != entry.Length || ~crc != entry.Crc32) Fail("ZIP_INTEGRITY_FAILED", "Archive chứa thành phần bị hỏng.");
        return output.ToArray();
    }
    private static async Task<string> SpoolEntryAsync(ZipArchiveEntry entry, string directory, long maxBytes, Limits limits, CancellationToken ct)
    {
        var path = NewPath(directory);
        await using var input = entry.Open(); await using var output = NewFile(path);
        var crc = uint.MaxValue; long length = 0; var buffer = new byte[8192];
        while (true)
        {
            var read = await input.ReadAsync(buffer, ct); if (read == 0) break;
            length += read; ChargeExpanded(limits, read);
            if (length > maxBytes) Fail("FILE_LIMIT_EXCEEDED", "Thành phần vượt giới hạn dung lượng.");
            ChargeWritten(limits, read); crc = QuestionImportFileParser.UpdateCrc32(crc, buffer.AsSpan(0, read));
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        if (length != entry.Length || ~crc != entry.Crc32) Fail("ZIP_INTEGRITY_FAILED", "Archive chứa thành phần bị hỏng.");
        return path;
    }
    private static async Task<long> CopyAsync(Stream input, Stream output, long maximum, Limits limits, bool expanded, CancellationToken ct, IncrementalHash digest)
    {
        long length = 0; var buffer = new byte[8192];
        while (true)
        {
            var read = await input.ReadAsync(buffer, ct); if (read == 0) break;
            length += read; if (length > maximum) Fail("FILE_LIMIT_EXCEEDED", "File upload vượt 30 MiB.");
            if (expanded) ChargeExpanded(limits, read); ChargeWritten(limits, read); digest.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return length;
    }
    private static string NewPath(string directory) => Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
    private static FileStream NewFile(string path)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.None, BufferSize = 8192, Options = FileOptions.Asynchronous };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new(path, options);
    }
    private static void ChargeExpanded(Limits limits, long count) { limits.Expanded += count; if (limits.Expanded > MaxExpandedBytes) Fail("FILE_LIMIT_EXCEEDED", "Tổng giải nén vượt 100 MiB."); }
    private static void ChargeWritten(Limits limits, long count) { limits.Written += count; if (limits.Written > MaxWorkBytes) Fail("STAGING_LIMIT", "Vùng tạm của phiên vượt 100 MiB."); }
    private static string CanonicalPath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\\') || path.Contains(':') || path.Contains('%') || path.StartsWith('/') || path.Any(char.IsControl))
            Fail("UNSAFE_ARCHIVE_PATH", "Đường dẫn trong archive không hợp lệ.");
        var normalized = path.Normalize(NormalizationForm.FormC);
        if (normalized.Split('/').Any(p => p is "" or "." or ".." || p != p.Trim())) Fail("UNSAFE_ARCHIVE_PATH", "Đường dẫn trong archive không hợp lệ.");
        return normalized;
    }
    private static string ResolveWordTarget(string target)
    {
        if (target.StartsWith('/')) return CanonicalPath(target[1..]);
        var parts = new List<string> { "word" };
        foreach (var part in target.Split('/'))
        {
            if (part == "..") { if (parts.Count == 0) Fail("UNSAFE_ARCHIVE_PATH", "Relationship ra ngoài DOCX."); parts.RemoveAt(parts.Count - 1); }
            else if (part != ".") parts.Add(part);
        }
        return CanonicalPath(string.Join('/', parts));
    }
    [DoesNotReturn]
    private static void Fail(string code, string message) => throw new QuestionImportFileException(code, message);
}
