using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Notifications;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Notifications;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Notifications;

public interface INotificationPreviewProtector
{
    string Protect(string value);
    string? Unprotect(string token);
}
public sealed record NotificationPreviewGrant(Guid CenterId, Guid UserId, uint AuthVersion, string Fingerprint,
    string RecipientFingerprint, DateTime ExpiresAt);

public sealed class NotificationManualService(EduTwinDbContext db, ITenantContext tenant, TimeProvider clock,
    NotificationAudiencePolicy policy, INotificationPreviewProtector protector)
{
    public const string ScopeChanged = "RECIPIENT_SCOPE_CHANGED";
    private async Task<User?> SenderAsync(CancellationToken ct)
    {
        if (!tenant.IsResolved || tenant.CenterId is not { } c || c == Guid.Empty || tenant.UserId is not { } u || u == Guid.Empty) return null;
        var sender = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.CenterId == c && x.UserId == u && x.Status == UserStatus.Active && x.RoleName != UserRole.Student, ct);
        if (sender is null || tenant.AuthVersion != sender.AuthVersion || !await db.Centers.AnyAsync(x => x.CenterId == c && !x.IsDeleted && x.Status == CenterStatus.Active, ct)) return null;
        return sender;
    }
    public async Task<NotificationResult<IReadOnlyList<NotificationAudienceOption>>> OptionsAsync(CancellationToken ct)
    {
        var sender = await SenderAsync(ct);
        if (sender is null) return new(null, ErrorCodes.ForbiddenResource);
        try { return new(await policy.OptionsAsync(sender, ct)); }
        catch (NotificationScopeException) { return new(null, ErrorCodes.ForbiddenResource); }
    }
    private static bool Valid(NotificationComposeRequest r) => !string.IsNullOrWhiteSpace(r.Title) && r.Title.Length <= 160 &&
        !string.IsNullOrWhiteSpace(r.Body) && r.Body.Length <= 2000 && r.TargetIds is { Length: > 0 and <= 500 } &&
        r.TargetIds.All(i => i != Guid.Empty) && r.TargetIds.Distinct().Count() == r.TargetIds.Length &&
        r.Priority is "Normal" or "Important" or "Urgent" && r.Category is "Information" or "Request" &&
        (r.CorrectionOfMessageId is null && string.IsNullOrEmpty(r.CorrectionReason) ||
         r.CorrectionOfMessageId is { } id && id != Guid.Empty && !string.IsNullOrWhiteSpace(r.CorrectionReason) && r.CorrectionReason.Length <= 500);
    private static NotificationComposeRequest Normalize(NotificationComposeRequest r) => new() { Title = r.Title.Trim(), Body = r.Body.Trim(),
        AudienceKind = r.AudienceKind, TargetIds = r.TargetIds.Order().ToArray(), Priority = r.Priority, Category = r.Category,
        RequiresAcknowledgement = r.RequiresAcknowledgement, AllowReply = r.AllowReply,
        CorrectionOfMessageId = r.CorrectionOfMessageId, CorrectionReason = r.CorrectionReason?.Trim() };
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string Fingerprint(NotificationComposeRequest r) => Hash(JsonSerializer.Serialize(Normalize(r)));
    private static string RecipientHash(IEnumerable<NotificationRecipient> recipients) => Hash(string.Join(";", recipients.OrderBy(x => x.CenterId).ThenBy(x => x.UserId).Select(x => $"{x.CenterId:D}/{x.UserId:D}")));
    public async Task<NotificationResult<NotificationPreviewDto>> PreviewAsync(NotificationComposeRequest request, CancellationToken ct)
    {
        var sender = await SenderAsync(ct);
        if (sender is null) return new(null, ErrorCodes.ForbiddenResource);
        if (!Valid(request)) return new(null, ErrorCodes.ValidationFailed);
        if (NotificationContentPolicy.IsRestricted(request.AudienceKind, request.Title, request.Body, request.CorrectionReason)) return new(null, NotificationContentPolicy.Restricted);
        try
        {
            var recipients = await policy.ResolveAsync(sender, request, ct);
            if (!await ValidCorrectionAsync(request, recipients, ct)) return new(null, ErrorCodes.ConcurrencyConflict);
            var expires = clock.GetUtcNow().UtcDateTime.AddMinutes(5);
            var grant = new NotificationPreviewGrant(sender.CenterId, sender.UserId, sender.AuthVersion, Fingerprint(request), RecipientHash(recipients), expires);
            return new(new(protector.Protect(JsonSerializer.Serialize(grant)), recipients.Length,
                recipients.Select(x => x.DisplayName).ToArray(), expires));
        }
        catch (NotificationScopeException) { return new(null, ErrorCodes.ForbiddenResource); }
    }
    private IQueryable<NotificationMessage> Own() => db.NotificationMessages.Where(m => m.CenterId == tenant.CenterId && m.SenderUserId == tenant.UserId);
    public async Task<NotificationResult<NotificationSentDto>> SendAsync(NotificationSendRequest request, CancellationToken ct)
    {
        var sender = await SenderAsync(ct);
        if (sender is null) return new(null, ErrorCodes.ForbiddenResource);
        if (!Valid(request) || request.ClientMessageId == Guid.Empty || string.IsNullOrWhiteSpace(request.PreviewToken) || request.PreviewToken.Length > 20000) return new(null, ErrorCodes.ValidationFailed);
        if (NotificationContentPolicy.IsRestricted(request.AudienceKind, request.Title, request.Body, request.CorrectionReason)) return new(null, NotificationContentPolicy.Restricted);
        var fingerprint = Fingerprint(request);
        var previous = await Own().AsNoTracking().SingleOrDefaultAsync(m => m.ClientMessageId == request.ClientMessageId, ct);
        if (previous is not null) return previous.Fingerprint == fingerprint ? new((await MapAsync([previous], ct))[0]) : new(null, ErrorCodes.ConcurrencyConflict);
        NotificationPreviewGrant? grant = null;
        try { var text = protector.Unprotect(request.PreviewToken); if (text is not null) grant = JsonSerializer.Deserialize<NotificationPreviewGrant>(text); }
        catch (JsonException) { }
        if (grant is null || grant.CenterId != sender.CenterId || grant.UserId != sender.UserId || grant.AuthVersion != sender.AuthVersion ||
            grant.ExpiresAt <= clock.GetUtcNow().UtcDateTime || grant.Fingerprint != fingerprint) return new(null, ErrorCodes.ValidationFailed);
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var recipients = await policy.ResolveAsync(sender, request, ct);
            if (!await ValidCorrectionAsync(request, recipients, ct)) return new(null, ErrorCodes.ConcurrencyConflict);
            if (RecipientHash(recipients) != grant.RecipientFingerprint) return new(null, ScopeChanged);
            var normalized = Normalize(request);
            var message = new NotificationMessage { MessageId = Guid.NewGuid(), CenterId = sender.CenterId, SenderUserId = sender.UserId,
                ClientMessageId = request.ClientMessageId, SenderName = sender.DisplayName.Length > 160 ? sender.DisplayName[..160] : sender.DisplayName,
                SenderAuthVersion = sender.AuthVersion, Title = normalized.Title, Body = normalized.Body,
                Audience = JsonSerializer.Serialize(normalized), Fingerprint = fingerprint, RecipientCount = recipients.Length,
                Priority = normalized.Priority, Category = normalized.Category, RequiresAcknowledgement = normalized.RequiresAcknowledgement,
                AllowReply = normalized.AllowReply, CorrectionOfMessageId = normalized.CorrectionOfMessageId,
                CreatedAt = clock.GetUtcNow().UtcDateTime, CreatedBy = sender.UserId };
            db.NotificationMessages.Add(message);
            if (request.CorrectionOfMessageId is { } originalId)
                db.NotificationMessageActions.Add(new() { ActionId = Guid.NewGuid(), CenterId = sender.CenterId, MessageId = originalId,
                    ClientActionId = request.ClientMessageId, ReplacementMessageId = message.MessageId, Kind = "Corrected",
                    Reason = normalized.CorrectionReason!, CreatedAt = message.CreatedAt, CreatedBy = sender.UserId });
            foreach (var group in recipients.GroupBy(r => r.CenterId))
            {
                // Target centers/identities originate only from the actor-specific policy above.
                // Relay envelopes are local to recipients, never assign a foreign user to CreatedBy.
                var eventId = Guid.NewGuid();
                db.NotificationEvents.Add(new() { EventId = eventId, CenterId = group.Key, MessageId = message.MessageId,
                    EventType = "ManualNotice", DeduplicationKey = $"manual:{message.MessageId:D}:{group.Key:D}",
                    Title = message.Title, Body = message.Body, SenderName = message.SenderName,
                    RecipientUserIds = JsonSerializer.Serialize(group.Select(r => r.UserId).Order().ToArray()),
                    CreatedAt = message.CreatedAt, CreatedBy = group.Key == sender.CenterId ? sender.UserId : null });
                db.NotificationDispatches.Add(new() { EventId = eventId, CenterId = group.Key, AvailableAt = message.CreatedAt });
            }
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return new((await MapAsync([message], ct))[0]);
        }
        catch (NotificationScopeException) { return new(null, ScopeChanged); }
        catch (DbUpdateException)
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            var duplicate = await Own().AsNoTracking().SingleOrDefaultAsync(m => m.ClientMessageId == request.ClientMessageId, ct);
            if (duplicate is null)
            {
                if (request.CorrectionOfMessageId is { } original && await db.NotificationMessageActions.AnyAsync(x => x.MessageId == original, ct))
                    return new(null, ErrorCodes.ConcurrencyConflict);
                throw;
            }
            return duplicate.Fingerprint == fingerprint ? new((await MapAsync([duplicate], ct))[0]) : new(null, ErrorCodes.ConcurrencyConflict);
        }
    }
    private async Task<bool> ValidCorrectionAsync(NotificationComposeRequest request, NotificationRecipient[] recipients, CancellationToken ct)
    {
        if (request.CorrectionOfMessageId is not { } original) return true;
        if (!await Own().AnyAsync(x => x.MessageId == original, ct) || await db.NotificationMessageActions.AnyAsync(x => x.MessageId == original, ct)) return false;
        // Only an owned original message authorizes the relay snapshot query.
        var envelopes = await db.NotificationEvents.IgnoreQueryFilters().AsNoTracking().Where(x => x.MessageId == original).ToArrayAsync(ct);
        var snapshot = envelopes.SelectMany(e => (JsonSerializer.Deserialize<Guid[]>(e.RecipientUserIds) ?? []).Select(u => (e.CenterId, UserId: u))).ToHashSet();
        return recipients.All(r => snapshot.Contains((r.CenterId, r.UserId)));
    }

    public async Task<string?> RetractAsync(Guid id, RetractNotificationRequest request, CancellationToken ct)
    {
        var sender = await SenderAsync(ct);
        if (sender is null) return ErrorCodes.ForbiddenResource;
        if (request.ClientActionId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500) return ErrorCodes.ValidationFailed;
        var message = await Own().AsNoTracking().SingleOrDefaultAsync(x => x.MessageId == id, ct);
        if (message is null) return ErrorCodes.ResourceNotFound;
        if (NotificationContentPolicy.IsRestricted(NotificationContentPolicy.StoredAudience(message), request.Reason)) return NotificationContentPolicy.Restricted;
        var previous = await db.NotificationMessageActions.AsNoTracking().SingleOrDefaultAsync(x => x.MessageId == id, ct);
        if (previous is not null) return previous.Kind == "Retracted" && previous.ClientActionId == request.ClientActionId && previous.Reason == request.Reason.Trim() ? null : ErrorCodes.ConcurrencyConflict;
        db.NotificationMessageActions.Add(new() { ActionId = Guid.NewGuid(), CenterId = sender.CenterId, MessageId = id,
            ClientActionId = request.ClientActionId, Kind = "Retracted", Reason = request.Reason.Trim(), CreatedAt = clock.GetUtcNow().UtcDateTime, CreatedBy = sender.UserId });
        try { await db.SaveChangesAsync(ct); return null; }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            previous = await db.NotificationMessageActions.AsNoTracking().SingleOrDefaultAsync(x => x.MessageId == id, ct);
            if (previous is null) throw;
            return previous.Kind == "Retracted" && previous.ClientActionId == request.ClientActionId && previous.Reason == request.Reason.Trim() ? null : ErrorCodes.ConcurrencyConflict;
        }
    }

    public async Task<NotificationResult<NotificationReplyPage>> RepliesAsync(Guid id, NotificationListQuery query, CancellationToken ct)
    {
        if (await SenderAsync(ct) is null) return new(null, ErrorCodes.ForbiddenResource);
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue) return new(null, ErrorCodes.ValidationFailed);
        if (!await Own().AnyAsync(x => x.MessageId == id, ct)) return new(null, ErrorCodes.ResourceNotFound);
        var replies = from reply in db.NotificationReplies.IgnoreQueryFilters().AsNoTracking()
                      join delivery in db.NotificationDeliveries.IgnoreQueryFilters() on new { reply.CenterId, reply.DeliveryId } equals new { delivery.CenterId, delivery.DeliveryId }
                      join envelope in db.NotificationEvents.IgnoreQueryFilters() on new { delivery.CenterId, delivery.EventId } equals new { envelope.CenterId, envelope.EventId }
                      where envelope.MessageId == id
                      select reply;
        var count = await replies.LongCountAsync(ct);
        var rows = await replies.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.ReplyId).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new NotificationReplyDto(x.ReplyId, x.Body, x.SenderName, x.CreatedAt)).ToArrayAsync(ct);
        return new(new(rows, count));
    }
    public async Task<NotificationResult<NotificationSentPage>> SentAsync(NotificationListQuery query, CancellationToken ct)
    {
        if (await SenderAsync(ct) is null) return new(null, ErrorCodes.ForbiddenResource);
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue) return new(null, ErrorCodes.ValidationFailed);
        var own = Own().AsNoTracking();
        var count = await own.LongCountAsync(ct);
        var rows = await own.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.MessageId).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return new(new(await MapAsync(rows, ct), count));
    }
    public async Task<NotificationResult<NotificationSentDto>> SentDetailAsync(Guid id, CancellationToken ct)
    {
        if (await SenderAsync(ct) is null) return new(null, ErrorCodes.ForbiddenResource);
        var row = await Own().AsNoTracking().SingleOrDefaultAsync(m => m.MessageId == id, ct);
        return row is null ? new(null, ErrorCodes.ResourceNotFound) : new((await MapAsync([row], ct))[0]);
    }
    private async Task<NotificationSentDto[]> MapAsync(NotificationMessage[] rows, CancellationToken ct)
    {
        if (rows.Length == 0) return [];
        // Only aggregate receipts of already loaded OWN message IDs, including explicit relay events.
        var events = await NotificationRecipientScope.WhereIds(db.NotificationEvents.IgnoreQueryFilters().AsNoTracking().Where(e => e.MessageId != null), e => e.MessageId!.Value, rows.Select(m => m.MessageId))
            .Select(e => new { e.EventId, e.MessageId }).ToArrayAsync(ct);
        var eventIds = events.Select(e => e.EventId).ToArray();
        var states = await NotificationRecipientScope.WhereIds(db.NotificationDispatches.IgnoreQueryFilters().AsNoTracking(), d => d.EventId, eventIds).ToArrayAsync(ct);
        var read = await NotificationRecipientScope.WhereIds(db.NotificationDeliveries.IgnoreQueryFilters().AsNoTracking().Where(d => d.ReadAt != null), d => d.EventId, eventIds)
            .GroupBy(d => d.EventId).Select(g => new { EventId = g.Key, Count = g.Count() }).ToArrayAsync(ct);
        var acknowledged = await NotificationRecipientScope.WhereIds(db.NotificationDeliveries.IgnoreQueryFilters().AsNoTracking().Where(d => d.AcknowledgedAt != null), d => d.EventId, eventIds)
            .GroupBy(d => d.EventId).Select(g => new { EventId = g.Key, Count = g.Count() }).ToArrayAsync(ct);
        var actions = await NotificationRecipientScope.WhereIds(db.NotificationMessageActions.AsNoTracking(), a => a.MessageId, rows.Select(m => m.MessageId)).ToArrayAsync(ct);
        return rows.Select(m => {
            var ids = events.Where(e => e.MessageId == m.MessageId).Select(e => e.EventId).ToHashSet();
            var dispatch = states.Where(s => ids.Contains(s.EventId)).ToArray();
            var action = actions.SingleOrDefault(a => a.MessageId == m.MessageId);
            return new NotificationSentDto(m.MessageId, m.Title, m.Body, m.SenderName, m.CreatedAt, m.RecipientCount,
                dispatch.Sum(d => d.DeliveredCount), dispatch.Sum(d => d.SuppressedCount), read.Where(r => ids.Contains(r.EventId)).Sum(r => r.Count),
                dispatch.Any(d => d.State == "DeadLetter") ? "Failed" : dispatch.Length != 0 && dispatch.All(d => d.State == "Completed") ? "Completed" : "Pending",
                m.Priority, m.Category, m.RequiresAcknowledgement, m.AllowReply, acknowledged.Where(a => ids.Contains(a.EventId)).Sum(a => a.Count),
                action?.Kind ?? "Active", action?.Reason, action?.ReplacementMessageId, m.CorrectionOfMessageId);
        }).ToArray();
    }
}
