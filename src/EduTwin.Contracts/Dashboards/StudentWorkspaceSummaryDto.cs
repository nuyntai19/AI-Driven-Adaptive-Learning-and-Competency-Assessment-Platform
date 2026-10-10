namespace EduTwin.Contracts.Dashboards;

public sealed class StudentWorkspaceSummaryDto
{
    public int AssignmentCount { get; init; }
    public int DailyStreak { get; init; }
    public bool StudiedToday { get; init; }
    public string LocalDate { get; init; } = "";
    public string Timezone { get; init; } = "";
    public DateTime GeneratedAt { get; init; }
}
