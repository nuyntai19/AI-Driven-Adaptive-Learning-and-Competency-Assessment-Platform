using System;

namespace EduTwin.Contracts.KnowledgeGraph;

public class CreateKnowledgeNodeRequest
{
    public Guid SubjectId { get; set; }
    public string? ParentNodeId { get; set; }
    public string? NodeType { get; set; }
    public string? NodeCode { get; set; }
    public string? NodeName { get; set; }
    public string? Description { get; set; }
    public uint OrderIndex { get; set; }
    public decimal? ExamImportance { get; set; }
    public uint EstimatedLearningMinutes { get; set; }
    public bool? IsActive { get; set; }
}
