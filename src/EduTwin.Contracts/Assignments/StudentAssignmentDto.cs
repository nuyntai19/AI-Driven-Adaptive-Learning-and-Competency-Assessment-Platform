using System;

namespace EduTwin.Contracts.Assignments;

public class StudentAssignmentDto
{
    public string AssignmentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public DateTime? DueAt { get; set; }
    public string? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public StudentAssignmentProgressDto Progress { get; set; } = new();
    public AssignmentResultSummaryDto? Summary { get; set; }
}
