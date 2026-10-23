using System;

namespace EduTwin.Contracts.KnowledgeGraph;

public class KnowledgeNodeListQuery : KnowledgeGraphQuery
{
    public string? NodeType { get; set; }
    public string? ParentNodeId { get; set; }
    public bool? IsActive { get; set; }
}
