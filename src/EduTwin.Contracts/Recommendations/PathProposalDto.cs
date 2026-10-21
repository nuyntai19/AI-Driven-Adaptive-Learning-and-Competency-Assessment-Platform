using System;
using System.Collections.Generic;

namespace EduTwin.Contracts.Recommendations;

public class PathProposalDto
{
    public List<string> SelfReportedPrerequisiteSkillIds { get; set; } = new();
    /// <summary>Parts the student left out of the placement check (self-assessed as mastered); each gets a short confirmation review at the end of the route.</summary>
    public List<string> SelfAssessedSkillIds { get; set; } = new();
    public bool IsPlacement { get; set; }
    public string GoalType { get; set; } = "Foundation";
    public List<string> RequestedSkillIds { get; set; } = new();
    /// <summary>2 = practice runs as a loop of fixed 6-question sets. Older routes are flagged for an update.</summary>
    public int PacingVersion { get; set; }
    public uint PracticeQuestionsPerSet { get; set; } = 6;
    public Dictionary<string, uint> PracticeQuestionsBySkill { get; set; } = new();
    public uint AvailableMinutesPerDay { get; set; } = 15;
    public uint AvailableDaysPerWeek { get; set; } = 5;
    public DateTime? DeadlineDate { get; set; }
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
    public bool IsSelfAssessedCheck { get; set; }
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
    /// <summary>Short review confirming a part the student self-assessed as mastered.</summary>
    public bool IsSelfAssessedCheck { get; set; }
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

public class CustomizePathProposalRequest
{
    public uint AvailableMinutesPerDay { get; set; } = 15;
    public uint AvailableDaysPerWeek { get; set; } = 5;
    public DateTime? DeadlineDate { get; set; }
    public uint PracticeQuestionsPerSet { get; set; } = 6;
    public Dictionary<string, uint> PracticeQuestionsBySkill { get; set; } = new();
    public List<string> TargetSkillIds { get; set; } = new();
}

/// <summary>Tests more parts inside the active route without replacing it.</summary>
public class ExtendPathCheckRequest
{
    public Guid SubjectId { get; set; }
    public Guid? ClassId { get; set; }
    public List<string> SkillIds { get; set; } = new();
}

public class ExtendPathCheckResponse
{
    /// <summary>New check missions, in the order to take them.</summary>
    public List<string> MissionIds { get; set; } = new();
    /// <summary>Parts without five fresh questions; nothing was added for them.</summary>
    public List<string> UnavailableSkillIds { get; set; } = new();
}
