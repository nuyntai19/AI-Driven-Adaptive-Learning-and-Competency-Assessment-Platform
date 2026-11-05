using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.KnowledgeGraph;

/// <summary>Relations and weights are scoped; an external prerequisite names its published source version explicitly.</summary>
public class KnowledgeEdgeGrade : ITenantJoinEntity
{
    public Guid CenterId { get; set; }
    public Guid GraphVersionId { get; set; }
    public ulong EdgeId { get; set; }
    public RelationType RelationType { get; set; }
    public decimal Weight { get; set; } = 1m;
    public Guid? SourceGraphVersionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
}
