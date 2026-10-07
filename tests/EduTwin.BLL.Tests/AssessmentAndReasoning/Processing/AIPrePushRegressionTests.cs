using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.AssessmentAndReasoning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Processing;

public sealed partial class AIAnalysisJobProcessorTests
{
    [Theory]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("{\"FailureCode\":null,\"FailureDetail\":null}")]
    public async Task Durable_DamagedDeferredFallbackDiagnosticDoesNotSpendAnotherProviderCall(string diagnostic)
    {
        var store = new InMemoryDatabaseRoot(); var name = Guid.NewGuid().ToString(); var center = Guid.NewGuid();
        await SeedAsync(store, name, center, retryCount: 1);
        var tenant = new TenantContext(); using var scope = tenant.BeginScope(center);
        await using var db = CreateContext(store, name, tenant);
        var job = await db.AIAnalysisJobs.SingleAsync();
        job.LastErrorCode = "AI_FALLBACK_WAITING_FOR_EARLIER_EVIDENCE"; job.LastErrorMessage = diagnostic;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var ai = new RecordingAIService((_, _) => throw new InvalidOperationException("must not call provider"));
        Assert.Equal(AIAnalysisJobProcessingOutcome.FallbackCompleted,
            (await CreateSut(db, tenant, UtcNow, ai, durable: true).ExecuteAsync(1, "worker-current", default)).Outcome);
        Assert.Equal(0, ai.CallCount); Assert.Single(await db.TwinUpdateHistories.ToListAsync());
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(3, true)] // Shared attachment retry budget and identical submission timestamps.
    public async Task Durable_LaterFallbackWaitsThenCommitsWithoutAnotherProviderCall(byte retryCount, bool sameTimestamp)
    {
        var store = new InMemoryDatabaseRoot(); var name = Guid.NewGuid().ToString(); var center = Guid.NewGuid();
        await SeedAsync(store, name, center, retryCount: 0);
        var tenant = new TenantContext(); using var scope = tenant.BeginScope(center);
        await using (var db = CreateContext(store, name, tenant))
        {
            var first = await db.Attempts.SingleAsync();
            var second = (Attempt)db.Entry(first).CurrentValues.ToObject();
            second.AttemptId = 2; second.ClientSubmissionId = Guid.NewGuid();
            second.CreatedAt = first.CreatedAt.AddSeconds(sameTimestamp ? 0 : 1);
            db.Attempts.Add(second);
            var firstJob = await db.AIAnalysisJobs.SingleAsync();
            var secondJob = (AIAnalysisJob)db.Entry(firstJob).CurrentValues.ToObject();
            secondJob.AnalysisJobId = 2; secondJob.AttemptId = 2; secondJob.RetryCount = retryCount;
            db.AIAnalysisJobs.Add(secondJob); await db.SaveChangesAsync();
        }
        var failingAI = new RecordingAIService((_, _) => throw new InvalidOperationException("synthetic failure"));
        await using (var db = CreateContext(store, name, tenant))
        {
            var result = await CreateSut(db, tenant, UtcNow, failingAI, durable: true).ExecuteAsync(2, "worker-current", default);
            Assert.Equal(AIAnalysisJobProcessingOutcome.RetryScheduled, result.Outcome);
            Assert.Empty(await db.TwinUpdateHistories.ToListAsync());
            Assert.Empty(await db.ReasoningAnalyses.ToListAsync());
            var later = await db.AIAnalysisJobs.SingleAsync(x => x.AnalysisJobId == 2);
            Assert.Equal(retryCount, later.RetryCount);
            Assert.Equal("AI_FALLBACK_WAITING_FOR_EARLIER_EVIDENCE", later.LastErrorCode);
        }
        Assert.Equal(1, failingAI.CallCount);
        var neverCall = new RecordingAIService((_, _) => throw new InvalidOperationException("must not call provider again"));
        // A premature reclaim, including after process restart, preserves the decision.
        await using (var db = CreateContext(store, name, tenant))
        {
            await ReclaimLaterAsync(db);
            Assert.Equal(AIAnalysisJobProcessingOutcome.RetryScheduled,
                (await CreateSut(db, tenant, UtcNow.AddSeconds(2), neverCall, durable: true).ExecuteAsync(2, "worker-current", default)).Outcome);
            Assert.Empty(await db.TwinUpdateHistories.ToListAsync());
            Assert.Equal(retryCount, (await db.AIAnalysisJobs.SingleAsync(x => x.AnalysisJobId == 2)).RetryCount);
        }
        await using (var db = CreateContext(store, name, tenant))
        {
            var goodAI = new RecordingAIService((_, _) => Task.FromResult(ValidResponse("vi")));
            Assert.Equal(AIAnalysisJobProcessingOutcome.Completed,
                (await CreateSut(db, tenant, UtcNow, goodAI, durable: true).ExecuteAsync(1, "worker-current", default)).Outcome);
        }
        await using (var db = CreateContext(store, name, tenant))
        {
            await ReclaimLaterAsync(db);
            Assert.Equal(AIAnalysisJobProcessingOutcome.FallbackCompleted,
                (await CreateSut(db, tenant, UtcNow.AddSeconds(4), neverCall, durable: true).ExecuteAsync(2, "worker-current", default)).Outcome);
            var history = await db.TwinUpdateHistories.OrderBy(x => x.HistoryId).ToListAsync();
            Assert.Equal(new ulong?[] { 1, 2 }, history.Select(x => x.AttemptId));
            Assert.Equal(2, await db.EvidenceAssessments.CountAsync());
            Assert.Equal("AI_ANALYSIS_ATTEMPT_FAILED", (await db.AIAnalysisJobs.SingleAsync(x => x.AnalysisJobId == 2)).LastErrorCode);
            Assert.True((await db.ReasoningAnalyses.SingleAsync(x => x.AttemptId == 2)).IsFallback);
        }
        Assert.Equal(0, neverCall.CallCount);
    }

    private static async Task ReclaimLaterAsync(EduTwin.DAL.Persistence.EduTwinDbContext db)
    {
        var job = await db.AIAnalysisJobs.SingleAsync(x => x.AnalysisJobId == 2);
        job.Status = AIJobStatus.Processing; job.LeaseOwner = "worker-current"; job.LeaseUntil = UtcNow.AddMinutes(5);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    }
}
