using System.Data.Common;
using System.Diagnostics;
using System.Text;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.CurriculumAndQuestions.Import;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.Data.MySqlClient;
using Xunit;
using Xunit.Abstractions;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

[Collection("MySqlDatabase")]
public sealed class QuestionImportMySqlTests(ITestOutputHelper output)
{
    private const string ConnectionVariable = "EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING";

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
