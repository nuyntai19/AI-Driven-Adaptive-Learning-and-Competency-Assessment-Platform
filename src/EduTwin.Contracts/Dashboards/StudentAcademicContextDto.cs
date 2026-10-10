namespace EduTwin.Contracts.Dashboards;

public sealed class StudentAcademicContextDto
{
    public bool IsHistory { get; set; }
    public Guid? SelectedClassId { get; set; }
    public List<StudentAcademicClassDto> Classes { get; set; } = new();
    public List<StudentAppliedCurriculumDto> Curriculums { get; set; } = new();
    public string? Message { get; set; }
}
public sealed class StudentAcademicClassDto
{
    public Guid ClassId { get; set; }
    public Guid SubjectId { get; set; }
    public string ClassName { get; set; } = "";
    public byte? GradeLevel { get; set; }
    public bool IsHistorical { get; set; }
}
public sealed class StudentAppliedCurriculumDto
{
    public Guid CurriculumId { get; set; }
    public Guid ClassId { get; set; }
    public string Title { get; set; } = "";
    public string ApplicationRole { get; set; } = "";
}
