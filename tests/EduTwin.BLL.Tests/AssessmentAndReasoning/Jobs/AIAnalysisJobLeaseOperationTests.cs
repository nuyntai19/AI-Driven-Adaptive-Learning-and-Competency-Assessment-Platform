using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Jobs;

public sealed class AIAnalysisJobLeaseOperationTests
{
    private static readonly DateTime UtcNow =
        new(2026, 8, 14, 7, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExecuteAsync_AvailablePendingJob_PersistsDurableClaim()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, CreateJob(1, centerId, AIJobStatus.Pending));
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);
        var sut = CreateSut(context, tenant, UtcNow);

        var result = await sut.ExecuteAsync(
            WorkItem(1, centerId, AIAnalysisJobWorkKind.Claim),
            "worker-a",
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        Assert.Equal(AIAnalysisJobLeaseOutcome.Claimed, result.Outcome);
        var persisted = await ReloadAsync(store, databaseName, centerId, 1);
        Assert.Equal(AIJobStatus.Processing, persisted.Status);
        Assert.Equal(UtcNow, persisted.StartedAt);
        Assert.Equal("worker-a", persisted.LeaseOwner);
        Assert.Equal(UtcNow.AddMinutes(5), persisted.LeaseUntil);
        Assert.Equal(UtcNow, persisted.UpdatedAt);
        Assert.Equal((byte)0, persisted.RetryCount);
        Assert.Null(persisted.CompletedAt);
        Assert.Equal(2ul, persisted.RowVersion);
    }

    [Fact]
    public async Task ExecuteAsync_StrictlyExpiredLease_PersistsRecoveryWithoutRetry()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        var job = CreateJob(2, centerId, AIJobStatus.Processing);
        job.RetryCount = 1;
        job.LeaseUntil = UtcNow.AddTicks(-1);
        await SeedAsync(store, databaseName, job);
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);
        var sut = CreateSut(context, tenant, UtcNow);

        var result = await sut.ExecuteAsync(
            WorkItem(2, centerId, AIAnalysisJobWorkKind.RecoverExpiredLease),
            "new-worker",
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        Assert.Equal(AIAnalysisJobLeaseOutcome.Recovered, result.Outcome);
        var persisted = await ReloadAsync(store, databaseName, centerId, 2);
        Assert.Equal(AIJobStatus.Pending, persisted.Status);
        Assert.Equal(UtcNow, persisted.AvailableAt);
        Assert.Null(persisted.StartedAt);
        Assert.Null(persisted.LeaseOwner);
        Assert.Null(persisted.LeaseUntil);
        Assert.Equal((byte)1, persisted.RetryCount);
        Assert.Null(persisted.CompletedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ExecuteAsync_NonExpiredLease_ReturnsNotEligibleWithoutMutation(int ticks)
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        var job = CreateJob(3, centerId, AIJobStatus.Processing);
        job.LeaseUntil = UtcNow.AddTicks(ticks);
        await SeedAsync(store, databaseName, job);
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);
        var sut = CreateSut(context, tenant, UtcNow);

        var result = await sut.ExecuteAsync(
            WorkItem(3, centerId, AIAnalysisJobWorkKind.RecoverExpiredLease),
            "new-worker",
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        Assert.Equal(AIAnalysisJobLeaseOutcome.NotEligible, result.Outcome);
        var persisted = await ReloadAsync(store, databaseName, centerId, 3);
        Assert.Equal(AIJobStatus.Processing, persisted.Status);
        Assert.Equal("old-worker", persisted.LeaseOwner);
        Assert.Equal(UtcNow.AddTicks(ticks), persisted.LeaseUntil);
        Assert.Equal(1ul, persisted.RowVersion);
    }

    [Fact]
    public async Task ExecuteAsync_CrossTenantWorkItem_ReturnsNotFoundAndDoesNotMutate()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerA = Guid.NewGuid();
        var centerB = Guid.NewGuid();
        await SeedAsync(store, databaseName, CreateJob(4, centerA, AIJobStatus.Pending));
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerB);
        await using var context = CreateContext(store, databaseName, tenant);
        var sut = CreateSut(context, tenant, UtcNow);

        var result = await sut.ExecuteAsync(
            WorkItem(4, centerA, AIAnalysisJobWorkKind.Claim),
            "worker-b",
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        Assert.Equal(AIAnalysisJobLeaseOutcome.NotFound, result.Outcome);
        var persisted = await ReloadAsync(store, databaseName, centerA, 4);
        Assert.Equal(AIJobStatus.Pending, persisted.Status);
        Assert.Null(persisted.LeaseOwner);
        Assert.Equal(1ul, persisted.RowVersion);
    }

    [Fact]
    public async Task ExecuteAsync_PendingSnapshotButPersistedRowChanged_ReturnsNotEligible()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        var job = CreateJob(40, centerId, AIJobStatus.Processing);
        job.LeaseUntil = UtcNow.AddMinutes(1);
        await SeedAsync(store, databaseName, job);
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);

        var result = await CreateSut(context, tenant, UtcNow).ExecuteAsync(
            WorkItem(40, centerId, AIAnalysisJobWorkKind.Claim),
            "stale-worker",
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        Assert.Equal(AIAnalysisJobLeaseOutcome.NotEligible, result.Outcome);
        var persisted = await ReloadAsync(store, databaseName, centerId, 40);
        Assert.Equal(AIJobStatus.Processing, persisted.Status);
        Assert.Equal("old-worker", persisted.LeaseOwner);
        Assert.Equal(UtcNow.AddMinutes(1), persisted.LeaseUntil);
        Assert.Equal(1ul, persisted.RowVersion);
    }

    [Theory]
    [InlineData(AIJobStatus.Completed, AIAnalysisJobWorkKind.Claim)]
    [InlineData(AIJobStatus.FallbackCompleted, AIAnalysisJobWorkKind.Claim)]
    [InlineData(AIJobStatus.FailedTerminal, AIAnalysisJobWorkKind.Claim)]
    [InlineData(AIJobStatus.Completed, AIAnalysisJobWorkKind.RecoverExpiredLease)]
    [InlineData(AIJobStatus.FallbackCompleted, AIAnalysisJobWorkKind.RecoverExpiredLease)]
    [InlineData(AIJobStatus.FailedTerminal, AIAnalysisJobWorkKind.RecoverExpiredLease)]
    public async Task ExecuteAsync_TerminalJob_ReturnsNotEligibleWithoutMutation(
        AIJobStatus status,
        AIAnalysisJobWorkKind kind)
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, CreateJob(41, centerId, status));
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);

        var result = await CreateSut(context, tenant, UtcNow).ExecuteAsync(
            WorkItem(41, centerId, kind),
            "worker",
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        Assert.Equal(AIAnalysisJobLeaseOutcome.NotEligible, result.Outcome);
        var persisted = await ReloadAsync(store, databaseName, centerId, 41);
        Assert.Equal(status, persisted.Status);
        Assert.Equal(1ul, persisted.RowVersion);
    }

    [Fact]
    public async Task ExecuteAsync_TwoWorkersClaimSameRow_ExactlyOneWinsConcurrencyFence()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, CreateJob(5, centerId, AIJobStatus.Pending));
        var barrier = new SaveBarrierInterceptor(2);
        var tenantA = new TenantContext();
        var tenantB = new TenantContext();
        using var scopeA = tenantA.BeginScope(centerId);
        using var scopeB = tenantB.BeginScope(centerId);
        await using var contextA = CreateContext(store, databaseName, tenantA, barrier);
        await using var contextB = CreateContext(store, databaseName, tenantB, barrier);
        var operationA = CreateSut(contextA, tenantA, UtcNow);
        var operationB = CreateSut(contextB, tenantB, UtcNow);
        var workItem = WorkItem(5, centerId, AIAnalysisJobWorkKind.Claim);

        var results = await Task.WhenAll(
            operationA.ExecuteAsync(
                workItem,
                "worker-a",
                TimeSpan.FromMinutes(5),
                CancellationToken.None),
            operationB.ExecuteAsync(
                workItem,
                "worker-b",
                TimeSpan.FromMinutes(5),
                CancellationToken.None));

        Assert.Single(results, result => result.Outcome == AIAnalysisJobLeaseOutcome.Claimed);
        Assert.Single(results, result => result.Outcome == AIAnalysisJobLeaseOutcome.LostRace);
        var persisted = await ReloadAsync(store, databaseName, centerId, 5);
        var winningOwner = results[0].Outcome == AIAnalysisJobLeaseOutcome.Claimed
            ? "worker-a"
            : "worker-b";
        Assert.Equal(AIJobStatus.Processing, persisted.Status);
        Assert.Equal(winningOwner, persisted.LeaseOwner);
        Assert.Equal(UtcNow.AddMinutes(5), persisted.LeaseUntil);
        Assert.Equal((byte)0, persisted.RetryCount);
        Assert.Equal(2ul, persisted.RowVersion);
    }

    [Fact]
    public async Task ExecuteAsync_TwoWorkersRecoverSameRow_ExactlyOneWinsConcurrencyFence()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        var job = CreateJob(6, centerId, AIJobStatus.Processing);
        job.LeaseUntil = UtcNow.AddMinutes(-1);
        await SeedAsync(store, databaseName, job);
        var barrier = new SaveBarrierInterceptor(2);
        var tenantA = new TenantContext();
        var tenantB = new TenantContext();
        using var scopeA = tenantA.BeginScope(centerId);
        using var scopeB = tenantB.BeginScope(centerId);
        await using var contextA = CreateContext(store, databaseName, tenantA, barrier);
        await using var contextB = CreateContext(store, databaseName, tenantB, barrier);
        var workItem = WorkItem(6, centerId, AIAnalysisJobWorkKind.RecoverExpiredLease);

        var results = await Task.WhenAll(
            CreateSut(contextA, tenantA, UtcNow).ExecuteAsync(
                workItem,
                "worker-a",
                TimeSpan.FromMinutes(5),
                CancellationToken.None),
            CreateSut(contextB, tenantB, UtcNow).ExecuteAsync(
                workItem,
                "worker-b",
                TimeSpan.FromMinutes(5),
                CancellationToken.None));

        Assert.Single(results, result => result.Outcome == AIAnalysisJobLeaseOutcome.Recovered);
        Assert.Single(results, result => result.Outcome == AIAnalysisJobLeaseOutcome.LostRace);
        var persisted = await ReloadAsync(store, databaseName, centerId, 6);
        Assert.Equal(AIJobStatus.Pending, persisted.Status);
        Assert.Null(persisted.LeaseOwner);
        Assert.Null(persisted.LeaseUntil);
        Assert.Equal((byte)0, persisted.RetryCount);
        Assert.Equal(2ul, persisted.RowVersion);
    }

    [Fact]
    public async Task ExecuteAsync_NewWorkerAfterRestart_OnlyRecoversAfterLeaseExpiresThenClaimsNextPoll()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        var job = CreateJob(7, centerId, AIJobStatus.Processing);
        job.LeaseUntil = UtcNow.AddMinutes(1);
        await SeedAsync(store, databaseName, job);

        var beforeExpiry = await ExecuteInTenantAsync(
            store,
            databaseName,
            centerId,
            UtcNow,
            WorkItem(7, centerId, AIAnalysisJobWorkKind.RecoverExpiredLease),
            "worker-after-restart");
        var afterExpiry = await ExecuteInTenantAsync(
            store,
            databaseName,
            centerId,
            UtcNow.AddMinutes(2),
            WorkItem(7, centerId, AIAnalysisJobWorkKind.RecoverExpiredLease),
            "worker-after-restart");
        var nextPollClaim = await ExecuteInTenantAsync(
            store,
            databaseName,
            centerId,
            UtcNow.AddMinutes(2),
            WorkItem(7, centerId, AIAnalysisJobWorkKind.Claim),
            "worker-after-restart");

        Assert.Equal(AIAnalysisJobLeaseOutcome.NotEligible, beforeExpiry.Outcome);
        Assert.Equal(AIAnalysisJobLeaseOutcome.Recovered, afterExpiry.Outcome);
        Assert.Equal(AIAnalysisJobLeaseOutcome.Claimed, nextPollClaim.Outcome);
        var persisted = await ReloadAsync(store, databaseName, centerId, 7);
        Assert.Equal(AIJobStatus.Processing, persisted.Status);
        Assert.Equal("worker-after-restart", persisted.LeaseOwner);
        Assert.Equal(UtcNow.AddMinutes(7), persisted.LeaseUntil);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledBeforePersistence_DoesNotMutatePersistedJob()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, CreateJob(8, centerId, AIJobStatus.Pending));
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);
        var sut = CreateSut(context, tenant, UtcNow);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.ExecuteAsync(
            WorkItem(8, centerId, AIAnalysisJobWorkKind.Claim),
            "worker",
            TimeSpan.FromMinutes(5),
            cancellation.Token));

        var persisted = await ReloadAsync(store, databaseName, centerId, 8);
        Assert.Equal(AIJobStatus.Pending, persisted.Status);
        Assert.Null(persisted.LeaseOwner);
        Assert.Equal(1ul, persisted.RowVersion);
    }

    private static async Task<AIAnalysisJobLeaseResult> ExecuteInTenantAsync(
        InMemoryDatabaseRoot store,
        string databaseName,
        Guid centerId,
        DateTime utcNow,
        AIAnalysisJobWorkItem workItem,
        string workerId)
    {
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);
        return await CreateSut(context, tenant, utcNow).ExecuteAsync(
            workItem,
            workerId,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);
    }

    private static AIAnalysisJobLeaseOperation CreateSut(
        EduTwinDbContext context,
        TenantContext tenant,
        DateTime utcNow) => new(
            context,
            tenant,
            new AIAnalysisJobStateMachine(),
            new FixedTimeProvider(utcNow));

    private static EduTwinDbContext CreateContext(
        InMemoryDatabaseRoot store,
        string databaseName,
        TenantContext tenant,
        params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName, store);
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        return new EduTwinDbContext(builder.Options, tenant);
    }

    private static async Task SeedAsync(
        InMemoryDatabaseRoot store,
        string databaseName,
        AIAnalysisJob job)
    {
        var tenant = new TenantContext();
        await using var context = CreateContext(store, databaseName, tenant);
        context.AIAnalysisJobs.Add(job);
        await context.SaveChangesAsync();
    }

    private static async Task<AIAnalysisJob> ReloadAsync(
        InMemoryDatabaseRoot store,
        string databaseName,
        Guid centerId,
        ulong jobId)
    {
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);
        return await context.AIAnalysisJobs.AsNoTracking()
            .SingleAsync(job => job.AnalysisJobId == jobId);
    }

    private static AIAnalysisJobWorkItem WorkItem(
        ulong id,
        Guid centerId,
        AIAnalysisJobWorkKind kind) =>
        new(id, centerId, $"correlation-{id}", kind, UtcNow.AddMinutes(-1));

    private static AIAnalysisJob CreateJob(
        ulong id,
        Guid centerId,
        AIJobStatus status) => new()
    {
        AnalysisJobId = id,
        CenterId = centerId,
        AttemptId = id,
        Status = status,
        RetryCount = 0,
        AvailableAt = UtcNow.AddMinutes(-2),
        StartedAt = status == AIJobStatus.Processing ? UtcNow.AddMinutes(-2) : null,
        LeaseOwner = status == AIJobStatus.Processing ? "old-worker" : null,
        LeaseUntil = status == AIJobStatus.Processing ? UtcNow.AddMinutes(-1) : null,
        CorrelationId = $"correlation-{id}",
        CreatedAt = UtcNow.AddMinutes(-3),
        UpdatedAt = UtcNow.AddMinutes(-2)
    };

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class SaveBarrierInterceptor(int participantCount) : SaveChangesInterceptor
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
}
