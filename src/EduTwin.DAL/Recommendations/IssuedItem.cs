using System;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Recommendations;

public class IssuedItem : IMutableTenantAggregate
{
    public ulong IssuedItemId { get; set; }
    public Guid CenterId { get; set; }
    public ulong ActivityRunId { get; set; }
    public ulong? QuestionId { get; set; }

    public uint ItemOrder { get; set; } = 1;
    public string ExposureMode { get; set; } = "Interactive"; // Interactive, Locked, SolutionExposed
    public DateTime IssuedAt { get; set; }

    public ulong? AttemptId { get; set; }
    public DateTime? SolutionExposedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public ulong RowVersion { get; set; } = 1;

    public ActivityRun ActivityRun { get; set; } = null!;
    public Attempt? Attempt { get; set; }
}
