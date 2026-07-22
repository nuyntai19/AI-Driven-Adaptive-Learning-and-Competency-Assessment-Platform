using System;

namespace EduTwin.Contracts.KnowledgeGraph;

public class UpdateKnowledgeNodeRequest
{

    public string? ParentNodeId { get; set; }
    public string? NodeName { get; set; }
    public string? Description { get; set; }
    public uint OrderIndex { get; set; }
    public decimal? ExamImportance { get; set; }
    public uint EstimatedLearningMinutes { get; set; }
    public bool? IsActive { get; set; }
    public string? RowVersion { get; set; }
}
