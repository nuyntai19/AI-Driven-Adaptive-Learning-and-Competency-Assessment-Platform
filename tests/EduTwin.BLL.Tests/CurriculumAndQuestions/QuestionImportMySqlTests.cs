using System.Data.Common;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.CurriculumAndQuestions.Import;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.Data.MySqlClient;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

[Collection("MySqlDatabase")]
public sealed class QuestionImportMySqlTests(ITestOutputHelper output)
{
    private const string ConnectionVariable = "EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING";

    [MySqlImportFact]
    public async Task V2_RealImageWrite_RollsBack_Retries_And_Benchmarks_500Questions_With20Images()
    {
        var admin = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable)) { Database = string.Empty, Pooling = false };
        var databaseName = "edutwin_import_" + Guid.NewGuid().ToString("N");
        await ExecuteAdminAsync(admin.ConnectionString, $"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4;");
        var connection = new MySqlConnectionStringBuilder(admin.ConnectionString) { Database = databaseName }.ConnectionString;
        var privateRoot = Path.Combine(Path.GetTempPath(), "edutwin-import-mysql-" + Guid.NewGuid().ToString("N"));
        try
        {
            var tenant = new TenantContext();
            var failure = new ImageSavedFailure();
            var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseMySQL(connection).AddInterceptors(failure).Options;
            var seed = new EduTwinSeedFactory(true).CreateData();
            await using (var setup = new EduTwinDbContext(options, tenant))
            {
                await setup.Database.GetService<IMigrator>().MigrateAsync();
                setup.Centers.Add(seed.Center);
                foreach (var user in seed.Users) user.PasswordHash = "disposable-test-hash";
                setup.Users.AddRange(seed.Users); setup.Teachers.AddRange(seed.Teachers); setup.Students.AddRange(seed.Students); setup.Subjects.AddRange(seed.Subjects); setup.KnowledgeNodes.AddRange(seed.Topics);
                await setup.SaveChangesAsync();
            }
            var subject = seed.Subjects[0];
            var topic = seed.Topics.First(n => n.SubjectId == subject.SubjectId && n.NodeType == NodeType.Topic);
            tenant.Initialize(seed.Center.CenterId, seed.Teachers[0].TeacherId, nameof(UserRole.Teacher), 1);
            await using var context = new EduTwinDbContext(options, tenant);
            using var media = new QuestionImportMediaStore(privateRoot);
            var sut = new QuestionImportUseCase(context, tenant, mediaStore: media);
            var request = new QuestionImportV2PreviewRequest { SubjectId = subject.SubjectId, GradeLevel = 12, SourceProfile = "media-1.0" };
            var jpeg = NoiseJpeg();
            using (var small = ImagePackage(topic.NodeCode, jpeg, 1, 1))
            {
                var preview = await sut.PreviewV2Async(small, "images.zip", request, default);
                Assert.True(preview.IsSuccess, preview.ErrorMessage); Assert.Empty(preview.Data!.Errors);
                var candidate = Assert.Single(preview.Data.ValidQuestions);
                Assert.Equal(QuestionImageContent.ImageOnlyText, candidate.QuestionText);
                var png = await sut.GetPreviewImageAsync(preview.Data.PreviewToken, 1, candidate.ImageAssetId!, default);
                Assert.NotNull(png); Assert.True(QuestionImageContent.TryDecode("data:image/png;base64," + Convert.ToBase64String(png.Data), out _));
                failure.Enabled = true;
                var failed = await sut.ConfirmV2Async(new() { PreviewToken = preview.Data.PreviewToken }, default);
                Assert.False(failed.IsSuccess); Assert.True(failure.DidThrow);
                failure.Enabled = false;
                await using (var verify = new EduTwinDbContext(options, tenant))
                { Assert.Equal(0, await verify.Questions.CountAsync()); Assert.Equal(0, await verify.QuestionImages.CountAsync()); Assert.Equal(0, await verify.QuestionOptions.CountAsync()); }
                Assert.NotNull(await sut.GetPreviewImageAsync(preview.Data.PreviewToken, 1, candidate.ImageAssetId!, default));
                var retry = await sut.ConfirmV2Async(new() { PreviewToken = preview.Data.PreviewToken }, default);
                Assert.True(retry.IsSuccess, retry.ErrorMessage);
                var id = ulong.Parse(Assert.Single(retry.Data!.QuestionIds));
                context.ChangeTracker.Clear();
                var read = await new GetQuestionUseCase(context, tenant).ExecuteAsync(id.ToString());
                Assert.True(read.Data!.HasImage); Assert.Equal(QuestionStatus.Draft, (await context.Questions.SingleAsync()).Status);
                Assert.Equal(png.Data, await new GetQuestionImageUseCase(context, tenant).ExecuteAsync(id, default));
                Assert.Null(await sut.GetPreviewImageAsync(preview.Data.PreviewToken, 1, candidate.ImageAssetId!, default));
            }
            using var bulk = ImagePackage(topic.NodeCode, jpeg, 500, 20);
            var allocated = GC.GetTotalAllocatedBytes(); var clock = Stopwatch.StartNew();
            var bulkPreview = await sut.PreviewV2Async(bulk, "500-images.zip", request, default);
            var previewMs = clock.ElapsedMilliseconds;
            Assert.True(bulkPreview.IsSuccess, bulkPreview.ErrorMessage); Assert.Empty(bulkPreview.Data!.Errors);
            Assert.Equal(500, bulkPreview.Data.ValidCount); Assert.Equal(20, bulkPreview.Data.ValidQuestions.Count(q => q.HasImage));
            var blobBytes = bulkPreview.Data.ValidQuestions.Sum(q => q.ImageBytes ?? 0);
            Assert.InRange(blobBytes, 20 * 1024 * 1024, 40 * 1024 * 1024);
            var bulkConfirm = await sut.ConfirmV2Async(new() { PreviewToken = bulkPreview.Data.PreviewToken }, default);
            Assert.True(bulkConfirm.IsSuccess, bulkConfirm.ErrorMessage); Assert.Equal(500, bulkConfirm.Data!.ImportedCount);
            output.WriteLine($"V2 500 questions/20 JPEG images: upload={bulk.Length}bytes; normalized={blobBytes}bytes; preview={previewMs}ms; preview+commit={clock.ElapsedMilliseconds}ms; allocated={GC.GetTotalAllocatedBytes() - allocated}bytes; processPeakWorkingSet={Process.GetCurrentProcess().PeakWorkingSet64}bytes.");
            await using var final = new EduTwinDbContext(options, tenant);
            Assert.Equal(501, await final.Questions.CountAsync()); Assert.Equal(21, await final.QuestionImages.CountAsync());
            Assert.Equal(21, await final.Questions.CountAsync(q => q.HasImage));
            Assert.Equal(501, await final.Questions.CountAsync(q => q.Status == QuestionStatus.Draft));
            Assert.Empty(Directory.EnumerateFiles(privateRoot, "*.png", SearchOption.AllDirectories));
            // Continue the actual stored image through activation, student ACL and the AI context.
            context.ChangeTracker.Clear();
            var imported = await context.Questions.OrderBy(q => q.QuestionId).FirstAsync();
            var originalImage = (await context.QuestionImages.SingleAsync(i => i.QuestionId == imported.QuestionId)).Data.ToArray();
            UpdateQuestionRequest EditImage(string? dataUrl, bool remove = false) => new()
            {
                PrimaryTopicNodeId = imported.PrimaryTopicNodeId.ToString(), QuestionType = imported.QuestionType.ToString(), Difficulty = imported.Difficulty,
                GradeLevel = imported.GradeLevel, QuestionText = "Đề đã rà soát $$x+1$$.", CorrectAnswer = imported.CorrectAnswer, Solution = imported.Solution,
                MaxScore = imported.MaxScore, EstimatedTimeSeconds = imported.EstimatedTimeSeconds, ReasoningRequired = imported.ReasoningRequired,
                LanguageCode = imported.LanguageCode, AnswerEvaluationMode = imported.AnswerEvaluationMode.ToString(), GradingCriteria = imported.GradingCriteria,
                RowVersion = imported.RowVersion.ToString(), ImageDataUrl = dataUrl, RemoveImage = remove,
            };
            var editor = new UpdateQuestionUseCase(context, tenant, TimeProvider.System);
            var replacement = await editor.ExecuteAsync(imported.QuestionId.ToString(), EditImage(QuestionImageFixture.DataUrl)); Assert.True(replacement.IsSuccess);
            Assert.Equal(QuestionImageFixture.Bytes, await new GetQuestionImageUseCase(context, tenant).ExecuteAsync(imported.QuestionId, default));
            var removal = await editor.ExecuteAsync(imported.QuestionId.ToString(), EditImage(null, true)); Assert.True(removal.IsSuccess);
            Assert.False(imported.HasImage); Assert.Null(await new GetQuestionImageUseCase(context, tenant).ExecuteAsync(imported.QuestionId, default));
            var restored = await editor.ExecuteAsync(imported.QuestionId.ToString(), EditImage("data:image/png;base64," + Convert.ToBase64String(originalImage))); Assert.True(restored.IsSuccess);
            Assert.Equal("Đề đã rà soát $$x+1$$.", (await new GetQuestionUseCase(context, tenant).ExecuteAsync(imported.QuestionId.ToString())).Data!.QuestionText);
            var activation = await new ActivateQuestionUseCase(context, tenant, TimeProvider.System).ExecuteAsync(imported.QuestionId.ToString(), new() { RowVersion = imported.RowVersion.ToString() });
            Assert.True(activation.IsSuccess);
            var studentTenant = new TenantContext(); studentTenant.Initialize(seed.Center.CenterId, seed.Students[0].StudentId, nameof(UserRole.Student), 1);
            await using var studentDb = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>().UseMySQL(connection).Options, studentTenant);
            Assert.Null(await new GetQuestionImageUseCase(studentDb, studentTenant).ExecuteAsync(imported.QuestionId, default));
            var attempt = new Attempt { CenterId = seed.Center.CenterId, StudentId = seed.Students[0].StudentId, QuestionId = imported.QuestionId,
                FinalAnswer = "2", ReasoningText = "Tính tổng.", ReasoningLanguage = "vi", Status = AttemptStatus.PendingAnalysis,
                ClientSubmissionId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Confidence = 50 };
            context.Attempts.Add(attempt); await context.SaveChangesAsync();
            var studentImage = await new GetQuestionImageUseCase(studentDb, studentTenant).ExecuteAsync(imported.QuestionId, default);
            Assert.NotNull(studentImage);
            var stored = await context.QuestionImages.SingleAsync(i => i.QuestionId == imported.QuestionId);
            Assert.Equal(stored.Data, studentImage);
            var ai = new AIAnalysisRequestFactory().Create(attempt, imported, [topic], questionImageParts: [new(studentImage, "image/png")]);
            Assert.Equal(stored.Data, Assert.Single(ai.Question.ImageParts).Data); Assert.Empty(ai.StudentSubmission.ImageParts);
        }
        finally
        {
            MySqlConnection.ClearAllPools(); await ExecuteAdminAsync(admin.ConnectionString, $"DROP DATABASE IF EXISTS `{databaseName}`;");
            // This absolute path is generated above; never accept a path from test input.
            if (Directory.Exists(privateRoot)) Directory.Delete(privateRoot, true);
        }
    }

    private static byte[] NoiseJpeg()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(1400, 1400, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var pixels = new byte[1400 * 1400 * 4]; new Random(20261015).NextBytes(pixels);
        for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        using var image = SKImage.FromBitmap(bitmap); using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 75);
        return encoded.ToArray();
    }
    private static MemoryStream ImagePackage(string topic, byte[] image, int questions, int images)
    {
        var result = new MemoryStream();
        using (var zip = new ZipArchive(result, ZipArchiveMode.Create, true))
        {
            var csv = zip.CreateEntry("questions.csv");
            using (var writer = new StreamWriter(csv.Open(), new UTF8Encoding(false)))
            {
                writer.WriteLine("QuestionKey,TopicCode,QuestionType,QuestionText,CorrectAnswer,Solution,GradeLevel,AnswerEvaluationMode,ImageRef");
                for (var i = 1; i <= questions; i++) writer.WriteLine($"IMAGE{i:D3},{topic},ShortAnswer,{(i <= images ? "" : "Tính $$1+1$$.")},2,Vì $$1+1=2$$.,12,NumericRational,{(i <= images ? $"media/{i}.jpg" : "")}");
            }
            for (var i = 1; i <= images; i++) { using var entry = zip.CreateEntry($"media/{i}.jpg", CompressionLevel.NoCompression).Open(); entry.Write(image); }
        }
        result.Position = 0; return result;
    }
    private sealed class ImageSavedFailure : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public bool DidThrow { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries().Any(e => e.Metadata.ClrType.Name == "QuestionImage"))
            { DidThrow = true; throw new InvalidOperationException("Injected failure after question image INSERT reached MySQL."); }
            return ValueTask.FromResult(result);
        }
    }

    [MySqlImportFact]
    public async Task Confirm_PersistsTopicsAndFeedback_RollsBackFailedBatch_AndImports500Drafts()
    {
        var admin = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable))
            { Database = string.Empty, Pooling = false };
        var databaseName = "edutwin_import_" + Guid.NewGuid().ToString("N");
        await ExecuteAdminAsync(admin.ConnectionString, $"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4;");
        var connection = new MySqlConnectionStringBuilder(admin.ConnectionString) { Database = databaseName }.ConnectionString;
        try
        {
            var tenant = new TenantContext();
            var interceptor = new OptionsInsertFailure();
            var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseMySQL(connection).AddInterceptors(interceptor).Options;
            var seed = new EduTwinSeedFactory(true).CreateData();
            await using (var setup = new EduTwinDbContext(options, tenant))
            {
                await setup.Database.GetService<IMigrator>().MigrateAsync();
                setup.Centers.Add(seed.Center);
                foreach (var user in seed.Users) user.PasswordHash = "disposable-test-hash";
                setup.Users.AddRange(seed.Users);
                setup.Teachers.AddRange(seed.Teachers);
                setup.Subjects.AddRange(seed.Subjects);
                setup.KnowledgeNodes.AddRange(seed.Topics);
                await setup.SaveChangesAsync();
            }
            var teacher = seed.Teachers[0];
            var subject = seed.Subjects[0];
            var topics = seed.Topics.Where(n => n.SubjectId == subject.SubjectId && n.NodeType == NodeType.Topic).Take(2).ToArray();
            Assert.Equal(2, topics.Length);
            tenant.Initialize(seed.Center.CenterId, teacher.TeacherId, nameof(UserRole.Teacher), 1);
            await using var context = new EduTwinDbContext(options, tenant);
            var sut = new QuestionImportUseCase(context, tenant);
            var request = new QuestionImportPreviewRequest { SubjectId = subject.SubjectId, GradeLevel = 12 };
            var csv = Csv(Mcq("MC1", topics[0].NodeCode), Short("SA1", topics[1].NodeCode));
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            var preview = await sut.PreviewAsync(stream, "questions.csv", request, default);
            Assert.True(preview.IsSuccess, preview.ErrorMessage);
            Assert.Empty(preview.Data!.Errors);
            var confirmed = await sut.ConfirmAsync(new QuestionImportConfirmRequest { PreviewToken = preview.Data.PreviewToken }, default);
            Assert.True(confirmed.IsSuccess, confirmed.ErrorMessage);
            Assert.Equal(2, confirmed.Data!.ImportedCount);
            context.ChangeTracker.Clear();
            var questions = await context.Questions.OrderBy(q => q.QuestionId).ToArrayAsync();
            Assert.Equal(2, questions.Length);
            Assert.All(questions, q => Assert.Equal(QuestionStatus.Draft, q.Status));
            Assert.Equal(topics[0].NodeId, questions[0].PrimaryTopicNodeId);
            Assert.Equal(topics[1].NodeId, questions[1].PrimaryTopicNodeId);
            Assert.Equal("en", questions[0].LanguageCode);
            Assert.Equal(MaterialVisibility.Shared, questions[0].Visibility);
            var read = await new GetQuestionUseCase(context, tenant).ExecuteAsync(questions[0].QuestionId.ToString());
            Assert.Equal("Nhầm $x^2$ với $2x$.", read.Data!.Options[1].Misconception);
            Assert.Equal("Ghi chú riêng, không phải lời giải.", read.Data.GradingCriteria!.ScoringNotes);
            Assert.Equal("Dòng một.\nDòng hai $x=1$.", questions[1].Solution);

            // Fail after at least one question INSERT has reached MySQL, not during preview validation.
            using var failedStream = new MemoryStream(Encoding.UTF8.GetBytes(Csv(Short("RETRY1", topics[0].NodeCode), Mcq("RETRY2", topics[1].NodeCode))));
            var failedPreview = await sut.PreviewAsync(failedStream, "retry.csv", request, default);
            Assert.True(failedPreview.IsSuccess);
            Assert.Empty(failedPreview.Data!.Errors);
            interceptor.Enabled = true;
            var failed = await sut.ConfirmAsync(new QuestionImportConfirmRequest { PreviewToken = failedPreview.Data.PreviewToken }, default);
            Assert.False(failed.IsSuccess);
            Assert.True(interceptor.DidThrow);
            interceptor.Enabled = false;
            await using (var verify = new EduTwinDbContext(options, tenant))
            {
                Assert.Equal(2, await verify.Questions.CountAsync());
                Assert.Equal(4, await verify.QuestionOptions.CountAsync());
            }
            var retry = await sut.ConfirmAsync(new QuestionImportConfirmRequest { PreviewToken = failedPreview.Data.PreviewToken }, default);
            Assert.True(retry.IsSuccess, retry.ErrorMessage);
            Assert.Equal(2, retry.Data!.ImportedCount);

            var rows = Enumerable.Range(1, 500).Select(i => Short($"BENCH{i:D3}", topics[i % 2].NodeCode)).ToArray();
            using var bulkStream = new MemoryStream(Encoding.UTF8.GetBytes(Csv(rows)));
            var allocatedStart = GC.GetTotalAllocatedBytes();
            var timer = Stopwatch.StartNew();
            var bulkPreview = await sut.PreviewAsync(bulkStream, "500.csv", request, default);
            var previewElapsed = timer.ElapsedMilliseconds;
            Assert.True(bulkPreview.IsSuccess, bulkPreview.ErrorMessage);
            Assert.Empty(bulkPreview.Data!.Errors);
            var bulkConfirm = await sut.ConfirmAsync(new QuestionImportConfirmRequest { PreviewToken = bulkPreview.Data.PreviewToken }, default);
            Assert.True(bulkConfirm.IsSuccess, bulkConfirm.ErrorMessage);
            Assert.Equal(500, bulkConfirm.Data!.ImportedCount);
            output.WriteLine($"500-question CSV benchmark: preview={previewElapsed}ms; preview+commit={timer.ElapsedMilliseconds}ms; allocated={GC.GetTotalAllocatedBytes() - allocatedStart}bytes.");
            await using var final = new EduTwinDbContext(options, tenant);
            Assert.Equal(504, await final.Questions.CountAsync());
            Assert.Equal(8, await final.QuestionOptions.CountAsync());
            Assert.Equal(504, await final.Questions.CountAsync(q => q.Status == QuestionStatus.Draft && q.GradeLevel == 12));
        }
        finally
        {
            // Name generated above is the only database this test can remove.
            MySqlConnection.ClearAllPools();
            await ExecuteAdminAsync(admin.ConnectionString, $"DROP DATABASE IF EXISTS `{databaseName}`;");
        }
    }

    private static string[] Mcq(string key, string topic) =>
        [key, topic, "MultipleChoice", "Cho $x=1$. Giá trị $x^2$?", "1", "", "2", "Nhầm $x^2$ với $2x$.", "3", "", "4", "", "A", "Vì $1^2=1$.", "12", "TextExact", "en", "Shared", "Ghi chú riêng, không phải lời giải."];
    private static string[] Short(string key, string topic) =>
        [key, topic, "ShortAnswer", "Tính $1/2+1/2$.", "", "", "", "", "", "", "", "", "1", "Dòng một.\nDòng hai $x=1$.", "12", "NumericRational", "vi", "Private", ""];
    private static string Csv(params string[][] rows) =>
        "QuestionKey,TopicCode,QuestionType,QuestionText,OptionA,MisconceptionA,OptionB,MisconceptionB,OptionC,MisconceptionC,OptionD,MisconceptionD,CorrectAnswer,Solution,GradeLevel,AnswerEvaluationMode,LanguageCode,Visibility,ScoringNotes\n" +
        string.Join("\n", rows.Select(row => string.Join(",", row.Select(value => "\"" + value.Replace("\"", "\"\"") + "\"")))) + "\n";
    private static async Task ExecuteAdminAsync(string connection, string sql)
    {
        await using var client = new MySqlConnection(connection);
        await client.OpenAsync();
        await using var command = client.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
    private sealed class OptionsInsertFailure : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public bool DidThrow { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.Contains("INSERT INTO `question_options`", StringComparison.OrdinalIgnoreCase))
            {
                DidThrow = true;
                throw new InvalidOperationException("Injected option persistence failure in disposable import test.");
            }
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
    private sealed class MySqlImportFactAttribute : FactAttribute
    {
        public MySqlImportFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
                Skip = $"Set {ConnectionVariable} to run isolated MySQL import checks.";
        }
    }
}
