using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Notifications;

public sealed class NotificationEvent : ITenantAppendOnlyEntity
{
    public Guid? MessageId { get; set; }
    public string? SenderName { get; set; }
    public string? ResourceContext { get; set; }
    public Guid EventId { get; set; }
    public Guid CenterId { get; set; }
    public string DeduplicationKey { get; set; } = null!;
    public string EventType { get; set; } = "SystemNotice";
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;
    public string RecipientUserIds { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}
