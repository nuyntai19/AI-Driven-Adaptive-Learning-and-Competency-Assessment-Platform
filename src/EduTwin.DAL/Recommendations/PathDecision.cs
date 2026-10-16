using System;
using System.Text.Json;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Recommendations;

public class PathDecision : IMutableTenantAggregate
{
    public ulong DecisionId { get; set; }
    public Guid CenterId { get; set; }
    public Guid LearningPathId { get; set; }
    public ulong? SessionRunId { get; set; }

    public string TriggerType { get; set; } = "DiagnosticCompleted";
    public JsonDocument ReasonCodes { get; set; } = null!;
    public JsonDocument? EvidenceReferenceIds { get; set; }
    public string DecisionSummary { get; set; } = string.Empty;
    public string PolicyVersion { get; set; } = "v4-pilot-1";

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public ulong RowVersion { get; set; } = 1;

    public LearningPath LearningPath { get; set; } = null!;
}
