using System;
using System.Collections.Generic;

namespace EduTwin.Contracts.Recommendations;

public class LearningAgendaDto
{
    public Guid? LearningPathId { get; set; }
    public string? GoalTitle { get; set; }
    public List<AgendaItemDto> Missions { get; set; } = new();
    public DateTime Date { get; set; }
    public uint AvailableMinutes { get; set; } = 15;
    public string AgendaMessage { get; set; } = string.Empty;

    public AgendaItemDto? PrimaryMission { get; set; }
    public List<AgendaItemDto> AlternativeOptions { get; set; } = new();
    public List<AgendaItemDto> OngoingRuns { get; set; } = new();
    public List<AgendaItemDto> ReviewDueTasks { get; set; } = new();
}

public class AgendaItemDto
{
    public string MissionId { get; set; } = string.Empty;
    public string? LegacySessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty; // e.g. "Remedial", "CoreStep", "TransferChallenge", "Review"
    public string SkillId { get; set; } = string.Empty;
    public string SkillName { get; set; } = string.Empty;
    public uint EstimatedDurationMinutes { get; set; } = 15;
    public List<string> ActivityTypes { get; set; } = new();
    public string RecommendationReason { get; set; } = string.Empty;

    public bool CanStart { get; set; } = true;
    public string Status { get; set; } = "NotStarted"; // NotStarted, InProgress, Completed, NeedsReview
    public ulong? ActiveRunId { get; set; }
}
