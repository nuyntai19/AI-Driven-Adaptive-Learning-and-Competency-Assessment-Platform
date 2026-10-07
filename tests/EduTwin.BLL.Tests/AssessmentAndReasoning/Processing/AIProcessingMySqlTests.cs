using EduTwin.API.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Assignments;
using EduTwin.BLL.Recommendations;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Processing;

public sealed partial class AIAnalysisJobProcessorMySqlTests
{
    [MySqlIntegrationFact]
    public async Task DurableMySql_PostProcessingAcknowledgesLeaseWithSubMicrosecondClock()
    {
        await using var database = await MySqlTestDatabase.CreateAsync(); var center = Guid.NewGuid();
        await SeedAsync(database.ConnectionString, center);
        var tenant = new TenantContext(); using var scope = tenant.BeginScope(center);
        await using var db = CreateContext(database.ConnectionString, tenant);
        var student = (await db.Attempts.SingleAsync()).StudentId; var subject = (await db.Questions.SingleAsync()).SubjectId;
        await new AIStudentPostProcessingQueue(db).EnqueueAsync(center, student, subject, null, 1, UtcNow.AddTicks(7), CancellationToken.None);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var recommendations = new Mock<IRecommendationEngine>();
        recommendations.Setup(x => x.GenerateAndPersistAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ulong?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RecommendationGenerationResult.NoCandidate());
        var processor = new AIStudentPostProcessor(db, recommendations.Object, Mock.Of<IOverallAssignmentCommentWorkflow>(),
            new FixedTimeProvider(UtcNow.AddSeconds(3).AddTicks(7)));
        Assert.True(await processor.RunOneAsync(student, subject, Guid.Empty, "precision-worker", CancellationToken.None));
        db.ChangeTracker.Clear(); var finished = await db.AIStudentPostProcessingJobs.SingleAsync();
        Assert.Equal(1ul, finished.ProcessedRevision); Assert.Null(finished.LeaseOwner); Assert.Null(finished.LeaseUntil);
    }

    [MySqlIntegrationFact]
    public async Task DurableMySql_ReverseCompletionPreservesEvidenceOrderAndAllTwinRecords()
    {
        await using var database = await MySqlTestDatabase.CreateAsync(); var center = Guid.NewGuid();
        await SeedAsync(database.ConnectionString, center);
        var tenant = new TenantContext(); using var scope = tenant.BeginScope(center);
        await using (var db = CreateContext(database.ConnectionString, tenant))
        {
            var original = await db.Attempts.SingleAsync();
            var second = (Attempt)db.Entry(original).CurrentValues.ToObject();
            second.AttemptId = 2; second.ClientSubmissionId = Guid.NewGuid(); second.CreatedAt = original.CreatedAt.AddSeconds(1);
            db.Attempts.Add(second);
            var originalJob = await db.AIAnalysisJobs.SingleAsync();
            var job = (AIAnalysisJob)db.Entry(originalJob).CurrentValues.ToObject();
            job.AnalysisJobId = 2; job.AttemptId = 2; db.AIAnalysisJobs.Add(job);
            await db.SaveChangesAsync();
        }
        await using (var db = CreateContext(database.ConnectionString, tenant))
        {
            var result = await CreateProcessor(db, tenant, durable: true).ExecuteAsync(2, "mysql-worker", CancellationToken.None);
            Assert.Equal(AIAnalysisJobProcessingOutcome.RetryScheduled, result.Outcome);
            Assert.Empty(await db.ReasoningAnalyses.ToListAsync());
            Assert.Single(await db.AIAnalysisCheckpoints.ToListAsync());
        }
        await using (var db = CreateContext(database.ConnectionString, tenant))
            Assert.Equal(AIAnalysisJobProcessingOutcome.Completed,
                (await CreateProcessor(db, tenant, durable: true).ExecuteAsync(1, "mysql-worker", CancellationToken.None)).Outcome);
        await using (var db = CreateContext(database.ConnectionString, tenant))
        {
            var job = await db.AIAnalysisJobs.SingleAsync(x => x.AnalysisJobId == 2);
            job.Status = AIJobStatus.Processing; job.LeaseOwner = "mysql-worker"; job.LeaseUntil = UtcNow.AddMinutes(5);
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var result = await CreateProcessor(db, tenant, aiService: new MustNotCallAI(), durable: true)
                .ExecuteAsync(2, "mysql-worker", CancellationToken.None);
            Assert.Equal(AIAnalysisJobProcessingOutcome.Completed, result.Outcome);
            Assert.Equal(2, await db.ReasoningAnalyses.CountAsync()); Assert.Equal(2, await db.EvidenceAssessments.CountAsync());
            Assert.Equal(2, await db.TwinUpdateHistories.CountAsync());
            Assert.Equal(2u, (await db.BehaviorTwins.SingleAsync()).AttemptCount);
            Assert.Equal(2ul, (await db.KnowledgeTwins.SingleAsync()).LastAttemptId);
            Assert.Equal(2ul, (await db.AIStudentPostProcessingJobs.SingleAsync()).Revision);
        }
    }

    [MySqlIntegrationFact]
    public async Task DurableMySql_StaleLeaseCannotReplaceAnotherWorkersCheckpoint()
    {
        await using var database = await MySqlTestDatabase.CreateAsync(); var center = Guid.NewGuid();
        await SeedAsync(database.ConnectionString, center);
        var tenant = new TenantContext(); using var scope = tenant.BeginScope(center);
        await using var db = CreateContext(database.ConnectionString, tenant);
        var old = await db.AIAnalysisJobs.AsNoTracking().SingleAsync();
        var response = new AnalyzeReasoningResponse { SchemaVersion = AIAnalysisContract.SchemaVersion, Language = "en",
            ReasoningQuality = 90, ErrorType = ErrorType.None, MissingSteps = [], RootCauseNodeIds = [], Confidence = 95, Feedback = "Synthetic valid response." };
        var checkpoints = new AIAnalysisCheckpointStore(db, new FixedTimeProvider(UtcNow));
        Assert.True(await checkpoints.SaveAsync(old, "mysql-worker", "new-result", response, UtcNow, CancellationToken.None));
        await using (var other = CreateContext(database.ConnectionString, tenant))
        { var changed = await other.AIAnalysisJobs.SingleAsync(); changed.LeaseOwner = "new-owner"; await other.SaveChangesAsync(); }
        Assert.False(await checkpoints.SaveAsync(old, "mysql-worker", "stale-result", response, UtcNow, CancellationToken.None));
        Assert.Equal("new-result", (await db.AIAnalysisCheckpoints.SingleAsync()).RequestFingerprint);
    }

    [MySqlIntegrationFact]
    public async Task DurableMySql_QuotaCoordinatesIndependentServiceProviders()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        await using var first = QuotaProvider(database.ConnectionString);
        await using var second = QuotaProvider(database.ConnectionString);
        var a = new GeminiQuotaCoordinator(first.GetRequiredService<IServiceScopeFactory>(), new FixedTimeProvider(UtcNow));
        var b = new GeminiQuotaCoordinator(second.GetRequiredService<IServiceScopeFactory>(), new FixedTimeProvider(UtcNow));
        var pool = new GeminiQuotaPoolOptions { ProjectId = "synthetic-project", KeyIndexes = [0], MaxConcurrentRequests = 2 };
        var attempts = await Task.WhenAll(Enumerable.Range(0, 10).Select(async index =>
        {
            try { return await (index % 2 == 0 ? a : b).AcquireAsync(pool, "test-model", 10, TimeSpan.FromSeconds(30), CancellationToken.None); }
            catch (AIAnalysisDeferredException) { return null; }
        }));
        var reserved = attempts.OfType<GeminiQuotaLease>().ToArray(); Assert.Equal(2, reserved.Length);
        Assert.Single(reserved.Select(x => x.PoolId).Distinct());
        await a.CompleteAsync(reserved[0], 12, false, false, CancellationToken.None);
        Assert.NotNull(await b.AcquireAsync(pool, "test-model", 10, TimeSpan.FromSeconds(30), CancellationToken.None));
        await a.CompleteAsync(reserved[1], null, true, true, CancellationToken.None);
        await Assert.ThrowsAsync<AIAnalysisDeferredException>(() => b.AcquireAsync(pool, "test-model", 10, TimeSpan.FromSeconds(30), CancellationToken.None));
    }

    private static ServiceProvider QuotaProvider(string connection)
    {
        var services = new ServiceCollection(); services.AddIdentityAndTenancy();
        services.AddDbContext<EduTwinDbContext>(options => options.UseMySQL(connection));
        return services.BuildServiceProvider(validateScopes: true);
    }
    private sealed class MustNotCallAI : IAIService
    { public Task<AnalyzeReasoningResponse> AnalyzeReasoningAsync(AnalyzeReasoningRequest request, CancellationToken token) => throw new InvalidOperationException("checkpoint required"); }
}
