using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
    private readonly TimeProvider _timeProvider;

    public AIAnalysisJobProcessor(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IAIService aiService,
        IAIAnalysisRequestFactory requestFactory,
        IAIReasoningAnalysisBuilder analysisBuilder,
        IRuleBasedFallbackBuilder fallbackBuilder,
        IAIAnalysisJobStateMachine stateMachine,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _aiService = aiService;
        _requestFactory = requestFactory;
        _analysisBuilder = analysisBuilder;
        _fallbackBuilder = fallbackBuilder;
        _stateMachine = stateMachine;
        _timeProvider = timeProvider;
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
            var request = _requestFactory.Create(
                initialAttempt,
                requestContext.Question,
                requestContext.AllowedNodes);
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

            _dbContext.ReasoningAnalyses.Add(analysis);
            attempt.Status = AttemptStatus.Completed;
            attempt.UpdatedAt = transactionalUtcNow;
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

            return Result(
                initialJob.AnalysisJobId,
                initialAttempt.AttemptId,
                AIAnalysisJobProcessingOutcome.Completed);
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
                _dbContext.ReasoningAnalyses.Add(fallback);
                transition = _stateMachine.CompleteFallback(
                    job,
                    transactionalUtcNow,
                    AnalysisFailureCode,
                    AnalysisFailureMessage);
                attempt.Status = AttemptStatus.NeedsTeacherReview;
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

            return Result(initialJob.AnalysisJobId, initialAttempt.AttemptId, outcome);
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

        return await _dbContext.ReasoningAnalyses
            .AsNoTracking()
            .AnyAsync(
                analysis => analysis.CenterId == centerId
                    && analysis.AttemptId == attemptId,
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
