using System;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class CurriculumListQuery
{
    public Guid? SubjectId { get; set; }
    public string? Status { get; set; }
}
