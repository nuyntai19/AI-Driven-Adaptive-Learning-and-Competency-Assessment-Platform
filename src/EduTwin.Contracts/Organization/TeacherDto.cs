namespace EduTwin.Contracts.Organization;

public class TeacherDto
{
    public string TeacherId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ClassCount { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
