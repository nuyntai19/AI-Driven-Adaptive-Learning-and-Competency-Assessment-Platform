using System;
using System.Text.Json;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Recommendations;

public class LearningContentVersion : IMutableTenantAggregate
{
    public Guid ContentVersionId { get; set; }
    public Guid CenterId { get; set; }
    public Guid SubjectId { get; set; }

    public string SkillId { get; set; } = string.Empty; // e.g. "MATH-EQ-01"
    public string ActivityType { get; set; } = "WorkedExample"; // WorkedExample, FindError, IndependentCheck, Explore, TransferChallenge
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public uint EstimatedDurationMinutes { get; set; } = 15;

    public JsonDocument Payload { get; set; } = null!; // Steps, misconception data, solutions, questions
    public JsonDocument? Rubric { get; set; }
    public JsonDocument? Prerequisites { get; set; } // List of skill IDs required

    public uint Version { get; set; } = 1;
    public string Status { get; set; } = "Draft"; // Publishing requires an explicit authoring decision.

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public ulong RowVersion { get; set; } = 1;

    public Subject Subject { get; set; } = null!;
}
