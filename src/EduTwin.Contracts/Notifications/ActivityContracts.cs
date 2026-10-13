namespace EduTwin.Contracts.Notifications;

public sealed class ActivityQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Source { get; set; }
    public string? Action { get; set; }
    public DateTime? From { get; set; }
    public DateTime? Until { get; set; }
}
public sealed record ActivityDto(string Source, string SourceId, DateTime OccurredAt, string ActorName,
    string Action, string TargetType, string TargetId, string? BeforeStatus, string? AfterStatus,
    string? Version, string? Reason, string? Link, string? TargetLabel = null);
public sealed record ActivityPage(IReadOnlyList<ActivityDto> Items, long TotalItems);
