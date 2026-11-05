using System;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class CurriculumListQuery
{
    public Guid? SubjectId { get; set; }
    public string? Status { get; set; }
    public byte? GradeLevel { get; set; }
    public Guid? ClassId { get; set; }
    public string? AcademicYear { get; set; }
    public bool History { get; set; }
}
