using EduTwin.API.AssessmentAndReasoning.Background;
using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Background;

public sealed class AIAnalysisJobBackgroundCheckpointTests
{
    private static readonly DateTime UtcNow =
        new(2026, 8, 14, 11, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RunBatchOnceAsync_PendingDatabaseJob_ClaimsAndCompletesFallbackCheckpoint()
    {
        var centerId = Guid.NewGuid();
        await using var provider = BuildProvider();
        await SeedAsync(provider, centerId, AIJobStatus.Pending);
        var worker = CreateWorker(provider);

        var result = await worker.RunBatchOnceAsync(CancellationToken.None);

        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.ClaimedCount);
        Assert.Equal(1, result.FallbackCompletedCount);
        Assert.Equal(0, result.ExceptionCount);
        var persisted = await ReloadAsync(provider, centerId);
        Assert.Equal(AIJobStatus.FallbackCompleted, persisted.Job.Status);
        Assert.Equal(AttemptStatus.NeedsTeacherReview, persisted.Attempt.Status);
        var analysis = Assert.Single(persisted.Analyses);
        Assert.True(analysis.IsFallback);
        Assert.True(analysis.NeedsTeacherReview);
        Assert.Equal(AnalysisProvider.RuleBased, analysis.Provider);
    }

    [Fact]
    public async Task RunBatchOnceAsync_ExpiredLeaseAfterRestart_RecoversThenProcessesNextBatch()
    {
        var centerId = Guid.NewGuid();
        await using var provider = BuildProvider();
        await SeedAsync(
            provider,
            centerId,
            AIJobStatus.Processing,
            leaseOwner: "old-worker",
            leaseUntil: UtcNow.AddMinutes(-1));
        var worker = CreateWorker(provider);

        var recoveryBatch = await worker.RunBatchOnceAsync(CancellationToken.None);
        var recovered = await ReloadAsync(provider, centerId);
        var processingBatch = await worker.RunBatchOnceAsync(CancellationToken.None);
        var terminal = await ReloadAsync(provider, centerId);

        Assert.Equal(1, recoveryBatch.RecoveredCount);
        Assert.Equal(0, recoveryBatch.ClaimedCount);
        Assert.Equal(0, recoveryBatch.FallbackCompletedCount);
        Assert.Equal(AIJobStatus.Pending, recovered.Job.Status);
        Assert.Empty(recovered.Analyses);
        Assert.Equal(1, processingBatch.ClaimedCount);
        Assert.Equal(1, processingBatch.FallbackCompletedCount);
        Assert.Equal(AIJobStatus.FallbackCompleted, terminal.Job.Status);
        Assert.Equal(AttemptStatus.NeedsTeacherReview, terminal.Attempt.Status);
        Assert.Single(terminal.Analyses);
    }

    private static ServiceProvider BuildProvider()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddIdentityAndTenancy();
        services.AddAssessmentAndReasoning();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(UtcNow));
        services.AddDbContext<EduTwinDbContext>((_, options) =>
            options
                .UseInMemoryDatabase(databaseName, databaseRoot)
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static AIAnalysisJobBackgroundService CreateWorker(
        ServiceProvider provider) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<TimeProvider>(),
            new AIAnalysisJobWorkerOptions
            {
                PollInterval = TimeSpan.FromMilliseconds(10),
                LeaseDuration = TimeSpan.FromMinutes(5),
                BatchSize = 10,
                PerCenterBatchSize = 10
            },
            new AIAnalysisJobWorkerIdentity("restart-worker"),
            NullLogger<AIAnalysisJobBackgroundService>.Instance);

    private static async Task SeedAsync(
        ServiceProvider provider,
        Guid centerId,
        AIJobStatus jobStatus,
        string? leaseOwner = null,
        DateTime? leaseUntil = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var tenantScopeFactory = scope.ServiceProvider
            .GetRequiredService<IBackgroundTenantScopeFactory>();
        using var tenantScope = tenantScopeFactory.BeginScope(centerId);
        var context = scope.ServiceProvider.GetRequiredService<EduTwinDbContext>();
        context.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterCode = $"CENTER-{centerId:N}"[..32],
            CenterName = "Processor checkpoint center",
            Status = CenterStatus.Active,
            Timezone = "Asia/Bangkok",
            CreatedAt = UtcNow.AddDays(-1),
            UpdatedAt = UtcNow.AddDays(-1)
        });
        context.Attempts.Add(new Attempt
        {
            AttemptId = 1,
            CenterId = centerId,
            StudentId = Guid.NewGuid(),
            QuestionId = 10,
            FinalAnswer = "checkpoint-answer",
            ReasoningText = "checkpoint-reasoning",
            IsCorrect = false,
            AwardedScore = 0,
            TimeSpentSeconds = 45,
            Confidence = 60,
            AnswerChanges = 1,
            Skipped = false,
            ReasoningLanguage = "en",
            Status = AttemptStatus.PendingAnalysis,
            ClientSubmissionId = Guid.NewGuid(),
            CreatedAt = UtcNow.AddMinutes(-5),
            CreatedBy = Guid.NewGuid(),
            UpdatedAt = UtcNow.AddMinutes(-5)
        });
        context.AIAnalysisJobs.Add(new AIAnalysisJob
        {
            AnalysisJobId = 1,
            CenterId = centerId,
            AttemptId = 1,
            Status = jobStatus,
            RetryCount = 0,
            AvailableAt = UtcNow.AddMinutes(-5),
            StartedAt = jobStatus == AIJobStatus.Processing
                ? UtcNow.AddMinutes(-3)
                : null,
            LeaseOwner = leaseOwner,
            LeaseUntil = leaseUntil,
            CorrelationId = "checkpoint-correlation",
            CreatedAt = UtcNow.AddMinutes(-5),
            UpdatedAt = UtcNow.AddMinutes(-3)
        });
        await context.SaveChangesAsync();
    }

    private static async Task<PersistedState> ReloadAsync(
        ServiceProvider provider,
        Guid centerId)
    {
        await using var scope = provider.CreateAsyncScope();
        var tenantScopeFactory = scope.ServiceProvider
            .GetRequiredService<IBackgroundTenantScopeFactory>();
        using var tenantScope = tenantScopeFactory.BeginScope(centerId);
        var context = scope.ServiceProvider.GetRequiredService<EduTwinDbContext>();
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
}
