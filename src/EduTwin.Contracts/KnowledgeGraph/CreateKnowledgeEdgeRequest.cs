using System;

namespace EduTwin.Contracts.KnowledgeGraph;

public class CreateKnowledgeEdgeRequest
{
    public Guid SubjectId { get; set; }
    public string SourceNodeId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string RelationType { get; set; } = string.Empty;
    public decimal? Weight { get; set; }
}
