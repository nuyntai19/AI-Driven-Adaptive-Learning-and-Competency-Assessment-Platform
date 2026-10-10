using System;
using System.Collections.Generic;

namespace EduTwin.Contracts.Assignments;

public class StudentAssignmentDetailDto
{
    public string AssignmentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public string? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public string? ClassId { get; set; }
    public bool IsReadOnly { get; set; }
    public string? ReadOnlyReason { get; set; }
    public DateTime? DueAt { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public DateTime? StartedAt { get; set; }
    public bool IsSubmitted { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? ElapsedSeconds { get; set; }
    public DateTime? EffectiveExpiresAt { get; set; }
    public int? RemainingSeconds { get; set; }
    public StudentAssignmentProgressDto Progress { get; set; } = new();
    public List<StudentQuestionDto> Questions { get; set; } = new();
    public bool CanRetake { get; set; }
    public AssignmentResultSummaryDto? Summary { get; set; }
    public List<AssignmentDraftAnswerItemDto>? DraftAnswers { get; set; }
    public int? DraftVersion { get; set; }
}
