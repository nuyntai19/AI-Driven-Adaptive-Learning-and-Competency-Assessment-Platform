using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySql.Data.MySqlClient;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Recommendations;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class RecommendationMySqlConcurrencyTests
{
    private const string AdminConnectionVariable = "EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING";
    private static readonly DateTime UtcNow = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);

    [MySqlIntegrationFact]
    public async Task Migration_MaintenanceReview_InsertsSuccessfullyIntoMySQL()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerId, studentId, subjectId);

        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(database.ConnectionString, tenant);

        var learningPath = new LearningPath
        {
            LearningPathId = Guid.NewGuid(),
            CenterId = centerId,
            StudentId = studentId,
            SubjectId = subjectId,
            Strategy = LearningPathStrategy.MaintenanceReview,
            Version = 1,
            Status = LearningPathStatus.Active,
            GeneratedAt = UtcNow,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow
        };

        context.LearningPaths.Add(learningPath);
        await context.SaveChangesAsync();

        var loaded = await context.LearningPaths
            .AsNoTracking()
            .SingleOrDefaultAsync(lp => lp.LearningPathId == learningPath.LearningPathId);

        Assert.NotNull(loaded);
        Assert.Equal(LearningPathStrategy.MaintenanceReview, loaded.Strategy);
    }

    [MySqlIntegrationFact]
    public async Task ConcurrentCompletions_SerializedByStudentRowLock_GuaranteesSingleActiveAndObservesLatestState()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerId, studentId, subjectId);

        var barrier = new Barrier(2);

        async Task RunCompletionPipelineAsync(int completionIndex)
        {
            var tenant = new TenantContext();
            using var scope = tenant.BeginScope(centerId);
            await using var context = CreateContext(database.ConnectionString, tenant);

            var candidateBuilder = new OpportunityCandidateBuilder(context);
            var linearSelector = new LinearFallbackSelector();
            var questionSelector = new AdaptiveQuestionSelector(context);
            var engine = new RecommendationEngine(context, candidateBuilder, linearSelector, questionSelector);

            await using var transaction = await context.Database.BeginTransactionAsync();

            // 1. Acquire early student row lock
            await StudentLockHelper.AcquireStudentLockAsync(context, centerId, studentId);

            // Synchronize workers to demonstrate serialization
            barrier.SignalAndWait(5000);

            // 2. Update KnowledgeTwin state
            var twin = await context.KnowledgeTwins
                .SingleOrDefaultAsync(kt => kt.CenterId == centerId && kt.StudentId == studentId && kt.TopicNodeId == 1);
            if (twin != null)
            {
                twin.MasteryPercentage += (completionIndex * 10m);
                twin.UpdatedAt = UtcNow;
            }

            // 3. SaveChanges checkpoint
            await context.SaveChangesAsync();

            // 4. Generate recommendation
            await engine.GenerateAndPersistAsync(
                centerId,
                studentId,
                subjectId,
                sourceAttemptId: (ulong)completionIndex,
                UtcNow.AddSeconds(completionIndex),
                CancellationToken.None);

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        // Run both concurrent pipelines
        await Task.WhenAll(
            Task.Run(() => RunCompletionPipelineAsync(1)),
            Task.Run(() => RunCompletionPipelineAsync(2)));

        // Verification on separate clean context
        var verifyTenant = new TenantContext();
        using var verifyScope = verifyTenant.BeginScope(centerId);
        await using var verifyContext = CreateContext(database.ConnectionString, verifyTenant);

        var activeRecs = await verifyContext.Recommendations
            .Where(r => r.CenterId == centerId && r.StudentId == studentId && r.SubjectId == subjectId && r.Status == RecommendationStatus.Active)
            .ToListAsync();

        Assert.Single(activeRecs);

        var activePaths = await verifyContext.LearningPaths
            .Where(lp => lp.CenterId == centerId && lp.StudentId == studentId && lp.SubjectId == subjectId && lp.Status == LearningPathStatus.Active)
            .ToListAsync();

        Assert.Single(activePaths);
        Assert.Equal(2u, activePaths[0].Version); // Second serialized execution incremented version to 2!

        var allRecs = await verifyContext.Recommendations
            .Where(r => r.CenterId == centerId && r.StudentId == studentId && r.SubjectId == subjectId)
            .ToListAsync();

        Assert.Equal(2, allRecs.Count);
        Assert.Single(allRecs, r => r.Status == RecommendationStatus.Active);
        Assert.Single(allRecs, r => r.Status == RecommendationStatus.Superseded);
    }

    private static EduTwinDbContext CreateContext(string connectionString, TenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseMySQL(connectionString);
        return new EduTwinDbContext(options.Options, tenant);
    }

    private static async Task SeedHierarchyAsync(
        string connectionString,
        Guid centerId,
        Guid studentId,
        Guid subjectId)
    {
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(connectionString, tenant);
        await context.Database.OpenConnectionAsync();
        try
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");

            context.Centers.Add(new Center
            {
                CenterId = centerId,
                CenterCode = $"C-{centerId:N}"[..10],
                CenterName = "MySQL Rec Center",
                Status = CenterStatus.Active,
                Timezone = "UTC",
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            context.Users.Add(new User
            {
                UserId = studentId,
                CenterId = centerId,
                Username = $"student-{studentId:N}",
                DisplayName = "MySQL Rec Student",
                PasswordHash = "hash",
                RoleName = UserRole.Student,
                Status = UserStatus.Active,
                AuthVersion = 1,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            context.Students.Add(new Student
            {
                StudentId = studentId,
                CenterId = centerId,
                FullName = "MySQL Rec Student",
                GradeLevel = 10,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            context.Subjects.Add(new Subject
            {
                SubjectId = subjectId,
                CenterId = centerId,
                SubjectCode = $"SUB-{subjectId:N}"[..10],
                SubjectName = "Rec Subject",
                IsActive = true,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            context.KnowledgeNodes.Add(new KnowledgeNode
            {
                CenterId = centerId,
                SubjectId = subjectId,
                NodeId = 1,
                NodeCode = "TOPIC-1",
                NodeName = "Topic 1",
                NodeType = NodeType.Topic,
                OrderIndex = 1,
                ExamImportance = 80m,
                EstimatedLearningMinutes = 60,
                IsActive = true,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            context.KnowledgeTwins.Add(new KnowledgeTwin
            {
                CenterId = centerId,
                StudentId = studentId,
                SubjectId = subjectId,
                TopicNodeId = 1,
                MasteryPercentage = 20m,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            context.Questions.Add(new Question
            {
                CenterId = centerId,
                SubjectId = subjectId,
                PrimaryTopicNodeId = 1,
                QuestionId = 101,
                QuestionText = "Sample Question",
                QuestionType = QuestionType.MultipleChoice,
                CorrectAnswer = "A",
                Solution = "S",
                LanguageCode = "vi",
                Difficulty = 2,
                MaxScore = 10m,
                EstimatedTimeSeconds = 60,
                Status = QuestionStatus.Active,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            context.Attempts.AddRange(
                new Attempt
                {
                    CenterId = centerId,
                    StudentId = studentId,
                    QuestionId = 101,
                    AttemptId = 1,
                    FinalAnswer = "A",
                    ReasoningLanguage = "vi",
                    Status = AttemptStatus.Completed,
                    ClientSubmissionId = Guid.NewGuid(),
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow
                },
                new Attempt
                {
                    CenterId = centerId,
                    StudentId = studentId,
                    QuestionId = 101,
                    AttemptId = 2,
                    FinalAnswer = "B",
                    ReasoningLanguage = "vi",
                    Status = AttemptStatus.Completed,
                    ClientSubmissionId = Guid.NewGuid(),
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow
                }
            );

            await context.SaveChangesAsync();
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
        }
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
            var databaseName = $"edutwin_rec_{Guid.NewGuid():N}";
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
            var database = new MySqlTestDatabase(
                adminBuilder.ConnectionString,
                databaseName,
                databaseBuilder.ConnectionString);
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
            try
            {
                await using var connection = new MySqlConnection(_adminConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP DATABASE IF EXISTS `{_databaseName}`;";
                await command.ExecuteNonQueryAsync();
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
