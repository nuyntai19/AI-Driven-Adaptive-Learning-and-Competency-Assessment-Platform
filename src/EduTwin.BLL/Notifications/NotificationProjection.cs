using EduTwin.DAL.Notifications;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Notifications;

// Only IDs from OWN deliveries or OWN sent messages may enter this projection.
// Relay data is read by authorized message ID, never by an arbitrary foreign center.
internal static class NotificationProjection
{
    public static async Task<(Dictionary<Guid, NotificationMessage> Messages, Dictionary<Guid, NotificationMessageAction> Actions)>
        LoadAsync(EduTwinDbContext db, IEnumerable<Guid> authorizedIds, CancellationToken ct)
    {
        var ids = authorizedIds.Distinct().ToArray();
        if (ids.Length == 0) return (new(), new());
        var messages = await NotificationRecipientScope.WhereIds(db.NotificationMessages.IgnoreQueryFilters().AsNoTracking(), x => x.MessageId, ids).ToArrayAsync(ct);
        var actions = await NotificationRecipientScope.WhereIds(db.NotificationMessageActions.IgnoreQueryFilters().AsNoTracking(), x => x.MessageId, ids).ToArrayAsync(ct);
        return (messages.ToDictionary(x => x.MessageId), actions.ToDictionary(x => x.MessageId));
    }
}
