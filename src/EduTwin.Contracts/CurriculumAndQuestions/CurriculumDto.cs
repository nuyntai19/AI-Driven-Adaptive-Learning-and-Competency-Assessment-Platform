using System.Collections.Generic;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class CurriculumDto
{
    public string Visibility { get; set; } = "Private";
    public string CurriculumId { get; set; } = string.Empty;
    public string TeacherId { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SourceFile { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public byte? GradeLevel { get; set; }
    public string? GraphVersionId { get; set; }
    public List<CurriculumApplicationClassDto> ApplicationClasses { get; set; } = new();
    public List<string> ClassIds { get; set; } = new();
    public List<string> NodeIds { get; set; } = new();
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class CurriculumApplicationClassDto
{
    public string ClassId { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public byte? GradeLevel { get; set; }
    public string AcademicYear { get; set; } = string.Empty;
    public string? ApplicationRole { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsPlanned { get; set; }
}
