using System.Text.Json.Serialization;

namespace EduTwin.Contracts.Notifications;

public sealed record NotificationDto(string DeliveryId, string EventType, string Title, string Body,
    DateTime DeliveredAt, DateTime? ReadAt, string? SenderName = null,
    string Priority = "Normal", string Category = "Information", bool RequiresAcknowledgement = false,
    bool AllowReply = false, DateTime? AcknowledgedAt = null, string LifecycleStatus = "Active", string? LifecycleReason = null,
    Guid? CorrectionMessageId = null, string? OwnReply = null, string? RelatedLink = null);
public sealed record NotificationSummaryDto(long UnreadCount);
public sealed class NotificationListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public bool UnreadOnly { get; set; }
    public string? Priority { get; set; }
    public string? Category { get; set; }
    public DateTime? From { get; set; }
    public DateTime? Until { get; set; }
    public bool NeedsAcknowledgement { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class NotificationComposeRequest
{
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string AudienceKind { get; set; } = "";
    public Guid[] TargetIds { get; set; } = [];
    public string Priority { get; set; } = "Normal";
    public string Category { get; set; } = "Information";
    public bool RequiresAcknowledgement { get; set; }
    public bool AllowReply { get; set; }
    public Guid? CorrectionOfMessageId { get; set; }
    public string? CorrectionReason { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class NotificationSendRequest : NotificationComposeRequest
{
    public Guid ClientMessageId { get; set; }
    public string PreviewToken { get; set; } = "";
}
public sealed record NotificationChoice(Guid Id, string Label);
public sealed record NotificationAudienceOption(string Kind, string Label, IReadOnlyList<NotificationChoice> Choices);
public sealed record NotificationRecipient(Guid CenterId, Guid UserId, string DisplayName);
public sealed record NotificationPreviewDto(string Token, int RecipientCount, IReadOnlyList<string> RecipientNames, DateTime ExpiresAt);
public sealed record NotificationSentDto(Guid MessageId, string Title, string Body, string SenderName, DateTime CreatedAt,
    int RecipientCount, int DeliveredCount, int SuppressedCount, int ReadCount, string DispatchStatus,
    string Priority = "Normal", string Category = "Information", bool RequiresAcknowledgement = false, bool AllowReply = false,
    int AcknowledgedCount = 0, string LifecycleStatus = "Active", string? LifecycleReason = null, Guid? ReplacementMessageId = null,
    Guid? CorrectionOfMessageId = null);
public sealed record NotificationSentPage(IReadOnlyList<NotificationSentDto> Items, long TotalItems);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ReadNotificationBatchRequest
{
    public string[] DeliveryIds { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RetractNotificationRequest
{
    public Guid ClientActionId { get; set; }
    public string Reason { get; set; } = "";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ReplyNotificationRequest
{
    public Guid ClientReplyId { get; set; }
    public string Body { get; set; } = "";
}
public sealed record NotificationReplyDto(Guid ReplyId, string Body, string SenderName, DateTime CreatedAt);
public sealed record NotificationReplyPage(IReadOnlyList<NotificationReplyDto> Items, long TotalItems);
