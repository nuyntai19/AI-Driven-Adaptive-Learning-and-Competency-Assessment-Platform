using System;

namespace EduTwin.Contracts.KnowledgeGraph;

public class UpdateKnowledgeNodeRequest
{

    public string? ParentNodeId { get; set; }
    public string? NodeName { get; set; }
    /// <summary>Optional; null keeps the current level. Same values as create (Chapter, Topic, Skill, Concept, Subject).</summary>
    public string? NodeType { get; set; }
    /// <summary>Optional; null keeps the current code. Unique per center + subject, deleted nodes included.</summary>
    public string? NodeCode { get; set; }
    public string? Description { get; set; }
    public uint OrderIndex { get; set; }
    public decimal? ExamImportance { get; set; }
    public uint EstimatedLearningMinutes { get; set; }
    public bool? IsActive { get; set; }
    public string? RowVersion { get; set; }
}
