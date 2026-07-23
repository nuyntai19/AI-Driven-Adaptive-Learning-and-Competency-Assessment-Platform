namespace EduTwin.Contracts.KnowledgeGraph;

public class KnowledgeGraphNodeDto
{
    public string NodeId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string NodeCode { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public uint OrderIndex { get; set; }
    public decimal ExamImportance { get; set; }
}
