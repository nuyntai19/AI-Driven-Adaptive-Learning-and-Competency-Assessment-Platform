using System;
using System.Text.Json;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Recommendations;

public class LearningGoal : IMutableTenantAggregate
{
    public Guid GoalId { get; set; }
    public Guid CenterId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid? ClassId { get; set; }

    public string GoalType { get; set; } = "Foundation"; // Foundation, Catchup, ExamPrep, Advanced
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public JsonDocument? TargetSkillIds { get; set; }
    public DateTime? DeadlineDate { get; set; }
    public decimal? TargetScore { get; set; }
    public bool StartWithPlacement { get; set; }
    public JsonDocument? ScheduleSettings { get; set; }

    public uint AvailableMinutesPerDay { get; set; } = 15;
    public uint AvailableDaysPerWeek { get; set; } = 5;
    public string PreferredPace { get; set; } = "Moderate"; // Gentle, Moderate, Accelerated
    public string PreferredMode { get; set; } = "Balanced";

    public string Status { get; set; } = "Active"; // Active, Achieved, Paused, Abandoned

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public ulong RowVersion { get; set; } = 1;

    public Student Student { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
    public Class? Class { get; set; }
}
