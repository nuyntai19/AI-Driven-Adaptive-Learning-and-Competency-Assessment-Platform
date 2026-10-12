using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Notifications;

public sealed class NotificationDelivery : ITenantJoinEntity
{
    public ulong DeliveryId { get; set; }
    public Guid CenterId { get; set; }
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public DateTime DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? HiddenAt { get; set; }
    public NotificationEvent Event { get; set; } = null!;
}
