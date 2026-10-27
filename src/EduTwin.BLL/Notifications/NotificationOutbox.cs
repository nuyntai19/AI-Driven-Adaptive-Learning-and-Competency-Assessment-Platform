using System.Text.Json;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Notifications;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Notifications;

// Internal-only. Stages entities in the caller's unit of work; NEVER saves or commits here.
public sealed class NotificationOutbox(EduTwinDbContext db, ITenantContext tenant, TimeProvider clock)
{
    public async Task<Guid> StageNoticeAsync(string key, string title, string body,
        IReadOnlyCollection<Guid> recipientIds, CancellationToken ct = default)
    {
        if (!tenant.IsResolved || tenant.CenterId is not { } center || center == Guid.Empty)
            throw new InvalidOperationException("Notification tenant is unresolved.");
        if (string.IsNullOrWhiteSpace(key) || key.Length > 160 || string.IsNullOrWhiteSpace(title) || title.Length > 160 ||
            string.IsNullOrWhiteSpace(body) || body.Length > 2000 || recipientIds.Count is < 1 or > 500 ||
            recipientIds.Any(x => x == Guid.Empty))
            throw new ArgumentException("Invalid notification envelope.");
        var ids = recipientIds.Distinct().Order().ToArray();
        // Validate every recipient within the current tenant, not just the first.
        // Bounded individual equality predicates avoid provider-specific JSON GUID list translation.
        var activeIds = await db.Users.AsNoTracking().Where(NotificationRecipientScope.Matches(ids)).Where(u => u.Status == UserStatus.Active)
            .Select(u => u.UserId).ToArrayAsync(ct);
        if (ids.Any(id => !activeIds.Contains(id))) throw new ArgumentException("Recipient is outside active tenant scope.");
        var existing = db.NotificationEvents.Local.FirstOrDefault(x => x.CenterId == center && x.DeduplicationKey == key)
            ?? await db.NotificationEvents.SingleOrDefaultAsync(x => x.DeduplicationKey == key, ct);
        if (existing is not null)
        {
            // JSON columns may normalize whitespace; recipient identity is independent of JSON formatting and order.
            var existingIds = JsonSerializer.Deserialize<Guid[]>(existing.RecipientUserIds);
            if (existing.Title != title.Trim() || existing.Body != body.Trim() ||
                existingIds is null || !ids.ToHashSet().SetEquals(existingIds))
                throw new InvalidOperationException("Notification deduplication key reused for a different envelope.");
            return existing.EventId;
        }
        var now = clock.GetUtcNow().UtcDateTime;
        var id = Guid.NewGuid();
        db.NotificationEvents.Add(new() { EventId = id, CenterId = center, DeduplicationKey = key,
            Title = title.Trim(), Body = body.Trim(), RecipientUserIds = JsonSerializer.Serialize(ids),
            CreatedAt = now, CreatedBy = tenant.UserId });
        db.NotificationDispatches.Add(new() { EventId = id, CenterId = center, AvailableAt = now });
        return id;
    }
}
