using System;
using System.Collections.Generic;

namespace EduTwin.Contracts.Recommendations;

public class LearningGoalDto
{
    public List<string> SelfReportedPrerequisiteSkillIds { get; set; } = new();
    public Guid GoalId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid? ClassId { get; set; }

    public string GoalType { get; set; } = "Foundation"; // Foundation, Catchup, ExamPrep, Advanced
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public List<string> TargetSkillIds { get; set; } = new();
    public DateTime? DeadlineDate { get; set; }

    public uint AvailableMinutesPerDay { get; set; } = 15;
    public uint AvailableDaysPerWeek { get; set; } = 5;
    public string PreferredPace { get; set; } = "Moderate";
    public string PreferredMode { get; set; } = "Balanced";

    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateLearningGoalRequest
{
    public List<string> SelfReportedPrerequisiteSkillIds { get; set; } = new();
    public Guid SubjectId { get; set; }
    public Guid? ClassId { get; set; }

    public string GoalType { get; set; } = "Foundation";
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public List<string> TargetSkillIds { get; set; } = new();
    public DateTime? DeadlineDate { get; set; }

    public uint AvailableMinutesPerDay { get; set; } = 15;
    public uint AvailableDaysPerWeek { get; set; } = 5;
    public string PreferredPace { get; set; } = "Moderate";
    public string PreferredMode { get; set; } = "Balanced";
}

public class UpdateLearningGoalRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<string>? TargetSkillIds { get; set; }
    public DateTime? DeadlineDate { get; set; }
    public uint? AvailableMinutesPerDay { get; set; }
    public uint? AvailableDaysPerWeek { get; set; }
    public string? PreferredPace { get; set; }
    public string? PreferredMode { get; set; }
    public string? Status { get; set; }
}
