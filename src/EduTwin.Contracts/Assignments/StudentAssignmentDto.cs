using System;

namespace EduTwin.Contracts.Assignments;

public class StudentAssignmentDto
{
    public string AssignmentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public DateTime? DueAt { get; set; }
    public StudentAssignmentProgressDto Progress { get; set; } = new();
}
