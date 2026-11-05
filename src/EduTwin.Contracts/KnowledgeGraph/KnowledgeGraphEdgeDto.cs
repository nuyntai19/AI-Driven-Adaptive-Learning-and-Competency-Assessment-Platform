namespace EduTwin.Contracts.KnowledgeGraph;

public class KnowledgeGraphEdgeDto
{
    public string EdgeId { get; set; } = string.Empty;
    public string SourceNodeId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string RelationType { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public string? OccurrenceId { get; set; }
    public string? GraphVersionId { get; set; }
    public byte? GradeLevel { get; set; }
    public string? CurriculumId { get; set; }
    public string? SourceOccurrenceId { get; set; }
    public string? TargetOccurrenceId { get; set; }
    public string? SourceGraphVersionId { get; set; }
    public string? SourceNodeName { get; set; }
    public byte? SourceGradeLevel { get; set; }
    public bool External { get; set; }
}
