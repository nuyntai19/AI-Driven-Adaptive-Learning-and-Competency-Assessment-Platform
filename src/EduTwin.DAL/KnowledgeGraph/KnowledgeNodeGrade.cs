using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.KnowledgeGraph;

/// <summary>Grade applicability and parent belong to a version, never to a classroom.</summary>
public class KnowledgeNodeGrade : ITenantJoinEntity
{
    public Guid CenterId { get; set; }
    public Guid GraphVersionId { get; set; }
    public ulong NodeId { get; set; }
    public ulong? ParentNodeId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
}
