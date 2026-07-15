using System;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.Organization;

namespace EduTwin.DAL.KnowledgeGraph;

public class KnowledgeNode : IMutableTenantAggregate
{
    public ulong NodeId { get; set; }
    public Guid CenterId { get; set; }
    public Guid SubjectId { get; set; }
    public ulong? ParentNodeId { get; set; }
    public NodeType NodeType { get; set; }
    public string NodeCode { get; set; } = null!;
    public string NodeName { get; set; } = null!;
    public string? Description { get; set; }
    public uint OrderIndex { get; set; }
    public decimal ExamImportance { get; set; }
    public uint EstimatedLearningMinutes { get; set; }
    public bool IsActive { get; set; }

    // Audit and MTA
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public ulong RowVersion { get; set; }

    // Navigations
    public Subject? Subject { get; set; }
    public KnowledgeNode? ParentNode { get; set; }
}
