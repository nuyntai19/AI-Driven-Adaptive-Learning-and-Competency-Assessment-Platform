using System;

namespace EduTwin.Contracts.Organization;

public class TeacherStudentNoteDto
{
    public required string NoteId { get; set; }
    public required string TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public required string StudentId { get; set; }
    public string? StudentName { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public required string RowVersion { get; set; }
}
