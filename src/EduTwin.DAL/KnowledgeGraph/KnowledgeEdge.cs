using System;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.Organization;

namespace EduTwin.DAL.KnowledgeGraph;

public class KnowledgeEdge : IMutableTenantAggregate
{
    public ulong EdgeId { get; set; }
    public Guid CenterId { get; set; }
    public Guid SubjectId { get; set; }
    public ulong SourceNodeId { get; set; }
    public ulong TargetNodeId { get; set; }
    public RelationType RelationType { get; set; }
    public decimal Weight { get; set; }

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
    public KnowledgeNode? SourceNode { get; set; }
    public KnowledgeNode? TargetNode { get; set; }
}
