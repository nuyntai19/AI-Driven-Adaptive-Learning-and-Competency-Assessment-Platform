using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.AssessmentAndReasoning.Feedback;
using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.DigitalTwin.Orchestration;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Recommendations;
using EduTwin.BLL.Assignments;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EduTwin.BLL.AssessmentAndReasoning.Processing;

public sealed class AIAnalysisJobProcessor : IAIAnalysisJobProcessor
{
    private const string AnalysisFailureCode = "AI_ANALYSIS_ATTEMPT_FAILED";
    private const string AnalysisFailureMessage = "AI analysis attempt failed.";
    private const string AttachmentStorageUnavailableCode = "AttachmentStorageUnavailable";
    private const string AttachmentStorageUnavailableMessage = "Attempt attachment storage is temporarily unavailable.";

    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IAIService _aiService;
    private readonly IAIAnalysisRequestFactory _requestFactory;
    private readonly IAIReasoningAnalysisBuilder _analysisBuilder;
    private readonly IRuleBasedFallbackBuilder _fallbackBuilder;
    private readonly IAIAnalysisJobStateMachine _stateMachine;
    private readonly IEvidenceGate _evidenceGate;
    private readonly IEvidenceAssessmentFactory _evidenceAssessmentFactory;
    private readonly IEvidenceConsistencyChecker _consistencyChecker;
    private readonly ITwinCompletionOrchestrator _twinCompletionOrchestrator;
    private readonly IRecommendationEngine? _recommendationEngine;
    private readonly IAttemptAttachmentStorage? _attachmentStorage;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AIAnalysisJobProcessor> _logger;
    private readonly IOverallAssignmentCommentWorkflow? _overallCommentWorkflow;
    private readonly IAIAnalysisCheckpointStore? _checkpointStore;
    private readonly IAIStudentPostProcessingQueue? _postProcessing;

    public AIAnalysisJobProcessor(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IAIService aiService,
        IAIAnalysisRequestFactory requestFactory,
        IAIReasoningAnalysisBuilder analysisBuilder,
        IRuleBasedFallbackBuilder fallbackBuilder,
        IAIAnalysisJobStateMachine stateMachine,
        IEvidenceGate evidenceGate,
        IEvidenceAssessmentFactory evidenceAssessmentFactory,
        TimeProvider timeProvider,
        ITwinCompletionOrchestrator? twinCompletionOrchestrator = null,
        IEvidenceConsistencyChecker? consistencyChecker = null,
        IRecommendationEngine? recommendationEngine = null,
        IAttemptAttachmentStorage? attachmentStorage = null,
        ILogger<AIAnalysisJobProcessor>? logger = null,
        IOverallAssignmentCommentWorkflow? overallCommentWorkflow = null,
        IAIAnalysisCheckpointStore? checkpointStore = null,
        IAIStudentPostProcessingQueue? postProcessing = null)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _aiService = aiService;
        _requestFactory = requestFactory;
        _analysisBuilder = analysisBuilder;
        _fallbackBuilder = fallbackBuilder;
        _stateMachine = stateMachine;
        _evidenceGate = evidenceGate;
        _evidenceAssessmentFactory = evidenceAssessmentFactory;
        _consistencyChecker = consistencyChecker ?? new EvidenceConsistencyChecker();
        _timeProvider = timeProvider;
        _twinCompletionOrchestrator = twinCompletionOrchestrator ?? new TwinCompletionOrchestrator(
            dbContext,
            evidenceGate,
            evidenceAssessmentFactory,
            new BehaviorTwinUpdater(dbContext),
            new KnowledgeTwinUpdater(dbContext),
            new TwinUpdateHistoryWriter(dbContext),
            new StudentGoalRiskUpdater(dbContext),
            new StudentTwinUpdater(dbContext),
            _consistencyChecker);
        _recommendationEngine = recommendationEngine;
        _attachmentStorage = attachmentStorage;
        _logger = logger ?? NullLogger<AIAnalysisJobProcessor>.Instance;
        _overallCommentWorkflow = overallCommentWorkflow;
        _checkpointStore = checkpointStore;
        _postProcessing = postProcessing;
    }

    public async Task<AIAnalysisJobProcessingResult> ExecuteAsync(
        ulong analysisJobId,
        string workerId,
        CancellationToken cancellationToken)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var result = await ExecuteCoreAsync(analysisJobId, workerId, cancellationToken);
            AIProcessingMetrics.Outcomes.Add(1, new KeyValuePair<string, object?>("outcome", result.Outcome.ToString()));
            return result;
        }
        finally
        {
            AIProcessingMetrics.Duration.Record(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                new KeyValuePair<string, object?>("stage", "job"));
        }
    }

    private async Task<AIAnalysisJobProcessingResult> ExecuteCoreAsync(
        ulong analysisJobId,
        string workerId,
        CancellationToken cancellationToken)
    {
        if (analysisJobId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(analysisJobId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_tenantContext.IsResolved || _tenantContext.UserId is not null)
        {
            return Result(analysisJobId, null, AIAnalysisJobProcessingOutcome.NotFound);
        }

        var centerId = _tenantContext.CenterId!.Value;
        var initialJob = await _dbContext.AIAnalysisJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                job => job.CenterId == centerId
                    && job.AnalysisJobId == analysisJobId,
                cancellationToken);

        if (initialJob is null)
        {
            return Result(analysisJobId, null, AIAnalysisJobProcessingOutcome.NotFound);
        }

        if (IsTerminal(initialJob.Status))
        {
            return Result(
                analysisJobId,
                initialJob.AttemptId,
                AIAnalysisJobProcessingOutcome.AlreadyTerminal);
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        if (!HasValidLease(initialJob, workerId, utcNow))
        {
            return Result(
                analysisJobId,
                initialJob.AttemptId,
                AIAnalysisJobProcessingOutcome.NotEligible);
        }

        var initialAttempt = await _dbContext.Attempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                attempt => attempt.CenterId == centerId
                    && attempt.AttemptId == initialJob.AttemptId,
                cancellationToken);

        if (initialAttempt is null)
        {
            return Result(
                analysisJobId,
                initialJob.AttemptId,
                AIAnalysisJobProcessingOutcome.NotFound);
        }

        if (!CanComplete(initialAttempt.Status))
        {
            return Result(
                analysisJobId,
                initialAttempt.AttemptId,
                AIAnalysisJobProcessingOutcome.NotEligible);
        }

        if (await AnalysisBlocksProcessingAsync(initialAttempt, cancellationToken))
        {
            return Result(
                analysisJobId,
                initialAttempt.AttemptId,
                AIAnalysisJobProcessingOutcome.NotEligible);
        }

        var requestContext = await LoadRequestContextAsync(
            centerId,
            initialAttempt.QuestionId,
            cancellationToken);
        if (requestContext is null)
        {
            return await PersistAnalysisFailureAsync(
                initialJob,
                initialAttempt,
                null,
                workerId,
                cancellationToken);
        }

        utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        if (!HasValidLease(initialJob, workerId, utcNow))
        {
            return Result(
                analysisJobId,
                initialAttempt.AttemptId,
                AIAnalysisJobProcessingOutcome.NotEligible);
        }

        // A skipped question is telemetry, not an answer to be graded by an AI.
        // Complete it with the deterministic Vietnamese fallback so skip-rate is
        // recorded while mastery remains unchanged (zero-weight evidence).
        if (initialAttempt.Skipped)
        {
            var skippedAnalysis = _fallbackBuilder.Build(new RuleBasedFallbackInput(
                initialAttempt.CenterId,
                initialAttempt.AttemptId,
                initialAttempt.IsCorrect,
                initialAttempt.AwardedScore,
                true,
                initialAttempt.ReasoningLanguage,
                utcNow));

            return await PersistSuccessAsync(
                initialJob,
                initialAttempt,
                requestContext,
                skippedAnalysis,
                TwinEventSource.RuleFallback,
                workerId,
                cancellationToken);
        }

        ReasoningAnalysis analysis;
        AnalyzeReasoningResponse? response = null;
        string? fingerprint = null;
        var checkpointHit = false;
        var providerFailure = true;
        try
        {
            var imageParts = await LoadAttachmentImagePartsAsync(
                centerId,
                initialAttempt.AttemptId,
                cancellationToken);
            var request = _requestFactory.Create(
                initialAttempt,
                requestContext.Question,
                requestContext.AllowedNodes,
                imageParts);
            cancellationToken.ThrowIfCancellationRequested();
            fingerprint = AIAnalysisCheckpointStore.Fingerprint(request,
                (_aiService as IAIAnalysisProfile)?.AnalysisProfileVersion ?? AIAnalysisContract.SchemaVersion, requestContext.Question.RowVersion);
            if (_checkpointStore is not null)
            {
                providerFailure = false;
                response = await _checkpointStore.ReadAsync(centerId, initialAttempt.AttemptId, fingerprint, cancellationToken);
                providerFailure = true;
                if (response is not null)
                {
                    try { new AnalyzeReasoningResponseValidator().Validate(request, response); }
                    catch (AIAnalysisValidationException) { response = null; }
                }
            }
            checkpointHit = response is not null;
            if (response is not null) AIProcessingMetrics.CheckpointHits.Add(1);
            else
            {
                var providerStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    response = _aiService is IPartitionedAIService partitioned && initialAttempt.AssignmentId.HasValue
                        ? await partitioned.AnalyzeReasoningAsync(request,
                            new(centerId, initialAttempt.StudentId, initialAttempt.AssignmentId.Value), cancellationToken)
                        : await _aiService.AnalyzeReasoningAsync(request, cancellationToken);
                }
                finally { AIProcessingMetrics.Duration.Record(System.Diagnostics.Stopwatch.GetElapsedTime(providerStarted).TotalMilliseconds, new KeyValuePair<string, object?>("stage", "provider")); }
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (_checkpointStore is not null) new AnalyzeReasoningResponseValidator().Validate(request, response);

            var analysisUtcNow = _timeProvider.GetUtcNow().UtcDateTime;
            analysis = _analysisBuilder.Build(
                centerId,
                initialAttempt.AttemptId,
                response,
                analysisUtcNow,
                initialAttempt.IsCorrect,
                initialAttempt.ReasoningLanguage);
            if (_aiService is IAIAnalysisProvenance provenance)
            {
                analysis.FeedbackOrigin = provenance.ProviderName;
                analysis.Provider = provenance.ProviderName == "Groq" ? AnalysisProvider.Groq : AnalysisProvider.Gemini;
                analysis.ModelName = provenance.ModelName;
                analysis.AnalysisProfileVersion = (_aiService as IAIAnalysisProfile)?.AnalysisProfileVersion;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AIAnalysisDeferredException deferred)
        {
            return await PersistDeferralAsync(initialJob, initialAttempt, requestContext, workerId,
                deferred.RetryAfter, deferred.ErrorCode, cancellationToken);
        }
        catch (AttemptAttachmentStorageUnavailableException)
        {
            return await PersistStorageFailureAsync(
                initialJob,
                initialAttempt,
                requestContext,
                workerId,
                cancellationToken);
        }
        catch (Exception exception) when (providerFailure && exception is not AIAnalysisInfrastructureException)
        {
            return await PersistAnalysisFailureAsync(
                initialJob,
                initialAttempt,
                requestContext,
                workerId,
                cancellationToken);
        }

        // Save outside the provider-failure catch: a database outage must not be graded as an AI failure.
        if (_checkpointStore is not null && !checkpointHit && !await _checkpointStore.SaveAsync(initialJob, workerId, fingerprint!, response!,
            _timeProvider.GetUtcNow().UtcDateTime, cancellationToken))
            return Result(analysisJobId, initialAttempt.AttemptId, AIAnalysisJobProcessingOutcome.NotEligible);

        return await PersistSuccessAsync(
            initialJob,
            initialAttempt,
            requestContext,
            analysis,
            TwinEventSource.AIAnalysis,
            workerId,
            cancellationToken);
    }

    private async Task<AIAnalysisJobProcessingResult> PersistSuccessAsync(
        AIAnalysisJob initialJob,
        Attempt initialAttempt,
        RequestContext requestContext,
        ReasoningAnalysis analysis,
        TwinEventSource eventSource,
        string workerId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        var commitStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        Guid recommendationCenterId = default;
        Guid recommendationStudentId = default;
        Guid recommendationSubjectId = default;
        ulong recommendationAttemptId = default;
        DateTime recommendationTriggerAt = default;

        try
        {
            if (_checkpointStore is not null)
                await StudentLockHelper.AcquireStudentLockAsync(_dbContext, initialJob.CenterId, initialAttempt.StudentId, cancellationToken);
            var reload = await ReloadAndRevalidateAsync(
                initialJob,
                initialAttempt,
                requestContext,
                workerId,
                cancellationToken);
            if (reload.Outcome.HasValue)
            {
                return await RollbackResultAsync(
                    transaction,
                    Result(initialJob.AnalysisJobId, initialAttempt.AttemptId, reload.Outcome.Value));
            }

            var job = reload.Job!;
            var attempt = reload.Attempt!;
            var transactionalUtcNow = reload.UtcNow;

            // Inference can finish out of order; mastery updates must retain the submission order.
            // The later response is already checkpointed, so this wait never needs another AI call.
            if (_checkpointStore is not null && await HasEarlierUnfinishedAnalysisAsync(job, attempt, cancellationToken))
            {
                // Discovery blocks this checkpoint until its predecessor is terminal;
                // it can then be committed immediately, without another timer delay.
                Defer(job, transactionalUtcNow, TimeSpan.Zero, "AI_WAITING_FOR_EARLIER_EVIDENCE");
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result(job.AnalysisJobId, attempt.AttemptId, AIAnalysisJobProcessingOutcome.RetryScheduled);
            }

            await _twinCompletionOrchestrator.CompleteAsync(
                attempt,
                requestContext.Question,
                analysis,
                eventSource,
                transactionalUtcNow,
                cancellationToken,
                requestContext.AllowedNodes.Select(n => n.NodeId).ToArray());
            if (_stateMachine.Complete(job, transactionalUtcNow)
                != AIAnalysisJobTransitionResult.Success)
            {
                return await RollbackResultAsync(
                    transaction,
                    Result(
                        initialJob.AnalysisJobId,
                        initialAttempt.AttemptId,
                        AIAnalysisJobProcessingOutcome.NotEligible));
            }

            if (_postProcessing is not null)
                await _postProcessing.EnqueueAsync(attempt.CenterId, attempt.StudentId, requestContext.Question.SubjectId,
                    attempt.AssignmentId, attempt.AttemptId, transactionalUtcNow, cancellationToken);

            // The authoritative analysis now carries the diagnostic evidence; the recovery copy is no longer needed.
            if (_checkpointStore is not null)
            {
                var checkpoint = await _dbContext.AIAnalysisCheckpoints.SingleOrDefaultAsync(x =>
                    x.CenterId == attempt.CenterId && x.AttemptId == attempt.AttemptId, cancellationToken);
                if (checkpoint is not null) _dbContext.AIAnalysisCheckpoints.Remove(checkpoint);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            recommendationCenterId = attempt.CenterId;
            recommendationStudentId = attempt.StudentId;
            recommendationSubjectId = requestContext.Question.SubjectId;
            recommendationAttemptId = attempt.AttemptId;
            recommendationTriggerAt = transactionalUtcNow;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            await transaction.DisposeAsync();
            _dbContext.ChangeTracker.Clear();
            if (_checkpointStore is not null)
                return await PersistDeferralAsync(initialJob, initialAttempt, requestContext, workerId,
                    TimeSpan.FromSeconds(1), "AI_DATABASE_RETRY", cancellationToken);
            return Result(
                initialJob.AnalysisJobId,
                initialAttempt.AttemptId,
                AIAnalysisJobProcessingOutcome.LostRace);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            await transaction.DisposeAsync();
            _dbContext.ChangeTracker.Clear();

            if (await WasCompletedByAnotherProcessorAsync(
                    initialJob.AnalysisJobId,
                    initialAttempt.AttemptId,
                    cancellationToken))
            {
                return Result(
                    initialJob.AnalysisJobId,
                    initialAttempt.AttemptId,
                    AIAnalysisJobProcessingOutcome.AlreadyTerminal);
            }

            throw;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _dbContext.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            AIProcessingMetrics.Duration.Record(System.Diagnostics.Stopwatch.GetElapsedTime(commitStarted).TotalMilliseconds,
                new KeyValuePair<string, object?>("stage", "commit"));
        }

        if (_postProcessing is null) await TryGenerateRecommendationAfterCommitAsync(
            recommendationCenterId,
            recommendationStudentId,
            recommendationSubjectId,
            recommendationAttemptId,
            recommendationTriggerAt);
        if (_postProcessing is null) await TryGenerateOverallCommentAfterCommitAsync(
            recommendationCenterId,
            initialAttempt.AssignmentId,
            recommendationStudentId);

        return Result(
            initialJob.AnalysisJobId,
            initialAttempt.AttemptId,
            AIAnalysisJobProcessingOutcome.Completed);
    }

    private async Task<AIAnalysisJobProcessingResult> PersistAnalysisFailureAsync(
        AIAnalysisJob initialJob,
        Attempt initialAttempt,
        RequestContext? requestContext,
        string workerId,
        CancellationToken cancellationToken)
    {
        if (initialJob.RetryCount > 1)
        {
            return Result(
                initialJob.AnalysisJobId,
                initialAttempt.AttemptId,
                AIAnalysisJobProcessingOutcome.NotEligible);
        }

        ReasoningAnalysis? fallback = null;
        if (initialJob.RetryCount == 1)
        {
            var fallbackUtcNow = _timeProvider.GetUtcNow().UtcDateTime;
            fallback = _fallbackBuilder.Build(new RuleBasedFallbackInput(
                initialAttempt.CenterId,
                initialAttempt.AttemptId,
                initialAttempt.IsCorrect,
                initialAttempt.AwardedScore,
                initialAttempt.Skipped,
                initialAttempt.ReasoningLanguage,
                fallbackUtcNow));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        AIAnalysisJobProcessingOutcome committedOutcome = default;
        Guid recommendationCenterId = default;
        Guid recommendationStudentId = default;
        Guid recommendationSubjectId = default;
        ulong recommendationQuestionId = default;
        ulong recommendationAttemptId = default;
        DateTime recommendationTriggerAt = default;

        try
        {
            if (_checkpointStore is not null)
                await StudentLockHelper.AcquireStudentLockAsync(_dbContext, initialJob.CenterId, initialAttempt.StudentId, cancellationToken);
            var reload = await ReloadAndRevalidateAsync(
                initialJob,
                initialAttempt,
                requestContext,
                workerId,
                cancellationToken);
            if (reload.Outcome.HasValue)
            {
                return await RollbackResultAsync(
                    transaction,
                    Result(initialJob.AnalysisJobId, initialAttempt.AttemptId, reload.Outcome.Value));
            }

            var job = reload.Job!;
            var attempt = reload.Attempt!;
            var transactionalUtcNow = reload.UtcNow;
            AIAnalysisJobProcessingOutcome outcome;
            AIAnalysisJobTransitionResult transition;

            if (job.RetryCount == 0)
            {
                transition = _stateMachine.Retry(
                    job,
                    transactionalUtcNow,
                    transactionalUtcNow,
                    AnalysisFailureCode,
                    AnalysisFailureMessage);
                attempt.Status = AttemptStatus.PendingAnalysis;
                outcome = AIAnalysisJobProcessingOutcome.RetryScheduled;
            }
            else if (job.RetryCount == 1 && fallback is not null)
            {
                var allowedIds = requestContext?.AllowedNodes?.Select(n => n.NodeId).ToArray();
                var question = requestContext?.Question
                    ?? attempt.Question
                    ?? await _dbContext.Questions
                        .SingleOrDefaultAsync(
                            q => q.CenterId == attempt.CenterId && q.QuestionId == attempt.QuestionId,
                            cancellationToken);

                if (question is not null)
                {
                    await _twinCompletionOrchestrator.CompleteAsync(
                        attempt,
                        question,
                        fallback,
                        TwinEventSource.RuleFallback,
                        transactionalUtcNow,
                        cancellationToken,
                        allowedIds);
                }
                else
                {
                    var consistency = new EvidenceConsistencyResult(
                        SemanticValidationPassed: true,
                        HasContradiction: false,
                        HasAnomaly: false,
                        HasRequiredEvidence: false,
                        ReasonCodes: [EvidenceReasonCodes.SourceRuleFallback],
                        StructuralValidationPassed: true);

                    var decision = _evidenceGate.Evaluate(new EvidenceGateInput(
                        EvidenceSourceType.RuleFallback,
                        StructuralValidationPassed: consistency.StructuralValidationPassed,
                        SemanticValidationPassed: consistency.SemanticValidationPassed,
                        HasContradiction: consistency.HasContradiction,
                        HasAnomaly: consistency.HasAnomaly,
                        HasRequiredEvidence: consistency.HasRequiredEvidence,
                        EffectiveIsCorrect: attempt.IsCorrect,
                        AnalysisConfidence: null,
                        AnalysisOverrideVersion: fallback.OverrideVersion,
                        IsPostFeedback: attempt.IsPostFeedback));
                    fallback.NeedsTeacherReview = decision.RequiresTeacherReview;
                    var evidence = _evidenceAssessmentFactory.Create(
                        attempt,
                        fallback,
                        supersedes: null,
                        decision,
                        transactionalUtcNow,
                        createdBy: null);

                    // If context disappeared during recovery, retain the old
                    // fallback and immutable evidence instead of inserting a
                    // second analysis into the unique attempt checkpoint.
                    var existing = await _dbContext.ReasoningAnalyses.AnyAsync(a =>
                        a.CenterId == attempt.CenterId && a.AttemptId == attempt.AttemptId, cancellationToken);
                    if (!existing)
                    {
                        _dbContext.ReasoningAnalyses.Add(fallback);
                        _dbContext.EvidenceAssessments.Add(evidence);
                    }
                    attempt.Status = AttemptStatus.NeedsTeacherReview;
                    attempt.UpdatedAt = transactionalUtcNow;
                }

                transition = _stateMachine.CompleteFallback(
                    job,
                    transactionalUtcNow,
                    AnalysisFailureCode,
                    AnalysisFailureMessage);
                outcome = AIAnalysisJobProcessingOutcome.FallbackCompleted;
            }
            else
            {
                return await RollbackResultAsync(
                    transaction,
                    Result(
                        initialJob.AnalysisJobId,
                        initialAttempt.AttemptId,
                        AIAnalysisJobProcessingOutcome.NotEligible));
            }

            if (transition != AIAnalysisJobTransitionResult.Success)
            {
                return await RollbackResultAsync(
                    transaction,
                    Result(
                        initialJob.AnalysisJobId,
                        initialAttempt.AttemptId,
                        AIAnalysisJobProcessingOutcome.NotEligible));
            }

            attempt.UpdatedAt = transactionalUtcNow;
            if (outcome == AIAnalysisJobProcessingOutcome.FallbackCompleted && _postProcessing is not null && requestContext is not null)
                await _postProcessing.EnqueueAsync(attempt.CenterId, attempt.StudentId, requestContext.Question.SubjectId,
                    attempt.AssignmentId, attempt.AttemptId, transactionalUtcNow, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committedOutcome = outcome;
            recommendationCenterId = attempt.CenterId;
            recommendationStudentId = attempt.StudentId;
            recommendationSubjectId = requestContext?.Question?.SubjectId
                ?? attempt.Question?.SubjectId
                ?? Guid.Empty;
            recommendationQuestionId = attempt.QuestionId;
            recommendationAttemptId = attempt.AttemptId;
            recommendationTriggerAt = transactionalUtcNow;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _dbContext.ChangeTracker.Clear();
            return Result(
                initialJob.AnalysisJobId,
                initialAttempt.AttemptId,
                AIAnalysisJobProcessingOutcome.LostRace);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            await transaction.DisposeAsync();
            _dbContext.ChangeTracker.Clear();

            if (await WasCompletedByAnotherProcessorAsync(
                    initialJob.AnalysisJobId,
                    initialAttempt.AttemptId,
                    cancellationToken))
            {
                return Result(
                    initialJob.AnalysisJobId,
                    initialAttempt.AttemptId,
                    AIAnalysisJobProcessingOutcome.AlreadyTerminal);
            }

            throw;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _dbContext.ChangeTracker.Clear();
            throw;
        }

        if (committedOutcome == AIAnalysisJobProcessingOutcome.FallbackCompleted && _postProcessing is null)
        {
            if (recommendationSubjectId == Guid.Empty)
            {
                recommendationSubjectId = await ResolveSubjectIdAfterCommitAsync(
                    recommendationCenterId,
                    recommendationQuestionId);
            }

            if (recommendationSubjectId != Guid.Empty)
            {
                await TryGenerateRecommendationAfterCommitAsync(
                    recommendationCenterId,
                    recommendationStudentId,
                    recommendationSubjectId,
                    recommendationAttemptId,
                    recommendationTriggerAt);
            }

            await TryGenerateOverallCommentAfterCommitAsync(
                recommendationCenterId,
                initialAttempt.AssignmentId,
                recommendationStudentId);
        }

        return Result(initialJob.AnalysisJobId, initialAttempt.AttemptId, committedOutcome);
    }

    private async Task<IReadOnlyList<AnalyzeReasoningImagePart>> LoadAttachmentImagePartsAsync(
        Guid centerId,
        ulong attemptId,
        CancellationToken cancellationToken)
    {
        var attachment = await _dbContext.AttemptAttachments.AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.CenterId == centerId && candidate.AttemptId == attemptId,
                cancellationToken);
        if (attachment is null)
        {
            return [];
        }

        if (_attachmentStorage is null)
        {
            throw new AttemptAttachmentStorageUnavailableException(
                "Attempt attachment storage is not configured.");
        }

        try
        {
            var bytes = await BoundedRetryHelper.ExecuteWithRetryAsync(async () =>
            {
                await using var source = await _attachmentStorage.OpenPermanentReadAsync(
                    attachment.StorageKey,
                    cancellationToken);
                await using var destination = new MemoryStream(checked((int)attachment.FileSizeBytes));
                await source.CopyToAsync(destination, cancellationToken);
                if (destination.Length != attachment.FileSizeBytes || destination.Length > 5_242_880)
                {
                    throw new AttemptAttachmentStorageUnavailableException(
                        "Stored attachment content does not match its immutable metadata.");
                }

                return destination.ToArray();
            }, maxAttempts: 3, initialDelayMs: 50, cancellationToken: cancellationToken);

            return [new AnalyzeReasoningImagePart(bytes, attachment.ContentType)];
        }
        catch (AttemptAttachmentStorageUnavailableException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AttemptAttachmentStorageUnavailableException(
                "Attempt attachment is temporarily unavailable.", exception);
        }
    }

    private async Task<AIAnalysisJobProcessingResult> PersistStorageFailureAsync(
        AIAnalysisJob initialJob,
        Attempt initialAttempt,
        RequestContext requestContext,
        string workerId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        AIAnalysisJobProcessingOutcome committedOutcome = default;

        try
        {
            if (_checkpointStore is not null)
                await StudentLockHelper.AcquireStudentLockAsync(_dbContext, initialJob.CenterId, initialAttempt.StudentId, cancellationToken);
            var reload = await ReloadAndRevalidateAsync(
                initialJob,
                initialAttempt,
                requestContext,
                workerId,
                cancellationToken);
            if (reload.Outcome.HasValue)
            {
                return await RollbackResultAsync(
                    transaction,
                    Result(initialJob.AnalysisJobId, initialAttempt.AttemptId, reload.Outcome.Value));
            }

            var job = reload.Job!;
            var attempt = reload.Attempt!;
            var transactionalUtcNow = reload.UtcNow;
            AIAnalysisJobProcessingOutcome outcome;
            AIAnalysisJobTransitionResult transition;

            if (job.RetryCount < 3)
            {
                var delaySeconds = Math.Min(300, (int)Math.Pow(2, job.RetryCount) * 10);
                var retryAvailableAt = transactionalUtcNow.AddSeconds(delaySeconds);

                transition = _stateMachine.Retry(
                    job,
                    transactionalUtcNow,
                    retryAvailableAt,
                    AttachmentStorageUnavailableCode,
                    AttachmentStorageUnavailableMessage,
                    maxRetries: 3);

                attempt.Status = AttemptStatus.PendingAnalysis;
                outcome = AIAnalysisJobProcessingOutcome.RetryScheduled;
            }
            else
            {
                if (attempt.AssignmentId.HasValue)
                {
                    attempt.Status = AttemptStatus.NeedsTeacherReview;
                    transition = _stateMachine.CompleteFallback(
                        job,
                        transactionalUtcNow,
                        AttachmentStorageUnavailableCode,
                        AttachmentStorageUnavailableMessage);

                    var fallbackUtcNow = _timeProvider.GetUtcNow().UtcDateTime;
                    var fallback = _fallbackBuilder.Build(new RuleBasedFallbackInput(
                        attempt.CenterId,
                        attempt.AttemptId,
                        attempt.IsCorrect,
                        attempt.AwardedScore,
                        attempt.Skipped,
                        attempt.ReasoningLanguage,
                        fallbackUtcNow));

                    var consistency = new EvidenceConsistencyResult(
                        SemanticValidationPassed: true,
                        HasContradiction: false,
                        HasAnomaly: false,
                        HasRequiredEvidence: false,
                        ReasonCodes: [EvidenceReasonCodes.SourceRuleFallback],
                        StructuralValidationPassed: true);

                    var decision = _evidenceGate.Evaluate(new EvidenceGateInput(
                        EvidenceSourceType.RuleFallback,
                        StructuralValidationPassed: consistency.StructuralValidationPassed,
                        SemanticValidationPassed: consistency.SemanticValidationPassed,
                        HasContradiction: consistency.HasContradiction,
                        HasAnomaly: consistency.HasAnomaly,
                        HasRequiredEvidence: consistency.HasRequiredEvidence,
                        EffectiveIsCorrect: attempt.IsCorrect,
                        AnalysisConfidence: null,
                        AnalysisOverrideVersion: fallback.OverrideVersion,
                        IsPostFeedback: attempt.IsPostFeedback));

                    fallback.NeedsTeacherReview = true;

                    var evidence = _evidenceAssessmentFactory.Create(
                        attempt,
                        fallback,
                        supersedes: null,
                        decision,
                        transactionalUtcNow,
                        createdBy: null);

                    var existing = await _dbContext.ReasoningAnalyses.AnyAsync(a =>
                        a.CenterId == attempt.CenterId && a.AttemptId == attempt.AttemptId, cancellationToken);
                    if (!existing)
                    {
                        _dbContext.ReasoningAnalyses.Add(fallback);
                        _dbContext.EvidenceAssessments.Add(evidence);
                    }

                    outcome = AIAnalysisJobProcessingOutcome.FallbackCompleted;
                }
                else
                {
                    transition = _stateMachine.FailTerminal(
                        job,
                        transactionalUtcNow,
                        AttachmentStorageUnavailableCode,
                        AttachmentStorageUnavailableMessage);

                    attempt.Status = AttemptStatus.AnalysisFailed;
                    outcome = AIAnalysisJobProcessingOutcome.FailedTerminal;
                }
            }

            if (transition != AIAnalysisJobTransitionResult.Success)
            {
                return await RollbackResultAsync(
                    transaction,
                    Result(
                        initialJob.AnalysisJobId,
                        initialAttempt.AttemptId,
                        AIAnalysisJobProcessingOutcome.NotEligible));
            }

            attempt.UpdatedAt = transactionalUtcNow;
            if (outcome == AIAnalysisJobProcessingOutcome.FallbackCompleted && _postProcessing is not null)
                await _postProcessing.EnqueueAsync(attempt.CenterId, attempt.StudentId, requestContext.Question.SubjectId,
                    attempt.AssignmentId, attempt.AttemptId, transactionalUtcNow, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committedOutcome = outcome;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _dbContext.ChangeTracker.Clear();
            return Result(
                initialJob.AnalysisJobId,
                initialAttempt.AttemptId,
                AIAnalysisJobProcessingOutcome.LostRace);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            await transaction.DisposeAsync();
            _dbContext.ChangeTracker.Clear();

            if (await WasCompletedByAnotherProcessorAsync(
                    initialJob.AnalysisJobId,
                    initialAttempt.AttemptId,
                    cancellationToken))
            {
                return Result(
                    initialJob.AnalysisJobId,
                    initialAttempt.AttemptId,
                    AIAnalysisJobProcessingOutcome.AlreadyTerminal);
            }

            throw;
        }

        return Result(initialJob.AnalysisJobId, initialAttempt.AttemptId, committedOutcome);
    }

    private async Task<Guid> ResolveSubjectIdAfterCommitAsync(Guid centerId, ulong questionId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            return await _dbContext.Questions
                .AsNoTracking()
                .Where(q => q.CenterId == centerId && q.QuestionId == questionId)
                .Select(q => q.SubjectId)
                .FirstOrDefaultAsync(timeout.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Post-commit recommendation subject lookup failed for center {CenterId}, question {QuestionId}.",
                centerId,
                questionId);
            return Guid.Empty;
        }
    }

    private async Task TryGenerateOverallCommentAfterCommitAsync(Guid centerId, Guid? assignmentId, Guid studentId)
    {
        if (_overallCommentWorkflow is null || !assignmentId.HasValue) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await _overallCommentWorkflow.GenerateAndCacheOverallCommentAsync(centerId, assignmentId.Value, studentId, timeout.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Post-commit assignment comment generation failed for assignment {AssignmentId} and student {StudentId}.", assignmentId, studentId);
        }
    }

    private async Task<AIAnalysisJobProcessingResult> PersistDeferralAsync(AIAnalysisJob initialJob, Attempt initialAttempt,
        RequestContext? context, string worker, TimeSpan delay, string code, CancellationToken token)
    {
        _dbContext.ChangeTracker.Clear();
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(token);
        await StudentLockHelper.AcquireStudentLockAsync(_dbContext, initialJob.CenterId, initialAttempt.StudentId, token);
        var reload = await ReloadAndRevalidateAsync(initialJob, initialAttempt, context, worker, token);
        if (reload.Outcome.HasValue)
            return await RollbackResultAsync(transaction, Result(initialJob.AnalysisJobId, initialAttempt.AttemptId, reload.Outcome.Value));
        var now = reload.UtcNow;
        var job = reload.Job!;
        Defer(job, now, delay, code);
        await _dbContext.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Result(job.AnalysisJobId, initialAttempt.AttemptId, AIAnalysisJobProcessingOutcome.RetryScheduled);
    }

    private static void Defer(AIAnalysisJob job, DateTime now, TimeSpan delay, string code)
    {
        job.Status = AIJobStatus.Pending;
        job.AvailableAt = now.AddSeconds(Math.Clamp(delay.TotalSeconds, 1, 3600));
        job.StartedAt = null;
        job.LeaseOwner = null;
        job.LeaseUntil = null;
        job.LastErrorCode = code;
        job.LastErrorMessage = "Waiting for processing capacity; submission is preserved.";
        job.UpdatedAt = now;
    }

    private Task<bool> HasEarlierUnfinishedAnalysisAsync(AIAnalysisJob job, Attempt attempt, CancellationToken token) =>
        (from earlier in _dbContext.AIAnalysisJobs.AsNoTracking()
         join submission in _dbContext.Attempts.AsNoTracking()
             on new { earlier.CenterId, earlier.AttemptId } equals new { submission.CenterId, submission.AttemptId }
         where earlier.CenterId == job.CenterId && submission.StudentId == attempt.StudentId
             && (submission.CreatedAt < attempt.CreatedAt ||
                 (submission.CreatedAt == attempt.CreatedAt && submission.AttemptId < attempt.AttemptId))
             && (earlier.Status == AIJobStatus.Pending || earlier.Status == AIJobStatus.Processing)
             && (submission.Status == AttemptStatus.PendingAnalysis || submission.Status == AttemptStatus.Processing)
             && !_dbContext.ReasoningAnalyses.Any(a => a.CenterId == job.CenterId && a.AttemptId == earlier.AttemptId)
         select earlier.AnalysisJobId).AnyAsync(token);

    private async Task TryGenerateRecommendationAfterCommitAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        ulong sourceAttemptId,
        DateTime triggerAt)
    {
        if (_recommendationEngine is null)
        {
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await _recommendationEngine.GenerateAndPersistAsync(
                centerId,
                studentId,
                subjectId,
                sourceAttemptId,
                triggerAt,
                timeout.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Best-effort recommendation generation failed after authoritative commit for center {CenterId}, student {StudentId}, subject {SubjectId}, attempt {AttemptId}.",
                centerId,
                studentId,
                subjectId,
                sourceAttemptId);
        }
    }

    private async Task<ReloadResult> ReloadAndRevalidateAsync(
        AIAnalysisJob initialJob,
        Attempt initialAttempt,
        RequestContext? requestContext,
        string workerId,
        CancellationToken cancellationToken)
    {
        var centerId = initialJob.CenterId;
        var job = await _dbContext.AIAnalysisJobs
            .SingleOrDefaultAsync(
                candidate => candidate.CenterId == centerId
                    && candidate.AnalysisJobId == initialJob.AnalysisJobId,
                cancellationToken);

        if (job is null)
        {
            return ReloadResult.Failed(AIAnalysisJobProcessingOutcome.NotFound);
        }

        if (IsTerminal(job.Status))
        {
            return ReloadResult.Failed(AIAnalysisJobProcessingOutcome.AlreadyTerminal);
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        if (job.RowVersion != initialJob.RowVersion
            || job.AttemptId != initialAttempt.AttemptId
            || !HasValidLease(job, workerId, utcNow))
        {
            return ReloadResult.Failed(AIAnalysisJobProcessingOutcome.NotEligible);
        }

        var attempt = await _dbContext.Attempts
            .SingleOrDefaultAsync(
                candidate => candidate.CenterId == centerId
                    && candidate.AttemptId == job.AttemptId,
                cancellationToken);

        if (attempt is null)
        {
            return ReloadResult.Failed(AIAnalysisJobProcessingOutcome.NotFound);
        }

        if ((attempt.RowVersion != initialAttempt.RowVersion
                && !OnlySolutionExposureChanged(initialAttempt, attempt))
            || attempt.QuestionId != initialAttempt.QuestionId
            || !CanComplete(attempt.Status))
        {
            return ReloadResult.Failed(AIAnalysisJobProcessingOutcome.NotEligible);
        }

        if (await AnalysisBlocksProcessingAsync(attempt, cancellationToken))
        {
            return ReloadResult.Failed(AIAnalysisJobProcessingOutcome.NotEligible);
        }

        if (requestContext is not null
            && !await RequestContextStillMatchesAsync(
                centerId,
                attempt.QuestionId,
                requestContext,
                cancellationToken))
        {
            return ReloadResult.Failed(AIAnalysisJobProcessingOutcome.NotEligible);
        }

        return ReloadResult.Valid(job, attempt, utcNow);
    }

    private async Task<bool> AnalysisBlocksProcessingAsync(Attempt attempt, CancellationToken cancellationToken)
    {
        var analysis = await _dbContext.ReasoningAnalyses.AsNoTracking().FirstOrDefaultAsync(a =>
            a.CenterId == attempt.CenterId && a.AttemptId == attempt.AttemptId, cancellationToken);
        if (analysis is null) return false;
        if (!AttemptFeedbackActionPolicy.CanRecoverFallback(attempt, analysis)) return true;
        if (await _dbContext.StudentReviewRequests.AnyAsync(r => r.CenterId == attempt.CenterId &&
            r.AttemptId == attempt.AttemptId && r.Status == StudentReviewRequestStatus.Pending, cancellationToken)) return true;
        if (!attempt.AssignmentId.HasValue) return false;
        return await _dbContext.StudentAssignmentProgresses.AnyAsync(p => p.CenterId == attempt.CenterId &&
            p.AssignmentId == attempt.AssignmentId && p.StudentId == attempt.StudentId && !p.IsDeleted &&
            p.TeacherFinalReviewStatus == TeacherFinalReviewStatus.Approved, cancellationToken) ||
            await _dbContext.AssignmentQuestions.AnyAsync(q => q.CenterId == attempt.CenterId &&
                q.AssignmentId == attempt.AssignmentId && q.QuestionId == attempt.QuestionId && q.IsVoided, cancellationToken);
    }

    private async Task<RequestContext?> LoadRequestContextAsync(
        Guid centerId,
        ulong questionId,
        CancellationToken cancellationToken)
    {
        var question = await _dbContext.Questions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.CenterId == centerId
                    && candidate.QuestionId == questionId
                    && !candidate.IsDeleted,
                cancellationToken);
        if (question is null)
        {
            return null;
        }

        var allowedNodes = await LoadAllowedNodesAsync(
            centerId,
            question.QuestionId,
            question.SubjectId,
            cancellationToken);
        if (allowedNodes.Length == 0
            || !allowedNodes.Any(node => node.NodeId == question.PrimaryTopicNodeId))
        {
            return null;
        }

        return new RequestContext(question, allowedNodes);
    }

    private async Task<KnowledgeNode[]> LoadAllowedNodesAsync(
        Guid centerId,
        ulong questionId,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        var mappedNodes = await (
            from mapping in _dbContext.QuestionKnowledgeNodes.AsNoTracking()
            join node in _dbContext.KnowledgeNodes.AsNoTracking()
                on new { mapping.CenterId, mapping.NodeId }
                equals new { node.CenterId, node.NodeId }
            where mapping.CenterId == centerId
                && mapping.QuestionId == questionId
                && (mapping.MappingRole == MappingRole.Primary
                    || mapping.MappingRole == MappingRole.Secondary
                    || mapping.MappingRole == MappingRole.Prerequisite)
                && node.CenterId == centerId
                && node.SubjectId == subjectId
                && node.IsActive
                && !node.IsDeleted
            select node)
            .ToListAsync(cancellationToken);

        return mappedNodes
            .GroupBy(node => node.NodeId)
            .Select(group => group.First())
            .OrderBy(node => node.NodeId)
            .ToArray();
    }

    private async Task<bool> RequestContextStillMatchesAsync(
        Guid centerId,
        ulong questionId,
        RequestContext initialContext,
        CancellationToken cancellationToken)
    {
        var question = await _dbContext.Questions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.CenterId == centerId
                    && candidate.QuestionId == questionId
                    && !candidate.IsDeleted,
                cancellationToken);
        if (question is null
            || question.SubjectId != initialContext.Question.SubjectId
            || question.RowVersion != initialContext.Question.RowVersion
            || question.PrimaryTopicNodeId != initialContext.Question.PrimaryTopicNodeId)
        {
            return false;
        }

        var allowedNodes = await LoadAllowedNodesAsync(
            centerId,
            questionId,
            question.SubjectId,
            cancellationToken);

        return allowedNodes
            .Select(node => new AllowedNodeSnapshot(node.NodeId, node.NodeName))
            .SequenceEqual(initialContext.AllowedNodes.Select(
                node => new AllowedNodeSnapshot(node.NodeId, node.NodeName)));
    }

    private async Task<bool> WasCompletedByAnotherProcessorAsync(
        ulong analysisJobId,
        ulong attemptId,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved)
        {
            return false;
        }

        var centerId = _tenantContext.CenterId!.Value;
        var job = await _dbContext.AIAnalysisJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.CenterId == centerId
                    && candidate.AnalysisJobId == analysisJobId,
                cancellationToken);
        if (job is null || !IsTerminal(job.Status))
        {
            return false;
        }

        var hasAnalysis = await _dbContext.ReasoningAnalyses
            .AsNoTracking()
            .AnyAsync(
                analysis => analysis.CenterId == centerId
                    && analysis.AttemptId == attemptId,
                cancellationToken);
        if (!hasAnalysis)
        {
            return false;
        }

        return await _dbContext.EvidenceAssessments
            .AsNoTracking()
            .AnyAsync(
                evidence => evidence.CenterId == centerId
                    && evidence.AttemptId == attemptId,
                cancellationToken);
    }

    private async Task<AIAnalysisJobProcessingResult> RollbackResultAsync(
        IDbContextTransaction transaction,
        AIAnalysisJobProcessingResult result)
    {
        await transaction.RollbackAsync(CancellationToken.None);
        _dbContext.ChangeTracker.Clear();
        return result;
    }

    private static bool HasValidLease(
        AIAnalysisJob job,
        string workerId,
        DateTime utcNow) =>
        job.Status == AIJobStatus.Processing
        && string.Equals(job.LeaseOwner, workerId, StringComparison.Ordinal)
        && job.LeaseUntil.HasValue
        && job.LeaseUntil.Value >= utcNow;

    private static bool CanComplete(AttemptStatus status) =>
        status is AttemptStatus.PendingAnalysis or AttemptStatus.Processing;

    // Viewing a solution is observational metadata, not a change to the answer
    // sent to AI. Keep the fresh tracked row (and its concurrency token) so this
    // timestamp is preserved while completion remains an optimistic-concurrency
    // write. Never tolerate changes to submission, grading, retry or audit data.
    private static bool OnlySolutionExposureChanged(Attempt before, Attempt after) =>
        !before.SolutionExposedAt.HasValue && after.SolutionExposedAt.HasValue
        && before.AttemptId == after.AttemptId
        && before.CenterId == after.CenterId
        && before.StudentId == after.StudentId
        && before.QuestionId == after.QuestionId
        && before.AssignmentId == after.AssignmentId
        && before.FinalAnswer == after.FinalAnswer
        && before.AnswerDisplayLatex == after.AnswerDisplayLatex
        && before.ReasoningText == after.ReasoningText
        && before.ReasoningLanguage == after.ReasoningLanguage
        && before.IsCorrect == after.IsCorrect
        && before.AwardedScore == after.AwardedScore
        && before.PreliminaryGradingReasonCode == after.PreliminaryGradingReasonCode
        && before.TimeSpentSeconds == after.TimeSpentSeconds
        && before.Confidence == after.Confidence
        && before.AnswerChanges == after.AnswerChanges
        && before.Skipped == after.Skipped
        && before.Status == after.Status
        && before.ClientSubmissionId == after.ClientSubmissionId
        && before.ManualRetryCount == after.ManualRetryCount
        && before.LastManualRetryAt == after.LastManualRetryAt
        && before.IsPostFeedback == after.IsPostFeedback
        && before.CreatedAt == after.CreatedAt
        && before.CreatedBy == after.CreatedBy
        && before.UpdatedAt == after.UpdatedAt;

    private static bool IsTerminal(AIJobStatus status) =>
        status is AIJobStatus.Completed
            or AIJobStatus.FallbackCompleted
            or AIJobStatus.FailedTerminal;

    private static AIAnalysisJobProcessingResult Result(
        ulong analysisJobId,
        ulong? attemptId,
        AIAnalysisJobProcessingOutcome outcome) =>
        new(analysisJobId, attemptId, outcome);

    private sealed record RequestContext(
        Question Question,
        IReadOnlyList<KnowledgeNode> AllowedNodes);

    private sealed record AllowedNodeSnapshot(ulong NodeId, string NodeName);

    private sealed record ReloadResult(
        AIAnalysisJob? Job,
        Attempt? Attempt,
        DateTime UtcNow,
        AIAnalysisJobProcessingOutcome? Outcome)
    {
        public static ReloadResult Valid(
            AIAnalysisJob job,
            Attempt attempt,
            DateTime utcNow) => new(job, attempt, utcNow, null);

        public static ReloadResult Failed(
            AIAnalysisJobProcessingOutcome outcome) => new(null, null, default, outcome);
    }
}
