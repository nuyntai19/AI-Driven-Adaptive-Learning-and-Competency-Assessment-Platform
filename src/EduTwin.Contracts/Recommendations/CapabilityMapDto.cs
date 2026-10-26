using System;
using System.Collections.Generic;

namespace EduTwin.Contracts.Recommendations;

public class CapabilityMapDto
{
    public Dictionary<string, decimal> ExternalPrerequisiteMastery { get; set; } = [];
    public Guid SubjectId { get; set; }
    public Guid StudentId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public decimal OverallProgressRate { get; set; }

    public List<SkillCapabilityStatusDto> Skills { get; set; } = new();
    public List<string> ProvenCapabilities { get; set; } = new();
    public List<string> AreasNeedingSupport { get; set; } = new();
    public List<string> ReviewDueSkills { get; set; } = new();

    public DateTime LastUpdatedAt { get; set; }
}

public class SkillCapabilityStatusDto
{
    public bool StageGateFailed { get; set; }
    public bool BlockedByStageGate { get; set; }
    public string SkillId { get; set; } = string.Empty;
    public string SkillName { get; set; } = string.Empty;
    public string TopicName { get; set; } = string.Empty;
    public string? TopicId { get; set; }
    public string Description { get; set; } = string.Empty;

    // State: Unknown, Developing, Demonstrated, Confirmed, ReviewDue
    public string State { get; set; } = "Unknown";

    // Availability: Available, PrerequisiteBlocked, ContentUnavailable, OutOfScope
    public string Availability { get; set; } = "Available";

    public List<string> PrerequisiteSkillIds { get; set; } = new();
    public uint EvidenceCount { get; set; }
    public decimal? MasteryScore { get; set; }
    public bool RequiresProbe { get; set; }
    public string Rationale { get; set; } = string.Empty;
}
