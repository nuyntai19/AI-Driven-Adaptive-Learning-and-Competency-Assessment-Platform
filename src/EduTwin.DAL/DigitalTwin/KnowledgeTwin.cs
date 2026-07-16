using System;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.Organization;
using EduTwin.DAL.KnowledgeGraph;

namespace EduTwin.DAL.DigitalTwin;

public class KnowledgeTwin : IMutableTenantAggregate
{
    public ulong KnowledgeTwinId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public ulong TopicNodeId { get; set; }
    public decimal MasteryPercentage { get; set; }
    public uint EvidenceCount { get; set; }
    public decimal? LastReasoningQuality { get; set; }
    public ulong? LastAttemptId { get; set; }
    public DateTime? LastEvidenceAt { get; set; }

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
    public KnowledgeNode TopicNode { get; set; } = null!;
}
