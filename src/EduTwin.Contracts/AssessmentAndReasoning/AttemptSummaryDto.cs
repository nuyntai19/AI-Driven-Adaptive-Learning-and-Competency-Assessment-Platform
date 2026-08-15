namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class AttemptSummaryDto
{
    public string AttemptId { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string? AssignmentId { get; set; }
    public string AttemptStatus { get; set; } = string.Empty;
    public AttemptSummaryGradingDto Grading { get; set; } = new();
    public string AnalysisJobId { get; set; } = string.Empty;
    public string JobStatus { get; set; } = string.Empty;
    public bool Terminal { get; set; }
    public string PollUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
