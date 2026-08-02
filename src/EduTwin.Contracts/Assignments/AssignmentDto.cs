using System;
using System.Collections.Generic;

namespace EduTwin.Contracts.Assignments;

/// <summary>
/// Assignment DTO theo API_CONTRACTS.md §49.
/// Dùng cho cả Teacher và CenterManager view (không lộ đáp án ở đây).
/// </summary>
public class AssignmentDto
{
    public string AssignmentId { get; set; } = string.Empty;
    public string ClassId { get; set; } = string.Empty;
    public string CreatedByTeacherId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public DateTime? DueAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public int TargetStudentCount { get; set; }
    public List<AssignmentQuestionDto> Questions { get; set; } = new();
    public List<AssignmentTargetDto> Targets { get; set; } = new();
    public string RowVersion { get; set; } = string.Empty;
}
