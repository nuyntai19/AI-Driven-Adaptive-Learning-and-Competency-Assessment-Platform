using System;
using System.Collections.Generic;

namespace EduTwin.Contracts.Recommendations;

public class PathProposalDto
{
    /// <summary>2 = practice runs as a loop of fixed 6-question sets. Older routes are flagged for an update.</summary>
    public int PacingVersion { get; set; }
    public Guid ProposalId { get; set; }
    public Guid GoalId { get; set; }
    public Guid SubjectId { get; set; }

    public string GoalTitle { get; set; } = string.Empty;
    public string StartingBaselineSummary { get; set; } = string.Empty;
    public List<string> ConfirmedSkills { get; set; } = new();
    public List<string> DevelopingSkills { get; set; } = new();
    public List<string> UnknownSkills { get; set; } = new();

    public List<ProposedMilestoneDto> Milestones { get; set; } = new();
    public ProposedMissionDto? InitialMission { get; set; }
    public ProposedMissionDto? AlternativeOption { get; set; }

    public string FeasibilityStatus { get; set; } = "Feasible"; // Feasible, TightBudget, InsufficientTime
    public string? FeasibilityMessage { get; set; }
    public uint EstimatedWeeks { get; set; } = 4;
    public DateTime ExpiresAt { get; set; }
    public string Status { get; set; } = "Pending";
}

public class ProposedMilestoneDto
{
    public uint Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OutcomeDescription { get; set; } = string.Empty;
    public List<string> SkillIds { get; set; } = new();
    public List<ProposedMissionDto> Missions { get; set; } = new();
    public bool HasTransferChallenge { get; set; }
    /// <summary>Set by the practice loop after two consecutive sets at or above 80%.</summary>
    public bool ReadyForCheck { get; set; }
}

public class ProposedMissionDto
{
    public byte? DifficultyFloor { get; set; }
    /// <summary>Preferred difficulty for a practice set; questions closest to it come first.</summary>
    public byte? TargetDifficulty { get; set; }
    public uint SetNumber { get; set; }
    public uint PassStreak { get; set; }
    public uint EstimatedSets { get; set; }
    /// <summary>Repair work created from a failed check or review; shown even when the skill was confirmed earlier.</summary>
    public bool IsRepair { get; set; }
    /// <summary>A probe (quick try or starting-point check) may run before practice.</summary>
    public bool IsProbe { get; set; }
    public uint QuestionCount { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public Guid? ContentVersionId { get; set; }
    public bool CanStart { get; set; } = true;
    public string MissionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string SkillId { get; set; } = string.Empty;
    public uint EstimatedMinutes { get; set; } = 15;
    public List<string> ActivityTypes { get; set; } = new();
    public string SelectionReason { get; set; } = string.Empty;
}

public class GeneratePathProposalRequest
{
    public Guid GoalId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid? ClassId { get; set; }
}

public class AcceptPathProposalResponse
{
    public Guid LearningPathId { get; set; }
    public uint Version { get; set; }
    public string Status { get; set; } = "Active";
    public string Message { get; set; } = string.Empty;
    public ProposedMissionDto? FirstMission { get; set; }
}
