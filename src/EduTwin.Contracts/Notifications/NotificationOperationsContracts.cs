using System.Text.Json.Serialization;
namespace EduTwin.Contracts.Notifications;

public sealed record NotificationDispatchDto(Guid EventId, string EventType, string State, DateTime CreatedAt, DateTime AvailableAt,
    int AttemptCount, int DeliveredCount, int SuppressedCount, string? ErrorCode, string RowVersion);
public sealed record NotificationOperationsSummary(long Pending, long Processing, long DeadLetter, long Completed,
    long Delivered, long Suppressed, DateTime? OldestPendingAt);
public sealed record NotificationOperationsPage(IReadOnlyList<NotificationDispatchDto> Items, long TotalItems, NotificationOperationsSummary Summary);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class NotificationRedriveRequest
{
    public Guid ClientActionId { get; set; }
    public string ExpectedRowVersion { get; set; } = "";
    public string Reason { get; set; } = "";
}
