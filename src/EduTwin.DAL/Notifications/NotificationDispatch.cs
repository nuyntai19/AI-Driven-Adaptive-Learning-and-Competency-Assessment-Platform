using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Notifications;

public sealed class NotificationDispatch : ITenantJoinEntity, IHasRowVersion
{
    public Guid CenterId { get; set; }
    public Guid EventId { get; set; }
    public string State { get; set; } = "Pending";
    public DateTime AvailableAt { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public int AttemptCount { get; set; }
    public int DeliveredCount { get; set; }
    public int SuppressedCount { get; set; }
    public string? LastErrorCode { get; set; }
    public ulong RowVersion { get; set; }
}
