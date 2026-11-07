namespace EduTwin.Contracts.Organization;

public class UpsertTeacherStudentNoteRequest
{
    public required string Content { get; set; }
    public string? RowVersion { get; set; }
}
