using System.Globalization;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Platform;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Notifications;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Notifications;

public sealed class NotificationOperationsService(EduTwinDbContext db, ITenantContext tenant, TimeProvider clock)
{
    private bool Identity => tenant.IsResolved && tenant.CenterId == db.CurrentTenantId && tenant.UserId is { } id && id != Guid.Empty &&
        (tenant.Role == nameof(UserRole.CenterManager) || tenant.Role == nameof(UserRole.PlatformAdmin) && tenant.CenterId == AuthorizationBootstrapper.ReservedPlatformCenterId);
    public async Task<NotificationResult<NotificationOperationsPage>> ListAsync(int page, int pageSize, string? state, CancellationToken ct)
    {
        if (!Identity) return new(null, ErrorCodes.ResourceNotFound);
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > 100_000 ||
            state != null && state is not ("Pending" or "Processing" or "Completed" or "DeadLetter")) return new(null, ErrorCodes.ValidationFailed);
        var all = db.NotificationDispatches.AsNoTracking();
        var query = state == null ? all : all.Where(d => d.State == state);
        var total = await query.LongCountAsync(ct);
        var rows = await (from d in query join e in db.NotificationEvents.AsNoTracking() on d.EventId equals e.EventId
            orderby e.CreatedAt descending, e.EventId
            select new { e.EventId, e.EventType, d.State, e.CreatedAt, d.AvailableAt, d.AttemptCount, d.DeliveredCount, d.SuppressedCount, d.LastErrorCode, d.RowVersion })
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        var counts = await all.GroupBy(d => d.State).Select(g => new { State = g.Key, Count = g.LongCount(), Delivered = g.Sum(d => (long)d.DeliveredCount), Suppressed = g.Sum(d => (long)d.SuppressedCount) }).ToArrayAsync(ct);
        var oldest = await (from d in all.Where(d => d.State == "Pending" || d.State == "Processing")
            join e in db.NotificationEvents.AsNoTracking() on d.EventId equals e.EventId select (DateTime?)e.CreatedAt).MinAsync(ct);
        long Count(string value) => counts.SingleOrDefault(c => c.State == value)?.Count ?? 0;
        return new(new(rows.Select(r => new NotificationDispatchDto(r.EventId, r.EventType, r.State, r.CreatedAt, r.AvailableAt,
            r.AttemptCount, r.DeliveredCount, r.SuppressedCount, r.LastErrorCode, r.RowVersion.ToString(CultureInfo.InvariantCulture))).ToArray(), total,
            new(Count("Pending"), Count("Processing"), Count("DeadLetter"), Count("Completed"), counts.Sum(c => c.Delivered), counts.Sum(c => c.Suppressed), oldest)));
    }
    public async Task<string?> RedriveAsync(Guid eventId, NotificationRedriveRequest request, CancellationToken ct)
    {
        if (!Identity) return ErrorCodes.ResourceNotFound;
        if (eventId == Guid.Empty || request.ClientActionId == Guid.Empty || !ulong.TryParse(request.ExpectedRowVersion, out var expected) ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500) return ErrorCodes.ValidationFailed;
        var action = request.ClientActionId.ToString("D");
        var prior = await db.AuthorizationAuditLogs.AsNoTracking().SingleOrDefaultAsync(a => a.ActionType == "NotificationRedriven" && a.TraceId == action && a.ActorUserId == tenant.UserId, ct);
        if (prior != null) return prior.TargetId == eventId.ToString("D") ? null : ErrorCodes.ConcurrencyConflict;
        var row = await db.NotificationDispatches.SingleOrDefaultAsync(d => d.EventId == eventId, ct);
        if (row is null) return ErrorCodes.ResourceNotFound;
        if (row.State != "DeadLetter" || row.RowVersion != expected) return ErrorCodes.ConcurrencyConflict;
        var now = clock.GetUtcNow().UtcDateTime;
        row.State = "Pending"; row.AttemptCount = 0; row.AvailableAt = now; row.LeaseToken = null; row.LeaseUntil = null; row.LastErrorCode = null;
        db.AuthorizationAuditLogs.Add(new() { CenterId = tenant.CenterId!.Value, ActorUserId = tenant.UserId, CreatedBy = tenant.UserId,
            ActionType = "NotificationRedriven", TargetType = "NotificationDispatch", TargetId = eventId.ToString("D"),
            BeforeData = "{\"State\":\"DeadLetter\"}", AfterData = "{\"State\":\"Pending\"}",
            Reason = PlatformAuditSanitizer.SanitizeReason(request.Reason.Trim()), TraceId = action, CreatedAt = now });
        try { await db.SaveChangesAsync(ct); return null; }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return ErrorCodes.ConcurrencyConflict; }
    }
}
