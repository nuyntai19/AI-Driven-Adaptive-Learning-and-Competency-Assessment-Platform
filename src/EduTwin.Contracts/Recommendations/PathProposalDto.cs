using System;
using System.Collections.Generic;

namespace EduTwin.Contracts.Recommendations;

public class PathProposalDto
{
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
}

public class ProposedMissionDto
{
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
