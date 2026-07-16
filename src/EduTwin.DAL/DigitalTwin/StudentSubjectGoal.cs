using System;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.Organization;

namespace EduTwin.DAL.DigitalTwin;

public class StudentSubjectGoal : IMutableTenantAggregate
{
    public ulong GoalId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public decimal TargetScore { get; set; }
    public uint RemainingDays { get; set; }
    public decimal CurrentPredictedScore { get; set; }
    public decimal RiskScore { get; set; }

    public Guid CenterId { get; set; }
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
