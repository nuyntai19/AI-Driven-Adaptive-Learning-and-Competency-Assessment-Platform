using System;

namespace EduTwin.Contracts.KnowledgeGraph;

public class KnowledgeNodeDto
{
    public string NodeId { get; set; } = null!;
    public string SubjectId { get; set; } = null!;
    public string? ParentNodeId { get; set; }
    public string NodeType { get; set; } = null!;
    public string NodeCode { get; set; } = null!;
    public string NodeName { get; set; } = null!;
    public string? Description { get; set; }
    public uint OrderIndex { get; set; }
    public decimal ExamImportance { get; set; }
    public uint EstimatedLearningMinutes { get; set; }
    public bool IsActive { get; set; }
    public string RowVersion { get; set; } = null!;
}
