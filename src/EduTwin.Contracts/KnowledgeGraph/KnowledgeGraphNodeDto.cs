namespace EduTwin.Contracts.KnowledgeGraph;

public class KnowledgeGraphNodeDto
{
    public string NodeId { get; set; } = string.Empty;
    public string? ParentNodeId { get; set; }
    public string NodeType { get; set; } = string.Empty;
    public string NodeCode { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public uint OrderIndex { get; set; }
    public decimal ExamImportance { get; set; }
    public uint EstimatedLearningMinutes { get; set; }
    public bool IsActive { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
