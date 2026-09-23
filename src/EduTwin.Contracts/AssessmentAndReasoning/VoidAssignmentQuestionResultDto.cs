using System;

namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class VoidAssignmentQuestionResultDto
{
    public string AssignmentId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public int VoidedAttemptsCount { get; set; }
    public int AffectedAttemptsCount => VoidedAttemptsCount;
    public bool QuestionArchived { get; set; }
    public bool QuarantinedInQuestionBank => QuestionArchived;
    public string Message { get; set; } = string.Empty;
}
