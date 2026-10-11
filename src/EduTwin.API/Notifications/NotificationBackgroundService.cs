using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Notifications;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace EduTwin.API.Notifications;

public sealed class NotificationWorkerOptions
{
    public bool Enabled { get; set; } = true;
    public int PollSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 20;
}

public sealed class NotificationBackgroundService(IServiceScopeFactory scopes, TimeProvider clock,
    Microsoft.Extensions.Options.IOptions<NotificationWorkerOptions> options,
    ILogger<NotificationBackgroundService> logger, EduTwin.BLL.Platform.Operations.OperationalTelemetry? telemetry = null) : BackgroundService
{
    private static readonly Meter Metrics = new("EduTwin.Notifications", "1.0");
    private static readonly Histogram<double> DispatchTime = Metrics.CreateHistogram<double>("edutwin.notifications.dispatch.duration", "ms");
    private static readonly Histogram<double> DeliveryLag = Metrics.CreateHistogram<double>("edutwin.notifications.delivery.lag", "ms");
    private static readonly Counter<long> Delivered = Metrics.CreateCounter<long>("edutwin.notifications.delivered");
    private static readonly Counter<long> Suppressed = Metrics.CreateCounter<long>("edutwin.notifications.suppressed");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunBatchAsync(stoppingToken); telemetry?.Beat("notifications"); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Notification batch failed: {ErrorType}", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(options.Value.PollSeconds, 1, 60)), clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public async Task RunBatchAsync(CancellationToken ct)
    {
        await using var discovery = scopes.CreateAsyncScope();
        var db = discovery.ServiceProvider.GetRequiredService<EduTwinDbContext>();
        var now = clock.GetUtcNow().UtcDateTime;
        // Controlled background-only cross-tenant discovery. Payloads never leave the tenant scope.
        var candidates = db.NotificationDispatches.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.AvailableAt <= now && (d.State == "Pending" || d.State == "Processing" && d.LeaseUntil <= now) &&
                db.Centers.Any(c => c.CenterId == d.CenterId && !c.IsDeleted && c.Status == CenterStatus.Active));
        var centers = await candidates.GroupBy(x => x.CenterId).Select(g => new { CenterId = g.Key, Oldest = g.Min(x => x.AvailableAt) })
            .OrderBy(x => x.Oldest).ThenBy(x => x.CenterId).Take(Math.Clamp(options.Value.BatchSize, 1, 100)).ToArrayAsync(ct);
        foreach (var center in centers)
        {
            await using var scope = scopes.CreateAsyncScope();
            using var tenant = scope.ServiceProvider.GetRequiredService<IBackgroundTenantScopeFactory>().BeginScope(center.CenterId);
            var scopedDb = scope.ServiceProvider.GetRequiredService<EduTwinDbContext>();
            var next = await scopedDb.NotificationDispatches.AsNoTracking()
                .Where(x => x.AvailableAt <= now && (x.State == "Pending" || x.State == "Processing" && x.LeaseUntil <= now))
                .OrderBy(x => x.AvailableAt).ThenBy(x => x.EventId).Select(x => (Guid?)x.EventId).FirstOrDefaultAsync(ct);
            if (next is { } eventId)
            {
                var started = Stopwatch.GetTimestamp();
                var completed = await scope.ServiceProvider.GetRequiredService<NotificationDispatcher>().DispatchAsync(eventId, ct);
                var metadata = await (from d in scopedDb.NotificationDispatches.AsNoTracking()
                    join e in scopedDb.NotificationEvents.AsNoTracking() on d.EventId equals e.EventId
                    where d.EventId == eventId
                    select new { d.State, d.AttemptCount, d.DeliveredCount, d.SuppressedCount, d.LastErrorCode, e.CreatedAt }).SingleOrDefaultAsync(ct);
                if (metadata == null) continue;
                var duration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                var lag = Math.Max(0, (clock.GetUtcNow().UtcDateTime - metadata.CreatedAt).TotalMilliseconds);
                DispatchTime.Record(duration);
                if (completed && metadata.State == "Completed")
                {
                    Delivered.Add(metadata.DeliveredCount); Suppressed.Add(metadata.SuppressedCount);
                    if (metadata.DeliveredCount > 0) DeliveryLag.Record(lag);
                }
                // IDs/counts/state only. Never log body, recipient snapshot, raw audit or provider data.
                logger.LogInformation("Notification dispatch tenant={CenterId} event={EventId} state={State} attempts={Attempts} delivered={Delivered} suppressed={Suppressed} code={Code} elapsedMs={ElapsedMs} lagMs={LagMs}",
                    center.CenterId, eventId, metadata.State, metadata.AttemptCount, metadata.DeliveredCount,
                    metadata.SuppressedCount, metadata.LastErrorCode, duration, lag);
            }
        }
    }
}
