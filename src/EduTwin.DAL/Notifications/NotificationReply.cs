using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Notifications;

public sealed class NotificationReply : ITenantAppendOnlyEntity
{
    public Guid ReplyId { get; set; }
    public Guid CenterId { get; set; }
    public ulong DeliveryId { get; set; }
    public Guid ClientReplyId { get; set; }
    public string Body { get; set; } = "";
    public string SenderName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}
