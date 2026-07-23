using System;

namespace EduTwin.Contracts.KnowledgeGraph;

public class KnowledgeEdgeDto
{
    public string EdgeId { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string SourceNodeId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string RelationType { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
