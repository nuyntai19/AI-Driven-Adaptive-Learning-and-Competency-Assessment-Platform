using System;

namespace EduTwin.Contracts.Assignments;

public class StudentAssignmentProgressDto
{
    public string Status { get; set; } = string.Empty;
    public int CompletedQuestionCount { get; set; }
    public int TotalQuestionCount { get; set; }
}
