using System.Collections.Generic;

namespace EduTwin.Contracts.Assignments;

public class AssignmentDraftAnswerItemDto
{
    public long QuestionId { get; set; }
    public string? FinalAnswer { get; set; }
    public string? AnswerDisplayLatex { get; set; }
    public string? ReasoningText { get; set; }
    public int? TimeSpentSeconds { get; set; }
    public int? Confidence { get; set; }
    public int? AnswerChanges { get; set; }
    public string? DrawingUploadToken { get; set; }
}

public class SaveAssignmentDraftRequest
{
    public List<AssignmentDraftAnswerItemDto> Answers { get; set; } = new();
}

public class SubmitAssignmentRequest
{
    public List<AssignmentDraftAnswerItemDto>? Answers { get; set; }
}

public class SubmitAssignmentResponseDto
{
    public string AssignmentId { get; set; } = string.Empty;
    public int SubmittedAttemptsCount { get; set; }
    public string? LastAnalysisJobId { get; set; }
    public bool IsCompleted { get; set; }
    public string Message { get; set; } = string.Empty;
}
