namespace EduTwin.Contracts.Organization;

public class UpdateTeacherStudentNoteRequest
{
    public required string Content { get; set; }
    public string? RowVersion { get; set; }
}
