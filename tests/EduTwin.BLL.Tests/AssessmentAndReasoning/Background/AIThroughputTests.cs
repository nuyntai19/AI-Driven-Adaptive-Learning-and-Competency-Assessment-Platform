using System.Collections.Concurrent;
using System.Diagnostics;
using EduTwin.API.AssessmentAndReasoning.Background;
using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.IdentityAndTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Background;

public sealed class AIThroughputTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(50, 1)] [InlineData(50, 2)] [InlineData(50, 4)] [InlineData(50, 8)]
    [InlineData(100, 1)] [InlineData(100, 2)] [InlineData(100, 4)] [InlineData(100, 8)]
    [InlineData(5000, 4)]
    public async Task SyntheticBatchUsesBoundedParallelismAndIsolatedTenantScopes(int count, int concurrency)
    {
        var scenario = new Scenario(count);
        var services = new ServiceCollection(); services.AddIdentityAndTenancy(); services.AddSingleton(scenario);
        services.AddScoped<IAIAnalysisJobCandidateDiscovery, Discovery>();
        services.AddScoped<IAIAnalysisJobLeaseOperation, Lease>(); services.AddScoped<IAIAnalysisJobProcessor, Processor>();
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var worker = new AIAnalysisJobBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
            new AIAnalysisJobWorkerOptions { BatchSize = count, PerCenterBatchSize = count, MaxConcurrentJobs = concurrency },
            new AIAnalysisJobWorkerIdentity("synthetic-throughput"), NullLogger<AIAnalysisJobBackgroundService>.Instance);
        var elapsed = Stopwatch.StartNew(); var result = await worker.RunBatchOnceAsync(CancellationToken.None); elapsed.Stop();
        Assert.Equal(count, result.CompletedCount); Assert.Equal(0, result.ExceptionCount);
        Assert.Equal(count, scenario.ContextIds.Distinct().Count());
        Assert.Equal(count, scenario.CompletedIds.Distinct().Count()); Assert.InRange(scenario.Peak, 1, concurrency);
        if (concurrency > 1) Assert.True(scenario.Peak > 1);
        output.WriteLine("SIMULATED jobs={0}, concurrency={1}, fake_provider_ms={2}, elapsed_ms={3}, peak={4}",
            count, concurrency, scenario.Delay, elapsed.ElapsedMilliseconds, scenario.Peak);
    }

    private sealed class Scenario(int count)
    {
        public int Count { get; } = count; public int Delay => Count > 100 ? 1 : 10;
        public Guid[] Centers { get; } = [Guid.NewGuid(), Guid.NewGuid()];
        public ConcurrentBag<Guid> ContextIds { get; } = []; public ConcurrentBag<ulong> CompletedIds { get; } = [];
        private int _active; private int _peak; public int Peak => _peak;
        public void Enter() { var active = Interlocked.Increment(ref _active); int old;
            do { old = _peak; if (old >= active) break; } while (Interlocked.CompareExchange(ref _peak, active, old) != old); }
        public void Exit() => Interlocked.Decrement(ref _active);
    }
    private sealed class Discovery(Scenario scenario, ITenantContext tenant) : IAIAnalysisJobCandidateDiscovery
    {
        public Task<AIAnalysisJobDiscoveryResult> DiscoverAsync(int perCenterBatchSize, int totalBatchSize, CancellationToken token)
        {
            Assert.False(tenant.IsResolved);
            return Task.FromResult(new AIAnalysisJobDiscoveryResult(Enumerable.Range(1, scenario.Count).Select(i =>
                new AIAnalysisJobWorkItem((ulong)i, (ulong)i, scenario.Centers[i % 2], $"fake-{i}", AIAnalysisJobWorkKind.Claim, DateTime.UtcNow)).ToArray()));
        }
    }
    private sealed class Lease(ITenantContext tenant) : IAIAnalysisJobLeaseOperation
    {
        public Task<AIAnalysisJobLeaseResult> ExecuteAsync(AIAnalysisJobWorkItem item, string owner, TimeSpan duration, CancellationToken token)
        { Assert.Equal(item.CenterId, tenant.CenterId); Assert.Null(tenant.UserId);
            return Task.FromResult(new AIAnalysisJobLeaseResult(item.AnalysisJobId, item.CenterId, AIAnalysisJobLeaseOutcome.Claimed)); }
    }
    private sealed class Processor(Scenario scenario, ITenantContext tenant) : IAIAnalysisJobProcessor
    {
        private readonly Guid _instance = Guid.NewGuid();
        public async Task<AIAnalysisJobProcessingResult> ExecuteAsync(ulong id, string owner, CancellationToken token)
        {
            var center = scenario.Centers[(int)id % 2]; Assert.Equal(center, tenant.CenterId); Assert.Null(tenant.UserId);
            scenario.ContextIds.Add(_instance); scenario.Enter();
            try { await Task.Delay(scenario.Delay, token); Assert.Equal(center, tenant.CenterId); scenario.CompletedIds.Add(id);
                return new(id, id, AIAnalysisJobProcessingOutcome.Completed); }
            finally { scenario.Exit(); }
        }
    }
}
