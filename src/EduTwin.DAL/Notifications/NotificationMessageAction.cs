using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Notifications;

public sealed class NotificationMessageAction : ITenantAppendOnlyEntity
{
    public Guid ActionId { get; set; }
    public Guid CenterId { get; set; }
    public Guid MessageId { get; set; }
    public Guid ClientActionId { get; set; }
    public Guid? ReplacementMessageId { get; set; }
    public string Kind { get; set; } = "Retracted";
    public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}
