using EduTwin.API.AssessmentAndReasoning.Background;
using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.IdentityAndTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Background;

public sealed class AIAnalysisJobBackgroundServiceTests
{
    [Fact]
    public async Task RunBatchOnceAsync_TwoCentersAndCandidateFailure_IsolatesScopesAndContinues()
    {
        var centerA = Guid.NewGuid();
        var centerB = Guid.NewGuid();
        var scenario = new WorkerScenario(
            [WorkItem(1, centerA), WorkItem(2, centerB)])
        {
            ThrowOnJobId = 1
        };
        await using var provider = BuildProvider(scenario);
        var sut = CreateWorker(provider, TimeProvider.System);

        var result = await sut.RunBatchOnceAsync(CancellationToken.None);

        Assert.Equal(2, result.CandidateCount);
        Assert.Equal(1, result.ClaimedCount);
        Assert.Equal(1, result.ExceptionCount);
        Assert.Equal(1, result.FallbackCompletedCount);
        Assert.Equal([centerA, centerB], scenario.ObservedCenters);
        Assert.Equal([centerB], scenario.ProcessorObservedCenters);
        Assert.All(scenario.ObservedUserIds, userId => Assert.Null(userId));
        Assert.All(scenario.ProcessorObservedUserIds, userId => Assert.Null(userId));
        Assert.Equal(2, scenario.LeaseOperationInstanceIds.Distinct().Count());
        Assert.Equal(2, scenario.LeaseOperationDisposals);
        Assert.Single(scenario.ProcessorInstanceIds);
        Assert.Equal(1, scenario.ProcessorDisposals);
        Assert.All(scenario.TenantResolvedAtLeaseDisposal, Assert.False);
        Assert.All(scenario.TenantResolvedAtProcessorDisposal, Assert.False);
        Assert.Equal(1, scenario.DiscoveryDisposals);
    }

    [Fact]
    public async Task RunBatchOnceAsync_EachCallCreatesNewBatchAndJobScopes()
    {
        var scenario = new WorkerScenario([WorkItem(3, Guid.NewGuid())]);
        await using var provider = BuildProvider(scenario);
        var sut = CreateWorker(provider, TimeProvider.System);

        await sut.RunBatchOnceAsync(CancellationToken.None);
        await sut.RunBatchOnceAsync(CancellationToken.None);

        Assert.Equal(2, scenario.DiscoveryInstanceIds.Distinct().Count());
        Assert.Equal(2, scenario.LeaseOperationInstanceIds.Distinct().Count());
        Assert.Equal(2, scenario.ProcessorInstanceIds.Distinct().Count());
        Assert.Equal(2, scenario.DiscoveryDisposals);
        Assert.Equal(2, scenario.LeaseOperationDisposals);
        Assert.Equal(2, scenario.ProcessorDisposals);
    }

    [Theory]
    [InlineData(AIAnalysisJobLeaseOutcome.Recovered)]
    [InlineData(AIAnalysisJobLeaseOutcome.LostRace)]
    [InlineData(AIAnalysisJobLeaseOutcome.NotFound)]
    [InlineData(AIAnalysisJobLeaseOutcome.NotEligible)]
    public async Task RunBatchOnceAsync_NonClaimOutcome_DoesNotInvokeProcessor(
        AIAnalysisJobLeaseOutcome leaseOutcome)
    {
        var scenario = new WorkerScenario([WorkItem(30, Guid.NewGuid())])
        {
            LeaseOutcome = leaseOutcome
        };
        await using var provider = BuildProvider(scenario);
        var sut = CreateWorker(provider, TimeProvider.System);

        await sut.RunBatchOnceAsync(CancellationToken.None);

        Assert.Empty(scenario.ProcessorInstanceIds);
        Assert.Equal(0, scenario.ProcessorDisposals);
    }

    [Fact]
    public async Task RunBatchOnceAsync_ProcessorFailure_DisposesScopeAndContinuesNextTenant()
    {
        var centerA = Guid.NewGuid();
        var centerB = Guid.NewGuid();
        var scenario = new WorkerScenario(
            [WorkItem(31, centerA), WorkItem(32, centerB)])
        {
            ThrowDuringProcessingJobId = 31
        };
        await using var provider = BuildProvider(scenario);
        var sut = CreateWorker(provider, TimeProvider.System);

        var result = await sut.RunBatchOnceAsync(CancellationToken.None);

        Assert.Equal(2, result.ClaimedCount);
        Assert.Equal(1, result.FallbackCompletedCount);
        Assert.Equal(1, result.ExceptionCount);
        Assert.Equal([centerA, centerB], scenario.ProcessorObservedCenters);
        Assert.Equal(2, scenario.ProcessorDisposals);
        Assert.All(scenario.TenantResolvedAtProcessorDisposal, Assert.False);
    }

    [Fact]
    public async Task RunBatchOnceAsync_CancellationInsideCandidate_DisposesTenantJobAndBatchScopes()
    {
        using var cancellation = new CancellationTokenSource();
        var scenario = new WorkerScenario([WorkItem(4, Guid.NewGuid())])
        {
            CancelOnJobId = 4,
            CancellationSource = cancellation
        };
        await using var provider = BuildProvider(scenario);
        var sut = CreateWorker(provider, TimeProvider.System);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.RunBatchOnceAsync(cancellation.Token));

        Assert.Equal(1, scenario.LeaseOperationDisposals);
        Assert.Single(scenario.TenantResolvedAtLeaseDisposal);
        Assert.False(scenario.TenantResolvedAtLeaseDisposal[0]);
        Assert.Equal(1, scenario.DiscoveryDisposals);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyBatch_UsesConfiguredCancellationAwareDelay()
    {
        var scenario = new WorkerScenario([]);
        await using var provider = BuildProvider(scenario);
        var timeProvider = new RecordingTimeProvider();
        var options = new AIAnalysisJobWorkerOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(17),
            LeaseDuration = TimeSpan.FromMinutes(1),
            BatchSize = 2,
            PerCenterBatchSize = 1
        };
        var sut = new AIAnalysisJobBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            timeProvider,
            options,
            new AIAnalysisJobWorkerIdentity("worker-test"),
            NullLogger<AIAnalysisJobBackgroundService>.Instance);

        await sut.StartAsync(CancellationToken.None);
        var dueTime = await timeProvider.TimerCreated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sut.StopAsync(CancellationToken.None);

        Assert.Equal(options.PollInterval, dueTime);
        Assert.True(scenario.DiscoveryDisposals >= 1);
    }

    [Fact]
    public async Task ExecuteAsync_DiscoveryException_StillUsesConfiguredDelay()
    {
        var scenario = new WorkerScenario([])
        {
            ThrowDuringDiscovery = true
        };
        await using var provider = BuildProvider(scenario);
        var timeProvider = new RecordingTimeProvider();
        var options = new AIAnalysisJobWorkerOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(23),
            LeaseDuration = TimeSpan.FromMinutes(1),
            BatchSize = 2,
            PerCenterBatchSize = 1
        };
        var sut = new AIAnalysisJobBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            timeProvider,
            options,
            new AIAnalysisJobWorkerIdentity("worker-test"),
            NullLogger<AIAnalysisJobBackgroundService>.Instance);

        await sut.StartAsync(CancellationToken.None);
        var dueTime = await timeProvider.TimerCreated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await sut.StopAsync(CancellationToken.None);

        Assert.Equal(options.PollInterval, dueTime);
        Assert.True(scenario.DiscoveryDisposals >= 1);
    }

    private static ServiceProvider BuildProvider(WorkerScenario scenario)
    {
        var services = new ServiceCollection();
        services.AddIdentityAndTenancy();
        services.AddSingleton(scenario);
        services.AddScoped<IAIAnalysisJobCandidateDiscovery, RecordingDiscovery>();
        services.AddScoped<IAIAnalysisJobLeaseOperation, RecordingLeaseOperation>();
        services.AddScoped<IAIAnalysisJobProcessor, RecordingProcessor>();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static AIAnalysisJobBackgroundService CreateWorker(
        ServiceProvider provider,
        TimeProvider timeProvider) => new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            timeProvider,
            new AIAnalysisJobWorkerOptions
            {
                PollInterval = TimeSpan.FromMilliseconds(10),
                LeaseDuration = TimeSpan.FromMinutes(1),
                BatchSize = 10,
                PerCenterBatchSize = 5
            },
            new AIAnalysisJobWorkerIdentity("worker-test"),
            NullLogger<AIAnalysisJobBackgroundService>.Instance);

    private static AIAnalysisJobWorkItem WorkItem(ulong id, Guid centerId) => new(
        id,
        centerId,
        $"correlation-{id}",
        AIAnalysisJobWorkKind.Claim,
        new DateTime(2026, 8, 14, 8, 0, 0, DateTimeKind.Utc));

    private sealed class WorkerScenario(IReadOnlyList<AIAnalysisJobWorkItem> workItems)
    {
        private readonly object _gate = new();

        public IReadOnlyList<AIAnalysisJobWorkItem> WorkItems { get; } = workItems;
        public ulong? ThrowOnJobId { get; init; }
        public ulong? CancelOnJobId { get; init; }
        public CancellationTokenSource? CancellationSource { get; init; }
        public bool ThrowDuringDiscovery { get; init; }
        public AIAnalysisJobLeaseOutcome LeaseOutcome { get; init; } =
            AIAnalysisJobLeaseOutcome.Claimed;
        public AIAnalysisJobProcessingOutcome ProcessingOutcome { get; init; } =
            AIAnalysisJobProcessingOutcome.FallbackCompleted;
        public ulong? ThrowDuringProcessingJobId { get; init; }
        public List<Guid> ObservedCenters { get; } = [];
        public List<Guid?> ObservedUserIds { get; } = [];
        public List<Guid> ProcessorObservedCenters { get; } = [];
        public List<Guid?> ProcessorObservedUserIds { get; } = [];
        public List<Guid> DiscoveryInstanceIds { get; } = [];
        public List<Guid> LeaseOperationInstanceIds { get; } = [];
        public List<Guid> ProcessorInstanceIds { get; } = [];
        public List<bool> TenantResolvedAtLeaseDisposal { get; } = [];
        public List<bool> TenantResolvedAtProcessorDisposal { get; } = [];
        public int DiscoveryDisposals { get; private set; }
        public int LeaseOperationDisposals { get; private set; }
        public int ProcessorDisposals { get; private set; }

        public void RecordDiscovery(Guid instanceId)
        {
            lock (_gate)
            {
                DiscoveryInstanceIds.Add(instanceId);
            }
        }

        public void RecordLeaseExecution(Guid instanceId, ITenantContext tenantContext)
        {
            lock (_gate)
            {
                LeaseOperationInstanceIds.Add(instanceId);
                ObservedCenters.Add(tenantContext.CenterId!.Value);
                ObservedUserIds.Add(tenantContext.UserId);
            }
        }

        public void RecordDiscoveryDisposal()
        {
            lock (_gate)
            {
                DiscoveryDisposals++;
            }
        }

        public void RecordProcessorExecution(Guid instanceId, ITenantContext tenantContext)
        {
            lock (_gate)
            {
                ProcessorInstanceIds.Add(instanceId);
                ProcessorObservedCenters.Add(tenantContext.CenterId!.Value);
                ProcessorObservedUserIds.Add(tenantContext.UserId);
            }
        }

        public void RecordLeaseDisposal(ITenantContext tenantContext)
        {
            lock (_gate)
            {
                LeaseOperationDisposals++;
                TenantResolvedAtLeaseDisposal.Add(tenantContext.IsResolved);
            }
        }

        public void RecordProcessorDisposal(ITenantContext tenantContext)
        {
            lock (_gate)
            {
                ProcessorDisposals++;
                TenantResolvedAtProcessorDisposal.Add(tenantContext.IsResolved);
            }
        }
    }

    private sealed class RecordingDiscovery : IAIAnalysisJobCandidateDiscovery, IDisposable
    {
        private readonly WorkerScenario _scenario;
        private readonly Guid _instanceId = Guid.NewGuid();

        public RecordingDiscovery(WorkerScenario scenario)
        {
            _scenario = scenario;
            _scenario.RecordDiscovery(_instanceId);
        }

        public Task<AIAnalysisJobDiscoveryResult> DiscoverAsync(
            int perCenterBatchSize,
            int totalBatchSize,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_scenario.ThrowDuringDiscovery)
            {
                throw new InvalidOperationException("Deliberate discovery exception.");
            }

            return Task.FromResult(new AIAnalysisJobDiscoveryResult(_scenario.WorkItems));
        }

        public void Dispose() => _scenario.RecordDiscoveryDisposal();
    }

    private sealed class RecordingLeaseOperation : IAIAnalysisJobLeaseOperation, IDisposable
    {
        private readonly WorkerScenario _scenario;
        private readonly ITenantContext _tenantContext;
        private readonly Guid _instanceId = Guid.NewGuid();

        public RecordingLeaseOperation(
            WorkerScenario scenario,
            ITenantContext tenantContext)
        {
            _scenario = scenario;
            _tenantContext = tenantContext;
        }

        public Task<AIAnalysisJobLeaseResult> ExecuteAsync(
            AIAnalysisJobWorkItem workItem,
            string workerId,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            _scenario.RecordLeaseExecution(_instanceId, _tenantContext);
            if (_scenario.CancelOnJobId == workItem.AnalysisJobId)
            {
                _scenario.CancellationSource!.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (_scenario.ThrowOnJobId == workItem.AnalysisJobId)
            {
                throw new InvalidOperationException("Deliberate test exception.");
            }

            return Task.FromResult(new AIAnalysisJobLeaseResult(
                workItem.AnalysisJobId,
                workItem.CenterId,
                _scenario.LeaseOutcome));
        }

        public void Dispose() => _scenario.RecordLeaseDisposal(_tenantContext);
    }

    private sealed class RecordingProcessor : IAIAnalysisJobProcessor, IDisposable
    {
        private readonly WorkerScenario _scenario;
        private readonly ITenantContext _tenantContext;
        private readonly Guid _instanceId = Guid.NewGuid();

        public RecordingProcessor(
            WorkerScenario scenario,
            ITenantContext tenantContext)
        {
            _scenario = scenario;
            _tenantContext = tenantContext;
        }

        public Task<AIAnalysisJobProcessingResult> ExecuteAsync(
            ulong analysisJobId,
            string workerId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _scenario.RecordProcessorExecution(_instanceId, _tenantContext);
            if (_scenario.ThrowDuringProcessingJobId == analysisJobId)
            {
                throw new InvalidOperationException("Deliberate processor failure.");
            }

            return Task.FromResult(new AIAnalysisJobProcessingResult(
                analysisJobId,
                analysisJobId,
                _scenario.ProcessingOutcome));
        }

        public void Dispose() => _scenario.RecordProcessorDisposal(_tenantContext);
    }

    private sealed class RecordingTimeProvider : TimeProvider
    {
        public TaskCompletionSource<TimeSpan> TimerCreated { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            TimerCreated.TrySetResult(dueTime);
            return new NoopTimer();
        }

        private sealed class NoopTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
