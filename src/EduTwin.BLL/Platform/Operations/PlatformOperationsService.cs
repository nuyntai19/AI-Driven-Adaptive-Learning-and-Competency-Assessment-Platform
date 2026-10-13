using System.Text.Json;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Platform;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Platform.Operations;

public interface IPlatformOperationsService
{
    Task<PlatformResult<PlatformOperationsData>> OperationsAsync(Guid? centerId, CancellationToken ct);
    Task<PlatformResult<PlatformAiUsageData>> AiUsageAsync(int days, CancellationToken ct);
    Task<PlatformResult<PlatformStorageData>> StorageAsync(CancellationToken ct);
}

public sealed class PlatformOperationsService(EduTwinDbContext db, ITenantContext tenant,
    OperationalTelemetry telemetry, OperationsQueryCache cache, TimeProvider clock) : IPlatformOperationsService
{
    private bool Allowed => tenant.IsResolved && tenant.Role == nameof(UserRole.PlatformAdmin) && tenant.UserId is not null && tenant.UserId != Guid.Empty &&
        tenant.CenterId == AuthorizationBootstrapper.ReservedPlatformCenterId && db.CurrentTenantId == tenant.CenterId;

    public async Task<PlatformResult<PlatformOperationsData>> OperationsAsync(Guid? centerId, CancellationToken ct)
    {
        if (!Allowed) return PlatformResult<PlatformOperationsData>.Failure(ErrorCodes.ForbiddenResource);
        if (centerId == Guid.Empty) return PlatformResult<PlatformOperationsData>.Failure(ErrorCodes.ValidationFailed);
        var data = await cache.GetAsync("queue:" + centerId, async () =>
        {
            var centers = await db.Centers.AsNoTracking().Where(x => !x.IsDeleted && x.CenterId != AuthorizationBootstrapper.ReservedPlatformCenterId)
                .OrderBy(x => x.CenterName).Select(x => new OpsCenter(x.CenterId, x.CenterName)).ToArrayAsync(ct);
            var eligible = db.Centers.AsNoTracking().Where(x => !x.IsDeleted && x.CenterId != AuthorizationBootstrapper.ReservedPlatformCenterId);
            if (centerId.HasValue) eligible = eligible.Where(x => x.CenterId == centerId.Value);
            var ids = eligible.Select(x => x.CenterId);
            // Root-only aggregate exemption: explicit eligible center subquery; no UUID-array parameters or academic navigations.
            var jobs = db.AIAnalysisJobs.IgnoreQueryFilters().AsNoTracking().Where(x => ids.Contains(x.CenterId));
            var counts = await jobs.GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToArrayAsync(ct);
            var oldest = await jobs.Where(x => x.Status == AIJobStatus.Pending).Select(x => (DateTime?)x.CreatedAt).MinAsync(ct);
            var now = clock.GetUtcNow().UtcDateTime;
            var expired = await jobs.CountAsync(x => x.Status == AIJobStatus.Processing && x.LeaseUntil != null && x.LeaseUntil < now, ct);
            var recent = await jobs.Where(x => x.Status == AIJobStatus.Pending || x.Status == AIJobStatus.Processing || x.Status == AIJobStatus.FailedTerminal)
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.AnalysisJobId).Take(20)
                .Select(x => new { x.AnalysisJobId, x.CenterId, x.Status, x.RetryCount, x.CreatedAt, x.AvailableAt, x.StartedAt, x.LeaseUntil, x.LastErrorCode }).ToArrayAsync(ct);
            var alerts = await db.PlatformOpsAlerts.AsNoTracking().Where(x => x.IsActive)
                .Select(x => new OpsAlertDto(x.AlertKey, x.FirstSeenAt, x.NotifiedAt)).ToArrayAsync(ct);
            return new PlatformOperationsData(now, true, telemetry.Snapshot(), centers, counts.Select(x => new OpsCount(x.Status.ToString(), x.Count)).ToArray(),
                oldest.HasValue ? Math.Max(0, (now - oldest.Value).TotalSeconds) : null, expired,
                recent.Select(x => new OpsJob(x.AnalysisJobId.ToString(), x.CenterId, x.Status.ToString(), x.RetryCount, x.CreatedAt, x.AvailableAt, x.StartedAt, x.LeaseUntil,
                    SafeError(x.LastErrorCode))).ToArray(), alerts);
        }, ct);
        return PlatformResult<PlatformOperationsData>.Success(data);
    }

    public async Task<PlatformResult<PlatformAiUsageData>> AiUsageAsync(int days, CancellationToken ct)
    {
        if (!Allowed) return PlatformResult<PlatformAiUsageData>.Failure(ErrorCodes.ForbiddenResource);
        if (days is not (1 or 7 or 30)) return PlatformResult<PlatformAiUsageData>.Failure(ErrorCodes.ValidationFailed);
        var data = await cache.GetAsync("usage:" + days, async () =>
        {
            var until = clock.GetUtcNow().UtcDateTime; var from = until.AddDays(-days);
            var start = await db.PlatformAiCalls.AsNoTracking().Select(x => (DateTime?)x.StartedAt).MinAsync(ct);
            var groups = await db.PlatformAiCalls.AsNoTracking().Where(x => x.StartedAt >= from && x.StartedAt <= until)
                .GroupBy(x => new { Day = x.StartedAt.Date, x.Provider, x.Model, x.Stage })
                .OrderBy(g => g.Key.Day)
                .Select(g => new AiUsageGroup(g.Key.Day, g.Key.Provider, g.Key.Model, g.Key.Stage, g.Count(), g.Count(x => x.Outcome != "success"),
                    g.Count(x => x.TotalTokens != null), g.Count(x => x.InputTokens != null) == 0 ? null : g.Sum(x => x.InputTokens),
                    g.Count(x => x.OutputTokens != null) == 0 ? null : g.Sum(x => x.OutputTokens),
                    g.Count(x => x.ThinkingTokens != null) == 0 ? null : g.Sum(x => x.ThinkingTokens),
                    g.Count(x => x.CachedTokens != null) == 0 ? null : g.Sum(x => x.CachedTokens),
                    g.Count(x => x.TotalTokens != null) == 0 ? null : g.Sum(x => x.TotalTokens), g.Count(x => x.EstimatedUsd != null),
                    g.Count(x => x.EstimatedUsd != null) == 0 ? null : g.Sum(x => x.EstimatedUsd), g.Average(x => (double)x.DurationMs)))
                .ToArrayAsync(ct);
            var beat = telemetry.Snapshot();
            return new PlatformAiUsageData(from, until, start, beat.DroppedCalls, beat.ProcessStartedAt, groups);
        }, ct);
        return PlatformResult<PlatformAiUsageData>.Success(data);
    }

    public async Task<PlatformResult<PlatformStorageData>> StorageAsync(CancellationToken ct)
    {
        if (!Allowed) return PlatformResult<PlatformStorageData>.Failure(ErrorCodes.ForbiddenResource);
        var snapshot = await db.PlatformOpsSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.SnapshotKey == "storage", ct);
        var data = snapshot is null ? new(null, [], null, null, new BackupObservation()) : JsonSerializer.Deserialize<PlatformStorageData>(snapshot.Payload);
        return PlatformResult<PlatformStorageData>.Success(data ?? new(null, [], null, null, new BackupObservation()));
    }

    public static string? SafeError(string? code) => code is null ? null : code is
        "AI_PROVIDER_CAPACITY_WAIT" or "AI_PROVIDER_DAILY_QUOTA_WAIT" or "AI_ANALYSIS_ATTEMPT_FAILED" or "AI_PROCESSING_DEFERRED" or
        "ATTACHMENT_STORAGE_UNAVAILABLE" or "AttachmentStorageUnavailable" ? code : "OPERATION_FAILED";
}
