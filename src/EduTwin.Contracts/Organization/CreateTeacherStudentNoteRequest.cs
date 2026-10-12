using System;

namespace EduTwin.Contracts.Organization;

public class CreateTeacherStudentNoteRequest
{
    public required Guid StudentId { get; set; }
    public required string Content { get; set; }
}
