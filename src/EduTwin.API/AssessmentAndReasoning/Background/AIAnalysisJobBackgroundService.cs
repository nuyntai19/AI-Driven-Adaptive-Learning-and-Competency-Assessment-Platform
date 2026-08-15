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
                await RunBatchOnceAsync(stoppingToken);
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
        var fallbackCompleted = 0;
        var alreadyTerminal = 0;
        var processingStale = 0;
        var processingLostRace = 0;
        var exceptions = 0;

        foreach (var workItem in discoveryResult.WorkItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                        claimed++;
                        var processingResult = await ProcessClaimedAsync(
                            workItem,
                            cancellationToken);
                        switch (processingResult.Outcome)
                        {
                            case AIAnalysisJobProcessingOutcome.FallbackCompleted:
                                fallbackCompleted++;
                                break;
                            case AIAnalysisJobProcessingOutcome.AlreadyTerminal:
                                alreadyTerminal++;
                                break;
                            case AIAnalysisJobProcessingOutcome.NotFound:
                            case AIAnalysisJobProcessingOutcome.NotEligible:
                                processingStale++;
                                break;
                            case AIAnalysisJobProcessingOutcome.LostRace:
                                processingLostRace++;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(
                                    nameof(processingResult),
                                    processingResult.Outcome,
                                    null);
                        }

                        _logger.LogDebug(
                            "AI analysis job processing outcome {Outcome} for job {AnalysisJobId}, center {CenterId}, correlation {CorrelationId}, worker {WorkerId}.",
                            processingResult.Outcome,
                            workItem.AnalysisJobId,
                            workItem.CenterId,
                            workItem.CorrelationId,
                            _identity.Value);
                        break;
                    case AIAnalysisJobLeaseOutcome.Recovered:
                        recovered++;
                        break;
                    case AIAnalysisJobLeaseOutcome.LostRace:
                        lostRace++;
                        break;
                    case AIAnalysisJobLeaseOutcome.NotFound:
                    case AIAnalysisJobLeaseOutcome.NotEligible:
                        stale++;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, null);
                }

                _logger.LogDebug(
                    "AI analysis job lease outcome {Outcome} for job {AnalysisJobId}, center {CenterId}, correlation {CorrelationId}, worker {WorkerId}.",
                    result.Outcome,
                    workItem.AnalysisJobId,
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
                exceptions++;
                _logger.LogError(
                    "AI analysis job candidate failed for job {AnalysisJobId}, center {CenterId}, correlation {CorrelationId}, worker {WorkerId} with {ExceptionType}.",
                    workItem.AnalysisJobId,
                    workItem.CenterId,
                    workItem.CorrelationId,
                    _identity.Value,
                    exception.GetType().Name);
            }
        }

        return new AIAnalysisJobBackgroundBatchResult(
            discoveryResult.WorkItems.Count,
            claimed,
            recovered,
            stale,
            lostRace,
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
