namespace EduTwin.Contracts.CurriculumAndQuestions;

public sealed class CurriculumScopeConflictDto
{
    public string Message { get; set; } = "Chủ đề hoặc lớp chưa phù hợp với khối và khung tri thức đã duyệt.";
    public List<string> InvalidNodeIds { get; set; } = new();
    public List<string> InvalidClassIds { get; set; } = new();
}
