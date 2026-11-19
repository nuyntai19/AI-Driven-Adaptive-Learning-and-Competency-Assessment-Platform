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
    // Administrative metadata; omitted for teacher/student catalogue requests.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? ActiveClassCount { get; set; }
    public string RowVersion { get; set; } = null!;
}
