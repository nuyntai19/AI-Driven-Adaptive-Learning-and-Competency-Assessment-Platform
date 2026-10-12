namespace EduTwin.Contracts.CurriculumAndQuestions;

public class ApplyCurriculumRequest
{
    public List<Guid> ClassIds { get; set; } = new();
    public string RowVersion { get; set; } = "";
    public string ApplicationRole { get; set; } = "Primary";
    public string? ChangeReason { get; set; }
    public string? GradeMismatchReason { get; set; }
}

public class CurriculumApplicationDto
{
    public Guid ApplicationId { get; set; }
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public bool PausedByClass { get; set; }
    public string ApplicationRole { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? ChangeReason { get; set; }
    public string? EndReason { get; set; }
    public string? GradeMismatchReason { get; set; }
    public Guid AssignedBy { get; set; }
}
