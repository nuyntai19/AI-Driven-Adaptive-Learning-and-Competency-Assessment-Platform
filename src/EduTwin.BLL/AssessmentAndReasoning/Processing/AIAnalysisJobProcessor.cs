using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning.Processing;

public sealed class AIAnalysisJobProcessor : IAIAnalysisJobProcessor
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IRuleBasedFallbackBuilder _fallbackBuilder;
    private readonly IAIAnalysisJobStateMachine _stateMachine;
    private readonly TimeProvider _timeProvider;

    public AIAnalysisJobProcessor(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IRuleBasedFallbackBuilder fallbackBuilder,
        IAIAnalysisJobStateMachine stateMachine,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
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

        var initialJob = await _dbContext.AIAnalysisJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                job => job.AnalysisJobId == analysisJobId,
                cancellationToken);

        if (initialJob is null || initialJob.CenterId != _tenantContext.CenterId)
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
                attempt => attempt.AttemptId == initialJob.AttemptId,
                cancellationToken);

        if (initialAttempt is null || initialAttempt.CenterId != initialJob.CenterId)
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
                analysis => analysis.AttemptId == initialAttempt.AttemptId,
                cancellationToken);
        if (analysisAlreadyExists)
        {
            return Result(
                analysisJobId,
                initialAttempt.AttemptId,
                AIAnalysisJobProcessingOutcome.NotEligible);
        }

        utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        if (!HasValidLease(initialJob, workerId, utcNow))
        {
            return Result(
                analysisJobId,
                initialAttempt.AttemptId,
                AIAnalysisJobProcessingOutcome.NotEligible);
        }

        var fallback = _fallbackBuilder.Build(new RuleBasedFallbackInput(
            initialAttempt.CenterId,
            initialAttempt.AttemptId,
            initialAttempt.IsCorrect,
            initialAttempt.AwardedScore,
            initialAttempt.Skipped,
            initialAttempt.ReasoningLanguage,
            utcNow));

        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            var job = await _dbContext.AIAnalysisJobs
                .SingleOrDefaultAsync(
                    candidate => candidate.AnalysisJobId == analysisJobId,
                    cancellationToken);

            if (job is null || job.CenterId != _tenantContext.CenterId)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _dbContext.ChangeTracker.Clear();
                return Result(analysisJobId, null, AIAnalysisJobProcessingOutcome.NotFound);
            }

            if (IsTerminal(job.Status))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _dbContext.ChangeTracker.Clear();
                return Result(
                    analysisJobId,
                    job.AttemptId,
                    AIAnalysisJobProcessingOutcome.AlreadyTerminal);
            }

            var transactionalUtcNow = _timeProvider.GetUtcNow().UtcDateTime;
            if (job.RowVersion != initialJob.RowVersion
                || !HasValidLease(job, workerId, transactionalUtcNow))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _dbContext.ChangeTracker.Clear();
                return Result(
                    analysisJobId,
                    job.AttemptId,
                    AIAnalysisJobProcessingOutcome.NotEligible);
            }

            var attempt = await _dbContext.Attempts
                .SingleOrDefaultAsync(
                    candidate => candidate.AttemptId == job.AttemptId,
                    cancellationToken);

            if (attempt is null || attempt.CenterId != job.CenterId)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _dbContext.ChangeTracker.Clear();
                return Result(
                    analysisJobId,
                    job.AttemptId,
                    AIAnalysisJobProcessingOutcome.NotFound);
            }

            if (attempt.RowVersion != initialAttempt.RowVersion
                || !CanComplete(attempt.Status))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _dbContext.ChangeTracker.Clear();
                return Result(
                    analysisJobId,
                    attempt.AttemptId,
                    AIAnalysisJobProcessingOutcome.NotEligible);
            }

            analysisAlreadyExists = await _dbContext.ReasoningAnalyses
                .AnyAsync(
                    analysis => analysis.AttemptId == attempt.AttemptId,
                    cancellationToken);
            if (analysisAlreadyExists)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _dbContext.ChangeTracker.Clear();
                return Result(
                    analysisJobId,
                    attempt.AttemptId,
                    AIAnalysisJobProcessingOutcome.NotEligible);
            }

            _dbContext.ReasoningAnalyses.Add(fallback);
            attempt.Status = AttemptStatus.NeedsTeacherReview;
            attempt.UpdatedAt = transactionalUtcNow;

            var transition = _stateMachine.CompleteFallback(job, transactionalUtcNow);
            if (transition != AIAnalysisJobTransitionResult.Success)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _dbContext.ChangeTracker.Clear();
                return Result(
                    analysisJobId,
                    attempt.AttemptId,
                    AIAnalysisJobProcessingOutcome.NotEligible);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result(
                analysisJobId,
                attempt.AttemptId,
                AIAnalysisJobProcessingOutcome.FallbackCompleted);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _dbContext.ChangeTracker.Clear();
            return Result(
                analysisJobId,
                initialAttempt.AttemptId,
                AIAnalysisJobProcessingOutcome.LostRace);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            await transaction.DisposeAsync();
            _dbContext.ChangeTracker.Clear();

            if (await WasCompletedByAnotherProcessorAsync(
                    analysisJobId,
                    initialAttempt.AttemptId,
                    cancellationToken))
            {
                return Result(
                    analysisJobId,
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

    private async Task<bool> WasCompletedByAnotherProcessorAsync(
        ulong analysisJobId,
        ulong attemptId,
        CancellationToken cancellationToken)
    {
        var job = await _dbContext.AIAnalysisJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.AnalysisJobId == analysisJobId,
                cancellationToken);
        if (job is null || !IsTerminal(job.Status))
        {
            return false;
        }

        return await _dbContext.ReasoningAnalyses
            .AsNoTracking()
            .AnyAsync(
                analysis => analysis.AttemptId == attemptId,
                cancellationToken);
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
}
