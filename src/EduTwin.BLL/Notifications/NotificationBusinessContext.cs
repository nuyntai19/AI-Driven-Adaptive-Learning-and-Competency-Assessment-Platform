using System.Text.Json;

namespace EduTwin.BLL.Notifications;

// IDs and small status/version metadata only. Never academic answers, raw audit JSON or provider data.
public sealed record NotificationBusinessContext(string Kind, Guid? ClassId = null, Guid? AssignmentId = null,
    Guid? StudentId = null, ulong? AttemptId = null, string? Version = null, Guid? MessageId = null);

internal static class NotificationBusinessContextSerializer
{
    public static NotificationBusinessContext? Read(string? json) => json is null ? null : JsonSerializer.Deserialize<NotificationBusinessContext>(json);
}
