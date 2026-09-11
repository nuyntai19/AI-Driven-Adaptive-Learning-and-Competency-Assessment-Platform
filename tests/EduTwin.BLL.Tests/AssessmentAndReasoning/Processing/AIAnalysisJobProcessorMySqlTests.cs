using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Recommendations;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.DigitalTwin;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySql.Data.MySqlClient;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Processing;

[Collection("MySqlDatabase")]
public sealed class AIAnalysisJobProcessorMySqlTests
{
    private const string AdminConnectionVariable =
        "EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING";
    private static readonly DateTime UtcNow =
        new(2026, 8, 15, 9, 0, 0, DateTimeKind.Utc);

    [MySqlIntegrationFact]
    public async Task ExecuteAsync_FailureAfterRelationalSave_RollsBackAllAggregates()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        await SeedAsync(database.ConnectionString, centerId);
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using (var context = CreateContext(
                         database.ConnectionString,
                         tenant,
                         new ThrowAfterSaveInterceptor()))
        {
            var processor = CreateProcessor(context, tenant);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                processor.ExecuteAsync(1, "mysql-worker", CancellationToken.None));
        }

        var persisted = await ReloadAsync(database.ConnectionString, centerId);
        Assert.Equal(AttemptStatus.PendingAnalysis, persisted.Attempt.Status);
        Assert.Equal(1ul, persisted.Attempt.RowVersion);
        Assert.Equal(AIJobStatus.Processing, persisted.Job.Status);
        Assert.Equal(1ul, persisted.Job.RowVersion);
        Assert.Empty(persisted.Analyses);
    }

    [MySqlIntegrationFact]
    public async Task ExecuteAsync_TwoRelationalTransactions_OnlyOneProcessorWins()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        await SeedAsync(database.ConnectionString, centerId);
        var barrier = new CompetingSaveBarrier(2);
        var tenantA = new TenantContext();
        var tenantB = new TenantContext();
        using var tenantScopeA = tenantA.BeginScope(centerId);
        using var tenantScopeB = tenantB.BeginScope(centerId);
        await using (var contextA = CreateContext(
                         database.ConnectionString,
                         tenantA,
                         barrier))
        await using (var contextB = CreateContext(
                         database.ConnectionString,
                         tenantB,
                         barrier))
        {
            var results = await Task.WhenAll(
                CreateProcessor(contextA, tenantA).ExecuteAsync(
                    1,
                    "mysql-worker",
                    CancellationToken.None),
                CreateProcessor(contextB, tenantB).ExecuteAsync(
                    1,
                    "mysql-worker",
                    CancellationToken.None));

            Assert.Single(
                results,
                result => result.Outcome
                    == AIAnalysisJobProcessingOutcome.Completed);
            Assert.Single(
                results,
                result => result.Outcome
                    is AIAnalysisJobProcessingOutcome.LostRace
                        or AIAnalysisJobProcessingOutcome.AlreadyTerminal);
        }

        var persisted = await ReloadAsync(database.ConnectionString, centerId);
        Assert.Equal(AttemptStatus.Completed, persisted.Attempt.Status);
        Assert.Equal(2ul, persisted.Attempt.RowVersion);
        Assert.Equal(AIJobStatus.Completed, persisted.Job.Status);
        Assert.Equal(2ul, persisted.Job.RowVersion);
        Assert.Single(persisted.Analyses);
    }

    [MySqlIntegrationFact]
    public async Task ReasoningAnalysis_RelationalUniqueConstraint_RejectsDuplicateAttempt()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        await SeedAsync(database.ConnectionString, centerId);
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using (var processorContext = CreateContext(
                         database.ConnectionString,
                         tenant))
        {
            var result = await CreateProcessor(processorContext, tenant)
                .ExecuteAsync(1, "mysql-worker", CancellationToken.None);
            Assert.Equal(
                AIAnalysisJobProcessingOutcome.Completed,
                result.Outcome);
        }

        await using (var duplicateContext = CreateContext(
                         database.ConnectionString,
                         tenant))
        {
            duplicateContext.ReasoningAnalyses.Add(
                new RuleBasedFallbackBuilder().Build(new RuleBasedFallbackInput(
                    centerId,
                    1,
                    true,
                    1,
                    false,
                    "en",
                    UtcNow.AddSeconds(1))));

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                duplicateContext.SaveChangesAsync());
        }

        var persisted = await ReloadAsync(database.ConnectionString, centerId);
        Assert.Single(persisted.Analyses);
    }

    [MySqlIntegrationFact]
    public async Task ExecuteAsync_RuleFallback_PersistsOperationalTelemetryAndAppliesExactlyOnceInMySql()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        await SeedAsync(database.ConnectionString, centerId);
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);

        var failingAiService = new FailingAIService();

        // 1. First run: retry 0 fails, transitions to Pending with retry 1
        await using (var context1 = CreateContext(database.ConnectionString, tenant))
        {
            var processor1 = CreateProcessor(context1, tenant, failingAiService);
            var result1 = await processor1.ExecuteAsync(1, "mysql-worker", CancellationToken.None);
            Assert.Equal(AIAnalysisJobProcessingOutcome.RetryScheduled, result1.Outcome);
        }

        // Simulate lease renewal for retry: worker picks up pending job
        await using (var leaseContext = CreateContext(database.ConnectionString, tenant))
        {
            var job = await leaseContext.AIAnalysisJobs.SingleAsync(j => j.AnalysisJobId == 1);
            Assert.Equal(1u, job.RetryCount);
            Assert.Equal(AIJobStatus.Pending, job.Status);
            job.Status = AIJobStatus.Processing;
            job.LeaseOwner = "mysql-worker";
            job.LeaseUntil = UtcNow.AddMinutes(5);
            await leaseContext.SaveChangesAsync();
        }

        // 2. Second run: retry 1 fails again -> triggers RuleFallback through TwinCompletionOrchestrator
        await using (var context2 = CreateContext(database.ConnectionString, tenant))
        {
            var processor2 = CreateProcessor(context2, tenant, failingAiService);
            var result2 = await processor2.ExecuteAsync(1, "mysql-worker", CancellationToken.None);
            Assert.Equal(AIAnalysisJobProcessingOutcome.FallbackCompleted, result2.Outcome);
        }

        // 3. Verify MySQL database state
        await using (var verifyContext = CreateContext(database.ConnectionString, tenant))
        {
            var attempt = await verifyContext.Attempts.SingleAsync(a => a.AttemptId == 1);
            Assert.Equal(AttemptStatus.NeedsTeacherReview, attempt.Status);

            var job = await verifyContext.AIAnalysisJobs.SingleAsync(j => j.AnalysisJobId == 1);
            Assert.Equal(AIJobStatus.FallbackCompleted, job.Status);

            var analysis = await verifyContext.ReasoningAnalyses.SingleAsync(ra => ra.AttemptId == 1);
            Assert.True(analysis.IsFallback);
            Assert.True(analysis.NeedsTeacherReview);

            var evidence = await verifyContext.EvidenceAssessments.SingleAsync(ea => ea.AttemptId == 1);
            Assert.Equal(EvidenceTrustLevel.ReviewOnly, evidence.TrustLevel);
            Assert.Equal(0.00m, evidence.ReasoningWeight);

            // Operational telemetry must be applied to BehaviorTwin!
            var behaviorTwin = await verifyContext.BehaviorTwins.SingleAsync(b => b.CenterId == centerId);
            Assert.Equal(1u, behaviorTwin.AttemptCount);
            Assert.Equal(20m, behaviorTwin.AvgTimeSpentSeconds);

            // KnowledgeTwin mastery delta is 0
            var knowledgeTwin = await verifyContext.KnowledgeTwins.SingleAsync(kt => kt.CenterId == centerId);
            Assert.Equal(0.00m, knowledgeTwin.MasteryPercentage);
        }

        // 4. Exactly-once check: re-running or reclaimed retry cannot double-apply
        await using (var retryContext = CreateContext(database.ConnectionString, tenant))
        {
            var processorRetry = CreateProcessor(retryContext, tenant, failingAiService);
            var retryResult = await processorRetry.ExecuteAsync(1, "mysql-worker", CancellationToken.None);
            Assert.Equal(AIAnalysisJobProcessingOutcome.AlreadyTerminal, retryResult.Outcome);

            var behaviorTwin = await retryContext.BehaviorTwins.SingleAsync(b => b.CenterId == centerId);
            Assert.Equal(1u, behaviorTwin.AttemptCount); // Did NOT increment to 2!
        }
    }

    [MySqlIntegrationFact]
    public async Task ExecuteAsync_TwoDistinctJobsForSameStudent_ConcurrentRaceAndRecovery_ConvergesBothEvidenceIntoTwin()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        await SeedTwoJobsForSameStudentAsync(
            database.ConnectionString,
            centerId,
            studentId,
            subjectId,
            teacherId);

        var barrier = new CompetingSaveBarrier(2);
        var tenant1 = new TenantContext();
        var tenant2 = new TenantContext();
        using var scope1 = tenant1.BeginScope(centerId);
        using var scope2 = tenant2.BeginScope(centerId);

        AIAnalysisJobProcessingResult[] results;
        await using (var context1 = CreateContext(database.ConnectionString, tenant1, barrier))
        await using (var context2 = CreateContext(database.ConnectionString, tenant2, barrier))
        {
            var processor1 = CreateProcessor(
                context1,
                tenant1,
                recommendationEngine: CreateRecommendationEngine(context1));
            var processor2 = CreateProcessor(
                context2,
                tenant2,
                recommendationEngine: CreateRecommendationEngine(context2));

            // Concurrent execution on distinct jobs (201 & 202) for the same student on the same topic.
            // Transaction A acquires NO Student row lock.
            // When both reach SaveChangesAsync, RowVersion concurrency token on twins ensures exactly one winner.
            results = await Task.WhenAll(
                processor1.ExecuteAsync(201, "worker-1", CancellationToken.None),
                processor2.ExecuteAsync(202, "worker-2", CancellationToken.None));

            Assert.Single(results, r => r.Outcome == AIAnalysisJobProcessingOutcome.Completed);
            Assert.Single(results, r => r.Outcome == AIAnalysisJobProcessingOutcome.LostRace);
        }

        var winnerOutcome = results.Single(r => r.Outcome == AIAnalysisJobProcessingOutcome.Completed);
        var loserOutcome = results.Single(r => r.Outcome == AIAnalysisJobProcessingOutcome.LostRace);
        var losingJobId = loserOutcome.AnalysisJobId;
        var losingWorkerId = losingJobId == 201 ? "worker-1" : "worker-2";

        // Verify intermediate state: loser rolled back Transaction A cleanly without dirty state
        var checkTenant = new TenantContext();
        using (checkTenant.BeginScope(centerId))
        {
            await using var checkContext = CreateContext(database.ConnectionString, checkTenant);
            var losingJob = await checkContext.AIAnalysisJobs.SingleAsync(j => j.AnalysisJobId == losingJobId);
            Assert.Equal(AIJobStatus.Processing, losingJob.Status);

            var losingAttempt = await checkContext.Attempts.SingleAsync(a => a.AttemptId == loserOutcome.AttemptId!.Value);
            Assert.Equal(AttemptStatus.PendingAnalysis, losingAttempt.Status);

            Assert.Single(await checkContext.ReasoningAnalyses.Where(ra => ra.CenterId == centerId).ToListAsync());
            Assert.Single(await checkContext.EvidenceAssessments.Where(ea => ea.CenterId == centerId).ToListAsync());
            var midBehavior = await checkContext.BehaviorTwins.SingleAsync(b => b.CenterId == centerId && b.StudentId == studentId);
            Assert.Equal(1u, midBehavior.AttemptCount);
        }

        // Advance time past loser's LeaseUntil to exercise true background recovery:
        // CandidateDiscovery (RecoverExpiredLease) -> LeaseOperation.RecoverExpiredLease ->
        // CandidateDiscovery (Claim) -> LeaseOperation.Claim -> AIAnalysisJobProcessor.ExecuteAsync
        var recoveryTime = UtcNow.AddMinutes(6);

        // Step 1: CandidateDiscovery detects strictly expired lease
        var discoveryTenant = new TenantContext();
        await using var discoveryContext = CreateContext(database.ConnectionString, discoveryTenant);
        var discovery = new AIAnalysisJobCandidateDiscovery(
            discoveryContext,
            discoveryTenant,
            discoveryTenant,
            new FixedTimeProvider(recoveryTime));

        var discoveryResult = await discovery.DiscoverAsync(10, 10, CancellationToken.None);
        Assert.True(discoveryResult.HasCandidates);
        var recoverWorkItem = discoveryResult.WorkItems.Single(w => w.AnalysisJobId == losingJobId);
        Assert.Equal(AIAnalysisJobWorkKind.RecoverExpiredLease, recoverWorkItem.Kind);

        // Step 2: LeaseOperation recovers expired lease, resetting status to Pending
        var recoverTenant = new TenantContext();
        using (recoverTenant.BeginScope(centerId))
        {
            await using var recoverContext = CreateContext(database.ConnectionString, recoverTenant);
            var leaseOperation = new AIAnalysisJobLeaseOperation(
                recoverContext,
                recoverTenant,
                new AIAnalysisJobStateMachine(),
                new FixedTimeProvider(recoveryTime));

            var recoverResult = await leaseOperation.ExecuteAsync(
                recoverWorkItem,
                "recovery-worker",
                TimeSpan.FromMinutes(5),
                CancellationToken.None);

            Assert.Equal(AIAnalysisJobLeaseOutcome.Recovered, recoverResult.Outcome);
        }

        // Step 3: Next batch discovery detects the now-Pending job for Claim
        var claimDiscoveryTenant = new TenantContext();
        await using var claimDiscoveryContext = CreateContext(database.ConnectionString, claimDiscoveryTenant);
        var claimDiscovery = new AIAnalysisJobCandidateDiscovery(
            claimDiscoveryContext,
            claimDiscoveryTenant,
            claimDiscoveryTenant,
            new FixedTimeProvider(recoveryTime));

        var claimDiscoveryResult = await claimDiscovery.DiscoverAsync(10, 10, CancellationToken.None);
        var claimWorkItem = claimDiscoveryResult.WorkItems.Single(w => w.AnalysisJobId == losingJobId);
        Assert.Equal(AIAnalysisJobWorkKind.Claim, claimWorkItem.Kind);

        // Step 4: Worker claims the job (transitions Pending -> Processing)
        var claimTenant = new TenantContext();
        using (claimTenant.BeginScope(centerId))
        {
            await using var claimContext = CreateContext(database.ConnectionString, claimTenant);
            var claimLeaseOperation = new AIAnalysisJobLeaseOperation(
                claimContext,
                claimTenant,
                new AIAnalysisJobStateMachine(),
                new FixedTimeProvider(recoveryTime));

            var claimResult = await claimLeaseOperation.ExecuteAsync(
                claimWorkItem,
                "recovery-worker",
                TimeSpan.FromMinutes(5),
                CancellationToken.None);

            Assert.Equal(AIAnalysisJobLeaseOutcome.Claimed, claimResult.Outcome);
        }

        // Step 5: Processor executes the claimed job in a fresh unit-of-work
        var retryTenant = new TenantContext();
        using (retryTenant.BeginScope(centerId))
        {
            await using var retryContext = CreateContext(database.ConnectionString, retryTenant);
            var retryProcessor = CreateProcessor(
                retryContext,
                retryTenant,
                recommendationEngine: CreateRecommendationEngine(retryContext));

            var retryResult = await retryProcessor.ExecuteAsync(
                losingJobId,
                "recovery-worker",
                CancellationToken.None);

            Assert.Equal(AIAnalysisJobProcessingOutcome.Completed, retryResult.Outcome);
        }

        // Final authoritative state assertions
        var verifyTenant = new TenantContext();
        using (verifyTenant.BeginScope(centerId))
        {
            await using var verifyContext = CreateContext(database.ConnectionString, verifyTenant);

            // 1. Both jobs reached terminal Completed status (no job stuck in Processing)
            var job1 = await verifyContext.AIAnalysisJobs.SingleAsync(j => j.AnalysisJobId == 201);
            var job2 = await verifyContext.AIAnalysisJobs.SingleAsync(j => j.AnalysisJobId == 202);
            Assert.Equal(AIJobStatus.Completed, job1.Status);
            Assert.Equal(AIJobStatus.Completed, job2.Status);

            var anyStuck = await verifyContext.AIAnalysisJobs
                .AnyAsync(j => j.CenterId == centerId && j.Status == AIJobStatus.Processing);
            Assert.False(anyStuck);

            // 2. Both attempts reached terminal Completed status
            var attempt1 = await verifyContext.Attempts.SingleAsync(a => a.AttemptId == 101);
            var attempt2 = await verifyContext.Attempts.SingleAsync(a => a.AttemptId == 102);
            Assert.Equal(AttemptStatus.Completed, attempt1.Status);
            Assert.Equal(AttemptStatus.Completed, attempt2.Status);

            // 3. Exactly 2 ReasoningAnalysis records exist (1 per attempt, no duplicates)
            var analyses = await verifyContext.ReasoningAnalyses
                .Where(ra => ra.CenterId == centerId)
                .OrderBy(ra => ra.AttemptId)
                .ToListAsync();
            Assert.Equal(2, analyses.Count);
            Assert.Equal(101ul, analyses[0].AttemptId);
            Assert.Equal(102ul, analyses[1].AttemptId);

            // 4. Exactly 2 EvidenceAssessment records exist (effective evidence for both A + B)
            var evidences = await verifyContext.EvidenceAssessments
                .Where(ea => ea.CenterId == centerId)
                .OrderBy(ea => ea.AttemptId)
                .ToListAsync();
            Assert.Equal(2, evidences.Count);
            Assert.Equal(101ul, evidences[0].AttemptId);
            Assert.Equal(102ul, evidences[1].AttemptId);

            // 5. BehaviorTwin.AttemptCount incremented to exactly 2 (not 1, not 3)
            var behaviorTwin = await verifyContext.BehaviorTwins
                .SingleAsync(b => b.CenterId == centerId && b.StudentId == studentId);
            Assert.Equal(2u, behaviorTwin.AttemptCount);

            // 6. KnowledgeTwin state reflects both evidence contributions
            var knowledgeTwin = await verifyContext.KnowledgeTwins
                .SingleAsync(k => k.CenterId == centerId && k.StudentId == studentId && k.TopicNodeId == 1);
            Assert.True(knowledgeTwin.MasteryPercentage > 20.00m);
            Assert.Equal(2u, knowledgeTwin.EvidenceCount);
            Assert.Equal(loserOutcome.AttemptId!.Value, knowledgeTwin.LastAttemptId);

            // 7. TwinUpdateHistory has exactly 2 entries (no duplicates)
            var histories = await verifyContext.TwinUpdateHistories
                .Where(h => h.CenterId == centerId && h.StudentId == studentId)
                .OrderBy(h => h.CreatedAt)
                .ToListAsync();
            Assert.Equal(2, histories.Count);
            Assert.Contains(histories, h => h.AttemptId == 101ul);
            Assert.Contains(histories, h => h.AttemptId == 102ul);

            // 8. Recommendation generation state watermark points to latest trigger by tie-break rule (102ul)
            var generationState = await verifyContext.RecommendationGenerationStates
                .SingleOrDefaultAsync(gs => gs.CenterId == centerId && gs.StudentId == studentId && gs.SubjectId == subjectId);
            Assert.NotNull(generationState);
            Assert.Equal(102ul, generationState.LastSourceAttemptId);
            Assert.Equal(RecommendationGenerationStatus.Generated.ToString(), generationState.LastOutcome);
        }
    }

    private static AIAnalysisJobProcessor CreateProcessor(
        EduTwinDbContext context,
        TenantContext tenant,
        IAIService? aiService = null,
        IRecommendationEngine? recommendationEngine = null,
        TimeProvider? timeProvider = null) =>
        new(
            context,
            tenant,
            aiService ?? new SuccessfulAIService(),
            new AIAnalysisRequestFactory(),
            new AIReasoningAnalysisBuilder(),
            new RuleBasedFallbackBuilder(),
            new AIAnalysisJobStateMachine(),
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            timeProvider ?? new FixedTimeProvider(UtcNow),
            recommendationEngine: recommendationEngine);

    private static RecommendationEngine CreateRecommendationEngine(EduTwinDbContext context) =>
        new(
            context,
            new OpportunityCandidateBuilder(context),
            new LinearFallbackSelector(),
            new AdaptiveQuestionSelector(context));

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

    private static async Task SeedAsync(
        string connectionString,
        Guid centerId)
    {
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(connectionString, tenant);
        await context.Database.OpenConnectionAsync();
        try
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");
            var studentId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();

            context.Centers.Add(new Center
            {
                CenterId = centerId,
                CenterCode = $"C-{centerId:N}"[..10],
                CenterName = "Test Center",
                Status = CenterStatus.Active,
                Timezone = "UTC",
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });

            context.Students.Add(new Student
            {
                CenterId = centerId,
                StudentId = studentId,
                FullName = "Relational Test Student",
                GradeLevel = 10,
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
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

            context.Attempts.Add(new Attempt
            {
                AttemptId = 1,
                CenterId = centerId,
                StudentId = studentId,
                QuestionId = 1,
                FinalAnswer = "relational-test-answer",
                ReasoningText = "relational-test-reasoning",
                IsCorrect = true,
                AwardedScore = 1,
                TimeSpentSeconds = 20,
                Confidence = 80,
                AnswerChanges = 0,
                Skipped = false,
                ReasoningLanguage = "en",
                Status = AttemptStatus.PendingAnalysis,
                ClientSubmissionId = Guid.NewGuid(),
                CreatedAt = UtcNow.AddMinutes(-2),
                CreatedBy = Guid.NewGuid(),
                UpdatedAt = UtcNow.AddMinutes(-2)
            });
            context.AIAnalysisJobs.Add(new AIAnalysisJob
            {
                AnalysisJobId = 1,
                CenterId = centerId,
                AttemptId = 1,
                Status = AIJobStatus.Processing,
                RetryCount = 0,
                AvailableAt = UtcNow.AddMinutes(-2),
                StartedAt = UtcNow.AddMinutes(-1),
                LeaseOwner = "mysql-worker",
                LeaseUntil = UtcNow.AddMinutes(5),
                CorrelationId = "mysql-processor-test",
                CreatedAt = UtcNow.AddMinutes(-2),
                UpdatedAt = UtcNow.AddMinutes(-1)
            });
            context.Questions.Add(new Question
            {
                QuestionId = 1,
                CenterId = centerId,
                SubjectId = subjectId,
                PrimaryTopicNodeId = 1,
                CreatedByTeacherId = Guid.NewGuid(),
                QuestionType = QuestionType.Essay,
                Difficulty = 3,
                QuestionText = "Relational question",
                CorrectAnswer = "answer",
                Solution = "solution",
                ExpectedReasoning = "reasoning",
                GradingCriteria = new GradingCriteria
                {
                    SchemaVersion = "1.0",
                    RequiredIdeas = ["idea"],
                    CommonErrors = ["error"],
                    ScoringNotes = "notes"
                },
                MaxScore = 1,
                EstimatedTimeSeconds = 60,
                ReasoningRequired = true,
                LanguageCode = "en",
                Status = QuestionStatus.Active,
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });
            context.KnowledgeNodes.Add(new KnowledgeNode
            {
                NodeId = 1,
                CenterId = centerId,
                SubjectId = subjectId,
                NodeType = NodeType.Topic,
                NodeCode = "REL-1",
                NodeName = "Relational topic",
                ExamImportance = 50,
                EstimatedLearningMinutes = 20,
                IsActive = true,
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });
            context.QuestionKnowledgeNodes.Add(new QuestionKnowledgeNode
            {
                CenterId = centerId,
                QuestionId = 1,
                NodeId = 1,
                MappingRole = MappingRole.Primary,
                CreatedAt = UtcNow.AddDays(-1)
            });
            await context.SaveChangesAsync();
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task SeedTwoJobsForSameStudentAsync(
        string connectionString,
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        Guid teacherId)
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
                CenterName = "Test Center",
                Status = CenterStatus.Active,
                Timezone = "UTC",
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });

            context.Users.Add(new User
            {
                UserId = studentId,
                CenterId = centerId,
                Username = $"student-{studentId:N}"[..20],
                DisplayName = "Relational Test Student",
                PasswordHash = "hash",
                RoleName = UserRole.Student,
                Status = UserStatus.Active,
                AuthVersion = 1,
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });

            context.Students.Add(new Student
            {
                CenterId = centerId,
                StudentId = studentId,
                FullName = "Relational Test Student",
                GradeLevel = 10,
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
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
                NodeId = 1,
                CenterId = centerId,
                SubjectId = subjectId,
                NodeType = NodeType.Topic,
                NodeCode = "REL-TOPIC-1",
                NodeName = "Relational Topic 1",
                OrderIndex = 1,
                ExamImportance = 50m,
                EstimatedLearningMinutes = 20,
                IsActive = true,
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });

            context.Questions.AddRange(
                new Question
                {
                    QuestionId = 1,
                    CenterId = centerId,
                    SubjectId = subjectId,
                    PrimaryTopicNodeId = 1,
                    CreatedByTeacherId = teacherId,
                    QuestionType = QuestionType.Essay,
                    Difficulty = 3,
                    QuestionText = "Distinct question 1",
                    CorrectAnswer = "answer 1",
                    Solution = "solution 1",
                    ExpectedReasoning = "reasoning 1",
                    GradingCriteria = new GradingCriteria
                    {
                        SchemaVersion = "1.0",
                        RequiredIdeas = ["idea 1"],
                        CommonErrors = ["error 1"],
                        ScoringNotes = "notes 1"
                    },
                    MaxScore = 1,
                    EstimatedTimeSeconds = 60,
                    ReasoningRequired = true,
                    LanguageCode = "en",
                    Status = QuestionStatus.Active,
                    CreatedAt = UtcNow.AddDays(-1),
                    UpdatedAt = UtcNow.AddDays(-1)
                },
                new Question
                {
                    QuestionId = 2,
                    CenterId = centerId,
                    SubjectId = subjectId,
                    PrimaryTopicNodeId = 1,
                    CreatedByTeacherId = teacherId,
                    QuestionType = QuestionType.Essay,
                    Difficulty = 3,
                    QuestionText = "Distinct question 2",
                    CorrectAnswer = "answer 2",
                    Solution = "solution 2",
                    ExpectedReasoning = "reasoning 2",
                    GradingCriteria = new GradingCriteria
                    {
                        SchemaVersion = "1.0",
                        RequiredIdeas = ["idea 2"],
                        CommonErrors = ["error 2"],
                        ScoringNotes = "notes 2"
                    },
                    MaxScore = 1,
                    EstimatedTimeSeconds = 60,
                    ReasoningRequired = true,
                    LanguageCode = "en",
                    Status = QuestionStatus.Active,
                    CreatedAt = UtcNow.AddDays(-1),
                    UpdatedAt = UtcNow.AddDays(-1)
                },
                new Question
                {
                    QuestionId = 3,
                    CenterId = centerId,
                    SubjectId = subjectId,
                    PrimaryTopicNodeId = 1,
                    CreatedByTeacherId = teacherId,
                    QuestionType = QuestionType.MultipleChoice,
                    Difficulty = 2,
                    QuestionText = "Recommendation pool question 3",
                    CorrectAnswer = "A",
                    Solution = "solution 3",
                    ExpectedReasoning = "reasoning 3",
                    MaxScore = 1,
                    EstimatedTimeSeconds = 60,
                    ReasoningRequired = false,
                    LanguageCode = "en",
                    Status = QuestionStatus.Active,
                    CreatedAt = UtcNow.AddDays(-1),
                    UpdatedAt = UtcNow.AddDays(-1)
                }
            );

            context.QuestionKnowledgeNodes.AddRange(
                new QuestionKnowledgeNode
                {
                    CenterId = centerId,
                    QuestionId = 1,
                    NodeId = 1,
                    MappingRole = MappingRole.Primary,
                    CreatedAt = UtcNow.AddDays(-1)
                },
                new QuestionKnowledgeNode
                {
                    CenterId = centerId,
                    QuestionId = 2,
                    NodeId = 1,
                    MappingRole = MappingRole.Primary,
                    CreatedAt = UtcNow.AddDays(-1)
                },
                new QuestionKnowledgeNode
                {
                    CenterId = centerId,
                    QuestionId = 3,
                    NodeId = 1,
                    MappingRole = MappingRole.Primary,
                    CreatedAt = UtcNow.AddDays(-1)
                }
            );

            context.KnowledgeTwins.Add(new KnowledgeTwin
            {
                KnowledgeTwinId = 1,
                CenterId = centerId,
                StudentId = studentId,
                SubjectId = subjectId,
                TopicNodeId = 1,
                MasteryPercentage = 20.00m,
                EvidenceCount = 0,
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });

            context.BehaviorTwins.Add(new BehaviorTwin
            {
                BehaviorTwinId = 1,
                CenterId = centerId,
                StudentId = studentId,
                SubjectId = subjectId,
                AvgTimeSpentSeconds = 0m,
                SkipRate = 0m,
                ChangeAnswerRate = 0m,
                AvgConfidence = 0m,
                ConfidenceCalibration = 0m,
                AttemptCount = 0,
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });

            context.StudentTwins.Add(new StudentTwin
            {
                TwinId = Guid.NewGuid(),
                CenterId = centerId,
                StudentId = studentId,
                OverallMastery = 20.00m,
                CreatedAt = UtcNow.AddDays(-1),
                UpdatedAt = UtcNow.AddDays(-1)
            });

            context.Attempts.AddRange(
                new Attempt
                {
                    AttemptId = 101,
                    CenterId = centerId,
                    StudentId = studentId,
                    QuestionId = 1,
                    FinalAnswer = "answer-1",
                    ReasoningText = "reasoning-1",
                    IsCorrect = true,
                    AwardedScore = 1,
                    TimeSpentSeconds = 20,
                    Confidence = 80,
                    AnswerChanges = 0,
                    Skipped = false,
                    ReasoningLanguage = "en",
                    Status = AttemptStatus.PendingAnalysis,
                    ClientSubmissionId = Guid.NewGuid(),
                    CreatedAt = UtcNow.AddMinutes(-2),
                    CreatedBy = studentId,
                    UpdatedAt = UtcNow.AddMinutes(-2)
                },
                new Attempt
                {
                    AttemptId = 102,
                    CenterId = centerId,
                    StudentId = studentId,
                    QuestionId = 2,
                    FinalAnswer = "answer-2",
                    ReasoningText = "reasoning-2",
                    IsCorrect = true,
                    AwardedScore = 1,
                    TimeSpentSeconds = 25,
                    Confidence = 85,
                    AnswerChanges = 0,
                    Skipped = false,
                    ReasoningLanguage = "en",
                    Status = AttemptStatus.PendingAnalysis,
                    ClientSubmissionId = Guid.NewGuid(),
                    CreatedAt = UtcNow.AddMinutes(-1),
                    CreatedBy = studentId,
                    UpdatedAt = UtcNow.AddMinutes(-1)
                }
            );

            context.AIAnalysisJobs.AddRange(
                new AIAnalysisJob
                {
                    AnalysisJobId = 201,
                    CenterId = centerId,
                    AttemptId = 101,
                    Status = AIJobStatus.Processing,
                    RetryCount = 0,
                    AvailableAt = UtcNow.AddMinutes(-2),
                    StartedAt = UtcNow.AddMinutes(-1),
                    LeaseOwner = "worker-1",
                    LeaseUntil = UtcNow.AddMinutes(5),
                    CorrelationId = "corr-201",
                    CreatedAt = UtcNow.AddMinutes(-2),
                    UpdatedAt = UtcNow.AddMinutes(-1)
                },
                new AIAnalysisJob
                {
                    AnalysisJobId = 202,
                    CenterId = centerId,
                    AttemptId = 102,
                    Status = AIJobStatus.Processing,
                    RetryCount = 0,
                    AvailableAt = UtcNow.AddMinutes(-2),
                    StartedAt = UtcNow.AddMinutes(-1),
                    LeaseOwner = "worker-2",
                    LeaseUntil = UtcNow.AddMinutes(5),
                    CorrelationId = "corr-202",
                    CreatedAt = UtcNow.AddMinutes(-2),
                    UpdatedAt = UtcNow.AddMinutes(-1)
                }
            );

            await context.SaveChangesAsync();
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task<PersistedState> ReloadAsync(
        string connectionString,
        Guid centerId)
    {
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(connectionString, tenant);
        return new PersistedState(
            await context.Attempts.AsNoTracking().SingleAsync(),
            await context.AIAnalysisJobs.AsNoTracking().SingleAsync(),
            await context.ReasoningAnalyses.AsNoTracking().ToArrayAsync());
    }

    private sealed record PersistedState(
        Attempt Attempt,
        AIAnalysisJob Job,
        IReadOnlyList<ReasoningAnalysis> Analyses);

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class SuccessfulAIService : IAIService
    {
        public Task<AnalyzeReasoningResponse> AnalyzeReasoningAsync(
            AnalyzeReasoningRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new AnalyzeReasoningResponse
            {
                SchemaVersion = AIAnalysisContract.SchemaVersion,
                Language = request.Language,
                ReasoningQuality = 90,
                ErrorType = ErrorType.None,
                MissingSteps = [],
                RootCauseNodeIds = [],
                Confidence = 95,
                Feedback = "Valid relational response."
            });
        }
    }

    private sealed class FailingAIService : IAIService
    {
        public Task<AnalyzeReasoningResponse> AnalyzeReasoningAsync(
            AnalyzeReasoningRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Deliberate AI failure for fallback test.");
    }

    private sealed class ThrowAfterSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new InvalidOperationException(
                "Deliberate failure after relational commands were executed."));
    }

    private sealed class CompetingSaveBarrier(int participantCount) :
        SaveChangesInterceptor
    {
        private int _remaining = participantCount;
        private readonly TaskCompletionSource _allArrived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

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

    private sealed class MySqlIntegrationFactAttribute : FactAttribute
    {
        public MySqlIntegrationFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable(AdminConnectionVariable)))
            {
                Skip = $"Set {AdminConnectionVariable} to run MySQL integration tests.";
            }
        }
    }

    private sealed class MySqlTestDatabase : IAsyncDisposable
    {
        private readonly string _adminConnectionString;
        private readonly string _databaseName;

        private MySqlTestDatabase(
            string adminConnectionString,
            string databaseName,
            string connectionString)
        {
            _adminConnectionString = adminConnectionString;
            _databaseName = databaseName;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static async Task<MySqlTestDatabase> CreateAsync()
        {
            var configuredConnection = Environment.GetEnvironmentVariable(
                AdminConnectionVariable)
                ?? throw new InvalidOperationException(
                    $"{AdminConnectionVariable} is required.");
            var adminBuilder = new MySqlConnectionStringBuilder(configuredConnection)
            {
                Database = string.Empty,
                Pooling = false
            };
            var databaseName = $"edutwin_p11t05_{Guid.NewGuid():N}";
            await using (var connection = new MySqlConnection(adminBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    $"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4;";
                await command.ExecuteNonQueryAsync();
            }

            var databaseBuilder = new MySqlConnectionStringBuilder(
                adminBuilder.ConnectionString)
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
}
