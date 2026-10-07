using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.IdentityAndTenancy;
using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.API.AssessmentAndReasoning.Background;

public sealed class AIAnalysisJobBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly AIAnalysisJobWorkerOptions _options;
    private readonly AIAnalysisJobWorkerIdentity _identity;
    private readonly ILogger<AIAnalysisJobBackgroundService> _logger;

    public AIAnalysisJobBackgroundService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        AIAnalysisJobWorkerOptions options,
        AIAnalysisJobWorkerIdentity identity,
        ILogger<AIAnalysisJobBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _options = options;
        _identity = identity;
        _logger = logger;
        _options.Validate();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var batch = await RunBatchOnceAsync(stoppingToken);
                // Drain newly-unblocked checkpoints without paying a poll interval per question.
                // No progress (quota wait/empty queue/failure) still backs off below.
                if (batch.CompletedCount > 0 || batch.FallbackCompletedCount > 0 || batch.RecoveredCount > 0)
                    continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    "AI analysis job batch failed for worker {WorkerId} with {ExceptionType}.",
                    _identity.Value,
                    exception.GetType().Name);
            }

            try
            {
                await Task.Delay(_options.PollInterval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task<AIAnalysisJobBackgroundBatchResult> RunBatchOnceAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var batchScope = _scopeFactory.CreateAsyncScope();
        var discovery = batchScope.ServiceProvider
            .GetRequiredService<IAIAnalysisJobCandidateDiscovery>();
        var discoveryResult = await discovery.DiscoverAsync(
            _options.PerCenterBatchSize,
            _options.BatchSize,
            cancellationToken);

        var claimed = 0;
        var recovered = 0;
        var stale = 0;
        var lostRace = 0;
        var completed = 0;
        var retryScheduled = 0;
        var fallbackCompleted = 0;
        var alreadyTerminal = 0;
        var processingStale = 0;
        var processingLostRace = 0;
        var exceptions = 0;

        // Round-robin centers inside the bounded batch; do not let one large exam take every slot.
        var centerQueues = discoveryResult.WorkItems.GroupBy(x => x.CenterId)
            .Select(g => new Queue<AIAnalysisJobWorkItem>(g)).ToArray();
        var fairItems = new List<AIAnalysisJobWorkItem>();
        while (centerQueues.Any(q => q.Count > 0))
            foreach (var queue in centerQueues) if (queue.TryDequeue(out var item)) fairItems.Add(item);

        await Parallel.ForEachAsync(fairItems,
            new ParallelOptions { MaxDegreeOfParallelism = _options.MaxConcurrentJobs, CancellationToken = cancellationToken },
            async (workItem, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var loggingScope = _logger.BeginScope(
                new Dictionary<string, object?>
                {
                    ["CorrelationId"] = workItem.CorrelationId,
                    ["CenterId"] = workItem.CenterId,
                    ["AttemptId"] = workItem.AttemptId,
                    ["AnalysisJobId"] = workItem.AnalysisJobId,
                    ["WorkerId"] = _identity.Value
                });

            try
            {
                AIAnalysisJobLeaseResult result;
                await using (var jobScope = _scopeFactory.CreateAsyncScope())
                {
                    var tenantScopeFactory = jobScope.ServiceProvider
                        .GetRequiredService<IBackgroundTenantScopeFactory>();
                    using var tenantScope = tenantScopeFactory.BeginScope(workItem.CenterId);
                    var leaseOperation = jobScope.ServiceProvider
                        .GetRequiredService<IAIAnalysisJobLeaseOperation>();

                    result = await leaseOperation.ExecuteAsync(
                        workItem,
                        _identity.Value,
                        _options.LeaseDuration,
                        cancellationToken);
                }

                switch (result.Outcome)
                {
                    case AIAnalysisJobLeaseOutcome.Claimed:
                        Interlocked.Increment(ref claimed);
                        AIProcessingMetrics.QueueWait.Record(Math.Max(0, (_timeProvider.GetUtcNow().UtcDateTime - workItem.EligibleAt).TotalMilliseconds));
                        var processingResult = await ProcessClaimedAsync(
                            workItem,
                            cancellationToken);
                        switch (processingResult.Outcome)
                        {
                            case AIAnalysisJobProcessingOutcome.Completed:
                                Interlocked.Increment(ref completed);
                                break;
                            case AIAnalysisJobProcessingOutcome.RetryScheduled:
                                Interlocked.Increment(ref retryScheduled);
                                break;
                            case AIAnalysisJobProcessingOutcome.FallbackCompleted:
                                Interlocked.Increment(ref fallbackCompleted);
                                break;
                            case AIAnalysisJobProcessingOutcome.AlreadyTerminal:
                                Interlocked.Increment(ref alreadyTerminal);
                                break;
                            case AIAnalysisJobProcessingOutcome.NotFound:
                            case AIAnalysisJobProcessingOutcome.NotEligible:
                                Interlocked.Increment(ref processingStale);
                                break;
                            case AIAnalysisJobProcessingOutcome.LostRace:
                                Interlocked.Increment(ref processingLostRace);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(
                                    nameof(processingResult),
                                    processingResult.Outcome,
                                    null);
                        }

                        var errorCode = processingResult.Outcome switch
                        {
                            AIAnalysisJobProcessingOutcome.RetryScheduled => "AI_PROCESSING_DEFERRED",
                            AIAnalysisJobProcessingOutcome.FallbackCompleted => "AI_ANALYSIS_ATTEMPT_FAILED",
                            _ => null
                        };
                        _logger.LogDebug(
                            "AI analysis job processing outcome {Outcome} with error code {ErrorCode} for job {AnalysisJobId}, attempt {AttemptId}, center {CenterId}, correlation {CorrelationId}, worker {WorkerId}.",
                            processingResult.Outcome,
                            errorCode,
                            workItem.AnalysisJobId,
                            workItem.AttemptId,
                            workItem.CenterId,
                            workItem.CorrelationId,
                            _identity.Value);
                        break;
                    case AIAnalysisJobLeaseOutcome.Recovered:
                        Interlocked.Increment(ref recovered);
                        break;
                    case AIAnalysisJobLeaseOutcome.LostRace:
                        Interlocked.Increment(ref lostRace);
                        break;
                    case AIAnalysisJobLeaseOutcome.NotFound:
                    case AIAnalysisJobLeaseOutcome.NotEligible:
                        Interlocked.Increment(ref stale);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, null);
                }

                _logger.LogDebug(
                    "AI analysis job lease outcome {Outcome} for job {AnalysisJobId}, attempt {AttemptId}, center {CenterId}, correlation {CorrelationId}, worker {WorkerId}.",
                    result.Outcome,
                    workItem.AnalysisJobId,
                    workItem.AttemptId,
                    workItem.CenterId,
                    workItem.CorrelationId,
                    _identity.Value);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Interlocked.Increment(ref exceptions);
                _logger.LogError(
                    "AI analysis job candidate failed for job {AnalysisJobId}, attempt {AttemptId}, center {CenterId}, correlation {CorrelationId}, worker {WorkerId} with {ExceptionType}.",
                    workItem.AnalysisJobId,
                    workItem.AttemptId,
                    workItem.CenterId,
                    workItem.CorrelationId,
                    _identity.Value,
                    exception.GetType().Name);
            }
        });

        return new AIAnalysisJobBackgroundBatchResult(
            discoveryResult.WorkItems.Count,
            claimed,
            recovered,
            stale,
            lostRace,
            completed,
            retryScheduled,
            fallbackCompleted,
            alreadyTerminal,
            processingStale,
            processingLostRace,
            exceptions);
    }

    private async Task<AIAnalysisJobProcessingResult> ProcessClaimedAsync(
        AIAnalysisJobWorkItem workItem,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var processingScope = _scopeFactory.CreateAsyncScope();
        var tenantScopeFactory = processingScope.ServiceProvider
            .GetRequiredService<IBackgroundTenantScopeFactory>();
        using var tenantScope = tenantScopeFactory.BeginScope(workItem.CenterId);
        var processor = processingScope.ServiceProvider
            .GetRequiredService<IAIAnalysisJobProcessor>();

        return await processor.ExecuteAsync(
            workItem.AnalysisJobId,
            _identity.Value,
            cancellationToken);
    }
}
