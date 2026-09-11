using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySql.Data.MySqlClient;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.AssessmentAndReasoning.Override;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Override;

[Collection("MySqlDatabase")]
public sealed class TeacherOverrideMySqlTests
{
    private const string AdminConnectionVariable = "EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING";
    private static readonly DateTime UtcNow = new(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

    [MySqlIntegrationFact]
    public async Task ExecuteAsync_ValidOverride_ReplaysAttemptsChronologicallyAndPersistsToMySql()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerId, teacherId, studentId, subjectId);

        var tenant = new TenantContext();
        tenant.Initialize(centerId, teacherId, nameof(UserRole.Teacher), 1);

        await using var context = CreateContext(database.ConnectionString, tenant);

        var timeProvider = new FixedTimeProvider(UtcNow);
        var useCase = new TeacherOverrideUseCase(
            context,
            tenant,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(context),
            new StudentTwinUpdater(context),
            new TwinUpdateHistoryWriter(context),
            timeProvider);

        var request = new TeacherOverrideRequest
        {
            ReasoningQuality = 88m,
            ErrorType = ErrorType.Presentation,
            Feedback = "Good approach, teacher confirmed.",
            IsCorrect = true,
            Reason = "Direct teacher evaluation",
            OverrideVersion = 0
        };

        var result = await useCase.ExecuteAsync(2001, request, CancellationToken.None);

        Assert.Equal(TeacherOverrideStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(1u, result.Data.OverrideVersion);
        Assert.False(result.Data.Replay.RecommendationRecalculated);

        // Verify database persistence in fresh context
        await using var verifyContext = CreateContext(database.ConnectionString, tenant);
        var analysis = await verifyContext.ReasoningAnalyses.SingleAsync(ra => ra.CenterId == centerId && ra.AnalysisId == 2001);
        Assert.Equal(88m, analysis.OverrideReasoningQuality);
        Assert.True(analysis.OverrideIsCorrect);
        Assert.Equal(1u, analysis.OverrideVersion);
        Assert.False(analysis.NeedsTeacherReview);

        var attempt = await verifyContext.Attempts.SingleAsync(a => a.CenterId == centerId && a.AttemptId == 1001);
        Assert.False(attempt.IsCorrect);
        Assert.True(analysis.OverrideIsCorrect);
        Assert.Equal(true, analysis.OverrideIsCorrect ?? attempt.IsCorrect);

        var newEvidence = await verifyContext.EvidenceAssessments.SingleAsync(ea => ea.CenterId == centerId && ea.AnalysisOverrideVersion == 1);
        Assert.Equal(EvidenceSourceType.TeacherOverride, newEvidence.SourceType);
        Assert.Equal(EvidenceTrustLevel.Trusted, newEvidence.TrustLevel);
        Assert.Equal(1.00m, newEvidence.ReasoningWeight);
        Assert.Equal(3001u, newEvidence.SupersedesAssessmentId);

        var twin = await verifyContext.KnowledgeTwins.SingleAsync(kt => kt.CenterId == centerId && kt.StudentId == studentId);
        Assert.Equal(1u, twin.EvidenceCount);
        Assert.True(twin.MasteryPercentage > 0m);

        var history = await verifyContext.TwinUpdateHistories.SingleAsync(h => h.CenterId == centerId && h.StudentId == studentId && h.EventSource == TwinEventSource.Replay);
        Assert.NotNull(history.CalculationBreakdown);
    }

    [MySqlIntegrationFact]
    public async Task ExecuteAsync_StaleOverrideVersion_ReturnsConflictInMySql()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerId, teacherId, studentId, subjectId);

        var tenant = new TenantContext();
        tenant.Initialize(centerId, teacherId, nameof(UserRole.Teacher), 1);

        await using var context = CreateContext(database.ConnectionString, tenant);

        var timeProvider = new FixedTimeProvider(UtcNow);
        var useCase = new TeacherOverrideUseCase(
            context,
            tenant,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(context),
            new StudentTwinUpdater(context),
            new TwinUpdateHistoryWriter(context),
            timeProvider);

        var request1 = new TeacherOverrideRequest
        {
            ReasoningQuality = 80m,
            ErrorType = ErrorType.None,
            Feedback = "First override",
            IsCorrect = true,
            Reason = "First check",
            OverrideVersion = 0
        };

        var result1 = await useCase.ExecuteAsync(2001, request1, CancellationToken.None);
        Assert.Equal(TeacherOverrideStatus.Success, result1.Status);

        // Second override using stale version 0
        var request2 = new TeacherOverrideRequest
        {
            ReasoningQuality = 90m,
            ErrorType = ErrorType.None,
            Feedback = "Conflicting override",
            IsCorrect = true,
            Reason = "Second check",
            OverrideVersion = 0 // Stale!
        };

        var result2 = await useCase.ExecuteAsync(2001, request2, CancellationToken.None);
        Assert.Equal(TeacherOverrideStatus.Conflict, result2.Status);
    }

    [MySqlIntegrationFact]
    public async Task ExecuteAsync_ConcurrentOverridesWithBarrier_ExactlyOneSucceedsAndOneReturnsConflict()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerId, teacherId, studentId, subjectId);

        var barrier = new CompetingSaveBarrier(2);
        var tenantA = new TenantContext();
        var tenantB = new TenantContext();
        tenantA.Initialize(centerId, teacherId, nameof(UserRole.Teacher), 1);
        tenantB.Initialize(centerId, teacherId, nameof(UserRole.Teacher), 1);

        await using var contextA = CreateContext(database.ConnectionString, tenantA, barrier);
        await using var contextB = CreateContext(database.ConnectionString, tenantB, barrier);

        var timeProvider = new FixedTimeProvider(UtcNow);
        var useCaseA = new TeacherOverrideUseCase(
            contextA,
            tenantA,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(contextA),
            new StudentTwinUpdater(contextA),
            new TwinUpdateHistoryWriter(contextA),
            timeProvider);

        var useCaseB = new TeacherOverrideUseCase(
            contextB,
            tenantB,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(contextB),
            new StudentTwinUpdater(contextB),
            new TwinUpdateHistoryWriter(contextB),
            timeProvider);

        var requestA = new TeacherOverrideRequest
        {
            ReasoningQuality = 80m,
            ErrorType = ErrorType.None,
            Feedback = "Concurrent teacher A",
            IsCorrect = true,
            Reason = "Race test A",
            OverrideVersion = 0
        };

        var requestB = new TeacherOverrideRequest
        {
            ReasoningQuality = 85m,
            ErrorType = ErrorType.None,
            Feedback = "Concurrent teacher B",
            IsCorrect = true,
            Reason = "Race test B",
            OverrideVersion = 0
        };

        var results = await Task.WhenAll(
            useCaseA.ExecuteAsync(2001, requestA, CancellationToken.None),
            useCaseB.ExecuteAsync(2001, requestB, CancellationToken.None));

        Assert.Single(results, r => r.Status == TeacherOverrideStatus.Success);
        Assert.Single(results, r => r.Status == TeacherOverrideStatus.Conflict);

        // Verify in fresh context: exactly 1 override evidence exists
        var verifyTenant = new TenantContext();
        verifyTenant.Initialize(centerId, teacherId, nameof(UserRole.Teacher), 1);
        await using var verifyContext = CreateContext(database.ConnectionString, verifyTenant);

        var overrideEvidences = await verifyContext.EvidenceAssessments
            .Where(e => e.CenterId == centerId && e.SourceType == EvidenceSourceType.TeacherOverride)
            .ToListAsync();
        Assert.Single(overrideEvidences);

        var analysis = await verifyContext.ReasoningAnalyses
            .SingleAsync(a => a.CenterId == centerId && a.AnalysisId == 2001);
        Assert.Equal(1u, analysis.OverrideVersion);
    }

    [MySqlIntegrationFact]
    public async Task ExecuteAsync_CenterManagerWithoutTeacherProfile_PersistsUserActor()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerId, teacherId, studentId, subjectId);

        var tenant = new TenantContext();
        tenant.Initialize(centerId, managerId, nameof(UserRole.CenterManager), 1);

        await using var context = CreateContext(database.ConnectionString, tenant);
        context.Users.Add(new User
        {
            CenterId = centerId,
            UserId = managerId,
            Username = $"manager-{managerId:N}",
            PasswordHash = "test-only-hash",
            RoleName = UserRole.CenterManager,
            DisplayName = "Center Manager",
            Status = UserStatus.Active,
            AuthVersion = 1,
            CreatedAt = UtcNow.AddDays(-1),
            UpdatedAt = UtcNow.AddDays(-1)
        });
        await context.SaveChangesAsync();

        var useCase = new TeacherOverrideUseCase(
            context,
            tenant,
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new StudentGoalRiskUpdater(context),
            new StudentTwinUpdater(context),
            new TwinUpdateHistoryWriter(context),
            new FixedTimeProvider(UtcNow));

        var result = await useCase.ExecuteAsync(
            2001,
            new TeacherOverrideRequest
            {
                ReasoningQuality = 88m,
                ErrorType = ErrorType.Presentation,
                Feedback = "Center manager confirmed the evidence.",
                IsCorrect = true,
                Reason = "Escalated review",
                OverrideVersion = 0
            },
            CancellationToken.None);

        Assert.Equal(TeacherOverrideStatus.Success, result.Status);

        await using var verifyContext = CreateContext(database.ConnectionString, tenant);
        var analysis = await verifyContext.ReasoningAnalyses
            .SingleAsync(item => item.CenterId == centerId && item.AnalysisId == 2001);
        Assert.Equal(managerId, analysis.OverriddenByUserId);
        Assert.False(await verifyContext.Teachers.AnyAsync(item => item.TeacherId == managerId));
    }

    [MySqlIntegrationFact]
    public async Task EvidenceGovernance_CompositeForeignKeysAndAppendOnlyTriggers_RejectInvalidRows()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerId, teacherId, studentId, subjectId);

        var tenant = new TenantContext();
        tenant.Initialize(centerId, teacherId, nameof(UserRole.Teacher), 1);

        await using (var setupContext = CreateContext(database.ConnectionString, tenant))
        {
            setupContext.Attempts.Add(new Attempt
            {
                CenterId = centerId,
                AttemptId = 1002,
                StudentId = studentId,
                QuestionId = 501,
                FinalAnswer = "B",
                ReasoningText = "Second attempt",
                IsCorrect = false,
                Confidence = 50m,
                ReasoningLanguage = "vi",
                Status = AttemptStatus.Completed,
                ClientSubmissionId = Guid.NewGuid(),
                CreatedAt = UtcNow.AddMinutes(-5),
                UpdatedAt = UtcNow.AddMinutes(-5)
            });
            setupContext.ReasoningAnalyses.Add(new ReasoningAnalysis
            {
                CenterId = centerId,
                AnalysisId = 2002,
                AttemptId = 1002,
                ReasoningQuality = 50m,
                AnalysisConfidence = 50m,
                Feedback = "Second analysis",
                SchemaVersion = "1.0",
                MissingSteps = JsonDocument.Parse("[]"),
                RootCauseNodeIds = JsonDocument.Parse("[]"),
                CreatedAt = UtcNow.AddMinutes(-5),
                UpdatedAt = UtcNow.AddMinutes(-5)
            });
            setupContext.EvidenceAssessments.Add(CreateEvidence(centerId, 3002, 1002, 2002));
            await setupContext.SaveChangesAsync();
        }

        await using (var mismatchedAnalysisContext = CreateContext(database.ConnectionString, tenant))
        {
            mismatchedAnalysisContext.EvidenceAssessments.Add(CreateEvidence(centerId, 4001, 1002, 2001));
            await Assert.ThrowsAsync<DbUpdateException>(() => mismatchedAnalysisContext.SaveChangesAsync());
        }

        await using (var mismatchedSupersedesContext = CreateContext(database.ConnectionString, tenant))
        {
            var evidence = CreateEvidence(centerId, 4002, 1002, 2002);
            evidence.SupersedesAssessmentId = 3001;
            mismatchedSupersedesContext.EvidenceAssessments.Add(evidence);
            await Assert.ThrowsAsync<DbUpdateException>(() => mismatchedSupersedesContext.SaveChangesAsync());
        }

        await using (var selfSupersedesContext = CreateContext(database.ConnectionString, tenant))
        {
            var evidence = CreateEvidence(centerId, 4003, 1002, 2002);
            evidence.SupersedesAssessmentId = 4003;
            selfSupersedesContext.EvidenceAssessments.Add(evidence);
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => selfSupersedesContext.SaveChangesAsync());
            Assert.Contains("cannot supersede itself", exception.InnerException?.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using (var updateContext = CreateContext(database.ConnectionString, tenant))
        {
            var evidence = await updateContext.EvidenceAssessments
                .SingleAsync(item => item.CenterId == centerId && item.EvidenceAssessmentId == 3002);
            evidence.RequiresTeacherReview = true;
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => updateContext.SaveChangesAsync());
            Assert.Contains("append-only", exception.InnerException?.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using (var deleteContext = CreateContext(database.ConnectionString, tenant))
        {
            var evidence = await deleteContext.EvidenceAssessments
                .SingleAsync(item => item.CenterId == centerId && item.EvidenceAssessmentId == 3002);
            deleteContext.EvidenceAssessments.Remove(evidence);
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => deleteContext.SaveChangesAsync());
            Assert.Contains("append-only", exception.InnerException?.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static EvidenceAssessment CreateEvidence(
        Guid centerId,
        ulong evidenceAssessmentId,
        ulong attemptId,
        ulong analysisId) => new()
    {
        CenterId = centerId,
        EvidenceAssessmentId = evidenceAssessmentId,
        AttemptId = attemptId,
        AnalysisId = analysisId,
        SourceType = EvidenceSourceType.AI,
        TrustLevel = EvidenceTrustLevel.Reduced,
        DecisionMode = EvidenceDecisionMode.AIWeighted,
        ReasoningWeight = 0.5m,
        ReasonCodes = JsonDocument.Parse("[\"AI_CONFIDENCE_50_TO_79\"]"),
        PolicyVersion = EvidenceGate.CurrentPolicyVersion,
        EvaluatedAt = UtcNow,
        CreatedAt = UtcNow
    };

    private static EduTwinDbContext CreateContext(
        string connectionString,
        TenantContext tenant,
        params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseMySQL(connectionString);
        if (interceptors.Length > 0)
        {
            options.AddInterceptors(interceptors);
        }
        return new EduTwinDbContext(options.Options, tenant);
    }

    private static async Task SeedHierarchyAsync(
        string connectionString,
        Guid centerId,
        Guid teacherId,
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

            var classId = Guid.NewGuid();

            context.Centers.Add(new Center
            {
                CenterId = centerId,
                CenterCode = $"C-{centerId:N}"[..10],
                CenterName = "MySQL Test Center",
                Status = CenterStatus.Active,
                Timezone = "UTC",
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });

            context.Users.Add(new User
            {
                CenterId = centerId,
                UserId = teacherId,
                Username = $"teacher-{teacherId:N}",
                PasswordHash = "test-only-hash",
                RoleName = UserRole.Teacher,
                DisplayName = "MySQL Test Teacher",
                Status = UserStatus.Active,
                AuthVersion = 1,
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });

            context.Teachers.Add(new Teacher
            {
                CenterId = centerId,
                TeacherId = teacherId,
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });

            context.Students.Add(new Student
            {
                CenterId = centerId,
                StudentId = studentId,
                FullName = "MySQL Test Student",
                GradeLevel = 10,
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });

            context.StudentTwins.Add(new StudentTwin
            {
                TwinId = Guid.NewGuid(),
                CenterId = centerId,
                StudentId = studentId,
                OverallMastery = 0m,
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });

            context.Classes.Add(new Class
            {
                CenterId = centerId,
                ClassId = classId,
                TeacherId = teacherId,
                ClassName = "Class 10-MySQL",
                AcademicYear = "2026-2027",
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });

            context.ClassStudents.Add(new ClassStudent
            {
                CenterId = centerId,
                ClassId = classId,
                StudentId = studentId,
                Status = ClassStudentStatus.Active,
                JoinedAt = UtcNow.AddDays(-1)
            });

            context.Subjects.Add(new Subject
            {
                CenterId = centerId,
                SubjectId = subjectId,
                SubjectCode = "MATH",
                SubjectName = "Mathematics",
                IsActive = true,
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });

            context.KnowledgeNodes.Add(new KnowledgeNode
            {
                CenterId = centerId,
                NodeId = 101,
                SubjectId = subjectId,
                NodeType = NodeType.Topic,
                NodeCode = "TOPIC-101",
                NodeName = "Quadratic Equations",
                OrderIndex = 1,
                ExamImportance = 1.0m,
                EstimatedLearningMinutes = 45,
                IsActive = true,
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });

            context.BehaviorTwins.Add(new BehaviorTwin
            {
                BehaviorTwinId = 1,
                CenterId = centerId,
                StudentId = studentId,
                SubjectId = subjectId,
                AvgTimeSpentSeconds = 60m,
                AvgConfidence = 90m,
                ConfidenceCalibration = 50.00m,
                AttemptCount = 1,
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });

            context.KnowledgeTwins.Add(new KnowledgeTwin
            {
                KnowledgeTwinId = 1,
                CenterId = centerId,
                StudentId = studentId,
                SubjectId = subjectId,
                TopicNodeId = 101,
                MasteryPercentage = 0m,
                EvidenceCount = 0,
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });

            context.Questions.Add(new Question
            {
                CenterId = centerId,
                QuestionId = 501,
                SubjectId = subjectId,
                PrimaryTopicNodeId = 101,
                QuestionText = "Question MySQL 1",
                CorrectAnswer = "A",
                Solution = "Sol 1",
                Difficulty = 3,
                EstimatedTimeSeconds = 60,
                LanguageCode = "vi",
                MaxScore = 10,
                Status = QuestionStatus.Active,
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });

            context.Attempts.Add(new Attempt
            {
                CenterId = centerId,
                AttemptId = 1001,
                StudentId = studentId,
                QuestionId = 501,
                FinalAnswer = "A",
                ReasoningText = "Reasoning text",
                IsCorrect = false,
                TimeSpentSeconds = 60,
                Confidence = 90m,
                ReasoningLanguage = "vi",
                Status = AttemptStatus.NeedsTeacherReview,
                CreatedAt = UtcNow.AddMinutes(-10),
                UpdatedAt = UtcNow.AddMinutes(-10)
            });

            context.ReasoningAnalyses.Add(new ReasoningAnalysis
            {
                CenterId = centerId,
                AnalysisId = 2001,
                AttemptId = 1001,
                ReasoningQuality = 40m,
                AnalysisConfidence = 45m,
                Feedback = "Needs human review",
                IsFallback = false,
                NeedsTeacherReview = true,
                OverrideVersion = 0,
                SchemaVersion = "1.0",
                MissingSteps = JsonDocument.Parse("[]"),
                RootCauseNodeIds = JsonDocument.Parse("[]"),
                CreatedAt = UtcNow.AddMinutes(-10),
                UpdatedAt = UtcNow.AddMinutes(-10)
            });

            context.EvidenceAssessments.Add(new EvidenceAssessment
            {
                CenterId = centerId,
                EvidenceAssessmentId = 3001,
                AttemptId = 1001,
                AnalysisId = 2001,
                SourceType = EvidenceSourceType.AI,
                TrustLevel = EvidenceTrustLevel.ReviewOnly,
                DecisionMode = EvidenceDecisionMode.AIWeighted,
                ReasoningWeight = 0.00m,
                ReasonCodes = JsonDocument.Parse("[\"AI_CONFIDENCE_BELOW_50\"]"),
                RequiresTeacherReview = true,
                PolicyVersion = "evidence-gate-v1",
                EvaluatedAt = UtcNow.AddMinutes(-10),
                CreatedAt = UtcNow.AddMinutes(-10)
            });

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
            var databaseName = $"edutwin_override_{Guid.NewGuid():N}";
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
            await using var connection = new MySqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS `{_databaseName}`;";
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTime utcNow) => _utcNow = new DateTimeOffset(utcNow);
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class CompetingSaveBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _allArrived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _remaining;

        public CompetingSaveBarrier(int participants)
        {
            _remaining = participants;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Decrement(ref _remaining) == 0)
            {
                _allArrived.TrySetResult();
            }

            await _allArrived.Task.WaitAsync(cancellationToken);
            return result;
        }
    }
}
