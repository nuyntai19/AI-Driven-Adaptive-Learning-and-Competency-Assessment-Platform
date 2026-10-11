using System.Globalization;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Notifications;
using EduTwin.DAL.Notifications;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Notifications;

public sealed record NotificationResult<T>(T? Data, string? ErrorCode = null)
{
    public bool IsSuccess => ErrorCode is null;
}
public sealed record NotificationPage(IReadOnlyList<NotificationDto> Items, long TotalItems);

public sealed class NotificationInboxService(EduTwinDbContext db, ITenantContext tenant, TimeProvider clock)
{
    private bool HasIdentity => tenant.IsResolved && tenant.CenterId is { } c && c != Guid.Empty &&
        tenant.UserId is { } u && u != Guid.Empty;

    private IQueryable<NotificationDelivery> Own() => db.NotificationDeliveries
        .Where(x => x.UserId == tenant.UserId && x.CenterId == tenant.CenterId);

    public async Task<NotificationResult<NotificationSummaryDto>> SummaryAsync(CancellationToken ct)
    {
        if (!HasIdentity) return new(null, ErrorCodes.ResourceNotFound);
        return new(new(await Own().LongCountAsync(x => x.ReadAt == null && x.HiddenAt == null, ct)));
    }

    public async Task<NotificationResult<NotificationPage>> ListAsync(NotificationListQuery query, CancellationToken ct)
    {
        if (!HasIdentity) return new(null, ErrorCodes.ResourceNotFound);
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue)
            return new(null, ErrorCodes.ValidationFailed);
        if (query.Priority is not (null or "Normal" or "Important" or "Urgent") || query.Category is not (null or "Information" or "Request" or "System") ||
            query.From > query.Until || query.From is { Kind: not DateTimeKind.Utc } || query.Until is { Kind: not DateTimeKind.Utc }) return new(null, ErrorCodes.ValidationFailed);
        var rows = Own().AsNoTracking().Where(x => x.HiddenAt == null);
        if (query.UnreadOnly) rows = rows.Where(x => x.ReadAt == null);
        if (query.From is { } from) rows = rows.Where(x => x.DeliveredAt >= from);
        if (query.Until is { } until) rows = rows.Where(x => x.DeliveredAt <= until);
        // Correlated exclusively with an OWN delivery; the relay message belongs to its source tenant.
        var messages = db.NotificationMessages.IgnoreQueryFilters();
        var actions = db.NotificationMessageActions.IgnoreQueryFilters();
        if (query.Priority is { } priority) rows = rows.Where(x => x.Event.MessageId == null ? priority == "Normal" : messages.Any(m => m.MessageId == x.Event.MessageId && m.Priority == priority));
        if (query.Category is { } category) rows = rows.Where(x => x.Event.MessageId == null ? category == "System" : messages.Any(m => m.MessageId == x.Event.MessageId && m.Category == category));
        if (query.NeedsAcknowledgement) rows = rows.Where(x => x.AcknowledgedAt == null && messages.Any(m => m.MessageId == x.Event.MessageId && m.RequiresAcknowledgement) &&
            !actions.Any(a => a.MessageId == x.Event.MessageId));
        var count = await rows.LongCountAsync(ct);
        var items = await rows.Include(x => x.Event).OrderByDescending(x => x.DeliveryId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return new(new(await MapAsync(items, ct), count));
    }

    public async Task<NotificationResult<NotificationDto>> DetailAsync(string id, CancellationToken ct)
    {
        if (!HasIdentity) return new(null, ErrorCodes.ResourceNotFound);
        if (!TryId(id, out var number)) return new(null, ErrorCodes.ValidationFailed);
        var row = await Own().AsNoTracking().Include(x => x.Event).SingleOrDefaultAsync(x => x.DeliveryId == number, ct);
        return row is null ? new(null, ErrorCodes.ResourceNotFound) : new((await MapAsync([row], ct))[0]);
    }

    public async Task<string?> ReadAsync(IReadOnlyList<string>? ids, CancellationToken ct)
    {
        if (!HasIdentity) return ErrorCodes.ResourceNotFound;
        if (ids is null || ids.Count is < 1 or > 100) return ErrorCodes.ValidationFailed;
        var numbers = new HashSet<ulong>();
        foreach (var id in ids)
        {
            if (!TryId(id, out var number) || !numbers.Add(number)) return ErrorCodes.ValidationFailed;
        }
        // Parameterized scalar IDs (the MySQL provider cannot bind primitive Guid arrays).
        var rows = await Own().Where(x => numbers.Contains(x.DeliveryId)).ToArrayAsync(ct);
        if (rows.Length != numbers.Count) return ErrorCodes.ResourceNotFound;
        var now = clock.GetUtcNow().UtcDateTime;
        if (db.Database.IsRelational())
            await Own().Where(x => numbers.Contains(x.DeliveryId) && x.ReadAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ReadAt, now), ct);
        else
        {
            foreach (var row in rows) row.ReadAt ??= now;
            await db.SaveChangesAsync(ct);
        }
        return null;
    }

    private static bool TryId(string? id, out ulong value) =>
        ulong.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;
    private async Task<NotificationDto[]> MapAsync(NotificationDelivery[] rows, CancellationToken ct)
    {
        var (messages, actions) = await NotificationProjection.LoadAsync(db, rows.Where(x => x.Event.MessageId != null).Select(x => x.Event.MessageId!.Value), ct);
        var ids = rows.Select(x => x.DeliveryId).ToArray();
        var replies = await db.NotificationReplies.AsNoTracking().Where(r => ids.Contains(r.DeliveryId) && r.CreatedBy == tenant.UserId).ToArrayAsync(ct);
        var links = await NotificationBusinessScope.LinksAsync(db, rows.Select(x => x.Event), tenant, ct);
        return rows.Select(x => {
            var message = x.Event.MessageId is { } id ? messages.GetValueOrDefault(id) : null;
            var action = x.Event.MessageId is { } mid ? actions.GetValueOrDefault(mid) : null;
            var active = action is null;
            return new NotificationDto(x.DeliveryId.ToString(CultureInfo.InvariantCulture), x.Event.EventType,
                active ? x.Event.Title : action!.Kind == "Retracted" ? "Thông báo đã được thu hồi" : "Thông báo đã được đính chính",
                active ? x.Event.Body : "Nội dung gốc không còn áp dụng. " + action!.Reason,
                x.DeliveredAt, x.ReadAt, x.Event.SenderName, message?.Priority ?? "Normal", message?.Category ?? "System",
                active && message?.RequiresAcknowledgement == true, active && message?.AllowReply == true, x.AcknowledgedAt,
                action?.Kind ?? "Active", action?.Reason, action?.ReplacementMessageId, replies.SingleOrDefault(r => r.DeliveryId == x.DeliveryId)?.Body,
                active ? links.GetValueOrDefault(x.EventId) : null);
        }).ToArray();
    }

    public async Task<string?> HideAsync(string id, CancellationToken ct)
    {
        var row = await OwnRowAsync(id, ct);
        if (row is null) return ErrorCodes.ResourceNotFound;
        var now = clock.GetUtcNow().UtcDateTime;
        if (db.Database.IsRelational()) await Own().Where(x => x.DeliveryId == row.DeliveryId && x.HiddenAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.HiddenAt, now), ct);
        else { row.HiddenAt ??= now; await db.SaveChangesAsync(ct); }
        return null;
    }
    public async Task<string?> AcknowledgeAsync(string id, CancellationToken ct)
    {
        var row = await OwnRowAsync(id, ct);
        if (row is null) return ErrorCodes.ResourceNotFound;
        var (messages, actions) = await NotificationProjection.LoadAsync(db, row.Event.MessageId is { } mid ? [mid] : [], ct);
        if (row.Event.MessageId is not { } messageId || messages.GetValueOrDefault(messageId)?.RequiresAcknowledgement != true) return ErrorCodes.ValidationFailed;
        if (actions.ContainsKey(messageId)) return ErrorCodes.ConcurrencyConflict;
        var now = clock.GetUtcNow().UtcDateTime;
        if (db.Database.IsRelational()) await Own().Where(x => x.DeliveryId == row.DeliveryId && x.AcknowledgedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.AcknowledgedAt, now), ct);
        else { row.AcknowledgedAt ??= now; await db.SaveChangesAsync(ct); }
        return null;
    }
    private async Task<NotificationDelivery?> OwnRowAsync(string id, CancellationToken ct) => HasIdentity && TryId(id, out var number)
        ? await Own().Include(x => x.Event).SingleOrDefaultAsync(x => x.DeliveryId == number, ct) : null;

    public async Task<NotificationResult<NotificationReplyDto>> ReplyAsync(string id, ReplyNotificationRequest request, CancellationToken ct)
    {
        if (request.ClientReplyId == Guid.Empty || string.IsNullOrWhiteSpace(request.Body) || request.Body.Length > 2000) return new(null, ErrorCodes.ValidationFailed);
        var row = await OwnRowAsync(id, ct);
        if (row is null) return new(null, ErrorCodes.ResourceNotFound);
        var (messages, actions) = await NotificationProjection.LoadAsync(db, row.Event.MessageId is { } mid ? [mid] : [], ct);
        if (row.Event.MessageId is not { } messageId || messages.GetValueOrDefault(messageId) is not { AllowReply: true } source) return new(null, ErrorCodes.ValidationFailed);
        if (NotificationContentPolicy.IsRestricted(NotificationContentPolicy.StoredAudience(source), request.Body)) return new(null, NotificationContentPolicy.Restricted);
        var previous = await db.NotificationReplies.AsNoTracking().SingleOrDefaultAsync(x => x.DeliveryId == row.DeliveryId && x.CreatedBy == tenant.UserId, ct);
        if (previous is not null) return previous.ClientReplyId == request.ClientReplyId && previous.Body == request.Body.Trim() ? new(new(previous.ReplyId, previous.Body, previous.SenderName, previous.CreatedAt)) : new(null, ErrorCodes.ConcurrencyConflict);
        if (actions.ContainsKey(messageId)) return new(null, ErrorCodes.ConcurrencyConflict);
        if (!(await new NotificationAudiencePolicy(db).RevalidateAsync(row.Event, ct)).Contains(row.UserId)) return new(null, ErrorCodes.ForbiddenResource);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == tenant.UserId && u.Status == EduTwin.Contracts.IdentityAndTenancy.UserStatus.Active && u.AuthVersion == tenant.AuthVersion, ct);
        if (user is null) return new(null, ErrorCodes.ForbiddenResource);
        var reply = new NotificationReply { ReplyId = Guid.NewGuid(), CenterId = row.CenterId, DeliveryId = row.DeliveryId,
            ClientReplyId = request.ClientReplyId, Body = request.Body.Trim(), SenderName = user.DisplayName.Length > 160 ? user.DisplayName[..160] : user.DisplayName,
            CreatedBy = user.UserId, CreatedAt = clock.GetUtcNow().UtcDateTime };
        db.NotificationReplies.Add(reply);
        var replyEventId = Guid.NewGuid();
        // The OWN delivery + revalidated message proves this relay. No reply body is copied into another inbox.
        db.NotificationEvents.Add(new() { CenterId = source.CenterId, EventId = replyEventId, EventType = "NotificationReplyReceived",
            DeduplicationKey = $"reply:{reply.ReplyId:D}", Title = "Có phản hồi riêng cho thông báo", Body = "Một người nhận đã phản hồi. Xem trong Đã gửi; xác nhận/phản hồi không thay đổi trạng thái nghiệp vụ.",
            CreatedAt = reply.CreatedAt, CreatedBy = source.CenterId == row.CenterId ? user.UserId : null,
            RecipientUserIds = System.Text.Json.JsonSerializer.Serialize(new[] { source.SenderUserId }),
            ResourceContext = System.Text.Json.JsonSerializer.Serialize(new NotificationBusinessContext("SentReply", MessageId: messageId)) });
        db.NotificationDispatches.Add(new() { CenterId = source.CenterId, EventId = replyEventId, AvailableAt = reply.CreatedAt });
        try { await db.SaveChangesAsync(ct); return new(new(reply.ReplyId, reply.Body, reply.SenderName, reply.CreatedAt)); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            previous = await db.NotificationReplies.AsNoTracking().SingleOrDefaultAsync(x => x.DeliveryId == row.DeliveryId && x.CreatedBy == tenant.UserId, ct);
            if (previous is null) throw;
            return previous.ClientReplyId == request.ClientReplyId && previous.Body == request.Body.Trim() ? new(new(previous.ReplyId, previous.Body, previous.SenderName, previous.CreatedAt)) : new(null, ErrorCodes.ConcurrencyConflict);
        }
    }
}
