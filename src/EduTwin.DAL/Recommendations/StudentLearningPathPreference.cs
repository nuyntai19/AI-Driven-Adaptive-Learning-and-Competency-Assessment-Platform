using System;
using System.Text.Json;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;

namespace EduTwin.DAL.Recommendations;

public class StudentLearningPathPreference : IMutableTenantAggregate
{
    public ulong PreferenceId { get; set; }
    public Guid CenterId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }

    public string SelfAssessedLevel { get; set; } = "Medium";
    public JsonDocument WeakTopicNodeIds { get; set; } = null!;
    public JsonDocument FocusTopicNodeIds { get; set; } = null!;
    public string GoalType { get; set; } = "Foundation";
    public decimal TargetMastery { get; set; } = 80m;
    public int TargetWeeks { get; set; } = 4;
    public int MinutesPerDay { get; set; } = 30;
    public int DaysPerWeek { get; set; } = 5;
    public string Pace { get; set; } = "Moderate";
    public string PreferredMode { get; set; } = "Balanced";
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public ulong RowVersion { get; set; }

    public Student Student { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
}
