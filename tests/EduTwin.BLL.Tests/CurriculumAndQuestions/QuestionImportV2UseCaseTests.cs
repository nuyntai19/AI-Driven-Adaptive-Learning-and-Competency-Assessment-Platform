using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.CurriculumAndQuestions.Import;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using SkiaSharp;
using Xunit;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

[CollectionDefinition("Question import use cases", DisableParallelization = true)]
public sealed class QuestionImportUseCaseCollection { }

[Collection("Question import use cases")]
public sealed class QuestionImportV2UseCaseTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "edutwin-v2-test-" + Guid.NewGuid().ToString("N"));
    private readonly Guid _center = Guid.NewGuid(), _actor = Guid.NewGuid(), _subject = Guid.NewGuid();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<TimeProvider> _clock = new();
    private readonly EduTwinDbContext _db;
    private readonly QuestionImportMediaStore _store;
    private readonly DateTimeOffset _now = new(2026, 10, 15, 1, 0, 0, TimeSpan.Zero);
    private const string Header = "QuestionKey,TopicCode,QuestionType,QuestionText,CorrectAnswer,Solution,ImageRef";
    public QuestionImportV2UseCaseTests()
    {
        _tenant.Setup(t => t.IsResolved).Returns(true); _tenant.Setup(t => t.CenterId).Returns(_center); _tenant.Setup(t => t.UserId).Returns(_actor); _tenant.Setup(t => t.Role).Returns("Teacher"); _clock.Setup(t => t.GetUtcNow()).Returns(_now);
        var accessor = new Mock<ITenantIdAccessor>(); accessor.Setup(t => t.CenterId).Returns(_center);
        _db = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options, accessor.Object);
        var user = new User { UserId = _actor, CenterId = _center, Username = "v2.teacher", DisplayName = "Teacher", PasswordHash = "hash", RoleName = UserRole.Teacher, Status = UserStatus.Active, CreatedAt = _now.UtcDateTime, UpdatedAt = _now.UtcDateTime };
        _db.Users.Add(user); _db.Teachers.Add(new Teacher { TeacherId = _actor, CenterId = _center, User = user, CreatedAt = _now.UtcDateTime, UpdatedAt = _now.UtcDateTime });
        _db.Subjects.Add(new Subject { SubjectId = _subject, CenterId = _center, SubjectCode = "MATH", SubjectName = "Toán", IsActive = true, CreatedAt = _now.UtcDateTime, UpdatedAt = _now.UtcDateTime });
        _db.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = _center, SubjectId = _subject, NodeCode = "TOP-1", NodeName = "Topic", NodeType = NodeType.Topic, IsActive = true, CreatedAt = _now.UtcDateTime, UpdatedAt = _now.UtcDateTime }); _db.SaveChanges();
        GraphScopeFixture.Approve(_db, _center, _subject, 12, 1); _db.SaveChanges();
        _store = new(_root);
    }
    private QuestionImportUseCase Sut() => new(_db, _tenant.Object, _clock.Object, mediaStore: _store);
    private QuestionImportV2PreviewRequest Defaults(string profile = "media-1.0") => new() { SubjectId = _subject, GradeLevel = 12, SourceProfile = profile };
    private static byte[] Image(int width = 16, int height = 12, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(width, height); using (var canvas = new SKCanvas(bitmap)) { canvas.Clear(SKColors.Transparent); using var paint = new SKPaint { Color = SKColors.Red }; canvas.DrawRect(0, 0, width / 2, height, paint); }
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(format, 90); return data.ToArray();
    }
    private static MemoryStream Package(string csv, params (string Name, byte[] Data)[] images)
    {
        var file = new MemoryStream(); using (var zip = new ZipArchive(file, ZipArchiveMode.Create, true))
        {
            using (var output = new StreamWriter(zip.CreateEntry("questions.csv").Open(), Encoding.UTF8)) output.Write(csv);
            foreach (var (name, data) in images) using (var output = zip.CreateEntry(name).Open()) output.Write(data);
        }
        file.Position = 0; return file;
    }
    private async Task<QuestionImportPreviewResult> Preview(string csv, params (string Name, byte[] Data)[] images)
    { using var file = Package(csv, images); return await Sut().PreviewV2Async(file, "questions.zip", Defaults(), default); }
    private string[] PngFiles() => Directory.GetFiles(_root, "*.png", SearchOption.AllDirectories);

    [Fact]
    public async Task ImageOnlyQuestion_UsesProtectedPreview_AndPersistsPngInDraftTransaction()
    {
        var preview = await Preview(Header + "\nQ1,TOP-1,ShortAnswer,,2,Lời giải,media/a.jpg", ("media/a.jpg", Image(format: SKEncodedImageFormat.Jpeg)));
        Assert.True(preview.IsSuccess, preview.ErrorMessage); var item = Assert.Single(preview.Data!.ValidQuestions); Assert.Empty(preview.Data.Errors);
        Assert.True(item.HasImage); Assert.Equal(QuestionImageContent.ImageOnlyText, item.QuestionText); Assert.Equal("media-1.0", item.SourceProfile); Assert.NotEmpty(item.CandidateId);
        var json = JsonSerializer.Serialize(preview.Data); Assert.DoesNotContain(_root, json); Assert.DoesNotContain("data:image", json);
        var bytes = await Sut().GetPreviewImageAsync(preview.Data.PreviewToken, 1, item.ImageAssetId!, default); Assert.NotNull(bytes); Assert.True(QuestionImageContent.TryValidate(bytes.Data));
        _tenant.Setup(t => t.UserId).Returns(Guid.NewGuid()); Assert.Null(await Sut().GetPreviewImageAsync(preview.Data.PreviewToken, 1, item.ImageAssetId!, default));
        _tenant.Setup(t => t.UserId).Returns(_actor); _tenant.Setup(t => t.CenterId).Returns(Guid.NewGuid()); Assert.Null(await Sut().GetPreviewImageAsync(preview.Data.PreviewToken, 1, item.ImageAssetId!, default));
        _tenant.Setup(t => t.CenterId).Returns(_center); Assert.Null(await Sut().GetPreviewImageAsync(preview.Data.PreviewToken, 2, item.ImageAssetId!, default));
        var request = new QuestionImportConfirmRequest { PreviewToken = preview.Data.PreviewToken };
        Assert.False((await Sut().ConfirmAsync(request, default)).IsSuccess);
        var confirmed = await Sut().ConfirmV2Async(request, default); Assert.True(confirmed.IsSuccess, confirmed.ErrorMessage);
        var saved = await _db.Questions.SingleAsync(); Assert.True(saved.HasImage); Assert.Equal(QuestionStatus.Draft, saved.Status);
        var savedImage = await _db.QuestionImages.SingleAsync(); Assert.Equal(bytes.Data, savedImage.Data); Assert.Equal(item.ImageSha256, savedImage.Sha256);
        Assert.Empty(PngFiles()); Assert.Null(await Sut().GetPreviewImageAsync(request.PreviewToken, 1, item.ImageAssetId!, default));
        Assert.False((await Sut().ConfirmV2Async(request, default)).IsSuccess);
    }

    [Fact]
    public async Task CorruptImageIsScopedToItsCandidate_ExplicitPartialRetainsOnlyValidAssets()
    {
        var preview = await Preview(Header + "\nQ1,TOP-1,ShortAnswer,Good,2,Lời giải,media/a.png\nQ2,TOP-1,ShortAnswer,Bad,2,Lời giải,media/b.png", ("media/a.png", Image()), ("media/b.png", new byte[] { 1, 2, 3 }));
        Assert.True(preview.IsSuccess, preview.ErrorMessage); Assert.Equal(1, preview.Data!.ValidCount); Assert.Equal(1, preview.Data.InvalidCount); Assert.Single(PngFiles());
        Assert.All(preview.Data.Errors, e => { Assert.NotEmpty(e.CandidateId); Assert.NotNull(e.SourceLocation); });
        Assert.False((await Sut().ConfirmV2Async(new() { PreviewToken = preview.Data.PreviewToken }, default)).IsSuccess);
        var result = await Sut().ConfirmV2Async(new() { PreviewToken = preview.Data.PreviewToken, ImportMode = "SelectedValid", SelectedQuestionKeys = new() { "Q1" } }, default);
        Assert.True(result.IsSuccess); Assert.Equal(1, result.Data!.SkippedCount); Assert.Single(await _db.QuestionImages.ToListAsync()); Assert.Empty(PngFiles());
    }

    [Fact]
    public async Task TamperedStagedImageNeverCommitsAndConsumesSession()
    {
        var preview = await Preview(Header + "\nQ1,TOP-1,ShortAnswer,Good,2,Lời giải,media/a.png", ("media/a.png", Image()));
        var path = Assert.Single(PngFiles()); var bytes = await File.ReadAllBytesAsync(path); bytes[^1] ^= 1; await File.WriteAllBytesAsync(path, bytes);
        var result = await Sut().ConfirmV2Async(new() { PreviewToken = preview.Data!.PreviewToken }, default);
        Assert.False(result.IsSuccess); Assert.Equal("IMPORT_ASSET_CHANGED", result.ErrorCode); Assert.Empty(await _db.Questions.ToListAsync()); Assert.Empty(PngFiles());
    }

    [Fact]
    public async Task DiscardAndTtlRemoveAssets_AndDoNotCrossActor()
    {
        var preview = await Preview(Header + "\nQ1,TOP-1,ShortAnswer,Good,2,Lời giải,media/a.png", ("media/a.png", Image())); var token = preview.Data!.PreviewToken;
        _tenant.Setup(t => t.UserId).Returns(Guid.NewGuid()); Assert.False(await Sut().DiscardSessionAsync(token, 1, default)); Assert.Single(PngFiles());
        _tenant.Setup(t => t.UserId).Returns(_actor); Assert.True(await Sut().DiscardSessionAsync(token, 1, default)); Assert.Empty(PngFiles());
        preview = await Preview(Header + "\nQ1,TOP-1,ShortAnswer,Good,2,Lời giải,media/a.png", ("media/a.png", Image()));
        _clock.Setup(t => t.GetUtcNow()).Returns(_now.AddMinutes(31));
        Assert.Null(await Sut().GetPreviewImageAsync(preview.Data!.PreviewToken, 1, preview.Data.ValidQuestions[0].ImageAssetId!, default)); Assert.Empty(PngFiles());
    }

    [Fact]
    public async Task FatalParseAndCancellationLeaveNoAttemptFilesOrQuota()
    {
        using var bad = new MemoryStream(new byte[] { 1, 2, 3 }); Assert.False((await Sut().PreviewV2Async(bad, "bad.zip", Defaults(), default)).IsSuccess);
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); using var file = Package(Header + "\nQ1,TOP-1,ShortAnswer,Text,2,Giải,");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Sut().PreviewV2Async(file, "q.zip", Defaults(), cancel.Token));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories));
        using var reservation = _store.CreateAttempt(_center, _actor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedDocxImageNormalizesOnceAndScopesStretchPerReference(bool firstImageStretched)
    {
        const string w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        string Cell(string text) => $"<w:tc><w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:tc>";
        string Field(string field, string value) => "<w:tr>" + Cell(field) + Cell(value) + "</w:tr>";
        string Question(string key) => "<w:tbl>" + Field("Trường", "Nội dung") + Field("QuestionKey", key) + Field("TopicCode", "TOP-1") + Field("QuestionType", "ShortAnswer") + Field("CorrectAnswer", "2") + Field("Solution", "Giải") + "<w:tr>" + Cell("Ảnh đề bài") + $"<w:tc><w:p><w:r><w:drawing><wp:inline><wp:extent cx='{(firstImageStretched && key == "Q1" ? 32 : 16) * 9525}' cy='{12 * 9525}'/><a:graphic><a:graphicData><a:blip r:embed='rId1'/></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p></w:tc></w:tr></w:tbl>";
        using var file = new MemoryStream(); using (var zip = new ZipArchive(file, ZipArchiveMode.Create, true))
        {
            using (var xml = new StreamWriter(zip.CreateEntry("word/document.xml").Open(), Encoding.UTF8)) xml.Write($"<w:document xmlns:w='{w}' xmlns:wp='http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing' xmlns:a='http://schemas.openxmlformats.org/drawingml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><w:body><w:tbl>{Field("EduTwin Import", "docx-1.0")}{Field("TemplateVersion", "2.0")}{Field("SubjectId", _subject.ToString())}</w:tbl>{Question("Q1")}{Question("Q2")}</w:body></w:document>");
            using (var rel = new StreamWriter(zip.CreateEntry("word/_rels/document.xml.rels").Open(), Encoding.UTF8)) rel.Write("<Relationships><Relationship Id='rId1' Target='media/shared.png'/></Relationships>");
            using var image = zip.CreateEntry("word/media/shared.png").Open(); image.Write(Image());
        }
        file.Position = 0; var preview = await Sut().PreviewV2Async(file, "shared.docx", Defaults("docx-1.0"), default);
        Assert.True(preview.IsSuccess, preview.ErrorMessage); Assert.Equal(firstImageStretched ? 1 : 2, preview.Data!.ValidCount); Assert.Equal(firstImageStretched ? 1 : 2, PngFiles().Length);
        if (firstImageStretched) Assert.Contains(preview.Data.Errors, e => e.Code == "UNSUPPORTED_IMAGE_TRANSFORM" && e.QuestionKey == "Q1");
        else Assert.Empty(preview.Data.Errors);
        var request = new QuestionImportConfirmRequest { PreviewToken = preview.Data.PreviewToken };
        if (firstImageStretched) { request.ImportMode = "SelectedValid"; request.SelectedQuestionKeys = new() { "Q2" }; }
        Assert.True((await Sut().ConfirmV2Async(request, default)).IsSuccess); Assert.Equal(firstImageStretched ? 1 : 2, await _db.QuestionImages.CountAsync()); Assert.Empty(PngFiles());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeWordMathCannotBecomeTextExactThroughExplicitOrBatchDefaultMode(bool useBatchDefault)
    {
        string Cell(string text) => $"<w:tc><w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:tc>";
        string Field(string label, string value) => "<w:tr>" + Cell(label) + Cell(value) + "</w:tr>";
        var question = Field("Trường", "Nội dung") + Field("QuestionKey", "Q1") + Field("TopicCode", "TOP-1") + Field("QuestionType", "ShortAnswer") + Field("QuestionText", "Đề") + Field("Solution", "Giải");
        if (!useBatchDefault) question += Field("AnswerEvaluationMode", "TextExact");
        question += "<w:tr>" + Cell("CorrectAnswer") + "<w:tc><w:p><m:oMath><m:r><m:t>x</m:t></m:r></m:oMath></w:p></w:tc></w:tr>";
        using var file = new MemoryStream(); using (var zip = new ZipArchive(file, ZipArchiveMode.Create, true))
        using (var output = new StreamWriter(zip.CreateEntry("word/document.xml").Open(), Encoding.UTF8))
            output.Write("<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main' xmlns:m='http://schemas.openxmlformats.org/officeDocument/2006/math'><w:body><w:tbl>" + Field("EduTwin Import", "docx-1.0") + Field("TemplateVersion", "2.0") + Field("SubjectId", _subject.ToString()) + "</w:tbl><w:tbl>" + question + "</w:tbl></w:body></w:document>");
        file.Position = 0; var defaults = Defaults("docx-1.0"); if (useBatchDefault) defaults.ShortAnswerEvaluationMode = "TextExact";
        var preview = await Sut().PreviewV2Async(file, "native-answer.docx", defaults, default);
        Assert.True(preview.IsSuccess, preview.ErrorMessage); Assert.Equal(1, preview.Data!.InvalidCount); Assert.Empty(preview.Data.ValidQuestions);
        Assert.Contains(preview.Data.Errors, e => e.Code == "UNSUPPORTED_NATIVE_MATH_ANSWER" && e.Field == "CorrectAnswer");
        Assert.Empty(await _db.Questions.ToListAsync());
    }

    [Fact]
    public async Task DecoderNormalizesStaticFormatsAndWhitesTransparency()
    {
        var normalizer = new QuestionImportImageNormalizer();
        foreach (var format in new[] { SKEncodedImageFormat.Png, SKEncodedImageFormat.Jpeg, SKEncodedImageFormat.Webp })
        {
            var result = await normalizer.NormalizeAsync(Image(3000, 20, format), default);
            Assert.InRange(result.Width, 1, 2048); Assert.InRange(result.Height, 1, 2048); Assert.InRange(result.Data.Length, 45, 2 * 1024 * 1024);
            Assert.True(QuestionImageContent.TryValidate(result.Data)); Assert.Equal(Convert.ToHexString(SHA256.HashData(result.Data)), result.Sha256);
            if (format == SKEncodedImageFormat.Png) { using var bitmap = SKBitmap.Decode(result.Data); Assert.Equal(SKColors.White, bitmap.GetPixel(bitmap.Width - 1, 0)); }
        }
    }

    [Theory]
    [InlineData(1, 0, 1, 2, 3)]
    [InlineData(2, 1, 0, 3, 2)]
    [InlineData(3, 3, 2, 1, 0)]
    [InlineData(4, 2, 3, 0, 1)]
    [InlineData(5, 0, 2, 1, 3)]
    [InlineData(6, 2, 0, 3, 1)]
    [InlineData(7, 3, 1, 2, 0)]
    [InlineData(8, 1, 3, 0, 2)]
    public async Task AllExifOrientationsPreserveCornerMeaningAndDimensions(ushort orientation, int topLeft, int topRight, int bottomLeft, int bottomRight)
    {
        var colors = new[] { SKColors.Red, SKColors.Blue, SKColors.Green, SKColors.Yellow };
        using var bitmap = new SKBitmap(40, 20);
        using (var canvas = new SKCanvas(bitmap))
        {
            for (var i = 0; i < 4; i++) { using var paint = new SKPaint { Color = colors[i] }; canvas.DrawRect((i % 2) * 20, (i / 2) * 10, 20, 10, paint); }
        }
        using var image = SKImage.FromBitmap(bitmap); using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        var original = jpeg.ToArray(); var exif = new byte[32]; Encoding.ASCII.GetBytes("Exif\0\0II").CopyTo(exif, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(exif.AsSpan(8), 42); BinaryPrimitives.WriteUInt32LittleEndian(exif.AsSpan(10), 8); BinaryPrimitives.WriteUInt16LittleEndian(exif.AsSpan(14), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(exif.AsSpan(16), 0x0112); BinaryPrimitives.WriteUInt16LittleEndian(exif.AsSpan(18), 3); BinaryPrimitives.WriteUInt32LittleEndian(exif.AsSpan(20), 1); BinaryPrimitives.WriteUInt16LittleEndian(exif.AsSpan(24), orientation);
        var source = original[..2].Concat(new byte[] { 255, 225, 0, 34 }).Concat(exif).Concat(original[2..]).ToArray();
        var result = await new QuestionImportImageNormalizer().NormalizeAsync(source, default);
        Assert.Equal(orientation >= 5 ? 20 : 40, result.Width); Assert.Equal(orientation >= 5 ? 40 : 20, result.Height);
        using var output = SKBitmap.Decode(result.Data);
        var corners = new[] { output.GetPixel(output.Width / 4, output.Height / 4), output.GetPixel(output.Width * 3 / 4, output.Height / 4), output.GetPixel(output.Width / 4, output.Height * 3 / 4), output.GetPixel(output.Width * 3 / 4, output.Height * 3 / 4) };
        var expected = new[] { topLeft, topRight, bottomLeft, bottomRight };
        for (var i = 0; i < 4; i++) Assert.True(Math.Abs(corners[i].Red - colors[expected[i]].Red) + Math.Abs(corners[i].Green - colors[expected[i]].Green) + Math.Abs(corners[i].Blue - colors[expected[i]].Blue) < 60, $"Orientation {orientation}, corner {i}: {corners[i]} vs {colors[expected[i]]}");
    }

    [Fact]
    public async Task DecoderRejectsOversizedBytesAndPixelHeaderBeforeFullRasterAllocation()
    {
        var normalizer = new QuestionImportImageNormalizer();
        var error = await Assert.ThrowsAsync<QuestionImportMediaException>(() => normalizer.NormalizeAsync(new byte[8 * 1024 * 1024 + 1], default)); Assert.Equal("IMAGE_SIZE_LIMIT", error.Code);
        var image = Image(); BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(16), 5000); BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(20), 5000);
        var crc = uint.MaxValue; foreach (var b in image.AsSpan(12, 17)) { crc ^= b; for (var i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0); }
        BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(29), ~crc);
        error = await Assert.ThrowsAsync<QuestionImportMediaException>(() => normalizer.NormalizeAsync(image, default)); Assert.Equal("IMAGE_PIXEL_LIMIT", error.Code);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => normalizer.NormalizeAsync(Image(), cancelled.Token));
        Assert.True(QuestionImageContent.TryValidate((await normalizer.NormalizeAsync(Image(), default)).Data));
    }

    private static byte[] PngChunk(string name, byte[] payload)
    {
        var chunk = new byte[12 + payload.Length]; BinaryPrimitives.WriteUInt32BigEndian(chunk, (uint)payload.Length); Encoding.ASCII.GetBytes(name).CopyTo(chunk, 4); payload.CopyTo(chunk, 8);
        var crc = uint.MaxValue; foreach (var b in chunk.AsSpan(4, 4 + payload.Length)) { crc ^= b; for (var i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0); }
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8 + payload.Length), ~crc); return chunk;
    }
    [Fact]
    public async Task ApngAndWebpAnimationMarkersRejectBeforeNativeDecode()
    {
        var control = new byte[8]; BinaryPrimitives.WriteUInt32BigEndian(control, 2);
        var png = Image(); var firstIdat = -1; using var compressed = new MemoryStream();
        for (var offset = 8; offset < png.Length;)
        {
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset)));
            if (png.AsSpan(offset + 4, 4).SequenceEqual("IDAT"u8)) { if (firstIdat < 0) firstIdat = offset; compressed.Write(png.AsSpan(offset + 8, length)); }
            offset += length + 12;
        }
        byte[] Frame(uint sequence)
        {
            var frame = new byte[26]; BinaryPrimitives.WriteUInt32BigEndian(frame, sequence); BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4), 16); BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(8), 12);
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(20), 1); BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(22), 10); return frame;
        }
        var secondFrame = new byte[4 + compressed.Length]; BinaryPrimitives.WriteUInt32BigEndian(secondFrame, 2); compressed.ToArray().CopyTo(secondFrame, 4);
        var apng = png[..firstIdat].Concat(PngChunk("acTL", control)).Concat(PngChunk("fcTL", Frame(0))).Concat(png[firstIdat..^12]).Concat(PngChunk("fcTL", Frame(1))).Concat(PngChunk("fdAT", secondFrame)).Concat(png[^12..]).ToArray();
        var error = await Assert.ThrowsAsync<QuestionImportMediaException>(() => new QuestionImportImageNormalizer().NormalizeAsync(apng, default)); Assert.Equal("ANIMATED_IMAGE", error.Code); Assert.False(QuestionImageContent.TryValidate(apng));
        byte[] WebpChunk(string name, byte[] payload) { var chunk = new byte[8 + payload.Length + (payload.Length & 1)]; Encoding.ASCII.GetBytes(name).CopyTo(chunk, 0); BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(4), (uint)payload.Length); payload.CopyTo(chunk, 8); return chunk; }
        var staticWebp = Image(format: SKEncodedImageFormat.Webp); using var frameData = new MemoryStream();
        for (var offset = 12; offset < staticWebp.Length;)
        {
            var size = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(staticWebp.AsSpan(offset + 4))); var total = 8 + size + (size & 1); var type = Encoding.ASCII.GetString(staticWebp, offset, 4);
            if (type is "VP8 " or "VP8L" or "ALPH") frameData.Write(staticWebp.AsSpan(offset, total)); offset += total;
        }
        var extended = new byte[10]; extended[0] = 18; extended[4] = 15; extended[7] = 11;
        var framePayload = new byte[16 + frameData.Length]; framePayload[6] = 15; framePayload[9] = 11; framePayload[12] = 100; frameData.ToArray().CopyTo(framePayload, 16);
        var chunks = WebpChunk("VP8X", extended).Concat(WebpChunk("ANIM", new byte[6])).Concat(WebpChunk("ANMF", framePayload)).Concat(WebpChunk("ANMF", framePayload)).ToArray();
        var webp = new byte[12 + chunks.Length]; "RIFF"u8.CopyTo(webp); BinaryPrimitives.WriteUInt32LittleEndian(webp.AsSpan(4), (uint)(webp.Length - 8)); "WEBP"u8.CopyTo(webp.AsSpan(8)); chunks.CopyTo(webp, 12);
        using (var data = SKData.CreateCopy(webp)) using (var codec = SKCodec.Create(data)) { Assert.NotNull(codec); Assert.Equal(2, codec.FrameCount); }
        error = await Assert.ThrowsAsync<QuestionImportMediaException>(() => new QuestionImportImageNormalizer().NormalizeAsync(webp, default)); Assert.Equal("ANIMATED_IMAGE", error.Code);
    }

    [Fact]
    public void FailedCleanupRetainsQuotaUntilSweepSucceeds()
    {
        var fail = true; using var store = new QuestionImportMediaStore(Path.Combine(_root, "fail-store"), path => { if (fail) throw new IOException("Injected filesystem failure"); Directory.Delete(path, true); });
        var attempt = store.CreateAttempt(_center, _actor); var path = attempt.WorkDirectory; attempt.Dispose(); Assert.True(Directory.Exists(path));
        Assert.Equal("IMPORT_MEDIA_QUOTA", Assert.Throws<QuestionImportMediaException>(() => store.CreateAttempt(_center, _actor)).Code);
        fail = false; store.RetryCleanup(); Assert.False(Directory.Exists(path)); using var retry = store.CreateAttempt(_center, _actor);
    }

    [Fact]
    public void StagingReservationsEnforceCenterAndGlobalBudgetsAcrossActors()
    {
        var attempts = new List<QuestionImportMediaAttempt>();
        try
        {
            for (var i = 0; i < 5; i++) attempts.Add(_store.CreateAttempt(_center, Guid.NewGuid()));
            Assert.Equal("IMPORT_MEDIA_QUOTA", Assert.Throws<QuestionImportMediaException>(() => _store.CreateAttempt(_center, Guid.NewGuid())).Code);
            for (var i = 0; i < 5; i++) attempts.Add(_store.CreateAttempt(Guid.NewGuid(), Guid.NewGuid()));
            Assert.Equal("IMPORT_MEDIA_QUOTA", Assert.Throws<QuestionImportMediaException>(() => _store.CreateAttempt(Guid.NewGuid(), Guid.NewGuid())).Code);
        }
        finally { foreach (var attempt in attempts) attempt.Dispose(); }
        using var retry = _store.CreateAttempt(_center, _actor);
    }

    [Fact]
    public void CleanupWaitsForAssetLeaseAndStartupPreservesLiveRuns()
    {
        var attempt = _store.CreateAttempt(_center, _actor); using var lease = attempt.AcquireLease(); Assert.NotNull(lease); attempt.Dispose(); Assert.True(Directory.Exists(attempt.WorkDirectory));
        using var second = new QuestionImportMediaStore(_root); Assert.True(Directory.Exists(attempt.WorkDirectory)); lease.Dispose(); Assert.False(Directory.Exists(attempt.WorkDirectory));
    }

    [Fact]
    public void StartupRemovesCrashedRunWithoutFollowingLinks()
    {
        var stale = Path.Combine(_root, "run-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stale); File.WriteAllText(Path.Combine(stale, ".lease"), ""); File.WriteAllText(Path.Combine(stale, "old.bin"), "private");
        using var restarted = new QuestionImportMediaStore(_root); Assert.False(Directory.Exists(stale));
    }

    [Fact]
    public async Task PrivateUnixModesAndSymlinkAssetGuard()
    {
        if (OperatingSystem.IsWindows()) return;
        var attempt = _store.CreateAttempt(_center, _actor); var image = await new QuestionImportImageNormalizer().NormalizeAsync(Image(), default); await attempt.AddNormalizedAsync("media/a.png", image, default);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(attempt.WorkDirectory));
        var path = Directory.GetFiles(attempt.WorkDirectory, "*.png").Single(); Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        var outside = Path.Combine(_root, "outside.png"); await File.WriteAllBytesAsync(outside, image.Data); File.Delete(path); File.CreateSymbolicLink(path, outside);
        var asset = new StagedQuestionImportAsset("media/a.png", "a", path, image.Sha256, image.Data.Length, image.Width, image.Height);
        await Assert.ThrowsAsync<IOException>(() => attempt.ReadVerifiedAsync(asset, default)); attempt.Dispose(); Assert.True(File.Exists(outside));
    }
    public void Dispose() { _store.Dispose(); _db.Dispose(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
