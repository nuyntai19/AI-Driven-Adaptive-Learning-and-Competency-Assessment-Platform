using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning.Jobs;

public sealed class AIAnalysisJobCandidateDiscovery : IAIAnalysisJobCandidateDiscovery
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IBackgroundTenantScopeFactory _tenantScopeFactory;
    private readonly TimeProvider _timeProvider;

    public AIAnalysisJobCandidateDiscovery(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IBackgroundTenantScopeFactory tenantScopeFactory,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _tenantScopeFactory = tenantScopeFactory;
        _timeProvider = timeProvider;
    }

    public async Task<AIAnalysisJobDiscoveryResult> DiscoverAsync(
        int perCenterBatchSize,
        int totalBatchSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(perCenterBatchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalBatchSize);

        cancellationToken.ThrowIfCancellationRequested();
        if (_tenantContext.IsResolved || _tenantContext.UserId is not null)
        {
            throw new InvalidOperationException(
                "Candidate discovery must start in an unresolved background tenant scope.");
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var centerIds = await _dbContext.Centers
            .AsNoTracking()
            .Where(center => !center.IsDeleted)
            .OrderBy(center => center.CenterId)
            .Select(center => center.CenterId)
            .ToListAsync(cancellationToken);

        var candidates = new List<AIAnalysisJobWorkItem>();
        foreach (var centerId in centerIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var tenantScope = _tenantScopeFactory.BeginScope(centerId);
            if (_tenantContext.CenterId != centerId || _tenantContext.UserId is not null)
            {
                throw new InvalidOperationException("Background tenant scope was not established safely.");
            }

            var rows = await _dbContext.AIAnalysisJobs
                .AsNoTracking()
                .Where(job =>
                    (job.Status == AIJobStatus.Pending && job.AvailableAt <= utcNow)
                    || (job.Status == AIJobStatus.Processing
                        && job.LeaseUntil.HasValue
                        && job.LeaseUntil.Value < utcNow))
                .Select(job => new
                {
                    job.AnalysisJobId,
                    job.AttemptId,
                    job.CenterId,
                    job.CorrelationId,
                    job.Status,
                    EligibleAt = job.Status == AIJobStatus.Pending
                        ? job.AvailableAt
                        : job.LeaseUntil!.Value
                })
                .OrderBy(job => job.EligibleAt)
                .ThenBy(job => job.AnalysisJobId)
                .Take(perCenterBatchSize)
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                if (row.CenterId != centerId)
                {
                    throw new InvalidOperationException(
                        "Candidate center does not match the active discovery tenant scope.");
                }

                candidates.Add(new AIAnalysisJobWorkItem(
                    row.AnalysisJobId,
                    row.AttemptId,
                    row.CenterId,
                    row.CorrelationId,
                    row.Status == AIJobStatus.Pending
                        ? AIAnalysisJobWorkKind.Claim
                        : AIAnalysisJobWorkKind.RecoverExpiredLease,
                    row.EligibleAt));
            }

            if (candidates.Count > totalBatchSize)
            {
                candidates = candidates
                    .OrderBy(candidate => candidate.EligibleAt)
                    .ThenBy(candidate => candidate.AnalysisJobId)
                    .Take(totalBatchSize)
                    .ToList();
            }
        }

        var workItems = candidates
            .OrderBy(candidate => candidate.EligibleAt)
            .ThenBy(candidate => candidate.AnalysisJobId)
            .Take(totalBatchSize)
            .ToArray();

        return new AIAnalysisJobDiscoveryResult(workItems);
    }
}
