using System;

namespace EduTwin.Contracts.KnowledgeGraph;

public class KnowledgeNodeListQuery
{
    public Guid SubjectId { get; set; }
    public string? NodeType { get; set; }
    public string? ParentNodeId { get; set; }
    public bool? IsActive { get; set; }
}
