namespace EduTwin.Contracts.KnowledgeGraph;

public class KnowledgeGraphEdgeDto
{
    public string EdgeId { get; set; } = string.Empty;
    public string SourceNodeId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string RelationType { get; set; } = string.Empty;
    public decimal Weight { get; set; }
}
