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

[Collection("MySqlDatabase")]
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

            // Phase A: Authoritative Transaction A (Update Twin & Commit)
            await using (var txA = await context.Database.BeginTransactionAsync())
            {
                await StudentLockHelper.AcquireStudentLockAsync(context, centerId, studentId);
                barrier.SignalAndWait(5000);

                var twin = await context.KnowledgeTwins
                    .SingleOrDefaultAsync(kt => kt.CenterId == centerId && kt.StudentId == studentId && kt.TopicNodeId == 1);
                if (twin != null)
                {
                    twin.MasteryPercentage += (completionIndex * 10m);
                    twin.UpdatedAt = UtcNow;
                }

                await context.SaveChangesAsync();
                await txA.CommitAsync();
            }

            // Phase B: Recommendation Transaction B (Separate Unit of Work)
            await engine.GenerateAndPersistAsync(
                centerId,
                studentId,
                subjectId,
                sourceAttemptId: (ulong)completionIndex,
                UtcNow.AddSeconds(completionIndex),
                CancellationToken.None);
        }

        // Run both concurrent pipelines
        await Task.WhenAll(
            Task.Run(() => RunCompletionPipelineAsync(1)),
            Task.Run(() => RunCompletionPipelineAsync(2)));

        // Verification on separate clean context
        var verifyTenant = new TenantContext();
        using var verifyScope = verifyTenant.BeginScope(centerId);
        await using var verifyContext = CreateContext(database.ConnectionString, verifyTenant);

        var finalTwin = await verifyContext.KnowledgeTwins
            .SingleOrDefaultAsync(kt => kt.CenterId == centerId && kt.StudentId == studentId && kt.TopicNodeId == 1);
        Assert.NotNull(finalTwin);
        Assert.Equal(50m, finalTwin.MasteryPercentage); // Seed 20m + P1 (10m) + P2 (20m) = 50m!

        var activeRecs = await verifyContext.Recommendations
            .Where(r => r.CenterId == centerId && r.StudentId == studentId && r.SubjectId == subjectId && r.Status == RecommendationStatus.Active)
            .ToListAsync();

        Assert.Single(activeRecs);
        Assert.Equal(
            50m,
            activeRecs[0].CalculationBreakdown.RootElement
                .GetProperty("CurrentMastery")
                .GetDecimal());

        var activePaths = await verifyContext.LearningPaths
            .Where(lp => lp.CenterId == centerId && lp.StudentId == studentId && lp.SubjectId == subjectId && lp.Status == LearningPathStatus.Active)
            .ToListAsync();

        Assert.Single(activePaths);

        var allRecs = await verifyContext.Recommendations
            .Where(r => r.CenterId == centerId && r.StudentId == studentId && r.SubjectId == subjectId)
            .ToListAsync();

        Assert.Single(allRecs, r => r.Status == RecommendationStatus.Active);
        if (allRecs.Count == 2)
        {
            // Pipeline 1 committed first, then Pipeline 2 superseded it and incremented version to 2
            Assert.Equal(2u, activePaths[0].Version);
            Assert.Single(allRecs, r => r.Status == RecommendationStatus.Superseded);
        }
        else
        {
            // Pipeline 2 committed first, then Pipeline 1 was safely recognized as a stale trigger and ignored
            Assert.Single(allRecs);
            Assert.Equal(1u, activePaths[0].Version);
        }
    }

    [MySqlIntegrationFact]
    public async Task StaleTrigger_DoesNotSupersedeNewerRecommendation()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerId, studentId, subjectId);

        var tenant = new TenantContext();
        using var scope = tenant.BeginScope(centerId);
        await using var context = CreateContext(database.ConnectionString, tenant);

        var candidateBuilder = new OpportunityCandidateBuilder(context);
        var linearSelector = new LinearFallbackSelector();
        var questionSelector = new AdaptiveQuestionSelector(context);
        var engine = new RecommendationEngine(context, candidateBuilder, linearSelector, questionSelector);

        // 1. Newer trigger generates recommendation at UtcNow (sourceAttemptId: 2)
        var newerResult = await engine.GenerateAndPersistAsync(
            centerId,
            studentId,
            subjectId,
            sourceAttemptId: 2,
            UtcNow,
            CancellationToken.None);

        Assert.Equal(RecommendationGenerationStatus.Generated, newerResult.Status);
        Assert.NotNull(newerResult.Recommendation);

        // 2. Older trigger runs with an earlier timestamp UtcNow.AddMinutes(-5) (sourceAttemptId: 1)
        var staleResult = await engine.GenerateAndPersistAsync(
            centerId,
            studentId,
            subjectId,
            sourceAttemptId: 1,
            UtcNow.AddMinutes(-5),
            CancellationToken.None);

        Assert.Equal(RecommendationGenerationStatus.StaleIgnored, staleResult.Status);

        // 3. Verify original recommendation remains active and was NOT superseded
        var reloadedRec = await context.Recommendations.FindAsync(newerResult.Recommendation.RecommendationId);
        Assert.NotNull(reloadedRec);
        Assert.Equal(RecommendationStatus.Active, reloadedRec.Status);
    }

    [MySqlIntegrationFact]
    public async Task NewerRecommendationAccepted_DelayedOlderTrigger_RemainsStaleInMySql()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SeedHierarchyAsync(database.ConnectionString, centerId, studentId, subjectId);

        var tenant = new TenantContext();
        using var scope = tenant.BeginScope(centerId);
        await using var context = CreateContext(database.ConnectionString, tenant);
        var engine = CreateEngine(context);

        var newer = await engine.GenerateAndPersistAsync(
            centerId,
            studentId,
            subjectId,
            sourceAttemptId: 2,
            UtcNow,
            CancellationToken.None);
        Assert.Equal(RecommendationGenerationStatus.Generated, newer.Status);

        var accepted = await engine.AcceptAsync(
            centerId,
            studentId,
            newer.Recommendation!.RecommendationId,
            UtcNow.AddSeconds(1),
            CancellationToken.None);
        Assert.True(accepted.Success);

        var stale = await engine.GenerateAndPersistAsync(
            centerId,
            studentId,
            subjectId,
            sourceAttemptId: 1,
            UtcNow.AddMinutes(-5),
            CancellationToken.None);

        Assert.Equal(RecommendationGenerationStatus.StaleIgnored, stale.Status);
        Assert.Single(
            await context.Recommendations.Where(r => r.Status == RecommendationStatus.Accepted).ToListAsync());
        Assert.Empty(
            await context.Recommendations.Where(r => r.Status == RecommendationStatus.Active).ToListAsync());
    }

    [MySqlIntegrationFact]
    public async Task NewerNoCandidate_DelayedOlderTrigger_RemainsStaleInMySql()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SeedHierarchyAsync(database.ConnectionString, centerId, studentId, subjectId);

        var tenant = new TenantContext();
        using var scope = tenant.BeginScope(centerId);
        await using var context = CreateContext(database.ConnectionString, tenant);
        var node = await context.KnowledgeNodes.SingleAsync(n => n.NodeId == 1);
        node.IsActive = false;
        await context.SaveChangesAsync();

        var engine = CreateEngine(context);
        var newer = await engine.GenerateAndPersistAsync(
            centerId,
            studentId,
            subjectId,
            sourceAttemptId: 2,
            UtcNow,
            CancellationToken.None);
        Assert.Equal(RecommendationGenerationStatus.NoCandidate, newer.Status);

        node = await context.KnowledgeNodes.SingleAsync(n => n.NodeId == 1);
        node.IsActive = true;
        await context.SaveChangesAsync();

        var stale = await engine.GenerateAndPersistAsync(
            centerId,
            studentId,
            subjectId,
            sourceAttemptId: 1,
            UtcNow.AddMinutes(-5),
            CancellationToken.None);

        Assert.Equal(RecommendationGenerationStatus.StaleIgnored, stale.Status);
        Assert.Empty(await context.Recommendations.ToListAsync());
        var watermark = await context.RecommendationGenerationStates.SingleAsync();
        Assert.Equal(nameof(RecommendationGenerationStatus.NoCandidate), watermark.LastOutcome);
        Assert.Equal(2ul, watermark.LastSourceAttemptId);
    }

    [MySqlIntegrationFact]
    public async Task ConcurrentAccept_IsSerializedAndIdempotentInMySql()
    {
        await AssertConcurrentMutationIsIdempotentAsync(dismiss: false);
    }

    [MySqlIntegrationFact]
    public async Task ConcurrentDismiss_IsSerializedAndIdempotentInMySql()
    {
        await AssertConcurrentMutationIsIdempotentAsync(dismiss: true);
    }

    [MySqlIntegrationFact]
    public async Task OpportunityCandidateBuilder_RelationalEvidenceLoading_ExecutesTopicSpecificWindow()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerId, studentId, subjectId);

        var tenant = new TenantContext();
        using var scope = tenant.BeginScope(centerId);
        await using var context = CreateContext(database.ConnectionString, tenant);

        // Seed 3 attempts on Topic 1 with Question loaded and positive-weight EvidenceAssessment
        for (ulong i = 10; i <= 12; i++)
        {
            var att = new Attempt
            {
                CenterId = centerId,
                AttemptId = i,
                StudentId = studentId,
                QuestionId = 101,
                FinalAnswer = "A",
                ReasoningLanguage = "vi",
                Status = AttemptStatus.Completed,
                ClientSubmissionId = Guid.NewGuid(),
                IsCorrect = true,
                CreatedAt = UtcNow.AddMinutes(-(double)i),
                UpdatedAt = UtcNow.AddMinutes(-(double)i)
            };
            context.Attempts.Add(att);

            var ana = new ReasoningAnalysis
            {
                CenterId = centerId,
                AnalysisId = i + 100,
                AttemptId = i,
                SchemaVersion = "v1",
                MissingSteps = JsonDocument.Parse("[]"),
                RootCauseNodeIds = JsonDocument.Parse("[]"),
                Feedback = "Solid proof",
                ReasoningQuality = 80m,
                CreatedAt = UtcNow.AddMinutes(-(double)i),
                UpdatedAt = UtcNow.AddMinutes(-(double)i)
            };
            context.ReasoningAnalyses.Add(ana);

            context.EvidenceAssessments.Add(new EvidenceAssessment
            {
                CenterId = centerId,
                EvidenceAssessmentId = i + 200,
                AttemptId = i,
                Attempt = att,
                AnalysisId = ana.AnalysisId,
                Analysis = ana,
                SourceType = EvidenceSourceType.AI,
                DecisionMode = EvidenceDecisionMode.AIWeighted,
                RequiresTeacherReview = false,
                AnalysisOverrideVersion = 0,
                ReasoningWeight = 1.0m,
                TrustLevel = EvidenceTrustLevel.Trusted,
                PolicyVersion = "v1",
                ReasonCodes = JsonDocument.Parse("[]"),
                EvaluatedAt = UtcNow.AddMinutes(-(double)i),
                CreatedAt = UtcNow.AddMinutes(-(double)i)
            });
        }
        await context.SaveChangesAsync();

        var candidateBuilder = new OpportunityCandidateBuilder(context);
        var result = await candidateBuilder.BuildCandidatesAsync(centerId, studentId, subjectId, CancellationToken.None);

        Assert.NotEmpty(result.EligibleCandidates);
        var topic1Candidate = result.EligibleCandidates.First(c => c.TopicNodeId == 1);
        Assert.Equal("TopicWindow", topic1Candidate.ReasoningQualitySource);
        Assert.Equal(3, topic1Candidate.ReasoningQualitySampleCount);
        Assert.Equal(3.0m, topic1Candidate.ReasoningWeightSum);
    }

    [MySqlIntegrationFact]
    public async Task FreshMigration_Catalog_Contains63PermissionsAnd102Mappings()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var tenant = new TenantContext();
        await using var context = CreateContext(database.ConnectionString, tenant);

        var permissionCount = await context.Permissions.CountAsync();
        var mappingCount = await context.PermissionAccountTypes.CountAsync();

        Assert.Equal(63, permissionCount);
        Assert.Equal(102, mappingCount);
    }

    [MySqlIntegrationFact]
    public async Task LearningPathItem_CalculationBreakdown_RoundTripsJsonCorrectly()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerId, studentId, subjectId);

        var tenant = new TenantContext();
        using var scope = tenant.BeginScope(centerId);
        await using var context = CreateContext(database.ConnectionString, tenant);

        var path = new LearningPath
        {
            LearningPathId = Guid.NewGuid(),
            CenterId = centerId,
            StudentId = studentId,
            SubjectId = subjectId,
            Strategy = LearningPathStrategy.OpportunityGap,
            Version = 1,
            Status = LearningPathStatus.Active,
            GeneratedAt = UtcNow,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow,
            Items = new List<LearningPathItem>
            {
                new()
                {
                    LearningPathItemId = 99991,
                    CenterId = centerId,
                    TopicNodeId = 1,
                    RankOrder = 1,
                    Status = LearningPathItemStatus.Current,
                    Reason = "Breakdown Test",
                    CalculationBreakdown = JsonDocument.Parse("{\"TopicNodeId\":1,\"CurrentMastery\":42.5}"),
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow
                }
            }
        };

        context.LearningPaths.Add(path);
        await context.SaveChangesAsync();

        await using var readContext = CreateContext(database.ConnectionString, tenant);
        var loadedItem = await readContext.LearningPathItems
            .AsNoTracking()
            .SingleOrDefaultAsync(i => i.LearningPathItemId == 99991);

        Assert.NotNull(loadedItem);
        Assert.NotNull(loadedItem.CalculationBreakdown);
        Assert.Equal(1ul, loadedItem.CalculationBreakdown.RootElement.GetProperty("TopicNodeId").GetUInt64());
        Assert.Equal(42.5m, loadedItem.CalculationBreakdown.RootElement.GetProperty("CurrentMastery").GetDecimal());
    }

    private static EduTwinDbContext CreateContext(string connectionString, TenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseMySQL(connectionString);
        return new EduTwinDbContext(options.Options, tenant);
    }

    private static RecommendationEngine CreateEngine(EduTwinDbContext context) =>
        new(
            context,
            new OpportunityCandidateBuilder(context),
            new LinearFallbackSelector(),
            new AdaptiveQuestionSelector(context));

    private static async Task AssertConcurrentMutationIsIdempotentAsync(bool dismiss)
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SeedHierarchyAsync(database.ConnectionString, centerId, studentId, subjectId);

        ulong recommendationId;
        var seedTenant = new TenantContext();
        using (seedTenant.BeginScope(centerId))
        {
            await using var seedContext = CreateContext(database.ConnectionString, seedTenant);
            var generated = await CreateEngine(seedContext).GenerateAndPersistAsync(
                centerId,
                studentId,
                subjectId,
                sourceAttemptId: 2,
                UtcNow,
                CancellationToken.None);
            recommendationId = generated.Recommendation!.RecommendationId;
        }

        async Task<RecommendationOperationResult> MutateAsync()
        {
            var tenant = new TenantContext();
            using var scope = tenant.BeginScope(centerId);
            await using var context = CreateContext(database.ConnectionString, tenant);
            var engine = CreateEngine(context);
            return dismiss
                ? await engine.DismissAsync(
                    centerId,
                    studentId,
                    recommendationId,
                    "Concurrent dismiss",
                    UtcNow.AddSeconds(1),
                    CancellationToken.None)
                : await engine.AcceptAsync(
                    centerId,
                    studentId,
                    recommendationId,
                    UtcNow.AddSeconds(1),
                    CancellationToken.None);
        }

        var results = await Task.WhenAll(
            Task.Run(MutateAsync),
            Task.Run(MutateAsync));
        Assert.All(results, result => Assert.True(result.Success));

        var verifyTenant = new TenantContext();
        using var verifyScope = verifyTenant.BeginScope(centerId);
        await using var verifyContext = CreateContext(database.ConnectionString, verifyTenant);
        var persisted = await verifyContext.Recommendations
            .SingleAsync(r => r.RecommendationId == recommendationId);
        Assert.Equal(
            dismiss ? RecommendationStatus.Dismissed : RecommendationStatus.Accepted,
            persisted.Status);
        if (dismiss)
        {
            Assert.Equal("Concurrent dismiss", persisted.DismissReason);
        }
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
