using System;
using System.Text.Json;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Recommendations;

public class PathProposal : IMutableTenantAggregate
{
    public Guid ProposalId { get; set; }
    public Guid CenterId { get; set; }
    public Guid StudentId { get; set; }
    public Guid GoalId { get; set; }
    public Guid SubjectId { get; set; }

    public string StartingBaselineSummary { get; set; } = string.Empty;
    public JsonDocument CandidatePlan { get; set; } = null!;
    public string FeasibilityStatus { get; set; } = "Feasible"; // Feasible, TightBudget, InsufficientTime
    public string? FeasibilityMessage { get; set; }
    public uint EstimatedWeeks { get; set; } = 4;
    public DateTime ExpiresAt { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Accepted, Expired, Superseded

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
    public LearningGoal Goal { get; set; } = null!;
}
