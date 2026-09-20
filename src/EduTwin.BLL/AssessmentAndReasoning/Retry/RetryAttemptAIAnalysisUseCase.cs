using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.AssessmentAndReasoning.Retry;

public sealed class RetryAttemptAIAnalysisUseCase : IRetryAttemptAIAnalysisUseCase
{
    private const byte MaxManualRetries = 3;
    private const int CooldownSeconds = 30;

    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IStudentOwnershipGuard _studentOwnershipGuard;
    private readonly TimeProvider _timeProvider;

    public RetryAttemptAIAnalysisUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IStudentOwnershipGuard studentOwnershipGuard,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _studentOwnershipGuard = studentOwnershipGuard ?? throw new ArgumentNullException(nameof(studentOwnershipGuard));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RetryAIAnalysisResult> ExecuteAsync(ulong attemptId, CancellationToken cancellationToken)
    {
        if (attemptId == 0)
        {
            return RetryAIAnalysisResult.NotFound();
        }

        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue ||
            _tenantContext.UserId.Value == Guid.Empty)
        {
            return RetryAIAnalysisResult.NotFound();
        }

        var centerId = _tenantContext.CenterId.Value;
        var currentUserId = _tenantContext.UserId.Value;
        var currentUserRole = _tenantContext.Role;

        var attempt = await _dbContext.Attempts
            .Where(a => a.CenterId == centerId && a.AttemptId == attemptId)
            .FirstOrDefaultAsync(cancellationToken);

        if (attempt == null)
        {
            return RetryAIAnalysisResult.NotFound();
        }

        // Check ownership
        if (currentUserRole == nameof(UserRole.Student))
        {
            if (attempt.StudentId != currentUserId)
            {
                return RetryAIAnalysisResult.Forbidden();
            }
        }
        else
        {
            var accessDecision = await _studentOwnershipGuard.CheckStudentAccessAsync(attempt.StudentId, cancellationToken);
            if (accessDecision == OwnershipDecision.NotFound)
            {
                return RetryAIAnalysisResult.NotFound();
            }
            if (accessDecision == OwnershipDecision.Forbidden)
            {
                return RetryAIAnalysisResult.Forbidden();
            }
        }

        // Quota check (max 3 manual retries)
        if (attempt.ManualRetryCount >= MaxManualRetries)
        {
            return RetryAIAnalysisResult.QuotaExceeded();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Cooldown check (30 seconds between manual retries)
        if (attempt.LastManualRetryAt.HasValue)
        {
            var elapsedSeconds = (now - attempt.LastManualRetryAt.Value).TotalSeconds;
            if (elapsedSeconds < CooldownSeconds)
            {
                var remaining = (int)Math.Ceiling(CooldownSeconds - elapsedSeconds);
                return RetryAIAnalysisResult.CooldownActive(remaining);
            }
        }

        // Apply retry
        attempt.ManualRetryCount += 1;
        attempt.LastManualRetryAt = now;
        attempt.Status = AttemptStatus.PendingAnalysis;
        attempt.UpdatedAt = now;

        var job = await _dbContext.AIAnalysisJobs
            .Where(j => j.CenterId == centerId && j.AttemptId == attemptId)
            .OrderByDescending(j => j.AnalysisJobId)
            .FirstOrDefaultAsync(cancellationToken);

        if (job == null)
        {
            job = new AIAnalysisJob
            {
                CenterId = centerId,
                AttemptId = attemptId,
                Status = AIJobStatus.Pending,
                RetryCount = 0,
                AvailableAt = now,
                CorrelationId = Guid.NewGuid().ToString("N"),
                CreatedAt = now,
                CreatedBy = currentUserId,
                UpdatedAt = now
            };
            _dbContext.AIAnalysisJobs.Add(job);
        }
        else
        {
            job.Status = AIJobStatus.Pending;
            job.RetryCount = 0;
            job.AvailableAt = now;
            job.LastErrorCode = null;
            job.LastErrorMessage = null;
            job.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var retriesRemaining = (byte)Math.Max(0, MaxManualRetries - attempt.ManualRetryCount);
        var nextAllowedAt = now.AddSeconds(CooldownSeconds);

        var data = new RetryAIAnalysisDataDto
        {
            AttemptId = attempt.AttemptId.ToString(CultureInfo.InvariantCulture),
            JobId = job.AnalysisJobId.ToString(CultureInfo.InvariantCulture),
            ManualRetriesUsed = attempt.ManualRetryCount,
            ManualRetriesRemaining = retriesRemaining,
            NextRetryAllowedAt = nextAllowedAt,
            CooldownRemainingSeconds = CooldownSeconds
        };

        return RetryAIAnalysisResult.Success(data);
    }
}
