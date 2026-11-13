using System;
using System.Collections.Generic;

namespace EduTwin.Contracts.Recommendations;

public class LearningGoalDto
{
    public List<string> SelfReportedPrerequisiteSkillIds { get; set; } = new();
    /// <summary>Parts the student left out of the placement check (self-assessed as mastered); each gets a short confirmation review at the end of the route.</summary>
    public List<string> SelfAssessedSkillIds { get; set; } = new();
    public Guid GoalId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid? ClassId { get; set; }

    public string GoalType { get; set; } = "Foundation"; // Foundation, Catchup, ExamPrep, Advanced
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public List<string> TargetSkillIds { get; set; } = new();
    public DateTime? DeadlineDate { get; set; }
    public decimal? TargetScore { get; set; }
    public bool StartWithPlacement { get; set; }
    public LearningScheduleSettings Schedule { get; set; } = new();

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
    public bool ClearDeadline { get; set; }
    public decimal? TargetScore { get; set; }
    public bool StartWithPlacement { get; set; }
    public LearningScheduleSettings Schedule { get; set; } = new();
    public List<string> SelfReportedPrerequisiteSkillIds { get; set; } = new();
    /// <summary>Parts the student left out of the placement check (self-assessed as mastered); each gets a short confirmation review at the end of the route.</summary>
    public List<string> SelfAssessedSkillIds { get; set; } = new();
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

/// <summary>
/// Feasibility of a goal while the student is still filling it in; nothing is saved. Without a deadline in the
/// request the server picks the suggested date (re-checked against the route it produces) and evaluates that.
/// Dates are calendar days in the schedule's time zone.
/// </summary>
public class LearningGoalEstimateDto
{
    public DateOnly? DeadlineDate { get; set; }
    public bool DeadlineSuggested { get; set; }
    public DateOnly? EarliestFeasibleDate { get; set; }
    public DateOnly? SuggestedDeadline { get; set; }
    public decimal? CurrentEstimatedScore { get; set; }
    public decimal? TargetScore { get; set; }
    public decimal? AchievableScore { get; set; }
    public uint RequiredStudyMinutes { get; set; }
    public uint MinimumStudyDays { get; set; }
    public string FeasibilityStatus { get; set; } = "Feasible"; // Feasible, TightBudget, InsufficientTime
    public string? FeasibilityMessage { get; set; }
    /// <summary>Study days from today through the deadline, on the chosen weekdays (excluded dates left out).</summary>
    public uint? StudyDaysUntilDeadline { get; set; }
    /// <summary>Minutes per study day needed to finish the required study by the deadline.</summary>
    public uint? MinutesPerDayForDeadline { get; set; }
}

public class UpdateLearningGoalRequest
{
    public decimal? TargetScore { get; set; }
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
