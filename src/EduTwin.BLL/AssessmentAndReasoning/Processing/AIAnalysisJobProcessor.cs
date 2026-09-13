using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.DigitalTwin.Orchestration;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Recommendations;
using EduTwin.Contracts.AssessmentAndReasoning;
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
        ILogger<AIAnalysisJobProcessor>? logger = null)
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
    }

    public async Task<AIAnalysisJobProcessingResult> ExecuteAsync(
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

        var analysisAlreadyExists = await _dbContext.ReasoningAnalyses
            .AsNoTracking()
            .AnyAsync(
                analysis => analysis.CenterId == centerId
                    && analysis.AttemptId == initialAttempt.AttemptId,
                cancellationToken);
        if (analysisAlreadyExists)
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

        ReasoningAnalysis analysis;
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

            var response = await _aiService.AnalyzeReasoningAsync(
                request,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var analysisUtcNow = _timeProvider.GetUtcNow().UtcDateTime;
            analysis = _analysisBuilder.Build(
                centerId,
                initialAttempt.AttemptId,
                response,
                analysisUtcNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return await PersistAnalysisFailureAsync(
                initialJob,
                initialAttempt,
                requestContext,
                workerId,
                cancellationToken);
        }

        return await PersistSuccessAsync(
            initialJob,
            initialAttempt,
            requestContext,
            analysis,
            workerId,
            cancellationToken);
    }

    private async Task<AIAnalysisJobProcessingResult> PersistSuccessAsync(
        AIAnalysisJob initialJob,
        Attempt initialAttempt,
        RequestContext requestContext,
        ReasoningAnalysis analysis,
        string workerId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        Guid recommendationCenterId = default;
        Guid recommendationStudentId = default;
        Guid recommendationSubjectId = default;
        ulong recommendationAttemptId = default;
        DateTime recommendationTriggerAt = default;

        try
        {
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

            await _twinCompletionOrchestrator.CompleteAsync(
                attempt,
                requestContext.Question,
                analysis,
                TwinEventSource.AIAnalysis,
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

        await TryGenerateRecommendationAfterCommitAsync(
            recommendationCenterId,
            recommendationStudentId,
            recommendationSubjectId,
            recommendationAttemptId,
            recommendationTriggerAt);

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
                        AnalysisOverrideVersion: fallback.OverrideVersion));
                    fallback.NeedsTeacherReview = decision.RequiresTeacherReview;
                    var evidence = _evidenceAssessmentFactory.Create(
                        attempt,
                        fallback,
                        supersedes: null,
                        decision,
                        transactionalUtcNow,
                        createdBy: null);

                    _dbContext.ReasoningAnalyses.Add(fallback);
                    _dbContext.EvidenceAssessments.Add(evidence);
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

        if (committedOutcome == AIAnalysisJobProcessingOutcome.FallbackCompleted)
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

            return [new AnalyzeReasoningImagePart(destination.ToArray(), attachment.ContentType)];
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

        if (attempt.RowVersion != initialAttempt.RowVersion
            || attempt.QuestionId != initialAttempt.QuestionId
            || !CanComplete(attempt.Status))
        {
            return ReloadResult.Failed(AIAnalysisJobProcessingOutcome.NotEligible);
        }

        var analysisAlreadyExists = await _dbContext.ReasoningAnalyses
            .AnyAsync(
                analysis => analysis.CenterId == centerId
                    && analysis.AttemptId == attempt.AttemptId,
                cancellationToken);
        if (analysisAlreadyExists)
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
