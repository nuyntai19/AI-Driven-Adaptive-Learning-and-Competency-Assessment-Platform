using EduTwin.API.AssessmentAndReasoning.Background;
using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.IdentityAndTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Background;

public sealed class AIAnalysisJobBackgroundServiceTests
{
    [Theory]
    [InlineData(AIAnalysisJobProcessingOutcome.Completed, 1, 0, 0, 0, 0, 0)]
    [InlineData(AIAnalysisJobProcessingOutcome.RetryScheduled, 0, 1, 0, 0, 0, 0)]
    [InlineData(AIAnalysisJobProcessingOutcome.FallbackCompleted, 0, 0, 1, 0, 0, 0)]
    [InlineData(AIAnalysisJobProcessingOutcome.AlreadyTerminal, 0, 0, 0, 1, 0, 0)]
    [InlineData(AIAnalysisJobProcessingOutcome.NotFound, 0, 0, 0, 0, 1, 0)]
    [InlineData(AIAnalysisJobProcessingOutcome.NotEligible, 0, 0, 0, 0, 1, 0)]
    [InlineData(AIAnalysisJobProcessingOutcome.LostRace, 0, 0, 0, 0, 0, 1)]
    public async Task RunBatchOnceAsync_MapsEveryProcessingOutcomeToExactlyOneCounter(
        AIAnalysisJobProcessingOutcome processingOutcome,
        int completed,
        int retryScheduled,
        int fallbackCompleted,
        int alreadyTerminal,
        int processingStale,
        int processingLostRace)
    {
        var scenario = new WorkerScenario([WorkItem(99, Guid.NewGuid())])
        {
            ProcessingOutcome = processingOutcome
        };
        await using var provider = BuildProvider(scenario);

        var result = await CreateWorker(provider, TimeProvider.System)
            .RunBatchOnceAsync(CancellationToken.None);

        Assert.Equal(1, result.ClaimedCount);
        Assert.Equal(completed, result.CompletedCount);
        Assert.Equal(retryScheduled, result.RetryScheduledCount);
        Assert.Equal(fallbackCompleted, result.FallbackCompletedCount);
        Assert.Equal(alreadyTerminal, result.AlreadyTerminalCount);
        Assert.Equal(processingStale, result.ProcessingStaleCount);
        Assert.Equal(processingLostRace, result.ProcessingLostRaceCount);
        Assert.Equal(0, result.ExceptionCount);

        var processingLog = Assert.Single(
            scenario.LoggerProvider.Entries,
            entry => entry.Template.Contains("processing outcome", StringComparison.Ordinal));
        Assert.Equal(processingOutcome, processingLog.State["Outcome"]);
        Assert.Equal(
            processingOutcome is AIAnalysisJobProcessingOutcome.RetryScheduled
                or AIAnalysisJobProcessingOutcome.FallbackCompleted
                    ? "AI_ANALYSIS_ATTEMPT_FAILED"
                    : null,
            processingLog.State["ErrorCode"]);
        AssertStructuredIdentity(processingLog, scenario.WorkItems[0]);
    }

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
        Assert.Empty(scenario.LoggerProvider.CaptureCurrentScopes());

        var candidateError = Assert.Single(
            scenario.LoggerProvider.Entries,
            entry => entry.Template.Contains("candidate failed", StringComparison.Ordinal));
        Assert.Null(candidateError.Exception);
        Assert.Equal(nameof(InvalidOperationException), candidateError.State["ExceptionType"]);
        Assert.DoesNotContain(
            "Deliberate test exception.",
            candidateError.Message,
            StringComparison.Ordinal);
        AssertJobScope(candidateError, scenario.WorkItems[0]);

        var leaseEntries = scenario.LoggerProvider.Entries
            .Where(entry => entry.Category.EndsWith(nameof(RecordingLeaseOperation), StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, leaseEntries.Length);
        AssertJobScope(leaseEntries[0], scenario.WorkItems[0]);
        AssertJobScope(leaseEntries[1], scenario.WorkItems[1]);
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

    [Fact]
    public async Task RunBatchOnceAsync_PerCandidateScopeSpansLeaseAndProcessingAndDisposesAfterSuccess()
    {
        var workItem = WorkItem(300, Guid.NewGuid());
        var scenario = new WorkerScenario([workItem])
        {
            ProcessingOutcome = AIAnalysisJobProcessingOutcome.Completed
        };
        await using var provider = BuildProvider(scenario);

        await CreateWorker(provider, TimeProvider.System)
            .RunBatchOnceAsync(CancellationToken.None);

        var leaseEntry = Assert.Single(
            scenario.LoggerProvider.Entries,
            entry => entry.Category.EndsWith(nameof(RecordingLeaseOperation), StringComparison.Ordinal));
        var processingEntry = Assert.Single(
            scenario.LoggerProvider.Entries,
            entry => entry.Category.EndsWith(nameof(RecordingProcessor), StringComparison.Ordinal));
        AssertJobScope(leaseEntry, workItem);
        AssertJobScope(processingEntry, workItem);
        var leaseOutcomeEntry = Assert.Single(
            scenario.LoggerProvider.Entries,
            entry => entry.Template.Contains("lease outcome", StringComparison.Ordinal));
        Assert.Equal(AIAnalysisJobLeaseOutcome.Claimed, leaseOutcomeEntry.State["Outcome"]);
        AssertStructuredIdentity(leaseOutcomeEntry, workItem);
        Assert.Empty(scenario.LoggerProvider.CaptureCurrentScopes());
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
        Assert.Empty(scenario.LoggerProvider.CaptureCurrentScopes());
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
        Assert.Empty(scenario.LoggerProvider.CaptureCurrentScopes());
        var leaseEntry = Assert.Single(
            scenario.LoggerProvider.Entries,
            entry => entry.Category.EndsWith(nameof(RecordingLeaseOperation), StringComparison.Ordinal));
        AssertJobScope(leaseEntry, scenario.WorkItems[0]);
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
            provider.GetRequiredService<ILogger<AIAnalysisJobBackgroundService>>());

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
            provider.GetRequiredService<ILogger<AIAnalysisJobBackgroundService>>());

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
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(scenario.LoggerProvider);
        });
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
            provider.GetRequiredService<ILogger<AIAnalysisJobBackgroundService>>());

    private static AIAnalysisJobWorkItem WorkItem(ulong id, Guid centerId) => new(
        id,
        id + 1000,
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
        public CapturingLoggerProvider LoggerProvider { get; } = new();

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
        private readonly ILogger<RecordingLeaseOperation> _logger;
        private readonly Guid _instanceId = Guid.NewGuid();

        public RecordingLeaseOperation(
            WorkerScenario scenario,
            ITenantContext tenantContext,
            ILogger<RecordingLeaseOperation> logger)
        {
            _scenario = scenario;
            _tenantContext = tenantContext;
            _logger = logger;
        }

        public Task<AIAnalysisJobLeaseResult> ExecuteAsync(
            AIAnalysisJobWorkItem workItem,
            string workerId,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            _scenario.RecordLeaseExecution(_instanceId, _tenantContext);
            _logger.LogDebug("Recording lease operation executed.");
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
        private readonly ILogger<RecordingProcessor> _logger;
        private readonly Guid _instanceId = Guid.NewGuid();

        public RecordingProcessor(
            WorkerScenario scenario,
            ITenantContext tenantContext,
            ILogger<RecordingProcessor> logger)
        {
            _scenario = scenario;
            _tenantContext = tenantContext;
            _logger = logger;
        }

        public Task<AIAnalysisJobProcessingResult> ExecuteAsync(
            ulong analysisJobId,
            string workerId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _scenario.RecordProcessorExecution(_instanceId, _tenantContext);
            _logger.LogDebug("Recording processor executed.");
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

    private static void AssertStructuredIdentity(
        CapturedLog entry,
        AIAnalysisJobWorkItem workItem)
    {
        Assert.Equal(workItem.AnalysisJobId, entry.State["AnalysisJobId"]);
        Assert.Equal(workItem.AttemptId, entry.State["AttemptId"]);
        Assert.Equal(workItem.CenterId, entry.State["CenterId"]);
        Assert.Equal(workItem.CorrelationId, entry.State["CorrelationId"]);
        Assert.Equal("worker-test", entry.State["WorkerId"]);
        AssertJobScope(entry, workItem);
    }

    private static void AssertJobScope(CapturedLog entry, AIAnalysisJobWorkItem workItem)
    {
        var scope = Assert.Single(entry.Scopes);
        Assert.Equal(5, scope.Count);
        Assert.Equal(workItem.CorrelationId, scope["CorrelationId"]);
        Assert.Equal(workItem.CenterId, scope["CenterId"]);
        Assert.Equal(workItem.AttemptId, scope["AttemptId"]);
        Assert.Equal(workItem.AnalysisJobId, scope["AnalysisJobId"]);
        Assert.Equal("worker-test", scope["WorkerId"]);
    }

    private sealed record CapturedLog(
        LogLevel Level,
        string Category,
        string Template,
        string Message,
        IReadOnlyDictionary<string, object?> State,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> Scopes,
        Exception? Exception);

    private sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly object _gate = new();
        private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

        public List<CapturedLog> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) =>
            _scopeProvider = scopeProvider;

        public IReadOnlyList<IReadOnlyDictionary<string, object?>> CaptureCurrentScopes() =>
            CaptureScopes();

        public void Dispose()
        {
        }

        private void Record<TState>(
            LogLevel logLevel,
            string category,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var structuredState = ToDictionary(state);
            var template = structuredState.TryGetValue("{OriginalFormat}", out var value)
                ? value?.ToString() ?? string.Empty
                : string.Empty;
            var entry = new CapturedLog(
                logLevel,
                category,
                template,
                formatter(state, exception),
                structuredState,
                CaptureScopes(),
                exception);
            lock (_gate)
            {
                Entries.Add(entry);
            }
        }

        private IReadOnlyList<IReadOnlyDictionary<string, object?>> CaptureScopes()
        {
            var scopes = new List<IReadOnlyDictionary<string, object?>>();
            _scopeProvider.ForEachScope(
                (scope, collection) => collection.Add(ToDictionary(scope)),
                scopes);
            return scopes;
        }

        private static IReadOnlyDictionary<string, object?> ToDictionary<TState>(TState state) =>
            state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>();

        private sealed class CapturingLogger(
            CapturingLoggerProvider provider,
            string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
                provider._scopeProvider.Push(state);

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                provider.Record(logLevel, category, state, exception, formatter);
        }
    }
}
