using System;
using System.Collections.Generic;

namespace EduTwin.Contracts.Organization;

public class StudentDetailDto
{
    public string StudentId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public byte GradeLevel { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ActiveClassCount { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public required IReadOnlyList<ClassDto> Classes { get; set; }
    public required IReadOnlyList<StudentSubjectGoalDto> SubjectGoals { get; set; }
}
