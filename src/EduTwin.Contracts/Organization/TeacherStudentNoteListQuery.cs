using System;

namespace EduTwin.Contracts.Organization;

public class TeacherStudentNoteListQuery
{
    public Guid? StudentId { get; set; }
    public Guid? TeacherId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
