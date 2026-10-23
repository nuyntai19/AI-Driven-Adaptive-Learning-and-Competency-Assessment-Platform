using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.KnowledgeGraph;

/// <summary>An immutable published topology for one subject and grade. Canonical node identities survive forks.</summary>
public class KnowledgeGraphVersion : ITenantJoinEntity, IHasRowVersion
{
    public Guid GraphVersionId { get; set; }
    public Guid CenterId { get; set; }
    public Guid SubjectId { get; set; }
    public byte GradeLevel { get; set; }
    public string Status { get; set; } = "Draft";
    public bool IsCurrent { get; set; }
    public bool IsLegacy { get; set; }
    public string? Title { get; set; }
    public Guid? SourceGraphVersionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; set; }
    public ulong RowVersion { get; set; } = 1;
}
