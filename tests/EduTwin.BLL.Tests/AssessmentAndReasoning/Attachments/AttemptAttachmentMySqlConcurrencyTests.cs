using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.API.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using MySql.Data.MySqlClient;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Attachments;

[Collection("MySqlDatabase")]
public sealed class AttemptAttachmentMySqlConcurrencyTests
{
    private const string AdminConnectionVariable = "EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING";
    private static readonly DateTime UtcNow = new(2026, 8, 15, 9, 0, 0, DateTimeKind.Utc);

    [MySqlIntegrationFact]
    public async Task SubmitAttempt_ConcurrentUploadNonceRace_WinnerCommitsAndLoserPreservesPhysicalFile()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var nonce = Guid.NewGuid().ToString("N");
        var storageRoot = Path.Combine(Path.GetTempPath(), "edutwin-concurrency-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storageRoot);

        try
        {
            await SeedBaseTenantAsync(database.ConnectionString, centerId, studentId);

            var options = Options.Create(new AttachmentStorageOptions
            {
                RootPath = storageRoot,
                GracePeriodHours = 24,
            });
            var envMock = new Mock<IWebHostEnvironment>();
            envMock.SetupGet(e => e.ContentRootPath).Returns(storageRoot);

            var storage = new FileSystemAttemptAttachmentStorage(options, envMock.Object);

            // Pre-create the temporary attachment file so promotion succeeds
            var tempDir = Path.Combine(storageRoot, "tenants", centerId.ToString("D"), "attempt-attachments-temp");
            Directory.CreateDirectory(tempDir);
            var tempFilePath = Path.Combine(tempDir, $"{nonce}.png");
            var dummyPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
            await File.WriteAllBytesAsync(tempFilePath, dummyPng);
            var sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(dummyPng)).ToLowerInvariant();

            var tokenServiceMock = new Mock<IAttemptAttachmentTokenService>();
            var payload = new AttachmentUploadTokenPayload(
                centerId,
                studentId,
                nonce,
                sha256,
                "drawing.png",
                dummyPng.Length,
                UtcNow.AddHours(24));

            tokenServiceMock.Setup(s => s.TryRead(It.Is<string>(t => t == "valid-token"), out payload)).Returns(true);

            // Create two competing submissions with DIFFERENT clientSubmissionId (so they don't idempotently deduplicate),
            // but the SAME drawingUploadToken / nonce.
            var submissionA = CreateSubmission(centerId, studentId, "valid-token", Guid.NewGuid());
            var submissionB = CreateSubmission(centerId, studentId, "valid-token", Guid.NewGuid());

            var validatorMock = new Mock<IAttemptSubmissionValidator>();
            validatorMock.Setup(v => v.ValidateAsync(It.Is<SubmitAttemptRequest>(r => r.ClientSubmissionId == submissionA.ClientSubmissionId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(AttemptSubmissionValidationResult.Success(submissionA));
            validatorMock.Setup(v => v.ValidateAsync(It.Is<SubmitAttemptRequest>(r => r.ClientSubmissionId == submissionB.ClientSubmissionId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(AttemptSubmissionValidationResult.Success(submissionB));

            var timeProvider = new FixedTimeProvider(UtcNow);

            var tenantA = new TenantContext();
            using var scopeA = tenantA.BeginScope(centerId);
            await using var contextA = CreateContext(database.ConnectionString, tenantA);

            var tenantB = new TenantContext();
            using var scopeB = tenantB.BeginScope(centerId);
            await using var contextB = CreateContext(database.ConnectionString, tenantB);

            var sutA = new SubmitAttemptUseCase(contextA, validatorMock.Object, timeProvider, tokenServiceMock.Object, storage);
            var sutB = new SubmitAttemptUseCase(contextB, validatorMock.Object, timeProvider, tokenServiceMock.Object, storage);

            var requestA = new SubmitAttemptRequest
            {
                ClientSubmissionId = submissionA.ClientSubmissionId,
                QuestionId = "101",
                FinalAnswer = "A",
                DrawingUploadToken = "valid-token"
            };
            var requestB = new SubmitAttemptRequest
            {
                ClientSubmissionId = submissionB.ClientSubmissionId,
                QuestionId = "101",
                FinalAnswer = "B",
                DrawingUploadToken = "valid-token"
            };

            // Run concurrently
            var taskA = sutA.ExecuteAsync(requestA, "trace-a");
            var taskB = sutB.ExecuteAsync(requestB, "trace-b");

            var results = await Task.WhenAll(taskA, taskB);

            // Exactly one must succeed, and one must fail with UploadTokenAlreadyUsed
            var winner = results.SingleOrDefault(r => r.IsSuccess);
            var loser = results.SingleOrDefault(r => !r.IsSuccess);

            Assert.NotNull(winner);
            Assert.NotNull(loser);
            Assert.Equal(ErrorCodes.UploadTokenAlreadyUsed, loser.ErrorCode);

            // Crucial verification: The winner's committed physical file on disk must NOT have been deleted by the loser!
            var permanentPath = Path.Combine(storageRoot, "tenants", centerId.ToString("D"), "attempt-attachments", $"{nonce}.png");
            Assert.True(File.Exists(permanentPath), "Winner's permanent attachment file must remain on disk despite loser's rollback.");

            // Exactly 1 attachment row in MySQL
            var verifyTenant = new TenantContext();
            using var verifyScope = verifyTenant.BeginScope(centerId);
            await using var verifyContext = CreateContext(database.ConnectionString, verifyTenant);

            var attachmentRows = await verifyContext.AttemptAttachments.AsNoTracking().ToListAsync();
            Assert.Single(attachmentRows);
            Assert.Equal(nonce, attachmentRows[0].UploadNonce);
        }
        finally
        {
            try { Directory.Delete(storageRoot, true); } catch { }
        }
    }

    private static ValidatedAttemptSubmission CreateSubmission(Guid centerId, Guid studentId, string uploadToken, Guid clientSubId) =>
        new()
        {
            CenterId = centerId,
            StudentId = studentId,
            ClientSubmissionId = clientSubId,
            QuestionId = 101,
            AssignmentId = null,
            FinalAnswer = "A",
            ReasoningText = "My reasoning",
            DrawingUploadToken = uploadToken,
            TimeSpentSeconds = 30,
            Confidence = 90,
            AnswerChanges = 0,
            Skipped = false,
            ReasoningLanguage = "vi",
            IsCorrect = true,
            AwardedScore = 1m
        };

    private static async Task SeedBaseTenantAsync(string connectionString, Guid centerId, Guid studentId)
    {
        var tenant = new TenantContext();
        using var scope = tenant.BeginScope(centerId);
        await using var context = CreateContext(connectionString, tenant);

        await context.Database.OpenConnectionAsync();
        try
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");

            context.Centers.Add(new EduTwin.DAL.Organization.Center
            {
                CenterId = centerId,
                CenterCode = $"C-{centerId:N}"[..10],
                CenterName = "Test Center",
                Status = EduTwin.Contracts.Organization.CenterStatus.Active,
                Timezone = "UTC",
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            context.Users.Add(new EduTwin.DAL.IdentityAndTenancy.User
            {
                UserId = studentId,
                CenterId = centerId,
                Username = $"student-{studentId:N}"[..20],
                DisplayName = "Student Test",
                PasswordHash = "hash",
                RoleName = EduTwin.Contracts.IdentityAndTenancy.UserRole.Student,
                Status = EduTwin.Contracts.IdentityAndTenancy.UserStatus.Active,
                AuthVersion = 1,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            context.Students.Add(new EduTwin.DAL.Organization.Student
            {
                CenterId = centerId,
                StudentId = studentId,
                FullName = "Student Test",
                GradeLevel = 10,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            context.Questions.Add(new EduTwin.DAL.CurriculumAndQuestions.Question
            {
                QuestionId = 101,
                CenterId = centerId,
                SubjectId = centerId,
                PrimaryTopicNodeId = 1,
                CreatedByTeacherId = studentId,
                QuestionText = "Question 101",
                QuestionType = EduTwin.Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice,
                Difficulty = 1,
                MaxScore = 1,
                EstimatedTimeSeconds = 60,
                ReasoningRequired = false,
                Status = EduTwin.Contracts.CurriculumAndQuestions.QuestionStatus.Active,
                CorrectAnswer = "A",
                Solution = "Solution 101",
                ExpectedReasoning = "Expected reasoning 101",
                LanguageCode = "en",
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            await context.SaveChangesAsync();
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
            await context.Database.CloseConnectionAsync();
        }
    }

    private static EduTwinDbContext CreateContext(string connectionString, ITenantIdAccessor tenantIdAccessor)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseMySQL(connectionString)
            .Options;
        return new EduTwinDbContext(options, tenantIdAccessor);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class MySqlIntegrationFactAttribute : FactAttribute
    {
        public MySqlIntegrationFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AdminConnectionVariable)))
            {
                Skip = $"Set {AdminConnectionVariable} to run MySQL integration tests.";
            }
        }
    }

    private sealed class MySqlTestDatabase : IAsyncDisposable
    {
        private readonly string _adminConnectionString;
        private readonly string _databaseName;

        private MySqlTestDatabase(string adminConnectionString, string databaseName, string connectionString)
        {
            _adminConnectionString = adminConnectionString;
            _databaseName = databaseName;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static async Task<MySqlTestDatabase> CreateAsync()
        {
            var configuredConnection = Environment.GetEnvironmentVariable(AdminConnectionVariable)
                ?? throw new InvalidOperationException($"{AdminConnectionVariable} is required.");
            var adminBuilder = new MySqlConnectionStringBuilder(configuredConnection)
            {
                Database = string.Empty,
                Pooling = false
            };
            var databaseName = $"edutwin_attach_{Guid.NewGuid():N}";
            await using (var connection = new MySqlConnection(adminBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4;";
                await command.ExecuteNonQueryAsync();
            }

            var databaseBuilder = new MySqlConnectionStringBuilder(adminBuilder.ConnectionString)
            {
                Database = databaseName,
                Pooling = false
            };
            var database = new MySqlTestDatabase(adminBuilder.ConnectionString, databaseName, databaseBuilder.ConnectionString);
            try
            {
                var tenant = new TenantContext();
                await using var context = CreateContext(database.ConnectionString, tenant);
                await context.Database.MigrateAsync();
                return database;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = new MySqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS `{_databaseName}`;";
            await command.ExecuteNonQueryAsync();
        }
    }
}
