using System.Text.Json;
using EduTwin.API.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Processing;

public sealed partial class AIAnalysisJobProcessorMySqlTests
{
    [MySqlIntegrationFact]
    public async Task DurableFallback_ChronologicalSQLCommitResumesWithoutAnotherProviderCall()
    {
        await using var database = await MySqlTestDatabase.CreateAsync(); var center = Guid.NewGuid();
        await SeedAsync(database.ConnectionString, center);
        var tenant = new TenantContext(); using var tenantScope = tenant.BeginScope(center);
        await using (var db = CreateContext(database.ConnectionString, tenant))
        {
            var first = await db.Attempts.SingleAsync();
            var second = (Attempt)db.Entry(first).CurrentValues.ToObject();
            second.AttemptId = 2; second.ClientSubmissionId = Guid.NewGuid(); second.CreatedAt = first.CreatedAt.AddSeconds(1);
            db.Attempts.Add(second);
            var firstJob = await db.AIAnalysisJobs.SingleAsync(); firstJob.RetryCount = 1;
            var secondJob = (AIAnalysisJob)db.Entry(firstJob).CurrentValues.ToObject();
            secondJob.AnalysisJobId = 2; secondJob.AttemptId = 2; db.AIAnalysisJobs.Add(secondJob);
            await db.SaveChangesAsync();
        }
        var ai = new CountingFallbackAI();
        await using (var db = CreateContext(database.ConnectionString, tenant))
        {
            Assert.Equal(AIAnalysisJobProcessingOutcome.RetryScheduled,
                (await CreateProcessor(db, tenant, ai, durable: true).ExecuteAsync(2, "mysql-worker", default)).Outcome);
            Assert.Empty(await db.TwinUpdateHistories.ToListAsync());
            Assert.Equal("AI_FALLBACK_WAITING_FOR_EARLIER_EVIDENCE", (await db.AIAnalysisJobs.SingleAsync(x => x.AnalysisJobId == 2)).LastErrorCode);
        }
        await using (var db = CreateContext(database.ConnectionString, tenant))
            Assert.Equal(AIAnalysisJobProcessingOutcome.FallbackCompleted,
                (await CreateProcessor(db, tenant, ai, durable: true).ExecuteAsync(1, "mysql-worker", default)).Outcome);
        await using (var db = CreateContext(database.ConnectionString, tenant))
        {
            var later = await db.AIAnalysisJobs.SingleAsync(x => x.AnalysisJobId == 2);
            later.Status = AIJobStatus.Processing; later.LeaseOwner = "mysql-worker"; later.LeaseUntil = UtcNow.AddMinutes(5);
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            Assert.Equal(AIAnalysisJobProcessingOutcome.FallbackCompleted,
                (await CreateProcessor(db, tenant, ai, timeProvider: new FixedTimeProvider(UtcNow.AddSeconds(2)), durable: true)
                    .ExecuteAsync(2, "mysql-worker", default)).Outcome);
            Assert.Equal(new ulong?[] { 1, 2 }, (await db.TwinUpdateHistories.OrderBy(x => x.HistoryId).ToListAsync()).Select(x => x.AttemptId));
            Assert.Equal(2, await db.EvidenceAssessments.CountAsync());
        }
        Assert.Equal(2, ai.Calls); // Once for each exhausted attempt; not again for the deferred fallback.
    }

    private sealed class CountingFallbackAI : IAIService
    {
        public int Calls;
        public Task<AnalyzeReasoningResponse> AnalyzeReasoningAsync(AnalyzeReasoningRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls); throw new InvalidOperationException("synthetic failure");
        }
    }

    [MySqlIntegrationFact]
    public async Task GeminiGlobalAdmission_ParallelIndependentPoolsAndInstancesCannotExceedTwo()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var services = new ServiceCollection();
        services.AddScoped(_ => CreateContext(database.ConnectionString, new TenantContext()));
        await using var provider = services.BuildServiceProvider();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var scopes = provider.GetRequiredService<IServiceScopeFactory>();
        var admitted = await Task.WhenAll(Enumerable.Range(0, 20).Select(async i =>
        {
            var coordinator = new GeminiQuotaCoordinator(scopes, TimeProvider.System);
            var pool = new GeminiQuotaPoolOptions { ProjectId = $"synthetic-project-{i}", KeyIndexes = [i], MaxConcurrentRequests = 1 };
            try { return await coordinator.AcquireAsync(pool, $"synthetic-model-{i % 3}", 10,
                TimeSpan.FromSeconds(120), timeout.Token, globalMaxConcurrentRequests: 2); }
            catch (AIAnalysisDeferredException ex) { Assert.True(ex.BlocksAllPools); return null; }
        }));
        var leases = admitted.OfType<GeminiQuotaLease>().ToArray();
        Assert.Equal(2, leases.Length);
        await using (var scope = provider.CreateAsyncScope())
        {
            var rows = await scope.ServiceProvider.GetRequiredService<EduTwinDbContext>().AIProviderQuotaStates.ToListAsync(timeout.Token);
            Assert.Equal(2, JsonSerializer.Deserialize<GeminiConcurrencyLedger>(rows.Single(x => x.PoolId == GeminiQuotaCoordinator.GlobalCapacityPoolId).StateJson)!.Entries.Count);
            Assert.Equal(2, rows.Where(x => x.PoolId != GeminiQuotaCoordinator.GlobalCapacityPoolId)
                .Sum(x => JsonSerializer.Deserialize<GeminiQuotaLedger>(x.StateJson)!.DayRequestCount));
        }
        await Task.WhenAll(leases.Select(lease => new GeminiQuotaCoordinator(scopes, TimeProvider.System)
            .CompleteAsync(lease, 10, false, false, timeout.Token)));
        var fresh = new GeminiQuotaCoordinator(scopes, TimeProvider.System);
        var next = await fresh.AcquireAsync(new() { ProjectId = "synthetic-project-after-release", KeyIndexes = [0], MaxConcurrentRequests = 1 },
            "synthetic-singleton-repair", 10, TimeSpan.FromSeconds(30), timeout.Token, 2);
        await fresh.CompleteAsync(next, null, true, true, timeout.Token);
        await using var verify = provider.CreateAsyncScope();
        var global = await verify.ServiceProvider.GetRequiredService<EduTwinDbContext>().AIProviderQuotaStates
            .SingleAsync(x => x.PoolId == GeminiQuotaCoordinator.GlobalCapacityPoolId, timeout.Token);
        Assert.Empty(JsonSerializer.Deserialize<GeminiConcurrencyLedger>(global.StateJson)!.Entries);
    }
}
