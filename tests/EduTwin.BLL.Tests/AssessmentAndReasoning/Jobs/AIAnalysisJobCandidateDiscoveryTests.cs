using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Jobs;

public sealed class AIAnalysisJobCandidateDiscoveryTests
{
    private static readonly DateTime UtcNow =
        new(2026, 8, 14, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DiscoverAsync_BoundariesAndTerminalStates_ReturnsOnlyEligibleOrderedJobs()
    {
        var tenantContext = new TenantContext();
        await using var dbContext = CreateContext(tenantContext);
        var centerId = Guid.NewGuid();
        dbContext.Centers.Add(CreateCenter(centerId));
        dbContext.AIAnalysisJobs.AddRange(
            CreateJob(1, centerId, AIJobStatus.Pending, UtcNow.AddMinutes(-3)),
            CreateJob(2, centerId, AIJobStatus.Pending, UtcNow),
            CreateJob(3, centerId, AIJobStatus.Pending, UtcNow.AddTicks(1)),
            CreateJob(4, centerId, AIJobStatus.Processing, UtcNow, UtcNow.AddMinutes(-2)),
            CreateJob(5, centerId, AIJobStatus.Processing, UtcNow, UtcNow),
            CreateJob(6, centerId, AIJobStatus.Processing, UtcNow, UtcNow.AddTicks(1)),
            CreateJob(7, centerId, AIJobStatus.Processing, UtcNow, null),
            CreateJob(8, centerId, AIJobStatus.Completed, UtcNow.AddMinutes(-10)),
            CreateJob(9, centerId, AIJobStatus.FallbackCompleted, UtcNow.AddMinutes(-10)),
            CreateJob(10, centerId, AIJobStatus.FailedTerminal, UtcNow.AddMinutes(-10)));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var sut = new AIAnalysisJobCandidateDiscovery(
            dbContext,
            tenantContext,
            tenantContext,
            new FixedTimeProvider(UtcNow));

        var result = await sut.DiscoverAsync(20, 20, CancellationToken.None);

        Assert.True(result.HasCandidates);
        Assert.Equal([1ul, 4ul, 2ul], result.WorkItems.Select(item => item.AnalysisJobId));
        Assert.Equal([1001ul, 1004ul, 1002ul], result.WorkItems.Select(item => item.AttemptId));
        Assert.Equal(
            [
                AIAnalysisJobWorkKind.Claim,
                AIAnalysisJobWorkKind.RecoverExpiredLease,
                AIAnalysisJobWorkKind.Claim
            ],
            result.WorkItems.Select(item => item.Kind));
        Assert.All(result.WorkItems, item => Assert.Equal(centerId, item.CenterId));
        Assert.False(tenantContext.IsResolved);
        Assert.Null(tenantContext.UserId);
    }

    [Fact]
    public async Task DiscoverAsync_TwoCenters_AppliesPerCenterAndGlobalOldestLimits()
    {
        var tenantContext = new TenantContext();
        await using var dbContext = CreateContext(tenantContext);
        var centerA = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var centerB = Guid.Parse("20000000-0000-0000-0000-000000000002");
        dbContext.Centers.AddRange(CreateCenter(centerA), CreateCenter(centerB));
        dbContext.AIAnalysisJobs.AddRange(
            CreateJob(11, centerA, AIJobStatus.Pending, UtcNow.AddMinutes(-4)),
            CreateJob(12, centerA, AIJobStatus.Pending, UtcNow.AddMinutes(-3)),
            CreateJob(21, centerB, AIJobStatus.Pending, UtcNow.AddMinutes(-5)),
            CreateJob(22, centerB, AIJobStatus.Pending, UtcNow.AddMinutes(-2)));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var sut = new AIAnalysisJobCandidateDiscovery(
            dbContext,
            tenantContext,
            tenantContext,
            new FixedTimeProvider(UtcNow));

        var allResults = await sut.DiscoverAsync(2, 4, CancellationToken.None);
        Assert.Equal(
            [
                (21ul, 1021ul, centerB),
                (11ul, 1011ul, centerA),
                (12ul, 1012ul, centerA),
                (22ul, 1022ul, centerB)
            ],
            allResults.WorkItems.Select(item =>
                (item.AnalysisJobId, item.AttemptId, item.CenterId)));

        var result = await sut.DiscoverAsync(1, 1, CancellationToken.None);

        var workItem = Assert.Single(result.WorkItems);
        Assert.Equal(21ul, workItem.AnalysisJobId);
        Assert.Equal(1021ul, workItem.AttemptId);
        Assert.Equal(centerB, workItem.CenterId);
        Assert.Equal("correlation-21", workItem.CorrelationId);
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task DiscoverAsync_CancelledBeforeQuery_PropagatesCancellation()
    {
        var tenantContext = new TenantContext();
        await using var dbContext = CreateContext(tenantContext);
        var sut = new AIAnalysisJobCandidateDiscovery(
            dbContext,
            tenantContext,
            tenantContext,
            new FixedTimeProvider(UtcNow));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.DiscoverAsync(1, 1, cancellation.Token));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public async Task DiscoverAsync_NonPositiveLimits_FailsFast(
        int perCenterBatchSize,
        int totalBatchSize)
    {
        var tenantContext = new TenantContext();
        await using var dbContext = CreateContext(tenantContext);
        var sut = new AIAnalysisJobCandidateDiscovery(
            dbContext,
            tenantContext,
            tenantContext,
            new FixedTimeProvider(UtcNow));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => sut.DiscoverAsync(
                perCenterBatchSize,
                totalBatchSize,
                CancellationToken.None));
    }

    private static EduTwinDbContext CreateContext(TenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EduTwinDbContext(options, tenantContext);
    }

    private static Center CreateCenter(Guid centerId) => new()
    {
        CenterId = centerId,
        CenterCode = centerId.ToString("N")[..8].ToUpperInvariant(),
        CenterName = $"Center {centerId:N}",
        Status = CenterStatus.Active,
        Timezone = "Asia/Bangkok",
        CreatedAt = UtcNow,
        UpdatedAt = UtcNow
    };

    private static AIAnalysisJob CreateJob(
        ulong id,
        Guid centerId,
        AIJobStatus status,
        DateTime availableAt,
        DateTime? leaseUntil = null) => new()
    {
        AnalysisJobId = id,
        CenterId = centerId,
        AttemptId = id + 1000,
        Status = status,
        AvailableAt = availableAt,
        StartedAt = status == AIJobStatus.Processing ? UtcNow.AddMinutes(-10) : null,
        LeaseOwner = status == AIJobStatus.Processing ? "old-worker" : null,
        LeaseUntil = leaseUntil,
        CorrelationId = $"correlation-{id}",
        CreatedAt = UtcNow.AddMinutes(-20),
        UpdatedAt = UtcNow.AddMinutes(-10)
    };

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
