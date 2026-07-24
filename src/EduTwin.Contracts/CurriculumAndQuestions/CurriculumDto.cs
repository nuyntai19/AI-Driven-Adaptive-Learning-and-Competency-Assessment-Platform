using System.Collections.Generic;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class CurriculumDto
{
    public string CurriculumId { get; set; } = string.Empty;
    public string TeacherId { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SourceFile { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public List<string> ClassIds { get; set; } = new();
    public List<string> NodeIds { get; set; } = new();
    public string RowVersion { get; set; } = string.Empty;
}
