using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Jobs;

/// <summary>
/// Capacity modeling and queue dynamics simulation tests for AI Analysis background processing.
/// NOTE: These tests execute an analytical capacity model against EF Core InMemory to evaluate algorithmic
/// query complexity, multi-tenant candidate discovery formulas, and concurrency state transitions.
/// They do NOT represent physical live-database I/O, network socket latency, or external LLM inference benchmarks.
/// </summary>
public sealed class AIAnalysisJobQueueCapacityModelTests
{
    private readonly ITestOutputHelper _output;
    private static readonly DateTime BaseUtc = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    public AIAnalysisJobQueueCapacityModelTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task AnalyticalModel_QuantifiesNPlusOneDiscoveryQueriesPerTick_AndCalculatesUnifiedQueryBenefit(int centerCount)
    {
        // 1. Arrange DB with centerCount centers
        var tenantContext = new TenantContext();
        var dbName = $"CapacityModel_NPlusOne_{centerCount}_{Guid.NewGuid():N}";
        await using var dbContext = CreateContext(dbName, tenantContext);

        var centers = Enumerable.Range(1, centerCount)
            .Select(i => new Center
            {
                CenterId = Guid.NewGuid(),
                CenterCode = $"CTR{i:D4}",
                CenterName = $"Center {i}",
                Status = CenterStatus.Active,
                Timezone = "Asia/Bangkok",
                CreatedAt = BaseUtc,
                UpdatedAt = BaseUtc
            })
            .ToList();

        dbContext.Centers.AddRange(centers);
        await dbContext.SaveChangesAsync();

        // 2. Compute N+1 query formula: 1 query for Centers + N queries for each center's AIAnalysisJobs
        // In current production code (AIAnalysisJobCandidateDiscovery.cs:43-82), this runs on every worker tick
        var baselineQueryCount = 1 + centerCount;
        var queryRateAt3SecInterval = (double)baselineQueryCount / 3.0;

        _output.WriteLine($"[Analytical Model: N+1 Discovery Analysis] Active Centers: {centerCount}");
        _output.WriteLine($"  - Formula: 1 center query + {centerCount} job candidate queries = {baselineQueryCount} queries / tick");
        _output.WriteLine($"  - Baseline Idle Polling Rate (at 3s interval): {queryRateAt3SecInterval:F1} QPS");
        _output.WriteLine($"  - Projected daily empty queries: {baselineQueryCount * (86400 / 3):N0} queries/day");

        // 3. Mathematical alternative: Unified single query (proposed optimization)
        var unifiedQueryCount = 1;
        var unifiedQps = (double)unifiedQueryCount / 3.0;
        _output.WriteLine($"  - Proposed Unified Query per tick: {unifiedQueryCount} (theoretical reduction of {((baselineQueryCount - 1.0) / baselineQueryCount * 100):F1}%)");
        _output.WriteLine($"  - Proposed Idle Polling Rate: {unifiedQps:F2} QPS");

        Assert.Equal(1 + centerCount, baselineQueryCount);
        Assert.Equal(1, unifiedQueryCount);
    }

    [Theory]
    [InlineData(100, 1, 100)]    // 100 submissions, 1 center, 100ms assumed worker throughput per task
    [InlineData(500, 5, 100)]    // 500 submissions, 5 centers, 100ms assumed worker throughput per task
    [InlineData(1000, 10, 100)]  // 1,000 submissions, 10 centers, 100ms assumed worker throughput per task
    public async Task CapacitySimulation_ProjectsQueueThroughputAndFrontendPollingRisk(
        int totalSubmissions,
        int centerCount,
        int assumedTaskProcessingMs)
    {
        var tenantContext = new TenantContext();
        var dbName = $"CapacitySimulation_Queue_{totalSubmissions}_{Guid.NewGuid():N}";
        await using var dbContext = CreateContext(dbName, tenantContext);

        var centerIds = Enumerable.Range(1, centerCount)
            .Select(_ => Guid.NewGuid())
            .ToList();

        foreach (var centerId in centerIds)
        {
            dbContext.Centers.Add(new Center
            {
                CenterId = centerId,
                CenterCode = $"C_{centerId.ToString()[..6]}",
                CenterName = "Test Center",
                Status = CenterStatus.Active,
                Timezone = "Asia/Bangkok",
                CreatedAt = BaseUtc,
                UpdatedAt = BaseUtc
            });
        }

        // Generate in-memory state representation
        var jobs = new List<AIAnalysisJob>(totalSubmissions);
        for (var i = 0; i < totalSubmissions; i++)
        {
            var assignedCenter = centerIds[i % centerCount];
            jobs.Add(new AIAnalysisJob
            {
                AnalysisJobId = (ulong)(i + 1),
                AttemptId = (ulong)(10000 + i + 1),
                CenterId = assignedCenter,
                CorrelationId = $"corr-{i + 1}",
                Status = AIJobStatus.Pending,
                AvailableAt = BaseUtc,
                CreatedAt = BaseUtc,
                UpdatedAt = BaseUtc,
                RetryCount = 0,
                RowVersion = 1ul
            });
        }

        dbContext.AIAnalysisJobs.AddRange(jobs);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // Model worker batch dynamics based on existing configuration options:
        // BatchSize = 50, PerCenterBatchSize = 25, PollInterval = 3s
        const int batchSize = 50;
        const int pollIntervalSec = 3;
        var simulatedWorkerThroughputPerSec = 1000.0 / assumedTaskProcessingMs;

        var totalBatchesNeeded = (int)Math.Ceiling((double)totalSubmissions / batchSize);
        var queueDepths = new List<int>();
        var latenciesSeconds = new List<double>();

        var remaining = totalSubmissions;
        var simulatedElapsedSec = 0.0;

        for (var b = 0; b < totalBatchesNeeded; b++)
        {
            queueDepths.Add(remaining);
            var batchCount = Math.Min(batchSize, remaining);

            // Modeled processing duration for this batch
            var batchProcessingTime = (double)batchCount / simulatedWorkerThroughputPerSec;
            simulatedElapsedSec += batchProcessingTime;

            for (var j = 0; j < batchCount; j++)
            {
                latenciesSeconds.Add(simulatedElapsedSec);
            }

            remaining -= batchCount;
            if (remaining > 0)
            {
                simulatedElapsedSec += pollIntervalSec;
            }
        }
        queueDepths.Add(0);

        latenciesSeconds.Sort();
        var p50 = latenciesSeconds[(int)(totalSubmissions * 0.50)];
        var p90 = latenciesSeconds[(int)(totalSubmissions * 0.90)];
        var p95 = latenciesSeconds[(int)(totalSubmissions * 0.95)];
        var maxLatency = latenciesSeconds[^1];

        // Frontend Bounded Polling Analysis (Interval: 1s -> 2s -> 3s, max 60 attempts = ~180s timeout limit)
        const double frontendMaxPollDurationSec = 180.0;
        var modeledTimedOutCount = latenciesSeconds.Count(l => l > frontendMaxPollDurationSec);

        // Projected polling reads hitting the database
        var modeledPollingQueries = latenciesSeconds.Sum(latency =>
        {
            var activeDuration = Math.Min(latency, frontendMaxPollDurationSec);
            return (int)Math.Ceiling(activeDuration / 2.5);
        });

        _output.WriteLine($"==================================================================");
        _output.WriteLine($"[CAPACITY SIMULATION MODEL] Submissions: {totalSubmissions} | Centers: {centerCount} | Assumed AI Task Latency: {assumedTaskProcessingMs}ms");
        _output.WriteLine($"==================================================================");
        _output.WriteLine($"  - Modeled Batches (50 jobs/batch): {totalBatchesNeeded}");
        _output.WriteLine($"  - Modeled Drain Duration: {simulatedElapsedSec:F1}s ({(simulatedElapsedSec / 60):F1} mins)");
        _output.WriteLine($"  - Modeled Latency Distribution:");
        _output.WriteLine($"      p50: {p50:F1}s");
        _output.WriteLine($"      p90: {p90:F1}s");
        _output.WriteLine($"      p95: {p95:F1}s");
        _output.WriteLine($"      Max: {maxLatency:F1}s");
        _output.WriteLine($"  - Modeled Frontend Bounded Polling Accumulation (max 60 attempts / ~180s threshold):");
        _output.WriteLine($"      Projected timed-out students: {modeledTimedOutCount} ({((double)modeledTimedOutCount / totalSubmissions * 100):F1}%)");
        _output.WriteLine($"      Projected total Polling SELECTs: {modeledPollingQueries:N0}");
        _output.WriteLine($"      Projected Polling Query Rate: {(modeledPollingQueries / Math.Max(1.0, simulatedElapsedSec)):F1} QPS");

        Assert.Equal(0, remaining);
        Assert.True(p50 > 0);
        Assert.True(p95 >= p50);
    }

    [Fact]
    public async Task StateMachineSimulation_VerifiesOptimisticConcurrencyRejection_UnderConcurrentLeasing()
    {
        var tenantContext = new TenantContext();
        var centerId = Guid.NewGuid();
        var dbName = $"StateSimulation_Concurrency_{Guid.NewGuid():N}";
        await using var dbContext = CreateContext(dbName, tenantContext);

        dbContext.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterCode = "CTR_RACE",
            CenterName = "Race Center",
            Status = CenterStatus.Active,
            Timezone = "Asia/Bangkok",
            CreatedAt = BaseUtc,
            UpdatedAt = BaseUtc
        });

        dbContext.AIAnalysisJobs.Add(new AIAnalysisJob
        {
            AnalysisJobId = 999ul,
            AttemptId = 888ul,
            CenterId = centerId,
            CorrelationId = "race-test",
            Status = AIJobStatus.Pending,
            AvailableAt = BaseUtc.AddMinutes(-1),
            CreatedAt = BaseUtc,
            UpdatedAt = BaseUtc,
            RetryCount = 0,
            RowVersion = 1ul
        });

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var stateMachine = new AIAnalysisJobStateMachine();
        var leaseOpWorker1 = new AIAnalysisJobLeaseOperation(
            dbContext,
            tenantContext,
            stateMachine,
            new FixedTimeProvider(BaseUtc));

        using var scope1 = tenantContext.BeginScope(centerId);
        var workItem = new AIAnalysisJobWorkItem(999ul, 888ul, centerId, "race-test", AIAnalysisJobWorkKind.Claim, BaseUtc);

        // Worker 1 claims job
        var result1 = await leaseOpWorker1.ExecuteAsync(workItem, "worker-1", TimeSpan.FromMinutes(2), CancellationToken.None);
        Assert.Equal(AIAnalysisJobLeaseOutcome.Claimed, result1.Outcome);

        // Worker 2 (using separate context and tenant instance simulating concurrent process) tries to claim the same job
        var tenantContextWorker2 = new TenantContext();
        using var scope2 = tenantContextWorker2.BeginScope(centerId);
        await using var dbContextWorker2 = CreateContext(dbName, tenantContextWorker2);
        var leaseOpWorker2 = new AIAnalysisJobLeaseOperation(
            dbContextWorker2,
            tenantContextWorker2,
            stateMachine,
            new FixedTimeProvider(BaseUtc));

        var result2 = await leaseOpWorker2.ExecuteAsync(workItem, "worker-2", TimeSpan.FromMinutes(2), CancellationToken.None);

        // Worker 2 finds job is now Processing (not Pending) -> State machine safely rejects claim
        _output.WriteLine($"[State Simulation] Worker 1 outcome: {result1.Outcome}");
        _output.WriteLine($"[State Simulation] Worker 2 outcome: {result2.Outcome} (Safely rejected by state machine OCC guard)");
        Assert.Equal(AIAnalysisJobLeaseOutcome.NotEligible, result2.Outcome);
    }

    private static EduTwinDbContext CreateContext(string dbName, TenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new EduTwinDbContext(options, tenantContext);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}
