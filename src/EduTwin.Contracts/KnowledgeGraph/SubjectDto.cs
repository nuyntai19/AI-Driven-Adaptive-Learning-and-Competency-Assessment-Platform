namespace EduTwin.Contracts.KnowledgeGraph;

public class SubjectDto
{
    public string SubjectId { get; set; } = null!;
    public string SubjectCode { get; set; } = null!;
    public string SubjectName { get; set; } = null!;
    // Read aliases for clients using the shorter subject labels.
    public string Name => SubjectName;
    public string Code => SubjectCode;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string RowVersion { get; set; } = null!;
}
