using System;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.Organization;

namespace EduTwin.DAL.DigitalTwin;

public class BehaviorTwin : IMutableTenantAggregate
{
    public ulong BehaviorTwinId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public decimal AvgTimeSpentSeconds { get; set; }
    public decimal SkipRate { get; set; }
    public decimal ChangeAnswerRate { get; set; }
    public decimal AvgConfidence { get; set; }
    public decimal ConfidenceCalibration { get; set; }
    public uint AttemptCount { get; set; }

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
