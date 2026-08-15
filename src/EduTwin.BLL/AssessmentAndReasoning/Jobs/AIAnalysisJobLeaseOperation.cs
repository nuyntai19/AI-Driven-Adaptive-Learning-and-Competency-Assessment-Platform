using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning.Jobs;

public sealed class AIAnalysisJobLeaseOperation : IAIAnalysisJobLeaseOperation
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IAIAnalysisJobStateMachine _stateMachine;
    private readonly TimeProvider _timeProvider;

    public AIAnalysisJobLeaseOperation(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IAIAnalysisJobStateMachine stateMachine,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _stateMachine = stateMachine;
        _timeProvider = timeProvider;
    }

    public async Task<AIAnalysisJobLeaseResult> ExecuteAsync(
        AIAnalysisJobWorkItem workItem,
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!_tenantContext.IsResolved
            || _tenantContext.UserId is not null
            || _tenantContext.CenterId != workItem.CenterId)
        {
            return Result(workItem, AIAnalysisJobLeaseOutcome.NotFound);
        }

        var job = await _dbContext.AIAnalysisJobs
            .SingleOrDefaultAsync(
                candidate => candidate.AnalysisJobId == workItem.AnalysisJobId,
                cancellationToken);

        if (job is null || job.CenterId != workItem.CenterId)
        {
            return Result(workItem, AIAnalysisJobLeaseOutcome.NotFound);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var transition = workItem.Kind switch
        {
            AIAnalysisJobWorkKind.Claim => _stateMachine.Claim(
                job,
                utcNow,
                workerId,
                utcNow.Add(leaseDuration)),
            AIAnalysisJobWorkKind.RecoverExpiredLease =>
                _stateMachine.RecoverExpiredLease(job, utcNow),
            _ => throw new ArgumentOutOfRangeException(nameof(workItem), workItem.Kind, null)
        };

        if (transition != AIAnalysisJobTransitionResult.Success)
        {
            _dbContext.ChangeTracker.Clear();
            return Result(workItem, AIAnalysisJobLeaseOutcome.NotEligible);
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();
            return Result(workItem, AIAnalysisJobLeaseOutcome.LostRace);
        }

        return Result(
            workItem,
            workItem.Kind == AIAnalysisJobWorkKind.Claim
                ? AIAnalysisJobLeaseOutcome.Claimed
                : AIAnalysisJobLeaseOutcome.Recovered);
    }

    private static AIAnalysisJobLeaseResult Result(
        AIAnalysisJobWorkItem workItem,
        AIAnalysisJobLeaseOutcome outcome) =>
        new(workItem.AnalysisJobId, workItem.CenterId, outcome);
}
